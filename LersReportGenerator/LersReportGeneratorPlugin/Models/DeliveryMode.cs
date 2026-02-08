namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Delivery modes for batch report generation
    /// </summary>
    public enum DeliveryMode
    {
        /// <summary>Save all reports as separate files in a folder</summary>
        SeparateFiles,

        /// <summary>Pack all reports into a ZIP archive</summary>
        Zip
    }

    public static class DeliveryModeExtensions
    {
        public static string GetDisplayName(this DeliveryMode mode)
        {
            switch (mode)
            {
                case DeliveryMode.SeparateFiles:
                    return "Отдельные файлы";
                case DeliveryMode.Zip:
                    return "ZIP-архив";
                default:
                    return "Отдельные файлы";
            }
        }
    }
}
