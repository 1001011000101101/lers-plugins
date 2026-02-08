using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportProxy.Models;
using LersReportProxy.Services;

namespace LersReportProxy.Http.Handlers
{
    /// <summary>
    /// HTTP обработчик запросов отчётов.
    /// Делегирует бизнес-логику в TemplateService и LersTypeResolver.
    /// </summary>
    public class ReportsHandler
    {
        private readonly LersConnectionManager _connectionManager;
        private readonly TemplateService _templateService;

        public ReportsHandler(LersConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _templateService = new TemplateService();
        }

        /// <summary>
        /// GET /proxy/reports/templates?systemTypeId=1
        /// Получить уникальные шаблоны отчётов ОДПУ
        /// </summary>
        public async Task GetTemplatesAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var query = context.Request.QueryString;
                int? systemTypeId = int.TryParse(query["systemTypeId"], out var stid) ? stid : (int?)null;

                var templates = await _templateService.GetOdpuTemplatesAsync(session.Server, systemTypeId);

                Logger.Info($"Возвращено {templates.Count} шаблонов ОДПУ");

                await RequestRouter.SendJsonAsync(context, 200, new { templates = templates });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения шаблонов: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /proxy/reports/apartment-templates
        /// Получить шаблоны отчётов ИПУ (квартирные)
        /// </summary>
        public async Task GetApartmentTemplatesAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var templates = await _templateService.GetIpuTemplatesAsync(session.Server);

                Logger.Info($"Возвращено {templates.Count} шаблонов ИПУ");

                await RequestRouter.SendJsonAsync(context, 200, new { templates = templates });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения шаблонов ИПУ: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /proxy/reports/generate
        /// Генерация отчёта
        /// </summary>
        public async Task GenerateAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var request = await RequestRouter.ReadJsonBodyAsync<GenerateReportRequest>(context);

