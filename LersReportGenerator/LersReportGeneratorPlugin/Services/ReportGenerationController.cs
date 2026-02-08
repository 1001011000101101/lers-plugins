using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Контроллер генерации отчётов.
    /// Координирует генерацию с локального и удалённых серверов.
    /// </summary>
    public class ReportGenerationController
    {
        private readonly ReportGeneratorService _localReportService;

        public ReportGenerationController(ReportGeneratorService localReportService)
        {
            _localReportService = localReportService ?? throw new ArgumentNullException(nameof(localReportService));
        }

        /// <summary>
        /// Единая точка входа — выбирает стратегию генерации автоматически
        /// по комбинации параметров (allServers / remote / local apartment / local ODPU).
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateAsync(
            ReportTemplateInfo selectedTemplate,
            MeasurePointType pointType,
            ResourceType resourceType,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            bool isRemoteMode,
            ServerConfig selectedRemoteServer,
            bool allServersMode,
            IProgress<Tuple<int, int, string>> progress,
            CancellationToken cancellationToken)
        {
            if (allServersMode)
            {
                return await GenerateFromAllServersAsync(
                    selectedTemplate, pointType, resourceType,
                    startDate, endDate, format, deliveryMode, outputPath,
                    progress, cancellationToken);
            }

            if (isRemoteMode && selectedRemoteServer != null)
            {
                var summary = await GenerateFromRemoteServerAsync(
                    selectedRemoteServer, selectedTemplate, pointType, resourceType,
                    startDate, endDate, format, outputPath,
                    progress, cancellationToken);

                // Для remote-ветки ZIP не встроен в метод генерации
                if (deliveryMode == DeliveryMode.Zip && summary.SuccessCount > 0)
                {
                    summary.ZipFilePath = await CreateZipFromResultsAsync(summary.Results, outputPath);
                }

                return summary;
            }

            if (pointType == MeasurePointType.Apartment)
            {
                return await _localReportService.GenerateBatchHouseReportsAsync(
                    selectedTemplate.ReportId,
                    selectedTemplate.TemplateTitle,
                    startDate, endDate, format, deliveryMode, outputPath,
                    progress, cancellationToken);
            }

            return await _localReportService.GenerateBatchReportsByReportIdAsync(
                resourceType,
                selectedTemplate.ReportId,
                startDate, endDate, format, deliveryMode, outputPath,
                progress, cancellationToken);
        }

        /// <summary>
        /// Генерация со всех серверов (локальный + удалённые)
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateFromAllServersAsync(
            ReportTemplateInfo selectedTemplate,
            MeasurePointType pointType,
            ResourceType resourceType,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress,
            CancellationToken cancellationToken)
        {
            var allResults = new System.Collections.Concurrent.ConcurrentBag<GenerationResult>();
            var tempFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
            string tempDir = Path.Combine(Path.GetTempPath(), $"LersReports_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var remoteServers = SettingsService.Instance.Servers.ToList();
                int totalServers = remoteServers.Count + 1; // +1 для локального
                int completedServers = 0;

                // Начальный прогресс
                progress?.Report(Tuple.Create(0, totalServers, $"Генерация с {totalServers} серверов..."));

                // Создаём задачи для всех серверов (параллельно)
                var tasks = new List<Task>();
                var startTime = DateTime.Now;
                Logger.Info($"[Параллельная генерация] СТАРТ: {totalServers} серверов, время: {startTime:HH:mm:ss.fff}");

                // Задача для локального сервера
                tasks.Add(Task.Run(async () =>
                {
                    var taskStart = DateTime.Now;
                    Logger.Info($"[Локальный] Задача НАЧАТА: {taskStart:HH:mm:ss.fff}");
                    try
                    {
                        var localSummary = await GenerateFromLocalServerAsync(
                            selectedTemplate, pointType, resourceType, startDate, endDate, format,
                            DeliveryMode.SeparateFiles, tempDir,
                            cancellationToken);

                        if (localSummary.TotalCount == 0)
                        {
                            string skipReason = pointType == MeasurePointType.Apartment
                                ? "нет квартирных точек (ИПУ)"
                                : "нет общедомовых точек (ОДПУ)";
                            Logger.Info($"[Локальный] Сервер пропущен: {skipReason}");
                            allResults.Add(new GenerationResult
                            {
                                Success = true,
                                ErrorMessage = $"Локальный сервер: {skipReason}",
                                MeasurePoint = new MeasurePointInfo { Title = "[Локальный] Пропущен" },
                                IsSkipped = true
                            });
                        }
                        else
                        {
                            foreach (var result in localSummary.Results)
                            {
                                allResults.Add(result);
                                if (result.Success && !string.IsNullOrEmpty(result.FilePath))
                                {
                                    tempFiles.Add(result.FilePath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Ошибка генерации с локального сервера: {ex.Message}");
                        allResults.Add(new GenerationResult
                        {
                            Success = false,
                            ErrorMessage = $"Локальный сервер: {ex.Message}",
                            MeasurePoint = new MeasurePointInfo { Title = "[Локальный]" }
                        });
                    }
                    finally
                    {
                        var taskEnd = DateTime.Now;
                        var duration = taskEnd - taskStart;
                        Logger.Info($"[Локальный] Задача ЗАВЕРШЕНА: {taskEnd:HH:mm:ss.fff}, длительность: {duration.TotalSeconds:F2} сек");
                        int completed = Interlocked.Increment(ref completedServers);
                        progress?.Report(Tuple.Create(completed, totalServers, $"Готово: Локальный ({completed}/{totalServers})"));
                    }
                }, cancellationToken));

                // Задачи для удалённых серверов
                foreach (var server in remoteServers)
                {
                    var serverCopy = server; // Capture for closure
                    tasks.Add(Task.Run(async () =>
                    {
                        var taskStart = DateTime.Now;
                        Logger.Info($"[{serverCopy.Name}] Задача НАЧАТА: {taskStart:HH:mm:ss.fff}");
                        try
                        {
                            var remoteSummary = await GenerateFromRemoteServerAsync(
                                serverCopy, selectedTemplate, pointType, resourceType, startDate, endDate, format,
                                tempDir,
                                null, // progress обрабатывается на уровне серверов
                                cancellationToken);

                            foreach (var result in remoteSummary.Results)
                            {
                                allResults.Add(result);
                                if (result.Success && !string.IsNullOrEmpty(result.FilePath))
                                {
                                    tempFiles.Add(result.FilePath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Ошибка генерации с сервера {serverCopy.Name}: {ex.Message}");
                            allResults.Add(new GenerationResult
                            {
                                Success = false,
                                ErrorMessage = $"Сервер {serverCopy.Name}: {ex.Message}",
                                MeasurePoint = new MeasurePointInfo { Title = serverCopy.Name }
                            });
                        }
                        finally
                        {
                            var taskEnd = DateTime.Now;
                            var duration = taskEnd - taskStart;
                            Logger.Info($"[{serverCopy.Name}] Задача ЗАВЕРШЕНА: {taskEnd:HH:mm:ss.fff}, длительность: {duration.TotalSeconds:F2} сек");
                            int completed = Interlocked.Increment(ref completedServers);
                            progress?.Report(Tuple.Create(completed, totalServers, $"Готово: {serverCopy.Name} ({completed}/{totalServers})"));
                        }
                    }, cancellationToken));
                }

                // Ждём завершения всех задач
                await Task.WhenAll(tasks);

                var totalDuration = DateTime.Now - startTime;
                Logger.Info($"[Параллельная генерация] ЗАВЕРШЕНО: все {totalServers} серверов, общее время: {totalDuration.TotalSeconds:F2} сек");

                // 3. Создаём итоговый архив или копируем файлы
                string zipFilePath = null;

                if (deliveryMode == DeliveryMode.Zip && tempFiles.Count > 0)
                {
                    string zipName = $"Отчёты_ВсеСерверы_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
                    zipFilePath = Path.Combine(outputPath, zipName);

                    Logger.Info($"Сборка архива: {tempFiles.Count} файлов в списке");
                    using (var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        foreach (var file in tempFiles)
                        {
                            if (File.Exists(file))
                            {
                                Logger.Info($"Добавляем в архив: {file}");
                                zip.CreateEntryFromFile(file, Path.GetFileName(file));
                            }
                            else
                            {
                                Logger.Warning($"Файл не найден: {file}");
                            }
                        }
                    }

                    Logger.Info($"Создан архив со всех серверов: {zipFilePath}");
                }
                else if (deliveryMode == DeliveryMode.SeparateFiles)
                {
                    foreach (var file in tempFiles)
                    {
                        if (File.Exists(file))
                        {
                            string destPath = Path.Combine(outputPath, Path.GetFileName(file));
                            File.Copy(file, destPath, true);
                        }
                    }
                }

                var resultsList = allResults.ToList();
                return new BatchGenerationSummary
                {
                    Results = resultsList,
                    TotalCount = resultsList.Count,
                    SuccessCount = resultsList.Count(r => r.Success),
                    FailedCount = resultsList.Count(r => !r.Success),
                    ZipFilePath = zipFilePath
                };
            }
            finally
            {
                // Очищаем временную папку
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Не удалось удалить временную папку {tempDir}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Генерация с локального сервера
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateFromLocalServerAsync(
            ReportTemplateInfo selectedTemplate,
            MeasurePointType pointType,
            ResourceType resourceType,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            DeliveryMode deliveryMode,
            string outputPath,
            CancellationToken cancellationToken)
        {
            if (pointType == MeasurePointType.Apartment)
            {
                return await _localReportService.GenerateBatchHouseReportsAsync(
                    selectedTemplate.ReportId,
                    selectedTemplate.TemplateTitle,
                    startDate, endDate, format, deliveryMode, outputPath,
                    new Progress<Tuple<int, int, string>>(),
                    cancellationToken);
            }

            return await _localReportService.GenerateBatchReportsByReportIdAsync(
                resourceType,
                selectedTemplate.ReportId,
                startDate, endDate, format, deliveryMode, outputPath,
                new Progress<Tuple<int, int, string>>(),
                cancellationToken);
        }

        /// <summary>
        /// Генерация с удалённого сервера через прокси-службу
        /// </summary>
        public async Task<BatchGenerationSummary> GenerateFromRemoteServerAsync(
            ServerConfig server,
            ReportTemplateInfo selectedTemplate,
            MeasurePointType pointType,
            ResourceType resourceType,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<GenerationResult>();

            using (var client = new LersProxyClient(server))
            {
                // Авторизуемся через прокси
                progress?.Report(Tuple.Create(0, 1, "Авторизация..."));
                string password = CredentialManager.DecryptPassword(server.EncryptedPassword);
                var loginResult = await client.LoginAsync(server.Login, password);
                if (!loginResult.Success)
                {
                    throw new Exception($"Ошибка авторизации: {loginResult.ErrorMessage}");
                }

                int? systemTypeId = null;
                if (resourceType != ResourceType.All)
                {
                    int[] ids = resourceType.GetSystemTypeIds();
                    systemTypeId = ids.Length > 0 ? ids[0] : (int?)null;
                }

                if (pointType == MeasurePointType.Apartment)
                {
                    // ИПУ: генерация по узлам (домам)
                    return await GenerateIpuReportsFromProxyAsync(
                        client, server, selectedTemplate, startDate, endDate, format, outputPath, progress, cancellationToken);
                }

                // ОДПУ: генерация по точкам учёта
                progress?.Report(Tuple.Create(0, 1, "Поиск отчёта на сервере..."));

                // Ищем отчёт по названию на этом сервере (ReportId может отличаться!)
                string templateName = selectedTemplate.InstanceTitle ?? selectedTemplate.TemplateTitle;
                var serverTemplates = await client.GetOdpuTemplatesAsync(systemTypeId);
                var matchingTemplate = serverTemplates.FirstOrDefault(t =>
                    (t.title ?? t.templateTitle ?? "").Equals(templateName, StringComparison.OrdinalIgnoreCase));

                if (matchingTemplate == null)
                {
                    // Отчёт не найден на этом сервере
                    Logger.Warning($"[{server.Name}] Отчёт '{templateName}' не найден на сервере");
                    results.Add(new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Отчёт '{templateName}' не найден на сервере {server.Name}",
                        MeasurePoint = new MeasurePointInfo { Title = $"[{server.Name}] Отчёт не найден" }
                    });
                    return new BatchGenerationSummary
                    {
                        Results = results,
                        TotalCount = 1,
                        SuccessCount = 0,
                        FailedCount = 1
                    };
                }

                int reportId = matchingTemplate.reportId;
                Logger.Info($"[{server.Name}] Найден отчёт '{templateName}' с ReportId={reportId}");

                progress?.Report(Tuple.Create(0, 1, "Загрузка точек учёта..."));
                var measurePoints = await client.GetMeasurePointsAsync(
                    type: Constants.MeasurePointTypes.Regular,
                    includeReports: false,
                    systemTypeId: systemTypeId);

                Logger.Info($"[{server.Name}] Загружено {measurePoints.Count} точек ОДПУ через прокси");

                int current = 0;
                int total = measurePoints.Count;

                foreach (var mp in measurePoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report(Tuple.Create(current, total, mp.title ?? $"Точка {mp.id}"));

                    try
                    {

                        string formatStr = format == ExportFormat.PDF ? Constants.ExportFormats.Pdf : Constants.ExportFormats.Xlsx;
                        byte[] reportBytes = await client.GenerateReportAsync(
                            reportId,
                            new[] { mp.id },
                            null,
                            Constants.DataTypes.Day,
                            startDate,
                            endDate,
                            formatStr);

                        string ext = format == ExportFormat.PDF ? ".pdf" : ".xlsx";
                        string dateRange = $"{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
                        string fileName = FileService.SanitizeFileName($"{mp.title}_{templateName}_{dateRange}{ext}");
                        string filePath = Path.Combine(outputPath, fileName);

                        File.WriteAllBytes(filePath, reportBytes);

                        results.Add(new GenerationResult
                        {
                            Success = true,
                            FilePath = filePath,
                            MeasurePoint = new MeasurePointInfo { Id = mp.id, Title = mp.title }
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new GenerationResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            MeasurePoint = new MeasurePointInfo { Id = mp.id, Title = mp.title }
                        });
                    }
                }
            }

            return new BatchGenerationSummary
            {
                Results = results,
                TotalCount = results.Count,
                SuccessCount = results.Count(r => r.Success),
                FailedCount = results.Count(r => !r.Success)
            };
        }

        /// <summary>
        /// Генерация ИПУ отчётов с удалённого сервера
        /// </summary>
        private async Task<BatchGenerationSummary> GenerateIpuReportsFromProxyAsync(
            LersProxyClient client,
            ServerConfig server,
            ReportTemplateInfo selectedTemplate,
            DateTime startDate,
            DateTime endDate,
            ExportFormat format,
            string outputPath,
            IProgress<Tuple<int, int, string>> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<GenerationResult>();

            try
            {
                // Ищем отчёт по названию на этом сервере (ReportId может отличаться!)
                progress?.Report(Tuple.Create(0, 1, "Поиск отчёта на сервере..."));
                string templateName = selectedTemplate.InstanceTitle ?? selectedTemplate.TemplateTitle;
                var serverTemplates = await client.GetApartmentTemplatesAsync();
                var matchingTemplate = serverTemplates.FirstOrDefault(t =>
                    (t.title ?? "").Equals(templateName, StringComparison.OrdinalIgnoreCase));

                if (matchingTemplate == null)
                {
                    // Отчёт не найден на этом сервере
                    Logger.Warning($"[{server.Name}] ИПУ отчёт '{templateName}' не найден на сервере");
                    results.Add(new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Отчёт '{templateName}' не найден на сервере {server.Name}",
                        MeasurePoint = new MeasurePointInfo { Title = $"[{server.Name}] Отчёт не найден" }
                    });
                    return new BatchGenerationSummary
                    {
                        Results = results,
                        TotalCount = 1,
                        SuccessCount = 0,
                        FailedCount = 1
                    };
                }

                int reportId = matchingTemplate.reportId;
                Logger.Info($"[{server.Name}] Найден ИПУ отчёт '{templateName}' с ReportId={reportId}");

                progress?.Report(Tuple.Create(0, 1, "Загрузка списка домов..."));
                var nodes = await client.GetNodesAsync(Constants.NodeTypes.House);
                Logger.Info($"[{server.Name}] Загружено {nodes.Count} домов через прокси");

                if (nodes.Count == 0)
                {
                    return new BatchGenerationSummary
                    {
                        Results = new List<GenerationResult>
                        {
                            new GenerationResult
                            {
                                Success = false,
                                ErrorMessage = "Не найдено объектов учёта типа 'Дом'"
                            }
                        },
                        TotalCount = 1,
                        FailedCount = 1
                    };
                }
                string formatStr = format == ExportFormat.PDF ? Constants.ExportFormats.Pdf : Constants.ExportFormats.Xlsx;
                string ext = format == ExportFormat.PDF ? ".pdf" : ".xlsx";
                string dateRange = $"{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";

                int current = 0;
                int total = nodes.Count;

                foreach (var node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;

                    // Формируем имя файла: [Адрес]_[Title]_[Шаблон]_[Даты]
                    string fileNamePrefix;
                    bool hasAddress = !string.IsNullOrEmpty(node.address);
                    bool hasTitle = !string.IsNullOrEmpty(node.title);

                    if (hasAddress && hasTitle)
                    {
                        // Есть и адрес, и название
                        fileNamePrefix = $"{node.address}_{node.title}";
                    }
                    else if (hasAddress)
                    {
                        // Только адрес
                        fileNamePrefix = node.address;
                    }
                    else if (hasTitle)
                    {
                        // Только название
                        fileNamePrefix = node.title;
                    }
                    else
                    {
                        // Fallback
                        fileNamePrefix = $"Дом_{node.id}";
                    }

                    // Для UI показываем title или адрес
                    string nodeDisplayName = node.title ?? node.address ?? $"Дом_{node.id}";
                    progress?.Report(Tuple.Create(current, total, nodeDisplayName));

                    try
                    {
                        byte[] reportBytes = await client.GenerateReportAsync(
                            reportId,
                            null,
                            new[] { node.id },
                            Constants.DataTypes.Day,
                            startDate,
                            endDate,
                            formatStr);

                        string fileName = FileService.SanitizeFileName($"{templateName}_{fileNamePrefix}_{dateRange}{ext}");
                        string filePath = Path.Combine(outputPath, fileName);

                        Logger.Info($"[{server.Name}] Записываем файл: {filePath} ({reportBytes.Length} байт)");
                        File.WriteAllBytes(filePath, reportBytes);
                        Logger.Info($"[{server.Name}] Файл записан успешно");

                        results.Add(new GenerationResult
                        {
                            Success = true,
                            FilePath = filePath,
                            MeasurePoint = new MeasurePointInfo { Id = node.id, Title = nodeDisplayName }
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new GenerationResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            MeasurePoint = new MeasurePointInfo { Id = node.id, Title = nodeDisplayName }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{server.Name}] Ошибка генерации ИПУ отчётов: {ex.Message}");
                results.Add(new GenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }

            return new BatchGenerationSummary
            {
                Results = results,
                TotalCount = results.Count,
                SuccessCount = results.Count(r => r.Success),
                FailedCount = results.Count(r => !r.Success)
            };
        }

        /// <summary>
        /// Создаёт ZIP-архив из результатов генерации
        /// </summary>
        public async Task<string> CreateZipFromResultsAsync(List<GenerationResult> results, string outputPath)
        {
            return await Task.Run(() =>
            {
                var successFiles = results
                    .Where(r => r.Success && !string.IsNullOrEmpty(r.FilePath) && File.Exists(r.FilePath))
                    .Select(r => r.FilePath)
                    .ToList();

                if (successFiles.Count == 0)
                    return null;

                string zipName = $"Отчёты_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
                string zipPath = Path.Combine(outputPath, zipName);

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in successFiles)
                    {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file));
                    }
                }

                // Удаляем исходные файлы после упаковки
                foreach (var file in successFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Не удалось удалить временный файл {file}: {ex.Message}");
                    }
                }

                return zipPath;
            });
        }
    }
}
