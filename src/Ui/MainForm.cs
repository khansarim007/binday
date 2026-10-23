using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    public sealed class MainForm : Form
    {
        private readonly List<Finding> _findings = new List<Finding>();
        private List<Finding> _visible = new List<Finding>();

        private CategoryRail _rail;
        private FindingGrid _grid;
        private Inspector _inspector;
        private EmptyState _empty;

        private Panel _pathPanel;
        private TextBox _pathBox;
        private PillButton _scanButton;
        private PillButton _browseButton;
        private PillButton _moreButton;

        private Label _titleLabel;
        private Label _blurbLabel;
        private Label _metaLabel;
        private PillButton _sortButton;
        private PillButton _directionButton;
        private readonly Dictionary<GridView, PillButton> _viewButtons = new Dictionary<GridView, PillButton>();

        private Label _tickedLabel;
        private Label _hintLabel;
        private PillButton _modeRecycle;
        private PillButton _modePermanent;
        private PillButton _cleanButton;
        private PillButton _clearButton;
        private PillButton _safeButton;

        private Label _statusLabel;
        private Label _countLabel;
        private PulseBar _pulse;

        private CancellationTokenSource _cts;
        private bool _busy;
        private bool _scanned;
        private ScanOptions _options = new ScanOptions();
        private DeleteMode _mode = DeleteMode.Recycle;
        private GridView _view = GridView.Large;
        private SortField _sort = SortField.Size;
        private bool _descending = true;

        private readonly string[] _startPaths;

        public MainForm(string[] args)
        {
            _startPaths = args ?? new string[0];

            Text = "BinDay " + Updater.CurrentVersion;
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1060, 700);
            ClientSize = new Size(1280, 830);

            try
            {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                if (area.Width > 0 && area.Height > 0)
                {
                    int width = Math.Min(1280, area.Width - 40);
                    int height = Math.Min(830, area.Height - 40);
                    if (width >= 1060 && height >= 700) ClientSize = new Size(width, height);
                }
            }
            catch (Exception) { }

            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            DoubleBuffered = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            BuildUi();
            LoadPreferences();

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile)) _pathBox.Text = profile;

            if (_pathBox != null)
            {
                _pathBox.SelectionStart = _pathBox.TextLength;
                _pathBox.SelectionLength = 0;
            }

            RefreshCategories();
            ApplyFilter(false);
            SetStatus("Ready. Choose a folder, then press Scan.");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            try { ActiveControl = _grid; } catch (Exception) { }
            
            if (_startPaths.Length > 0)
            {
                _pathBox.Text = string.Join(";", _startPaths);
                ScanClick(this, EventArgs.Empty);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SavePreferences();
            if (_cts != null) _cts.Cancel();
            base.OnFormClosing(e);
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int enabled = 1;
                DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
                DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
            }
            catch (Exception) { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // ------------------------------------------------------------------ ui

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.BackColor = Theme.Background;

            root.Controls.Add(BuildTopBar(), 0, 0);
            root.Controls.Add(BuildToolbar(), 0, 1);
            root.Controls.Add(BuildContent(), 0, 2);
            root.Controls.Add(BuildActionBar(), 0, 3);
            root.Controls.Add(BuildStatusBar(), 0, 4);

            Controls.Add(root);
        }

        private Control BuildTopBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Header;
            bar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Header))
                {
                    e.Graphics.FillRectangle(brush, bar.ClientRectangle);
                }

                using (Pen pen = new Pen(Theme.LineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
                }
            };

            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 3;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            row.Padding = new Padding(22, 0, 18, 0);

            row.Controls.Add(CreateTitleSection(), 0, 0);
            row.Controls.Add(CreateSubtitleSection(), 1, 0);
            row.Controls.Add(CreateTopButtons(), 2, 0);

            return row;
        }

        private Control CreateTitleSection()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;

            _titleLabel = new Label();
            _titleLabel.Text = "BinDay";
            _titleLabel.Font = Theme.Brand;
            _titleLabel.ForeColor = Theme.Text;
            _titleLabel.AutoSize = false;
            _titleLabel.Width = 280;

            _blurbLabel = new Label();
            _blurbLabel.Text = "Free up space on your PC.";
            _blurbLabel.Font = Theme.Huge;
            _blurbLabel.ForeColor = Theme.Text;
            _blurbLabel.AutoSize = false;
            _blurbLabel.Width = 600;

            panel.Controls.Add(_titleLabel, 0, 0);
            panel.Controls.Add(_blurbLabel, 0, 1);

            return panel;
        }

        private Control CreateSubtitleSection()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0, 10, 0, 0);

            _metaLabel = new Label();
            _metaLabel.Text = "Native Windows app &middot; 192&nbsp;KB &middot; no admin rights";
            _metaLabel.Font = Theme.Small;
            _metaLabel.ForeColor = Theme.TextMuted;
            _metaLabel.AutoSize = false;
            _metaLabel.Width = 400;

            panel.Controls.Add(_metaLabel);
            return panel;
        }

        private Control CreateTopButtons()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0, 10, 0, 0);

            _scanButton = new PillButton();
            _scanButton.Text = "Scan";
            _scanButton.Width = 120;
            _scanButton.Height = 36;
            _scanButton.Margin = new Padding(0, 0, 8, 0);

            _browseButton = new PillButton();
            _browseButton.Text = "Browse";
            _browseButton.Width = 100;
            _browseButton.Height = 36;
            _browseButton.Margin = new Padding(0, 0, 8, 0);

            _moreButton = new PillButton();
            _moreButton.Text = "...";
            _moreButton.Width = 40;
            _moreButton.Height = 36;
            _moreButton.Margin = new Padding(0, 0, 0, 0);

            panel.Controls.Add(_scanButton);
            panel.Controls.Add(_browseButton);
            panel.Controls.Add(_moreButton);

            return panel;
        }

        private Control BuildContent()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0);

            _rail = new CategoryRail();
            _rail.Dock = DockStyle.Left;
            _rail.Width = 220;

            _grid = new FindingGrid();
            _grid.Dock = DockStyle.Fill;

            _empty = new EmptyState();

            panel.Controls.Add(_rail);
            panel.Controls.Add(_grid);
            panel.Controls.Add(_empty);

            return panel;
        }

        private Control BuildActionBar()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0, 10, 0, 0);

            _modeRecycle = new PillButton();
            _modeRecycle.Text = "Recycle";
            _modeRecycle.Width = 100;
            _modeRecycle.Height = 36;
            _modeRecycle.Margin = new Padding(0, 0, 8, 0);

            _modePermanent = new PillButton();
            _modePermanent.Text = "Delete permanently";
            _modePermanent.Width = 130;
            _modePermanent.Height = 36;
            _modePermanent.Margin = new Padding(0, 0, 8, 0);

            _cleanButton = new PillButton();
            _cleanButton.Text = "Clean";
            _cleanButton.Width = 80;
            _cleanButton.Height = 36;
            _cleanButton.Margin = new Padding(0, 0, 8, 0);

            _clearButton = new PillButton();
            _clearButton.Text = "Clear";
            _clearButton.Width = 60;
            _clearButton.Height = 36;
            _clearButton.Margin = new Padding(0, 0, 8, 0);

            _safeButton = new PillButton();
            _safeButton.Text = "Safe";
            _safeButton.Width = 60;
            _safeButton.Height = 36;
            _safeButton.Margin = new Padding(0, 0, 8, 0);

            panel.Controls.Add(_modeRecycle);
            panel.Controls.Add(_modePermanent);
            panel.Controls.Add(_cleanButton);
            panel.Controls.Add(_clearButton);
            panel.Controls.Add(_safeButton);

            return panel;
        }

        private Control BuildStatusBar()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0, 10, 0, 0);

            _statusLabel = new Label();
            _statusLabel.Text = "Ready";
            _statusLabel.Font = Theme.Small;
            _statusLabel.ForeColor = Theme.TextMuted;
            _statusLabel.AutoSize = false;
            _statusLabel.Width = 200;

            _countLabel = new Label();
            _countLabel.Text = "0 items";
            _countLabel.Font = Theme.Small;
            _countLabel.ForeColor = Theme.TextMuted;
            _countLabel.AutoSize = false;
            _countLabel.Width = 100;

            _pulse = new PulseBar();
            _pulse.Width = 80;
            _pulse.Height = 6;

            panel.Controls.Add(_statusLabel);
            panel.Controls.Add(_countLabel);
            panel.Controls.Add(_pulse);

            return panel;
        }

        // ------------------------------------------------------------------ event handlers
        private void ScanClick(object sender, EventArgs e)
        {
            if (_busy) return;

            _cts = new CancellationTokenSource();
            _busy = true;
            _scanned = false;

            SetStatus("Scanning... Please wait.");

            Task.Run(() => ScanCore(_cts.Token), _cts.Token);
        }

        private void ScanCore(CancellationToken token)
        {
            try
            {
                _findings.Clear();
                _visible.Clear();

                // Simulate scanning process
                Thread.Sleep(1500);

                // Add some dummy findings for testing
                _findings.Add(new Finding("Build output", "node_modules", FindingKind.DevJunk, 150000000, 100, 1, 1, 1, "Large"));
                _findings.Add(new Finding("Package cache", "npm", FindingKind.Cache, 20000000, 50, 1, 1, 1, "Medium"));
                _findings.Add(new Finding("Old installer", "setup.exe", FindingKind.Installer, 50000000, 30, 1, 1, 1, "Medium"));
                _findings.Add(new Finding("Large video", "movie.mp4", FindingKind.Media, 500000000, 200, 1, 1, 1, "Large"));
                _findings.Add(new Finding("Duplicate file", "file1.txt", FindingKind.Duplicate, 1000000, 1, 1, 1, "Small"));

                _visible = _findings;
                _scanned = true;

                SetStatus("Scan complete. " + _findings.Count + " items found.");
            }
            catch (Exception ex)
            {
                SetStatus("Scan failed: " + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private void SetStatus(string text)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = Theme.TextMuted;
        }

        private void RefreshCategories()
        {
            _rail.Refresh();
            _grid.Refresh();
        }

        private void ApplyFilter(bool refresh)
        {
            if (!refresh) return;

            _visible.Clear();
            foreach (Finding finding in _findings)
            {
                if (ShouldShow(finding))
                {
                    _visible.Add(finding);
                }
            }

            _grid.SetDataSource(_visible);
            _countLabel.Text = $"{_visible.Count} items";
        }

        private bool ShouldShow(Finding finding)
        {
            switch (_mode)
            {
                case DeleteMode.Recycle:
                    return finding.Recovery == Recovery.Safe || finding.Recovery == Recovery.Check;
                case DeleteMode.Permanent:
                    return finding.Recovery == Recovery.Safe;
                default:
                    return true;
            }
        }
    }
}