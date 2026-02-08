using System;
using System.Drawing;
using System.Windows.Forms;

namespace LersReportGeneratorPlugin.Forms
{
    /// <summary>
    /// Диалог ввода мастер-пароля для шифрования/расшифровки
    /// </summary>
    public class MasterPasswordDialog : Form
    {
        private Label lblTitle;
        private Label lblPassword;
        private Label lblConfirm;
        private TextBox txtPassword;
        private TextBox txtConfirm;
        private Label lblWarning;
        private Button btnOk;
        private Button btnCancel;

        /// <summary>
        /// Введённый мастер-пароль
        /// </summary>
        public string MasterPassword { get; private set; }

        /// <summary>
        /// Режим диалога
        /// </summary>
        public enum DialogMode
        {
            /// <summary>Ввод пароля для экспорта (с подтверждением)</summary>
            Encrypt,
            /// <summary>Ввод пароля для импорта (без подтверждения)</summary>
            Decrypt
        }

        private readonly DialogMode _mode;

        public MasterPasswordDialog(DialogMode mode)
        {
            _mode = mode;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = _mode == DialogMode.Encrypt ? "Мастер-пароль для шифрования" : "Мастер-пароль для расшифровки";
            this.ClientSize = new Size(450, _mode == DialogMode.Encrypt ? 250 : 210);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 15;

            // Заголовок
            lblTitle = new Label
            {
                Location = new Point(20, y),
                Size = new Size(410, 40),
                Text = _mode == DialogMode.Encrypt
                    ? "Введите мастер-пароль для шифрования паролей серверов.\nЭтот пароль потребуется при импорте."
                    : "Файл содержит зашифрованные пароли.\nВведите мастер-пароль для расшифровки.",
                Font = new Font(this.Font, FontStyle.Regular)
            };
            y += 50;

            // Поле пароля
            lblPassword = new Label
            {
                Location = new Point(20, y + 3),
                Size = new Size(120, 20),
                Text = "Мастер-пароль:",
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtPassword = new TextBox
            {
                Location = new Point(145, y),
                Size = new Size(285, 23),
                PasswordChar = '●',
                UseSystemPasswordChar = false
            };
            y += 35;

            // Подтверждение пароля (только для экспорта)
            if (_mode == DialogMode.Encrypt)
            {
                lblConfirm = new Label
                {
                    Location = new Point(20, y + 3),
                    Size = new Size(120, 20),
                    Text = "Подтверждение:",
                    TextAlign = ContentAlignment.MiddleLeft
                };

                txtConfirm = new TextBox
                {
                    Location = new Point(145, y),
                    Size = new Size(285, 23),
                    PasswordChar = '●',
                    UseSystemPasswordChar = false
                };
                y += 35;
            }

            // Предупреждение
            lblWarning = new Label
            {
                Location = new Point(20, y),
                Size = new Size(410, 40),
                Text = _mode == DialogMode.Encrypt
                    ? "⚠ ВАЖНО: Запомните этот пароль!\nБез него восстановить данные невозможно."
                    : "⚠ При неверном пароле импорт будет полностью отменён.",
                ForeColor = _mode == DialogMode.Encrypt ? Color.Red : Color.DarkOrange,
                Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold)
            };
            y += 55;

            // Кнопки
            int btnY = y;

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(250, btnY),
                Size = new Size(90, 28),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(350, btnY),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);

            if (_mode == DialogMode.Encrypt)
            {
                this.Controls.Add(lblConfirm);
                this.Controls.Add(txtConfirm);
            }

            this.Controls.Add(lblWarning);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            // Фокус на поле пароля
            txtPassword.Select();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string password = txtPassword.Text;

            // Проверка пустого пароля
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Введите мастер-пароль",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtPassword.Focus();
                this.DialogResult = DialogResult.None;
                return;
            }

            // Проверка минимальной длины (для безопасности)
            if (password.Length < 4)
            {
                MessageBox.Show(
                    "Мастер-пароль должен содержать минимум 4 символа",
                    "Слишком короткий пароль",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtPassword.Focus();
                this.DialogResult = DialogResult.None;
                return;
            }

            // Проверка подтверждения (только для экспорта)
            if (_mode == DialogMode.Encrypt)
            {
                string confirm = txtConfirm.Text;

                if (password != confirm)
                {
                    MessageBox.Show(
                        "Пароли не совпадают. Проверьте ввод.",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    txtConfirm.Text = "";
                    txtConfirm.Focus();
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            MasterPassword = password;
        }
    }
}
