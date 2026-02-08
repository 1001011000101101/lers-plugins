using System;
using System.Reflection;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Информация о версии плагина
    /// </summary>
    public static class VersionInfo
    {
        private static string _version;
        private static string _fileVersion;
        private static string _buildDate;

        /// <summary>
        /// Версия сборки (Major.Minor.Patch.Build)
        /// </summary>
        public static string Version
        {
            get
            {
                if (_version == null)
                    LoadVersionInfo();
                return _version;
            }
        }

        /// <summary>
        /// Версия файла
        /// </summary>
        public static string FileVersion
        {
            get
            {
                if (_fileVersion == null)
                    LoadVersionInfo();
                return _fileVersion;
            }
        }

        /// <summary>
        /// Дата сборки
        /// </summary>
        public static string BuildDate
        {
            get
            {
                if (_buildDate == null)
                    LoadVersionInfo();
                return _buildDate;
            }
        }

        /// <summary>
        /// Краткая версия (Major.Minor.Patch)
        /// </summary>
        public static string ShortVersion
        {
            get
            {
                var ver = Version;
                var parts = ver.Split('.');
                if (parts.Length >= 3)
                    return $"{parts[0]}.{parts[1]}.{parts[2]}";
                return ver;
            }
        }

        /// <summary>
        /// Строка для отображения в UI
        /// </summary>
        public static string DisplayVersion => $"Версия {ShortVersion}";

        /// <summary>
        /// Полная строка версии с датой
        /// </summary>
        public static string FullDisplayVersion => $"Версия {ShortVersion} ({BuildDate})";

        private static void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // AssemblyVersion
                var version = assembly.GetName().Version;
                _version = version?.ToString() ?? "1.0.0.0";

                // AssemblyFileVersion
                var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                _fileVersion = fileVersionAttr?.Version ?? _version;

                // Дата сборки из времени файла
                try
                {
                    var location = assembly.Location;
                    if (!string.IsNullOrEmpty(location))
                    {
                        var fileInfo = new System.IO.FileInfo(location);
                        _buildDate = fileInfo.LastWriteTime.ToString("dd.MM.yyyy");
                    }
                    else
                    {
                        _buildDate = "unknown";
                    }
                }
                catch
                {
                    _buildDate = "unknown";
                }
            }
            catch
            {
                _version = "1.0.0.0";
                _fileVersion = "1.0.0.0";
                _buildDate = "unknown";
            }
        }
    }
}
