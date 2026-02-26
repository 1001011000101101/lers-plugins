using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using Lers;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;
using LersReportGeneratorPlugin.Services;

namespace LersReportGeneratorPlugin.Forms
{
    /// <summary>
    /// Главная форма генератора отчётов с контролами DevExpress
    /// </summary>
    public class MainGeneratorForm : XtraForm
    {
        private readonly LersServer _server;
        private readonly ReportGeneratorService _reportService;
        private readonly ReportGenerationController _generationController;
        private readonly RemoteTemplateLoader _templateLoader;
        private readonly TemplateLoadingService _templateLoadingService;
        private readonly ServerConnectionService _serverConnectionService;
        private CancellationTokenSource _cts;

        // Remote server support
        private LersProxyClient _remoteClient;
        private ServerConfig _selectedRemoteServer;
        private bool _isRemoteMode;
        private bool _isLoadingTemplates;

        // Контролы
        private MenuStrip mainMenu;
        private GroupControl grpServer;
        private LabelControl lblServer;
        private ComboBoxEdit cmbServer;
        private CheckEdit chkAllServers;
        private RadioGroup rgPointType;
        private LabelControl lblResourceType;
        private ComboBoxEdit cmbResourceType;
        private LabelControl lblBatchTemplate;
        private CheckedListBoxControl lstBatchTemplates;
        private LabelControl lblPeriod;
        private ComboBoxEdit cmbPeriod;
        private LabelControl lblStartDate;
        private DateEdit dtpStart;
        private LabelControl lblEndDate;
        private DateEdit dtpEnd;
        private LabelControl lblFormat;
        private ComboBoxEdit cmbFormat;
        private LabelControl lblDelivery;
        private ComboBoxEdit cmbDelivery;
        private LabelControl lblOutputPath;
        private TextEdit txtOutputPath;
        private SimpleButton btnBrowse;
        private SimpleButton btnGenerate;
        private SimpleButton btnClose;
        private ProgressBarControl progressBar;
        private LabelControl lblStatus;
        private GroupControl grpSettings;
        private GroupControl grpOutput;

        public MainGeneratorForm(LersServer server, ReportGeneratorService reportService)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _generationController = new ReportGenerationController(reportService);
            _templateLoader = new RemoteTemplateLoader();
            _templateLoadingService = new TemplateLoadingService(reportService, _templateLoader);
            _serverConnectionService = new ServerConnectionService();

            InitializeComponent();
            InitializeFormData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Создаём контролы
            this.mainMenu = new MenuStrip();
            this.grpServer = new GroupControl();
            this.lblServer = new LabelControl();
            this.cmbServer = new ComboBoxEdit();
            this.chkAllServers = new CheckEdit();
            this.grpSettings = new GroupControl();
            this.grpOutput = new GroupControl();
            this.rgPointType = new RadioGroup();
            this.lblResourceType = new LabelControl();
            this.cmbResourceType = new ComboBoxEdit();
            this.lblBatchTemplate = new LabelControl();
            this.lstBatchTemplates = new CheckedListBoxControl();
            this.lblPeriod = new LabelControl();
            this.cmbPeriod = new ComboBoxEdit();
            this.lblStartDate = new LabelControl();
            this.dtpStart = new DateEdit();
            this.lblEndDate = new LabelControl();
            this.dtpEnd = new DateEdit();
            this.lblFormat = new LabelControl();
            this.cmbFormat = new ComboBoxEdit();
            this.lblDelivery = new LabelControl();
            this.cmbDelivery = new ComboBoxEdit();
            this.lblOutputPath = new LabelControl();
            this.txtOutputPath = new TextEdit();
            this.btnBrowse = new SimpleButton();
            this.btnGenerate = new SimpleButton();
            this.btnClose = new SimpleButton();
            this.progressBar = new ProgressBarControl();
            this.lblStatus = new LabelControl();

            // === Главное меню ===
            var menuService = new ToolStripMenuItem("Сервис");
            var menuServers = new ToolStripMenuItem("Управление серверами...");
            menuServers.Click += (s, e) => btnServerManager_Click(s, e);
            var menuLogsPlugin = new ToolStripMenuItem("Логи плагина...");
            menuLogsPlugin.Click += (s, e) => OpenPluginLogs();
            var menuLogsProxy = new ToolStripMenuItem("Логи прокси-службы...");
            menuLogsProxy.Click += (s, e) => OpenProxyLogs();
            menuService.DropDownItems.Add(menuServers);
            menuService.DropDownItems.Add(new ToolStripSeparator());
            menuService.DropDownItems.Add(menuLogsPlugin);
            menuService.DropDownItems.Add(menuLogsProxy);

            var menuHelp = new ToolStripMenuItem("Справка");
            var menuAbout = new ToolStripMenuItem("О модуле...");
            menuAbout.Click += (s, e) => btnAbout_Click(s, e);
            menuHelp.DropDownItems.Add(menuAbout);