                if (request == null || request.ReportId <= 0)
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "ReportId is required" });
                    return;
                }

                if ((request.MeasurePointIds == null || request.MeasurePointIds.Length == 0) &&
                    (request.NodeIds == null || request.NodeIds.Length == 0))
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "MeasurePointIds or NodeIds required" });
                    return;
                }

                // Валидация дат
                if (request.StartDate == default || request.EndDate == default)
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "StartDate and EndDate are required" });
                    return;
                }

                if (request.StartDate > request.EndDate)
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "StartDate must be before EndDate" });
                    return;
                }

                if ((request.EndDate - request.StartDate).TotalDays > 366)
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "Date range too large (max 1 year)" });
                    return;
                }

                if (request.StartDate > DateTime.Now.AddDays(1))
                {
                    await RequestRouter.SendJsonAsync(context, 400, new { error = "StartDate cannot be in the future" });
                    return;
                }

                var reportBytes = await GenerateReportAsync(session.Server, request);

                if (reportBytes == null || reportBytes.Length == 0)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "Report generation returned empty result" });
                    return;
                }

                // Возвращаем файл
                string contentType = GetContentType(request.Format);
                string fileName = $"report_{request.ReportId}_{DateTime.Now:yyyyMMdd_HHmmss}.{(request.Format ?? "pdf").ToLower()}";

                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType;
                context.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                context.Response.ContentLength64 = reportBytes.Length;

                await context.Response.OutputStream.WriteAsync(reportBytes, 0, reportBytes.Length);
                context.Response.Close();

                Logger.Info($"Отчёт сгенерирован: {fileName}, размер: {reportBytes.Length} байт");
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                Logger.Error($"Ошибка генерации отчёта: {innerMessage}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = innerMessage });
            }
        }

        /// <summary>
        /// Таймаут генерации одного отчёта (3 минуты)
        /// </summary>
        private static readonly TimeSpan ReportGenerationTimeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// Генерация отчёта через ReportManager
        /// </summary>
        private async Task<byte[]> GenerateReportAsync(object server, GenerateReportRequest request)
        {
            // Получаем ReportManager
            var reportManagerType = LersTypeResolver.FindReportManagerType();
            if (reportManagerType == null)
                throw new InvalidOperationException("ReportManager type not found");

            var reportManager = Activator.CreateInstance(reportManagerType, server);
            if (reportManager == null)
                throw new InvalidOperationException("Failed to create ReportManager instance");

            // Ищем метод GenerateExported
            var generateMethods = reportManagerType.GetMethods()
                .Where(m => m.Name == "GenerateExported")
                .ToList();

            Logger.Debug($"Найдено {generateMethods.Count} методов GenerateExported");

            var generateMethod = generateMethods.FirstOrDefault(m => m.GetParameters().Length == 12)
                ?? generateMethods.FirstOrDefault();

            if (generateMethod == null)
                throw new InvalidOperationException("GenerateExported method not found");

            Logger.Info($"Используем GenerateExported с {generateMethod.GetParameters().Length} параметрами");

            // Подготавливаем параметры
            var exportOptions = CreateExportOptions(request.Format);
            var (reportEntity, entityIdList, nodeIdList) = PrepareEntityParameters(request);
            var reportType = GetReportTypeValue();
            var dataType = GetDataTypeValue(request.DataType);
            var reportOptions = CreateReportOptions();
            var reportParameters = CreateReportParameters();

            Logger.Info($"Генерация отчёта {request.ReportId} для {entityIdList.Length} точек, {nodeIdList.Length} узлов");

            // Создаём CancellationToken с таймаутом
            using (var cts = new CancellationTokenSource(ReportGenerationTimeout))
            {
                // Формируем аргументы динамически
                var methodParams = generateMethod.GetParameters();
                var methodArguments = BuildMethodArguments(methodParams, exportOptions, entityIdList, nodeIdList,
                    reportEntity, reportType, request.ReportId, dataType, request.StartDate, request.EndDate,
                    reportOptions, reportParameters, cts.Token);

                // Вызываем генерацию
                var taskResult = generateMethod.Invoke(reportManager, methodArguments) as Task;
                if (taskResult == null)
                    throw new InvalidOperationException("GenerateExported returned null");

                try
                {
                    await taskResult;
                }
                catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
                {
                    throw new TimeoutException($"Таймаут генерации отчёта {request.ReportId} (превышено {ReportGenerationTimeout.TotalMinutes} мин)");
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Таймаут генерации отчёта {request.ReportId} (превышено {ReportGenerationTimeout.TotalMinutes} мин)");
                }

                // Извлекаем результат
                var resultProp = taskResult.GetType().GetProperty("Result");
                var exportedReport = resultProp?.GetValue(taskResult);
                if (exportedReport == null)
                    throw new InvalidOperationException("Export result is null");

                var contentProp = exportedReport.GetType().GetProperty("Content");
                return contentProp?.GetValue(exportedReport) as byte[];
            }
        }

        private object CreateExportOptions(string format)
        {
            var exportOptionsType = LersTypeResolver.FindExportOptionsType();
            if (exportOptionsType == null)
                throw new InvalidOperationException("ReportExportOptions type not found");

            var exportOptions = Activator.CreateInstance(exportOptionsType);

            var formatProp = exportOptionsType.GetProperty("Format");
            if (formatProp != null)
            {
                var formatEnumType = formatProp.PropertyType;
                var formatValue = Enum.Parse(formatEnumType, format ?? "Pdf");
                formatProp.SetValue(exportOptions, formatValue);
            }

            return exportOptions;
        }

        private (object reportEntity, int[] entityIdList, int[] nodeIdList) PrepareEntityParameters(GenerateReportRequest request)
        {
            var reportEntityType = LersTypeResolver.FindType("ReportEntity");
            if (reportEntityType == null)
                throw new InvalidOperationException("ReportEntity type not found");

            bool isHouseReport = request.NodeIds != null && request.NodeIds.Length > 0;
            string entityTypeName = !string.IsNullOrEmpty(request.ReportEntityType)
                ? request.ReportEntityType
                : (isHouseReport ? "House" : "MeasurePoint");

            object reportEntity;
            try
            {
                reportEntity = Enum.Parse(reportEntityType, entityTypeName);
            }
            catch (ArgumentException)
            {
                Logger.Debug($"ReportEntity.{entityTypeName} не найден, используем константу");
                reportEntity = Enum.ToObject(reportEntityType,
                    isHouseReport ? Constants.ReportEntityType.House : Constants.ReportEntityType.MeasurePoint);
            }

            Logger.Info($"ReportEntity: {entityTypeName}, isHouseReport: {isHouseReport}");

            int[] entityIdList;
            int[] nodeIdList;

            if (isHouseReport)
            {
                entityIdList = request.NodeIds;
                nodeIdList = new int[0];
            }
            else
            {
                entityIdList = request.MeasurePointIds ?? new int[0];
                nodeIdList = request.NodeIds ?? new int[0];
            }

            return (reportEntity, entityIdList, nodeIdList);
        }

        private object GetReportTypeValue()
        {
            var reportTypeType = LersTypeResolver.FindReportTypeType();
            if (reportTypeType == null)
                throw new InvalidOperationException("ReportType type not found");

            try
            {
                return Enum.Parse(reportTypeType, "Common");
            }
            catch (ArgumentException)
            {
                Logger.Debug("ReportType.Common не найден, используем первое значение enum");
                return Enum.GetValues(reportTypeType).GetValue(0);
            }
        }

        private object GetDataTypeValue(string dataType)
        {
            var dataTypeType = LersTypeResolver.FindDeviceDataTypeType();
            if (dataTypeType == null)
                throw new InvalidOperationException("DeviceDataType type not found");

            return Enum.Parse(dataTypeType, dataType ?? "Day");
        }

        private object CreateReportOptions()
        {
            var reportOptionsType = LersTypeResolver.FindType("ReportOptions");
            if (reportOptionsType == null)
                throw new InvalidOperationException("ReportOptions type not found");

            return Activator.CreateInstance(reportOptionsType);
        }

        private Array CreateReportParameters()
        {
            var reportParameterType = LersTypeResolver.FindType("ReportParameter");
            if (reportParameterType != null)
                return Array.CreateInstance(reportParameterType, 0);

            return new object[0];
        }

        private object[] BuildMethodArguments(
            System.Reflection.ParameterInfo[] methodParams,
            object exportOptions, int[] entityIdList, int[] nodeIdList,
            object reportEntity, object reportType, int reportId, object dataType,
            DateTime startDate, DateTime endDate, object reportOptions, Array reportParameters,
            CancellationToken cancellationToken = default)
        {
            var methodArguments = new object[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                var paramTypeName = param.ParameterType.Name;

                if (paramTypeName == "ReportExportOptions")
                    methodArguments[i] = exportOptions;
                else if (paramTypeName == "Int32[]" && param.Name.Contains("entity"))
                    methodArguments[i] = entityIdList;
                else if (paramTypeName == "Int32[]" && param.Name.Contains("node"))
                    methodArguments[i] = nodeIdList;
                else if (paramTypeName == "ReportEntity")
                    methodArguments[i] = reportEntity;
                else if (paramTypeName == "ReportType")
                    methodArguments[i] = reportType;
                else if (param.ParameterType == typeof(int) && param.Name.Contains("report"))
                    methodArguments[i] = reportId;
                else if (paramTypeName == "DeviceDataType")
                    methodArguments[i] = dataType;
                else if (param.ParameterType == typeof(DateTime) && param.Name.Contains("start"))
                    methodArguments[i] = startDate;
                else if (param.ParameterType == typeof(DateTime) && param.Name.Contains("end"))
                    methodArguments[i] = endDate;
                else if (paramTypeName == "ReportOptions")
                    methodArguments[i] = reportOptions;
                else if (paramTypeName.Contains("ReportParameter"))
                    methodArguments[i] = reportParameters;
                else if (paramTypeName == "CancellationToken")
                    methodArguments[i] = cancellationToken;
                else
                {
                    Logger.Warning($"Неизвестный параметр [{i}] {param.Name}: {paramTypeName}");
                    methodArguments[i] = null;
                }

                Logger.Debug($"  Arg[{i}] {param.Name} ({paramTypeName}) = {methodArguments[i]?.GetType().Name ?? "null"}");
            }

            return methodArguments;
        }

        private static string GetContentType(string format)
        {
            switch (format?.ToLower())
            {
                case "pdf": return "application/pdf";
                case "xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case "xls": return "application/vnd.ms-excel";
                case "rtf": return "application/rtf";
                case "csv": return "text/csv";
                case "html": return "text/html";
                default: return "application/octet-stream";
            }
        }
    }
}
