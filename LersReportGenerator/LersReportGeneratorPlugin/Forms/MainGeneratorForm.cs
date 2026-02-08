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
        private ReportTemplateInfo _selectedBatchTemplate;
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
        private ComboBoxEdit cmbBatchTemplate;
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
            this.cmbBatchTemplate = new ComboBoxEdit();
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
            this.grpSettings.Size = new Size(460, 220);
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

            // Отчёт
            this.lblBatchTemplate.Text = "Отчёт:";
            this.lblBatchTemplate.Location = new Point(15, 80);
            this.cmbBatchTemplate.Location = new Point(120, 77);
            this.cmbBatchTemplate.Size = new Size(320, 20);
            this.cmbBatchTemplate.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.cmbBatchTemplate.SelectedIndexChanged += cmbBatchTemplate_SelectedIndexChanged;

            // Период
            this.lblPeriod.Text = "Период:";
            this.lblPeriod.Location = new Point(15, 108);
            this.cmbPeriod.Location = new Point(120, 105);
            this.cmbPeriod.Size = new Size(150, 20);
            this.cmbPeriod.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.cmbPeriod.SelectedIndexChanged += cmbPeriod_SelectedIndexChanged;

            // Дата начала (на отдельной строке)
            this.lblStartDate.Text = "с";
            this.lblStartDate.Location = new Point(15, 136);
            this.lblStartDate.Visible = false;
            this.dtpStart.Location = new Point(30, 133);
            this.dtpStart.Size = new Size(120, 20);
            this.dtpStart.Visible = false;

            // Дата окончания
            this.lblEndDate.Text = "по";
            this.lblEndDate.Location = new Point(160, 136);
            this.lblEndDate.Visible = false;
            this.dtpEnd.Location = new Point(180, 133);
            this.dtpEnd.Size = new Size(120, 20);
            this.dtpEnd.Visible = false;

            // Формат (сдвигаем вниз когда даты скрыты, или ещё ниже когда показаны)
            this.lblFormat.Text = "Формат:";
            this.lblFormat.Location = new Point(15, 136);
            this.cmbFormat.Location = new Point(120, 133);
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
            this.grpSettings.Controls.Add(this.cmbBatchTemplate);
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
            this.grpOutput.Location = new Point(12, 348);
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
            this.btnGenerate.Location = new Point(277, 426);
            this.btnGenerate.Size = new Size(115, 32);
            this.btnGenerate.Appearance.BackColor = Color.FromArgb(0, 122, 204);
            this.btnGenerate.Appearance.ForeColor = Color.White;
            this.btnGenerate.Appearance.Options.UseBackColor = true;
            this.btnGenerate.Appearance.Options.UseForeColor = true;
            this.btnGenerate.Appearance.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.btnGenerate.Appearance.Options.UseFont = true;
            this.btnGenerate.Click += btnGenerate_Click;

            this.btnClose.Text = "Закрыть";
            this.btnClose.Location = new Point(397, 426);
            this.btnClose.Size = new Size(75, 32);
            this.btnClose.Click += (s, e) => {
                _remoteClient?.Dispose();
                Close();
            };

            // === Прогресс ===
            this.progressBar.Location = new Point(12, 468);
            this.progressBar.Size = new Size(460, 22);
            this.progressBar.Properties.ShowTitle = true;
            this.progressBar.Visible = false;

            this.lblStatus.Location = new Point(12, 496);
            this.lblStatus.AutoSizeMode = LabelAutoSizeMode.None;
            this.lblStatus.Size = new Size(460, 20);
            this.lblStatus.Text = "";
            this.lblStatus.Appearance.ForeColor = Color.FromArgb(80, 80, 80);
            this.lblStatus.Appearance.Options.UseForeColor = true;

            // === Форма ===
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(484, 528);
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

            cmbBatchTemplate.Properties.Items.Clear();
            cmbBatchTemplate.Properties.Items.Add("Загрузка...");
            cmbBatchTemplate.SelectedIndex = 0;
            cmbBatchTemplate.Enabled = false;
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
                    cmbBatchTemplate.Properties.Items.Clear();
                    cmbBatchTemplate.Properties.Items.Add(result.EmptyMessage);
                    cmbBatchTemplate.SelectedIndex = 0;
                    lblStatus.Text = result.EmptyMessage;
                    return;
                }

                DisplayTemplates(result.Templates);
            }
            catch (Exception ex)
            {
                cmbBatchTemplate.Properties.Items.Clear();
                cmbBatchTemplate.Properties.Items.Add($"Ошибка: {ex.Message}");
                cmbBatchTemplate.SelectedIndex = 0;
                lblStatus.Text = $"Ошибка загрузки: {ex.Message}";
            }
            finally
            {
                cmbBatchTemplate.Enabled = true;
                rgPointType.Enabled = true;
                cmbResourceType.Enabled = true;
                _isLoadingTemplates = false;
            }
        }

        /// <summary>
        /// Отображает шаблоны в комбобоксе
        /// </summary>
        private void DisplayTemplates(List<ReportTemplateInfo> templates, string source = null)
        {
            cmbBatchTemplate.Properties.Items.Clear();
            cmbBatchTemplate.Enabled = true;
            rgPointType.Enabled = true;
            cmbResourceType.Enabled = true;

            if (templates == null || templates.Count == 0)
            {
                cmbBatchTemplate.Properties.Items.Add("Нет шаблонов");
                cmbBatchTemplate.SelectedIndex = 0;
                lblStatus.Text = "Нет шаблонов";
                return;
            }

            foreach (var template in templates)
            {
                cmbBatchTemplate.Properties.Items.Add(template);
            }
            cmbBatchTemplate.SelectedIndex = 0;
            _selectedBatchTemplate = templates[0];

            var countText = $"{templates.Count} {Pluralize(templates.Count, "шаблон", "шаблона", "шаблонов")}";
            lblStatus.Text = source != null
                ? $"Загружено {countText} ({source})"
                : $"Загружено {countText}";
        }

        private void cmbBatchTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            var template = cmbBatchTemplate.SelectedItem as ReportTemplateInfo;
            if (template != null)
            {
                _selectedBatchTemplate = template;
            }
        }

        private void cmbPeriod_SelectedIndexChanged(object sender, EventArgs e)
        {
            var isCustom = GetSelectedPeriod() == ReportPeriod.Custom;
            lblStartDate.Visible = isCustom;
            dtpStart.Visible = isCustom;
            lblEndDate.Visible = isCustom;
            dtpEnd.Visible = isCustom;

            // Сдвигаем контролы формата и режима в зависимости от видимости дат
            int formatY = isCustom ? 164 : 136;
            int deliveryY = isCustom ? 192 : 164;

            lblFormat.Location = new Point(15, formatY);
            cmbFormat.Location = new Point(120, formatY - 3);
            lblDelivery.Location = new Point(15, deliveryY);
            cmbDelivery.Location = new Point(120, deliveryY - 3);

            // Подстраиваем высоту группы настроек
            grpSettings.Height = isCustom ? 248 : 220;

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
            if (_selectedBatchTemplate == null)
            {
                XtraMessageBox.Show("Выберите отчёт", "Внимание",
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
                var progress = new Progress<Tuple<int, int, string>>(p =>
                {
                    if (p.Item2 > 0)
                    {
                        progressBar.Properties.Maximum = p.Item2;
                        progressBar.EditValue = Math.Min(p.Item1, p.Item2);
                    }
                    lblStatus.Text = $"{p.Item1}/{p.Item2}: {p.Item3}";
                });

                var summary = await _generationController.GenerateAsync(
                    _selectedBatchTemplate,
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
                    progress,
                    _cts.Token);

                stopwatch.Stop();
                ShowBatchResults(summary, deliveryMode, stopwatch.Elapsed);
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
            cmbResourceType.Enabled = enabled;
            cmbBatchTemplate.Enabled = enabled;
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
