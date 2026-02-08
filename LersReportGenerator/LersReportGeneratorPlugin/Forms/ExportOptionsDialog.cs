using System;
using System.Drawing;
using System.Windows.Forms;

namespace LersReportGeneratorPlugin.Forms
{
    /// <summary>
    /// Диалог выбора опций экспорта серверов
    /// </summary>
    public class ExportOptionsDialog : Form
    {
        private GroupBox grpScope;
        private RadioButton rbAllServers;
        private RadioButton rbSelectedOnly;
        private CheckBox chkIncludePasswords;
        private CheckBox chkEncryptPasswords;
        private Label lblWarning;
        private Button btnOk;
        private Button btnCancel;

        /// <summary>
        /// Экспортировать все серверы (иначе - только выбранные)
        /// </summary>
        public bool ExportAll { get; private set; }

        /// <summary>
        /// Включить пароли в экспорт
        /// </summary>
        public bool IncludePasswords { get; private set; }

        /// <summary>
        /// Шифровать пароли с мастер-паролем
        /// </summary>
        public bool EncryptPasswords { get; private set; }

        /// <summary>
        /// Количество выбранных серверов (для отображения в UI)
        /// </summary>
        private readonly int _selectedCount;

        public ExportOptionsDialog(int selectedCount)
        {
            _selectedCount = selectedCount;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Опции экспорта";
            this.Size = new Size(450, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            int y = 20;

            // Группа выбора серверов
            grpScope = new GroupBox
            {
                Location = new Point(20, y),
                Size = new Size(400, 80),
                Text = "Какие серверы экспортировать?"
            };

            rbAllServers = new RadioButton
            {
                Location = new Point(15, 25),
                Size = new Size(370, 20),
                Text = "Все серверы",
                Checked = true
            };

            rbSelectedOnly = new RadioButton
            {
                Location = new Point(15, 50),
                Size = new Size(370, 20),
                Text = _selectedCount > 0
                    ? $"Только выбранные ({_selectedCount})"
                    : "Только выбранные (нет выбранных)",
                Enabled = _selectedCount > 0
            };

            grpScope.Controls.Add(rbAllServers);
            grpScope.Controls.Add(rbSelectedOnly);

            y += 90;

            // Опции паролей
            chkIncludePasswords = new CheckBox
            {
                Location = new Point(20, y),
                Size = new Size(400, 20),
                Text = "Включить пароли в экспорт",
                Checked = false
            };
            chkIncludePasswords.CheckedChanged += ChkIncludePasswords_CheckedChanged;
            y += 30;

            chkEncryptPasswords = new CheckBox
            {
                Location = new Point(40, y),
                Size = new Size(380, 20),
                Text = "Зашифровать пароли с мастер-паролем (рекомендуется)",
                Checked = true,
                Enabled = false // Включается только если включены пароли
            };
            y += 30;

            // Предупреждение
            lblWarning = new Label
            {
                Location = new Point(20, y),
                Size = new Size(400, 40),
                Text = "",
                ForeColor = Color.Red,
                Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold),
                Visible = false
            };
            y += 50;

            // Кнопки
            btnOk = new Button
            {
                Text = "Экспортировать",
                Location = new Point(230, y),
                Size = new Size(110, 28),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(350, y),
                Size = new Size(70, 28),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(grpScope);
            this.Controls.Add(chkIncludePasswords);
            this.Controls.Add(chkEncryptPasswords);
            this.Controls.Add(lblWarning);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void ChkIncludePasswords_CheckedChanged(object sender, EventArgs e)
        {
            bool includePasswords = chkIncludePasswords.Checked;

            chkEncryptPasswords.Enabled = includePasswords;

            if (includePasswords)
            {
                // Показываем предупреждение, если пароли НЕ будут зашифрованы
                if (!chkEncryptPasswords.Checked)
                {
                    lblWarning.Text = "⚠ ВНИМАНИЕ: Пароли будут сохранены в открытом виде!\nЗащитите файл экспорта от посторонних.";
                    lblWarning.Visible = true;
                }
                else
                {
                    lblWarning.Visible = false;
                }

                // Подписываемся на изменение чекбокса шифрования
                chkEncryptPasswords.CheckedChanged += ChkEncryptPasswords_CheckedChanged;
            }
            else
            {
                lblWarning.Visible = false;
                chkEncryptPasswords.CheckedChanged -= ChkEncryptPasswords_CheckedChanged;
            }
        }

        private void ChkEncryptPasswords_CheckedChanged(object sender, EventArgs e)
        {
            if (chkIncludePasswords.Checked && !chkEncryptPasswords.Checked)
            {
                lblWarning.Text = "⚠ ВНИМАНИЕ: Пароли будут сохранены в открытом виде!\nЗащитите файл экспорта от посторонних.";
                lblWarning.Visible = true;
            }
            else
            {
                lblWarning.Visible = false;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            ExportAll = rbAllServers.Checked;
            IncludePasswords = chkIncludePasswords.Checked;
            EncryptPasswords = chkEncryptPasswords.Checked && chkIncludePasswords.Checked;

            // Дополнительное подтверждение, если пароли в открытом виде
            if (IncludePasswords && !EncryptPasswords)
            {
                var result = MessageBox.Show(
                    "Вы действительно хотите экспортировать пароли в открытом виде?\n\n" +
                    "Любой, кто получит доступ к файлу, сможет прочитать пароли.\n\n" +
                    "Рекомендуется использовать шифрование с мастер-паролем.",
                    "Подтверждение небезопасного экспорта",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }
        }
    }
}
