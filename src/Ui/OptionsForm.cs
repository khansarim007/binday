using System;
using System.Drawing;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>What a scan should look for. Everything here is a choice, not a default trap.</summary>
    internal sealed class OptionsForm : Form
    {
        private readonly ToggleRow _dev;
        private readonly ToggleRow _caches;
        private readonly ToggleRow _installers;
        private readonly ToggleRow _duplicates;
        private readonly ToggleRow _media;
        private readonly ToggleRow _large;
        private readonly ComboBox _duplicateSize;
        private readonly NumericUpDown _installerAge;
        private readonly ComboBox _mediaSize;
        private readonly ComboBox _largeSize;

        public ScanOptions Options { get; private set; }

        public OptionsForm(ScanOptions current)
        {
            Text = "Scan options";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(600, 610);

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            Options = current ?? new ScanOptions();

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 13;
            root.Padding = new Padding(26, 20, 26, 16);
            root.BackColor = Theme.Background;

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label heading = Ui.Label("What should a scan look for?", Theme.Large, Theme.Text, false);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;

            Label sub = Ui.Label("Everything below is optional. BinDay still asks before removing anything.",
                Theme.Tiny, Theme.TextDim, false);
            sub.Dock = DockStyle.Fill;
            sub.TextAlign = ContentAlignment.TopLeft;

            _dev = Toggle("Build output and dependency folders",
                "node_modules, bin, obj, target, .venv and friends — only next to a project file", Options.ScanDevArtifacts);
            _caches = Toggle("Package caches and temp files",
                "npm, pip, NuGet, Gradle and Windows temp folders", Options.ScanCaches);
            _installers = Toggle("Old installers and redundant copies",
                "Setup files in Downloads and on the Desktop", Options.ScanInstallers);
            _duplicates = Toggle("Duplicate files",
                "Byte-identical copies, confirmed with SHA-256", Options.ScanDuplicates);
            _media = Toggle("Big media",
                "Videos, photos, audio and APK files above the size below", Options.ScanMedia);
            _large = Toggle("Any very large file",
                "A single file can be worth reviewing even if it is not media", Options.ScanLargeFiles);

            _duplicateSize = Combo("Duplicates bigger than", new SizeChoice[]
            {
                new SizeChoice(1024L * 1024L, "1 MB"),
                new SizeChoice(10L * 1024L * 1024L, "10 MB"),
                new SizeChoice(100L * 1024L * 1024L, "100 MB"),
                new SizeChoice(1024L * 1024L * 1024L, "1 GB")
            }, Options.DuplicateMinBytes);

            _installerAge = new NumericUpDown();
            _installerAge.Minimum = 1;
            _installerAge.Maximum = 3650;
            _installerAge.Value = Math.Max(1, Math.Min(3650, Options.InstallerMinAgeDays));
            _installerAge.Font = Theme.Body;
            _installerAge.BackColor = Theme.SurfaceAlt;
            _installerAge.ForeColor = Theme.Text;
            _installerAge.BorderStyle = BorderStyle.FixedSingle;
            _installerAge.Width = 90;

            _mediaSize = Combo("Media bigger than", new SizeChoice[]
            {
                new SizeChoice(10L * 1024L * 1024L, "10 MB"),
                new SizeChoice(25L * 1024L * 1024L, "25 MB"),
                new SizeChoice(50L * 1024L * 1024L, "50 MB"),
                new SizeChoice(100L * 1024L * 1024L, "100 MB")
            }, Options.VideoMinBytes);

            _largeSize = Combo("Large files over", new SizeChoice[]
            {
                new SizeChoice(100L * 1024L * 1024L, "100 MB"),
                new SizeChoice(300L * 1024L * 1024L, "300 MB"),
                new SizeChoice(500L * 1024L * 1024L, "500 MB"),
                new SizeChoice(1024L * 1024L * 1024L, "1 GB")
            }, Options.LargeFileMinBytes);

            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(sub, 0, 1);
            root.Controls.Add(_dev, 0, 2);
            root.Controls.Add(_caches, 0, 3);
            root.Controls.Add(_installers, 0, 4);
            root.Controls.Add(_duplicates, 0, 5);
            root.Controls.Add(_media, 0, 6);
            root.Controls.Add(_large, 0, 7);
            root.Controls.Add(SettingRow("Duplicate size floor", _duplicateSize), 0, 8);
            root.Controls.Add(SettingRow("Installers older than (days)", _installerAge), 0, 9);
            root.Controls.Add(SettingRow("Media size floor", _mediaSize), 0, 10);
            root.Controls.Add(SettingRow("Large file floor", _largeSize), 0, 11);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0, 8, 0, 0);

            PillButton ok = new PillButton();
            ok.Text = "Save";
            ok.Style = PillButton.Kind.Solid;
            ok.Width = 100;
            ok.Height = 36;
            ok.Margin = new Padding(8, 0, 0, 0);
            ok.Click += delegate { DialogResult = DialogResult.OK; };

            PillButton cancel = new PillButton();
            cancel.Text = "Cancel";
            cancel.Style = PillButton.Kind.Outline;
            cancel.Width = 100;
            cancel.Height = 36;
            cancel.Margin = new Padding(0);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            root.Controls.Add(buttons, 0, 12);

            Controls.Add(root);
            AcceptButton = null;
            CancelButton = null;

            ok.Click += delegate
            {
                ScanOptions options = new ScanOptions();
                options.ScanDevArtifacts = _dev.Checked;
                options.ScanCaches = _caches.Checked;
                options.ScanInstallers = _installers.Checked;
                options.ScanDuplicates = _duplicates.Checked;
                options.ScanMedia = _media.Checked;
                options.ScanLargeFiles = _large.Checked;
                options.DuplicateMinBytes = ((SizeChoice)_duplicateSize.SelectedItem).Bytes;

                long mediaFloor = ((SizeChoice)_mediaSize.SelectedItem).Bytes;
                options.VideoMinBytes = mediaFloor;
                options.AudioMinBytes = mediaFloor;
                options.ImageMinBytes = Math.Max(5L * 1024L * 1024L, mediaFloor / 2);
                options.LargeFileMinBytes = ((SizeChoice)_largeSize.SelectedItem).Bytes;

                options.InstallerMinAgeDays = (int)_installerAge.Value;
                options.ExtraExcludedNames.AddRange(Options.ExtraExcludedNames);
                Options = options;
            };
        }

        private static ToggleRow Toggle(string title, string detail, bool value)
        {
            ToggleRow row = new ToggleRow();
            row.Text = title;
            row.Detail = detail;
            row.Checked = value;
            row.Dock = DockStyle.Fill;
            row.Margin = new Padding(0);
            return row;
        }

        private static ComboBox Combo(string label, SizeChoice[] choices, long selected)
        {
            ComboBox box = new ComboBox();
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.FlatStyle = FlatStyle.Flat;
            box.Font = Theme.Body;
            box.BackColor = Theme.SurfaceAlt;
            box.ForeColor = Theme.Text;
            box.Width = 120;

            for (int i = 0; i < choices.Length; i++) box.Items.Add(choices[i]);

            int index = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i].Bytes == selected) { index = i; break; }
            }
            box.SelectedIndex = index;
            return box;
        }

        private static Control SettingRow(string label, Control field)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Margin = new Padding(0);

            Label text = Ui.Label(label, Theme.Small, Theme.TextMuted, false);
            text.Dock = DockStyle.Fill;
            text.TextAlign = ContentAlignment.MiddleLeft;

            field.Anchor = AnchorStyles.Right;
            field.Margin = new Padding(0, 8, 0, 0);

            row.Controls.Add(text, 0, 0);
            row.Controls.Add(field, 1, 0);
            return row;
        }

        private sealed class SizeChoice
        {
            public readonly long Bytes;
            private readonly string _text;

            public SizeChoice(long bytes, string text)
            {
                Bytes = bytes;
                _text = text;
            }

            public override string ToString()
            {
                return _text;
            }
        }
    }
}