            this.mainMenu.Items.Add(menuService);
            this.mainMenu.Items.Add(menuHelp);

            // === Группа сервера ===
            this.grpServer.Text = "  Источник данных";
            this.grpServer.Location = new Point(12, 32);
            this.grpServer.Size = new Size(460, 80);
            this.grpServer.AppearanceCaption.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.grpServer.AppearanceCaption.ForeColor = Color.FromArgb(0, 122, 204);
            this.grpServer.AppearanceCaption.Options.UseFont = true;
            this.grpServer.AppearanceCaption.Options.UseForeColor = true;

            this.lblServer.Text = "Сервер:";
            this.lblServer.Location = new Point(15, 27);
            this.cmbServer.Location = new Point(75, 24);
            this.cmbServer.Size = new Size(365, 20);
            this.cmbServer.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.cmbServer.SelectedIndexChanged += cmbServer_SelectedIndexChanged;

            // Чекбокс "Все серверы"
            this.chkAllServers.Text = "Генерировать со всех серверов (локальный + удалённые)";
            this.chkAllServers.Location = new Point(15, 52);
            this.chkAllServers.Size = new Size(420, 20);
            this.chkAllServers.CheckedChanged += chkAllServers_CheckedChanged;

            this.grpServer.Controls.Add(this.lblServer);
            this.grpServer.Controls.Add(this.cmbServer);
            this.grpServer.Controls.Add(this.chkAllServers);

            // === Группа настроек ===
            this.grpSettings.Text = "  Параметры генерации";
            this.grpSettings.Location = new Point(12, 120);
            this.grpSettings.Size = new Size(460, 300);
            this.grpSettings.AppearanceCaption.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.grpSettings.AppearanceCaption.ForeColor = Color.FromArgb(0, 122, 204);
            this.grpSettings.AppearanceCaption.Options.UseFont = true;
            this.grpSettings.AppearanceCaption.Options.UseForeColor = true;

            // Тип точки (общедомовые / квартирные) - RadioGroup для выбора в 1 клик
            this.rgPointType.Location = new Point(15, 25);
            this.rgPointType.Size = new Size(430, 25);
            this.rgPointType.Properties.Items.AddRange(new DevExpress.XtraEditors.Controls.RadioGroupItem[] {
                new DevExpress.XtraEditors.Controls.RadioGroupItem((int)MeasurePointType.Building, "Общедомовые (ОДПУ)"),
                new DevExpress.XtraEditors.Controls.RadioGroupItem((int)MeasurePointType.Apartment, "Квартирные (ИПУ)")
            });
            this.rgPointType.Properties.ItemsLayout = DevExpress.XtraEditors.RadioGroupItemsLayout.Flow;
            this.rgPointType.SelectedIndexChanged += rgPointType_SelectedIndexChanged;

            // Тип ресурса
            this.lblResourceType.Text = "Тип ресурса:";
            this.lblResourceType.Location = new Point(15, 55);
            this.cmbResourceType.Location = new Point(120, 52);
            this.cmbResourceType.Size = new Size(180, 20);
            this.cmbResourceType.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.cmbResourceType.SelectedIndexChanged += cmbResourceType_SelectedIndexChanged;

            // Отчёты (множественный выбор)
            this.lblBatchTemplate.Text = "Отчёты:";
            this.lblBatchTemplate.Location = new Point(15, 80);
            this.lstBatchTemplates.Location = new Point(120, 77);
            this.lstBatchTemplates.Size = new Size(320, 120);
            this.lstBatchTemplates.CheckOnClick = true;
            this.lstBatchTemplates.SelectionMode = SelectionMode.One;
            this.lstBatchTemplates.DisplayMember = "DisplayTitle";

            // Период
            this.lblPeriod.Text = "Период:";
            this.lblPeriod.Location = new Point(15, 205);
            this.cmbPeriod.Location = new Point(120, 202);
            this.cmbPeriod.Size = new Size(150, 20);
            this.cmbPeriod.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.cmbPeriod.SelectedIndexChanged += cmbPeriod_SelectedIndexChanged;

            // Дата начала (на отдельной строке)
            this.lblStartDate.Text = "с";
            this.lblStartDate.Location = new Point(15, 233);
            this.lblStartDate.Visible = false;
            this.dtpStart.Location = new Point(30, 230);
            this.dtpStart.Size = new Size(120, 20);
            this.dtpStart.Visible = false;

            // Дата окончания
            this.lblEndDate.Text = "по";
            this.lblEndDate.Location = new Point(160, 233);
            this.lblEndDate.Visible = false;
            this.dtpEnd.Location = new Point(180, 230);
            this.dtpEnd.Size = new Size(120, 20);
            this.dtpEnd.Visible = false;

