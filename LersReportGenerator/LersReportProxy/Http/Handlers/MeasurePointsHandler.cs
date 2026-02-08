using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportProxy.Services;

namespace LersReportProxy.Http.Handlers
{
    /// <summary>
    /// Обработчик запросов точек учёта
    /// </summary>
    public class MeasurePointsHandler
    {
        private readonly LersConnectionManager _connectionManager;

        public MeasurePointsHandler(LersConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// GET /proxy/measurepoints?type=Regular|Communal&includeReports=true
        /// Получить список точек учёта с возможностью включения отчётов.
        /// Для получения шаблонов ОДПУ лучше использовать /reports/templates (оптимизирован).
        /// </summary>
        public async Task GetListAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var query = context.Request.QueryString;
                string pointType = query["type"]; // Regular (ОДПУ), Communal (ИПУ)
                bool includeReports = query["includeReports"]?.ToLower() == "true";
                int? systemTypeId = int.TryParse(query["systemTypeId"], out var stid) ? stid : (int?)null;

                var server = session.Server;
                var serverType = server.GetType();

                // Получаем MeasurePoints через reflection
                var measurePointsProp = serverType.GetProperty("MeasurePoints");
                var measurePointsManager = measurePointsProp?.GetValue(server);
                if (measurePointsManager == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "MeasurePoints manager not found" });
                    return;
                }

