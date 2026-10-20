using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>A small dialog with the knobs behind a scan.</summary>
    internal sealed class OptionsForm : Form
    {
        private readonly CheckBox _dev;
        private readonly CheckBox _caches;
        private readonly CheckBox _installers;
        private readonly CheckBox _duplicates;
        private readonly ComboBox _duplicateSize;
        private readonly NumericUpDown _installerAge;

        public ScanOptions Options { get; private set; }

        public OptionsForm(ScanOptions current)
        {
            Text = "BinDay options";
            Font = Theme.Body;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 330);

            Options = current ?? new ScanOptions();

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 8;
            root.Padding = new Padding(20, 18, 20, 14);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label heading = Ui.Label("What should a scan look for?", Theme.Medium, Theme.Text, false);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;

            _dev = Check("Build output and dependency folders", Options.ScanDevArtifacts);
            _caches = Check("Package caches and temporary files", Options.ScanCaches);
            _installers = Check("Old installers in Downloads and Desktop", Options.ScanInstallers);
            _duplicates = Check("Duplicate files", Options.ScanDuplicates);

            TableLayoutPanel sizes = new TableLayoutPanel();
            sizes.Dock = DockStyle.Fill;
            sizes.ColumnCount = 2;
            sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            sizes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label sizeLabel = Ui.Label("Duplicates bigger than", Theme.Body, Theme.Text, false);
            sizeLabel.Dock = DockStyle.Fill;
            sizeLabel.TextAlign = ContentAlignment.MiddleLeft;

            _duplicateSize = new ComboBox();
            _duplicateSize.DropDownStyle = ComboBoxStyle.DropDownList;
            _duplicateSize.Dock = DockStyle.Fill;
            _duplicateSize.Font = Theme.Body;
            _duplicateSize.Items.Add(new SizeChoice(1024L * 1024L, "1 MB"));
            _duplicateSize.Items.Add(new SizeChoice(10L * 1024L * 1024L, "10 MB"));
            _duplicateSize.Items.Add(new SizeChoice(100L * 1024L * 1024L, "100 MB"));
            _duplicateSize.Items.Add(new SizeChoice(1024L * 1024L * 1024L, "1 GB"));
            _duplicateSize.SelectedIndex = PickSize(Options.DuplicateMinBytes);

            sizes.Controls.Add(sizeLabel, 0, 0);
            sizes.Controls.Add(_duplicateSize, 1, 0);

            TableLayoutPanel ages = new TableLayoutPanel();
            ages.Dock = DockStyle.Fill;
            ages.ColumnCount = 2;
            ages.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            ages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label ageLabel = Ui.Label("Installers older than (days)", Theme.Body, Theme.Text, false);
            ageLabel.Dock = DockStyle.Fill;
            ageLabel.TextAlign = ContentAlignment.MiddleLeft;

            _installerAge = new NumericUpDown();
            _installerAge.Minimum = 1;
            _installerAge.Maximum = 3650;
            _installerAge.Dock = DockStyle.Fill;
            _installerAge.Font = Theme.Body;
            _installerAge.Value = Math.Max(1, Math.Min(3650, Options.InstallerMinAgeDays));

            ages.Controls.Add(ageLabel, 0, 0);
            ages.Controls.Add(_installerAge, 1, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);

            Button cancel = Ui.FlatButton("Cancel", Theme.Surface, Theme.Text, false);
            cancel.Width = 96;
            cancel.Margin = new Padding(8, 0, 0, 0);
            cancel.DialogResult = DialogResult.Cancel;

            Button ok = Ui.FlatButton("Save", Theme.AccentDark, Color.White, true);
            ok.Width = 96;
            ok.Margin = new Padding(8, 0, 0, 0);
            ok.DialogResult = DialogResult.OK;

            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(_dev, 0, 1);
            root.Controls.Add(_caches, 0, 2);
            root.Controls.Add(_installers, 0, 3);
            root.Controls.Add(_duplicates, 0, 4);
            root.Controls.Add(sizes, 0, 5);
            root.Controls.Add(ages, 0, 6);
            root.Controls.Add(buttons, 0, 7);

            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;

            ok.Click += delegate (object sender, EventArgs e)
            {
                ScanOptions options = new ScanOptions();
                options.ScanDevArtifacts = _dev.Checked;
                options.ScanCaches = _caches.Checked;
                options.ScanInstallers = _installers.Checked;
                options.ScanDuplicates = _duplicates.Checked;
                options.DuplicateMinBytes = ((SizeChoice)_duplicateSize.SelectedItem).Bytes;
                options.InstallerMinAgeDays = (int)_installerAge.Value;
                options.ExtraExcludedNames.AddRange(Options.ExtraExcludedNames);
                Options = options;
            };
        }

        private static CheckBox Check(string text, bool value)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Checked = value;
            box.AutoSize = false;
            box.Dock = DockStyle.Fill;
            box.Font = Theme.Body;
            box.ForeColor = Theme.Text;
            box.TextAlign = ContentAlignment.MiddleLeft;
            return box;
        }

        private int PickSize(long bytes)
        {
            for (int i = 0; i < _duplicateSize.Items.Count; i++)
            {
                SizeChoice choice = (SizeChoice)_duplicateSize.Items[i];
                if (choice.Bytes == bytes) return i;
            }
            return 1;
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
