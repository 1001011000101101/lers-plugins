using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;
using LersReportGeneratorPlugin.Services;

namespace LersReportGeneratorPlugin.Forms
{
    /// <summary>
    /// Форма управления списком серверов
    /// </summary>
    public class ServerManagerForm : Form
    {
        private ListView lvServers;
        private ColumnHeader colNumber;
        private ColumnHeader colName;
        private ColumnHeader colAddress;
        private ColumnHeader colUrl;
        private ColumnHeader colStatus;
        private ColumnHeader colVersion;
        private ColumnHeader colCoverage;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnSetDefault;
        private Button btnRefresh;
        private Button btnSelectAll;
        private Button btnImport;
        private Button btnExport;
        private Button btnClose;
        private Label lblInfo;
        private Label lblLocalCoverage;
        private Timer _refreshTimer;

        private int _sortColumn = -1;
        private SortOrder _sortOrder = SortOrder.None;

        public ServerManagerForm()
        {
            InitializeComponent();

            // Загружаем сохранённое состояние сортировки
            LoadSortingState();

            LoadServers();

            // Подписываемся на обновления покрытия
            DataCoverageService.Instance.CoverageUpdated += OnCoverageUpdated;

            // Инициализируем таймер автоматической проверки (каждые 60 секунд)
            _refreshTimer = new Timer();
            _refreshTimer.Interval = 60000; // 60 секунд
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DataCoverageService.Instance.CoverageUpdated -= OnCoverageUpdated;

                // Останавливаем и удаляем таймер
                if (_refreshTimer != null)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Dispose();
                    _refreshTimer = null;
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.Text = "Управление серверами";
            this.Size = new Size(820, 460);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Информация о локальном сервере
            lblLocalCoverage = new Label
            {
                Location = new Point(20, 15),
                Size = new Size(670, 20),
                Text = "Локальный сервер: загрузка...",
                Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold)
            };

            // Таблица серверов
            lvServers = new ListView
            {
                Location = new Point(20, 45),
                Size = new Size(670, 280),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                CheckBoxes = true,  // Включаем чекбоксы
                OwnerDraw = true
            };

            lvServers.DrawColumnHeader += LvServers_DrawColumnHeader;
            lvServers.DrawSubItem += LvServers_DrawSubItem;

            // Колонки
            colNumber = new ColumnHeader { Text = "#", Width = 35 };
            colName = new ColumnHeader { Text = "Название", Width = 105 };
            colAddress = new ColumnHeader { Text = "Адрес", Width = 120 };
            colUrl = new ColumnHeader { Text = "URL", Width = 110 };
            colStatus = new ColumnHeader { Text = "Статус", Width = 70 };
            colVersion = new ColumnHeader { Text = "Версия", Width = 60 };
            colCoverage = new ColumnHeader { Text = "Покрытие", Width = 115 };
            lvServers.Columns.AddRange(new[] { colNumber, colName, colAddress, colUrl, colStatus, colVersion, colCoverage });

            lvServers.SelectedIndexChanged += LvServers_SelectedIndexChanged;
            lvServers.ItemChecked += LvServers_ItemChecked;
            lvServers.DoubleClick += LvServers_DoubleClick;
            lvServers.ColumnClick += LvServers_ColumnClick;

            // Кнопки
            int btnX = 710;
            int btnY = 45;
            int btnWidth = 90;
            int btnHeight = 28;
            int btnSpacing = 35;

            btnAdd = new Button
            {
                Text = "Добавить",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight)
            };
            btnAdd.Click += BtnAdd_Click;
            btnY += btnSpacing;

