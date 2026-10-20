using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using RedundantFinder.Core;

namespace RedundantFinder
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _folders = new TextBox();
        private readonly Button _browse = new Button();
        private readonly Button _profile = new Button();
        private readonly NumericUpDown _minMb = new NumericUpDown();
        private readonly ComboBox _keep = new ComboBox();
        private readonly Button _scan = new Button();
        private readonly Button _cancel = new Button();
        private readonly Button _quarantine = new Button();
        private readonly Button _delete = new Button();
        private readonly Button _export = new Button();
        private readonly ListView _groups = new ListView();
        private readonly ListView _files = new ListView();
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _statusText = new ToolStripStatusLabel();
        private readonly ToolStripProgressBar _bar = new ToolStripProgressBar();

        private ScanResult _result;
        private CancellationTokenSource _cts;
        private bool _busy;

        public MainForm()
        {
            Text = "Redundant File Finder";
            ClientSize = new Size(960, 660);
            MinimumSize = new Size(820, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            AllowDrop = true;

            BuildLayout();
            WireEvents();
            UpdateButtons();
            _statusText.Text = "Pick a folder (or press \"My profile\"), then Scan. Nothing is changed until you choose Quarantine or Delete.";
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ---- options ----
            var options = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                Padding = new Padding(12, 12, 12, 4)
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var foldersLabel = new Label
            {
                Text = "Folders:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 6, 3)
            };

            _folders.Dock = DockStyle.Fill;
            _folders.Margin = new Padding(0, 5, 6, 3);
            _folders.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            _browse.Text = "Browse...";
            _browse.AutoSize = true;
            _browse.Padding = new Padding(6, 2, 6, 2);
            _browse.Margin = new Padding(0, 4, 4, 3);

            _profile.Text = "My profile";
            _profile.AutoSize = true;
            _profile.Padding = new Padding(6, 2, 6, 2);
            _profile.Margin = new Padding(0, 4, 0, 3);

            options.Controls.Add(foldersLabel, 0, 0);
            options.Controls.Add(_folders, 1, 0);
            options.Controls.Add(_browse, 2, 0);
            options.Controls.Add(_profile, 3, 0);

            var minLabel = new Label
            {
                Text = "Ignore files under",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 12, 6, 3)
            };

            var tuneRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, 8, 0, 0)
            };

            _minMb.Width = 70;
            _minMb.Minimum = 0;
            _minMb.Maximum = 1048576;
            _minMb.Value = 1;
            _minMb.TextAlign = HorizontalAlignment.Right;
            _minMb.Margin = new Padding(0, 0, 4, 0);

            var mbLabel = new Label { Text = "MB", AutoSize = true, Margin = new Padding(0, 6, 18, 0) };

            var keepLabel = new Label { Text = "Keep:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };

            _keep.DropDownStyle = ComboBoxStyle.DropDownList;
            _keep.Width = 150;
            _keep.Margin = new Padding(0, 0, 18, 0);
            _keep.Items.AddRange(new object[] { "Oldest copy", "Newest copy", "Shortest path" });
            _keep.SelectedIndex = 0;

            var hint = new Label
            {
                Text = "Smaller files are skipped. Nothing is deleted without confirmation.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 6, 0, 0)
            };

            tuneRow.Controls.Add(_minMb);
            tuneRow.Controls.Add(mbLabel);
            tuneRow.Controls.Add(keepLabel);
            tuneRow.Controls.Add(_keep);
            tuneRow.Controls.Add(hint);

            options.Controls.Add(minLabel, 0, 1);
            options.Controls.Add(tuneRow, 1, 1);
            options.SetColumnSpan(tuneRow, 3);

            // ---- action buttons ----
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(12, 2, 12, 8)
            };

            ConfigureButton(_scan, "Scan", 84);
            ConfigureButton(_cancel, "Cancel", 84);
            ConfigureButton(_quarantine, "Quarantine duplicates...", 170);
            ConfigureButton(_delete, "Delete duplicates...", 150);
            ConfigureButton(_export, "Export CSV...", 110);

            actions.Controls.Add(_scan);
            actions.Controls.Add(_cancel);
            actions.Controls.Add(_quarantine);
            actions.Controls.Add(_delete);
            actions.Controls.Add(_export);

            // ---- results ----
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 330,
                Panel1MinSize = 140,
                Panel2MinSize = 120
            };

            _groups.Dock = DockStyle.Fill;
            _groups.View = View.Details;
            _groups.FullRowSelect = true;
            _groups.HideSelection = false;
            _groups.MultiSelect = false;
            _groups.VirtualMode = true;
            _groups.ShowItemToolTips = true;
            _groups.Columns.Add("Wasted", 90, HorizontalAlignment.Right);
            _groups.Columns.Add("File size", 90, HorizontalAlignment.Right);
            _groups.Columns.Add("Copies", 60, HorizontalAlignment.Right);
            _groups.Columns.Add("Copy that is kept", 640, HorizontalAlignment.Left);

            _files.Dock = DockStyle.Fill;
            _files.View = View.Details;
            _files.FullRowSelect = true;
            _files.HideSelection = false;
            _files.MultiSelect = false;
            _files.ShowItemToolTips = true;
            _files.Columns.Add("Action", 72, HorizontalAlignment.Left);
            _files.Columns.Add("Path", 560, HorizontalAlignment.Left);
            _files.Columns.Add("Size", 90, HorizontalAlignment.Right);
            _files.Columns.Add("Created", 130, HorizontalAlignment.Left);

            split.Panel1.Controls.Add(_groups);
            split.Panel2.Controls.Add(_files);

            // ---- status ----
            _status.SizingGrip = false;
            _status.Dock = DockStyle.Fill;
            _statusText.Spring = true;
            _statusText.TextAlign = ContentAlignment.MiddleLeft;
            _bar.Visible = false;
            _bar.Width = 180;
            _bar.Style = ProgressBarStyle.Marquee;
            _status.Items.Add(_statusText);
            _status.Items.Add(_bar);

            root.Controls.Add(options, 0, 0);
            root.Controls.Add(actions, 0, 1);
            root.Controls.Add(split, 0, 2);
            root.Controls.Add(_status, 0, 3);

            Controls.Add(root);
        }

        private static void ConfigureButton(Button button, string text, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.Margin = new Padding(4, 2, 0, 0);
            button.FlatStyle = FlatStyle.System;
        }

        private void WireEvents()
        {
            _scan.Click += Scan_Click;
            _cancel.Click += delegate { if (_cts != null) _cts.Cancel(); };
            _quarantine.Click += Quarantine_Click;
            _delete.Click += Delete_Click;
            _export.Click += Export_Click;
            _browse.Click += Browse_Click;
            _profile.Click += Profile_Click;

            _groups.RetrieveVirtualItem += Groups_RetrieveVirtualItem;
            _groups.SelectedIndexChanged += Groups_SelectedIndexChanged;
            _groups.Resize += delegate { FitGroupColumns(); };

            _files.Resize += delegate { FitFileColumns(); };
            _files.DoubleClick += Files_DoubleClick;

            DragEnter += Form_DragEnter;
            DragDrop += Form_DragDrop;
            FormClosing += Form_FormClosing;

            // Children would otherwise swallow drops meant for the form.
            foreach (Control dropTarget in new Control[] { _folders, _groups, _files })
            {
                dropTarget.AllowDrop = true;
                dropTarget.DragEnter += Form_DragEnter;
                dropTarget.DragDrop += Form_DragDrop;
            }

            Resize += delegate { FitGroupColumns(); FitFileColumns(); };
            Shown += delegate { FitGroupColumns(); FitFileColumns(); };
        }

        // ------------------------------------------------------------------ UI glue

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;

            _folders.Enabled = !busy;
            _browse.Enabled = !busy;
            _profile.Enabled = !busy;
            _minMb.Enabled = !busy;
            _keep.Enabled = !busy;

            _scan.Enabled = !busy;
            _cancel.Enabled = busy;
            _cancel.Visible = busy;

            _bar.Visible = busy;
            _bar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;

            UseWaitCursor = busy;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasGroups = !_busy && _result != null && _result.Groups.Count > 0;
            _quarantine.Enabled = hasGroups;
            _delete.Enabled = hasGroups;
            _export.Enabled = !_busy && _result != null && _result.Groups.Count > 0;
        }

        private void UpdateSummary()
        {
            if (_result == null)
            {
                _statusText.Text = "Ready.";
                return;
            }

            _statusText.Text = string.Format(CultureInfo.CurrentCulture,
                "{0:N0} duplicate groups  |  {1:N0} redundant copies  |  {2} reclaimable  |  {3:N0} files seen in {4:0.0}s",
                _result.Groups.Count,
                _result.RedundantCopies,
                FormatSize(_result.ReclaimableBytes),
                _result.FilesSeen,
                _result.Elapsed.TotalSeconds);
        }

        private void RecalculateTotals()
        {
            if (_result == null) return;

            _result.ReclaimableBytes = 0;
            _result.RedundantCopies = 0;
            foreach (DuplicateGroup group in _result.Groups)
            {
                _result.ReclaimableBytes += group.WastedBytes;
                _result.RedundantCopies += group.Files.Count - 1;
            }
        }

        private void BindGroups()
        {
            _groups.VirtualListSize = 0;
            _groups.VirtualListSize = _result == null ? 0 : _result.Groups.Count;
            _groups.Invalidate();
            _files.Items.Clear();
            RecalculateTotals();
            UpdateSummary();
            UpdateButtons();
        }

        private void Groups_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (_result == null || e.ItemIndex < 0 || e.ItemIndex >= _result.Groups.Count)
            {
                e.Item = new ListViewItem(string.Empty);
                return;
            }

            DuplicateGroup group = _result.Groups[e.ItemIndex];
            var item = new ListViewItem(FormatSize(group.WastedBytes));
            item.SubItems.Add(FormatSize(group.Size));
            item.SubItems.Add(group.Files.Count.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(group.Keeper == null ? string.Empty : group.Keeper.Path);
            item.Tag = e.ItemIndex;
            item.ToolTipText = string.Format(CultureInfo.CurrentCulture,
                "{0} identical files, {1} each", group.Files.Count, FormatSize(group.Size));
            e.Item = item;
        }

        private void Groups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_result == null) return;

            int index = _groups.SelectedIndices.Count > 0 ? _groups.SelectedIndices[0] : -1;
            if (index < 0 || index >= _result.Groups.Count)
            {
                _files.Items.Clear();
                return;
            }

            DuplicateGroup group = _result.Groups[index];

            _files.BeginUpdate();
            try
            {
                _files.Items.Clear();
                foreach (FileEntry file in group.Files)
                {
                    bool keep = file == group.Keeper;
                    var item = new ListViewItem(keep ? "KEEP" : "remove");
                    item.SubItems.Add(file.Path);
                    item.SubItems.Add(FormatSize(file.Size));
                    item.SubItems.Add(file.CreatedUtc == DateTime.MinValue
                        ? string.Empty
                        : file.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
                    item.ForeColor = keep ? Color.FromArgb(0, 110, 60) : Color.FromArgb(165, 40, 40);
                    item.ToolTipText = file.Path;
                    item.Tag = file.Path;
                    _files.Items.Add(item);
                }
            }
            finally
            {
                _files.EndUpdate();
            }
        }

        private void Files_DoubleClick(object sender, EventArgs e)
        {
            if (_files.SelectedItems.Count == 0) return;
            string path = _files.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                else
                {
                    MessageBox.Show(this, "That file no longer exists.", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not open Explorer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FitGroupColumns()
        {
            if (_groups.Columns.Count < 4) return;
            int available = _groups.ClientSize.Width - 4;
            int fixedWidth = _groups.Columns[0].Width + _groups.Columns[1].Width + _groups.Columns[2].Width;
            int width = Math.Max(120, available - fixedWidth);
            if (_groups.Columns[3].Width != width) _groups.Columns[3].Width = width;
        }

        private void FitFileColumns()
        {
            if (_files.Columns.Count < 4) return;
            int available = _files.ClientSize.Width - 4;
            int fixedWidth = _files.Columns[0].Width + _files.Columns[2].Width + _files.Columns[3].Width;
            int width = Math.Max(120, available - fixedWidth);
            if (_files.Columns[1].Width != width) _files.Columns[1].Width = width;
        }

        // ------------------------------------------------------------------ folder input

        private void Browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder to scan";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(_folders.Text.Trim()))
                {
                    dialog.SelectedPath = _folders.Text.Trim();
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AppendFolder(dialog.SelectedPath);
                }
            }
        }

        private void Profile_Click(object sender, EventArgs e)
        {
            AppendFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        private void AppendFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;

            List<string> current = ParseRoots();
            foreach (string existing in current)
            {
                if (string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)) return;
            }

            current.Add(folder);
            _folders.Text = string.Join("; ", current);
        }

        private List<string> ParseRoots()
        {
            var result = new List<string>();
            string[] parts = _folders.Text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string cleaned = part.Trim().Trim('"');
                if (cleaned.Length == 0) continue;
                if (!result.Any(r => string.Equals(r, cleaned, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(cleaned);
                }
            }

            return result;
        }

        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped == null) return;

            foreach (string path in dropped)
            {
                if (Directory.Exists(path)) AppendFolder(path);
                else if (File.Exists(path))
                {
                    string parent = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(parent)) AppendFolder(parent);
                }
            }
        }

        // ------------------------------------------------------------------ scan

        private async void Scan_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            List<string> roots = ParseRoots();
            if (roots.Count == 0)
            {
                MessageBox.Show(this, "Choose at least one folder to scan.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var missing = roots.Where(r => !Directory.Exists(r)).ToList();
            if (missing.Count > 0)
            {
                MessageBox.Show(this,
                    "These folders do not exist:\r\n\r\n" + string.Join("\r\n", missing),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var options = new ScanOptions
            {
                Roots = roots,
                MinSizeBytes = (long)_minMb.Value * 1024L * 1024L,
                Keep = (KeepPolicy)_keep.SelectedIndex,
                ExcludedDirNames = new List<string>
                {
                    "Windows", "Program Files", "Program Files (x86)", "ProgramData",
                    "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
                    "WinSxS", "AppData", "node_modules", ".git", ".svn", ".hg"
                },
                SkipPaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DuplicateQuarantine")
                }
            };

            _result = null;
            BindGroups();
            SetBusy(true);
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            var progress = new Progress<ScanProgress>(p =>
            {
                if (p == null) return;
                if (p.Phase == "Comparing")
                {
                    _statusText.Text = string.Format(CultureInfo.CurrentCulture,
                        "Comparing contents... {0:N0} candidates, {1:N0} duplicate groups loaded",
                        p.Candidates, p.GroupsFound);
                }
                else
                {
                    _statusText.Text = string.Format(CultureInfo.CurrentCulture,
                        "Scanning... {0:N0} files  |  {1}", p.FilesSeen, Shorten(p.CurrentFolder, 80));
                }
            });

            try
            {
                ScanResult result = await Task.Run(() => Scanner.Scan(options, progress, token));

                _result = result;
                BindGroups();

                if (result.Groups.Count == 0)
                {
                    _statusText.Text = string.Format(CultureInfo.CurrentCulture,
                        "No duplicates found ({0:N0} files seen{1}).",
                        result.FilesSeen,
                        result.ErrorCount > 0 ? string.Format(", {0:N0} unreadable", result.ErrorCount) : string.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                _statusText.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Scan failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        // ------------------------------------------------------------------ clean-up

        private async void Quarantine_Click(object sender, EventArgs e)
        {
            if (_busy || _result == null || _result.Groups.Count == 0) return;

            string quarantineDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DuplicateQuarantine");

            string message = string.Format(CultureInfo.CurrentCulture,
                "Move {0:N0} duplicate files ({1}) into:\r\n\r\n{2}\r\n\r\n" +
                "One copy of every file stays where it is. Files in this folder can be restored at any time.\r\n\r\nContinue?",
                _result.RedundantCopies, FormatSize(_result.ReclaimableBytes), quarantineDir);

            if (MessageBox.Show(this, message, "Quarantine duplicates",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            await RunCleanup(CleanupMode.Quarantine, quarantineDir);
        }

        private async void Delete_Click(object sender, EventArgs e)
        {
            if (_busy || _result == null || _result.Groups.Count == 0) return;

            string message = string.Format(CultureInfo.CurrentCulture,
                "Permanently delete {0:N0} duplicate files ({1})?\r\n\r\n" +
                "This cannot be undone and these files do not go to the Recycle Bin.\r\n" +
                "One copy of every file is always kept.\r\n\r\nContinue?",
                _result.RedundantCopies, FormatSize(_result.ReclaimableBytes));

            if (MessageBox.Show(this, message, "Delete duplicates",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            await RunCleanup(CleanupMode.Delete, null);
        }

        private async Task RunCleanup(CleanupMode mode, string quarantineDir)
        {
            if (_result == null) return;

            var snapshot = new List<DuplicateGroup>(_result.Groups);
            int total = 0;
            foreach (DuplicateGroup g in snapshot) total += Math.Max(0, g.Files.Count - 1);

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            SetBusy(true);

            _bar.Visible = true;
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Maximum = Math.Max(1, total);
            _bar.Value = 0;

            var progress = new Progress<int>(done =>
            {
                if (done <= _bar.Maximum) _bar.Value = done;
                _statusText.Text = string.Format(CultureInfo.CurrentCulture,
                    "{0}... {1:N0} of {2:N0}", mode == CleanupMode.Quarantine ? "Moving" : "Deleting", done, total);
            });

            CleanupOutcome outcome = null;
            try
            {
                outcome = await Task.Run(() => Cleaner.Run(snapshot, mode, quarantineDir, progress, token));

                PruneGroups();
                BindGroups();

                string summary = string.Format(CultureInfo.CurrentCulture,
                    "{0:N0} of {1:N0} files processed. {2} freed.",
                    outcome.Succeeded, outcome.Records.Count, FormatSize(outcome.BytesFreed));

                if (outcome.Failed > 0)
                {
                    string details = string.Join("\r\n", outcome.Records
                        .Where(r => !r.Success)
                        .Take(8)
                        .Select(r => Path.GetFileName(r.Source) + ": " + r.Error));

                    summary += string.Format(CultureInfo.CurrentCulture,
                        "\r\n\r\n{0:N0} could not be processed (in use or access denied):\r\n\r\n{1}",
                        outcome.Failed, details);
                }

                if (mode == CleanupMode.Quarantine)
                {
                    summary += "\r\n\r\nQuarantine folder:\r\n" + quarantineDir;
                }

                MessageBox.Show(this, summary, mode == CleanupMode.Quarantine ? "Quarantine finished" : "Delete finished",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                PruneGroups();
                BindGroups();
                _statusText.Text = "Cleanup cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Cleanup failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        /// <summary>Drops files that no longer exist (moved/deleted), then empty groups.</summary>
        private void PruneGroups()
        {
            if (_result == null) return;

            var empty = new List<DuplicateGroup>();
            foreach (DuplicateGroup group in _result.Groups)
            {
                group.Files.RemoveAll(f => f != group.Keeper && !File.Exists(f.Path));
                if (group.Files.Count < 2) empty.Add(group);
            }

            foreach (DuplicateGroup group in empty) _result.Groups.Remove(group);
        }

        private void Export_Click(object sender, EventArgs e)
        {
            if (_result == null) return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV file (*.csv)|*.csv";
                dialog.FileName = "duplicates.csv";
                dialog.Title = "Save duplicate report";

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    CsvExport.Write(_result, dialog.FileName);
                    _statusText.Text = "Report saved: " + dialog.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Could not save report",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_busy) return;

            DialogResult answer = MessageBox.Show(this,
                "A scan or cleanup is still running. Stop it and close?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            if (_cts != null) _cts.Cancel();
        }

        // ------------------------------------------------------------------ formatting

        private static string FormatSize(long bytes)
        {
            double value = bytes;
            if (value >= 1024d * 1024d * 1024d * 1024d) return (value / (1024d * 1024d * 1024d * 1024d)).ToString("0.00", CultureInfo.CurrentCulture) + " TB";
            if (value >= 1024d * 1024d * 1024d) return (value / (1024d * 1024d * 1024d)).ToString("0.00", CultureInfo.CurrentCulture) + " GB";
            if (value >= 1024d * 1024d) return (value / (1024d * 1024d)).ToString("0.0", CultureInfo.CurrentCulture) + " MB";
            if (value >= 1024d) return (value / 1024d).ToString("0.0", CultureInfo.CurrentCulture) + " KB";
            return bytes.ToString("N0", CultureInfo.CurrentCulture) + " B";
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return "..." + text.Substring(text.Length - max + 3);
        }
    }
}
