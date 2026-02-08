using System;

namespace LersReportCommon
{
    /// <summary>
    /// Общие константы для проектов LersReport
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Порт прокси-службы по умолчанию ("LERS" на T9 клавиатуре)
        /// </summary>
        public const int DefaultProxyPort = 5377;

        /// <summary>
        /// Таймаут сессии
        /// </summary>
        public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Количество дней хранения логов
        /// </summary>
        public const int LogRetentionDays = 7;

        /// <summary>
        /// Таймаут генерации отчёта
        /// </summary>
        public static readonly TimeSpan ReportGenerationTimeout = TimeSpan.FromMinutes(3);

        /// <summary>
        /// Максимальный диапазон дат для отчёта (дней)
        /// </summary>
        public const int MaxDateRangeDays = 366;

        /// <summary>
        /// Состояния точек учёта (MeasurePointState enum values)
        /// </summary>
        public static class MeasurePointState
        {
            public const int NoData = 0;
            public const int Normal = 1;
            public const int Warning = 2;
            public const int Error = 3;
        }

        /// <summary>
        /// Типы точек учёта для фильтрации
        /// </summary>
        public static class MeasurePointTypes
        {
            public const string Regular = "Regular";
            public const string Communal = "Communal";
        }

        /// <summary>
        /// Типы узлов
        /// </summary>
        public static class NodeTypes
        {
            public const string House = "House";
        }

        /// <summary>
        /// Типы данных для API
        /// </summary>
        public static class DataTypes
        {
            public const string Day = "Day";
        }

        /// <summary>
        /// Форматы экспорта
        /// </summary>
        public static class ExportFormats
        {
            public const string Pdf = "Pdf";
            public const string Xlsx = "Xlsx";
        }

        /// <summary>
        /// Типы сущностей отчётов (ReportEntityType enum values)
        /// </summary>
        public static class ReportEntityType
        {
            public const int MeasurePoint = 1;
            public const int House = 4;
        }

        /// <summary>
        /// Типы отчётов (ReportType enum values)
        /// </summary>
        public static class ReportType
        {
            /// <summary>Сводный отчёт по квартирным точкам (ИПУ)</summary>
            public const int CommunalMeasurePointSummary = 7;
        }

        /// <summary>
        /// Пороги покрытия данными для цветовой индикации
        /// </summary>
        public static class CoverageThresholds
        {
            /// <summary>Отличное покрытие (зелёный)</summary>
            public const double Excellent = 80.0;
            /// <summary>Приемлемое покрытие (оранжевый)</summary>
            public const double Acceptable = 50.0;
        }

        /// <summary>
        /// Имена свойств ЛЭРС API для reflection
        /// </summary>
        public static class LersPropertyNames
        {
            public const string MeasurePoints = "MeasurePoints";
            public const string Nodes = "Nodes";
            public const string Reports = "Reports";
            public const string Report = "Report";
            public const string ReportTemplate = "ReportTemplate";
            public const string Id = "Id";
            public const string Title = "Title";
            public const string FullTitle = "FullTitle";
            public const string NodeId = "NodeId";
            public const string SystemTypeId = "SystemTypeId";
            public const string PersonalAccountId = "PersonalAccountId";
            public const string State = "State";
            public const string Type = "Type";
            public const string Address = "Address";
        }

        /// <summary>
        /// Имена методов ЛЭРС API для reflection
        /// </summary>
        public static class LersMethodNames
        {
            public const string GetListAsync = "GetListAsync";
            public const string GetByIdAsync = "GetByIdAsync";
            public const string RefreshAsync = "RefreshAsync";
        }

        /// <summary>
        /// Имена типов ЛЭРС API для reflection
        /// </summary>
        public static class LersTypeNames
        {
            public const string MeasurePointInfoFlags = "MeasurePointInfoFlags";
            public const string ReportManager = "ReportManager";
            public const string ReportExportOptions = "ReportExportOptions";
            public const string ReportExportFormat = "ReportExportFormat";
            public const string DeviceDataType = "DeviceDataType";
            public const string ReportEntityType = "ReportEntityType";
        }

        /// <summary>
        /// Авторизация
        /// </summary>
        public static class Auth
        {
            public const string BearerPrefix = "Bearer ";
        }

        /// <summary>
        /// HTTP коды ответов
        /// </summary>
        public static class HttpStatus
        {
            public const int Ok = 200;
            public const int BadRequest = 400;
            public const int Unauthorized = 401;
            public const int NotFound = 404;
            public const int InternalServerError = 500;
        }

        /// <summary>
        /// API endpoints прокси-службы
        /// </summary>
        public static class Endpoints
        {
            public const string Health = "lersproxy/health";
            public const string Version = "lersproxy/version";
            public const string Login = "lersproxy/login";
            public const string Logout = "lersproxy/logout";
            public const string MeasurePoints = "lersproxy/measurepoints";
            public const string MeasurePointsCoverage = "lersproxy/measurepoints/coverage";
            public const string Nodes = "lersproxy/nodes";
            public const string ReportsTemplates = "lersproxy/reports/templates";
            public const string ReportsApartmentTemplates = "lersproxy/reports/apartment-templates";
            public const string ReportsGenerate = "lersproxy/reports/generate";
        }
    }
}
