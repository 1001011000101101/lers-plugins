using System;
using System.Threading.Tasks;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Результат подключения к удалённому серверу
    /// </summary>
    public class ServerConnectionResult
    {
        public bool Success { get; set; }

        /// <summary>
        /// Подключённый клиент (не null только при Success = true).
        /// Вызывающая сторона отвечает за Dispose.
        /// </summary>
        public LersProxyClient Client { get; set; }

        /// <summary>
        /// Сообщение об ошибке (при Success = false)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Заголовок ошибки для диалога (при Success = false)
        /// </summary>
        public string ErrorTitle { get; set; }

        /// <summary>
        /// Статусное сообщение (при Success = true)
        /// </summary>
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// Сервис подключения к удалённым серверам ЛЭРС.
    /// Выполняет проверку доступности прокси-службы и авторизацию.
    /// </summary>
    public class ServerConnectionService
    {
        /// <summary>
        /// Подключается к удалённому серверу: проверка доступности + авторизация.
        /// При ошибке клиент автоматически освобождается.
        /// </summary>
        public async Task<ServerConnectionResult> ConnectAsync(
            ServerConfig server,
            Action<string> statusCallback = null)
        {
            var client = new LersProxyClient(server);

            try
            {
                // Проверяем доступность прокси-службы
                statusCallback?.Invoke("Проверка доступности сервера...");
                bool proxyAvailable = await client.CheckHealthAsync();
                if (!proxyAvailable)
                {
                    client.Dispose();
                    return new ServerConnectionResult
                    {
                        ErrorTitle = "Ошибка подключения",
                        ErrorMessage = $"Прокси-служба на сервере {server.Name} недоступна.\n" +
                            $"URL: {server.Url}\n" +
                            $"Проверьте, что служба LersReportProxy запущена."
                    };
                }

                // Расшифровываем пароль
                string password = CredentialManager.DecryptPassword(server.EncryptedPassword);
                if (string.IsNullOrEmpty(password))
                {
                    client.Dispose();
                    return new ServerConnectionResult
                    {
                        ErrorTitle = "Ошибка авторизации",
                        ErrorMessage = "Пароль не найден. Отредактируйте настройки сервера."
                    };
                }

                // Авторизуемся
                statusCallback?.Invoke("Авторизация на сервере...");
                var loginResult = await client.LoginAsync(server.Login, password);
                if (loginResult.Success)
                {
                    return new ServerConnectionResult
                    {
                        Success = true,
                        Client = client,
                        StatusMessage = $"Подключено к {server.Name}"
                    };
                }

                client.Dispose();
                return new ServerConnectionResult
                {
                    ErrorTitle = "Ошибка авторизации",
                    ErrorMessage = $"Ошибка авторизации: {loginResult.ErrorMessage}"
                };
            }
            catch (Exception ex)
            {
                client.Dispose();
                return new ServerConnectionResult
                {
                    ErrorTitle = "Ошибка подключения",
                    ErrorMessage = $"Ошибка подключения: {ex.Message}"
                };
            }
        }
    }
}
