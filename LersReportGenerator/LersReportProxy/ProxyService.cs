using System;
using System.ServiceProcess;
using LersReportCommon;
using LersReportProxy.Http;
using LersReportProxy.Services;

namespace LersReportProxy
{
    /// <summary>
    /// Windows Service для прокси-сервера отчётов ЛЭРС
    /// </summary>
    public class ProxyService : ServiceBase
    {
        private HttpServer _httpServer;

        public ProxyService()
        {
            ServiceName = "LersReportProxy";
        }

        protected override void OnStart(string[] args)
        {
            Logger.Info("Служба LersReportProxy запускается...");
            StartServer();
        }

        protected override void OnStop()
        {
            Logger.Info("Служба LersReportProxy останавливается...");
            StopServer();
        }

        /// <summary>
        /// Запуск в консольном режиме (для отладки)
        /// </summary>
        public void StartConsole()
        {
            StartServer();
        }

        /// <summary>
        /// Остановка в консольном режиме
        /// </summary>
        public void StopConsole()
        {
            StopServer();
        }

        private void StartServer()
        {
            try
            {
                var config = Configuration.Load();
                Logger.Info($"Конфигурация: порт {config.Port}, ЛЭРС: {config.LersServerHost}:{config.LersServerPort}");

                _httpServer = new HttpServer(config);
                _httpServer.Start();

                Logger.Info($"HTTP сервер запущен на порту {config.Port}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка запуска сервера: {ex.Message}");
                throw;
            }
        }

        private void StopServer()
        {
            try
            {
                _httpServer?.Stop();
                _httpServer = null;
                Logger.Info("HTTP сервер остановлен");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка остановки сервера: {ex.Message}");
            }
        }
    }
}