            // Формат (сдвигаем вниз когда даты скрыты, или ещё ниже когда показаны)
            this.lblFormat.Text = "Формат:";
            this.lblFormat.Location = new Point(15, 233);
            this.cmbFormat.Location = new Point(120, 230);
            this.cmbFormat.Size = new Size(150, 20);
            this.cmbFormat.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            // Режим доставки
            this.lblDelivery.Text = "Режим:";
            this.lblDelivery.Location = new Point(15, 164);
            this.cmbDelivery.Location = new Point(120, 161);
            this.cmbDelivery.Size = new Size(180, 20);
            this.cmbDelivery.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            // Добавляем контролы в группу настроек
            this.grpSettings.Controls.Add(this.rgPointType);
            this.grpSettings.Controls.Add(this.lblResourceType);
            this.grpSettings.Controls.Add(this.cmbResourceType);
            this.grpSettings.Controls.Add(this.lblBatchTemplate);
            this.grpSettings.Controls.Add(this.lstBatchTemplates);
            this.grpSettings.Controls.Add(this.lblPeriod);
            this.grpSettings.Controls.Add(this.cmbPeriod);
            this.grpSettings.Controls.Add(this.lblStartDate);
            this.grpSettings.Controls.Add(this.dtpStart);
            this.grpSettings.Controls.Add(this.lblEndDate);
            this.grpSettings.Controls.Add(this.dtpEnd);
            this.grpSettings.Controls.Add(this.lblFormat);
            this.grpSettings.Controls.Add(this.cmbFormat);
            this.grpSettings.Controls.Add(this.lblDelivery);
            this.grpSettings.Controls.Add(this.cmbDelivery);

            // === Группа вывода ===
            this.grpOutput.Text = "  Сохранение";
            this.grpOutput.Location = new Point(12, 428);
            this.grpOutput.Size = new Size(460, 65);
            this.grpOutput.AppearanceCaption.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.grpOutput.AppearanceCaption.ForeColor = Color.FromArgb(0, 122, 204);
            this.grpOutput.AppearanceCaption.Options.UseFont = true;
            this.grpOutput.AppearanceCaption.Options.UseForeColor = true;

            // Путь для сохранения
            this.lblOutputPath.Text = "Папка:";
            this.lblOutputPath.Location = new Point(15, 30);
            this.txtOutputPath.Location = new Point(120, 27);
            this.txtOutputPath.Size = new Size(280, 20);
            this.txtOutputPath.Properties.ReadOnly = true;

            // Кнопка обзора
            this.btnBrowse.Text = "...";
            this.btnBrowse.Location = new Point(405, 25);
            this.btnBrowse.Size = new Size(35, 23);
            this.btnBrowse.Click += btnBrowse_Click;

            // Добавляем контролы в группу вывода
            this.grpOutput.Controls.Add(this.lblOutputPath);
            this.grpOutput.Controls.Add(this.txtOutputPath);
            this.grpOutput.Controls.Add(this.btnBrowse);

            // === Кнопки ===
            this.btnGenerate.Text = "  Сгенерировать";
            this.btnGenerate.Location = new Point(277, 505);
            this.btnGenerate.Size = new Size(115, 32);
            this.btnGenerate.Appearance.BackColor = Color.FromArgb(0, 122, 204);
            this.btnGenerate.Appearance.ForeColor = Color.White;
            this.btnGenerate.Appearance.Options.UseBackColor = true;
            this.btnGenerate.Appearance.Options.UseForeColor = true;
            this.btnGenerate.Appearance.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.btnGenerate.Appearance.Options.UseFont = true;
            this.btnGenerate.Click += btnGenerate_Click;

            this.btnClose.Text = "Закрыть";
            this.btnClose.Location = new Point(397, 505);
            this.btnClose.Size = new Size(75, 32);
            this.btnClose.Click += (s, e) => {
                _remoteClient?.Dispose();
                Close();
            };

            // === Прогресс ===
            this.progressBar.Location = new Point(12, 549);
            this.progressBar.Size = new Size(460, 22);
            this.progressBar.Properties.ShowTitle = true;
            this.progressBar.Visible = false;

            this.lblStatus.Location = new Point(12, 579);
            this.lblStatus.AutoSizeMode = LabelAutoSizeMode.None;
            this.lblStatus.Size = new Size(460, 20);
            this.lblStatus.Text = "";
            this.lblStatus.Appearance.ForeColor = Color.FromArgb(80, 80, 80);
            this.lblStatus.Appearance.Options.UseForeColor = true;

            // === Форма ===
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(484, 610);
            this.MainMenuStrip = this.mainMenu;
            this.Controls.Add(this.mainMenu);
            this.Controls.Add(this.grpServer);
            this.Controls.Add(this.grpSettings);
            this.Controls.Add(this.grpOutput);
            this.Controls.Add(this.btnGenerate);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblStatus);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainGeneratorForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = $"Генератор отчётов ЛЭРС v{Services.VersionInfo.ShortVersion}";
            this.Load += MainGeneratorForm_Load;

