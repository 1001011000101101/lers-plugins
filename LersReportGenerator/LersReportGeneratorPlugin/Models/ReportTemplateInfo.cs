namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Report template information for UI display
    /// </summary>
    public class ReportTemplateInfo
    {
        /// <summary>
        /// Report ID from MeasurePointReport table (used for generation)
        /// </summary>
        public int ReportId { get; set; }

        /// <summary>
        /// Report template ID from ReportTemplate table
        /// </summary>
        public int ReportTemplateId { get; set; }

        /// <summary>
        /// Template title
        /// </summary>
        public string TemplateTitle { get; set; } = string.Empty;

        /// <summary>
        /// Report instance title (optional)
        /// </summary>
        public string InstanceTitle { get; set; }

        /// <summary>
        /// Display title - shows InstanceTitle if available, otherwise TemplateTitle
        /// </summary>
        public string DisplayTitle
        {
            get
            {
                return string.IsNullOrEmpty(InstanceTitle)
                    ? TemplateTitle
                    : InstanceTitle;
            }
        }

        public override string ToString() => DisplayTitle;
    }
}
