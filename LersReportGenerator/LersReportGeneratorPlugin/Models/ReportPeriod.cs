using System;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Report period options
    /// </summary>
    public enum ReportPeriod
    {
        Today,
        Yesterday,
        CurrentWeek,
        CurrentMonth,
        PreviousMonth,
        Custom
    }

    public static class ReportPeriodExtensions
    {
        /// <summary>
        /// Get start and end dates for the period
        /// </summary>
        public static Tuple<DateTime, DateTime> GetDates(
            this ReportPeriod period,
            DateTime? customStart = null,
            DateTime? customEnd = null)
        {
            var today = DateTime.Today;

            switch (period)
            {
                case ReportPeriod.Today:
                    return Tuple.Create(today, today);

                case ReportPeriod.Yesterday:
                    return Tuple.Create(today.AddDays(-1), today.AddDays(-1));

                case ReportPeriod.CurrentWeek:
                    var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                    return Tuple.Create(startOfWeek, today);

                case ReportPeriod.CurrentMonth:
                    return Tuple.Create(new DateTime(today.Year, today.Month, 1), today);

                case ReportPeriod.PreviousMonth:
                    var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                    return Tuple.Create(firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1));

                case ReportPeriod.Custom:
                    return Tuple.Create(
                        customStart ?? today.AddMonths(-1),
                        customEnd ?? today);

                default:
                    return Tuple.Create(today.AddMonths(-1), today);
            }
        }

        public static string GetDisplayName(this ReportPeriod period)
        {
            switch (period)
            {
                case ReportPeriod.Today:
                    return "Сегодня";
                case ReportPeriod.Yesterday:
                    return "Вчера";
                case ReportPeriod.CurrentWeek:
                    return "Текущая неделя";
                case ReportPeriod.CurrentMonth:
                    return "Текущий месяц";
                case ReportPeriod.PreviousMonth:
                    return "Прошлый месяц";
                case ReportPeriod.Custom:
                    return "Произвольный период";
                default:
                    return "Неизвестно";
            }
        }
    }
}
