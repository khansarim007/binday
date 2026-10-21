using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BinDay.Core;

namespace BinDay.App
{
    /// <summary>
    /// The last step before anything is touched. Shows exactly what is about to
    /// happen, item by item, and makes permanent deletion an explicit choice.
    /// </summary>
    internal sealed class ConfirmForm : Form
    {
        private readonly List<Finding> _items;
        private readonly DeleteMode _mode;

        public ConfirmForm(List<Finding> items, DeleteMode mode)
        {
            _items = items ?? new List<Finding>();
            _mode = mode;

            Text = mode == DeleteMode.Permanent ? "Delete permanently" : "Move to Recycle Bin";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(720, 560);
            KeyPreview = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            Build();
        }

        private void Build()
        {
            long bytes = 0;
            long files = 0;
            int skipped = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                bytes += _items[i].Bytes;
                files += Math.Max(1, _items[i].FileCount);
                if (_mode == DeleteMode.Permanent && _items[i].Level != Safety.Safe) skipped++;
            }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.Padding = new Padding(24, 22, 24, 20);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            Label title = Ui.Label(_mode == DeleteMode.Permanent ? "Delete permanently?" : "Move to the Recycle Bin?",
                Theme.Large, Theme.Text, false);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.TopLeft;

            FlowLayoutPanel summary = new FlowLayoutPanel();
            summary.Dock = DockStyle.Fill;
            summary.FlowDirection = FlowDirection.LeftToRight;
            summary.WrapContents = false;
            summary.Margin = new Padding(0, 4, 0, 0);

            Label number = Ui.Label(Format.Bytes(bytes), Theme.Huge, Theme.Accent, false);
            number.Margin = new Padding(0, 0, 10, 0);
            number.AutoSize = true;

            Label caption = Ui.Label(
                "will be freed from " + _items.Count + (itemsWord(_items.Count)) +
                " (" + Format.Count(files) + " files)", Theme.Small, Theme.TextMuted, false);
            caption.Margin = new Padding(4, 10, 0, 0);

            summary.Controls.Add(number);
            summary.Controls.Add(caption);

            Panel listCard = new Panel();
            listCard.Dock = DockStyle.Fill;
            listCard.BackColor = Theme.Surface;
            listCard.Padding = new Padding(1);

            ListView list = new ListView();
            list.Dock = DockStyle.Fill;
            DarkList.Attach(list);
            list.Columns.Add("Item", 280);
            list.Columns.Add("Collection", 130);
            list.Columns.Add("Size", 86);
            list.Columns.Add("Grade", 80);

            for (int i = 0; i < _items.Count && i < 400; i++)
            {
                Finding item = _items[i];
                ListViewItem row = new ListViewItem(item.Title);
                row.SubItems.Add(item.CategoryLabel);
                row.SubItems.Add(Format.Bytes(item.Bytes));
                row.SubItems.Add(item.SafetyLabel);
                row.ForeColor = item.Level == Safety.Safe ? Theme.Text : Theme.ForSafety(item.Level);
                for (int c = 0; c < row.SubItems.Count; c++) row.SubItems[c].ForeColor = Theme.TextMuted;
                row.ToolTipText = item.Path;
                list.Items.Add(row);
            }

            if (_items.Count > 400)
            {
                ListViewItem more = new ListViewItem("... and " + (_items.Count - 400) + " more");
                more.ForeColor = Theme.TextDim;
                list.Items.Add(more);
            }

            listCard.Controls.Add(list);

            Panel note = new Panel();
            note.Dock = DockStyle.Fill;
            note.Margin = new Padding(0, 10, 0, 0);

            Label noteText = Ui.Label(string.Empty, Theme.Small, Theme.TextMuted, false);
            noteText.Dock = DockStyle.Top;
            noteText.Height = 34;
            noteText.TextAlign = ContentAlignment.TopLeft;

            CheckBox confirmPermanent = new CheckBox();
            confirmPermanent.AutoSize = true;
            confirmPermanent.FlatStyle = FlatStyle.Flat;
            confirmPermanent.ForeColor = Theme.Caution;
            confirmPermanent.Font = Theme.Small;
            confirmPermanent.Text = "I understand these files cannot be recovered.";
            confirmPermanent.Visible = _mode == DeleteMode.Permanent;
            confirmPermanent.Top = 30;
            confirmPermanent.Left = 0;

            if (_mode == DeleteMode.Permanent)
            {
                noteText.Text = skipped == 0
                    ? "Permanent deletion skips the Recycle Bin. There is no undo."
                    : skipped + " item" + (skipped == 1 ? " is" : "s are") + " not marked Safe, so " +
                      (skipped == 1 ? "it" : "they") + " will go to the Recycle Bin instead.";
            }
            else
            {
                noteText.Text = "Everything goes to the Recycle Bin, so you can restore it later if you change your mind.";
            }

            note.Controls.Add(noteText);
            note.Controls.Add(confirmPermanent);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);

            PillButton confirm = new PillButton();
            confirm.Text = _mode == DeleteMode.Permanent ? "Delete permanently" : "Move to Recycle Bin";
            confirm.Style = _mode == DeleteMode.Permanent ? PillButton.Kind.Danger : PillButton.Kind.Solid;
            confirm.Width = 190;
            confirm.Height = 36;
            confirm.Margin = new Padding(8, 0, 0, 0);
            confirm.Enabled = _mode != DeleteMode.Permanent;
            confirm.Click += delegate { DialogResult = DialogResult.OK; };

            PillButton cancel = new PillButton();
            cancel.Text = "Keep them";
            cancel.Style = PillButton.Kind.Outline;
            cancel.Width = 110;
            cancel.Height = 36;
            cancel.Margin = new Padding(8, 0, 0, 0);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            confirmPermanent.CheckedChanged += delegate { confirm.Enabled = confirmPermanent.Checked; };

            buttons.Controls.Add(confirm);
            buttons.Controls.Add(cancel);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(summary, 0, 1);
            root.Controls.Add(listCard, 0, 2);
            root.Controls.Add(note, 0, 3);
            root.Controls.Add(buttons, 0, 4);

            Controls.Add(root);
            CancelButton = null;

            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
                else if (e.KeyCode == Keys.Enter && confirm.Enabled) DialogResult = DialogResult.OK;
            };
        }

        private static string itemsWord(int count)
        {
            return count == 1 ? " item" : " items";
        }
    }
}
