using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lers;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Сервис для получения покрытия данными точек учёта через ЛЭРС Framework.
    /// Кэширует результаты для быстрого доступа.
    /// </summary>
    public class DataCoverageService
    {
        private static volatile DataCoverageService _instance;
        private static readonly object _lock = new object();

        // Кэш результатов покрытия (ServerName -> Result)
        private readonly ConcurrentDictionary<string, DataCoverageResult> _cache
            = new ConcurrentDictionary<string, DataCoverageResult>();

        // Локальный сервер (из Plugin API)
        private LersServer _localServer;

        // Событие обновления данных
        public event EventHandler<DataCoverageResult> CoverageUpdated;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static DataCoverageService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new DataCoverageService();
                    }
                }
                return _instance;
            }
        }

        private DataCoverageService() { }

        /// <summary>
        /// Установить локальный сервер (вызывается при инициализации плагина)
        /// </summary>
        public void SetLocalServer(LersServer server)
        {
            _localServer = server;
        }

        /// <summary>
        /// Получить кэшированный результат покрытия для сервера
        /// </summary>
        public DataCoverageResult GetCachedCoverage(string serverName)
        {
            _cache.TryGetValue(serverName, out var result);
            return result;
        }

        /// <summary>
        /// Получить все кэшированные результаты
        /// </summary>
        public IReadOnlyDictionary<string, DataCoverageResult> GetAllCachedCoverage()
        {
            return new Dictionary<string, DataCoverageResult>(_cache);
        }

        /// <summary>
        /// Получить покрытие данными с локального сервера
        /// </summary>
        public async Task<DataCoverageResult> GetLocalCoverageAsync(CancellationToken cancellationToken = default)
        {
            const string serverName = "Локальный";
            var result = new DataCoverageResult { ServerName = serverName };

            try
            {
                if (_localServer == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Сервер не инициализирован";
                    return UpdateCache(serverName, result);
                }

                Logger.Info("[Coverage] Загрузка точек учёта с локального сервера...");

                // Получаем все точки учёта
                var measurePoints = await _localServer.MeasurePoints.GetListAsync();

                if (measurePoints == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Не удалось получить список точек";
                    return UpdateCache(serverName, result);
                }

                var pointsList = measurePoints.ToList();
                result.TotalMeasurePoints = pointsList.Count;

                // Считаем точки с данными (State != NoData)
                // Используем числовое сравнение для совместимости с разными версиями ЛЭРС
                result.WithData = pointsList.Count(mp => (int)mp.State != Constants.MeasurePointState.NoData);

                // Вычисляем процент
                result.CoveragePercent = result.TotalMeasurePoints > 0
                    ? Math.Round((double)result.WithData / result.TotalMeasurePoints * 100, 1)
                    : 0;

                result.Success = true;
                result.CheckedAt = DateTime.Now;

                Logger.Info($"[Coverage] Локальный: {result.FormattedCoverage}");

                return UpdateCache(serverName, result);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Coverage] Ошибка получения покрытия с локального сервера: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return UpdateCache(serverName, result);
            }
        }

        /// <summary>
        /// Получить покрытие данными с удалённого сервера через прокси
        /// </summary>
        public async Task<DataCoverageResult> GetRemoteCoverageAsync(
            ServerConfig server,
            CancellationToken cancellationToken = default)
        {
            var result = new DataCoverageResult { ServerName = server.Name };

            try
            {
                using (var client = new LersProxyClient(server))
                {
                    // Проверяем доступность прокси
                    bool available = await client.CheckHealthAsync();
                    if (!available)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Прокси недоступен";
                        return UpdateCache(server.Name, result);
                    }

                    // Авторизуемся
                    string password = CredentialManager.DecryptPassword(server.EncryptedPassword);
                    var loginResult = await client.LoginAsync(server.Login, password);
                    if (!loginResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Ошибка авторизации";
                        return UpdateCache(server.Name, result);
                    }

                    Logger.Info($"[Coverage] Запрос покрытия с {server.Name}...");

                    // Получаем покрытие через новый endpoint прокси
                    var coverageResult = await client.GetCoverageAsync();

                    if (coverageResult.Success)
                    {
                        result.Success = true;
                        result.TotalMeasurePoints = coverageResult.TotalMeasurePoints;
                        result.WithData = coverageResult.WithData;
                        result.CoveragePercent = coverageResult.CoveragePercent;
                        result.CheckedAt = DateTime.Now;

                        Logger.Info($"[Coverage] {server.Name}: {result.FormattedCoverage}");
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = coverageResult.ErrorMessage ?? "Ошибка получения покрытия";
                    }

                    return UpdateCache(server.Name, result);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Coverage] Ошибка получения покрытия с {server.Name}: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return UpdateCache(server.Name, result);
            }
        }

        /// <summary>
        /// Обновить покрытие для всех серверов (локальный + удалённые)
        /// </summary>
        public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("[Coverage] Начало обновления покрытия для всех серверов");

            // Локальный сервер
            await GetLocalCoverageAsync(cancellationToken);

            // Удалённые серверы (параллельно)
            var remoteServers = SettingsService.Instance.Servers.ToList();
            var tasks = remoteServers.Select(s => GetRemoteCoverageAsync(s, cancellationToken));
            await Task.WhenAll(tasks);

            Logger.Info("[Coverage] Обновление покрытия завершено");
        }

        /// <summary>
        /// Запустить фоновое обновление покрытия
        /// </summary>
        public void StartBackgroundRefresh()
        {
            Task.Run(async () =>
            {
                try
                {
                    await RefreshAllAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Coverage] Ошибка фонового обновления: {ex.Message}");
                }
            });
        }

        private DataCoverageResult UpdateCache(string serverName, DataCoverageResult result)
        {
            _cache[serverName] = result;
            CoverageUpdated?.Invoke(this, result);
            return result;
        }
    }
}
