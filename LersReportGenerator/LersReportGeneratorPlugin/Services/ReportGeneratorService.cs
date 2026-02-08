using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lers;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Сервис генерации отчётов через ЛЭРС ReportManager.
    /// Использует reflection для вызовов API, т.к. типы ЛЭРС различаются между версиями.
    /// </summary>
    public class ReportGeneratorService : IReportGeneratorService
    {
        private readonly LersServer _server;

        public ReportGeneratorService(LersServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Получить точки учёта с пагинацией и поиском
        /// </summary>
        public async Task<List<MeasurePointInfo>> GetMeasurePointsAsync(
            int offset = 0,
            int limit = 20,
            string searchQuery = null)
        {
            var measurePoints = await _server.MeasurePoints.GetListAsync();

            var query = measurePoints.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(mp =>
                    mp.Title.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query
                .Skip(offset)
                .Take(limit)
                .Select(mp => new MeasurePointInfo
                {
                    Id = mp.Id,
                    Title = mp.Title,
                    SystemTypeId = (int)mp.SystemType,
                    SystemTypeName = mp.SystemType.ToString(),
                    NodeId = mp.NodeId,
                    NodeTitle = null // Не загружаем узел, чтобы избежать проблем с API
                })
                .ToList();
        }

        /// <summary>
        /// Получить общее количество точек учёта (с поиском)
        /// </summary>
        public async Task<int> GetMeasurePointsCountAsync(string searchQuery = null)
        {
            var measurePoints = await _server.MeasurePoints.GetListAsync();

            if (string.IsNullOrWhiteSpace(searchQuery))
                return measurePoints.Count();

            return measurePoints.Count(mp =>
                mp.Title.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Получить точки учёта по типу ресурса
        /// </summary>
        public async Task<List<MeasurePointInfo>> GetMeasurePointsByResourceTypeAsync(ResourceType resourceType)
        {
            return await GetMeasurePointsByResourceTypeAsync(resourceType, null);
        }

        /// <summary>
        /// Получить точки учёта по типу ресурса и типу точки (общедомовая/квартирная)
        /// </summary>
        public async Task<List<MeasurePointInfo>> GetMeasurePointsByResourceTypeAsync(
            ResourceType resourceType,
            MeasurePointType? pointType)
        {
            var systemTypeIds = resourceType.GetSystemTypeIds();
            var measurePoints = await _server.MeasurePoints.GetListAsync();

            var result = new List<MeasurePointInfo>();

            // Счётчики для логирования
            int totalCount = 0;
            int filteredBySystemType = 0;
            int apartmentCount = 0;
            int buildingCount = 0;

            foreach (var mp in measurePoints)
            {
                totalCount++;

                if (!systemTypeIds.Contains((int)mp.SystemType))
                {
                    filteredBySystemType++;
                    continue;
                }

                // Получаем PersonalAccountId через reflection (может называться PersonalAccountId или RoomId)
                int? personalAccountId = GetPersonalAccountId(mp);
                bool isApartment = personalAccountId.HasValue;

                if (isApartment)
                    apartmentCount++;
                else
                    buildingCount++;

                // Фильтруем по типу точки
                if (pointType.HasValue)
                {
                    if (pointType.Value == MeasurePointType.Building && isApartment)
                        continue; // Пропускаем квартирные, нужны общедомовые
                    if (pointType.Value == MeasurePointType.Apartment && !isApartment)
                        continue; // Пропускаем общедомовые, нужны квартирные
                }

                result.Add(new MeasurePointInfo
                {
                    Id = mp.Id,
                    Title = mp.Title,
                    SystemTypeId = (int)mp.SystemType,
                    SystemTypeName = mp.SystemType.ToString(),
                    NodeId = mp.NodeId,
                    NodeTitle = null,
                    PersonalAccountId = personalAccountId
                });
            }

            Logger.Info($"GetMeasurePointsByResourceTypeAsync: всего={totalCount}, " +
                $"отфильтровано по SystemType={filteredBySystemType}, " +
                $"квартирных (ИПУ)={apartmentCount}, общедомовых (ОДПУ)={buildingCount}, " +
                $"запрошен тип={pointType}, результат={result.Count}");

            return result;
        }

        /// <summary>
        /// Получить PersonalAccountId из MeasurePoint через reflection
        /// </summary>
        private static int? GetPersonalAccountId(object measurePoint)
        {
            var mpType = measurePoint.GetType();

            // Пробуем разные возможные названия свойства
            var propertyNames = new[] { "PersonalAccountId", "RoomId", "PersonalAccount" };

            // Логируем один раз доступные свойства (для первой точки)
            // Используем Interlocked для потокобезопасности
            if (Interlocked.CompareExchange(ref _loggedMeasurePointProperties, 1, 0) == 0)
            {
                var allProps = mpType.GetProperties().Select(p => $"{p.Name}:{p.PropertyType.Name}").ToArray();
                Logger.Info($"MeasurePoint свойства: {string.Join(", ", allProps)}");
            }

            foreach (var propName in propertyNames)
            {
                var prop = mpType.GetProperty(propName);
                if (prop != null)
                {
                    var value = prop.GetValue(measurePoint);
                    if (value == null)
                        return null;

                    // Если это int? или int
                    if (value is int intVal)
                        return intVal == 0 ? (int?)null : intVal;

                    // Если это Nullable<int>
                    if (prop.PropertyType == typeof(int?))
                        return (int?)value;

                    // Если это объект Room - получаем Id
                    var idProp = value.GetType().GetProperty("Id");
                    if (idProp != null)
                    {
                        var idValue = idProp.GetValue(value);
                        if (idValue is int roomId && roomId > 0)
                            return roomId;
                    }
                }
            }

            return null;
        }

        // Используем int для атомарной операции Interlocked.CompareExchange (0 = не логировали, 1 = логировали)
        private static int _loggedMeasurePointProperties = 0;

        /// <summary>
        /// Получить шаблоны отчётов для конкретной точки учёта
        /// </summary>
        public async Task<List<ReportTemplateInfo>> GetReportTemplatesAsync(int measurePointId)
        {
            var measurePoint = await _server.MeasurePoints.GetByIdAsync(measurePointId);
            if (measurePoint == null)
                return new List<ReportTemplateInfo>();

            // Пытаемся обновить для загрузки отчётов через reflection (API различается между версиями)
            try
            {
                var refreshMethod = measurePoint.GetType().GetMethod("RefreshAsync");
                if (refreshMethod != null)
                {
                    // Ищем MeasurePointInfoFlags.Reports через reflection
                    var flagsType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch (ReflectionTypeLoadException ex)
                            {
                                Logger.Debug($"ReflectionTypeLoadException при загрузке типов из {a.GetName().Name}: {ex.Message}");
                                return new Type[0];
                            }
                        })
                        .FirstOrDefault(t => t.Name == "MeasurePointInfoFlags");

                    if (flagsType != null)
                    {
                        var reportsFlag = Enum.Parse(flagsType, "Reports");
                        var task = refreshMethod.Invoke(measurePoint, new[] { reportsFlag }) as Task;
                        if (task != null)
                            await task;
                    }
                }
            }
            catch (Exception ex)
            {
                // Отчёты могли быть уже загружены, но логируем для отладки
                Logger.Debug($"RefreshAsync для MeasurePoint {measurePointId}: {ex.Message}");
            }

            // Получаем свойство Reports через reflection
            var reportsProperty = measurePoint.GetType().GetProperty("Reports");
            if (reportsProperty == null)
                return new List<ReportTemplateInfo>();

            var reports = reportsProperty.GetValue(measurePoint) as IEnumerable;
            if (reports == null)
                return new List<ReportTemplateInfo>();

            var result = new List<ReportTemplateInfo>();
            int count = 0;

            foreach (var report in reports)
            {
                if (count >= 15) break; // Ограничение как в Telegram боте

                var reportType = report.GetType();

                // Получаем объект Report и извлекаем его Id
                var reportObjProp = reportType.GetProperty("Report");
                var reportObj = reportObjProp?.GetValue(report);

                int reportId = 0;
                string instanceTitle = null;

                if (reportObj != null)
                {
                    var reportObjType = reportObj.GetType();
                    var idProp = reportObjType.GetProperty("Id");
                    if (idProp != null)
                    {
                        var idValue = idProp.GetValue(reportObj);
                        reportId = idValue != null ? Convert.ToInt32(idValue) : 0;
                    }
                    // InstanceTitle = Report.ToString() или Report.Title
                    var titleProp = reportObjType.GetProperty("Title");
                    instanceTitle = titleProp?.GetValue(reportObj) as string ?? reportObj.ToString();
                }

                // Получаем объект ReportTemplate и извлекаем Id и Title
                var templateProp = reportType.GetProperty("ReportTemplate");
                var templateObj = templateProp?.GetValue(report);

                int templateId = 0;
                string templateTitle = null;

                if (templateObj != null)
                {
                    var templateObjType = templateObj.GetType();
                    var idProp = templateObjType.GetProperty("Id");
                    if (idProp != null)
                    {
                        var idValue = idProp.GetValue(templateObj);
                        templateId = idValue != null ? Convert.ToInt32(idValue) : 0;
                    }
                    var titleProp = templateObjType.GetProperty("Title");
                    templateTitle = titleProp?.GetValue(templateObj) as string ?? templateObj.ToString();
                }

                result.Add(new ReportTemplateInfo
                {
                    ReportId = reportId,
                    ReportTemplateId = templateId,
                    TemplateTitle = templateTitle ?? "Без названия",
                    InstanceTitle = instanceTitle
                });

                count++;
            }

            return result;
        }

        /// <summary>
        /// Найти шаблон отчёта для точки учёта по имени.
        /// Сначала точное совпадение по InstanceTitle (как в Telegram боте), затем частичное.
        /// </summary>
        public async Task<ReportTemplateInfo> FindReportTemplateByNameAsync(
            int measurePointId,
            string templateName)
        {
            var templates = await GetReportTemplatesAsync(measurePointId);

            // Сначала точное совпадение по InstanceTitle (как в Telegram боте)
            var result = templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.InstanceTitle) &&
                t.InstanceTitle.Equals(templateName, StringComparison.OrdinalIgnoreCase));

            if (result != null)
                return result;

            // Затем частичное совпадение по InstanceTitle
            result = templates.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.InstanceTitle) &&
                t.InstanceTitle.IndexOf(templateName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (result != null)
                return result;

            // Наконец частичное совпадение по TemplateTitle
            return templates.FirstOrDefault(t =>
                t.TemplateTitle.IndexOf(templateName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Найти шаблон отчёта для точки учёта по ReportTemplateId.
        /// Возвращает MeasurePointReport с соответствующим ReportTemplateId.
        /// </summary>
        public async Task<ReportTemplateInfo> FindReportTemplateByIdAsync(
            int measurePointId,
            int reportTemplateId)
        {
            var templates = await GetReportTemplatesAsync(measurePointId);

            return templates.FirstOrDefault(t => t.ReportTemplateId == reportTemplateId);
        }

        /// <summary>
        /// Найти MeasurePointReport для точки учёта по ReportId.
        /// </summary>
        public async Task<ReportTemplateInfo> FindReportByIdAsync(
            int measurePointId,
            int reportId)
        {
            var templates = await GetReportTemplatesAsync(measurePointId);

            return templates.FirstOrDefault(t => t.ReportId == reportId);
        }

        /// <summary>
        /// Получить агрегированные отчёты со всех точек ОДПУ.
        /// Собирает уникальные отчёты (по ReportId) со всех общедомовых точек.
        /// </summary>
        public async Task<List<ReportTemplateInfo>> GetAggregatedOdpuTemplatesAsync(ResourceType resourceType)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Получаем все ОДПУ точки (PersonalAccountId == null)
            var odpuPoints = await GetMeasurePointsByResourceTypeAsync(resourceType, MeasurePointType.Building);

            Logger.Info($"GetAggregatedOdpuTemplatesAsync: найдено {odpuPoints.Count} ОДПУ точек за {sw.ElapsedMilliseconds}мс");

            if (odpuPoints.Count == 0)
                return new List<ReportTemplateInfo>();

            // Словарь для агрегации уникальных отчётов по ReportId
            var uniqueReports = new Dictionary<int, ReportTemplateInfo>();
            int processed = 0;

            foreach (var point in odpuPoints)
            {
                processed++;
                if (processed % 10 == 0 || processed == odpuPoints.Count)
                {
                    Logger.Debug($"GetAggregatedOdpuTemplatesAsync: обработано {processed}/{odpuPoints.Count} точек, " +
                        $"найдено {uniqueReports.Count} уникальных отчётов, время={sw.ElapsedMilliseconds}мс");
                }

                var templates = await GetReportTemplatesAsync(point.Id);

                foreach (var template in templates)
                {
                    // Используем ReportId как ключ для уникальности
                    if (template.ReportId > 0 &&
                        !uniqueReports.ContainsKey(template.ReportId))
                    {
                        uniqueReports[template.ReportId] = template;
                    }
                }
            }

            sw.Stop();
            Logger.Info($"GetAggregatedOdpuTemplatesAsync: завершено за {sw.ElapsedMilliseconds}мс, " +
                $"найдено {uniqueReports.Count} уникальных отчётов из {odpuPoints.Count} точек");

            return uniqueReports.Values.ToList();
        }

        /// <summary>
        /// Сгенерировать один отчёт
        /// </summary>
        public async Task<GenerationResult> GenerateSingleReportAsync(
            MeasurePointInfo measurePoint,
            ReportTemplateInfo template,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            string outputPath,
            IProgress<string> progress = null)
        {
            var result = new GenerationResult { MeasurePoint = measurePoint };

            try
            {
                progress?.Report($"Генерация отчёта для {measurePoint.Title}...");

                var reportBytes = await GenerateReportBytesAsync(
                    measurePoint.Id,
                    template.ReportId,
                    startDate,
                    endDate,
                    format);

                // Формируем имя файла
                var fileName = FileService.SanitizeFileName(
                    $"{measurePoint.Title}_{template.TemplateTitle}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}{format.GetFileExtension()}");

                var filePath = Path.Combine(outputPath, fileName);
                File.WriteAllBytes(filePath, reportBytes);

                result.Success = true;
                result.FilePath = filePath;
                result.FileSize = reportBytes.Length;

                progress?.Report($"Сохранён: {fileName}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = GetUserFriendlyErrorMessage(ex);
                progress?.Report($"Ошибка: {result.ErrorMessage}");
            }

            return result;
        }

        /// <summary>
        /// Массовая генерация отчётов для нескольких точек учёта (поиск по имени отчёта)
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateBatchReportsAsync(
            ResourceType resourceType,
            MeasurePointType pointType,
            string templateName,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Делегируем в основной метод (поиск по имени)
            return await GenerateBatchReportsInternalAsync(
                resourceType, pointType, templateName, null,
                startDate, endDate, format, deliveryMode, outputPath,
                progress, cancellationToken);
        }

        /// <summary>
        /// Массовая генерация отчётов для ОДПУ (поиск по ReportId).
        /// Для каждой точки ищется её MeasurePointReport по ReportId.
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateBatchReportsByReportIdAsync(
            ResourceType resourceType,
            int reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Делегируем в основной метод с reportId (поиск по ID отчёта)
            return await GenerateBatchReportsInternalAsync(
                resourceType, MeasurePointType.Building, null, reportId,
                startDate, endDate, format, deliveryMode, outputPath,
                progress, cancellationToken);
        }

        /// <summary>
        /// Внутренний метод массовой генерации отчётов.
        /// Приоритет поиска: reportId -> templateName
        /// </summary>
        private async Task<BatchGenerationSummary> GenerateBatchReportsInternalAsync(
            ResourceType resourceType,
            MeasurePointType pointType,
            string templateName,
            int? reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var summary = new BatchGenerationSummary();

            // Получаем точки учёта по типу ресурса и типу точки
            var measurePoints = await GetMeasurePointsByResourceTypeAsync(resourceType, pointType);

            if (measurePoints.Count == 0)
            {
                var pointTypeName = pointType == MeasurePointType.Apartment ? "квартирных" : "общедомовых";
                progress?.Report(Tuple.Create(0, 0, $"Не найдено {pointTypeName} точек учёта для выбранного типа ресурса"));
                return summary;
            }

            summary.TotalCount = measurePoints.Count;

            // Для режима ZIP используем временную папку
            var workFolder = deliveryMode == DeliveryMode.Zip
                ? FileService.CreateTempFolder()
                : outputPath;

            var current = 0;

            foreach (var mp in measurePoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                current++;

                progress?.Report(Tuple.Create(current, summary.TotalCount, $"Генерация: {mp.Title}"));

                var result = new GenerationResult { MeasurePoint = mp };

                try
                {
                    // Ищем MeasurePointReport для этой точки учёта
                    ReportTemplateInfo template;

                    if (reportId.HasValue)
                    {
                        // Поиск по ReportId (для ОДПУ)
                        template = await FindReportByIdAsync(mp.Id, reportId.Value);
                    }
                    else
                    {
                        // Поиск по имени (для ИПУ и обратной совместимости)
                        template = await FindReportTemplateByNameAsync(mp.Id, templateName);
                    }

                    if (template == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = reportId.HasValue
                            ? $"Отчёт не настроен для этой точки"
                            : $"Отчёт '{templateName}' не найден";
                        summary.Results.Add(result);
                        summary.FailedCount++;
                        continue;
                    }

                    var reportBytes = await GenerateReportBytesAsync(
                        mp.Id,
                        template.ReportId,
                        startDate,
                        endDate,
                        format);

                    var reportTitle = template.InstanceTitle ?? template.TemplateTitle ?? "Отчёт";
                    var fileName = FileService.SanitizeFileName(
                        $"{mp.Title}_{reportTitle}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}{format.GetFileExtension()}");

                    var filePath = Path.Combine(workFolder, fileName);
                    File.WriteAllBytes(filePath, reportBytes);

                    result.Success = true;
                    result.FilePath = filePath;
                    result.FileSize = reportBytes.Length;
                    summary.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = GetUserFriendlyErrorMessage(ex);
                    summary.FailedCount++;
                }

                summary.Results.Add(result);
            }

            // Создаём ZIP-архив при необходимости
            if (deliveryMode == DeliveryMode.Zip && summary.SuccessCount > 0)
            {
                progress?.Report(Tuple.Create(summary.TotalCount, summary.TotalCount, "Создание ZIP-архива..."));

                var pointTypePrefix = pointType == MeasurePointType.Apartment ? "ИПУ" : "ОДПУ";
                var zipFileName = FileService.BuildZipFileName(pointTypePrefix, resourceType.GetDisplayName(), startDate, endDate);

                summary.ZipFilePath = FileService.CreateZipArchive(workFolder, outputPath, zipFileName);

                FileService.TryDeleteFolder(workFolder);

                progress?.Report(Tuple.Create(summary.TotalCount, summary.TotalCount, $"Архив сохранён: {zipFileName}"));
            }

            return summary;
        }

        /// <summary>
        /// Сгенерировать байты отчёта через ReportManager с использованием reflection
        /// </summary>
        /// <summary>
        /// Генерация отчёта ОДПУ (по точке учёта)
        /// </summary>
        private Task<byte[]> GenerateReportBytesAsync(
            int measurePointId,
            int reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format)
        {
            return GenerateReportBytesInternalAsync(
                entityId: measurePointId,
                reportEntityName: "MeasurePoint",
                reportEntityFallbackValue: 1,
                reportId: reportId,
                startDate: startDate,
                endDate: endDate,
                format: format);
        }

        /// <summary>
        /// Общий метод генерации отчётов через ReportManager.
        /// Используется для ОДПУ (MeasurePoint) и ИПУ (House).
        /// </summary>
        /// <param name="entityId">ID сущности (MeasurePointId для ОДПУ, NodeId для ИПУ)</param>
        /// <param name="reportEntityName">Название ReportEntity: "MeasurePoint" или "House"</param>
        /// <param name="reportEntityFallbackValue">Числовое значение ReportEntity если Enum.Parse не сработает</param>
        private async Task<byte[]> GenerateReportBytesInternalAsync(
            int entityId,
            string reportEntityName,
            int reportEntityFallbackValue,
            int reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format)
        {
            // Ищем тип ReportManager
            var reportManagerType = ReflectionHelper.FindType("ReportManager");
            if (reportManagerType == null)
                throw new InvalidOperationException("ReportManager type not found");

            // Создаём экземпляр ReportManager
            var reportManager = Activator.CreateInstance(reportManagerType, _server);
            if (reportManager == null)
                throw new InvalidOperationException("Failed to create ReportManager");

            // Создаём ReportExportOptions
            var exportOptionsType = ReflectionHelper.FindType("ReportExportOptions");
            if (exportOptionsType == null)
                throw new InvalidOperationException("ReportExportOptions type not found");

            var exportOptions = Activator.CreateInstance(exportOptionsType);
            if (exportOptions == null)
                throw new InvalidOperationException("Failed to create ReportExportOptions");

            // Устанавливаем формат экспорта
            var formatProp = exportOptionsType.GetProperty("Format");
            if (formatProp != null)
            {
                var formatEnumType = formatProp.PropertyType;
                var formatValue = Enum.ToObject(formatEnumType, (int)format);
                formatProp.SetValue(exportOptions, formatValue);
            }

            // Ищем необходимые enum-ы
            var reportEntityType = ReflectionHelper.FindType("ReportEntity");
            var reportTypeType = ReflectionHelper.FindType("ReportType");
            var deviceDataTypeType = ReflectionHelper.FindType("DeviceDataType");

            if (reportEntityType == null || reportTypeType == null || deviceDataTypeType == null)
                throw new InvalidOperationException("Required enum types not found");

            // Получаем ReportEntity с fallback на числовое значение
            object reportEntity;
            try
            {
                reportEntity = Enum.Parse(reportEntityType, reportEntityName);
            }
            catch (ArgumentException)
            {
                Logger.Debug($"ReportEntity.{reportEntityName} не найден, используем значение {reportEntityFallbackValue}");
                reportEntity = Enum.ToObject(reportEntityType, reportEntityFallbackValue);
            }

            // Пробуем Common, Standard или первое доступное значение для ReportType
            object reportTypeValue;
            try
            {
                reportTypeValue = Enum.Parse(reportTypeType, "Common");
            }
            catch (ArgumentException)
            {
                Logger.Debug("ReportType.Common не найден, пробуем Standard");
                try
                {
                    reportTypeValue = Enum.Parse(reportTypeType, "Standard");
                }
                catch (ArgumentException)
                {
                    Logger.Debug("ReportType.Standard не найден, используем первое значение");
                    reportTypeValue = Enum.GetValues(reportTypeType).GetValue(0);
                }
            }

            var dataTypeValue = Enum.Parse(deviceDataTypeType, "Day");

            // Создаём ReportOptions при необходимости
            var reportOptionsType = ReflectionHelper.FindType("ReportOptions");
            object reportOptions = null;
            if (reportOptionsType != null)
            {
                reportOptions = Activator.CreateInstance(reportOptionsType);
            }

            // Создаём пустой массив ReportParameter
            var reportParameterType = ReflectionHelper.FindType("ReportParameter");
            Array reportParameters;
            if (reportParameterType != null)
            {
                reportParameters = Array.CreateInstance(reportParameterType, 0);
            }
            else
            {
                reportParameters = new object[0];
            }

            // Ищем метод GenerateExported
            var generateMethod = reportManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GenerateExported");

            if (generateMethod == null)
                throw new InvalidOperationException("GenerateExported method not found");

            // Подготавливаем аргументы
            var entityIdList = new[] { entityId };
            var nodeIdList = new int[0];

            var args = new object[]
            {
                exportOptions,
                entityIdList,
                nodeIdList,
                reportEntity,
                reportTypeValue,
                reportId,
                dataTypeValue,
                startDate,
                endDate,
                reportOptions,
                reportParameters,
                CancellationToken.None
            };

            // Вызываем и ожидаем
            var task = generateMethod.Invoke(reportManager, args) as Task;
            if (task == null)
                throw new InvalidOperationException("GenerateExported did not return a Task");

            await task;

            // Получаем результат
            var resultProp = task.GetType().GetProperty("Result");
            var exportedReport = resultProp?.GetValue(task);
            if (exportedReport == null)
                throw new InvalidOperationException("Export result is null");

            // Получаем Content
            var contentProp = exportedReport.GetType().GetProperty("Content");
            var content = contentProp?.GetValue(exportedReport) as byte[];

            return content ?? throw new InvalidOperationException("Export content is null");
        }

        /// <summary>
        /// Преобразовать исключение в понятное пользователю сообщение об ошибке
        /// </summary>
        private static string GetUserFriendlyErrorMessage(Exception ex)
        {
            var message = ex.Message;

            // Проверяем известные ошибки ЛЭРС
            if (message.Contains("не задана отчетная форма") ||
                message.Contains("Объект учета удален или не существует"))
            {
                return "Для точки учёта не настроен объект учёта (энергоснабжающая организация)";
            }

            if (message.IndexOf("нет данных", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("no data", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Нет данных за выбранный период";
            }

            return message;
        }

        #region Методы для узлов/домов (отчёты по квартирам)

        /// <summary>
        /// Получить узлы (дома) типа House для квартирных отчётов
        /// </summary>
        public async Task<List<NodeInfo>> GetNodesWithApartmentsAsync()
        {
            var result = new List<NodeInfo>();
            var nodes = await _server.Nodes.GetListAsync();

            foreach (var node in nodes)
            {
                // Проверяем, что узел типа House
                var nodeType = node.GetType();
                var typeProp = nodeType.GetProperty("Type");
                if (typeProp != null)
                {
                    var typeValue = typeProp.GetValue(node);
                    if (typeValue?.ToString() != "House")
                        continue; // Пропускаем узлы не типа House
                }

                result.Add(new NodeInfo
                {
                    Id = node.Id,
                    Title = node.Title,
                    Address = GetNodeAddress(node),
                    ApartmentPointsCount = 0, // Не считаем, т.к. это дом
                    BuildingPointsCount = 0
                });
            }

            return result;
        }

        /// <summary>
        /// Получить адрес узла через reflection
        /// </summary>
        private static string GetNodeAddress(object node)
        {
            var prop = node.GetType().GetProperty("Address");
            return prop?.GetValue(node) as string;
        }

        /// <summary>
        /// Получить отчёты для квартирных точек (ReportType = CommunalMeasurePointSummary, ReportEntity = House).
        /// Использует ReportManager.GetReportListAsync() для получения Report (не ReportTemplate).
        /// </summary>
        public async Task<List<ReportTemplateInfo>> GetApartmentReportTemplatesAsync()
        {
            var result = new List<ReportTemplateInfo>();

            try
            {
                // Создаём ReportManager
                var reportManagerType = ReflectionHelper.FindType("ReportManager");
                if (reportManagerType == null)
                    return result;

                var reportManager = Activator.CreateInstance(reportManagerType, _server);
                if (reportManager == null)
                    return result;

                // Ищем enum-ы
                var reportTypeType = ReflectionHelper.FindType("ReportType");
                var reportEntityType = ReflectionHelper.FindType("ReportEntity");

                if (reportTypeType == null || reportEntityType == null)
                    return result;

                // ReportType.CommunalMeasurePointSummary = 7
                var communalType = Enum.Parse(reportTypeType, "CommunalMeasurePointSummary");
                // ReportEntity.House = 4
                var houseEntity = Enum.Parse(reportEntityType, "House");

                // Вызываем GetReportListAsync(ReportType, ReportEntity) - получаем Report, а не ReportTemplate!
                var getReportsMethod = reportManagerType.GetMethod(
                    "GetReportListAsync",
                    new[] { reportTypeType, reportEntityType });

                if (getReportsMethod == null)
                {
                    // Пробуем без параметров
                    getReportsMethod = reportManagerType.GetMethod(
                        "GetReportListAsync",
                        Type.EmptyTypes);

                    if (getReportsMethod == null)
                        return result;

                    var task = getReportsMethod.Invoke(reportManager, null) as Task;
                    if (task == null)
                        return result;

                    await task;

                    var resultProp = task.GetType().GetProperty("Result");
                    var reports = resultProp?.GetValue(task) as IEnumerable;
                    if (reports == null)
                        return result;

                    // Фильтруем вручную по ReportType = 7
                    foreach (var report in reports)
                    {
                        var repType = report.GetType();

                        var typeProp = repType.GetProperty("Type");
                        if (typeProp != null)
                        {
                            var typeValue = typeProp.GetValue(report);
                            int typeInt = Convert.ToInt32(typeValue);
                            if (typeInt != 7) // CommunalMeasurePointSummary
                                continue;
                        }

                        var idProp = repType.GetProperty("Id");
                        int reportId = idProp != null ? Convert.ToInt32(idProp.GetValue(report)) : 0;

                        var titleProp = repType.GetProperty("Title");
                        string title = titleProp?.GetValue(report) as string ?? report.ToString();

                        // Получаем ReportTemplateId если есть
                        int templateId = reportId;
                        var templateIdProp = repType.GetProperty("ReportTemplateId");
                        if (templateIdProp != null)
                        {
                            var tplIdValue = templateIdProp.GetValue(report);
                            if (tplIdValue != null)
                                templateId = Convert.ToInt32(tplIdValue);
                        }

                        result.Add(new ReportTemplateInfo
                        {
                            ReportId = reportId,           // ID отчёта (Report) - для генерации
                            ReportTemplateId = templateId, // ID шаблона (ReportTemplate)
                            TemplateTitle = title,
                            InstanceTitle = title
                        });
                    }
                }
                else
                {
                    // Вызываем с параметрами (ReportType, ReportEntity)
                    var task = getReportsMethod.Invoke(reportManager, new[] { communalType, houseEntity }) as Task;
                    if (task == null)
                        return result;

                    await task;

                    var resultProp = task.GetType().GetProperty("Result");
                    var reports = resultProp?.GetValue(task) as IEnumerable;
                    if (reports == null)
                        return result;

                    foreach (var report in reports)
                    {
                        var repType = report.GetType();

                        var idProp = repType.GetProperty("Id");
                        int reportId = idProp != null ? Convert.ToInt32(idProp.GetValue(report)) : 0;

                        var titleProp = repType.GetProperty("Title");
                        string title = titleProp?.GetValue(report) as string ?? report.ToString();

                        // Получаем ReportTemplateId если есть
                        int templateId = reportId;
                        var templateIdProp = repType.GetProperty("ReportTemplateId");
                        if (templateIdProp != null)
                        {
                            var tplIdValue = templateIdProp.GetValue(report);
                            if (tplIdValue != null)
                                templateId = Convert.ToInt32(tplIdValue);
                        }

                        result.Add(new ReportTemplateInfo
                        {
                            ReportId = reportId,           // ID отчёта (Report) - для генерации
                            ReportTemplateId = templateId, // ID шаблона (ReportTemplate)
                            TemplateTitle = title,
                            InstanceTitle = title
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка при получении шаблонов отчётов для квартир", ex);
            }

            return result;
        }


        /// <summary>
        /// Массовая генерация отчётов для узлов (домов) - для квартирных отчётов.
        /// Использует ReportEntity=House (4) вместо MeasurePoint (1).
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateBatchHouseReportsAsync(
            int reportId,
            string reportTitle,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var summary = new BatchGenerationSummary();

            // Получаем дома (узлы типа House)
            var nodes = await GetNodesWithApartmentsAsync();

            if (nodes.Count == 0)
            {
                progress?.Report(Tuple.Create(0, 0, "Не найдено объектов учёта с квартирными точками"));
                return summary;
            }

            summary.TotalCount = nodes.Count;

            // Для режима ZIP используем временную папку
            var workFolder = deliveryMode == DeliveryMode.Zip
                ? FileService.CreateTempFolder()
                : outputPath;

            var current = 0;

            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                current++;

                progress?.Report(Tuple.Create(current, summary.TotalCount, $"Генерация: {node.Title}"));

                var result = new GenerationResult
                {
                    MeasurePoint = new MeasurePointInfo
                    {
                        Id = node.Id,
                        Title = node.Title,
                        NodeId = node.Id
                    }
                };

                try
                {
                    var reportBytes = await GenerateHouseReportBytesAsync(
                        node.Id,
                        reportId,
                        startDate,
                        endDate,
                        format);

                    var fileName = FileService.SanitizeFileName(
                        $"{node.Title}_{reportTitle}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}{format.GetFileExtension()}");

                    var filePath = Path.Combine(workFolder, fileName);
                    File.WriteAllBytes(filePath, reportBytes);

                    result.Success = true;
                    result.FilePath = filePath;
                    result.FileSize = reportBytes.Length;
                    summary.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = GetUserFriendlyErrorMessage(ex);
                    summary.FailedCount++;
                }

                summary.Results.Add(result);
            }

            // Создаём ZIP-архив при необходимости
            if (deliveryMode == DeliveryMode.Zip && summary.SuccessCount > 0)
            {
                progress?.Report(Tuple.Create(summary.TotalCount, summary.TotalCount, "Создание ZIP-архива..."));

                var zipFileName = FileService.BuildZipFileName("ИПУ", reportTitle, startDate, endDate);

                summary.ZipFilePath = FileService.CreateZipArchive(workFolder, outputPath, zipFileName);

                FileService.TryDeleteFolder(workFolder);

                progress?.Report(Tuple.Create(summary.TotalCount, summary.TotalCount, $"Архив сохранён: {zipFileName}"));
            }

            return summary;
        }

        /// <summary>
        /// Сгенерировать байты отчёта по дому через ReportManager с ReportEntity=House
        /// </summary>
        /// <summary>
        /// Генерация отчёта ИПУ (по узлу/дому)
        /// </summary>
        private Task<byte[]> GenerateHouseReportBytesAsync(
            int nodeId,
            int reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format)
        {
            return GenerateReportBytesInternalAsync(
                entityId: nodeId,
                reportEntityName: "House",
                reportEntityFallbackValue: 4,
                reportId: reportId,
                startDate: startDate,
                endDate: endDate,
                format: format);
        }

        #endregion

        public void Dispose()
        {
            // Пока нечего освобождать
        }
    }
}
