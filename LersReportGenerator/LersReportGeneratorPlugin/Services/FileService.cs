using System;
using System.IO;
using System.IO.Compression;
using LersReportCommon;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Сервис для работы с файлами и архивами
    /// </summary>
    public static class FileService
    {
        /// <summary>
        /// Максимальная длина имени файла
        /// </summary>
        public const int MaxFileNameLength = 200;

        /// <summary>
        /// Очистить имя файла от недопустимых символов
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "report";

            var invalid = Path.GetInvalidFileNameChars();
            var parts = fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries);
            var sanitized = string.Join("_", parts);

            // Заменяем пробелы на подчёркивания
            sanitized = sanitized.Replace(" ", "_");

            // Ограничиваем длину
            if (sanitized.Length > MaxFileNameLength)
                sanitized = sanitized.Substring(0, MaxFileNameLength);

            return sanitized;
        }

        /// <summary>
        /// Создать временную папку для работы
        /// </summary>
        public static string CreateTempFolder(string prefix = "LersReports")
        {
            var folderPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(folderPath);
            Logger.Debug($"Создана временная папка: {folderPath}");
            return folderPath;
        }

        /// <summary>
        /// Безопасно удалить папку
        /// </summary>
        public static bool TryDeleteFolder(string folderPath, bool recursive = true)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return true;

            try
            {
                Directory.Delete(folderPath, recursive);
                Logger.Debug($"Удалена папка: {folderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Не удалось удалить папку {folderPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Безопасно удалить файл
        /// </summary>
        public static bool TryDeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return true;

            try
            {
                File.Delete(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Не удалось удалить файл {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Сохранить байты в файл
        /// </summary>
        public static void SaveToFile(byte[] content, string filePath)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, content);
            Logger.Debug($"Файл сохранён: {filePath} ({content.Length} байт)");
        }

        /// <summary>
        /// Создать ZIP-архив из папки
        /// </summary>
        public static string CreateZipArchive(string sourceFolder, string outputPath, string zipFileName)
        {
            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException($"Папка не найдена: {sourceFolder}");

            var zipPath = Path.Combine(outputPath, zipFileName);

            // Удаляем существующий файл
            TryDeleteFile(zipPath);

            ZipFile.CreateFromDirectory(sourceFolder, zipPath, CompressionLevel.Optimal, false);

            Logger.Info($"ZIP-архив создан: {zipPath}");
            return zipPath;
        }

        /// <summary>
        /// Сформировать имя файла отчёта
        /// </summary>
        public static string BuildReportFileName(
            string title,
            string templateName,
            DateTime startDate,
            DateTime endDate,
            string extension)
        {
            var baseName = string.IsNullOrEmpty(templateName)
                ? $"{title}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}"
                : $"{title}_{templateName}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";

            return SanitizeFileName(baseName) + extension;
        }

        /// <summary>
        /// Сформировать имя ZIP-архива
        /// </summary>
        public static string BuildZipFileName(
            string prefix,
            string resourceTypeName,
            DateTime startDate,
            DateTime endDate)
        {
            return SanitizeFileName($"Отчеты_{prefix}_{resourceTypeName}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.zip");
        }
    }
}
