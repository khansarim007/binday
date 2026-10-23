using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            row.Padding = new Padding(22, 0, 18, 0);
            row.Margin = new Padding(0);

            row.Controls.Add(BuildBrand(), 0, 0);
            row.Controls.Add(BuildPathField(), 1, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 20, 0, 0);
            buttons.Margin = new Padding(0);

            _moreButton = new PillButton();
            _moreButton.Text = "...";
            _moreButton.Style = PillButton.Kind.Ghost;
            _moreButton.Width = 40;
            _moreButton.Height = 36;
            _moreButton.Margin = new Padding(6, 0, 0, 0);
            _moreButton.Click += MoreClick;

            _scanButton = new PillButton();
            _scanButton.Text = "Scan";
            _scanButton.Style = PillButton.Kind.Solid;
            _scanButton.Font = Theme.Medium;
            _scanButton.Width = 96;
            _scanButton.Height = 36;
            _scanButton.Margin = new Padding(6, 0, 0, 0);
            _scanButton.Click += ScanClick;

            _browseButton = new PillButton();
            _browseButton.Text = "Browse";
            _browseButton.Style = PillButton.Kind.Outline;
            _browseButton.Width = 94;
            _browseButton.Height = 36;
            _browseButton.Margin = new Padding(0, 0, 0, 0);
            _browseButton.Click += BrowseClick;

            buttons.Controls.Add(_moreButton);
            buttons.Controls.Add(_scanButton);
            buttons.Controls.Add(_browseButton);

            row.Controls.Add(buttons, 2, 0);
            bar.Controls.Add(row);
            return bar;
        }

        private Control BuildBrand()
        {
            TableLayoutPanel brand = new TableLayoutPanel();
            brand.Dock = DockStyle.Fill;
            brand.ColumnCount = 2;
            brand.RowCount = 1;
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            brand.Margin = new Padding(0);
            brand.Padding = new Padding(0, 18, 0, 12);

            Panel mark = new Panel();
            mark.Dock = DockStyle.Fill;
            mark.Margin = new Padding(0, 0, 8, 8);
            mark.Paint += delegate (object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle tile = new Rectangle(0, 0, Math.Min(mark.Width - 2, 34), Math.Min(mark.Height - 2, 34));
                Ink.FillRounded(g, tile, 10, Theme.AccentSoft);
                Ink.OutlineRounded(g, tile, 10, Theme.AccentDark, 1f);

                using (Pen pen = new Pen(Theme.Accent, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    g.DrawLine(pen, tile.Left + 10, tile.Top + 10, tile.Right - 10, tile.Top + 10);
                    g.DrawLine(pen, tile.Left + 14, tile.Top + 7, tile.Right - 14, tile.Top + 7);

                    g.DrawLines(pen, new[]
                    {
                        new PointF(tile.Left + 11, tile.Top + 14),
                        new PointF(tile.Left + 13, tile.Bottom - 8),
                        new PointF(tile.Right - 13, tile.Bottom - 8),
                        new PointF(tile.Right - 11, tile.Top + 14)
                    });

                    g.DrawLine(pen, tile.Left + 16, tile.Top + 18, tile.Left + 17, tile.Bottom - 12);
                    g.DrawLine(pen, tile.Right - 16, tile.Top + 18, tile.Right - 17, tile.Bottom - 12);
                }
            };

            TableLayoutPanel words = new TableLayoutPanel();
            words.Dock = DockStyle.Fill;
            words.ColumnCount = 1;
            words.RowCount = 2;
            words.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            words.RowStyles.Add(new RowStyle(SizeType.Absolute, 17));
            words.Margin = new Padding(0, 1, 0, 0);

            Label name = Ui.Label("BinDay", Theme.Brand, Theme.Text, false);
            name.Dock = DockStyle.Fill;
            name.TextAlign = ContentAlignment.BottomLeft;

            Label tagline = Ui.Label("Clear the junk, keep the good stuff.", Theme.Tiny, Theme.TextDim, false);
            tagline.Dock = DockStyle.Fill;
            tagline.TextAlign = ContentAlignment.TopLeft;

            words.Controls.Add(name, 0, 0);
            words.Controls.Add(tagline, 0, 1);

            brand.Controls.Add(mark, 0, 0);
            brand.Controls.Add(words, 1, 0);
            return brand;
        }

        private Control BuildPathField()
        {
            _pathPanel = new Panel();
            _pathPanel.Dock = DockStyle.Fill;
            _pathPanel.BackColor = Theme.SurfaceAlt;
            _pathPanel.Margin = new Padding(0, 20, 0, 20);
            _pathPanel.Paint += delegate (object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (SolidBrush background = new SolidBrush(Theme.SurfaceAlt))
                {
                    g.FillRectangle(background, ClientRectangle);
                }

                Ink.OutlineRounded(g, new Rectangle(0, 0, _pathPanel.Width - 1, _pathPanel.Height - 1), 9, Theme.Line, 1f);
                Glyphs.Draw(g, ItemKind.Folder, new Rectangle(12, 10, 16, 16), Theme.TextMuted);



                if (_pathBox != null && _pathBox.TextLength == 0 && !_pathBox.Focused)
                {
                    Ink.Text(g, "Type a folder to scan, or press Browse", Theme.Small, Theme.TextDim,
                        new Rectangle(38, 0, _pathPanel.Width - 48, _pathPanel.Height),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            };

            _pathPanel.Padding = new Padding(38, 11, 12, 9);

            _pathBox = new TextBox();
            _pathBox.Dock = DockStyle.Fill;
            _pathBox.BorderStyle = BorderStyle.None;
            _pathBox.BackColor = Theme.SurfaceAlt;
            _pathBox.ForeColor = Theme.Text;
            _pathBox.Font = Theme.Body;
            _pathBox.TextChanged += delegate
            {
                if (!_busy) SetStatus("Press Scan to look inside these folders.");
                _pathPanel.Invalidate();
            };
            _pathBox.GotFocus += delegate
            {
                _pathPanel.Invalidate();
                if (_pathBox.SelectionLength > 0) _pathBox.SelectionLength = 0;
            };
            _pathBox.LostFocus += delegate { _pathPanel.Invalidate(); };
            _pathBox.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ScanClick(this, EventArgs.Empty);
                }
            };

            _pathPanel.Controls.Add(_pathBox);
            _pathPanel.Resize += delegate
            {
                Control bar = _pathPanel.Parent != null && _pathPanel.Parent.Parent != null
                    ? _pathPanel.Parent.Parent
                    : null;
                if (bar != null) bar.Invalidate();
                _pathPanel.Invalidate();

                using (GraphicsPath path = Ink.Rounded(new Rectangle(0, 0, _pathPanel.Width, _pathPanel.Height), 9))
                {
                    Region old = _pathPanel.Region;
                    _pathPanel.Region = new Region(path);
                    if (old != null) old.Dispose();
                }
            };

            return _pathPanel;
        }

        private Control BuildToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Background;
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
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 560));
            row.Padding = new Padding(24, 6, 16, 6);
            row.Margin = new Padding(0);

            TableLayoutPanel titles = new TableLayoutPanel();
            titles.Dock = DockStyle.Fill;
            titles.ColumnCount = 1;
            titles.RowCount = 2;
            titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            titles.Margin = new Padding(0);

            _titleLabel = Ui.Label("All items", Theme.Large, Theme.Text, false);
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.TextAlign = ContentAlignment.BottomLeft;
            _titleLabel.AutoEllipsis = true;

            _blurbLabel = Ui.Label("Everything the scan found, in one place.", Theme.Tiny, Theme.TextDim, false);
            _blurbLabel.Dock = DockStyle.Fill;
            _blurbLabel.TextAlign = ContentAlignment.TopLeft;
            _blurbLabel.AutoEllipsis = true;

            titles.Controls.Add(_titleLabel, 0, 0);
            titles.Controls.Add(_blurbLabel, 0, 1);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.FlowDirection = FlowDirection.RightToLeft;
            tools.WrapContents = false;
            tools.Margin = new Padding(0);
            tools.Padding = new Padding(0, 4, 0, 0);

            GridView[] views = { GridView.Large, GridView.Medium, GridView.List, GridView.Details };
            string[] labels = { "Large", "Medium", "List", "Details" };
            int[] widths = { 58, 66, 52, 62 };

            for (int i = views.Length - 1; i >= 0; i--)
            {
                GridView view = views[i];
                PillButton button = new PillButton();
                button.Text = labels[i];
                button.Style = PillButton.Kind.Outline;
                button.Width = widths[i];
                button.Height = 30;
                button.Margin = new Padding(4, 0, 0, 0);
                button.Toggled = view == _view;
                button.Click += delegate { SetView(view); };
                _viewButtons[view] = button;
                tools.Controls.Add(button);
            }

            Panel separator = new Panel();
            separator.Width = 1;
            separator.Height = 20;
            separator.BackColor = Theme.Line;
            separator.Margin = new Padding(12, 5, 8, 5);
            tools.Controls.Add(separator);

            _directionButton = new PillButton();
            _directionButton.Text = "↓";
            _directionButton.Style = PillButton.Kind.Ghost;
            _directionButton.Width = 34;
            _directionButton.Height = 30;
            _directionButton.Margin = new Padding(4, 0, 0, 0);
            _directionButton.Click += delegate
            {
                _descending = !_descending;
                ApplySort();
            };
            tools.Controls.Add(_directionButton);

            _sortButton = new PillButton();
            _sortButton.Text = "Sort: Size";
            _sortButton.Style = PillButton.Kind.Outline;
            _sortButton.Width = 124;
            _sortButton.Height = 30;
            _sortButton.Margin = new Padding(0, 0, 0, 0);
            _sortButton.Click += SortClick;
            tools.Controls.Add(_sortButton);

            _metaLabel = Ui.Label(string.Empty, Theme.Small, Theme.TextMuted, false);
            _metaLabel.AutoSize = true;
            _metaLabel.TextAlign = ContentAlignment.MiddleRight;
            _metaLabel.Margin = new Padding(0, 3, 12, 0);
            tools.Controls.Add(_metaLabel);

            row.Controls.Add(titles, 0, 0);
            row.Controls.Add(tools, 1, 0);

            bar.Controls.Add(row);
            return bar;
        }

        private Control BuildContent()
        {
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 3;
            content.RowCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 342));
            content.Margin = new Padding(0);
            content.BackColor = Theme.Background;

            _rail = new CategoryRail();
            _rail.Dock = DockStyle.Fill;
            _rail.Margin = new Padding(0);
            _rail.SelectionChanged += delegate { ApplyFilter(true); };

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Background;
            host.Margin = new Padding(0);

            _grid = new FindingGrid();
            _grid.Dock = DockStyle.Fill;
            _grid.Margin = new Padding(0);
            _grid.SelectionChanged += delegate { UpdateInspector(); };
            _grid.CheckChanged += delegate { UpdateActionBar(); UpdateInspector(); };
            _grid.Activated += delegate { OpenPrimary(); };
            _grid.SortRequested += delegate (object sender, SortField field) { SortBy(field); };
            _grid.ContextMenuStrip = BuildItemMenu();

            _empty = new EmptyState();
            _empty.Dock = DockStyle.Fill;
            _empty.Visible = false;

            host.Controls.Add(_grid);
            host.Controls.Add(_empty);

            _inspector = new Inspector();
            _inspector.Dock = DockStyle.Fill;
            _inspector.Margin = new Padding(0);
            _inspector.TickRequested += delegate { TogglePrimary(); };
            _inspector.OpenRequested += delegate { OpenPrimary(); };

            content.Controls.Add(_rail, 0, 0);
            content.Controls.Add(host, 1, 0);
            content.Controls.Add(_inspector, 2, 0);
            return content;
        }

        private ContextMenuStrip BuildItemMenu()
        {
            ContextMenuStrip menu = Ui.DarkMenu();
            menu.Items.Add("Open location", null, delegate { OpenPrimary(); });
            menu.Items.Add("Tick for cleanup", null, delegate { TogglePrimary(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Copy path", null, delegate { CopyPrimaryPath(); });
            return menu;
        }

        private Control BuildActionBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Surface;
            bar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Surface))
                {
                    e.Graphics.FillRectangle(brush, bar.ClientRectangle);
                }

                using (Pen pen = new Pen(Theme.Line, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
                }
            };

            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 3;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Padding = new Padding(24, 12, 18, 12);
            row.Margin = new Padding(0);

            TableLayoutPanel summary = new TableLayoutPanel();
            summary.Dock = DockStyle.Fill;
            summary.ColumnCount = 1;
            summary.RowCount = 2;
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            summary.Margin = new Padding(0);

            _tickedLabel = Ui.Label("Nothing ticked yet", Theme.Medium, Theme.Text, false);
            _tickedLabel.Dock = DockStyle.Fill;
            _tickedLabel.TextAlign = ContentAlignment.BottomLeft;

            _hintLabel = Ui.Label("Tick items to build a cleanup list. Nothing is removed until you confirm.",
                Theme.Tiny, Theme.TextDim, false);
            _hintLabel.Dock = DockStyle.Fill;
            _hintLabel.TextAlign = ContentAlignment.TopLeft;
            _hintLabel.AutoEllipsis = true;

            summary.Controls.Add(_tickedLabel, 0, 0);
            summary.Controls.Add(_hintLabel, 0, 1);

            FlowLayoutPanel modes = new FlowLayoutPanel();
            modes.Dock = DockStyle.Fill;
            modes.FlowDirection = FlowDirection.LeftToRight;
            modes.WrapContents = false;
            modes.Margin = new Padding(8, 8, 8, 0);

            _modeRecycle = new PillButton();
            _modeRecycle.Text = "Recycle Bin";
            _modeRecycle.Style = PillButton.Kind.Outline;
            _modeRecycle.Width = 122;
            _modeRecycle.Height = 36;
            _modeRecycle.Toggled = true;
            _modeRecycle.Margin = new Padding(0, 0, 4, 0);
            _modeRecycle.Click += delegate { SetMode(DeleteMode.Recycle); };

            _modePermanent = new PillButton();
            _modePermanent.Text = "Delete permanently";
            _modePermanent.Style = PillButton.Kind.Outline;
            _modePermanent.Width = 152;
            _modePermanent.Height = 36;
            _modePermanent.Margin = new Padding(0);
            _modePermanent.Click += delegate { SetMode(DeleteMode.Permanent); };

            modes.Controls.Add(_modeRecycle);
            modes.Controls.Add(_modePermanent);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Margin = new Padding(0);
            actions.Padding = new Padding(0, 6, 0, 0);

            _cleanButton = new PillButton();
            _cleanButton.Text = "Clean";
            _cleanButton.Style = PillButton.Kind.Solid;
            _cleanButton.Font = Theme.Medium;
            _cleanButton.Width = 196;
            _cleanButton.Height = 40;
            _cleanButton.Margin = new Padding(8, 0, 0, 0);
            _cleanButton.Enabled = false;
            _cleanButton.Click += CleanClick;

            _clearButton = new PillButton();
            _clearButton.Text = "Clear";
            _clearButton.Style = PillButton.Kind.Ghost;
            _clearButton.Width = 72;
            _clearButton.Height = 40;
            _clearButton.Margin = new Padding(4, 0, 0, 0);
            _clearButton.Enabled = false;
            _clearButton.Click += delegate
            {
                _grid.ClearChecks();
                SetStatus("Ticks cleared.");
            };

            _safeButton = new PillButton();
            _safeButton.Text = "Tick all Safe";
            _safeButton.Style = PillButton.Kind.Ghost;
            _safeButton.Width = 112;
            _safeButton.Height = 40;
            _safeButton.Margin = new Padding(4, 0, 0, 0);
            _safeButton.Click += TickSafeClick;

            actions.Controls.Add(_cleanButton);
            actions.Controls.Add(_clearButton);
            actions.Controls.Add(_safeButton);

            row.Controls.Add(summary, 0, 0);
            row.Controls.Add(modes, 1, 0);
            row.Controls.Add(actions, 2, 0);

            bar.Controls.Add(row);
            return bar;
        }

        private Control BuildStatusBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Surface;
            bar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Surface))
                {
                    e.Graphics.FillRectangle(brush, bar.ClientRectangle);
                }

                using (Pen pen = new Pen(Theme.LineSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
                }
            };

            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 3;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            row.Padding = new Padding(24, 0, 18, 0);
            row.Margin = new Padding(0);

            _statusLabel = Ui.Label(string.Empty, Theme.Tiny, Theme.TextDim, false);
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.AutoEllipsis = true;

            _pulse = new PulseBar();
            _pulse.Dock = DockStyle.Fill;
            _pulse.Margin = new Padding(0, 10, 0, 10);
            _pulse.Visible = false;

            _countLabel = Ui.Label(string.Empty, Theme.Tiny, Theme.TextDim, false);
            _countLabel.Dock = DockStyle.Fill;
            _countLabel.TextAlign = ContentAlignment.MiddleRight;

            row.Controls.Add(_statusLabel, 0, 0);
            row.Controls.Add(_pulse, 1, 0);
            row.Controls.Add(_countLabel, 2, 0);

            bar.Controls.Add(row);
            return bar;
        }

        // ------------------------------------------------------------- actions

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder for BinDay to scan";
                dialog.ShowNewFolderButton = false;

                string current = FirstRoot();
                if (!string.IsNullOrEmpty(current) && Directory.Exists(current)) dialog.SelectedPath = current;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _pathBox.Text = dialog.SelectedPath;
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
                    SetStatus("Scan settings saved. Press Scan to use them.");
                }
            }
        }

        private void HistoryClick(object sender, EventArgs e)
        {
            using (HistoryForm dialog = new HistoryForm())
            {
                dialog.ShowDialog(this);
            }
        }

        private void AboutClick(object sender, EventArgs e)
        {
            string message =
                "BinDay " + Application.ProductVersion + "\n\n" +
                "A native Windows disk cleaner that shows you what it found, explains what would happen, " +
                "and never removes anything you have not ticked and confirmed.\n\n" +
                "Website: https://getbinday.vercel.app\n" +
                "Source: https://github.com/khansarim007/binday\n" +
                "Licence: MIT";

            MessageBox.Show(this, message, "About BinDay", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MoreClick(object sender, EventArgs e)
        {
            ContextMenuStrip menu = Ui.DarkMenu();
            menu.Items.Add("Scan options...", null, OptionsClick);
            menu.Items.Add("Cleanup history", null, HistoryClick);
            menu.Items.Add("Create desktop shortcut", null, ShortcutClick);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About BinDay", null, AboutClick);
            menu.Show(_moreButton, new Point(0, _moreButton.Height + 4));
        }

        private void SortClick(object sender, EventArgs e)
        {
            ContextMenuStrip menu = Ui.DarkMenu();

            AddSortItem(menu, "Size", SortField.Size);
            AddSortItem(menu, "Name", SortField.Name);
            AddSortItem(menu, "Date changed", SortField.Changed);
            AddSortItem(menu, "Safety", SortField.Safety);
            AddSortItem(menu, "Collection", SortField.Category);
            AddSortItem(menu, "File count", SortField.Files);

            menu.Show(_sortButton, new Point(0, _sortButton.Height + 4));
        }

        private void AddSortItem(ContextMenuStrip menu, string text, SortField field)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = _sort == field;
            item.Click += delegate { SortBy(field); };
            menu.Items.Add(item);
        }

        private void SortBy(SortField field)
        {
            if (_sort == field)
            {
                _descending = !_descending;
            }
            else
            {
                _sort = field;
                _descending = field == SortField.Size || field == SortField.Changed || field == SortField.Files;
            }

            ApplySort();
        }

        private void ApplySort()
        {
            Comparison<Finding> comparison = ComparisonFor(_sort);

            _visible.Sort(delegate (Finding a, Finding b)
            {
                int result = comparison(a, b);
                return _descending ? -result : result;
            });

            _grid.Sort = _sort;
            _grid.SortDescending = _descending;
            _grid.SetItems(_visible, true);

            _sortButton.Text = "Sort: " + SortName(_sort);
            _directionButton.Text = _descending ? "↓" : "↑";

            UpdateToolbar();
            UpdateInspector();
        }

        private static string SortName(SortField field)
        {
            switch (field)
            {
                case SortField.Size: return "Size";
                case SortField.Name: return "Name";
                case SortField.Changed: return "Date";
                case SortField.Safety: return "Safety";
                case SortField.Category: return "Collection";
                default: return "Files";
            }
        }

        private static Comparison<Finding> ComparisonFor(SortField field)
        {
            switch (field)
            {
                case SortField.Size:
                    return delegate (Finding a, Finding b) { return a.Bytes.CompareTo(b.Bytes); };

                case SortField.Name:
                    return delegate (Finding a, Finding b)
                    {
                        return string.Compare(a.Title ?? string.Empty, b.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    };

                case SortField.Changed:
                    return delegate (Finding a, Finding b) { return a.Modified.CompareTo(b.Modified); };

                case SortField.Safety:
                    return delegate (Finding a, Finding b) { return a.Level.CompareTo(b.Level); };

                case SortField.Category:
                    return delegate (Finding a, Finding b)
                    {
                        return string.Compare(a.CategoryLabel, b.CategoryLabel, StringComparison.OrdinalIgnoreCase);
                    };

                default:
                    return delegate (Finding a, Finding b) { return a.FileCount.CompareTo(b.FileCount); };
            }
        }

        private void SetView(GridView view)
        {
            _view = view;
            _grid.ViewMode = view;

            foreach (KeyValuePair<GridView, PillButton> pair in _viewButtons)
            {
                pair.Value.Toggled = pair.Key == view;
            }

            SavePreferences();
        }

        private void SetMode(DeleteMode mode)
        {
            _mode = mode;
            _modeRecycle.Toggled = mode == DeleteMode.Recycle;
            _modePermanent.Toggled = mode == DeleteMode.Permanent;
            _cleanButton.Style = mode == DeleteMode.Permanent ? PillButton.Kind.Danger : PillButton.Kind.Solid;

            _inspector.SetMode(mode);
            UpdateActionBar();
        }

        private void OpenPrimary()
        {
            Finding finding = _grid.Primary;
            if (finding == null) return;

            string path = finding.IsDuplicateGroup ? finding.KeeperPath : finding.Path;
            Ui.OpenLocation(path, !finding.IsFolder);
        }

        private void CopyPrimaryPath()
        {
            Finding finding = _grid.Primary;
            if (finding == null) return;

            try
            {
                Clipboard.SetText(finding.IsDuplicateGroup ? finding.KeeperPath : finding.Path);
                SetStatus("Path copied to the clipboard.");
            }
            catch (Exception) { }
        }

        private void TogglePrimary()
        {
            Finding finding = _grid.Primary;
            if (finding == null) return;

            _grid.ToggleChecked(finding);
            UpdateInspector();
        }

        // ------------------------------------------------------------- scanning

        private string FirstRoot()
        {
            if (_pathBox == null) return null;

            string[] parts = _pathBox.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim().Trim('"');
                if (trimmed.Length > 0) return trimmed;
            }
            return null;
        }

        private async void ScanClick(object sender, EventArgs e)
        {
            if (_busy) return;

            List<string> roots = new List<string>();
            string[] parts = _pathBox.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim().Trim('"');
                if (trimmed.Length > 0) roots.Add(trimmed);
            }

            if (roots.Count == 0)
            {
                SetStatus("Choose a folder first, then press Scan.");
                _pathBox.Focus();
                return;
            }

            for (int i = 0; i < roots.Count; i++)
            {
                if (!Directory.Exists(roots[i]))
                {
                    SetStatus("That folder does not exist: " + roots[i]);
                    return;
                }
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
                catch (OperationCanceledException) { }
                catch (Exception ex) { failure = ex; }
            });

            _busy = false;
            ToggleBusy(false);

            if (failure != null)
            {
                SetStatus("The scan failed: " + failure.Message);
                return;
            }

            if (result == null)
            {
                SetStatus("Scan cancelled.");
                return;
            }

            _findings.Clear();
            _findings.AddRange(result.Findings);
            _grid.Checked.Clear();
            Thumbnails.Clear();
            _scanned = true;

            RefreshCategories();
            ApplyFilter(false);

            SetStatus(string.Format(CultureInfo.CurrentCulture,
                "Scanned {0} folders and {1} files in {2:0.0}s.",
                Format.Count(result.FoldersScanned), Format.Count(result.FilesScanned), result.Elapsed.TotalSeconds));

            _countLabel.Text = _findings.Count == 0
                ? "nothing to clean"
                : Format.Count(_findings.Count) + " items  ·  " + Format.Bytes(TotalBytes()) + " found";
        }

        private ScanOptions BuildOptions(List<string> roots)
        {
            ScanOptions options = new ScanOptions();
            options.Roots.AddRange(roots);
            options.ScanDevArtifacts = _options.ScanDevArtifacts;
            options.ScanCaches = _options.ScanCaches;
            options.ScanInstallers = _options.ScanInstallers;
            options.ScanDuplicates = _options.ScanDuplicates;
            options.ScanMedia = _options.ScanMedia;
            options.ScanLargeFiles = _options.ScanLargeFiles;
            options.DuplicateMinBytes = _options.DuplicateMinBytes;
            options.InstallerMinAgeDays = _options.InstallerMinAgeDays;
            options.InstallerMinBytes = _options.InstallerMinBytes;
            options.VideoMinBytes = _options.VideoMinBytes;
            options.AudioMinBytes = _options.AudioMinBytes;
            options.ImageMinBytes = _options.ImageMinBytes;
            options.AppPackageMinBytes = _options.AppPackageMinBytes;
            options.LargeFileMinBytes = _options.LargeFileMinBytes;
            options.ExtraExcludedNames.AddRange(_options.ExtraExcludedNames);

            if (options.ScanCaches) options.CacheLocations.AddRange(RuleSet.DefaultCacheLocations());
            if (options.ScanInstallers) options.InstallerFolders.AddRange(RuleSet.DefaultInstallerFolders());

            return options;
        }

        private void OnScanProgress(ScanProgress progress)
        {
            SetStatus(progress.Phase + "  ·  " + Format.Count(progress.Folders) + " folders, " +
                      Format.Count(progress.Findings) + " items, " + Format.Bytes(progress.Bytes));
        }

        private void ToggleBusy(bool busy)
        {
            _scanButton.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _moreButton.Enabled = !busy;
            _scanButton.Text = busy ? "Scanning" : "Scan";
            _pulse.Running = busy;
            _rail.Enabled = !busy;

            if (busy)
            {
                _scanButton.Style = PillButton.Kind.Outline;
            }
            else
            {
                _scanButton.Style = PillButton.Kind.Solid;
            }
        }

        private void CancelScan()
        {
            if (_cts != null) _cts.Cancel();
        }

        // -------------------------------------------------------------- listing

        private void RefreshCategories()
        {
            List<CategoryRail.Item> items = new List<CategoryRail.Item>();

            items.Add(new CategoryRail.Item
            {
                Title = "All items",
                Category = null,
                Count = _findings.Count,
                Bytes = TotalBytes()
            });

            Category[] order = CategoryInfo.InRailOrder;
            for (int i = 0; i < order.Length; i++)
            {
                int count = 0;
                long bytes = 0;

                for (int f = 0; f < _findings.Count; f++)
                {
                    if (_findings[f].Category != order[i]) continue;
                    count++;
                    bytes += _findings[f].Bytes;
                }

                items.Add(new CategoryRail.Item
                {
                    Title = CategoryInfo.Name(order[i]),
                    Category = order[i],
                    Count = count,
                    Bytes = bytes
                });
            }

            _rail.SetItems(items, _rail.SelectedIndex);
        }

        private void ApplyFilter(bool keepSelection)
        {
            CategoryRail.Item selected = _rail.SelectedItem;
            Category? filter = selected != null ? selected.Category : null;

            _visible = new List<Finding>();
            for (int i = 0; i < _findings.Count; i++)
            {
                if (filter.HasValue && _findings[i].Category != filter.Value) continue;
                _visible.Add(_findings[i]);
            }

            ApplySort();
            _grid.SetItems(_visible, keepSelection);
            UpdateEmptyState();
        }

        private void UpdateToolbar()
        {
            CategoryRail.Item selected = _rail.SelectedItem;
            bool all = selected == null || !selected.Category.HasValue;

            _titleLabel.Text = all ? "All items" : CategoryInfo.Name(selected.Category.Value);
            _blurbLabel.Text = all
                ? "Everything the scan found, in one place."
                : CategoryInfo.Blurb(selected.Category.Value);

            long bytes = 0;
            for (int i = 0; i < _visible.Count; i++) bytes += _visible[i].Bytes;

            _metaLabel.Text = _visible.Count == 0
                ? "empty"
                : _visible.Count + (_visible.Count == 1 ? " item" : " items") + "  ·  " + Format.Bytes(bytes);
        }

        private void UpdateEmptyState()
        {
            bool empty = _visible.Count == 0;
            _grid.Visible = !empty;
            _empty.Visible = empty;

            if (!empty) return;

            if (_findings.Count == 0)
            {
                if (!_scanned)
                {
                    _empty.Kind = ItemKind.Build;
                    _empty.SetText("Nothing scanned yet",
                        "Choose a folder above and press Scan. BinDay looks for build junk, caches, old installers, duplicate files, big media and leftover app packages.");
                }
                else
                {
                    _empty.Kind = ItemKind.Cache;
                    _empty.SetText("Nothing to clean here",
                        "The scan finished without finding anything it could offer to remove. Try a broader folder, such as your user folder or a projects folder.");
                }
            }
            else
            {
                CategoryRail.Item selected = _rail.SelectedItem;
                string name = selected == null || !selected.Category.HasValue ? "this collection" : selected.Title;

                _empty.Kind = selected != null && selected.Category.HasValue
                    ? CategoryInfo.Shape(selected.Category.Value)
                    : ItemKind.Build;

                _empty.SetText("Nothing in " + name,
                    "This collection is empty. Pick another one on the left — the counts update as you scan.");
            }
        }

        private void UpdateInspector()
        {
            Finding primary = _grid.Primary;
            bool ticked = primary != null && _grid.Checked.Contains(primary.Id);
            _inspector.Display(primary, _mode, ticked);
        }

        private void UpdateActionBar()
        {
            int count = 0;
            long bytes = 0;

            for (int i = 0; i < _findings.Count; i++)
            {
                if (!_grid.Checked.Contains(_findings[i].Id)) continue;
                count++;
                bytes += _findings[i].Bytes;
            }

            if (count == 0)
            {
                _tickedLabel.Text = "Nothing ticked yet";
                _hintLabel.Text = "Tick items to build a cleanup list. Nothing is removed until you confirm.";
            }
            else
            {
                _tickedLabel.Text = count + (count == 1 ? " item ticked" : " items ticked") + "  ·  " + Format.Bytes(bytes);
                _hintLabel.Text = _mode == DeleteMode.Recycle
                    ? "All of them go to the Recycle Bin, so they can be restored."
                    : "Only items marked Safe are deleted permanently. The rest go to the Recycle Bin.";
            }

            _cleanButton.Enabled = count > 0;
            _clearButton.Enabled = count > 0;
            _cleanButton.Text = count == 0
                ? "Clean"
                : (_mode == DeleteMode.Permanent ? "Delete " : "Clean ") + count +
                  (count == 1 ? " item" : " items") + "  ·  " + Format.Bytes(bytes);
        }

        private void TickSafeClick(object sender, EventArgs e)
        {
            int added = 0;
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Level != Safety.Safe) continue;
                if (_grid.Checked.Add(_visible[i].Id)) added++;
            }

            _grid.Invalidate();
            UpdateActionBar();
            UpdateInspector();
            SetStatus(added == 0
                ? "Nothing marked Safe in this collection."
                : "Ticked " + added + " Safe item" + (added == 1 ? "" : "s") + " in this collection.");
        }

        private long TotalBytes()
        {
            long total = 0;
            for (int i = 0; i < _findings.Count; i++) total += _findings[i].Bytes;
            return total;
        }

        // ------------------------------------------------------------- cleaning

        private void CleanClick(object sender, EventArgs e)
        {
            List<Finding> selected = new List<Finding>();
            for (int i = 0; i < _findings.Count; i++)
            {
                if (_grid.Checked.Contains(_findings[i].Id)) selected.Add(_findings[i]);
            }

            if (selected.Count == 0)
            {
                SetStatus("Tick something first.");
                return;
            }

            using (ConfirmForm dialog = new ConfirmForm(selected, _mode))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
            }

            RunCleanup(selected, _mode);
        }

        private async void RunCleanup(List<Finding> selected, DeleteMode mode)
        {
            _busy = true;
            ToggleBusy(true);

            CancellationTokenSource cleanup = new CancellationTokenSource();
            IProgress<CleanupProgress> progress = new Progress<CleanupProgress>(delegate (CleanupProgress p)
            {
                SetStatus("Removing " + Ink.ShortPath(p.Current, 90));
            });

            CleanupReport report = null;
            Exception failure = null;

            await Task.Run(delegate
            {
                try
                {
                    report = Cleaner.Run(selected, mode, new WindowsRecycler(), progress, cleanup.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { failure = ex; }
            });

            cleanup.Dispose();
            _busy = false;
            ToggleBusy(false);

            if (failure != null)
            {
                SetStatus("The cleanup failed: " + failure.Message);
                MessageBox.Show(this, "The cleanup failed:\n\n" + failure.Message, "BinDay",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (report == null)
            {
                SetStatus("Cleanup cancelled.");
                return;
            }

            History.Append(report, mode, selected, null);

            RemoveCleanedFindings(selected);
            _grid.Checked.Clear();
            RefreshCategories();
            ApplyFilter(false);
            UpdateActionBar();

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
                    _grid.Checked.Remove(finding.Id);
                }
            }
        }

        // ------------------------------------------------------------- shortcut

        private void ShortcutClick(object sender, EventArgs e)
        {
            try
            {
                string exe = Application.ExecutablePath;
                string folder = Path.GetDirectoryName(exe);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                if (string.IsNullOrEmpty(desktop))
                {
                    desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
                }

                if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop))
                {
                    SetStatus("The Desktop folder could not be found, so no shortcut was created.");
                    return;
                }

                string link = Path.Combine(desktop, "BinDay.lnk");
                CreateShortcut(link, exe, folder);

                SetStatus("Desktop shortcut created.");
                MessageBox.Show(this, "A BinDay shortcut was placed on your Desktop.", "BinDay",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The shortcut could not be created:\n\n" + ex.Message, "BinDay",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void CreateShortcut(string linkPath, string target, string workingDirectory)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("Windows Script Host is not available.");

            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;

            try
            {
                shortcut = shellType.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });

                Type linkType = shortcut.GetType();

                linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                linkType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
                linkType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Clear the junk, keep the good stuff" });
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null)
                {
                    try { Marshal.ReleaseComObject(shortcut); } catch (Exception) { }
                }

                try { Marshal.ReleaseComObject(shell); } catch (Exception) { }
            }
        }

        // ----------------------------------------------------------- preference

        private static string PreferencesPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BinDay", "ui.txt");
            }
        }

        private void LoadPreferences()
        {
            try
            {
                if (!File.Exists(PreferencesPath)) return;

                string[] lines = File.ReadAllLines(PreferencesPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;

                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();

                    if (key == "view")
                    {
                        try
                        {
                            _view = (GridView)Enum.Parse(typeof(GridView), value, true);
                        }
                        catch (Exception) { }
                    }
                    else if (key == "sort")
                    {
                        try
                        {
                            _sort = (SortField)Enum.Parse(typeof(SortField), value, true);
                        }
                        catch (Exception) { }
                    }
                    else if (key == "desc")
                    {
                        _descending = value == "1";
                    }
                }
            }
            catch (Exception) { }

            _grid.ViewMode = _view;
            _grid.Sort = _sort;
            _grid.SortDescending = _descending;

            foreach (KeyValuePair<GridView, PillButton> pair in _viewButtons)
            {
                pair.Value.Toggled = pair.Key == _view;
            }

            _sortButton.Text = "Sort: " + SortName(_sort);
            _directionButton.Text = _descending ? "↓" : "↑";
        }

        private void SavePreferences()
        {
            try
            {
                string folder = Path.GetDirectoryName(PreferencesPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllLines(PreferencesPath, new[]
                {
                    "view=" + _view,
                    "sort=" + _sort,
                    "desc=" + (_descending ? "1" : "0")
                });
            }
            catch (Exception) { }
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.Text = text;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ScanClick(this, EventArgs.Empty);
                return true;
            }

            if (keyData == Keys.Escape && _busy)
            {
                CancelScan();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
