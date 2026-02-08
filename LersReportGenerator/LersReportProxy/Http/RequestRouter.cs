using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LersReportProxy.Http.Handlers;
using LersReportProxy.Services;
using Newtonsoft.Json;

namespace LersReportProxy.Http
{
    /// <summary>
    /// Маршрутизатор HTTP запросов
    /// </summary>
    public class RequestRouter
    {
        private readonly LersConnectionManager _connectionManager;
        private readonly AuthHandler _authHandler;
        private readonly MeasurePointsHandler _measurePointsHandler;
        private readonly ReportsHandler _reportsHandler;
        private readonly NodesHandler _nodesHandler;

        public RequestRouter(LersConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _authHandler = new AuthHandler(connectionManager);
            _measurePointsHandler = new MeasurePointsHandler(connectionManager);
            _reportsHandler = new ReportsHandler(connectionManager);
            _nodesHandler = new NodesHandler(connectionManager);
        }

        /// <summary>
        /// Маршрутизация запроса к соответствующему обработчику
        /// </summary>
        public async Task RouteAsync(HttpListenerContext context)
        {
            var path = context.Request.Url.AbsolutePath.ToLower().TrimEnd('/');
            var method = context.Request.HttpMethod;

            // Маршруты без авторизации
            if (path == "/lersproxy/login" && method == "POST")
            {
                await _authHandler.LoginAsync(context);
                return;
            }

            if (path == "/lersproxy/version" && method == "GET")
            {
                await SendVersionAsync(context);
                return;
            }

            if (path == "/lersproxy/health" && method == "GET")
            {
                await SendHealthAsync(context);
                return;
            }

            // Для остальных маршрутов требуется авторизация
            var session = GetSessionFromRequest(context);
            if (session == null)
            {
                await SendJsonAsync(context, 401, new { error = "Unauthorized" });
                return;
            }

            // Маршруты с авторизацией
            switch (path)
            {
                // Точки учёта
                case "/lersproxy/measurepoints":
                    if (method == "GET")
                        await _measurePointsHandler.GetListAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                case "/lersproxy/measurepoints/coverage":
                    if (method == "GET")
                        await _measurePointsHandler.GetCoverageAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                case var p when p.StartsWith("/lersproxy/measurepoints/") && method == "GET":
                    await _measurePointsHandler.GetByIdAsync(context, session);
                    break;

                // Узлы (дома)
                case "/lersproxy/nodes":
                    if (method == "GET")
                        await _nodesHandler.GetListAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                // Отчёты
                case "/lersproxy/reports/templates":
                    if (method == "GET")
                        await _reportsHandler.GetTemplatesAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                case "/lersproxy/reports/apartment-templates":
                    if (method == "GET")
                        await _reportsHandler.GetApartmentTemplatesAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                case "/lersproxy/reports/generate":
                    if (method == "POST")
                        await _reportsHandler.GenerateAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                // Выход
                case "/lersproxy/logout":
                    if (method == "POST")
                        await _authHandler.LogoutAsync(context, session);
                    else
                        await SendMethodNotAllowedAsync(context);
                    break;

                default:
                    await SendNotFoundAsync(context);
                    break;
            }
        }

        private LersSession GetSessionFromRequest(HttpListenerContext context)
        {
            // Получаем токен из заголовка Authorization: Bearer <token>
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader))
                return null;

            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring(7).Trim();
                return _connectionManager.GetSession(token);
            }

            return null;
        }

        private async Task SendVersionAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context, 200, VersionInfo.GetFullInfo());
        }

        private async Task SendHealthAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context, 200, new { status = "ok" });
        }

        private async Task SendNotFoundAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context, 404, new { error = "Not Found" });
        }

        private async Task SendMethodNotAllowedAsync(HttpListenerContext context)
        {
            await SendJsonAsync(context, 405, new { error = "Method Not Allowed" });
        }

        public static async Task SendJsonAsync(HttpListenerContext context, int statusCode, object data)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);

            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        public static async Task<T> ReadJsonBodyAsync<T>(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                var json = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
    }
}
