using System;
using System.Collections.Generic;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Статус кэшированных данных
    /// </summary>
    public enum CacheStatus
    {
        /// <summary>Успешно загружено с данными</summary>
        LoadedWithData,

        /// <summary>Успешно загружено, но 0 шаблонов (сервер ответил корректно)</summary>
        LoadedEmpty,

        /// <summary>Ошибка при загрузке (сервер недоступен или другая ошибка)</summary>
        Error
    }

    /// <summary>
    /// Кэш шаблонов отчётов. Шаблоны редко меняются, поэтому кэшируем их
    /// на время сессии работы плагина.
    /// </summary>
    public static class TemplateCache
    {
        private static readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private static readonly object _lock = new object();

        private class CacheEntry
        {
            public List<ReportTemplateInfo> Templates { get; set; }
            public DateTime LoadedAt { get; set; }
            public CacheStatus Status { get; set; }
        }

        /// <summary>
        /// Формирует ключ кэша
        /// </summary>
        /// <param name="serverName">Имя сервера (null или пустая строка = локальный)</param>
        /// <param name="pointType">Тип точки учёта</param>
        /// <param name="resourceType">Тип ресурса</param>
        private static string MakeKey(string serverName, MeasurePointType pointType, ResourceType resourceType)
        {
            var server = string.IsNullOrEmpty(serverName) ? "local" : serverName;
            return $"{server}|{pointType}|{resourceType}";
        }

        /// <summary>
        /// Проверяет, есть ли шаблоны в кэше
        /// </summary>
        public static bool Has(string serverName, MeasurePointType pointType, ResourceType resourceType)
        {
            var key = MakeKey(serverName, pointType, resourceType);
            lock (_lock)
            {
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Получает шаблоны из кэша (или null, если не найдены)
        /// </summary>
        public static List<ReportTemplateInfo> Get(string serverName, MeasurePointType pointType, ResourceType resourceType)
        {
            var key = MakeKey(serverName, pointType, resourceType);
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    string statusText = entry.Status == CacheStatus.Error ? "ошибка" :
                                       entry.Status == CacheStatus.LoadedEmpty ? "пусто" : "данные";
                    Logger.Info($"[TemplateCache] Возвращаем из кэша: {key} ({entry.Templates.Count} шаблонов, {statusText}, загружено {entry.LoadedAt:HH:mm:ss})");
                    return entry.Templates;
                }
                return null;
            }
        }

        /// <summary>
        /// Проверяет статус кэша для сервера
        /// </summary>
        public static CacheStatus? GetStatus(string serverName, MeasurePointType pointType, ResourceType resourceType)
        {
            var key = MakeKey(serverName, pointType, resourceType);
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    return entry.Status;
                }
                return null;
            }
        }

        /// <summary>
        /// Сохраняет шаблоны в кэш
        /// </summary>
        public static void Set(string serverName, MeasurePointType pointType, ResourceType resourceType, List<ReportTemplateInfo> templates, CacheStatus status)
        {
            var key = MakeKey(serverName, pointType, resourceType);
            lock (_lock)
            {
                _cache[key] = new CacheEntry
                {
                    Templates = templates,
                    LoadedAt = DateTime.Now,
                    Status = status
                };

                string statusText = status == CacheStatus.Error ? "ошибка" :
                                   status == CacheStatus.LoadedEmpty ? "пусто" : "данные";
                Logger.Info($"[TemplateCache] Сохранено в кэш: {key} ({templates.Count} шаблонов, статус: {statusText})");
            }
        }

        /// <summary>
        /// Очищает кэш для указанного сервера (или весь кэш, если serverName = null)
        /// </summary>
        /// <param name="serverName">Имя сервера для очистки, или null для очистки всего кэша</param>
        public static void Invalidate(string serverName = null)
        {
            lock (_lock)
            {
                if (serverName == null)
                {
                    _cache.Clear();
                    Logger.Info("[TemplateCache] Кэш полностью очищен");
                }
                else
                {
                    var prefix = string.IsNullOrEmpty(serverName) ? "local|" : $"{serverName}|";
                    var keysToRemove = new List<string>();
                    foreach (var key in _cache.Keys)
                    {
                        if (key.StartsWith(prefix))
                            keysToRemove.Add(key);
                    }
                    foreach (var key in keysToRemove)
                    {
                        _cache.Remove(key);
                    }
                    Logger.Info($"[TemplateCache] Кэш очищен для сервера: {serverName ?? "local"} ({keysToRemove.Count} записей)");
                }
            }
        }

        /// <summary>
        /// Возвращает количество записей в кэше
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }
    }
}
