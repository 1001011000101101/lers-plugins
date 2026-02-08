using System;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Resource types for batch report generation
    /// </summary>
    public enum ResourceType
    {
        /// <summary>All resource types</summary>
        All = 0,

        /// <summary>Cold and hot water (SystemType 2, 4)</summary>
        Water = 1,

        /// <summary>Heat (SystemType 1)</summary>
        Heat = 2,

        /// <summary>Electricity (SystemType 32)</summary>
        Electricity = 3
    }

    public static class ResourceTypeExtensions
    {
        /// <summary>
        /// Get LERS SystemTypeId values for resource type
        /// </summary>
        public static int[] GetSystemTypeIds(this ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Water:
                    return new[] { 2, 4 };        // Hot water, Cold water
                case ResourceType.Heat:
                    return new[] { 1 };            // Heat
                case ResourceType.Electricity:
                    return new[] { 32 };           // Electricity
                case ResourceType.All:
                    return new[] { 1, 2, 4, 32 };
                default:
                    return new int[0];
            }
        }

        public static string GetDisplayName(this ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Water:
                    return "ХВС/ГВС";
                case ResourceType.Heat:
                    return "Тепло";
                case ResourceType.Electricity:
                    return "Электричество";
                case ResourceType.All:
                    return "Все ресурсы";
                default:
                    return "Неизвестно";
            }
        }
    }
}
