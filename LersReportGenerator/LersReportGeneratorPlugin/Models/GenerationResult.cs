using System;
using System.Collections.Generic;
using System.Linq;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Result of a single report generation
    /// </summary>
    public class GenerationResult
    {
        public MeasurePointInfo MeasurePoint { get; set; }
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string ErrorMessage { get; set; }
        public long FileSize { get; set; }

        /// <summary>
        /// Сервер был пропущен (нет данных для этого типа отчёта)
        /// </summary>
        public bool IsSkipped { get; set; }
    }

    /// <summary>
    /// Summary of batch generation results
    /// </summary>
    public class BatchGenerationSummary
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<GenerationResult> Results { get; set; } = new List<GenerationResult>();
        public string ZipFilePath { get; set; }

        public List<GenerationResult> FailedResults
        {
            get { return Results.Where(r => !r.Success).ToList(); }
        }

        /// <summary>
        /// Серверы, которые были пропущены (нет данных)
        /// </summary>
        public List<GenerationResult> SkippedResults
        {
            get { return Results.Where(r => r.IsSkipped).ToList(); }
        }

        public int SkippedCount
        {
            get { return Results.Count(r => r.IsSkipped); }
        }

        /// <summary>
        /// Форматирует текст результатов генерации для отображения пользователю
        /// </summary>
        public string FormatResultMessage(DeliveryMode deliveryMode, TimeSpan elapsed)
        {
            // Считаем только реальные отчёты (без пропущенных серверов)
            int actualSuccess = Results.Count(r => r.Success && !r.IsSkipped);
            int actualTotal = Results.Count(r => !r.IsSkipped);

            // Форматируем время
            string timeStr;
            if (elapsed.TotalMinutes >= 1)
                timeStr = $"{(int)elapsed.TotalMinutes} мин {elapsed.Seconds} сек";
            else
                timeStr = $"{elapsed.TotalSeconds:F1} сек";

            var msg = $"Генерация завершена!\n\n" +
                      $"Сгенерировано: {actualSuccess}\n" +
                      $"Время: {timeStr}";

            if (actualTotal > actualSuccess)
            {
                msg += $"\nОшибки: {FailedCount}";
            }

            // Показываем информацию о пропущенных серверах
            if (SkippedCount > 0)
            {
                msg += "\n\nПропущенные серверы:\n" + string.Join("\n",
                    SkippedResults
                        .Select(r => $"• {r.ErrorMessage}")
                        .Take(5));
            }

            if (deliveryMode == DeliveryMode.Zip && !string.IsNullOrEmpty(ZipFilePath))
            {
                msg += $"\n\nАрхив: {ZipFilePath}";
            }

            if (FailedCount > 0)
            {
                msg += "\n\nОшибки:\n" + string.Join("\n",
                    FailedResults
                        .Where(r => !r.IsSkipped)
                        .Select(r => $"• {r.MeasurePoint?.Title ?? "?"}: {r.ErrorMessage}")
                        .Take(10));

                if (FailedCount > 10)
                    msg += $"\n... и ещё {FailedCount - 10}";
            }

            return msg;
        }
    }
}
