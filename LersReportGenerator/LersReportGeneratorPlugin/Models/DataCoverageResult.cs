using System;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Результат проверки покрытия данными
    /// </summary>
    public class DataCoverageResult
    {
        /// <summary>
        /// Название сервера
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// Успешно ли получены данные
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Общее количество точек учёта
        /// </summary>
        public int TotalMeasurePoints { get; set; }

        /// <summary>
        /// Количество точек с данными (State != NoData)
        /// </summary>
        public int WithData { get; set; }

        /// <summary>
        /// Процент покрытия
        /// </summary>
        public double CoveragePercent { get; set; }

        /// <summary>
        /// Время проверки
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Сообщение об ошибке (если Success = false)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Форматированная строка покрытия для колонки таблицы (только проценты)
        /// </summary>
        public string FormattedCoverage
        {
            get
            {
                if (!Success)
                    return ErrorMessage ?? "Ошибка";

                if (TotalMeasurePoints == 0)
                    return "Нет точек";

                return $"{CoveragePercent:F1}%";
            }
        }

        /// <summary>
        /// Полная строка покрытия для заголовка (с процентами)
        /// </summary>
        public string FormattedCoverageFull
        {
            get
            {
                if (!Success)
                    return ErrorMessage ?? "Ошибка";

                if (TotalMeasurePoints == 0)
                    return "нет точек учёта";

                return $"{CoveragePercent:F1}% ({WithData}/{TotalMeasurePoints})";
            }
        }
    }
}
