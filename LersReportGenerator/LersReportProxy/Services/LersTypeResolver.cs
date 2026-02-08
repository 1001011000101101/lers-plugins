using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using LersReportCommon;

namespace LersReportProxy.Services
{
    /// <summary>
    /// Сервис для поиска типов ЛЭРС через reflection.
    /// Кэширует найденные типы для повторного использования.
    /// </summary>
    public static class LersTypeResolver
    {
        private static readonly string[] CommonPrefixes = { "Lers.Reports.", "Lers.", "Lers.Core." };
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();

        private static volatile bool _assembliesLoaded = false;
        private static readonly object _assembliesLock = new object();

        /// <summary>
        /// Найти тип ReportManager
        /// </summary>
        public static Type FindReportManagerType() => FindType("ReportManager");

        /// <summary>
        /// Найти тип ReportExportOptions
        /// </summary>
        public static Type FindExportOptionsType() => FindType("ReportExportOptions");

        /// <summary>
        /// Найти тип DeviceDataType
        /// </summary>
        public static Type FindDeviceDataTypeType() => FindType("DeviceDataType");

        /// <summary>
        /// Найти тип ReportType
        /// </summary>
        public static Type FindReportTypeType() => FindType("ReportType");

        /// <summary>
        /// Найти тип ЛЭРС по имени.
        /// Сначала ищет в типичных namespace (Lers.Reports, Lers, Lers.Core),
        /// затем выполняет полный поиск по всем сборкам Lers.
        /// </summary>
        /// <param name="typeName">Имя типа (без namespace)</param>
        /// <returns>Найденный тип или null</returns>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            return _typeCache.GetOrAdd(typeName, FindTypeInternal);
        }

        /// <summary>
        /// Убедиться, что сборки ЛЭРС загружены в AppDomain
        /// </summary>
        public static void EnsureLersAssembliesLoaded()
        {
            if (_assembliesLoaded) return;

            lock (_assembliesLock)
            {
                if (_assembliesLoaded) return; // Double-checked locking

                // Принудительно загружаем нужные сборки
                string[] assemblies = { "Lers.Reports", "Lers.Core", "Lers.System" };
                foreach (var asmName in assemblies)
                {
                    try
                    {
                        Assembly.Load(asmName);
                        Logger.Debug($"Сборка {asmName} загружена");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Не удалось загрузить {asmName}: {ex.Message}");
                    }
                }

                _assembliesLoaded = true;
            }
        }

        private static Type FindTypeInternal(string typeName)
        {
            // Убедимся что сборки загружены
            EnsureLersAssembliesLoaded();

            // Сначала пробуем типичные namespace
            foreach (var prefix in CommonPrefixes)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("Lers") == true);

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var type = assembly.GetType(prefix + typeName);
                        if (type != null)
                        {
                            Logger.Debug($"FindType: {typeName} найден как {type.FullName}");
                            return type;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Тип может отсутствовать в сборке или сборка может быть недоступна
                        Logger.Debug($"FindType: не удалось получить тип {prefix}{typeName} из {assembly.GetName().Name}: {ex.Message}");
                    }
                }
            }

            // Ищем по имени во всех сборках Lers
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = assembly.GetName().Name;
                if (asmName == null || !asmName.StartsWith("Lers"))
                    continue;

                try
                {
                    var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (type != null)
                    {
                        Logger.Debug($"FindType: {typeName} найден (полный поиск) как {type.FullName}");
                        return type;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // GetTypes() бросает ReflectionTypeLoadException если не все типы могут быть загружены
                    // Это нормально для сборок ЛЭРС с опциональными зависимостями
                    Logger.Debug($"FindType: ReflectionTypeLoadException при поиске {typeName} в {asmName}");
                }
            }

            Logger.Warning($"FindType: тип {typeName} НЕ найден");
            return null;
        }
    }
}
