using System;
using System.Drawing;
using System.Windows.Forms;

namespace BinDay.App
{
    /// <summary>
    /// What the update check found. One dialog for all three answers, so the user
    /// always gets a clear next step.
    /// </summary>
    internal sealed class UpdateForm : Form
    {
        public enum Choice
        {
            Later = 0,
            Download = 1,
            OpenPage = 2
        }

        private readonly string _current;
        private readonly Updater.Result _result;
        private readonly bool _newer;

        public Choice Action { get; private set; }

        public UpdateForm(string current, Updater.Result result)
        {
            _current = Updater.Result.Normalise(current);
            _result = result;
            _newer = result != null && result.NewerThan(current);

            Text = "Check for updates";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 320);
            KeyPreview = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            Build();
        }

        private void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(26, 22, 26, 18);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            string headline;
            string body;

            if (_result == null || !_result.Ok)
            {
                headline = "Could not check for updates";
                body = "BinDay could not reach GitHub.\n\n"
                     + "The newest version is always one click away:\n"
                     + "getbinday.vercel.app";
            }
            else if (_newer)
            {
                headline = "A new version is available";
                body = "You are running BinDay " + _current + ".\n"
                     + "BinDay " + _result.Latest + " is out.\n\n"
                     + "Download it and replace your copy - the new file is a single exe, "
                     + "there is nothing to install.";
            }
            else
            {
                headline = "You are up to date";
                body = "BinDay " + _current + " is the newest version.\n\n"
                     + "Nothing to do.";
            }

            Label title = Ui.Label(headline, Theme.Large, Theme.Text, false);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.AutoEllipsis = true;

            TableLayoutPanel versions = new TableLayoutPanel();
            versions.Dock = DockStyle.Fill;
            versions.ColumnCount = 2;
            versions.RowCount = 2;
            versions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            versions.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            versions.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            versions.Margin = new Padding(0, 4, 0, 0);

            Label yours = Ui.Label("Your copy", Theme.Tiny, Theme.TextDim, false);
            yours.Dock = DockStyle.Fill;
            yours.TextAlign = ContentAlignment.MiddleLeft;

            Label mine = Ui.Label(_current, Theme.Huge, Theme.TextMuted, false);
            mine.Dock = DockStyle.Fill;
            mine.TextAlign = ContentAlignment.MiddleLeft;
            mine.AutoEllipsis = true;

            Label theirsLabel = Ui.Label("Latest release", Theme.Tiny, Theme.TextDim, false);
            theirsLabel.Dock = DockStyle.Fill;
            theirsLabel.TextAlign = ContentAlignment.MiddleLeft;

            string latest = _result != null && _result.Ok ? _result.Latest : "?";
            Label theirs = Ui.Label(latest, Theme.Huge, _newer ? Theme.Accent : Theme.TextMuted, false);
            theirs.Dock = DockStyle.Fill;
            theirs.TextAlign = ContentAlignment.MiddleLeft;
            theirs.AutoEllipsis = true;

            versions.Controls.Add(yours, 0, 0);
            versions.Controls.Add(theirsLabel, 1, 0);
            versions.Controls.Add(mine, 0, 1);
            versions.Controls.Add(theirs, 1, 1);

            Label text = Ui.Label(body, Theme.Small, Theme.TextMuted, false);
            text.Dock = DockStyle.Fill;
            text.TextAlign = ContentAlignment.TopLeft;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);
            buttons.Padding = new Padding(0, 8, 0, 0);

            PillButton primary = new PillButton();
            primary.Style = PillButton.Kind.Solid;
            primary.Width = 176;
            primary.Height = 36;
            primary.Margin = new Padding(8, 0, 0, 0);

            PillButton secondary = new PillButton();
            secondary.Style = PillButton.Kind.Outline;
            secondary.Width = 132;
            secondary.Height = 36;
            secondary.Margin = new Padding(0);

            if (_newer)
            {
                primary.Text = "Download " + latest;
                primary.Click += delegate { Action = Choice.Download; DialogResult = DialogResult.OK; };

                secondary.Text = "Open the page";
                secondary.Click += delegate { Action = Choice.OpenPage; DialogResult = DialogResult.OK; };
            }
            else if (_result != null && _result.Ok)
            {
                primary.Text = "Good";
                primary.Click += delegate { DialogResult = DialogResult.OK; };

                secondary.Text = "Open the page";
                secondary.Click += delegate { Action = Choice.OpenPage; DialogResult = DialogResult.OK; };
            }
            else
            {
                primary.Text = "Open the website";
                primary.Click += delegate { Action = Choice.OpenPage; DialogResult = DialogResult.OK; };

                secondary.Text = "Close";
                secondary.Click += delegate { DialogResult = DialogResult.Cancel; };
            }

            buttons.Controls.Add(primary);
            buttons.Controls.Add(secondary);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(versions, 0, 1);
            root.Controls.Add(text, 0, 2);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);

            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            };
        }
    }
}
