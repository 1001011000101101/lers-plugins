namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Информация об объекте учёта (доме/узле)
    /// </summary>
    public class NodeInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; }

        /// <summary>
        /// Количество квартирных точек учёта (ИПУ)
        /// </summary>
        public int ApartmentPointsCount { get; set; }

        /// <summary>
        /// Количество общедомовых точек учёта (ОДПУ)
        /// </summary>
        public int BuildingPointsCount { get; set; }

        public override string ToString() => Title;
    }
}
