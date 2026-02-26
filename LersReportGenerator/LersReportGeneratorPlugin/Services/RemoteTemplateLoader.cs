using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Загрузчик шаблонов отчётов с удалённых серверов через прокси-службу
    /// </summary>
    public class RemoteTemplateLoader
    {
        /// <summary>
        /// Загружает шаблоны ОДПУ отчётов с удалённого сервера через прокси-службу.
        /// Использует оптимизированный endpoint /lersproxy/reports/templates.
        /// </summary>
        public async Task<List<ReportTemplateInfo>> LoadOdpuTemplatesAsync(ServerConfig server, ResourceType resourceType)
        {
            var templates = new List<ReportTemplateInfo>();

            try
            {
                using (var client = new LersProxyClient(server))
                {
                    // Проверяем доступность прокси
                    bool available = await client.CheckHealthAsync();
                    if (!available)
                    {
                        Logger.Error($"[{server.Name}] Прокси-служба недоступна");
                        return templates;
                    }

                    // Авторизуемся
                    string password = CredentialManager.DecryptPassword(server.EncryptedPassword);
                    var loginResult = await client.LoginAsync(server.Login, password);
                    if (!loginResult.Success)
                    {
                        Logger.Error($"[{server.Name}] Ошибка авторизации: {loginResult.ErrorMessage}");
                        return templates;
                    }

                    // Получаем systemTypeId для фильтрации
                    int? systemTypeId = null;
                    if (resourceType != ResourceType.All)
                    {
                        int[] ids = resourceType.GetSystemTypeIds();
                        systemTypeId = ids.Length > 0 ? ids[0] : (int?)null;
                    }

                    // Используем оптимизированный endpoint для получения шаблонов
                    // Прокси сам вычисляет уникальные шаблоны локально (быстро!)
                    var proxyTemplates = await client.GetOdpuTemplatesAsync(systemTypeId);

                    foreach (var t in proxyTemplates)
                    {
                        templates.Add(new ReportTemplateInfo
                        {
                            ReportId = t.reportId,
                            ReportTemplateId = t.reportTemplateId ?? t.reportId,
                            TemplateTitle = t.title ?? t.templateTitle ?? $"Отчёт {t.reportId}",
                            InstanceTitle = t.title ?? t.templateTitle ?? $"Отчёт {t.reportId}"
                        });
                    }

                    Logger.Info($"[{server.Name}] Загружено {templates.Count} шаблонов ОДПУ через прокси");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{server.Name}] Ошибка загрузки шаблонов ОДПУ: {ex.Message}");
            }

            return templates;
        }

        /// <summary>
        /// Загружает шаблоны ИПУ отчётов с удалённого сервера через прокси-службу.
        /// </summary>
        public async Task<List<ReportTemplateInfo>> LoadIpuTemplatesAsync(ServerConfig server)
        {
            var templates = new List<ReportTemplateInfo>();

            try
            {
                using (var client = new LersProxyClient(server))
                {
                    bool available = await client.CheckHealthAsync();
                    if (!available)
                    {
                        Logger.Error($"[{server.Name}] Прокси-служба недоступна");
                        return templates;
                    }

                    string password = CredentialManager.DecryptPassword(server.EncryptedPassword);
                    var loginResult = await client.LoginAsync(server.Login, password);
                    if (!loginResult.Success)
                    {
                        Logger.Error($"[{server.Name}] Ошибка авторизации: {loginResult.ErrorMessage}");
                        return templates;
                    }

                    var proxyTemplates = await client.GetApartmentTemplatesAsync();

                    foreach (var t in proxyTemplates)
                    {
                        templates.Add(new ReportTemplateInfo
                        {
                            ReportId = t.reportId,
                            ReportTemplateId = t.reportTemplateId ?? t.reportId,
                            TemplateTitle = t.templateTitle ?? t.title,  // Template Title (название шаблона)
                            InstanceTitle = t.title                       // Report Title (название отчёта)
                        });
                    }

                    Logger.Info($"[{server.Name}] Загружено {templates.Count} шаблонов ИПУ через прокси");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{server.Name}] Ошибка загрузки шаблонов ИПУ: {ex.Message}");
            }

            return templates;
        }
    }
}
