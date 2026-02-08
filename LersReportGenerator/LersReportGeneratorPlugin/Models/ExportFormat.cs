namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Report export formats (values match LERS ReportExportFormat enum)
    /// </summary>
    public enum ExportFormat
    {
        PDF = 0,
        RTF = 1,
        XLSX = 2,
        XLS = 3,
        CSV = 4,
        HTML = 5
    }

    public static class ExportFormatExtensions
    {
        public static string GetFileExtension(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.PDF:
                    return ".pdf";
                case ExportFormat.RTF:
                    return ".rtf";
                case ExportFormat.XLSX:
                    return ".xlsx";
                case ExportFormat.XLS:
                    return ".xls";
                case ExportFormat.CSV:
                    return ".csv";
                case ExportFormat.HTML:
                    return ".html";
                default:
                    return ".pdf";
            }
        }

        public static string GetDisplayName(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.PDF:
                    return "PDF";
                case ExportFormat.XLSX:
                    return "Excel (XLSX)";
                case ExportFormat.XLS:
                    return "Excel (XLS)";
                case ExportFormat.RTF:
                    return "RTF";
                case ExportFormat.CSV:
                    return "CSV";
                case ExportFormat.HTML:
                    return "HTML";
                default:
                    return "PDF";
            }
        }

        public static string GetContentType(this ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.PDF:
                    return "application/pdf";
                case ExportFormat.RTF:
                    return "application/rtf";
                case ExportFormat.XLSX:
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ExportFormat.XLS:
                    return "application/vnd.ms-excel";
                case ExportFormat.CSV:
                    return "text/csv";
                case ExportFormat.HTML:
                    return "text/html";
                default:
                    return "application/pdf";
            }
        }
    }
}
