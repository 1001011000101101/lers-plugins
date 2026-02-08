using System;
using System.IO;
using LersReportCommon;
using Newtonsoft.Json;

namespace LersReportProxy.Services
{
    /// <summary>
    /// Конфигурация прокси-службы
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Путь к ЛЭРС Client по умолчанию
        /// </summary>
        public const string DefaultLersLibraryPath = @"C:\Program Files\LERS\Client";

        /// <summary>
        /// Порт HTTP сервера (по умолчанию 5377 = "LERS" на T9)
        /// </summary>
        public int Port { get; set; } = Constants.DefaultProxyPort;

        /// <summary>
        /// Хост локального ЛЭРС сервера
        /// </summary>
        public string LersServerHost { get; set; } = "localhost";

        /// <summary>
        /// Порт ЛЭРС сервера (по умолчанию 10000)
        /// </summary>
        public ushort LersServerPort { get; set; } = 10000;

        /// <summary>
        /// Путь к папке с библиотеками ЛЭРС (Lers.Core.dll и др.)
        /// </summary>
        public string LersLibraryPath { get; set; } = DefaultLersLibraryPath;

        /// <summary>
        /// Список разрешённых IP-адресов. Пустой список = доступ со всех IP.
        /// </summary>
        public string[] AllowedIPs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Проверить, разрешён ли доступ с указанного IP
        /// </summary>
        public bool IsIpAllowed(string clientIp)
        {
            // Пустой список = разрешены все
            if (AllowedIPs == null || AllowedIPs.Length == 0)
                return true;

            // localhost всегда разрешён
            if (clientIp == "127.0.0.1" || clientIp == "::1" || clientIp == "localhost")
                return true;

            foreach (var allowed in AllowedIPs)
            {
                if (string.Equals(allowed, clientIp, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Путь к файлу конфигурации
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath);
                return Path.Combine(exeDir, "config.json");
            }
        }

        /// <summary>
        /// Загрузить конфигурацию из файла
        /// </summary>
        public static Configuration Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonConvert.DeserializeObject<Configuration>(json);
                    if (config != null)
                    {
                        Logger.Info($"Конфигурация загружена из {ConfigPath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Ошибка загрузки конфигурации: {ex.Message}. Используются значения по умолчанию.");
            }

            // Возвращаем конфигурацию по умолчанию
            var defaultConfig = new Configuration();
            defaultConfig.Save(); // Сохраняем для последующего редактирования
            return defaultConfig;
        }

        /// <summary>
        /// Сохранить конфигурацию в файл
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                Logger.Info($"Конфигурация сохранена в {ConfigPath}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }
    }
}
