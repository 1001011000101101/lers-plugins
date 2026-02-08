using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using LersReportGeneratorPlugin.Services;

namespace LersReportGeneratorPlugin.Forms
{
    public class AboutForm : XtraForm
    {
        public AboutForm()
        {
            InitializeComponent();
            UpdateVersionInfo();
        }

        private void UpdateVersionInfo()
        {
            lblVersion.Text = VersionInfo.FullDisplayVersion;
        }

        private void InitializeComponent()
        {
            this.lblTitle = new LabelControl();
            this.lblVersion = new LabelControl();
            this.lblDescription = new LabelControl();
            this.lblContactHeader = new LabelControl();
            this.lblDeveloper = new LabelControl();
            this.lblEmail = new LabelControl();
            this.lnkEmail = new HyperlinkLabelControl();
            this.lblPhone = new LabelControl();
            this.lnkPhone = new HyperlinkLabelControl();
            this.lblTelegram = new LabelControl();
            this.lnkTelegram = new HyperlinkLabelControl();
            this.lblMax = new LabelControl();
            this.lnkMax = new HyperlinkLabelControl();
            this.btnOk = new SimpleButton();
            this.panelHeader = new PanelControl();
            this.separator = new LabelControl();

            this.SuspendLayout();

            // === Header Panel ===
            this.panelHeader.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            this.panelHeader.Location = new Point(0, 0);
            this.panelHeader.Size = new Size(380, 80);
            this.panelHeader.Appearance.BackColor = Color.FromArgb(0, 122, 204);
            this.panelHeader.Appearance.Options.UseBackColor = true;

            // lblTitle - в хедере
            this.lblTitle.Text = "Генератор отчётов ЛЭРС";
            this.lblTitle.Appearance.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            this.lblTitle.Appearance.ForeColor = Color.White;
            this.lblTitle.Appearance.Options.UseFont = true;
            this.lblTitle.Appearance.Options.UseForeColor = true;
            this.lblTitle.Location = new Point(20, 18);
            this.lblTitle.AutoSizeMode = LabelAutoSizeMode.Default;
            this.panelHeader.Controls.Add(this.lblTitle);

            // lblVersion - в хедере
            this.lblVersion.Text = "Версия 1.0.0";
            this.lblVersion.Appearance.ForeColor = Color.FromArgb(200, 220, 255);
            this.lblVersion.Appearance.Options.UseForeColor = true;
            this.lblVersion.Location = new Point(20, 48);
            this.lblVersion.AutoSizeMode = LabelAutoSizeMode.Default;
            this.panelHeader.Controls.Add(this.lblVersion);

            // lblDescription
            this.lblDescription.Text = "Плагин для массовой генерации отчётов\nпо точкам учёта в системе ЛЭРС УЧЁТ.\n\nПозволяет выгружать отчёты по всем точкам\nвыбранного типа ресурса в формате Excel или PDF.";
            this.lblDescription.Location = new Point(20, 95);
            this.lblDescription.AutoSizeMode = LabelAutoSizeMode.None;
            this.lblDescription.Size = new Size(340, 85);
            this.lblDescription.Appearance.ForeColor = Color.FromArgb(60, 60, 60);
            this.lblDescription.Appearance.Options.UseForeColor = true;

            // separator
            this.separator.Text = "";
            this.separator.Location = new Point(20, 185);
            this.separator.Size = new Size(340, 1);
            this.separator.AutoSizeMode = LabelAutoSizeMode.None;
            this.separator.Appearance.BackColor = Color.FromArgb(220, 220, 220);
            this.separator.Appearance.Options.UseBackColor = true;

            // lblContactHeader
            this.lblContactHeader.Text = "По вопросам автоматизаций, миграций и интеграций\nсистем диспетчеризации, а также по работе данного\nмодуля со мной можно связаться следующим образом:";
            this.lblContactHeader.Location = new Point(20, 195);
            this.lblContactHeader.AutoSizeMode = LabelAutoSizeMode.None;
            this.lblContactHeader.Size = new Size(340, 55);
            this.lblContactHeader.Appearance.ForeColor = Color.FromArgb(80, 80, 80);
            this.lblContactHeader.Appearance.Options.UseForeColor = true;

            // lblDeveloper
            this.lblDeveloper.Text = "Разработчик: Матюшкин Роман";
            this.lblDeveloper.Appearance.Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold);
            this.lblDeveloper.Appearance.ForeColor = Color.FromArgb(0, 122, 204);
            this.lblDeveloper.Appearance.Options.UseFont = true;
            this.lblDeveloper.Appearance.Options.UseForeColor = true;
            this.lblDeveloper.Location = new Point(20, 260);
            this.lblDeveloper.AutoSizeMode = LabelAutoSizeMode.Default;

