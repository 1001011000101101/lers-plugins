namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Measure point information for UI display
    /// </summary>
    public class MeasurePointInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SystemTypeId { get; set; }
        public string SystemTypeName { get; set; } = string.Empty;
        public int? NodeId { get; set; }
        public string NodeTitle { get; set; }

        /// <summary>
        /// ID помещения (квартиры). Если NULL - это общедомовая точка (ОДПУ).
        /// Если NOT NULL - это квартирная точка (ИПУ).
        /// </summary>
        public int? PersonalAccountId { get; set; }

        /// <summary>
        /// Является ли точка квартирной (ИПУ)
        /// </summary>
        public bool IsApartment => PersonalAccountId.HasValue;

        /// <summary>
        /// Является ли точка общедомовой (ОДПУ)
        /// </summary>
        public bool IsBuilding => !PersonalAccountId.HasValue;

        public override string ToString() => Title;
    }
}
