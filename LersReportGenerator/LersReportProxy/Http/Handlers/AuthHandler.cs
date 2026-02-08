using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportProxy.Services;

namespace LersReportProxy.Http.Handlers
{
    /// <summary>
    /// Обработчик запросов авторизации
    /// </summary>
    public class AuthHandler
    {
        private readonly LersConnectionManager _connectionManager;

        // Rate limiting: максимум попыток в минуту с одного IP
        private const int MaxAttemptsPerMinute = 5;
        private static readonly ConcurrentDictionary<string, RateLimitInfo> _loginAttempts = new ConcurrentDictionary<string, RateLimitInfo>();

        public AuthHandler(LersConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// POST /proxy/login
        /// Авторизация пользователя
        /// </summary>
        public async Task LoginAsync(HttpListenerContext context)
        {
            var clientIp = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";

            // Проверяем rate limit
            if (!CheckRateLimit(clientIp))
            {
                Logger.Warning($"Rate limit превышен для IP: {clientIp}");
                await RequestRouter.SendJsonAsync(context, 429, new { error = "Too many login attempts. Try again later." });
                return;
            }

            var request = await RequestRouter.ReadJsonBodyAsync<LoginRequest>(context);

            if (string.IsNullOrEmpty(request?.Login) || string.IsNullOrEmpty(request?.Password))
            {
                await RequestRouter.SendJsonAsync(context, 400, new { error = "Login and password are required" });
                return;
            }

            var result = await _connectionManager.LoginAsync(request.Login, request.Password);

            if (result.Success)
            {
                // Успешный вход - сбрасываем счётчик
                ResetRateLimit(clientIp);

                await RequestRouter.SendJsonAsync(context, 200, new
                {
                    success = true,
                    token = result.Token
                });
            }
            else
            {
                // Неудачная попытка - увеличиваем счётчик
                IncrementRateLimit(clientIp);

                await RequestRouter.SendJsonAsync(context, 401, new
                {
                    success = false,
                    error = result.Error
                });
            }
        }

        /// <summary>
        /// Проверить, не превышен ли лимит попыток
        /// </summary>
        private static bool CheckRateLimit(string clientIp)
        {
            if (_loginAttempts.TryGetValue(clientIp, out var info))
            {
                // Если прошло больше минуты - сбрасываем
                if (DateTime.UtcNow > info.ResetAt)
                {
                    _loginAttempts.TryRemove(clientIp, out _);
                    return true;
                }

                return info.Attempts < MaxAttemptsPerMinute;
            }

            return true;
        }

        /// <summary>
        /// Увеличить счётчик неудачных попыток
        /// </summary>
        private static void IncrementRateLimit(string clientIp)
        {
            _loginAttempts.AddOrUpdate(
                clientIp,
                new RateLimitInfo { Attempts = 1, ResetAt = DateTime.UtcNow.AddMinutes(1) },
                (key, existing) =>
                {
                    // Если истекло время - начинаем заново
                    if (DateTime.UtcNow > existing.ResetAt)
                    {
                        return new RateLimitInfo { Attempts = 1, ResetAt = DateTime.UtcNow.AddMinutes(1) };
                    }
                    existing.Attempts++;
                    return existing;
                });
        }

        /// <summary>
        /// Сбросить счётчик при успешном входе
        /// </summary>
        private static void ResetRateLimit(string clientIp)
        {
            _loginAttempts.TryRemove(clientIp, out _);
        }

        private class RateLimitInfo
        {
            public int Attempts { get; set; }
            public DateTime ResetAt { get; set; }
        }

        /// <summary>
        /// POST /proxy/logout
        /// Выход из системы
        /// </summary>
        public async Task LogoutAsync(HttpListenerContext context, LersSession session)
        {
            _connectionManager.CloseSession(session.Token);

            await RequestRouter.SendJsonAsync(context, 200, new { success = true });
        }
    }

    public class LoginRequest
    {
        public string Login { get; set; }
        public string Password { get; set; }
    }
}
