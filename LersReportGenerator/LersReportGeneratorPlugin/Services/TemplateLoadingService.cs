using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Результат загрузки шаблонов
    /// </summary>
    public class TemplateLoadingResult
    {
        public List<ReportTemplateInfo> Templates { get; set; } = new List<ReportTemplateInfo>();

        /// <summary>
        /// Сообщение для отображения, если шаблоны не найдены (null если шаблоны есть)
        /// </summary>
        public string EmptyMessage { get; set; }

        /// <summary>
        /// Данные получены из кэша
        /// </summary>
        public bool FromCache { get; set; }
    }

    /// <summary>
    /// Сервис загрузки шаблонов отчётов.
    /// Инкапсулирует маршрутизацию по комбинации (тип точки × режим сервера × тип ресурса),
    /// параллельную загрузку со всех серверов и кэширование.
    /// </summary>
    public class TemplateLoadingService
    {
        private readonly ReportGeneratorService _reportService;
        private readonly RemoteTemplateLoader _templateLoader;

        public TemplateLoadingService(
            ReportGeneratorService reportService,
            RemoteTemplateLoader templateLoader)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _templateLoader = templateLoader ?? throw new ArgumentNullException(nameof(templateLoader));
        }

        /// <summary>
        /// Единая точка входа — загружает шаблоны, автоматически выбирая источник
        /// по комбинации параметров.
        /// </summary>
        public async Task<TemplateLoadingResult> LoadTemplatesAsync(
            MeasurePointType pointType,
            ResourceType resourceType,
            bool isRemoteMode,
            ServerConfig selectedRemoteServer,
            bool allServersMode,
            Action<string> progressCallback = null)
        {
            // Проверяем кэш
            string cacheServerName = GetCacheServerName(allServersMode, isRemoteMode, selectedRemoteServer);
            var cachedTemplates = TemplateCache.Get(cacheServerName, pointType, resourceType);
            if (cachedTemplates != null)
            {
                return new TemplateLoadingResult
                {
                    Templates = cachedTemplates,
                    FromCache = true
                };
            }

            progressCallback?.Invoke("Загрузка списка отчётов...");

            var result = await LoadTemplatesFromSourceAsync(
                pointType, resourceType, isRemoteMode, selectedRemoteServer, allServersMode, progressCallback);

            // Сохраняем в кэш если есть шаблоны
            if (result.Templates.Count > 0)
            {
                TemplateCache.Set(cacheServerName, pointType, resourceType, result.Templates);
            }

            return result;
        }

        private async Task<TemplateLoadingResult> LoadTemplatesFromSourceAsync(
            MeasurePointType pointType,
            ResourceType resourceType,
            bool isRemoteMode,
            ServerConfig selectedRemoteServer,
            bool allServersMode,
            Action<string> progressCallback)
        {
            if (pointType == MeasurePointType.Apartment && isRemoteMode && selectedRemoteServer != null)
            {
                return await LoadRemoteApartmentTemplatesAsync(selectedRemoteServer);
            }

            if (pointType == MeasurePointType.Apartment && allServersMode)
            {
                return await LoadTemplatesFromAllServersAsync(pointType, resourceType, progressCallback);
            }

            if (pointType == MeasurePointType.Apartment)
            {
                return await LoadLocalApartmentTemplatesAsync(resourceType);
            }

            if (isRemoteMode && selectedRemoteServer != null)
            {
                return await LoadRemoteOdpuTemplatesAsync(selectedRemoteServer, resourceType);
            }

            if (allServersMode)
            {
                return await LoadTemplatesFromAllServersAsync(pointType, resourceType, progressCallback);
            }

            return await LoadLocalOdpuTemplatesAsync(pointType, resourceType);
        }

        /// <summary>
        /// ИПУ + удалённый сервер → загружаем через прокси
        /// </summary>
        private async Task<TemplateLoadingResult> LoadRemoteApartmentTemplatesAsync(ServerConfig server)
        {
            var templates = await _templateLoader.LoadIpuTemplatesAsync(server);

            if (templates.Count == 0)
            {
                return new TemplateLoadingResult
                {
                    EmptyMessage = "Нет отчётов ИПУ на сервере"
                };
            }

            return new TemplateLoadingResult { Templates = templates };
        }

        /// <summary>
        /// ИПУ + локальный сервер → получаем отчёты с ReportType = 7 через API
        /// </summary>
        private async Task<TemplateLoadingResult> LoadLocalApartmentTemplatesAsync(ResourceType resourceType)
        {
            var templates = await _reportService.GetApartmentReportTemplatesAsync();

            if (templates.Count == 0)
            {
                // Fallback: получаем из первой квартирной точки
                var apartmentPoints = await _reportService.GetMeasurePointsByResourceTypeAsync(resourceType, MeasurePointType.Apartment);
                if (apartmentPoints.Count > 0)
                {
                    templates = await _reportService.GetReportTemplatesAsync(apartmentPoints[0].Id);
                }
            }

            if (templates.Count == 0)
            {
                return new TemplateLoadingResult
                {
                    EmptyMessage = "Нет отчётов для квартирных точек"
                };
            }

            return new TemplateLoadingResult { Templates = templates };
        }

        /// <summary>
        /// ОДПУ + удалённый сервер → загружаем шаблоны через прокси
        /// </summary>
        private async Task<TemplateLoadingResult> LoadRemoteOdpuTemplatesAsync(ServerConfig server, ResourceType resourceType)
        {
            var templates = await _templateLoader.LoadOdpuTemplatesAsync(server, resourceType);

            if (templates.Count == 0)
            {
                return new TemplateLoadingResult
                {
                    EmptyMessage = "Нет ОДПУ отчётов на сервере"
                };
            }

            return new TemplateLoadingResult { Templates = templates };
        }

        /// <summary>
        /// ОДПУ + локальный сервер — собираем шаблоны со ВСЕХ точек ОДПУ
        /// </summary>
        private async Task<TemplateLoadingResult> LoadLocalOdpuTemplatesAsync(MeasurePointType pointType, ResourceType resourceType)
        {
            var templates = await _reportService.GetAggregatedOdpuTemplatesAsync(resourceType);

            if (templates.Count == 0)
            {
                // Проверяем, есть ли вообще ОДПУ точки
                var measurePoints = await _reportService.GetMeasurePointsByResourceTypeAsync(resourceType, pointType);
                if (measurePoints.Count == 0)
                {
                    return new TemplateLoadingResult
                    {
                        EmptyMessage = "Нет общедомовых точек учёта"
                    };
                }
            }

            return new TemplateLoadingResult { Templates = templates };
        }

        /// <summary>
        /// Загружает шаблоны отчётов со всех серверов (локальный + удалённые) и объединяет их.
        /// Уникализация по названию шаблона (InstanceTitle).
        /// </summary>
        private async Task<TemplateLoadingResult> LoadTemplatesFromAllServersAsync(
            MeasurePointType pointType,
            ResourceType resourceType,
            Action<string> progressCallback)
        {
            var allTemplates = new ConcurrentDictionary<string, ReportTemplateInfo>(StringComparer.OrdinalIgnoreCase);
            var errors = new ConcurrentBag<string>();
            var servers = SettingsService.Instance.Servers.ToList();
            int totalServers = servers.Count + 1;
            int completedServers = 0;

            void UpdateProgress(string serverName)
            {
                int completed = Interlocked.Increment(ref completedServers);
                progressCallback?.Invoke($"Загрузка шаблонов: {completed}/{totalServers} ({serverName})");
            }

            progressCallback?.Invoke($"Загрузка шаблонов со всех серверов (0/{totalServers})...");

            var tasks = new List<Task>();

            // Задача для локального сервера
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    List<ReportTemplateInfo> localTemplates;
                    if (pointType == MeasurePointType.Apartment)
                    {
                        localTemplates = await _reportService.GetApartmentReportTemplatesAsync();
                    }
                    else
                    {
                        localTemplates = await _reportService.GetAggregatedOdpuTemplatesAsync(resourceType);
                    }

                    foreach (var t in localTemplates)
                    {
                        string key = t.InstanceTitle ?? t.TemplateTitle ?? $"Report_{t.ReportId}";
                        allTemplates.TryAdd(key, t);
                    }
                    Logger.Info($"Локальный сервер: загружено {localTemplates.Count} шаблонов");
                    UpdateProgress("Локальный");
                }
                catch (Exception ex)
                {
                    errors.Add($"Локальный: {ex.Message}");
                    Logger.Error($"Ошибка загрузки шаблонов с локального сервера: {ex.Message}");
                    UpdateProgress("Локальный (ошибка)");
                }
            }));

            // Задачи для удалённых серверов
            foreach (var server in servers)
            {
                var serverCopy = server;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        List<ReportTemplateInfo> remoteTemplates;
                        if (pointType == MeasurePointType.Apartment)
                        {
                            remoteTemplates = await _templateLoader.LoadIpuTemplatesAsync(serverCopy);
                        }
                        else
                        {
                            remoteTemplates = await _templateLoader.LoadOdpuTemplatesAsync(serverCopy, resourceType);
                        }

                        foreach (var t in remoteTemplates)
                        {
                            string key = t.InstanceTitle ?? t.TemplateTitle ?? $"Report_{t.ReportId}";
                            allTemplates.TryAdd(key, t);
                        }
                        Logger.Info($"{serverCopy.Name}: загружено {remoteTemplates.Count} шаблонов");
                        UpdateProgress(serverCopy.Name);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{serverCopy.Name}: {ex.Message}");
                        Logger.Error($"Ошибка загрузки шаблонов с {serverCopy.Name}: {ex.Message}");
                        UpdateProgress($"{serverCopy.Name} (ошибка)");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            if (errors.Count > 0 && allTemplates.Count == 0)
            {
                Logger.Warning($"Не удалось загрузить шаблоны: {string.Join("; ", errors.ToArray())}");
            }

            Logger.Info($"Всего уникальных шаблонов со всех серверов: {allTemplates.Count}");

            var templates = allTemplates.Values.ToList();

            if (templates.Count == 0)
            {
                string emptyMsg = pointType == MeasurePointType.Apartment
                    ? "Нет отчётов ИПУ ни на одном сервере"
                    : "Нет общедомовых точек учёта";

                return new TemplateLoadingResult { EmptyMessage = emptyMsg };
            }

            return new TemplateLoadingResult { Templates = templates };
        }

        /// <summary>
        /// Возвращает имя сервера для ключа кэша
        /// </summary>
        private static string GetCacheServerName(bool allServersMode, bool isRemoteMode, ServerConfig selectedRemoteServer)
        {
            if (allServersMode)
                return "__all_servers__";
            if (isRemoteMode && selectedRemoteServer != null)
                return selectedRemoteServer.Name;
            return null; // локальный сервер
        }
    }
}