            this.ResumeLayout(false);
        }

        private async void MainGeneratorForm_Load(object sender, EventArgs e)
        {
            // Применяем layout для текущего периода
            cmbPeriod_SelectedIndexChanged(sender, e);
            await LoadBatchTemplatesAsync();

            // Запускаем предзагрузку шаблонов в фоне
            PreloadAllTemplatesInBackground();
        }

        /// <summary>
        /// Предзагрузка шаблонов со всех серверов в фоновом режиме для ускорения работы
        /// </summary>
        private void PreloadAllTemplatesInBackground()
        {
            Logger.Info("[Предзагрузка] Метод PreloadAllTemplatesInBackground вызван");

            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.Info("[Предзагрузка] Начало фоновой загрузки шаблонов");
                    Logger.Info("[Предзагрузка] Task.Run запущен");

                    // ИПУ - все серверы (тип ресурса не используется)
                    Logger.Info("[Предзагрузка] Начинаем загрузку шаблонов ИПУ со всех серверов...");
                    var ipuResult = await _templateLoadingService.LoadTemplatesAsync(
                        MeasurePointType.Apartment,
                        ResourceType.All,
                        false,
                        null,
                        true, // все серверы
                        null
                    );
                    Logger.Info($"[Предзагрузка] ИПУ загружено: {ipuResult.Templates.Count} шаблонов");

                    // ОДПУ - все серверы, все типы ресурсов
                    var resourceTypes = new[] {
                        ResourceType.All,          // Все ресурсы (самая частая комбинация)
                        ResourceType.Water,        // ХВС/ГВС
                        ResourceType.Heat,
                        ResourceType.Electricity
                    };

                    foreach (var resourceType in resourceTypes)
                    {
                        Logger.Info($"[Предзагрузка] Загрузка шаблонов ОДПУ ({resourceType.GetDisplayName()}) со всех серверов");
                        await _templateLoadingService.LoadTemplatesAsync(
                            MeasurePointType.Building,
                            resourceType,
                            false,
                            null,
                            true, // все серверы
                            null
                        );
                    }

                    Logger.Info("[Предзагрузка] Фоновая загрузка шаблонов завершена успешно");

                    // Ждём 30 секунд и пробуем повторно загрузить с серверов, где была ошибка
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    await RetryFailedServersAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error("[Предзагрузка] Ошибка фоновой загрузки шаблонов", ex);
                }
            });
        }

        /// <summary>
        /// Повторная попытка загрузки для серверов с ошибками (status == Error)
        /// </summary>
        private async Task RetryFailedServersAsync()
        {
            try
            {
                Logger.Info("[Повтор] Начинаем повторную загрузку для серверов с ошибками");

                var pointTypes = new[] { MeasurePointType.Apartment, MeasurePointType.Building };
                var resourceTypes = new[] { ResourceType.All, ResourceType.Water, ResourceType.Heat, ResourceType.Electricity };

                foreach (var pointType in pointTypes)
                {
                    foreach (var resourceType in resourceTypes)
                    {
                        // Для ИПУ пропускаем не-All типы ресурсов
                        if (pointType == MeasurePointType.Apartment && resourceType != ResourceType.All)
                            continue;

                        // Проверяем статус в кэше
                        var status = TemplateCache.GetStatus(null, pointType, resourceType);
                        if (status == CacheStatus.Error)
                        {
                            Logger.Info($"[Повтор] Локальный сервер: повторная загрузка ({pointType}, {resourceType.GetDisplayName()})");

                            // Очищаем кэш для этой комбинации чтобы загрузилось заново
                            TemplateCache.Invalidate(null);

                            await _templateLoadingService.LoadTemplatesAsync(
                                pointType, resourceType, false, null, true, null);
                        }

                        // Проверяем удалённые серверы
                        foreach (var server in SettingsService.Instance.Servers)
                        {
                            status = TemplateCache.GetStatus(server.Name, pointType, resourceType);
                            if (status == CacheStatus.Error)
                            {
                                Logger.Info($"[Повтор] {server.Name}: повторная загрузка ({pointType}, {resourceType.GetDisplayName()})");

                                // Очищаем кэш для этого сервера
                                TemplateCache.Invalidate(server.Name);

                                await _templateLoadingService.LoadTemplatesAsync(
                                    pointType, resourceType, false, null, true, null);
                            }
                        }
                    }
                }

                Logger.Info("[Повтор] Повторная загрузка завершена");
            }
            catch (Exception ex)
            {
                Logger.Error("[Повтор] Ошибка повторной загрузки", ex);
            }
        }

        private void InitializeFormData()
        {
            // Заполняем список серверов
            LoadServerList();

            // Тип точки - установить Общедомовые по умолчанию
            rgPointType.SelectedIndex = 0;

            // Типы ресурсов
            cmbResourceType.Properties.Items.Clear();
            foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            {
                cmbResourceType.Properties.Items.Add(new ComboBoxItem<ResourceType>(rt, rt.GetDisplayName()));
            }
            cmbResourceType.SelectedIndex = 0;

            // Периоды
            cmbPeriod.Properties.Items.Clear();
            foreach (ReportPeriod p in Enum.GetValues(typeof(ReportPeriod)))
            {
                cmbPeriod.Properties.Items.Add(new ComboBoxItem<ReportPeriod>(p, p.GetDisplayName()));
            }
            cmbPeriod.SelectedIndex = 4; // Предыдущий месяц

            // Форматы
            cmbFormat.Properties.Items.Clear();
            cmbFormat.Properties.Items.Add(new ComboBoxItem<ExportFormat>(ExportFormat.PDF, "PDF"));
            cmbFormat.Properties.Items.Add(new ComboBoxItem<ExportFormat>(ExportFormat.XLSX, "Excel (XLSX)"));
            cmbFormat.SelectedIndex = 1;

            // Режимы доставки
            cmbDelivery.Properties.Items.Clear();
            foreach (DeliveryMode d in Enum.GetValues(typeof(DeliveryMode)))
            {
                cmbDelivery.Properties.Items.Add(new ComboBoxItem<DeliveryMode>(d, d.GetDisplayName()));
            }
            cmbDelivery.SelectedIndex = 1; // ZIP-архив

            // Путь по умолчанию
            txtOutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Поля дат
            var today = DateTime.Today;
            dtpStart.DateTime = new DateTime(today.Year, today.Month, 1);
            dtpEnd.DateTime = today.AddDays(-1);

            // Чекбокс "Все серверы" доступен только если есть удалённые серверы
            UpdateAllServersCheckbox();
        }

        private void UpdateAllServersCheckbox()
        {
            bool hasRemoteServers = SettingsService.Instance.Servers.Count > 0;
            chkAllServers.Enabled = hasRemoteServers;
            if (!hasRemoteServers)
            {
                chkAllServers.Checked = false;
            }
        }

        private async void chkAllServers_CheckedChanged(object sender, EventArgs e)
        {
            // Когда "Все серверы" включен - отключаем выбор конкретного сервера
            cmbServer.Enabled = !chkAllServers.Checked;
            lblServer.Enabled = !chkAllServers.Checked;

            // Перезагружаем шаблоны (для ИПУ логика отличается)
            await LoadBatchTemplatesAsync();
        }

        private void LoadServerList()
        {
            cmbServer.Properties.Items.Clear();

            // Add local server option
            cmbServer.Properties.Items.Add(new ServerListItem(null, "Текущий сервер (локальный)"));

            // Add remote servers from settings
            foreach (var server in SettingsService.Instance.Servers)
            {
                string displayName = server.Name;
                if (server.IsDefault)
                    displayName += " *";
                cmbServer.Properties.Items.Add(new ServerListItem(server, displayName));
            }

            // Всегда выбираем локальный сервер по умолчанию
            cmbServer.SelectedIndex = 0;
        }

        private async void cmbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = cmbServer.SelectedItem as ServerListItem;
            if (item == null)
                return;

            // Dispose previous remote client
            _remoteClient?.Dispose();
            _remoteClient = null;

            if (item.Server == null)
            {
                // Local server
                _isRemoteMode = false;
                _selectedRemoteServer = null;
                SettingsService.Instance.SetLastSelectedServer(null);
            }
            else
            {
                // Remote server
                _isRemoteMode = true;
                _selectedRemoteServer = item.Server;
                SettingsService.Instance.SetLastSelectedServer(item.Server.Id);

                var connResult = await _serverConnectionService.ConnectAsync(
                    item.Server, status => lblStatus.Text = status);

                if (!connResult.Success)
                {
                    XtraMessageBox.Show(connResult.ErrorMessage, connResult.ErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbServer.SelectedIndex = 0;
                    return;
                }

                _remoteClient = connResult.Client;
                lblStatus.Text = connResult.StatusMessage;
            }

            // Reload templates for new server
            await LoadBatchTemplatesAsync();
        }

        private void btnServerManager_Click(object sender, EventArgs e)
        {
            using (var form = new ServerManagerForm())
            {
                form.ShowDialog(this);
            }

            // Reload server list after manager closes
            LoadServerList();
            UpdateAllServersCheckbox();
        }

        private async void rgPointType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var pointType = GetSelectedPointType();

            // Для ИПУ - тип ресурса не используется, деактивируем
            if (pointType == MeasurePointType.Apartment)
            {
                cmbResourceType.Enabled = false;
                lblResourceType.Enabled = false;
            }
            else
            {
                cmbResourceType.Enabled = true;
                lblResourceType.Enabled = true;
            }

            await LoadBatchTemplatesAsync();
        }

        private async void cmbResourceType_SelectedIndexChanged(object sender, EventArgs e)
        {
            await LoadBatchTemplatesAsync();
        }

        private async Task LoadBatchTemplatesAsync()
        {
            // Защита от параллельных вызовов
            if (_isLoadingTemplates)
                return;

            _isLoadingTemplates = true;

            var pointType = GetSelectedPointType();
            var resourceType = GetSelectedResourceType();

            lstBatchTemplates.Items.Clear();
            lstBatchTemplates.Items.Add("Загрузка...");
            lstBatchTemplates.Enabled = false;
            rgPointType.Enabled = false;
            cmbResourceType.Enabled = false;

            try
            {
                var result = await _templateLoadingService.LoadTemplatesAsync(
                    pointType,
                    resourceType,
                    _isRemoteMode,
                    _selectedRemoteServer,
                    chkAllServers.Checked,
                    status =>
                    {
                        if (InvokeRequired)
                            BeginInvoke(new Action(() => lblStatus.Text = status));
                        else
                            lblStatus.Text = status;
                    });

                if (result.FromCache)
                {
                    DisplayTemplates(result.Templates, "из кэша");
                    return;
                }

                if (result.EmptyMessage != null)
                {
                    lstBatchTemplates.Items.Clear();
                    lstBatchTemplates.Items.Add(result.EmptyMessage);
                            lblStatus.Text = result.EmptyMessage;
                    return;
                }

                DisplayTemplates(result.Templates);
            }
            catch (Exception ex)
            {
                lstBatchTemplates.Items.Clear();
                lstBatchTemplates.Items.Add($"Ошибка: {ex.Message}");
                    lblStatus.Text = $"Ошибка загрузки: {ex.Message}";
            }
            finally
            {
                lstBatchTemplates.Enabled = true;
                rgPointType.Enabled = true;
                // Для ИПУ - тип ресурса остаётся отключенным
                cmbResourceType.Enabled = (pointType != MeasurePointType.Apartment);
                lblResourceType.Enabled = (pointType != MeasurePointType.Apartment);
                _isLoadingTemplates = false;
            }
        }

        /// <summary>
        /// Отображает шаблоны в комбобоксе
        /// </summary>
        private void DisplayTemplates(List<ReportTemplateInfo> templates, string source = null)
        {
            lstBatchTemplates.Items.Clear();
            lstBatchTemplates.Enabled = true;
            rgPointType.Enabled = true;

            // Для ИПУ - тип ресурса остаётся отключенным
            var pointType = GetSelectedPointType();
            cmbResourceType.Enabled = (pointType != MeasurePointType.Apartment);
            lblResourceType.Enabled = (pointType != MeasurePointType.Apartment);

            if (templates == null || templates.Count == 0)
            {
                lstBatchTemplates.Items.Add("Нет шаблонов");
                lblStatus.Text = "Нет шаблонов";
                lstBatchTemplates.Enabled = false;
                return;
            }

            foreach (var template in templates)
            {
                // Для DevExpress CheckedListBoxControl просто добавляем элемент
                // По умолчанию все элементы будут Unchecked
                lstBatchTemplates.Items.Add(template);
            }

            var countText = $"{templates.Count} {Pluralize(templates.Count, "шаблон", "шаблона", "шаблонов")}";
            lblStatus.Text = source != null
                ? $"Загружено {countText} ({source})"
                : $"Загружено {countText}";
        }


        private void cmbPeriod_SelectedIndexChanged(object sender, EventArgs e)
        {
            var isCustom = GetSelectedPeriod() == ReportPeriod.Custom;
            lblStartDate.Visible = isCustom;
            dtpStart.Visible = isCustom;
            lblEndDate.Visible = isCustom;
            dtpEnd.Visible = isCustom;

            // Сдвигаем контролы формата и режима в зависимости от видимости дат
            // С учётом нового размера lstBatchTemplates (120px)
            int formatY = isCustom ? 260 : 233;
            int deliveryY = isCustom ? 288 : 261;

            lblFormat.Location = new Point(15, formatY);
            cmbFormat.Location = new Point(120, formatY - 3);
            lblDelivery.Location = new Point(15, deliveryY);
            cmbDelivery.Location = new Point(120, deliveryY - 3);

            // Подстраиваем высоту группы настроек
            // 300 для обычных периодов (с учётом lstBatchTemplates высотой 120)
            // 330 для кастомного периода (+ дополнительные элементы даты)
            grpSettings.Height = isCustom ? 330 : 300;

            // Сдвигаем группу вывода и кнопки
            grpOutput.Location = new Point(12, grpSettings.Bottom + 12);
            btnGenerate.Location = new Point(277, grpOutput.Bottom + 12);
            btnClose.Location = new Point(397, grpOutput.Bottom + 12);
            progressBar.Location = new Point(12, btnGenerate.Bottom + 12);
            lblStatus.Location = new Point(12, progressBar.Bottom + 8);

            // Подстраиваем высоту формы
            this.ClientSize = new Size(484, lblStatus.Bottom + 15);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = txtOutputPath.Text;
                dialog.Description = "Выберите папку для сохранения отчётов";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            using (var form = new AboutForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OpenPluginLogs()
        {
            var logsPath = Logger.GetLogsDirectory();
            if (Directory.Exists(logsPath))
            {
                Process.Start("explorer.exe", logsPath);
            }
            else
            {
                XtraMessageBox.Show($"Папка логов не найдена:\n{logsPath}", "Логи плагина",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenProxyLogs()
        {
            // Путь к логам прокси-службы: %ProgramData%\LersReportProxy\Logs
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var logsPath = Path.Combine(programData, "LersReportProxy", "Logs");

            if (Directory.Exists(logsPath))
            {
                Process.Start("explorer.exe", logsPath);
            }
            else
            {
                XtraMessageBox.Show(
                    "Папка логов прокси-службы не найдена.\n\n" +
                    "Прокси-служба не установлена на этом компьютере или ещё не запускалась.\n\n" +
                    $"Ожидаемый путь: {logsPath}",
                    "Логи прокси-службы",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void btnGenerate_Click(object sender, EventArgs e)
        {
            var selectedTemplates = GetSelectedTemplates();
            if (selectedTemplates.Count == 0)
            {
                XtraMessageBox.Show("Выберите хотя бы один отчёт", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOutputPath.Text) || !Directory.Exists(txtOutputPath.Text))
            {
                XtraMessageBox.Show("Укажите существующую папку", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var period = GetSelectedPeriod();
            var dates = period.GetDates(dtpStart.DateTime, dtpEnd.DateTime);
            var startDate = dates.Item1;
            var endDate = dates.Item2;
            var format = GetSelectedFormat();
            var outputPath = txtOutputPath.Text;
            var pointType = GetSelectedPointType();
            var resourceType = GetSelectedResourceType();
            var deliveryMode = GetSelectedDeliveryMode();

            SetControlsEnabled(false);
            progressBar.EditValue = 0;
            progressBar.Visible = true;
            lblStatus.Text = "Подготовка...";
            _cts = new CancellationTokenSource();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var allResults = new List<GenerationResult>();
                string zipFilePath = null;
                int currentTemplateIndex = 0;

                foreach (var template in selectedTemplates)
                {
                    currentTemplateIndex++;
                    var templateProgress = new Progress<Tuple<int, int, string>>(p =>
                    {
                        if (p.Item2 > 0)
                        {
                            progressBar.Properties.Maximum = p.Item2 * selectedTemplates.Count;
                            progressBar.EditValue = Math.Min(p.Item1 + (currentTemplateIndex - 1) * p.Item2, p.Item2 * selectedTemplates.Count);
                        }
                        lblStatus.Text = $"[{currentTemplateIndex}/{selectedTemplates.Count}] {template.DisplayTitle}: {p.Item1}/{p.Item2} - {p.Item3}";
                    });

                    var summary = await _generationController.GenerateAsync(
                        template,
                        pointType,
                        resourceType,
                        startDate,
                        endDate,
                        format,
                        deliveryMode,
                        outputPath,
                        _isRemoteMode,
                        _selectedRemoteServer,
                        chkAllServers.Checked,
                        templateProgress,
                        _cts.Token);

                    allResults.AddRange(summary.Results);
                    if (!string.IsNullOrEmpty(summary.ZipFilePath))
                    {
                        zipFilePath = summary.ZipFilePath;
                    }
                }

                stopwatch.Stop();

                // Создаём объединённый summary
                var combinedSummary = new BatchGenerationSummary
                {
                    Results = allResults,
                    TotalCount = allResults.Count,
                    SuccessCount = allResults.Count(r => r.Success),
                    FailedCount = allResults.Count(r => !r.Success),
                    ZipFilePath = zipFilePath
                };

                ShowBatchResults(combinedSummary, deliveryMode, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Отменено";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Ошибка: {ex.Message}";
                XtraMessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetControlsEnabled(true);
                progressBar.Visible = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            rgPointType.Enabled = enabled;

            // Для ИПУ - тип ресурса всегда отключен
            var pointType = GetSelectedPointType();
            cmbResourceType.Enabled = enabled && (pointType != MeasurePointType.Apartment);
            lblResourceType.Enabled = enabled && (pointType != MeasurePointType.Apartment);

            lstBatchTemplates.Enabled = enabled;
            cmbDelivery.Enabled = enabled;
            cmbPeriod.Enabled = enabled;
            dtpStart.Enabled = enabled;
            dtpEnd.Enabled = enabled;
            cmbFormat.Enabled = enabled;
            btnBrowse.Enabled = enabled;
            btnGenerate.Enabled = enabled;
            chkAllServers.Enabled = enabled && SettingsService.Instance.Servers.Count > 0;
            cmbServer.Enabled = enabled && !chkAllServers.Checked;
        }

        private void ShowBatchResults(BatchGenerationSummary summary, DeliveryMode deliveryMode, TimeSpan elapsed)
        {
            var msg = summary.FormatResultMessage(deliveryMode, elapsed);
            var icon = summary.FailedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information;

            if (deliveryMode == DeliveryMode.Zip && summary.SuccessCount > 0 && !string.IsNullOrEmpty(summary.ZipFilePath))
            {
                if (XtraMessageBox.Show(msg + "\n\nОткрыть папку?", "Результаты",
                    MessageBoxButtons.YesNo, icon) == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.GetDirectoryName(summary.ZipFilePath),
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                XtraMessageBox.Show(msg, "Результаты", MessageBoxButtons.OK, icon);
            }
        }

        private MeasurePointType GetSelectedPointType()
        {
            if (rgPointType.EditValue != null)
            {
                return (MeasurePointType)(int)rgPointType.EditValue;
            }
            return MeasurePointType.Building;
        }

        private ResourceType GetSelectedResourceType()
        {
            var item = cmbResourceType.SelectedItem as ComboBoxItem<ResourceType>;
            return item != null ? item.Value : ResourceType.All;
        }

        private List<ReportTemplateInfo> GetSelectedTemplates()
        {
            var selected = new List<ReportTemplateInfo>();

            Logger.Debug($"[GetSelectedTemplates] Items.Count = {lstBatchTemplates.Items.Count}");
            Logger.Debug($"[GetSelectedTemplates] CheckedIndices.Count = {lstBatchTemplates.CheckedIndices.Count}");

            // Используем CheckedIndices для DevExpress CheckedListBoxControl
            foreach (int index in lstBatchTemplates.CheckedIndices)
            {
                Logger.Debug($"[GetSelectedTemplates] Checked index: {index}");

                // Для DevExpress CheckedListBoxControl Items[index] возвращает CheckedListBoxItem
                // Нужно получить Value из него
                var item = lstBatchTemplates.Items[index];
                Logger.Debug($"[GetSelectedTemplates] Item type: {item?.GetType().Name}");

                if (item is DevExpress.XtraEditors.Controls.CheckedListBoxItem checkedItem)
                {
                    Logger.Debug($"[GetSelectedTemplates] CheckedItem.Value type: {checkedItem.Value?.GetType().Name}");

                    if (checkedItem.Value is ReportTemplateInfo template)
                    {
                        selected.Add(template);
                        Logger.Debug($"[GetSelectedTemplates] Added: '{template.DisplayTitle}'");
                    }
                }
            }

            Logger.Info($"[GetSelectedTemplates] Total selected: {selected.Count}");
            return selected;
        }

        private ReportPeriod GetSelectedPeriod()
        {
            var item = cmbPeriod.SelectedItem as ComboBoxItem<ReportPeriod>;
            return item != null ? item.Value : ReportPeriod.PreviousMonth;
        }

        private ExportFormat GetSelectedFormat()
        {
            var item = cmbFormat.SelectedItem as ComboBoxItem<ExportFormat>;
            return item != null ? item.Value : ExportFormat.PDF;
        }

        private DeliveryMode GetSelectedDeliveryMode()
        {
            var item = cmbDelivery.SelectedItem as ComboBoxItem<DeliveryMode>;
            return item != null ? item.Value : DeliveryMode.SeparateFiles;
        }

        /// <summary>
        /// Склонение существительных по числу (1 шаблон, 2 шаблона, 5 шаблонов)
        /// </summary>
        private static string Pluralize(int count, string one, string few, string many)
        {
            int abs = Math.Abs(count) % 100;
            if (abs >= 11 && abs <= 19)
                return many;
            int lastDigit = abs % 10;
            if (lastDigit == 1)
                return one;
            if (lastDigit >= 2 && lastDigit <= 4)
                return few;
            return many;
        }
    }

    /// <summary>
    /// Server list item for ComboBox
    /// </summary>
    internal class ServerListItem
    {
        public ServerConfig Server { get; }
        public string DisplayName { get; }

        public ServerListItem(ServerConfig server, string displayName)
        {
            Server = server;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Вспомогательный класс для элементов ComboBox с типизированным значением
    /// </summary>
    internal class ComboBoxItem<T>
    {
        public T Value { get; private set; }
        public string DisplayText { get; private set; }

        public ComboBoxItem(T value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
