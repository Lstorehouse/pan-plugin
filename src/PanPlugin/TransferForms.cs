using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>从候选客户里挑一个作为账目转移目标，支持按名称/别名搜索。</summary>
    public class PickCustomerForm : Form
    {
        public Customer Selected;

        private readonly List<Customer> _all;
        private readonly List<Customer> _shown = new List<Customer>();
        private readonly TextBox _txt = new TextBox();
        private readonly ListBox _list = new ListBox();

        public PickCustomerForm(IEnumerable<Customer> candidates, string prompt)
        {
            _all = candidates.OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();

            Text = "选择目标客户";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 390);

            var lbl = new Label { Text = prompt, AutoSize = true, Location = new Point(14, 14) };
            _txt.Location = new Point(14, 40);
            _txt.Width = 410;
            _txt.TextChanged += (s, e) => Reload();
            _list.Location = new Point(14, 70);
            _list.Size = new Size(410, 240);
            _list.DoubleClick += (s, e) =>
            {
                if (_list.SelectedIndex < 0) return;
                DialogResult = DialogResult.OK;
                Close();
            };

            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(236, 330), Width = 88 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(334, 330), Width = 88 };
            Controls.AddRange(new Control[] { lbl, _txt, _list, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _shown.Count)
                {
                    MessageBox.Show(this, "请先选择一个目标客户。", "生化盘插件");
                    e.Cancel = true;
                    return;
                }
                Selected = _shown[_list.SelectedIndex];
            };

            Reload();
        }

        private void Reload()
        {
            var q = (_txt.Text ?? "").Trim();
            _shown.Clear();
            _shown.AddRange(_all.Where(c => q.Length == 0 ||
                c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                c.Aliases.Any(a => a.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)));
            _list.Items.Clear();
            foreach (var c in _shown)
                _list.Items.Add(c.Name + (c.Archived ? "　（已归档）" : "")
                    + (c.Aliases.Count > 0 ? "　（别名：" + string.Join("、", c.Aliases) + "）" : ""));
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }
    }

    /// <summary>账目转移日志：列出每次转移，选中未撤销的批次可整批撤销。</summary>
    public class TransferLogForm : Form
    {
        public bool Changed;

        private readonly Store _store;
        private readonly ListBox _list = new ListBox();
        private readonly List<TransferBatch> _shown = new List<TransferBatch>();

        public TransferLogForm(Store store)
        {
            _store = store;

            Text = "账目转移记录（可撤销）";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(580, 410);

            var lbl = new Label
            {
                Text = "每次转移生成一条日志。撤销会把该批记录退回原客户；\n"
                     + "已被更晚的转移重新处理或已删除的记录会自动跳过。",
                AutoSize = true,
                MaximumSize = new Size(550, 0),
                Location = new Point(14, 12),
                ForeColor = Color.DimGray
            };
            _list.Location = new Point(14, 62);
            _list.Size = new Size(550, 270);

            var btnUndo = new Button { Text = "撤销所选转移", Location = new Point(14, 346), Width = 140 };
            btnUndo.Click += (s, e) => UndoSelectedBatch();
            var btnClose = new Button { Text = "关闭", DialogResult = DialogResult.OK, Location = new Point(466, 346), Width = 96 };
            Controls.AddRange(new Control[] { lbl, _list, btnUndo, btnClose });
            AcceptButton = btnClose;
            CancelButton = btnClose;

            Reload();
        }

        private void Reload()
        {
            _shown.Clear();
            _shown.AddRange(_store.TransferBatches.OrderByDescending(b => b.At));
            _list.Items.Clear();
            foreach (var b in _shown)
                _list.Items.Add(b.At.ToString("MM-dd HH:mm") + "   " + b.FromName
                    + " → " + b.ToName + "   " + b.Count + " 条"
                    + (b.Undone ? "　（已撤销 " + b.UndoneCount + " 条）" : ""));
            if (_list.Items.Count == 0) _list.Items.Add("（还没有账目转移记录）");
            else _list.SelectedIndex = 0;
        }

        private void UndoSelectedBatch()
        {
            if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _shown.Count)
            {
                MessageBox.Show(this, "请先选中一条转移记录。", "生化盘插件");
                return;
            }
            var b = _shown[_list.SelectedIndex];
            if (b.Undone)
            {
                MessageBox.Show(this, "这次转移已经撤销过了。", "生化盘插件");
                return;
            }
            if (MessageBox.Show(this,
                    "确定撤销这次转移吗？\n\n" + b.At.ToString("yyyy-MM-dd HH:mm")
                    + "  " + b.FromName + " → " + b.ToName + "（" + b.Count + " 条）\n"
                    + "记录会退回「" + b.FromName + "」。",
                    "确认撤销", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            int n = _store.UndoTransfer(b);
            _store.Save();
            Changed = true;
            Reload();
            MessageBox.Show(this,
                "已撤销 " + n + " 条，退回「" + b.FromName + "」。\n跳过 "
                + (b.Count - n) + " 条（已被再次转移或已删除）。",
                "生化盘插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
