using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    public sealed class MainForm : Form
    {
        private readonly List<Finding> _findings = new List<Finding>();
        private readonly List<Finding> _visible = new List<Finding>();
        private readonly HashSet<string> _checked = new HashSet<string>(StringComparer.Ordinal);

        private readonly Category?[] _categories = new Category?[]
        {
            null, Category.Dev, Category.Cache, Category.Temp, Category.Installer, Category.Duplicate
        };

        private TextBox _rootsBox;
        private Button _browseButton;
        private Button _scanButton;
        private Button _cancelButton;
        private Button _optionsButton;

        private Label _headerNumber;
        private Label _headerCaption;
        private ListBox _categoryList;
        private ListView _list;
        private Label _detailTitle;
        private Label _detailMeta;
        private Label _detailWhy;
        private Label _detailProject;
        private Label _emptyState;
        private Button _selectSafeButton;
        private Button _selectNoneButton;
        private Button _recycleButton;
        private Button _deleteButton;

        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _countLabel;
        private ToolStripProgressBar _stripProgress;

        private CancellationTokenSource _cts;
        private bool _binding;
        private bool _busy;
        private ScanOptions _options = new ScanOptions();
        private readonly string[] _startPaths;

        public MainForm(string[] args)
        {
            _startPaths = args ?? new string[0];

            Text = "BinDay";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 660);
            ClientSize = new Size(1120, 780);
            AutoScaleMode = AutoScaleMode.Dpi;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception) { }

            BuildUi();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildScanBar(), 0, 1);
            root.Controls.Add(BuildMiddle(), 0, 2);
            root.Controls.Add(BuildDetail(), 0, 3);
            root.Controls.Add(BuildActionBar(), 0, 4);
            root.Controls.Add(BuildStatusStrip(), 0, 5);

            Controls.Add(root);

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile)) _rootsBox.Text = profile;

            PopulateCategories();
            BindList();
            UpdateHeader();
            SetStatus("Ready. Pick a folder and press Scan.");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ListResized(this, EventArgs.Empty);

            // "BinDay.exe C:\some\folder" scans that folder straight away.
            if (_startPaths.Length > 0)
            {
                _rootsBox.Text = string.Join(";", _startPaths);
                ScanClick(this, EventArgs.Empty);
            }
        }

        // ---------------------------------------------------------------- header

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Theme.Header;

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            columns.Margin = new Padding(0);
            columns.Padding = new Padding(24, 0, 28, 0);

            TableLayoutPanel brand = new TableLayoutPanel();
            brand.Dock = DockStyle.Fill;
            brand.ColumnCount = 1;
            brand.RowCount = 2;
            brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            brand.Margin = new Padding(0);
            brand.Padding = new Padding(0, 20, 0, 0);

            Label title = Ui.Label("BinDay", Theme.Brand, Theme.HeaderText, false);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;

            Label subtitle = Ui.Label("Clear the junk, keep the good stuff.", Theme.Small, Theme.HeaderMuted, false);
            subtitle.Dock = DockStyle.Fill;
            subtitle.TextAlign = ContentAlignment.MiddleLeft;

            brand.Controls.Add(title, 0, 0);
            brand.Controls.Add(subtitle, 0, 1);

            TableLayoutPanel numbers = new TableLayoutPanel();
            numbers.Dock = DockStyle.Fill;
            numbers.ColumnCount = 1;
            numbers.RowCount = 2;
            numbers.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            numbers.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            numbers.Margin = new Padding(0);
            numbers.Padding = new Padding(0);

            _headerNumber = Ui.Label("0 B", Theme.Huge, Theme.Accent, false);
            _headerNumber.Dock = DockStyle.Fill;
            _headerNumber.TextAlign = ContentAlignment.MiddleRight;

            _headerCaption = Ui.Label("nothing to clean yet", Theme.Small, Theme.HeaderMuted, false);
            _headerCaption.Dock = DockStyle.Fill;
            _headerCaption.TextAlign = ContentAlignment.MiddleRight;

            numbers.Controls.Add(_headerNumber, 0, 0);
            numbers.Controls.Add(_headerCaption, 0, 1);

            columns.Controls.Add(brand, 0, 0);
            columns.Controls.Add(numbers, 1, 0);

            header.Controls.Add(columns);
            return header;
        }

        // -------------------------------------------------------------- scan bar

        private Control BuildScanBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Surface;
            bar.Padding = new Padding(16, 11, 16, 11);

            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
            row.Margin = new Padding(0);

            _rootsBox = new TextBox();
            _rootsBox.Dock = DockStyle.Fill;
            _rootsBox.Margin = new Padding(0, 1, 8, 1);
            Ui.ConfigureTextBox(_rootsBox);
            _rootsBox.TextChanged += delegate { if (!_busy) SetStatus("Press Scan to look for junk in these folders."); };

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);
            buttons.Padding = new Padding(0, 1, 0, 0);

            _browseButton = Ui.FlatButton("...", Theme.Surface, Theme.Text, false);
            _browseButton.MinimumSize = new Size(42, 32);
            _browseButton.Margin = new Padding(0, 0, 8, 0);
            _browseButton.Click += BrowseClick;

            _optionsButton = Ui.FlatButton("Options", Theme.Surface, Theme.Text, false);
            _optionsButton.MinimumSize = new Size(96, 32);
            _optionsButton.Margin = new Padding(0, 0, 8, 0);
            _optionsButton.Click += OptionsClick;

            _scanButton = Ui.FlatButton("Scan", Theme.AccentDark, Color.White, true);
            _scanButton.MinimumSize = new Size(96, 32);
            _scanButton.Font = Theme.Medium;
            _scanButton.Margin = new Padding(0, 0, 8, 0);
            _scanButton.Click += ScanClick;

            _cancelButton = Ui.FlatButton("Cancel", Theme.Surface, Theme.TextMuted, false);
            _cancelButton.MinimumSize = new Size(92, 32);
            _cancelButton.Margin = new Padding(0, 0, 0, 0);
            _cancelButton.Enabled = false;
            _cancelButton.Click += CancelClick;

            buttons.Controls.Add(_browseButton);
            buttons.Controls.Add(_optionsButton);
            buttons.Controls.Add(_scanButton);
            buttons.Controls.Add(_cancelButton);

            row.Controls.Add(_rootsBox, 0, 0);
            row.Controls.Add(buttons, 1, 0);

            bar.Controls.Add(row);
            return bar;
        }

        // ---------------------------------------------------------------- middle

        private Control BuildMiddle()
        {
            TableLayoutPanel middle = new TableLayoutPanel();
            middle.Dock = DockStyle.Fill;
            middle.ColumnCount = 2;
            middle.RowCount = 1;
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216));
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            middle.Margin = new Padding(0);
            middle.Padding = new Padding(0);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.BackColor = Theme.Sidebar;
            sidebar.Padding = new Padding(14, 14, 8, 14);
            sidebar.Margin = new Padding(0);

            TableLayoutPanel side = new TableLayoutPanel();
            side.Dock = DockStyle.Fill;
            side.ColumnCount = 1;
            side.RowCount = 2;
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            side.Margin = new Padding(0);

            Label heading = Ui.Label("CATEGORIES", Theme.SmallBold, Theme.TextMuted, false);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;

            _categoryList = new ListBox();
            _categoryList.Dock = DockStyle.Fill;
            _categoryList.BorderStyle = BorderStyle.None;
            _categoryList.BackColor = Theme.Sidebar;
            _categoryList.ForeColor = Theme.Text;
            _categoryList.Font = Theme.Body;
            _categoryList.IntegralHeight = false;
            _categoryList.SelectedIndexChanged += CategoryChanged;

            side.Controls.Add(heading, 0, 0);
            side.Controls.Add(_categoryList, 0, 1);
            sidebar.Controls.Add(side);

            Panel card = new Panel();
            card.Dock = DockStyle.Fill;
            card.BackColor = Theme.Surface;
            card.Margin = new Padding(0);
            card.Padding = new Padding(0);

            _list = new ListView();
            Ui.ConfigureListView(_list);
            _list.Dock = DockStyle.Fill;
            _list.Columns.Add("Item", 240);
            _list.Columns.Add("Location", 380);
            _list.Columns.Add("Size", 92);
            _list.Columns.Add("Safety", 86);
            _list.ItemCheck += ItemCheck;
            _list.SelectedIndexChanged += SelectionChanged;
            _list.Resize += ListResized;

            _emptyState = Ui.Label("Nothing scanned yet.\n\nPress Scan and BinDay will look for build output, caches, old installers and duplicate files.", Theme.Body, Theme.TextMuted, false);
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Dock = DockStyle.Fill;
            _emptyState.Visible = false;

            card.Controls.Add(_list);
            card.Controls.Add(_emptyState);
            middle.Controls.Add(sidebar, 0, 0);
            middle.Controls.Add(card, 1, 0);

            return middle;
        }

        private void ListResized(object sender, EventArgs e)
        {
            int width = _list.ClientSize.Width - 240 - 92 - 86 - 28;
            if (width < 120) width = 120;
            _list.Columns[1].Width = width;
        }

        // ---------------------------------------------------------------- detail

        private Control BuildDetail()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.Surface;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            columns.Margin = new Padding(0);
            columns.Padding = new Padding(0);

            Panel stripe = new Panel();
            stripe.Dock = DockStyle.Fill;
            stripe.BackColor = Theme.AccentDark;

            TableLayoutPanel detail = new TableLayoutPanel();
            detail.Dock = DockStyle.Fill;
            detail.ColumnCount = 1;
            detail.RowCount = 4;
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            detail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            detail.Margin = new Padding(0);
            detail.Padding = new Padding(20, 12, 20, 10);

            _detailTitle = Ui.Label("Nothing selected", Theme.Large, Theme.Text, false);
            _detailTitle.Dock = DockStyle.Fill;
            _detailTitle.TextAlign = ContentAlignment.MiddleLeft;
            _detailTitle.AutoEllipsis = true;

            _detailMeta = Ui.Label(string.Empty, Theme.Small, Theme.TextMuted, false);
            _detailMeta.Dock = DockStyle.Fill;
            _detailMeta.TextAlign = ContentAlignment.MiddleLeft;
            _detailMeta.AutoEllipsis = true;

            _detailWhy = Ui.Label("Tick an item on the left to see why it is safe to remove.", Theme.Body, Theme.Text, false);
            _detailWhy.Dock = DockStyle.Fill;
            _detailWhy.TextAlign = ContentAlignment.TopLeft;

            _detailProject = Ui.Label(string.Empty, Theme.Small, Theme.TextMuted, false);
            _detailProject.Dock = DockStyle.Fill;
            _detailProject.TextAlign = ContentAlignment.MiddleLeft;
            _detailProject.AutoEllipsis = true;

            detail.Controls.Add(_detailTitle, 0, 0);
            detail.Controls.Add(_detailMeta, 0, 1);
            detail.Controls.Add(_detailWhy, 0, 2);
            detail.Controls.Add(_detailProject, 0, 3);

            columns.Controls.Add(stripe, 0, 0);
            columns.Controls.Add(detail, 1, 0);
            panel.Controls.Add(columns);
            return panel;
        }

        // ----------------------------------------------------------- action bar

        private Control BuildActionBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Surface;
            bar.Padding = new Padding(16, 11, 16, 11);

            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 700));
            row.Margin = new Padding(0);

            Label hint = Ui.Label("Nothing is deleted until you tick it and confirm.", Theme.Small, Theme.TextMuted, false);
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);

            _recycleButton = Ui.FlatButton("Move to Recycle Bin", Theme.AccentDark, Color.White, true);
            _recycleButton.MinimumSize = new Size(178, 32);
            _recycleButton.Font = Theme.Medium;
            _recycleButton.Margin = new Padding(8, 0, 0, 0);
            _recycleButton.Click += RecycleClick;

            _deleteButton = Ui.FlatButton("Delete permanently", Theme.Surface, ColorTranslator.FromHtml("#B91C1C"), false);
            _deleteButton.MinimumSize = new Size(160, 32);
            _deleteButton.Margin = new Padding(8, 0, 0, 0);
            _deleteButton.Click += DeleteClick;

            _selectNoneButton = Ui.FlatButton("Clear ticks", Theme.Surface, Theme.Text, false);
            _selectNoneButton.MinimumSize = new Size(104, 32);
            _selectNoneButton.Margin = new Padding(8, 0, 0, 0);
            _selectNoneButton.Click += ClearTicksClick;

            _selectSafeButton = Ui.FlatButton("Tick all Safe items", Theme.Surface, Theme.Text, false);
            _selectSafeButton.MinimumSize = new Size(158, 32);
            _selectSafeButton.Margin = new Padding(8, 0, 0, 0);
            _selectSafeButton.Click += SelectSafeClick;

            buttons.Controls.Add(_recycleButton);
            buttons.Controls.Add(_deleteButton);
            buttons.Controls.Add(_selectNoneButton);
            buttons.Controls.Add(_selectSafeButton);

            row.Controls.Add(hint, 0, 0);
            row.Controls.Add(buttons, 1, 0);

            bar.Controls.Add(row);
            return bar;
        }

        private StatusStrip BuildStatusStrip()
        {
            StatusStrip strip = new StatusStrip();
            strip.BackColor = Theme.Surface;
            strip.ForeColor = Theme.TextMuted;
            strip.Font = Theme.Small;
            strip.SizingGrip = false;

            _statusLabel = new ToolStripStatusLabel();
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            _stripProgress = new ToolStripProgressBar();
            _stripProgress.Visible = false;
            _stripProgress.Width = 180;

            _countLabel = new ToolStripStatusLabel();
            _countLabel.Text = "";

            strip.Items.Add(_statusLabel);
            strip.Items.Add(_stripProgress);
            strip.Items.Add(_countLabel);

            return strip;
        }

        // ------------------------------------------------------------- scanning

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder to scan";
                if (!string.IsNullOrEmpty(_rootsBox.Text)) dialog.SelectedPath = _rootsBox.Text.Trim();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _rootsBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OptionsClick(object sender, EventArgs e)
        {
            using (OptionsForm dialog = new OptionsForm(_options))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _options = dialog.Options;
                }
            }
        }

        private void CancelClick(object sender, EventArgs e)
        {
            if (_cts != null) _cts.Cancel();
        }

        private async void ScanClick(object sender, EventArgs e)
        {
            if (_busy) return;

            List<string> roots = new List<string>();
            string[] parts = _rootsBox.Text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length > 0) roots.Add(trimmed);
            }

            if (roots.Count == 0)
            {
                MessageBox.Show(this, "Type or browse for a folder to scan first.", "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ScanOptions options = BuildOptions(roots);

            _busy = true;
            _cts = new CancellationTokenSource();
            ToggleBusy(true);

            IProgress<ScanProgress> progress = new Progress<ScanProgress>(OnScanProgress);

            ScanResult result = null;
            Exception failure = null;

            await Task.Run(delegate
            {
                try
                {
                    result = DiskScanner.Scan(options, progress, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            _busy = false;
            ToggleBusy(false);

            if (failure != null)
            {
                MessageBox.Show(this, "The scan failed:\n\n" + failure.Message, "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Scan failed.");
                return;
            }

            if (result == null)
            {
                SetStatus("Scan cancelled.");
                return;
            }

            _findings.Clear();
            _findings.AddRange(result.Findings);
            _checked.Clear();

            PopulateCategories();
            BindList();
            UpdateHeader();

            SetStatus(string.Format(CultureInfo.CurrentCulture,
                "Scanned {0} folders in {1:0.0}s. {2} items found.",
                Format.Count(result.FoldersScanned), result.Elapsed.TotalSeconds, Format.Count(result.Findings.Count)));
        }

        private ScanOptions BuildOptions(List<string> roots)
        {
            ScanOptions options = new ScanOptions();
            options.Roots.AddRange(roots);
            options.ScanDevArtifacts = _options.ScanDevArtifacts;
            options.ScanCaches = _options.ScanCaches;
            options.ScanInstallers = _options.ScanInstallers;
            options.ScanDuplicates = _options.ScanDuplicates;
            options.DuplicateMinBytes = _options.DuplicateMinBytes;
            options.InstallerMinAgeDays = _options.InstallerMinAgeDays;
            options.InstallerMinBytes = _options.InstallerMinBytes;
            options.ExtraExcludedNames.AddRange(_options.ExtraExcludedNames);

            if (options.ScanCaches)
            {
                options.CacheLocations.AddRange(RuleSet.DefaultCacheLocations());
            }

            if (options.ScanInstallers)
            {
                options.InstallerFolders.AddRange(RuleSet.DefaultInstallerFolders());
            }

            return options;
        }

        private void OnScanProgress(ScanProgress progress)
        {
            _stripProgress.Visible = true;
            _stripProgress.Style = ProgressBarStyle.Marquee;
            _stripProgress.MarqueeAnimationSpeed = 30;
            SetStatus(progress.Phase + " - " + Format.Count(progress.Folders) + " folders, " + Format.Count(progress.Findings) + " items, " + Format.Bytes(progress.Bytes) + " found");
        }

        private void ToggleBusy(bool busy)
        {
            _scanButton.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _optionsButton.Enabled = !busy;
            _cancelButton.Enabled = busy;
            _recycleButton.Enabled = !busy;
            _deleteButton.Enabled = !busy;
            _selectSafeButton.Enabled = !busy;
            _selectNoneButton.Enabled = !busy;

            if (!busy)
            {
                _stripProgress.Visible = false;
                _stripProgress.Style = ProgressBarStyle.Continuous;
            }
        }

        // -------------------------------------------------------------- listing

        private void PopulateCategories()
        {
            _categoryList.BeginUpdate();
            _categoryList.Items.Clear();

            string[] names = { "All", "Dev junk", "Caches", "Temp files", "Installers", "Duplicates" };

            for (int i = 0; i < _categories.Length; i++)
            {
                int count = 0;
                long bytes = 0;

                for (int f = 0; f < _findings.Count; f++)
                {
                    if (_categories[i].HasValue && _findings[f].Category != _categories[i].Value) continue;
                    count++;
                    bytes += _findings[f].Bytes;
                }

                string label = names[i] + "  (" + count + ")";
                if (count > 0) label += "  -  " + Format.Bytes(bytes);
                _categoryList.Items.Add(label);
            }

            _categoryList.EndUpdate();

            if (_categoryList.Items.Count > 0) _categoryList.SelectedIndex = 0;
        }

        private void CategoryChanged(object sender, EventArgs e)
        {
            BindList();
        }

        private void BindList()
        {
            int index = _categoryList.SelectedIndex;
            if (index < 0 || index >= _categories.Length) index = 0;
            Category? filter = _categories[index];

            _visible.Clear();
            for (int i = 0; i < _findings.Count; i++)
            {
                if (filter.HasValue && _findings[i].Category != filter.Value) continue;
                _visible.Add(_findings[i]);
            }

            _binding = true;
            _list.BeginUpdate();
            _list.Items.Clear();

            for (int i = 0; i < _visible.Count; i++)
            {
                Finding finding = _visible[i];
                ListViewItem item = new ListViewItem(finding.Title);
                item.SubItems.Add(LocationOf(finding));
                item.SubItems.Add(Format.Bytes(finding.Bytes));
                item.SubItems.Add(finding.SafetyLabel);
                item.Tag = finding;
                item.ForeColor = ColourFor(finding.Level);
                item.Checked = _checked.Contains(finding.Id);
                item.Font = Theme.Body;
                _list.Items.Add(item);
            }

            _list.EndUpdate();
            _binding = false;

            _countLabel.Text = _visible.Count == _findings.Count
                ? Format.Count(_findings.Count) + " items"
                : Format.Count(_visible.Count) + " of " + Format.Count(_findings.Count) + " items";

            _emptyState.Visible = _visible.Count == 0;
            _list.Visible = _visible.Count > 0;

            UpdateHeader();
            ClearDetail();
        }

        private static string LocationOf(Finding finding)
        {
            if (finding.IsDuplicateGroup)
            {
                string dir = SafeDirectory(finding.KeeperPath);
                return string.IsNullOrEmpty(dir) ? finding.KeeperPath : dir;
            }

            string parent = DiskScanner.SafeParent(finding.Path);
            return string.IsNullOrEmpty(parent) ? finding.Path : parent;
        }

        private static string SafeDirectory(string path)
        {
            try { return Path.GetDirectoryName(path); }
            catch (Exception) { return path; }
        }

        private static Color ColourFor(Safety level)
        {
            switch (level)
            {
                case Safety.Safe: return Theme.Text;
                case Safety.Caution: return Theme.Caution;
                default: return Theme.Review;
            }
        }

        private void ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_binding) return;

            ListViewItem item = _list.Items[e.Index];
            Finding finding = item.Tag as Finding;
            if (finding == null) return;

            if (e.NewValue == CheckState.Checked) _checked.Add(finding.Id);
            else _checked.Remove(finding.Id);
        }

        private void SelectionChanged(object sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0)
            {
                ClearDetail();
                return;
            }

            Finding finding = _list.SelectedItems[0].Tag as Finding;
            if (finding == null)
            {
                ClearDetail();
                return;
            }

            _detailTitle.Text = finding.Title;
            _detailTitle.ForeColor = ColourFor(finding.Level);

            string meta = Format.Bytes(finding.Bytes) + "  -  " + Format.Count(finding.FileCount) + " file" + (finding.FileCount == 1 ? "" : "s") + "  -  " + finding.SafetyLabel;
            if (finding.IsDuplicateGroup)
            {
                meta += "  -  " + finding.DeletePaths.Count + " cop" + (finding.DeletePaths.Count == 1 ? "y" : "ies") + " to remove";
            }
            _detailMeta.Text = meta;

            _detailWhy.Text = finding.Why;

            if (finding.IsDuplicateGroup)
            {
                _detailProject.Text = "Keeping: " + finding.KeeperPath;
            }
            else if (!string.IsNullOrEmpty(finding.MarkerPath))
            {
                _detailProject.Text = "Project file: " + finding.MarkerPath;
            }
            else
            {
                _detailProject.Text = finding.Path;
            }
        }

        private void ClearDetail()
        {
            _detailTitle.Text = "Nothing selected";
            _detailTitle.ForeColor = Theme.Text;
            _detailMeta.Text = string.Empty;
            _detailWhy.Text = "Tick an item on the left to see why it is safe to remove.";
            _detailProject.Text = string.Empty;
        }

        private void UpdateHeader()
        {
            long total = 0;
            int count = 0;
            for (int i = 0; i < _findings.Count; i++)
            {
                total += _findings[i].Bytes;
                count++;
            }

            _headerNumber.Text = Format.Bytes(total);
            _headerCaption.Text = count == 0
                ? "nothing to clean yet"
                : Format.Count(count) + " item" + (count == 1 ? "" : "s") + " can be freed";
        }

        // ------------------------------------------------------------- cleaning

        private List<Finding> SelectedFindings()
        {
            List<Finding> selected = new List<Finding>();
            for (int i = 0; i < _findings.Count; i++)
            {
                if (_checked.Contains(_findings[i].Id)) selected.Add(_findings[i]);
            }
            return selected;
        }

        private void SelectSafeClick(object sender, EventArgs e)
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Level == Safety.Safe) _checked.Add(_visible[i].Id);
            }
            BindList();
            SetStatus("Ticked every item marked Safe in the current category.");
        }

        private void ClearTicksClick(object sender, EventArgs e)
        {
            _checked.Clear();
            BindList();
        }

        private void RecycleClick(object sender, EventArgs e)
        {
            List<Finding> selected = SelectedFindings();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Tick the items you want to remove first.", "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long bytes = 0;
            for (int i = 0; i < selected.Count; i++) bytes += selected[i].Bytes;

            DialogResult answer = MessageBox.Show(this,
                "Move " + selected.Count + " item" + (selected.Count == 1 ? "" : "s") + " (" + Format.Bytes(bytes) + ") to the Recycle Bin?\n\nYou can put them back from the Recycle Bin afterwards.",
                "BinDay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;

            RunCleanup(selected, DeleteMode.Recycle);
        }

        private void DeleteClick(object sender, EventArgs e)
        {
            List<Finding> selected = SelectedFindings();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Tick the items you want to remove first.", "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int notSafe = 0;
            long bytes = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].Level != Safety.Safe) notSafe++;
                bytes += selected[i].Bytes;
            }

            string message = "Permanently delete " + selected.Count + " item" + (selected.Count == 1 ? "" : "s") + " (" + Format.Bytes(bytes) + ")?\n\nThis cannot be undone.";
            if (notSafe > 0)
            {
                message += "\n\n" + notSafe + " item" + (notSafe == 1 ? " is" : "s are") + " not marked Safe and will be skipped. Use the Recycle Bin for those.";
            }

            DialogResult answer = MessageBox.Show(this, message, "BinDay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            RunCleanup(selected, DeleteMode.Permanent);
        }

        private async void RunCleanup(List<Finding> selected, DeleteMode mode)
        {
            _busy = true;
            ToggleBusy(true);

            CancellationTokenSource cleanup = new CancellationTokenSource();
            IProgress<CleanupProgress> progress = new Progress<CleanupProgress>(delegate (CleanupProgress p)
            {
                SetStatus("Removing " + p.Current);
            });

            CleanupReport report = null;
            Exception failure = null;

            await Task.Run(delegate
            {
                try
                {
                    report = Cleaner.Run(selected, mode, new WindowsRecycler(), progress, cleanup.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            cleanup.Dispose();
            _busy = false;
            ToggleBusy(false);

            if (failure != null)
            {
                MessageBox.Show(this, "The cleanup failed:\n\n" + failure.Message, "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (report == null)
            {
                SetStatus("Cleanup cancelled.");
                return;
            }

            History.Append(report, mode, selected, null);

            RemoveCleanedFindings(selected);
            PopulateCategories();
            BindList();
            UpdateHeader();

            string summary = "Freed " + Format.Bytes(report.BytesFreed) + ".";
            if (report.Failed > 0) summary += " " + report.Failed + " item(s) could not be removed.";
            if (report.Refused > 0) summary += " " + report.Refused + " item(s) were skipped for safety.";
            SetStatus(summary);

            MessageBox.Show(this, summary, "BinDay", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RemoveCleanedFindings(List<Finding> selected)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                Finding finding = selected[i];
                bool stillThere = false;

                for (int p = 0; p < finding.DeletePaths.Count; p++)
                {
                    if (Directory.Exists(finding.DeletePaths[p]) || File.Exists(finding.DeletePaths[p]))
                    {
                        stillThere = true;
                        break;
                    }
                }

                if (!stillThere)
                {
                    _findings.Remove(finding);
                    _checked.Remove(finding.Id);
                }
            }
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.Text = text;
        }
    }
}
