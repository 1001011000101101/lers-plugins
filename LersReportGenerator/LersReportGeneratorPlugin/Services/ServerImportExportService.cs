using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Сервис для импорта/экспорта серверов
    /// </summary>
    public static class ServerImportExportService
    {
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        /// <summary>
        /// Экспортирует серверы в JSON файл
        /// </summary>
        /// <param name="filePath">Путь к файлу для сохранения</param>
        /// <param name="servers">Список серверов для экспорта</param>
        /// <param name="includePasswords">Включить пароли в экспорт</param>
        /// <param name="encryptPasswords">Шифровать пароли (требует masterPassword)</param>
        /// <param name="masterPassword">Мастер-пароль для шифрования (если encryptPasswords = true)</param>
        public static void ExportServers(
            string filePath,
            List<ServerConfig> servers,
            bool includePasswords,
            bool encryptPasswords = false,
            string masterPassword = null)
        {
            if (servers == null || servers.Count == 0)
            {
                throw new ArgumentException("Список серверов пуст", nameof(servers));
            }

            if (encryptPasswords && string.IsNullOrEmpty(masterPassword))
            {
                throw new ArgumentException("Для шифрования паролей требуется мастер-пароль", nameof(masterPassword));
            }

            var exportData = new ServerExportData
            {
                Version = "1.0",
                Encrypted = encryptPasswords,
                Servers = new List<ExportedServer>()
            };

            foreach (var server in servers)
            {
                var exported = new ExportedServer
                {
                    Name = server.Name,
                    Address = server.Address,
                    Url = server.Url,
                    Login = server.Login,
                    UseProxy = server.UseProxy,
                    IgnoreSslErrors = server.IgnoreSslErrors
                };

                // Обработка паролей
                if (includePasswords)
                {
                    // Получаем пароль из DPAPI
                    string plainPassword = null;
                    if (!string.IsNullOrEmpty(server.EncryptedPassword))
                    {
                        try
                        {
                            plainPassword = CredentialManager.DecryptPassword(server.EncryptedPassword);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Не удалось расшифровать пароль для сервера '{server.Name}': {ex.Message}");
                        }
                    }

                    if (!string.IsNullOrEmpty(plainPassword))
                    {
                        if (encryptPasswords)
                        {
                            // Шифруем пароль с мастер-паролем
                            string salt, iv;
                            exported.EncryptedPassword = EncryptionService.Encrypt(plainPassword, masterPassword, out salt, out iv);
                            exported.Salt = salt;
                            exported.IV = iv;
                        }
                        else
                        {
                            // Сохраняем в открытом виде (небезопасно!)
                            exported.PlainPassword = plainPassword;
                        }
                    }
                }

                exportData.Servers.Add(exported);
            }

            // Сериализуем в JSON
            string json = _serializer.Serialize(exportData);
            json = FormatJson(json);

            // Сохраняем в файл
            File.WriteAllText(filePath, json);

            Logger.Info($"Экспортировано серверов: {servers.Count}, пароли: {(includePasswords ? (encryptPasswords ? "зашифрованы" : "открытые") : "не включены")}");
        }

        /// <summary>
        /// Импортирует серверы из JSON файла
        /// </summary>
        /// <param name="filePath">Путь к файлу импорта</param>
        /// <param name="masterPassword">Мастер-пароль для расшифровки (если файл содержит зашифрованные пароли)</param>
        /// <returns>Список импортированных серверов</returns>
        public static List<ServerConfig> ImportServers(string filePath, string masterPassword = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл не найден", filePath);
            }

            string json = File.ReadAllText(filePath);
            var exportData = _serializer.Deserialize<ServerExportData>(json);

            if (exportData == null || exportData.Servers == null)
            {
                throw new InvalidDataException("Некорректный формат файла импорта");
            }

            // Проверяем версию формата
            if (exportData.Version != "1.0")
            {
                Logger.Warning($"Неизвестная версия формата: {exportData.Version}. Попытка импорта...");
            }

            var importedServers = new List<ServerConfig>();

            foreach (var exported in exportData.Servers)
            {
                var server = new ServerConfig
                {
                    Id = Guid.NewGuid(), // Генерируем новый ID
                    Name = exported.Name,
                    Address = exported.Address,
                    Url = exported.Url,
                    Login = exported.Login,
                    UseProxy = exported.UseProxy,
                    IgnoreSslErrors = exported.IgnoreSslErrors,
                    IsDefault = false // При импорте не делаем сервер по умолчанию
                };

                // Обработка пароля
                string plainPassword = null;

                if (exportData.Encrypted)
                {
                    // Пароль зашифрован - расшифровываем
                    if (!string.IsNullOrEmpty(exported.EncryptedPassword))
                    {
                        if (string.IsNullOrEmpty(masterPassword))
                        {
                            throw new InvalidOperationException("Файл содержит зашифрованные пароли, но мастер-пароль не предоставлен");
                        }

                        try
                        {
                            plainPassword = EncryptionService.Decrypt(
                                exported.EncryptedPassword,
                                masterPassword,
                                exported.Salt,
                                exported.IV);
                        }
                        catch (CryptographicException ex)
                        {
                            // Неверный мастер-пароль - прерываем импорт
                            Logger.Error($"Ошибка расшифровки пароля для '{exported.Name}': {ex.Message}");
                            throw new CryptographicException("Неверный мастер-пароль. Импорт отменён.", ex);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Ошибка расшифровки пароля для '{exported.Name}': {ex.Message}");
                            throw new InvalidOperationException($"Не удалось расшифровать пароль для '{exported.Name}'. Импорт отменён.", ex);
                        }
                    }
                }
                else
                {
                    // Пароль в открытом виде
                    plainPassword = exported.PlainPassword;
                }

                // Шифруем пароль через DPAPI для локального хранения
                if (!string.IsNullOrEmpty(plainPassword))
                {
                    server.EncryptedPassword = CredentialManager.EncryptPassword(plainPassword);
                }

                importedServers.Add(server);
            }

            Logger.Info($"Импортировано серверов: {importedServers.Count}");
            return importedServers;
        }

        /// <summary>
        /// Умное слияние: обновляет существующие серверы (по URL), добавляет новые
        /// </summary>
        /// <param name="existingServers">Существующий список серверов</param>
        /// <param name="importedServers">Импортированные серверы</param>
        /// <returns>Результат слияния (обновлённые, добавленные, пропущенные)</returns>
        public static MergeResult MergeServers(List<ServerConfig> existingServers, List<ServerConfig> importedServers)
        {
            var result = new MergeResult();

            foreach (var imported in importedServers)
            {
                // Ищем существующий сервер по нормализованному URL
                var existing = existingServers.FirstOrDefault(s =>
                    NormalizeUrl(s.Url) == NormalizeUrl(imported.Url));

                if (existing != null)
                {
                    // Обновляем существующий сервер
                    existing.Name = imported.Name;
                    existing.Address = imported.Address;
                    existing.Login = imported.Login;
                    existing.UseProxy = imported.UseProxy;
                    existing.IgnoreSslErrors = imported.IgnoreSslErrors;

                    // Обновляем пароль только если он был импортирован
                    if (!string.IsNullOrEmpty(imported.EncryptedPassword))
                    {
                        existing.EncryptedPassword = imported.EncryptedPassword;
                    }

                    result.Updated.Add(existing);
                }
                else
                {
                    // Добавляем новый сервер
                    existingServers.Add(imported);
                    result.Added.Add(imported);
                }
            }

            return result;
        }

        /// <summary>
        /// Нормализует URL для сравнения (приводит к единому виду)
        /// </summary>
        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            try
            {
                var uri = new Uri(url.Trim());
                // Возвращаем схему + хост (без порта и пути)
                return $"{uri.Scheme}://{uri.Host}".ToLowerInvariant();
            }
            catch
            {
                // Если не удалось распарсить как URI, возвращаем как есть
                return url.Trim().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Результат слияния серверов
        /// </summary>
        public class MergeResult
        {
            public List<ServerConfig> Added { get; set; } = new List<ServerConfig>();
            public List<ServerConfig> Updated { get; set; } = new List<ServerConfig>();
        }

        /// <summary>
        /// Простое форматирование JSON для читаемости
        /// </summary>
        private static string FormatJson(string json)
        {
            var sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char c in json)
            {
                if (c == '"' && (sb.Length == 0 || sb[sb.Length - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    switch (c)
                    {
                        case '{':
                        case '[':
                            sb.Append(c);
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case '}':
                        case ']':
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 2));
                            sb.Append(c);
                            break;
                        case ',':
                            sb.Append(c);
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case ':':
                            sb.Append(c);
                            sb.Append(' ');
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
