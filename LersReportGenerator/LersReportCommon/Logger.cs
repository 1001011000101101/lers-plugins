using System;
using System.IO;
using System.Text;

namespace LersReportCommon
{
    /// <summary>
    /// Универсальный файловый логгер.
    /// Поддерживает настраиваемый путь и префикс файла.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logDirectory;
        private static string _logFilePath;
        private static string _filePrefix = "log";
        private static bool _isEnabled = true;
        private static bool _consoleOutput = false;
        private static bool _initialized = false;

        /// <summary>
        /// Инициализировать логгер с указанными параметрами
        /// </summary>
        /// <param name="logDirectory">Папка для логов</param>
        /// <param name="filePrefix">Префикс файла (например "plugin" → "plugin_2026-02-06.log")</param>
        /// <param name="consoleOutput">Дублировать вывод в консоль</param>
        public static void Initialize(string logDirectory, string filePrefix = "log", bool consoleOutput = false)
        {
            _logDirectory = logDirectory;
            _filePrefix = filePrefix;
            _consoleOutput = consoleOutput;
            _logFilePath = Path.Combine(_logDirectory, $"{_filePrefix}_{DateTime.Now:yyyy-MM-dd}.log");
            _initialized = true;
        }

        /// <summary>
        /// Включить/выключить логирование
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Путь к файлу лога
        /// </summary>
        public static string LogFilePath => _logFilePath;

        /// <summary>
        /// Путь к папке логов
        /// </summary>
        public static string GetLogsDirectory() => _logDirectory;

        /// <summary>
        /// Записать информационное сообщение
        /// </summary>
        public static void Info(string message) => WriteLog("INFO", message);

        /// <summary>
        /// Записать предупреждение
        /// </summary>
        public static void Warning(string message) => WriteLog("WARN", message);

        /// <summary>
        /// Записать ошибку
        /// </summary>
        public static void Error(string message) => WriteLog("ERROR", message);

        /// <summary>
        /// Записать исключение
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine($"  Exception: {ex.GetType().Name}");
            sb.AppendLine($"  Message: {ex.Message}");

            if (ex.InnerException != null)
            {
                sb.AppendLine($"  Inner: {ex.InnerException.Message}");
            }

            sb.AppendLine($"  StackTrace: {ex.StackTrace}");

            WriteLog("ERROR", sb.ToString());
        }

        /// <summary>
        /// Записать отладочное сообщение (только в DEBUG сборках)
        /// </summary>
        public static void Debug(string message)
        {
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        private static void WriteLog(string level, string message)
        {
            if (!_isEnabled)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";

            // Вывод в консоль
            if (_consoleOutput)
            {
                Console.WriteLine(logLine);
            }

            // Запись в файл
            if (_initialized && _logFilePath != null)
            {
                try
                {
                    lock (_lock)
                    {
                        if (!Directory.Exists(_logDirectory))
                        {
                            Directory.CreateDirectory(_logDirectory);
                        }

                        File.AppendAllText(_logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    // Ошибки записи выводим только в консоль
                    if (_consoleOutput)
                    {
                        Console.WriteLine($"[Logger] Ошибка записи в файл: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Очистить старые логи (старше указанного количества дней)
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            if (!_initialized || string.IsNullOrEmpty(_logDirectory))
                return;

            try
            {
                if (!Directory.Exists(_logDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var pattern = $"{_filePrefix}_*.log";

                foreach (var file in Directory.GetFiles(_logDirectory, pattern))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки очистки логов
            }
        }
    }
}