            // lblEmail
            this.lblEmail.Text = "Email:";
            this.lblEmail.Location = new Point(20, 285);
            this.lblEmail.Appearance.ForeColor = Color.FromArgb(100, 100, 100);
            this.lblEmail.Appearance.Options.UseForeColor = true;

            // lnkEmail
            this.lnkEmail.Text = "0406590@gmail.com";
            this.lnkEmail.Location = new Point(90, 285);
            this.lnkEmail.HyperlinkClick += lnkEmail_HyperlinkClick;

            // lblPhone
            this.lblPhone.Text = "Телефон:";
            this.lblPhone.Location = new Point(20, 308);
            this.lblPhone.Appearance.ForeColor = Color.FromArgb(100, 100, 100);
            this.lblPhone.Appearance.Options.UseForeColor = true;

            // lnkPhone
            this.lnkPhone.Text = "+7 922 040 65 90";
            this.lnkPhone.Location = new Point(90, 308);
            this.lnkPhone.HyperlinkClick += lnkPhone_HyperlinkClick;

            // lblTelegram
            this.lblTelegram.Text = "Telegram:";
            this.lblTelegram.Location = new Point(20, 331);
            this.lblTelegram.Appearance.ForeColor = Color.FromArgb(100, 100, 100);
            this.lblTelegram.Appearance.Options.UseForeColor = true;

            // lnkTelegram
            this.lnkTelegram.Text = "@RomanMatyushkin";
            this.lnkTelegram.Location = new Point(90, 331);
            this.lnkTelegram.HyperlinkClick += lnkTelegram_HyperlinkClick;

            // lblMax
            this.lblMax.Text = "MAX:";
            this.lblMax.Location = new Point(20, 354);
            this.lblMax.Appearance.ForeColor = Color.FromArgb(100, 100, 100);
            this.lblMax.Appearance.Options.UseForeColor = true;

            // lnkMax
            this.lnkMax.Text = "max.ru/u/f9LHodD0cOJmSrq36ZPsZ...";
            this.lnkMax.Location = new Point(90, 354);
            this.lnkMax.HyperlinkClick += lnkMax_HyperlinkClick;

            // btnOk
            this.btnOk.Text = "OK";
            this.btnOk.Location = new Point(145, 390);
            this.btnOk.Size = new Size(90, 30);
            this.btnOk.Appearance.Font = new Font(this.Font.FontFamily, 9F, FontStyle.Bold);
            this.btnOk.Appearance.Options.UseFont = true;
            this.btnOk.Click += (s, e) => Close();

            // AboutForm
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(380, 435);
            this.Controls.Add(this.panelHeader);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.separator);
            this.Controls.Add(this.lblContactHeader);
            this.Controls.Add(this.lblDeveloper);
            this.Controls.Add(this.lblEmail);
            this.Controls.Add(this.lnkEmail);
            this.Controls.Add(this.lblPhone);
            this.Controls.Add(this.lnkPhone);
            this.Controls.Add(this.lblTelegram);
            this.Controls.Add(this.lnkTelegram);
            this.Controls.Add(this.lblMax);
            this.Controls.Add(this.lnkMax);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "О модуле";

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void lnkEmail_HyperlinkClick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mailto:0406590@gmail.com",
                    UseShellExecute = true
                });
            }
            catch { } // Обработчик mailto:/tel:/https: может отсутствовать
        }

        private void lnkPhone_HyperlinkClick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "tel:+79220406590",
                    UseShellExecute = true
                });
            }
            catch { } // Обработчик mailto:/tel:/https: может отсутствовать
        }

        private void lnkTelegram_HyperlinkClick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://t.me/RomanMatyushkin",
                    UseShellExecute = true
                });
            }
            catch { } // Обработчик mailto:/tel:/https: может отсутствовать
        }

        private void lnkMax_HyperlinkClick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://max.ru/u/f9LHodD0cOJmSrq36ZPsZ_cI6OHC0O_IZhfc6AxIXuDnBRctTT7xWQCOMr8",
                    UseShellExecute = true
                });
            }
            catch { } // Обработчик mailto:/tel:/https: может отсутствовать
        }

        private PanelControl panelHeader;
        private LabelControl lblTitle;
        private LabelControl lblVersion;
        private LabelControl lblDescription;
        private LabelControl separator;
        private LabelControl lblContactHeader;
        private LabelControl lblDeveloper;
        private LabelControl lblEmail;
        private HyperlinkLabelControl lnkEmail;
        private LabelControl lblPhone;
        private HyperlinkLabelControl lnkPhone;
        private LabelControl lblTelegram;
        private HyperlinkLabelControl lnkTelegram;
        private LabelControl lblMax;
        private HyperlinkLabelControl lnkMax;
        private SimpleButton btnOk;
    }
}
