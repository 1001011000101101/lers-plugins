using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LersReportCommon;

namespace LersReportProxy.Services
{
    /// <summary>
    /// Сервис для получения шаблонов отчётов ЛЭРС
    /// </summary>
    public class TemplateService
    {
        // Флаг для однократного логирования свойств MeasurePoint
        private static bool _loggedMeasurePointProperties = false;
        private static readonly object _logLock = new object();

        /// <summary>
        /// Получить уникальные шаблоны отчётов ОДПУ (общедомовые приборы учёта).
        /// Алгоритм аналогичен локальному плагину: проходим все точки, собираем уникальные шаблоны.
        /// </summary>
        /// <param name="server">Объект LersServer</param>
        /// <param name="systemTypeId">Фильтр по типу ресурса (опционально)</param>
        public async Task<List<object>> GetOdpuTemplatesAsync(object server, int? systemTypeId)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var uniqueTemplates = new Dictionary<int, object>();
            var serverType = server.GetType();

            // Получаем точки
            Logger.Info("GetOdpuTemplatesAsync: загружаем список точек...");
            var measurePointsProp = serverType.GetProperty("MeasurePoints");
            var measurePointsManager = measurePointsProp?.GetValue(server);

            var getListMethod = measurePointsManager.GetType().GetMethod("GetListAsync", Type.EmptyTypes);
            var task = getListMethod?.Invoke(measurePointsManager, null) as Task;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            var measurePoints = resultProperty?.GetValue(task) as IEnumerable;

            // Сначала собираем статистику и фильтруем точки
            var allPoints = new List<object>();
            int totalCount = 0;
            int ipuCount = 0;
            int odpuCount = 0;
            int filteredBySystemType = 0;

            foreach (var mp in measurePoints)
            {
                totalCount++;

                // Логируем свойства первой точки для диагностики
                LogMeasurePointPropertiesOnce(mp);

                // Проверяем является ли точка квартирной (ИПУ)
                bool isApartment = IsApartmentMeasurePoint(mp);

                if (isApartment)
                {
                    ipuCount++;
                    continue; // Пропускаем ИПУ
                }

                odpuCount++;

                // Фильтр по типу ресурса
                if (systemTypeId.HasValue)
                {
                    var mpSystemTypeId = GetSystemTypeId(mp);
                    if (mpSystemTypeId != systemTypeId.Value)
                    {
                        filteredBySystemType++;
                        continue;
                    }
                }

                allPoints.Add(mp);
            }

            Logger.Info($"GetOdpuTemplatesAsync: всего={totalCount}, ИПУ={ipuCount}, ОДПУ={odpuCount}, " +
                $"отфильтровано по SystemType={filteredBySystemType}, к обработке={allPoints.Count}, " +
                $"время загрузки списка={sw.ElapsedMilliseconds}мс");

            if (allPoints.Count == 0)
            {
                Logger.Info($"GetOdpuTemplatesAsync: нет ОДПУ точек для обработки");
                return uniqueTemplates.Values.ToList();
            }

            // Теперь обрабатываем только ОДПУ точки
            int processedCount = 0;
            foreach (var mp in allPoints)
            {
                // Загружаем отчёты
                await RefreshReportsAsync(mp);
                processedCount++;

                var reportsProperty = mp.GetType().GetProperty("Reports");
                var reports = reportsProperty?.GetValue(mp) as IEnumerable;

                if (reports != null)
                {
                    foreach (var report in reports)
                    {
                        int reportId = ReflectionHelper.GetPropertyValue<int>(report, "Id");
                        if (reportId > 0 && !uniqueTemplates.ContainsKey(reportId))
                        {
                            uniqueTemplates[reportId] = new
                            {
                                reportId = reportId,
                                title = ReflectionHelper.GetPropertyValue<string>(report, "InstanceTitle") ?? ReflectionHelper.GetPropertyValue<string>(report, "Title"),
                                templateTitle = ReflectionHelper.GetPropertyValue<string>(report, "Title")
                            };
                        }
                    }
                }

                // Логируем прогресс каждые 10 точек
                if (processedCount % 10 == 0)
                {
                    Logger.Debug($"GetOdpuTemplatesAsync: обработано {processedCount}/{allPoints.Count} точек, " +
                        $"найдено {uniqueTemplates.Count} шаблонов, время={sw.ElapsedMilliseconds}мс");
                }
            }

            sw.Stop();
            Logger.Info($"GetOdpuTemplatesAsync: обработано {processedCount} точек, найдено {uniqueTemplates.Count} шаблонов, " +
                $"общее время={sw.ElapsedMilliseconds}мс");
            return uniqueTemplates.Values.ToList();
        }

