using System;
using System.Drawing;
using System.Windows.Forms;

namespace BinDay.App
{
    /// <summary>
    /// A small themed message box. The stock WinForms one is light grey and looks
    /// like it belongs to a different program.
    /// </summary>
    internal sealed class NoteForm : Form
    {
        public NoteForm(string title, string body, string detail, string okText, string secondText)
        {
            Text = "BinDay";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 260);
            KeyPreview = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(26, 22, 26, 18);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            Label heading = Ui.Label(title, Theme.Large, Theme.Text, false);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.AutoEllipsis = true;

            Label message = Ui.Label(body, Theme.Small, Theme.TextMuted, false);
            message.Dock = DockStyle.Fill;
            message.TextAlign = ContentAlignment.TopLeft;
            message.AutoEllipsis = true;

            Label extra = Ui.Label(detail ?? string.Empty, Theme.Mono, Theme.TextDim, false);
            extra.Dock = DockStyle.Fill;
            extra.TextAlign = ContentAlignment.TopLeft;
            extra.AutoEllipsis = true;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);
            buttons.Padding = new Padding(0, 8, 0, 0);

            PillButton ok = new PillButton();
            ok.Text = okText ?? "OK";
            ok.Style = PillButton.Kind.Solid;
            ok.Width = 120;
            ok.Height = 36;
            ok.Margin = new Padding(8, 0, 0, 0);
            ok.Click += delegate { DialogResult = DialogResult.OK; };

            buttons.Controls.Add(ok);

            if (!string.IsNullOrEmpty(secondText))
            {
                PillButton second = new PillButton();
                second.Text = secondText;
                second.Style = PillButton.Kind.Outline;
                second.Width = 140;
                second.Height = 36;
                second.Margin = new Padding(0);
                second.Click += delegate { DialogResult = DialogResult.Cancel; };
                buttons.Controls.Add(second);
            }

            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(message, 0, 1);
            root.Controls.Add(extra, 0, 2);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            AcceptButton = null;
            CancelButton = null;

            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) DialogResult = DialogResult.OK;
            };
        }

        public static void Show(IWin32Window owner, string title, string body, string detail, string okText, string secondText)
        {
            using (NoteForm form = new NoteForm(title, body, detail, okText, secondText))
            {
                form.ShowDialog(owner);
            }
        }
    }
}