            btnEdit = new Button
            {
                Text = "Изменить",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight),
                Enabled = false
            };
            btnEdit.Click += BtnEdit_Click;
            btnY += btnSpacing;

            btnDelete = new Button
            {
                Text = "Удалить",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight),
                Enabled = false
            };
            btnDelete.Click += BtnDelete_Click;
            btnY += btnSpacing;

            btnSetDefault = new Button
            {
                Text = "По умолч.",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight),
                Enabled = false
            };
            btnSetDefault.Click += BtnSetDefault_Click;
            btnY += btnSpacing + 10;

            btnRefresh = new Button
            {
                Text = "Обновить",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight)
            };
            btnRefresh.Click += BtnRefresh_Click;
            btnY += btnSpacing;

            btnSelectAll = new Button
            {
                Text = "Выбрать все",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight)
            };
            btnSelectAll.Click += BtnSelectAll_Click;
            btnY += btnSpacing + 10;

            btnImport = new Button
            {
                Text = "Импорт",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight)
            };
            btnImport.Click += BtnImport_Click;
            btnY += btnSpacing;

            btnExport = new Button
            {
                Text = "Экспорт",
                Location = new Point(btnX, btnY),
                Size = new Size(btnWidth, btnHeight)
            };
            btnExport.Click += BtnExport_Click;

            // Информационная надпись
            lblInfo = new Label
            {
                Location = new Point(20, 335),
                Size = new Size(670, 40),
                Text = "Добавьте удалённые серверы ЛЭРС для генерации отчётов из нескольких источников.\nПокрытие: процент точек учёта с состоянием ≠ Нет данных (Данные из Сводки).",
                ForeColor = Color.Gray
            };

            // Кнопка закрытия
            btnClose = new Button
            {
                Text = "Закрыть",
                Location = new Point(710, 380),
                Size = new Size(90, 28),
                DialogResult = DialogResult.OK
            };

            this.Controls.AddRange(new Control[]
            {
                lblLocalCoverage,
                lvServers,
                btnAdd, btnEdit, btnDelete, btnSetDefault, btnRefresh, btnSelectAll,
                btnImport, btnExport,
                lblInfo, btnClose
            });

            this.AcceptButton = btnClose;
            this.Load += ServerManagerForm_Load;
        }

        private async void ServerManagerForm_Load(object sender, EventArgs e)
        {
            // Отображаем кэшированные данные локального сервера
            UpdateLocalCoverageLabel();

            // Запускаем обновление покрытия для локального сервера
            _ = DataCoverageService.Instance.GetLocalCoverageAsync();

            // Проверяем статусы удалённых серверов (и запускаем загрузку покрытия для них)
            await CheckServerStatusesAsync();

            // Запускаем таймер автоматической проверки
            _refreshTimer?.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Автоматическая проверка серверов
            _ = DataCoverageService.Instance.GetLocalCoverageAsync();
            await CheckServerStatusesAsync();
        }

        private void UpdateLocalCoverageLabel()
        {
            var localCoverage = DataCoverageService.Instance.GetCachedCoverage("Локальный");
            if (localCoverage != null && localCoverage.Success)
            {
                lblLocalCoverage.Text = $"Локальный сервер. Покрытие: {localCoverage.FormattedCoverageFull}";
                lblLocalCoverage.ForeColor = GetCoverageColor(localCoverage.CoveragePercent);
            }
            else if (localCoverage != null)
            {
                lblLocalCoverage.Text = $"Локальный сервер: {localCoverage.ErrorMessage}";
                lblLocalCoverage.ForeColor = Color.Red;
            }
            else
            {
                lblLocalCoverage.Text = "Локальный сервер: загрузка...";
                lblLocalCoverage.ForeColor = Color.Gray;
            }
        }

        private void OnCoverageUpdated(object sender, DataCoverageResult result)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnCoverageUpdated(sender, result)));
                return;
            }

            if (result.ServerName == "Локальный")
            {
                UpdateLocalCoverageLabel();
            }
            else
            {
                // Обновляем строку в списке (покрытие на индексе 6)
                foreach (ListViewItem item in lvServers.Items)
                {
                    var server = item.Tag as ServerConfig;
                    if (server != null && server.Name == result.ServerName)
                    {
                        item.SubItems[6].Text = result.FormattedCoverage;
                        break;
                    }
                }
            }
        }

        private void LoadServers()
        {
            lvServers.Items.Clear();

            int number = 1;
            foreach (var server in SettingsService.Instance.Servers)
            {
                // Первая колонка - номер
                var item = new ListViewItem(number.ToString());

                // Вторая колонка - название (с маркером по умолчанию)
                string serverName = server.Name;
                if (server.IsDefault)
                {
                    serverName += " *";
                }
                item.SubItems.Add(serverName);

                item.SubItems.Add(server.Address ?? "");  // Адрес
                item.SubItems.Add(server.Url);            // URL
                item.SubItems.Add("Проверка...");         // Статус
                item.SubItems.Add("—");                   // Версия

                // Покрытие из кэша
                var cachedCoverage = DataCoverageService.Instance.GetCachedCoverage(server.Name);
                item.SubItems.Add(cachedCoverage?.FormattedCoverage ?? "—");

                item.Tag = server;

                // Выделяем сервер по умолчанию жирным
                if (server.IsDefault)
                {
                    item.Font = new Font(lvServers.Font, FontStyle.Bold);
                }

                lvServers.Items.Add(item);
                number++;
            }

            // Применяем сохранённую сортировку
            ApplySorting();

            UpdateButtonStates();
        }

        private async Task CheckServerStatusesAsync()
        {
            // Создаём список задач для параллельной проверки
            var tasks = new List<Task>();

            foreach (ListViewItem item in lvServers.Items)
            {
                var server = item.Tag as ServerConfig;
                if (server == null) continue;

                // Обновляем статус "Проверка..." (индекс 3)
                item.SubItems[4].Text = "Проверка...";
                item.SubItems[5].Text = "—";  // Версия
                item.ForeColor = Color.Gray;

                // Запускаем проверку сервера в отдельной задаче
                var task = CheckSingleServerAsync(item, server);
                tasks.Add(task);
            }

            // Ждём завершения всех проверок параллельно
            await Task.WhenAll(tasks);
        }

        private async Task CheckSingleServerAsync(ListViewItem item, ServerConfig server)
        {
            try
            {
                using (var client = new LersProxyClient(server))
                {
                    bool online = await client.CheckHealthAsync();

                    // Обновляем UI в основном потоке
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => UpdateServerStatus(item, server, online, client)));
                    }
                    else
                    {
                        await UpdateServerStatusAsync(item, server, online, client);
                    }
                }
            }
            catch
            {
                // Обновляем UI в основном потоке
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        item.SubItems[4].Text = "Ошибка";
                        item.ForeColor = Color.Red;
                        item.SubItems[5].Text = "—";
                        item.SubItems[6].Text = "—";
                    }));
                }
                else
                {
                    item.SubItems[4].Text = "Ошибка";
                    item.ForeColor = Color.Red;
                    item.SubItems[5].Text = "—";
                    item.SubItems[6].Text = "—";
                }
            }
        }

        private async Task UpdateServerStatusAsync(ListViewItem item, ServerConfig server, bool online, LersProxyClient client)
        {
            if (online)
            {
                item.SubItems[4].Text = "Онлайн";
                item.ForeColor = Color.Green;

                // Получаем версию прокси
                var version = await client.GetVersionAsync();
                item.SubItems[5].Text = version ?? "?";

                // Запускаем загрузку покрытия для этого сервера
                _ = DataCoverageService.Instance.GetRemoteCoverageAsync(server);
            }
            else
            {
                item.SubItems[4].Text = "Недоступен";
                item.ForeColor = Color.Red;
                item.SubItems[5].Text = "—";
                item.SubItems[6].Text = "—";
            }
        }

        private void UpdateServerStatus(ListViewItem item, ServerConfig server, bool online, LersProxyClient client)
        {
            // Синхронная версия для вызова из Invoke
            var task = UpdateServerStatusAsync(item, server, online, client);
            task.Wait();
        }

        private Color GetCoverageColor(double percent)
        {
            if (percent >= Constants.CoverageThresholds.Excellent) return Color.Green;
            if (percent >= Constants.CoverageThresholds.Acceptable) return Color.DarkOrange;
            return Color.Red;
        }

        private void LvServers_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LvServers_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Колонка покрытия (индекс 6) рисуется как прогресс-бар
            if (e.ColumnIndex == 6)
            {
                DrawCoverageBar(e);
            }
            else
            {
                e.DrawDefault = true;
            }
        }

        private void DrawCoverageBar(DrawListViewSubItemEventArgs e)
        {
            var server = e.Item.Tag as ServerConfig;
            if (server == null)
            {
                e.DrawDefault = true;
                return;
            }

            var coverage = DataCoverageService.Instance.GetCachedCoverage(server.Name);

            // Фон ячейки
            Color bgColor = e.Item.Selected ? SystemColors.Highlight : e.Item.BackColor;
            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            if (coverage == null || !coverage.Success)
            {
                // Нет данных — показываем текст
                string text = coverage?.ErrorMessage ?? "—";
                TextRenderer.DrawText(e.Graphics, text, e.Item.Font, e.Bounds,
                    e.Item.Selected ? SystemColors.HighlightText : Color.Gray,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                return;
            }

            // Отступы для прогресс-бара
            int padding = 3;
            var barRect = new Rectangle(
                e.Bounds.X + padding,
                e.Bounds.Y + padding,
                e.Bounds.Width - padding * 2,
                e.Bounds.Height - padding * 2);

            // Рамка прогресс-бара
            using (var pen = new Pen(Color.Gray))
            {
                e.Graphics.DrawRectangle(pen, barRect);
            }

            // Заполненная часть
            int fillWidth = (int)(barRect.Width * coverage.CoveragePercent / 100.0);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(barRect.X + 1, barRect.Y + 1, fillWidth - 1, barRect.Height - 1);
                Color barColor = GetCoverageColor(coverage.CoveragePercent);

                using (var brush = new SolidBrush(barColor))
                {
                    e.Graphics.FillRectangle(brush, fillRect);
                }
            }

            // Текст поверх бара с тенью для контраста
            string label = coverage.FormattedCoverage;
            var textColor = e.Item.Selected ? SystemColors.HighlightText : Color.White;

            // Тень (смещение на 1 пиксель)
            var shadowRect = new Rectangle(barRect.X + 1, barRect.Y + 1, barRect.Width, barRect.Height);
            TextRenderer.DrawText(e.Graphics, label, e.Item.Font, shadowRect,
                Color.FromArgb(128, 0, 0, 0),
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

            // Основной текст
            TextRenderer.DrawText(e.Graphics, label, e.Item.Font, barRect,
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = lvServers.SelectedItems.Count > 0;

            // Проверяем наличие отмеченных чекбоксами серверов
            int checkedCount = 0;
            foreach (ListViewItem item in lvServers.Items)
            {
                if (item.Checked)
                {
                    checkedCount++;
                }
            }

            // Кнопка "Изменить" - только если выбран один сервер
            btnEdit.Enabled = hasSelection;

            // Кнопка "Удалить" - если есть отмеченные ИЛИ выбранный сервер
            btnDelete.Enabled = checkedCount > 0 || hasSelection;

            // Кнопка "По умолчанию" - только если выбран один сервер
            btnSetDefault.Enabled = hasSelection;

            if (hasSelection)
            {
                var server = lvServers.SelectedItems[0].Tag as ServerConfig;
                if (server != null)
                {
                    btnSetDefault.Enabled = !server.IsDefault;
                }
            }
        }

        private void LvServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void LvServers_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void LvServers_DoubleClick(object sender, EventArgs e)
        {
            if (lvServers.SelectedItems.Count > 0)
            {
                EditSelectedServer();
            }
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var form = new ServerEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SettingsService.Instance.AddServer(form.Server);
                    LoadServers();
                    await CheckServerStatusesAsync();
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            EditSelectedServer();
        }

        private async void EditSelectedServer()
        {
            if (lvServers.SelectedItems.Count == 0)
                return;

            var server = lvServers.SelectedItems[0].Tag as ServerConfig;
            if (server == null) return;

            using (var form = new ServerEditForm(server))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SettingsService.Instance.UpdateServer(form.Server);
                    LoadServers();
                    await CheckServerStatusesAsync();
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            // Собираем отмеченные серверы
            var serversToDelete = new List<ServerConfig>();
            foreach (ListViewItem item in lvServers.Items)
            {
                if (item.Checked)
                {
                    var server = item.Tag as ServerConfig;
                    if (server != null)
                    {
                        serversToDelete.Add(server);
                    }
                }
            }

            // Если ничего не отмечено - берём выбранный сервер (обратная совместимость)
            if (serversToDelete.Count == 0 && lvServers.SelectedItems.Count > 0)
            {
                var server = lvServers.SelectedItems[0].Tag as ServerConfig;
                if (server != null)
                {
                    serversToDelete.Add(server);
                }
            }

            if (serversToDelete.Count == 0)
                return;

            // Подтверждение
            string message;
            if (serversToDelete.Count == 1)
            {
                message = $"Удалить сервер '{serversToDelete[0].Name}'?";
            }
            else
            {
                message = $"Удалить выбранные серверы ({serversToDelete.Count})?";
            }

            var result = MessageBox.Show(
                message,
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                foreach (var server in serversToDelete)
                {
                    SettingsService.Instance.RemoveServer(server.Id);
                }
                LoadServers();
            }
        }

        private void BtnSetDefault_Click(object sender, EventArgs e)
        {
            if (lvServers.SelectedItems.Count == 0)
                return;

            var server = lvServers.SelectedItems[0].Tag as ServerConfig;
            if (server == null) return;

            server.IsDefault = true;
            SettingsService.Instance.UpdateServer(server);
            LoadServers();
        }

        private async void BtnRefresh_Click(object sender, EventArgs e)
        {
            // Временно останавливаем таймер
            _refreshTimer?.Stop();

            btnRefresh.Enabled = false;
            btnRefresh.Text = "Загрузка...";
            lblLocalCoverage.Text = "Локальный сервер: обновление...";
            lblLocalCoverage.ForeColor = Color.Gray;

            try
            {
                // Обновляем покрытие локального сервера и ждём результат
                await DataCoverageService.Instance.GetLocalCoverageAsync();

                // Перезагружаем список и проверяем статусы удалённых серверов
                LoadServers();
                await CheckServerStatusesAsync();
            }
            finally
            {
                btnRefresh.Enabled = true;
                btnRefresh.Text = "Обновить";

                // Перезапускаем таймер
                _refreshTimer?.Start();
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            // Проверяем, есть ли отмеченные серверы
            bool hasChecked = false;
            foreach (ListViewItem item in lvServers.Items)
            {
                if (item.Checked)
                {
                    hasChecked = true;
                    break;
                }
            }

            // Если есть отмеченные - снимаем все, иначе - отмечаем все
            bool newState = !hasChecked;

            foreach (ListViewItem item in lvServers.Items)
            {
                item.Checked = newState;
            }

            // Обновляем текст кнопки
            btnSelectAll.Text = newState ? "Снять все" : "Выбрать все";
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                // Получаем количество отмеченных серверов
                int selectedCount = lvServers.CheckedItems.Count;

                // Показываем диалог выбора опций
                using (var optionsDialog = new ExportOptionsDialog(selectedCount))
                {
                    if (optionsDialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    // Определяем список серверов для экспорта
                    var serversToExport = optionsDialog.ExportAll
                        ? SettingsService.Instance.Servers
                        : GetSelectedServers();

                    if (serversToExport.Count == 0)
                    {
                        MessageBox.Show(
                            "Нет серверов для экспорта",
                            "Ошибка",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    // Запрашиваем мастер-пароль, если требуется шифрование
                    string masterPassword = null;
                    if (optionsDialog.IncludePasswords && optionsDialog.EncryptPasswords)
                    {
                        using (var passwordDialog = new MasterPasswordDialog(MasterPasswordDialog.DialogMode.Encrypt))
                        {
                            if (passwordDialog.ShowDialog(this) != DialogResult.OK)
                                return;

                            masterPassword = passwordDialog.MasterPassword;
                        }
                    }

                    // Выбираем файл для сохранения
                    using (var sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*";
                        sfd.DefaultExt = "json";
                        sfd.FileName = "lers_servers_export.json";
                        sfd.Title = "Экспорт серверов";

                        if (sfd.ShowDialog(this) != DialogResult.OK)
                            return;

                        // Экспортируем
                        ServerImportExportService.ExportServers(
                            sfd.FileName,
                            serversToExport,
                            optionsDialog.IncludePasswords,
                            optionsDialog.EncryptPasswords,
                            masterPassword);

                        MessageBox.Show(
                            $"Экспортировано серверов: {serversToExport.Count}\n" +
                            $"Файл: {sfd.FileName}",
                            "Экспорт завершён",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        Logger.Info($"Экспорт серверов: {serversToExport.Count} в файл {sfd.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка экспорта:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Logger.Error($"Ошибка экспорта серверов: {ex.Message}", ex);
            }
        }

        private async void BtnImport_Click(object sender, EventArgs e)
        {
            try
            {
                // Выбираем файл для импорта
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*";
                    ofd.Title = "Импорт серверов";

                    if (ofd.ShowDialog(this) != DialogResult.OK)
                        return;

                    // Читаем файл для проверки наличия шифрования
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    string json = System.IO.File.ReadAllText(ofd.FileName);
                    var exportData = serializer.Deserialize<ServerExportData>(json);

                    if (exportData == null)
                    {
                        MessageBox.Show(
                            "Некорректный формат файла импорта",
                            "Ошибка",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    // Запрашиваем мастер-пароль, если файл содержит зашифрованные пароли
                    string masterPassword = null;
                    if (exportData.Encrypted)
                    {
                        using (var passwordDialog = new MasterPasswordDialog(MasterPasswordDialog.DialogMode.Decrypt))
                        {
                            var result = passwordDialog.ShowDialog(this);

                            if (result == DialogResult.Cancel)
                                return;

                            if (result == DialogResult.OK)
                            {
                                masterPassword = passwordDialog.MasterPassword;
                            }
                        }
                    }

                    // Импортируем серверы
                    var importedServers = ServerImportExportService.ImportServers(ofd.FileName, masterPassword);

                    if (importedServers.Count == 0)
                    {
                        MessageBox.Show(
                            "Файл не содержит серверов для импорта",
                            "Предупреждение",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    // Умное слияние: обновляем существующие, добавляем новые
                    var mergeResult = ServerImportExportService.MergeServers(
                        SettingsService.Instance.Servers,
                        importedServers);

                    // Сохраняем изменения
                    SettingsService.Instance.Save();

                    // Перезагружаем список и обновляем статусы
                    LoadServers();
                    await CheckServerStatusesAsync();

                    // Показываем результат
                    string message = $"Импорт завершён!\n\n" +
                        $"Добавлено новых серверов: {mergeResult.Added.Count}\n" +
                        $"Обновлено существующих: {mergeResult.Updated.Count}";

                    MessageBox.Show(message, "Импорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    Logger.Info($"Импорт серверов: добавлено {mergeResult.Added.Count}, обновлено {mergeResult.Updated.Count}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка импорта:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Logger.Error($"Ошибка импорта серверов: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Возвращает список отмеченных чекбоксами серверов
        /// </summary>
        private List<ServerConfig> GetSelectedServers()
        {
            var selected = new List<ServerConfig>();

            foreach (ListViewItem item in lvServers.Items)
            {
                if (item.Checked)
                {
                    var server = item.Tag as ServerConfig;
                    if (server != null)
                    {
                        selected.Add(server);
                    }
                }
            }

            return selected;
        }

        /// <summary>
        /// Загружает состояние сортировки из настроек
        /// </summary>
        private void LoadSortingState()
        {
            var settings = SettingsService.Instance.Settings;
            _sortColumn = settings.ServerListSortColumn;
            _sortOrder = (SortOrder)settings.ServerListSortOrder;
        }

        /// <summary>
        /// Применяет сортировку к списку серверов
        /// </summary>
        private void ApplySorting()
        {
            if (_sortColumn >= 0 && _sortOrder != SortOrder.None)
            {
                lvServers.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortOrder);
                lvServers.Sort();
            }
        }

        /// <summary>
        /// Сохраняет состояние сортировки в настройки
        /// </summary>
        private void SaveSortingState()
        {
            var settings = SettingsService.Instance.Settings;
            settings.ServerListSortColumn = _sortColumn;
            settings.ServerListSortOrder = (int)_sortOrder;
            SettingsService.Instance.Save();
        }

        private void LvServers_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Определяем направление сортировки
            if (e.Column == _sortColumn)
            {
                // Переключаем направление
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                // Новая колонка - сортируем по возрастанию
                _sortColumn = e.Column;
                _sortOrder = SortOrder.Ascending;
            }

            // Устанавливаем компаратор
            lvServers.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortOrder);
            lvServers.Sort();

            // Сохраняем состояние сортировки
            SaveSortingState();
        }
    }

    /// <summary>
    /// Компаратор для сортировки элементов ListView
    /// </summary>
    class ListViewItemComparer : System.Collections.IComparer
    {
        private int _column;
        private SortOrder _order;

        public ListViewItemComparer(int column, SortOrder order)
        {
            _column = column;
            _order = order;
        }

        public int Compare(object x, object y)
        {
            var itemX = (ListViewItem)x;
            var itemY = (ListViewItem)y;

            string textX = itemX.SubItems[_column].Text;
            string textY = itemY.SubItems[_column].Text;

            int result;

            // Специальная обработка для номера (колонка 0)
            if (_column == 0)
            {
                int numX = int.TryParse(textX, out int nx) ? nx : 0;
                int numY = int.TryParse(textY, out int ny) ? ny : 0;
                result = numX.CompareTo(numY);
            }
            // Специальная обработка для покрытия (колонка 6) - сортировка по проценту
            else if (_column == 6)
            {
                double percentX = ParseCoveragePercent(textX);
                double percentY = ParseCoveragePercent(textY);
                result = percentX.CompareTo(percentY);
            }
            else
            {
                // Обычное текстовое сравнение
                result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            }

            // Применяем направление сортировки
            return _order == SortOrder.Ascending ? result : -result;
        }

        private double ParseCoveragePercent(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "—")
                return -1;

            // Извлекаем число из строки вида "85.5%"
            string numStr = text.Replace("%", "").Trim();
            return double.TryParse(numStr, out double percent) ? percent : -1;
        }
    }
}
