using System;

namespace LersReportProxy.Models
{
    /// <summary>
    /// Запрос на генерацию отчёта
    /// </summary>
    public class GenerateReportRequest
    {
        /// <summary>
        /// ID шаблона отчёта
        /// </summary>
        public int ReportId { get; set; }

        /// <summary>
        /// ID точек учёта (для ОДПУ)
        /// </summary>
        public int[] MeasurePointIds { get; set; }

        /// <summary>
        /// ID узлов/домов (для ИПУ)
        /// </summary>
        public int[] NodeIds { get; set; }

        /// <summary>
        /// Тип данных: Day, Hour, Month и т.д.
        /// </summary>
        public string DataType { get; set; } = "Day";

        /// <summary>
        /// Начало периода
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Конец периода
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Формат экспорта: Pdf, Xlsx, Xls, Rtf, Csv, Html
        /// </summary>
        public string Format { get; set; } = "Pdf";

        /// <summary>
        /// Тип сущности: "MeasurePoint" для ОДПУ, "House" для ИПУ.
        /// Если не указан, определяется автоматически по наличию NodeIds.
        /// </summary>
        public string ReportEntityType { get; set; }
    }
}