                // Вызываем GetListAsync()
                var getListMethod = measurePointsManager.GetType().GetMethod("GetListAsync", Type.EmptyTypes);
                var task = getListMethod?.Invoke(measurePointsManager, null) as Task;
                if (task == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "GetListAsync method not found" });
                    return;
                }

                await task;

                // Получаем результат
                var resultProperty = task.GetType().GetProperty("Result");
                var measurePoints = resultProperty?.GetValue(task) as IEnumerable;
                if (measurePoints == null)
                {
                    await RequestRouter.SendJsonAsync(context, 200, new { measurePoints = new object[0] });
                    return;
                }

                var result = new List<object>();

                foreach (var mp in measurePoints)
                {
                    var mpType = mp.GetType();

                    // Фильтр по типу точки
                    if (!string.IsNullOrEmpty(pointType))
                    {
                        var personalAccountId = ReflectionHelper.GetPropertyValue<int?>(mp, "PersonalAccountId");
                        bool isCommunal = personalAccountId.HasValue;

                        if (pointType == "Regular" && isCommunal) continue;
                        if (pointType == "Communal" && !isCommunal) continue;
                    }

                    // Фильтр по типу ресурса
                    if (systemTypeId.HasValue)
                    {
                        var mpSystemTypeId = ReflectionHelper.GetPropertyValue<int>(mp, "SystemTypeId");
                        if (mpSystemTypeId != systemTypeId.Value) continue;
                    }

                    var mpData = new Dictionary<string, object>
                    {
                        ["id"] = ReflectionHelper.GetPropertyValue<int>(mp, "Id"),
                        ["title"] = ReflectionHelper.GetPropertyValue<string>(mp, "Title"),
                        ["fullTitle"] = ReflectionHelper.GetPropertyValue<string>(mp, "FullTitle"),
                        ["nodeId"] = ReflectionHelper.GetPropertyValue<int?>(mp, "NodeId"),
                        ["systemTypeId"] = ReflectionHelper.GetPropertyValue<int>(mp, "SystemTypeId"),
                        ["measurePointType"] = ReflectionHelper.GetPropertyValue<int?>(mp, "PersonalAccountId").HasValue ? "Communal" : "Regular"
                    };

                    // Загружаем отчёты если запрошено
                    if (includeReports)
                    {
                        var reports = await GetMeasurePointReportsAsync(mp);
                        mpData["reports"] = reports;
                    }

                    result.Add(mpData);
                }

                Logger.Info($"Возвращено {result.Count} точек учёта (type={pointType}, includeReports={includeReports})");

                await RequestRouter.SendJsonAsync(context, 200, new { measurePoints = result });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения точек учёта: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /proxy/measurepoints/{id}?includeReports=true
        /// Получить точку учёта по ID
        /// </summary>
        public async Task GetByIdAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                // Извлекаем ID из пути
                var path = context.Request.Url.AbsolutePath;
                var segments = path.Split('/');
                var idStr = segments.LastOrDefault();

                if (!int.TryParse(idStr, out int id))
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "Invalid ID" });
                    return;
                }

                bool includeReports = context.Request.QueryString["includeReports"]?.ToLower() == "true";

                var server = session.Server;
                var serverType = server.GetType();

                var measurePointsProp = serverType.GetProperty("MeasurePoints");
                var measurePointsManager = measurePointsProp?.GetValue(server);

                var getByIdMethod = measurePointsManager.GetType().GetMethod("GetByIdAsync", new[] { typeof(int) });
                var task = getByIdMethod?.Invoke(measurePointsManager, new object[] { id }) as Task;

                await task;

                var resultProperty = task.GetType().GetProperty("Result");
                var mp = resultProperty?.GetValue(task);

                if (mp == null)
                {
                    await RequestRouter.SendJsonAsync(context, 404, new { error = "MeasurePoint not found" });
                    return;
                }

                var mpData = new Dictionary<string, object>
                {
                    ["id"] = ReflectionHelper.GetPropertyValue<int>(mp, "Id"),
                    ["title"] = ReflectionHelper.GetPropertyValue<string>(mp, "Title"),
                    ["fullTitle"] = ReflectionHelper.GetPropertyValue<string>(mp, "FullTitle"),
                    ["nodeId"] = ReflectionHelper.GetPropertyValue<int?>(mp, "NodeId"),
                    ["systemTypeId"] = ReflectionHelper.GetPropertyValue<int>(mp, "SystemTypeId"),
                    ["measurePointType"] = ReflectionHelper.GetPropertyValue<int?>(mp, "PersonalAccountId").HasValue ? "Communal" : "Regular"
                };

                if (includeReports)
                {
                    var reports = await GetMeasurePointReportsAsync(mp);
                    mpData["reports"] = reports;
                }

                await RequestRouter.SendJsonAsync(context, 200, new { measurePoint = mpData });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения точки учёта: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

        private async Task<List<object>> GetMeasurePointReportsAsync(object measurePoint)
        {
            var reports = new List<object>();
            var mpTitle = ReflectionHelper.GetPropertyValue<string>(measurePoint, "Title");

            try
            {
                var mpType = measurePoint.GetType();

                // Пытаемся обновить для загрузки отчётов через RefreshAsync
                try
                {
                    var refreshMethod = mpType.GetMethod("RefreshAsync");
                    if (refreshMethod != null)
                    {
                        Logger.Debug($"[{mpTitle}] RefreshAsync найден");

                        var flagsType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }) // ReflectionTypeLoadException для некоторых сборок — норма
                            .FirstOrDefault(t => t.Name == "MeasurePointInfoFlags");

                        if (flagsType != null)
                        {
                            Logger.Debug($"[{mpTitle}] MeasurePointInfoFlags найден");
                            var reportsFlag = Enum.Parse(flagsType, "Reports");
                            var task = refreshMethod.Invoke(measurePoint, new[] { reportsFlag }) as Task;
                            if (task != null)
                            {
                                await task;
                                Logger.Debug($"[{mpTitle}] RefreshAsync выполнен");
                            }
                        }
                        else
                        {
                            Logger.Warning($"[{mpTitle}] MeasurePointInfoFlags НЕ найден");
                        }
                    }
                    else
                    {
                        Logger.Warning($"[{mpTitle}] RefreshAsync НЕ найден");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[{mpTitle}] Ошибка RefreshAsync: {ex.Message}");
                }

                // Получаем свойство Reports
                var reportsProperty = mpType.GetProperty("Reports");
                var reportsCollection = reportsProperty?.GetValue(measurePoint) as IEnumerable;

                if (reportsCollection != null)
                {
                    bool firstReport = true;
                    foreach (var report in reportsCollection)
                    {
                        // Для первого отчёта выводим все свойства
                        if (firstReport)
                        {
                            firstReport = false;
                            var props = report.GetType().GetProperties();
                            Logger.Info($"[{mpTitle}] Свойства отчёта ({report.GetType().Name}):");
                            foreach (var p in props)
                            {
                                try
                                {
                                    var val = p.GetValue(report);
                                    Logger.Info($"  {p.Name} = {val}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Debug($"  {p.Name} = <ошибка: {ex.Message}>");
                                }
                            }
                        }

                        // MeasurePointReport имеет свойства Report и ReportTemplate (объекты)
                        var reportObj = report.GetType().GetProperty("Report")?.GetValue(report);
                        var templateObj = report.GetType().GetProperty("ReportTemplate")?.GetValue(report);

                        int reportId = 0;
                        string reportTitle = "";
                        int templateId = 0;
                        string templateTitle = "";

                        if (reportObj != null)
                        {
                            reportId = ReflectionHelper.GetPropertyValue<int>(reportObj, "Id");
                            reportTitle = ReflectionHelper.GetPropertyValue<string>(reportObj, "Title") ?? reportObj.ToString();
                        }

                        if (templateObj != null)
                        {
                            templateId = ReflectionHelper.GetPropertyValue<int>(templateObj, "Id");
                            templateTitle = ReflectionHelper.GetPropertyValue<string>(templateObj, "Title") ?? templateObj.ToString();
                        }

                        Logger.Debug($"[{mpTitle}] Отчёт: ID={reportId}, Title={reportTitle}, TemplateID={templateId}");

                        reports.Add(new
                        {
                            reportId = reportId,
                            reportTitle = reportTitle,
                            reportTemplateId = templateId,
                            reportTemplateTitle = templateTitle
                        });
                    }
                    Logger.Info($"[{mpTitle}] Загружено {reports.Count} отчётов");
                }
                else
                {
                    Logger.Warning($"[{mpTitle}] Reports = null или пустой");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[{mpTitle}] Ошибка загрузки отчётов: {ex.Message}");
            }

            return reports;
        }

        /// <summary>
        /// GET /lersproxy/measurepoints/coverage
        /// Получить покрытие данными (процент точек с State != NoData)
        /// </summary>
        public async Task GetCoverageAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var server = session.Server;
                var serverType = server.GetType();

                // Получаем MeasurePoints через reflection
                var measurePointsProp = serverType.GetProperty("MeasurePoints");
                var measurePointsManager = measurePointsProp?.GetValue(server);
                if (measurePointsManager == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "MeasurePoints manager not found" });
                    return;
                }

                // Вызываем GetListAsync()
                var getListMethod = measurePointsManager.GetType().GetMethod("GetListAsync", Type.EmptyTypes);
                var task = getListMethod?.Invoke(measurePointsManager, null) as Task;
                if (task == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "GetListAsync method not found" });
                    return;
                }

                await task;

                // Получаем результат
                var resultProperty = task.GetType().GetProperty("Result");
                var measurePoints = resultProperty?.GetValue(task) as IEnumerable;
                if (measurePoints == null)
                {
                    await RequestRouter.SendJsonAsync(context, 200, new
                    {
                        success = true,
                        totalMeasurePoints = 0,
                        withData = 0,
                        coveragePercent = 0.0
                    });
                    return;
                }

                int total = 0;
                int withData = 0;

                foreach (var mp in measurePoints)
                {
                    total++;

                    // Получаем State через reflection
                    // MeasurePointState: NoData = 0, Normal = 1, Warning = 2, Error = 3, None = 4
                    var stateProp = mp.GetType().GetProperty("State");
                    if (stateProp != null)
                    {
                        var stateValue = stateProp.GetValue(mp);
                        int stateInt = Convert.ToInt32(stateValue);

                        // State != NoData означает что есть данные
                        if (stateInt != Constants.MeasurePointState.NoData)
                        {
                            withData++;
                        }
                    }
                }

                double coveragePercent = total > 0
                    ? Math.Round((double)withData / total * 100, 1)
                    : 0;

                Logger.Info($"Покрытие данными: {coveragePercent}% ({withData}/{total})");

                await RequestRouter.SendJsonAsync(context, 200, new
                {
                    success = true,
                    totalMeasurePoints = total,
                    withData = withData,
                    coveragePercent = coveragePercent
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения покрытия: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

    }
}