        /// <summary>
        /// Логирует свойства MeasurePoint один раз для диагностики
        /// </summary>
        private void LogMeasurePointPropertiesOnce(object mp)
        {
            if (_loggedMeasurePointProperties) return;

            lock (_logLock)
            {
                if (_loggedMeasurePointProperties) return;
                _loggedMeasurePointProperties = true;

                try
                {
                    var props = mp.GetType().GetProperties()
                        .Select(p => $"{p.Name}:{p.PropertyType.Name}")
                        .ToArray();
                    Logger.Info($"MeasurePoint свойства: {string.Join(", ", props)}");
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Ошибка логирования свойств MeasurePoint: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Проверяет является ли точка квартирной (ИПУ).
        /// Использует несколько возможных названий свойств для совместимости с разными версиями ЛЭРС.
        /// </summary>
        private bool IsApartmentMeasurePoint(object mp)
        {
            var mpType = mp.GetType();

            // Пробуем разные возможные названия свойства (как в локальном плагине)
            var propertyNames = new[] { "PersonalAccountId", "RoomId", "PersonalAccount" };

            foreach (var propName in propertyNames)
            {
                var prop = mpType.GetProperty(propName);
                if (prop == null) continue;

                try
                {
                    var value = prop.GetValue(mp);
                    if (value == null) continue;

                    // Если это int или int?
                    if (value is int intVal && intVal > 0)
                        return true;

                    // Если это объект (например Room) - проверяем Id
                    var idProp = value.GetType().GetProperty("Id");
                    if (idProp != null)
                    {
                        var idValue = idProp.GetValue(value);
                        if (idValue is int roomId && roomId > 0)
                            return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"IsApartmentMeasurePoint: ошибка чтения {propName}: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Получает SystemTypeId из точки учёта
        /// </summary>
        private int GetSystemTypeId(object mp)
        {
            // Пробуем разные возможные названия
            var value = ReflectionHelper.GetPropertyValue<int>(mp, "SystemTypeId");
            if (value != 0) return value;

            // Пробуем SystemType (enum)
            var systemTypeProp = mp.GetType().GetProperty("SystemType");
            if (systemTypeProp != null)
            {
                var enumValue = systemTypeProp.GetValue(mp);
                if (enumValue != null)
                    return Convert.ToInt32(enumValue);
            }

            return 0;
        }

        /// <summary>
        /// Получить шаблоны отчётов ИПУ (индивидуальные приборы учёта)
        /// </summary>
        /// <param name="server">Объект LersServer</param>
        public async Task<List<object>> GetIpuTemplatesAsync(object server)
        {
            var templates = new List<object>();

            try
            {
                Logger.Info("GetIpuTemplatesAsync: начинаем поиск шаблонов ИПУ");

                // Создаём ReportManager
                var reportManagerType = LersTypeResolver.FindReportManagerType();
                if (reportManagerType == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: ReportManager type не найден");
                    return templates;
                }
                Logger.Info($"GetIpuTemplatesAsync: ReportManager найден: {reportManagerType.FullName}");

                var reportManager = Activator.CreateInstance(reportManagerType, server);
                if (reportManager == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: не удалось создать экземпляр ReportManager");
                    return templates;
                }
                Logger.Info("GetIpuTemplatesAsync: экземпляр ReportManager создан");

                // Ищем enum-ы
                var reportTypeType = LersTypeResolver.FindReportTypeType();
                var reportEntityType = LersTypeResolver.FindType("ReportEntity");

                if (reportTypeType == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: ReportType type не найден");
                    return templates;
                }

                // Логируем значения ReportType
                var reportTypeValues = Enum.GetNames(reportTypeType);
                Logger.Info($"GetIpuTemplatesAsync: значения ReportType: {string.Join(", ", reportTypeValues)}");

                // ReportType.CommunalMeasurePointSummary = 7
                object communalType;
                try
                {
                    communalType = Enum.Parse(reportTypeType, "CommunalMeasurePointSummary");
                    Logger.Info($"GetIpuTemplatesAsync: CommunalMeasurePointSummary = {communalType}");
                }
                catch (ArgumentException)
                {
                    Logger.Warning("GetIpuTemplatesAsync: CommunalMeasurePointSummary не найден в ReportType enum");
                    return templates;
                }

                // Пробуем вызвать GetReportListAsync с двумя параметрами (ReportType, ReportEntity)
                MethodInfo getReportsMethod = null;
                object[] methodArgs = null;

                if (reportEntityType != null)
                {
                    // Логируем значения ReportEntity
                    var entityValues = Enum.GetNames(reportEntityType);
                    Logger.Info($"GetIpuTemplatesAsync: значения ReportEntity: {string.Join(", ", entityValues)}");

                    // ReportEntity.House = 4
                    object houseEntity;
                    try
                    {
                        houseEntity = Enum.Parse(reportEntityType, "House");
                        Logger.Info($"GetIpuTemplatesAsync: House = {houseEntity}");
                    }
                    catch (ArgumentException)
                    {
                        houseEntity = null;
                        Logger.Warning("GetIpuTemplatesAsync: House не найден в ReportEntity enum");
                    }

                    if (houseEntity != null)
                    {
                        getReportsMethod = reportManagerType.GetMethod(
                            "GetReportListAsync",
                            new[] { reportTypeType, reportEntityType });

                        if (getReportsMethod != null)
                        {
                            Logger.Info("GetIpuTemplatesAsync: найден GetReportListAsync(ReportType, ReportEntity)");
                            methodArgs = new[] { communalType, houseEntity };
                        }
                    }
                }

                // Если не нашли метод с двумя параметрами, пробуем без параметров
                if (getReportsMethod == null)
                {
                    getReportsMethod = reportManagerType.GetMethod("GetReportListAsync", Type.EmptyTypes);
                    if (getReportsMethod != null)
                    {
                        Logger.Info("GetIpuTemplatesAsync: найден GetReportListAsync() без параметров, будем фильтровать вручную");
                        methodArgs = null;
                    }
                }

                // Логируем все методы ReportManager для диагностики
                var methods = reportManagerType.GetMethods()
                    .Where(m => m.Name.Contains("GetReport"))
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .ToList();
                Logger.Info($"GetIpuTemplatesAsync: методы GetReport*: {string.Join("; ", methods)}");

                if (getReportsMethod == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: метод GetReportListAsync не найден");
                    return templates;
                }

                // Вызываем метод
                Logger.Info("GetIpuTemplatesAsync: вызываем GetReportListAsync...");
                var task = getReportsMethod.Invoke(reportManager, methodArgs) as Task;
                if (task == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: GetReportListAsync вернул null");
                    return templates;
                }

                await task;
                Logger.Info("GetIpuTemplatesAsync: GetReportListAsync завершён");

                var resultProp = task.GetType().GetProperty("Result");
                var reports = resultProp?.GetValue(task) as IEnumerable;

                if (reports == null)
                {
                    Logger.Warning("GetIpuTemplatesAsync: результат GetReportListAsync = null");
                    return templates;
                }

                // Фильтруем вручную по Type = 7 (если вызывали без параметров)
                bool needFilter = methodArgs == null;

                int count = 0;
                foreach (var report in reports)
                {
                    var repType = report.GetType();

                    // Если вызывали без параметров, фильтруем по Type = 7
                    if (needFilter)
                    {
                        var typeProp = repType.GetProperty("Type");
                        if (typeProp != null)
                        {
                            var typeValue = typeProp.GetValue(report);
                            int typeInt = Convert.ToInt32(typeValue);
                            if (typeInt != Constants.ReportType.CommunalMeasurePointSummary)
                                continue;
                        }
                    }

                    count++;
                    int reportId = ReflectionHelper.GetPropertyValue<int>(report, "Id");
                    string title = ReflectionHelper.GetPropertyValue<string>(report, "Title") ?? report.ToString();

                    // Получаем ReportTemplateId если есть
                    int templateId = reportId;
                    var templateIdProp = repType.GetProperty("ReportTemplateId");
                    if (templateIdProp != null)
                    {
                        var tplIdValue = templateIdProp.GetValue(report);
                        if (tplIdValue != null)
                            templateId = Convert.ToInt32(tplIdValue);
                    }

                    Logger.Info($"GetIpuTemplatesAsync: отчёт [{count}]: Id={reportId}, TemplateId={templateId}, Title={title}");

                    templates.Add(new
                    {
                        reportId = reportId,
                        reportTemplateId = templateId,
                        title = title,
                        reportType = "CommunalMeasurePointSummary"
                    });
                }

                Logger.Info($"GetIpuTemplatesAsync: всего найдено {count} отчётов ИПУ");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Ошибка получения шаблонов ИПУ: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.Warning($"Inner exception: {ex.InnerException.Message}");
                }
                Logger.Warning($"Stack trace: {ex.StackTrace}");
            }

            Logger.Info($"GetIpuTemplatesAsync: возвращаем {templates.Count} шаблонов");
            return templates;
        }

        /// <summary>
        /// Обновить данные отчётов для точки учёта
        /// </summary>
        private async Task RefreshReportsAsync(object measurePoint)
        {
            try
            {
                var refreshMethod = measurePoint.GetType().GetMethod("RefreshAsync");
                if (refreshMethod != null)
                {
                    var flagsType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch (ReflectionTypeLoadException)
                            {
                                // ReflectionTypeLoadException — норма при загрузке сборок с отсутствующими зависимостями
                                return new Type[0];
                            }
                        })
                        .FirstOrDefault(t => t.Name == "MeasurePointInfoFlags");

                    if (flagsType != null)
                    {
                        var reportsFlag = Enum.Parse(flagsType, "Reports");
                        var task = refreshMethod.Invoke(measurePoint, new[] { reportsFlag }) as Task;
                        if (task != null) await task;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"RefreshReportsAsync: {ex.Message}");
            }
        }
    }
}
