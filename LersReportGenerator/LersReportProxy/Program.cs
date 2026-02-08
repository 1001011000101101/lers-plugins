using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using LersReportCommon;
using LersReportProxy.Services;

namespace LersReportProxy
{
    static class Program
    {
        private static string _lersLibraryPath;
        private static bool _assemblyResolverInitialized;

        /// <summary>
        /// Статический конструктор - выполняется ДО Main().
        /// Регистрируем AssemblyResolve здесь, чтобы перехватить загрузку Lers.*.dll
        /// </summary>
        static Program()
        {
            SetupLersAssemblyResolver();
        }

        /// <summary>
        /// Точка входа приложения.
        /// Поддерживает запуск как Windows Service и как консольное приложение (для отладки).
        /// </summary>
        static void Main(string[] args)
        {
            // Устанавливаем рабочую директорию на папку exe-файла
            // Важно для Windows Service, где рабочая директория может быть C:\Windows\System32
            var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(exePath))
            {
                Directory.SetCurrentDirectory(exePath);
            }

            // Загружаем конфигурацию (AssemblyResolve уже настроен в статическом конструкторе)
            var config = Configuration.Load();
            _lersLibraryPath = config.LersLibraryPath;

            // Инициализация логгера
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LersReportProxy", "Logs");
            Logger.Initialize(logDirectory, "proxy", consoleOutput: true);
            Logger.CleanupOldLogs(Constants.LogRetentionDays);

            Logger.Info($"Setting up AssemblyResolve for LERS libraries: {_lersLibraryPath}");

            // Режим консоли для отладки: --console или -c
            bool consoleMode = args.Length > 0 &&
                (args[0] == "--console" || args[0] == "-c");

            if (consoleMode || Environment.UserInteractive && System.Diagnostics.Debugger.IsAttached)
            {
                // Консольный режим
                RunAsConsole();
            }
            else
            {
                // Режим Windows Service
                RunAsService();
            }
        }

        /// <summary>
        /// Настройка загрузки библиотек ЛЭРС из указанной папки.
        /// Вызывается из статического конструктора ДО Main().
        /// </summary>
        private static void SetupLersAssemblyResolver()
        {
            if (_assemblyResolverInitialized)
                return;

            _assemblyResolverInitialized = true;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name);

                // Загружаем библиотеки ЛЭРС и их зависимости (System.*, Microsoft.*)
                if (assemblyName.Name != null &&
                    (assemblyName.Name.StartsWith("Lers.") ||
                     assemblyName.Name.StartsWith("System.") ||
                     assemblyName.Name.StartsWith("Microsoft.")))
                {
                    // Определяем путь к библиотекам
                    string libraryPath = _lersLibraryPath;

                    // Если путь ещё не загружен из конфигурации, пробуем стандартные пути
                    if (string.IsNullOrEmpty(libraryPath))
                    {
                        // Пробуем прочитать config.json рядом с exe
                        try
                        {
                            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                            var configPath = Path.Combine(exeDir, "config.json");
                            if (File.Exists(configPath))
                            {
                                var json = File.ReadAllText(configPath);
                                // Простой парсинг без зависимостей
                                var match = System.Text.RegularExpressions.Regex.Match(json, "\"LersLibraryPath\"\\s*:\\s*\"([^\"]+)\"");
                                if (match.Success)
                                {
                                    libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                                }
                            }
                        }
                        catch { } // Конфиг может отсутствовать — используем fallback

                        // Fallback на стандартный путь
                        if (string.IsNullOrEmpty(libraryPath))
                        {
                            libraryPath = Configuration.DefaultLersLibraryPath;
                        }
                    }

                    var assemblyPath = Path.Combine(libraryPath, $"{assemblyName.Name}.dll");

                    if (File.Exists(assemblyPath))
                    {
                        try
                        {
                            // Не используем Logger здесь - он может ещё не инициализирован
                            Console.WriteLine($"[AssemblyResolve] Loading: {assemblyPath}");
                            return Assembly.LoadFrom(assemblyPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AssemblyResolve] Failed to load {assemblyName.Name}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[AssemblyResolve] Not found: {assemblyPath}");
                    }
                }

                return null;
            };
        }

        private static void RunAsService()
        {
            var servicesToRun = new ServiceBase[]
            {
                new ProxyService()
            };
            ServiceBase.Run(servicesToRun);
        }

        private static void RunAsConsole()
        {
            Console.WriteLine("=== LERS Report Proxy Service ===");
            Console.WriteLine("Запуск в консольном режиме...");
            Console.WriteLine("Нажмите Ctrl+C для остановки.");
            Console.WriteLine();

            var service = new ProxyService();
            service.StartConsole();

            // Ожидаем Ctrl+C
            var exitEvent = new System.Threading.ManualResetEvent(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                exitEvent.Set();
            };

            exitEvent.WaitOne();

            Console.WriteLine("\nОстановка службы...");
            service.StopConsole();
            Console.WriteLine("Служба остановлена.");
        }
    }
}
