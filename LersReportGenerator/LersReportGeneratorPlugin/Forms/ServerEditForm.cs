using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;
using LersReportGeneratorPlugin.Services;

namespace LersReportGeneratorPlugin.Forms
{
    /// <summary>
    /// Form for adding/editing a server configuration
    /// </summary>
    public class ServerEditForm : Form
    {
        private TextBox txtName;
        private TextBox txtAddress;
        private TextBox txtUrl;
        private TextBox txtLogin;
        private TextBox txtPassword;
        private CheckBox chkDefault;
        private CheckBox chkIgnoreSsl;
        private Button btnTest;
        private Button btnSave;
        private Button btnCancel;
        private Label lblStatus;

        private readonly ServerConfig _server;
        private readonly bool _isNew;

        public ServerConfig Server => _server;

        public ServerEditForm(ServerConfig server = null)
        {
            _isNew = server == null;
            _server = server ?? new ServerConfig();

            InitializeComponent();
            LoadServerData();
        }

        private void InitializeComponent()
        {
            this.Text = _isNew ? "Добавить сервер" : "Редактировать сервер";
            this.Size = new Size(500, 390);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;
            int labelWidth = 100;
            int inputWidth = 340;

            // Название
            var lblName = new Label
            {
                Text = "Название:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 20)
            };
            txtName = new TextBox
            {
                Location = new Point(120, y),
                Size = new Size(inputWidth, 23)
            };
            y += 35;

            // Адрес
            var lblAddress = new Label
            {
                Text = "Адрес:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 20)
            };
            txtAddress = new TextBox
            {
                Location = new Point(120, y),
                Size = new Size(inputWidth, 23)
            };
            y += 35;

            // URL
            var lblUrl = new Label
            {
                Text = "URL:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 20)
            };
            txtUrl = new TextBox
            {
                Location = new Point(120, y),
                Size = new Size(inputWidth, 23)
            };
            y += 35;

            // Логин
            var lblLogin = new Label
            {
                Text = "Логин:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 20)
            };
            txtLogin = new TextBox
            {
                Location = new Point(120, y),
                Size = new Size(inputWidth, 23)
            };
            y += 35;

            // Пароль
            var lblPassword = new Label
            {
                Text = "Пароль:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 20)
            };
            txtPassword = new TextBox
            {
                Location = new Point(120, y),
                Size = new Size(inputWidth, 23),
                UseSystemPasswordChar = true
            };
            y += 35;

            // Флаг "По умолчанию"
            chkDefault = new CheckBox
            {
                Text = "Использовать по умолчанию",
                Location = new Point(120, y),
                AutoSize = true
            };
            y += 28;

            // Флаг "Игнорировать SSL"
            chkIgnoreSsl = new CheckBox
            {
                Text = "Игнорировать ошибки SSL (самоподписанные сертификаты)",
                Location = new Point(120, y),
                AutoSize = true,
                Checked = true
            };
            y += 35;

            // Статус
            lblStatus = new Label
            {
                Location = new Point(120, y),
                Size = new Size(340, 20),
                ForeColor = Color.Gray
            };
            y += 30;

            // Кнопки
            btnTest = new Button
            {
                Text = "Проверить",
                Location = new Point(120, y),
                Size = new Size(100, 28)
            };
            btnTest.Click += BtnTest_Click;

            btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(270, y),
                Size = new Size(90, 28),
                DialogResult = DialogResult.OK
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(370, y),
                Size = new Size(90, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                lblName, txtName,
                lblAddress, txtAddress,
                lblUrl, txtUrl,
                lblLogin, txtLogin,
                lblPassword, txtPassword,
                chkDefault,
                chkIgnoreSsl,
                lblStatus,
                btnTest, btnSave, btnCancel
            });

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadServerData()
        {
            txtName.Text = _server.Name ?? "";
            txtAddress.Text = _server.Address ?? "";
            txtUrl.Text = _server.Url ?? "";
            txtLogin.Text = _server.Login ?? "";
            chkDefault.Checked = _server.IsDefault;
            chkIgnoreSsl.Checked = _server.IgnoreSslErrors;

            // Decrypt and show password placeholder
            if (!string.IsNullOrEmpty(_server.EncryptedPassword))
            {
                string password = CredentialManager.DecryptPassword(_server.EncryptedPassword);
                txtPassword.Text = password ?? "";
            }
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            if (!ValidateInput())
                return;

            btnTest.Enabled = false;
            lblStatus.Text = "Проверка прокси-службы...";
            lblStatus.ForeColor = Color.Gray;

            try
            {
                // Создаём временную конфигурацию для теста
                var testServer = new ServerConfig
                {
                    Url = txtUrl.Text.Trim(),
                    Login = txtLogin.Text.Trim(),
                    IgnoreSslErrors = chkIgnoreSsl.Checked
                };

                using (var client = new LersProxyClient(testServer))
                {
                    // Сначала проверяем доступность прокси-службы
                    bool proxyAvailable = await client.CheckHealthAsync();
                    if (!proxyAvailable)
                    {
                        lblStatus.Text = $"Прокси-служба недоступна ({testServer.Url})";
                        lblStatus.ForeColor = Color.Red;
                        return;
                    }

                    // Получаем версию прокси
                    var proxyVersion = await client.GetVersionAsync();

                    lblStatus.Text = "Авторизация...";

                    // Пробуем авторизоваться
                    var loginResult = await client.LoginAsync(txtLogin.Text.Trim(), txtPassword.Text);
                    if (loginResult.Success)
                    {
                        var versionText = !string.IsNullOrEmpty(proxyVersion) ? $" (v{proxyVersion})" : "";
                        lblStatus.Text = $"Подключение успешно!{versionText}";
                        lblStatus.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblStatus.Text = $"Ошибка: {loginResult.ErrorMessage}";
                        lblStatus.ForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Ошибка: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInput())
            {
                this.DialogResult = DialogResult.None;
                return;
            }

            _server.Name = txtName.Text.Trim();
            _server.Address = txtAddress.Text.Trim();
            _server.Url = txtUrl.Text.Trim();
            _server.Login = txtLogin.Text.Trim();
            _server.IsDefault = chkDefault.Checked;
            _server.IgnoreSslErrors = chkIgnoreSsl.Checked;
            _server.UseProxy = true; // Всегда используем прокси для удалённых серверов

            // Encrypt password
            if (!string.IsNullOrEmpty(txtPassword.Text))
            {
                _server.EncryptedPassword = CredentialManager.EncryptPassword(txtPassword.Text);
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите название сервера.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                MessageBox.Show("Введите адрес сервера.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUrl.Focus();
                return false;
            }

            // Проверка формата URL
            string url = txtUrl.Text.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                MessageBox.Show("Адрес должен начинаться с http:// или https://", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUrl.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                MessageBox.Show("Введите логин.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLogin.Focus();
                return false;
            }

            return true;
        }
    }
}
