using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Интерфейс сервиса генерации отчётов
    /// </summary>
    public interface IReportGeneratorService : IDisposable
    {
        #region Получение точек учёта

        /// <summary>
        /// Получить точки учёта с пагинацией и поиском
        /// </summary>
        Task<List<MeasurePointInfo>> GetMeasurePointsAsync(int offset = 0, int limit = 20, string searchQuery = null);

        /// <summary>
        /// Получить общее количество точек учёта
        /// </summary>
        Task<int> GetMeasurePointsCountAsync(string searchQuery = null);

        /// <summary>
        /// Получить точки учёта по типу ресурса
        /// </summary>
        Task<List<MeasurePointInfo>> GetMeasurePointsByResourceTypeAsync(ResourceType resourceType);

        /// <summary>
        /// Получить точки учёта по типу ресурса и типу точки
        /// </summary>
        Task<List<MeasurePointInfo>> GetMeasurePointsByResourceTypeAsync(ResourceType resourceType, MeasurePointType? pointType);

        #endregion

        #region Работа с шаблонами отчётов

        /// <summary>
        /// Получить шаблоны отчётов для точки учёта
        /// </summary>
        Task<List<ReportTemplateInfo>> GetReportTemplatesAsync(int measurePointId);

        /// <summary>
        /// Найти шаблон по имени
        /// </summary>
        Task<ReportTemplateInfo> FindReportTemplateByNameAsync(int measurePointId, string templateName);

        /// <summary>
        /// Найти шаблон по ID шаблона
        /// </summary>
        Task<ReportTemplateInfo> FindReportTemplateByIdAsync(int measurePointId, int reportTemplateId);

        /// <summary>
        /// Найти отчёт по ReportId
        /// </summary>
        Task<ReportTemplateInfo> FindReportByIdAsync(int measurePointId, int reportId);

        /// <summary>
        /// Получить агрегированные шаблоны ОДПУ
        /// </summary>
        Task<List<ReportTemplateInfo>> GetAggregatedOdpuTemplatesAsync(ResourceType resourceType);

        /// <summary>
        /// Получить шаблоны для квартирных отчётов
        /// </summary>
        Task<List<ReportTemplateInfo>> GetApartmentReportTemplatesAsync();

        #endregion

        #region Работа с узлами/домами

        /// <summary>
        /// Получить узлы (дома) с квартирами
        /// </summary>
        Task<List<NodeInfo>> GetNodesWithApartmentsAsync();

        #endregion

        #region Генерация отчётов

        /// <summary>
        /// Сгенерировать один отчёт
        /// </summary>
        Task<GenerationResult> GenerateSingleReportAsync(
            MeasurePointInfo measurePoint,
            ReportTemplateInfo template,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            string outputPath,
            IProgress<string> progress = null);

        /// <summary>
        /// Массовая генерация отчётов (поиск по имени)
        /// </summary>
        Task<BatchGenerationSummary> GenerateBatchReportsAsync(
            ResourceType resourceType,
            MeasurePointType pointType,
            string templateName,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Массовая генерация отчётов ОДПУ (поиск по ReportId)
        /// </summary>
        Task<BatchGenerationSummary> GenerateBatchReportsByReportIdAsync(
            ResourceType resourceType,
            int reportId,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Массовая генерация отчётов по домам (для квартир)
        /// </summary>
        Task<BatchGenerationSummary> GenerateBatchHouseReportsAsync(
            int reportId,
            string reportTitle,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress = null,
            CancellationToken cancellationToken = default(CancellationToken));

        #endregion
    }
}
