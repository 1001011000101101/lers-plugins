using System;
using System.IO;
using Lers;
using Lers.Plugins;
using Lers.UI;
using LersReportCommon;
using LersReportGeneratorPlugin.Forms;
using LersReportGeneratorPlugin.Services;

namespace LersReportGeneratorPlugin
{
    /// <summary>
    /// Main plugin class implementing IPlugin interface for LERS integration
    /// </summary>
    public class Plugin : IPlugin
    {
        private IPluginHost _host;
        private ReportGeneratorService _reportService;

        /// <summary>
        /// Initialize the plugin
        /// </summary>
        public void Initialize(IPluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            // Инициализация логгера
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LersReportGeneratorPlugin", "Logs");
            Logger.Initialize(logDirectory, "plugin", consoleOutput: false);

            // Логирование и очистка старых логов
            Logger.Info("=== Плагин LersReportGenerator инициализируется ===");
            Logger.Info($"Версия ЛЭРС: {_host.Server?.Version}");
            Logger.CleanupOldLogs(daysToKeep: 7);

            // Create report generation service
            _reportService = new ReportGeneratorService(_host.Server);

            // Инициализация сервиса покрытия данными и запуск фоновой загрузки
            DataCoverageService.Instance.SetLocalServer(_host.Server);
            DataCoverageService.Instance.StartBackgroundRefresh();
            Logger.Info("Фоновая загрузка покрытия данными запущена");

            // Register menu item in main menu under "Service"
            RegisterMenuItems();

            Logger.Info("Плагин успешно инициализирован");
        }

        /// <summary>
        /// Register menu items in LERS main menu
        /// </summary>
        private void RegisterMenuItems()
        {
            if (_host?.MainWindow?.MainMenu == null)
                return;

            // Find "Service" menu and add our item
            foreach (var item in _host.MainWindow.MainMenu.Items)
            {
                if (item.ID == (int)SystemMenuId.Service)
                {
                    item.AddItem("Генератор отчётов", null, true, OnGeneratorMenuClick);
                    break;
                }
            }
        }

        /// <summary>
        /// Handler for menu item click
        /// </summary>
        private void OnGeneratorMenuClick(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("Открытие формы генератора отчётов");
                ShowMainForm();
            }
            catch (Exception ex)
            {
                Logger.Error("Ошибка при открытии формы генератора", ex);
                System.Windows.Forms.MessageBox.Show(
                    $"Ошибка при открытии генератора отчётов:\n{ex.Message}",
                    "Ошибка",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Show main generator form
        /// </summary>
        private void ShowMainForm()
        {
            if (_host == null || _reportService == null)
            {
                Logger.Warning("ShowMainForm вызван, но host или reportService не инициализированы");
                return;
            }

            using (var form = new MainGeneratorForm(_host.Server, _reportService))
            {
                form.ShowDialog();
            }

            Logger.Info("Форма генератора отчётов закрыта");
        }

        /// <summary>
        /// Dispose plugin resources
        /// </summary>
        public void Dispose()
        {
            _reportService?.Dispose();
            _reportService = null;
            _host = null;
        }
    }
}
