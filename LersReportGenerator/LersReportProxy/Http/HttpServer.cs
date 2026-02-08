using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportProxy.Services;

namespace LersReportProxy.Http
{
    /// <summary>
    /// HTTP сервер для обработки запросов к прокси
    /// </summary>
    public class HttpServer : IDisposable
    {
        private readonly Configuration _config;
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly LersConnectionManager _connectionManager;
        private readonly RequestRouter _router;
        private bool _disposed;

        public HttpServer(Configuration config)
        {
            _config = config;
            _listener = new HttpListener();
            _cts = new CancellationTokenSource();
            _connectionManager = new LersConnectionManager(config.LersServerHost, config.LersServerPort);
            _router = new RequestRouter(_connectionManager);

            // Добавляем префиксы для прослушивания
            _listener.Prefixes.Add($"http://+:{config.Port}/");
        }

        /// <summary>
        /// Запуск сервера
        /// </summary>
        public void Start()
        {
            try
            {
                _listener.Start();
                Logger.Info($"HTTP сервер слушает на порту {_config.Port}");

                // Запускаем обработку запросов в фоне
                Task.Run(() => ProcessRequestsAsync(_cts.Token));
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode == 5) // Access denied
                {
                    Logger.Error($"Ошибка доступа. Запустите от имени администратора или выполните:");
                    Logger.Error($"  netsh http add urlacl url=http://+:{_config.Port}/ user=Everyone");
                }
                throw;
            }
        }

        /// <summary>
        /// Остановка сервера
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();

                    // Обрабатываем запрос асинхронно
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleRequestAsync(context);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Ошибка обработки запроса: {ex.Message}");
                            await SendErrorAsync(context, 500, "Internal Server Error");
                        }
                    });
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    // Нормальное завершение при отмене
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка получения запроса: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Получаем IP клиента
            var clientIp = request.RemoteEndPoint?.Address?.ToString() ?? "unknown";

            // Проверяем IP whitelist
            if (!_config.IsIpAllowed(clientIp))
            {
                Logger.Warning($"Доступ запрещён для IP: {clientIp}");
                await SendErrorAsync(context, 403, "Access denied");
                return;
            }

            var path = request.Url.AbsolutePath.ToLower();
            var method = request.HttpMethod;

            Logger.Debug($"{method} {path} от {clientIp}");

            try
            {
                await _router.RouteAsync(context);
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка маршрутизации: {ex.Message}");
                await SendErrorAsync(context, 500, ex.Message);
            }
        }

        private async Task SendErrorAsync(HttpListenerContext context, int statusCode, string message)
        {
            try
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";

                var json = $"{{\"error\":\"{message}\"}}";
                var buffer = System.Text.Encoding.UTF8.GetBytes(json);

                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                // Ошибки при отправке ответа об ошибке - выводим в лог (соединение могло быть закрыто клиентом)
                Logger.Debug($"Не удалось отправить ответ об ошибке: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _connectionManager?.Dispose();
                _cts?.Dispose();
                _disposed = true;
            }
        }
    }
}
