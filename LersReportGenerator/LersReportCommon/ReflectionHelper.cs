using System;
using System.Collections.Concurrent;
using System.Linq;

namespace LersReportCommon
{
    /// <summary>
    /// Вспомогательный класс для работы с reflection.
    /// Кэширует найденные типы для повышения производительности.
    /// </summary>
    public static class ReflectionHelper
    {
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();
        private static readonly string[] CommonPrefixes = { "Lers.Reports.", "Lers.", "Lers.Core." };

        /// <summary>
        /// Найти тип по имени в загруженных сборках ЛЭРС (с кэшированием)
        /// </summary>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            return _typeCache.GetOrAdd(typeName, FindTypeInternal);
        }

        private static Type FindTypeInternal(string typeName)
        {
            // Сначала пробуем типичные namespace
            foreach (var prefix in CommonPrefixes)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("Lers") == true);

                foreach (var assembly in assemblies)
                {
                    var type = assembly.GetType(prefix + typeName);
                    if (type != null)
                    {
                        Logger.Debug($"Тип {typeName} найден: {type.FullName}");
                        return type;
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
                        Logger.Debug($"Тип {typeName} найден (полный поиск): {type.FullName}");
                        return type;
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    Logger.Debug($"Не удалось загрузить типы из сборки {asmName}: {ex.Message}");
                }
            }

            Logger.Warning($"Тип {typeName} не найден в сборках ЛЭРС");
            return null;
        }

        /// <summary>
        /// Безопасно получить значение свойства через reflection
        /// </summary>
        public static T GetPropertyValue<T>(object obj, string propertyName, T defaultValue = default(T))
        {
            if (obj == null || string.IsNullOrEmpty(propertyName))
                return defaultValue;

            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop == null)
                    return defaultValue;

                var value = prop.GetValue(obj);
                if (value == null)
                    return defaultValue;

                if (value is T typedValue)
                    return typedValue;

                // Проверяем совместимость типов
                if (typeof(T).IsAssignableFrom(value.GetType()))
                    return (T)value;

                // Попробуем конвертировать
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Безопасно получить значение свойства, пробуя несколько имён
        /// </summary>
        public static T GetPropertyValue<T>(object obj, string[] propertyNames, T defaultValue = default(T))
        {
            if (obj == null || propertyNames == null)
                return defaultValue;

            foreach (var propName in propertyNames)
            {
                var value = GetPropertyValue<T>(obj, propName, defaultValue);
                if (!Equals(value, defaultValue))
                    return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Безопасно спарсить enum значение
        /// </summary>
        public static object ParseEnum(Type enumType, string value, object fallback = null)
        {
            if (enumType == null || string.IsNullOrEmpty(value))
                return fallback;

            try
            {
                return Enum.Parse(enumType, value);
            }
            catch (ArgumentException)
            {
                Logger.Debug($"Enum значение '{value}' не найдено в {enumType.Name}");
                return fallback;
            }
        }

        /// <summary>
        /// Безопасно спарсить enum, пробуя несколько значений
        /// </summary>
        public static object ParseEnum(Type enumType, string[] values)
        {
            if (enumType == null || values == null || values.Length == 0)
                return null;

            foreach (var value in values)
            {
                var result = ParseEnum(enumType, value);
                if (result != null)
                    return result;
            }

            // Возвращаем первое значение enum как fallback
            var enumValues = Enum.GetValues(enumType);
            if (enumValues.Length > 0)
            {
                Logger.Debug($"Используем первое значение enum {enumType.Name}");
                return enumValues.GetValue(0);
            }

            return null;
        }

        /// <summary>
        /// Очистить кэш типов (при необходимости)
        /// </summary>
        public static void ClearCache()
        {
            _typeCache.Clear();
            Logger.Debug("Кэш типов очищен");
        }
    }
}
