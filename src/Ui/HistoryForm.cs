using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>Shows what BinDay cleaned up in the past.</summary>
    internal sealed class HistoryForm : Form
    {
        private readonly ListView _list;
        private readonly PillButton _clearButton;

        public HistoryForm()
        {
            Text = "BinDay - cleanup history";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 420);
            ClientSize = new Size(780, 480);
            ShowInTaskbar = false;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.Padding = new Padding(16, 14, 16, 14);

            Label heading = Ui.Label("Cleanup history", Theme.Large, Theme.Text, false);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.AutoEllipsis = true;

            _list = new ListView();
            DarkList.Attach(_list);
            _list.Dock = DockStyle.Fill;
            _list.Columns.Add("When", 175);
            _list.Columns.Add("Action", 90);
            _list.Columns.Add("Items", 70);
            _list.Columns.Add("Freed", 100);
            _list.Columns.Add("Skipped", 80);
            _list.Columns.Add("What", 260);
            _list.Resize += delegate
            {
                int width = _list.ClientSize.Width - 175 - 90 - 70 - 100 - 80 - 30;
                if (width < 120) width = 120;
                _list.Columns[5].Width = width;
            };

            _clearButton = new PillButton();
            _clearButton.Text = "Clear history";
            _clearButton.Style = PillButton.Kind.Outline;
            _clearButton.Width = 140;
            _clearButton.Height = 34;
            _clearButton.Click += delegate
            {
                if (MessageBox.Show(this, "Delete the stored cleanup history?\n\nNothing else is touched.",
                    "BinDay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    return;
                }

                History.Clear(null);
                LoadEntries();
            };

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.WrapContents = false;
            footer.Controls.Add(_clearButton);

            PillButton close = new PillButton();
            close.Text = "Close";
            close.Style = PillButton.Kind.Ghost;
            close.Width = 90;
            close.Height = 34;
            close.Margin = new Padding(8, 0, 0, 0);
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);

            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(_list, 0, 1);
            root.Controls.Add(footer, 0, 2);

            Controls.Add(root);

            LoadEntries();
        }

        private void LoadEntries()
        {
            List<HistoryEntry> entries = History.ReadAll(null);

            _list.BeginUpdate();
            _list.Items.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                HistoryEntry entry = entries[i];

                ListViewItem item = new ListViewItem(entry.When);
                item.SubItems.Add(entry.ModeLabel);
                item.SubItems.Add(entry.Removed.ToString());
                item.SubItems.Add(Format.Bytes(entry.Bytes));
                item.SubItems.Add((entry.Failed + entry.Refused).ToString());
                item.SubItems.Add(Summarise(entry));

                if (entry.Failed + entry.Refused > 0) item.ForeColor = Theme.Caution;
                else item.ForeColor = Theme.Text;
                for (int c = 0; c < item.SubItems.Count; c++) item.SubItems[c].ForeColor = Theme.TextMuted;
                _list.Items.Add(item);
            }

            _list.EndUpdate();

            if (entries.Count == 0)
            {
                ListViewItem empty = new ListViewItem("No cleanups yet.");
                empty.SubItems.Add("");
                empty.SubItems.Add("");
                empty.SubItems.Add("");
                empty.SubItems.Add("");
                empty.SubItems.Add("Run a scan and remove something to see it here.");
                empty.ForeColor = Theme.TextDim;
                _list.Items.Add(empty);
            }
        }

        private static string Summarise(HistoryEntry entry)
        {
            if (entry.Paths.Count == 0) return "";

            string first = entry.Paths[0];
            int cut = first.LastIndexOfAny(new char[] { '\\', '/' });
            if (cut >= 0) first = first.Substring(cut + 1);
            if (entry.Paths.Count == 1) return first;

            return first + " and " + (entry.Paths.Count - 1) + " more";
        }
    }
}
