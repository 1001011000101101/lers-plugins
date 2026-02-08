using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// HTTP клиент для работы с LERS Report Proxy службой.
    /// Используется вместо LersApiClient для удалённых серверов с установленной прокси-службой.
    /// </summary>
    public class LersProxyClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JavaScriptSerializer _serializer;
        private readonly ServerConfig _server;
        private string _token;
        private bool _disposed;

        /// <summary>
        /// Создаёт клиент для указанного сервера
        /// </summary>
        public LersProxyClient(ServerConfig server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _serializer = new JavaScriptSerializer();

            var handler = new HttpClientHandler();

            if (server.IgnoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(GetProxyBaseUrl(server)),
                Timeout = TimeSpan.FromMinutes(10)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Авторизован ли клиент
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

        /// <summary>
        /// Авторизация
        /// </summary>
        public async Task<ProxyLoginResult> LoginAsync(string login, string password)
        {
            try
            {
                var request = new { login = login, password = password };
                var json = _serializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("lersproxy/login", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = _serializer.Deserialize<ProxyLoginResponse>(responseJson);
                    if (result.success && !string.IsNullOrEmpty(result.token))
                    {
                        _token = result.token;
                        SetAuthToken(_token);
                        Logger.Info($"[Proxy] Авторизация успешна: {_server.Name}");
                        return new ProxyLoginResult { Success = true };
                    }
                }

                var error = TryParseError(responseJson);
                Logger.Error($"[Proxy] Ошибка авторизации: {error}");
                return new ProxyLoginResult { Success = false, ErrorMessage = error };
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Исключение при авторизации: {ex.Message}");
                return new ProxyLoginResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Получить список точек учёта.
        /// Для получения шаблонов ОДПУ лучше использовать GetOdpuTemplatesAsync (оптимизирован).
        /// </summary>
        /// <param name="type">Тип точки: Regular (ОДПУ), Communal (ИПУ)</param>
        /// <param name="includeReports">Включить отчёты в ответ (медленно для большого кол-ва точек!)</param>
        /// <param name="systemTypeId">Фильтр по типу ресурса</param>
        public async Task<List<ProxyMeasurePointDto>> GetMeasurePointsAsync(
            string type = null,
            bool includeReports = false,
            int? systemTypeId = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(type))
                    queryParams.Add($"type={type}");
                if (includeReports)
                    queryParams.Add("includeReports=true");
                if (systemTypeId.HasValue)
                    queryParams.Add($"systemTypeId={systemTypeId.Value}");

                string url = "lersproxy/measurepoints";
                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = _serializer.Deserialize<ProxyMeasurePointListResponse>(json);

                Logger.Info($"[Proxy] Загружено {result?.measurePoints?.Count ?? 0} точек учёта");
                return result?.measurePoints ?? new List<ProxyMeasurePointDto>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Ошибка получения точек: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить список узлов (домов)
        /// </summary>
        public async Task<List<ProxyNodeDto>> GetNodesAsync(string type = null)
        {
            try
            {
                string url = "lersproxy/nodes";
                if (!string.IsNullOrEmpty(type))
                    url += $"?type={type}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = _serializer.Deserialize<ProxyNodeListResponse>(json);

                Logger.Info($"[Proxy] Загружено {result?.nodes?.Count ?? 0} узлов");
                return result?.nodes ?? new List<ProxyNodeDto>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Ошибка получения узлов: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить шаблоны ОДПУ отчётов
        /// </summary>
        public async Task<List<ProxyReportTemplateDto>> GetOdpuTemplatesAsync(int? systemTypeId = null)
        {
            try
            {
                string url = "lersproxy/reports/templates";
                if (systemTypeId.HasValue)
                    url += $"?systemTypeId={systemTypeId.Value}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = _serializer.Deserialize<ProxyTemplateListResponse>(json);

                Logger.Info($"[Proxy] Загружено {result?.templates?.Count ?? 0} шаблонов ОДПУ");
                return result?.templates ?? new List<ProxyReportTemplateDto>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Ошибка получения шаблонов ОДПУ: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получить шаблоны ИПУ отчётов
        /// </summary>
        public async Task<List<ProxyReportTemplateDto>> GetApartmentTemplatesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("lersproxy/reports/apartment-templates");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = _serializer.Deserialize<ProxyTemplateListResponse>(json);

                Logger.Info($"[Proxy] Загружено {result?.templates?.Count ?? 0} шаблонов ИПУ");
                return result?.templates ?? new List<ProxyReportTemplateDto>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Ошибка получения шаблонов ИПУ: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Таймаут генерации одного отчёта (3 минуты)
        /// </summary>
        private static readonly TimeSpan ReportGenerationTimeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// Сгенерировать отчёт
        /// </summary>
        public async Task<byte[]> GenerateReportAsync(
            int reportId,
            int[] measurePointIds,
            int[] nodeIds,
            string dataType,
            DateTime startDate,
            DateTime endDate,
            string format = "Pdf")
        {
            try
            {
                var request = new
                {
                    reportId = reportId,
                    measurePointIds = measurePointIds,
                    nodeIds = nodeIds,
                    dataType = dataType ?? "Day",
                    startDate = startDate,
                    endDate = endDate,
                    format = format ?? "Pdf"
                };

                var json = _serializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Logger.Info($"[Proxy] Генерация отчёта {reportId}...");

                // Используем отдельный таймаут для генерации отчётов
                using (var cts = new CancellationTokenSource(ReportGenerationTimeout))
                {
                    HttpResponseMessage response;
                    try
                    {
                        response = await _httpClient.PostAsync("lersproxy/reports/generate", content, cts.Token);
                    }
                    catch (TaskCanceledException) when (cts.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Таймаут генерации отчёта {reportId} (превышено {ReportGenerationTimeout.TotalMinutes} мин)");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorJson = await response.Content.ReadAsStringAsync();
                        var error = TryParseError(errorJson);
                        throw new Exception($"HTTP {(int)response.StatusCode}: {error}");
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    Logger.Info($"[Proxy] Отчёт сгенерирован: {bytes.Length} байт");
                    return bytes;
                }
            }
            catch (TimeoutException)
            {
                Logger.Error($"[Proxy] Таймаут генерации отчёта {reportId}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Ошибка генерации отчёта: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Проверить доступность прокси-службы
        /// </summary>
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("lersproxy/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получить покрытие данными (процент точек с данными)
        /// </summary>
        public async Task<ProxyCoverageResult> GetCoverageAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("lersproxy/measurepoints/coverage");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = _serializer.Deserialize<ProxyCoverageResponse>(json);

                    if (result != null && result.success)
                    {
                        Logger.Info($"[Proxy] Покрытие: {result.coveragePercent}% ({result.withData}/{result.totalMeasurePoints})");
                        return new ProxyCoverageResult
                        {
                            Success = true,
                            TotalMeasurePoints = result.totalMeasurePoints,
                            WithData = result.withData,
                            CoveragePercent = result.coveragePercent
                        };
                    }
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var error = TryParseError(errorJson);
                Logger.Error($"[Proxy] Ошибка получения покрытия: {error}");
                return new ProxyCoverageResult { Success = false, ErrorMessage = error };
            }
            catch (Exception ex)
            {
                Logger.Error($"[Proxy] Исключение при получении покрытия: {ex.Message}");
                return new ProxyCoverageResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Получить версию прокси-службы
        /// </summary>
        public async Task<string> GetVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("lersproxy/version");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = _serializer.Deserialize<ProxyVersionResponse>(json);
                    return result?.version;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        private string GetProxyBaseUrl(ServerConfig server)
        {
            // URL уже содержит полный адрес с портом
            var url = server.Url?.TrimEnd('/');
            return string.IsNullOrEmpty(url) ? "" : url + "/";
        }

        private string TryParseError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return "Пустой ответ";

            try
            {
                var error = _serializer.Deserialize<ProxyErrorResponse>(responseBody);
                if (!string.IsNullOrEmpty(error?.error))
                    return error.error;
            }
            catch { } // Если JSON не парсится, вернём raw текст (ниже)

            return responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    #region Proxy DTOs

    // DTO классы с именами свойств в camelCase для совместимости с JavaScriptSerializer

    public class ProxyLoginResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ProxyLoginResponse
    {
        public bool success { get; set; }
        public string token { get; set; }
        public string error { get; set; }
    }

    public class ProxyMeasurePointListResponse
    {
        public List<ProxyMeasurePointDto> measurePoints { get; set; }
    }

    public class ProxyMeasurePointDto
    {
        public int id { get; set; }
        public string title { get; set; }
        public string fullTitle { get; set; }
        public int? nodeId { get; set; }
        public int systemTypeId { get; set; }
        public string measurePointType { get; set; }
        public List<ProxyMeasurePointReportDto> reports { get; set; }
    }

    public class ProxyMeasurePointReportDto
    {
        public int reportId { get; set; }
        public string reportTitle { get; set; }
        public int? reportTemplateId { get; set; }
        public string reportTemplateTitle { get; set; }
    }

    public class ProxyNodeListResponse
    {
        public List<ProxyNodeDto> nodes { get; set; }
    }

    public class ProxyNodeDto
    {
        public int id { get; set; }
        public string title { get; set; }
        public string fullTitle { get; set; }
        public string address { get; set; }
        public string nodeType { get; set; }
    }

    public class ProxyTemplateListResponse
    {
        public List<ProxyReportTemplateDto> templates { get; set; }
    }

    public class ProxyReportTemplateDto
    {
        public int reportId { get; set; }
        public int? reportTemplateId { get; set; }
        public string title { get; set; }
        public string templateTitle { get; set; }
        public string reportType { get; set; }
    }

    public class ProxyVersionResponse
    {
        public string service { get; set; }
        public string version { get; set; }
    }

    public class ProxyErrorResponse
    {
        public string error { get; set; }
    }

    public class ProxyCoverageResponse
    {
        public bool success { get; set; }
        public int totalMeasurePoints { get; set; }
        public int withData { get; set; }
        public double coveragePercent { get; set; }
        public string error { get; set; }
    }

    public class ProxyCoverageResult
    {
        public bool Success { get; set; }
        public int TotalMeasurePoints { get; set; }
        public int WithData { get; set; }
        public double CoveragePercent { get; set; }
        public string ErrorMessage { get; set; }
    }

    #endregion
}
