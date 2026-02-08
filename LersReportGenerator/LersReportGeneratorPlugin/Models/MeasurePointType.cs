namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Тип точек учёта
    /// </summary>
    public enum MeasurePointType
    {
        /// <summary>
        /// Общедомовые точки учёта (ОДПУ)
        /// </summary>
        Building,

        /// <summary>
        /// Квартирные точки учёта (ИПУ)
        /// </summary>
        Apartment
    }

    public static class MeasurePointTypeExtensions
    {
        public static string GetDisplayName(this MeasurePointType type)
        {
            switch (type)
            {
                case MeasurePointType.Building:
                    return "Общедомовые (ОДПУ)";
                case MeasurePointType.Apartment:
                    return "Квартирные (ИПУ)";
                default:
                    return type.ToString();
            }
        }
    }
}
