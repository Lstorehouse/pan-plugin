using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PanPlugin
{
    public class MainForm : Form
    {
        private readonly Store _store = Store.Load();

        private TextBox _txtSearch;
        private Button _btnImportBupan;
        private Button _btnImportRefund;
        private ListBox _lstCustomers;
        private Label _lblName;
        private Label _lblPending;
        private Label _lblBalance;
        private ListView _lvPending;
        private ListView _lvHistory;
        private Button _btnMarkDone;
        private Button _btnAddBupan;
        private Button _btnAddCredit;
        private Button _btnDeduct;
        private Button _btnUndo;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusText;

        private Customer _current;

        public MainForm()
        {
            Text = "生化盘客户管理插件";
            ClientSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            RefreshList();
        }

        // ---------- UI 构建 ----------

        private void BuildUi()
        {
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 8, 8, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            _txtSearch = new TextBox { Width = 280, Font = new Font(Font, FontStyle.Regular) };
            _txtSearch.TextChanged += (s, e) => RefreshList();
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _lstCustomers.Items.Count > 0)
                {
                    _lstCustomers.SelectedIndex = 0;
                    e.SuppressKeyPress = true;
                }
            };
            _btnImportBupan = new Button { Text = "导入补盘名单…", Width = 120 };
            _btnImportBupan.Click += (s, e) => ImportBupan();
            _btnImportRefund = new Button { Text = "导入退款名单…", Width = 120 };
            _btnImportRefund.Click += (s, e) => ImportRefund();

            top.Controls.Add(new Label { Text = "搜索客户：", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            top.Controls.Add(_txtSearch);
            top.Controls.Add(_btnImportBupan);
            top.Controls.Add(_btnImportRefund);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill
            };

            _lstCustomers = new ListBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 9F) };
            _lstCustomers.SelectedIndexChanged += (s, e) => RefreshDetail();
            split.Panel1.Controls.Add(_lstCustomers);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblName = new Label { Text = "（未选择客户）", AutoSize = true, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), Padding = new Padding(8, 6, 3, 0) };
            _lblPending = new Label { Text = "", AutoSize = true, Padding = new Padding(10, 0, 3, 0), ForeColor = Color.Firebrick };
            _lblBalance = new Label { Text = "", AutoSize = true, Padding = new Padding(10, 0, 3, 4), ForeColor = Color.SeaGreen };

            _lvPending = NewList(new[] { "工单号", "盘型", "张数", "日期", "备注" });
            _lvHistory = NewList(new[] { "时间", "类型", "内容", "备注" });

            var pendingBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Height = 34, Padding = new Padding(8, 4, 0, 0) };
            _btnMarkDone = new Button { Text = "勾选项标记已补发", Width = 140 };
            _btnMarkDone.Click += (s, e) => MarkDone();
            _btnAddBupan = new Button { Text = "手工新增补盘…", Width = 120 };
            _btnAddBupan.Click += (s, e) => AddManualBupan();
            pendingBtns.Controls.Add(_btnMarkDone);
            pendingBtns.Controls.Add(_btnAddBupan);

            var creditBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Height = 34, Padding = new Padding(8, 4, 0, 0) };
            _btnAddCredit = new Button { Text = "手工增加抵扣…", Width = 120 };
            _btnAddCredit.Click += (s, e) => AddCredit();
            _btnDeduct = new Button { Text = "进货抵扣（扣减）…", Width = 140 };
            _btnDeduct.Click += (s, e) => DeductCredit();
            _btnUndo = new Button { Text = "撤销历史中所选记录", Width = 160 };
            _btnUndo.Click += (s, e) => UndoSelected();
            creditBtns.Controls.Add(_btnAddCredit);
            creditBtns.Controls.Add(_btnDeduct);
            creditBtns.Controls.Add(_btnUndo);

            right.Controls.Add(_lblName, 0, 0);
            right.Controls.Add(_lblPending, 0, 1);
            right.Controls.Add(_lblBalance, 0, 2);
            right.Controls.Add(_lvPending, 0, 3);
            right.Controls.Add(_lvHistory, 0, 4);
            var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            btnPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            btnPanel.Controls.Add(pendingBtns, 0, 0);
            btnPanel.Controls.Add(creditBtns, 1, 0);
            right.Controls.Add(btnPanel, 0, 5);

            split.Panel2.Controls.Add(right);

            _status = new StatusStrip();
            _statusText = new ToolStripStatusLabel { Text = "就绪" };
            _status.Items.Add(_statusText);

            Controls.Add(split);
            Controls.Add(top);
            Controls.Add(_status);
            split.SplitterDistance = 320;
        }

        private static ListView NewList(string[] cols)
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                CheckBoxes = false
            };
            foreach (var c in cols)
                lv.Columns.Add(c, c.Length > 2 ? 110 : 70);
            lv.Columns[0].Width = 160;
            return lv;
        }

        // ---------- 数据刷新 ----------

        private List<LedgerEntry> PendingOf(string customerId)
        {
            return _store.Entries.Where(e => e.CustomerId == customerId
                && (e.Kind == EntryKind.BupanOrder || e.Kind == EntryKind.ManualBupan)
                && !e.Processed).ToList();
        }

        private Dictionary<DiscType, int> PendingByType(string customerId)
        {
            var dict = new Dictionary<DiscType, int>();
            foreach (var e in PendingOf(customerId))
            {
                if (!dict.ContainsKey(e.DiscType)) dict[e.DiscType] = 0;
                dict[e.DiscType] += e.Qty;
            }
            return dict;
        }

        private void RefreshList()
        {
            var query = (_txtSearch.Text ?? "").Trim();
            IEnumerable<Customer> q = _store.Customers;
            if (query.Length > 0)
            {
                q = q.Where(c =>
                    c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.Aliases.Any(a => a.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                // 模糊排序：开头命中优先
                q = q.OrderByDescending(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            }
            var ordered = q.OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();

            _lstCustomers.BeginUpdate();
            _lstCustomers.Items.Clear();
            foreach (var c in ordered)
            {
                var pending = PendingOf(c.Id).Sum(e => e.Qty);
                var balance = _store.CreditBalance(c.Id);
                _lstCustomers.Items.Add(new ItemHolder<Customer>(c,
                    c.Name + (pending > 0 ? "　[待补 " + pending + "]" : "") + (balance != 0 ? "　[抵扣 " + balance + "元]" : "")));
            }
            _lstCustomers.EndUpdate();
            if (_current != null)
            {
                var idx = ordered.FindIndex(c => c.Id == _current.Id);
                if (idx >= 0) _lstCustomers.SelectedIndex = idx;
            }
            if (_lstCustomers.SelectedIndex < 0) RefreshDetail();
        }

        private class ItemHolder<T>
        {
            public readonly T Value;
            private readonly string _text;

            public ItemHolder(T value, string text)
            {
                Value = value;
                _text = text;
            }

            public override string ToString() { return _text; }
        }

        private void RefreshDetail()
        {
            _lvPending.Items.Clear();
            _lvHistory.Items.Clear();

            var holder = _lstCustomers.SelectedItem as ItemHolder<Customer>;
            _current = holder != null ? holder.Value : null;

            if (_current == null)
            {
                _lblName.Text = "（未选择客户）";
                _lblPending.Text = "";
                _lblBalance.Text = "";
                return;
            }

            _lblName.Text = _current.Name;

            var byType = PendingByType(_current.Id);
            _lblPending.Text = "待补盘：" + (byType.Count == 0
                ? "无 ✓"
                : string.Join("；", byType.Select(kv => DiscTypes.DisplayName(kv.Key) + " " + kv.Value + " 张")));

            var balance = _store.CreditBalance(_current.Id);
            _lblBalance.Text = "抵扣余额：" + balance + " 元";

            foreach (var e in PendingOf(_current.Id).OrderBy(e => e.CreatedAt))
            {
                var it = new ListViewItem(new[]
                {
                    e.Ref != null && e.Ref.StartsWith("bupan|") ? e.Ref.Substring(6) : "(手工)",
                    DiscTypes.DisplayName(e.DiscType),
                    e.Qty + "",
                    e.CreatedAt.ToString("MM-dd"),
                    (e.Note ?? "") + "（" + (e.Source ?? "") + "）"
                });
                it.Tag = e;
                it.Checked = e.Processed;
                _lvPending.Items.Add(it);
            }

            foreach (var e in _store.Entries
                .Where(x => x.CustomerId == _current.Id)
                .OrderByDescending(x => x.CreatedAt))
            {
                string type, content;
                switch (e.Kind)
                {
                    case EntryKind.BupanOrder:
                    case EntryKind.ManualBupan:
                        type = e.Processed ? "补盘·已发" : "补盘·待发";
                        content = DiscTypes.DisplayName(e.DiscType) + " " + e.Qty + " 张";
                        break;
                    case EntryKind.RefundCredit:
                        type = "退款入账";
                        content = MoneyText(e.Note, "+" + e.Amount + " 元");
                        break;
                    case EntryKind.ManualCredit:
                        type = "手工入账";
                        content = MoneyText(e.Note, "+" + e.Amount + " 元");
                        break;
                    case EntryKind.Deduct:
                        type = "进货抵扣";
                        content = MoneyText(e.Note, e.Amount + " 元");
                        break;
                    default:
                        type = "其他"; content = "";
                        break;
                }
                var src = string.IsNullOrEmpty(e.Period)
                    ? (e.Source ?? "")
                    : "[" + e.Period + "] " + (e.Source ?? "");
                var it = new ListViewItem(new[] { e.CreatedAt.ToString("MM-dd HH:mm"), type, content, src });
                it.Tag = e;
                _lvHistory.Items.Add(it);
            }
        }

        /// <summary>历史「内容」列：业务描述（如 4张68元档）在外，金额放括号内；无描述时只显示金额。</summary>
        private static string MoneyText(string note, string money)
        {
            return string.IsNullOrWhiteSpace(note) ? money : note.Trim() + "（" + money + "）";
        }

        private void SetStatus(string text)
        {
            _statusText.Text = text;
        }

        // ---------- 导入 ----------

        private void ImportBupan()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "选择补盘名单（xlsx）",
                Filter = "Excel 文件|*.xlsx;*.xlsm",
                InitialDirectory = SafeDir(_store.LastFolder)
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _store.LastFolder = Path.GetDirectoryName(dlg.FileName);
                try
                {
                    var fileName = Path.GetFileName(dlg.FileName);
                    var parsed = BupanExcelParser.Parse(dlg.FileName);
                    string period;
                    if (!ConfirmPeriod(fileName, parsed.SuggestedPeriod, "补盘名单", out period))
                    {
                        SetStatus("已取消导入（未确认账期），未写入任何数据。");
                        return;
                    }
                    CommitImport(parsed, "补盘名单 " + fileName, period);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导入失败：" + ex.Message, "生化盘插件", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportRefund()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "选择退款名单（txt）",
                Filter = "文本文件|*.txt;*.csv",
                InitialDirectory = SafeDir(_store.LastFolder)
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _store.LastFolder = Path.GetDirectoryName(dlg.FileName);
                try
                {
                    var fileName = Path.GetFileName(dlg.FileName);
                    var parsed = RefundTextParser.Parse(File.ReadAllLines(dlg.FileName), fileName);
                    string period;
                    if (!ConfirmPeriod(fileName, parsed.SuggestedPeriod, "退款名单", out period))
                    {
                        SetStatus("已取消导入（未确认账期），未写入任何数据。");
                        return;
                    }
                    CommitImport(parsed, "退款名单 " + fileName, period);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导入失败：" + ex.Message, "生化盘插件", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string SafeDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        /// <summary>导入前确认账期；取消则不导入任何数据。</summary>
        private bool ConfirmPeriod(string fileName, string suggested, string label, out string period)
        {
            period = null;
            var guess = suggested ?? Period.FromFileName(fileName, DateTime.Now);
            using (var dlg = new ImportPeriodForm(fileName, guess, label))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                period = dlg.PeriodValue;
                return true;
            }
        }

        /// <summary>导入核心：确认账期 → 客户归属（自动/关联/新建）→ 跨月去重 → 入账。</summary>
        private void CommitImport(ImportResult parsed, string sourceLabel, string period)
        {
            int linked = 0, created = 0, added = 0, skipped = 0;

            foreach (var g in parsed.Groups)
            {
                Customer target = _store.Customers.FirstOrDefault(c => c.Matches(g.RawName));
                if (target == null)
                {
                    // 模糊候选：互相包含
                    var candidates = _store.Customers.Where(c =>
                        c.Name.IndexOf(g.RawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        g.RawName.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        c.Aliases.Any(a => a.IndexOf(g.RawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           g.RawName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

                    using (var dlg = new LinkCustomerForm(g.RawName, candidates))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK) { skipped += g.Entries.Count; continue; }
                        if (dlg.Result != null)
                        {
                            target = dlg.Result;
                        }
                        else
                        {
                            target = new Customer { Id = Guid.NewGuid().ToString("N"), Name = dlg.NewName };
                            _store.Customers.Add(target);
                            created++;
                        }
                    }
                }
                else linked++;

                target.AddAlias(g.RawName);

                foreach (var e in g.Entries)
                {
                    e.CustomerId = target.Id;
                    e.Period = period;
                    e.Ref = Period.WithPeriod(e.Ref, period);
                    if (e.Ref != null && _store.ImportedRefs.Contains(e.Ref)) { skipped++; continue; }
                    if (e.Ref != null) _store.ImportedRefs.Add(e.Ref);
                    _store.Entries.Add(e);
                    added++;
                }
            }

            _store.Save();
            RefreshList();
            RefreshDetail();

            var msg = sourceLabel + "（账期 " + Period.Display(period) + "）：入账 " + added + " 条，自动/关联归属 " + linked + " 组，新建客户 " + created + " 个，跳过 " + skipped + " 条";
            if (parsed.SkippedVs2 > 0) msg += "（含 VS2 综合盘 " + parsed.SkippedVs2 + " 条）";
            if (parsed.UnparsedLines.Count > 0)
                msg += "；⚠ " + parsed.UnparsedLines.Count + " 行无法解析：" + string.Join(" / ", parsed.UnparsedLines.Take(3));
            SetStatus(msg);
            if (parsed.UnparsedLines.Count > 0)
                MessageBox.Show(msg, "导入完成（有告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---------- 操作 ----------

        private void MarkDone()
        {
            if (_current == null) return;
            var checkedItems = _lvPending.Items.Cast<ListViewItem>().Where(i => i.Checked).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("请先在待补清单里勾选要标记的条目。", "生化盘插件");
                return;
            }
            foreach (var it in checkedItems)
            {
                var e = it.Tag as LedgerEntry;
                e.Processed = true;
                e.ProcessedAt = DateTime.Now;
            }
            _store.Save();
            RefreshList();
            RefreshDetail();
            SetStatus("已标记补发 " + checkedItems.Count + " 条");
        }

        private void AddManualBupan()
        {
            if (_current == null) return;
            DiscType type;
            int qty;
            string note;
            if (!PromptBupan(out type, out qty, out note)) return;

            _store.Entries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = _current.Id,
                Kind = EntryKind.ManualBupan,
                DiscType = type,
                Qty = qty,
                Note = note,
                Source = "手工",
                Ref = null
            });
            _store.Save();
            RefreshList();
            RefreshDetail();
            SetStatus("手工新增补盘：" + DiscTypes.DisplayName(type) + " " + qty + " 张");
        }

        private void AddCredit()
        {
            if (_current == null) return;
            int amount;
            string note;
            if (!PromptAmount("手工增加抵扣", out amount, out note)) return;
            _store.Entries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = _current.Id,
                Kind = EntryKind.ManualCredit,
                Amount = amount,
                Note = note,
                Source = "手工"
            });
            _store.Save();
            RefreshList();
            RefreshDetail();
            SetStatus("手工增加抵扣 +" + amount + " 元");
        }

        private void DeductCredit()
        {
            if (_current == null) return;
            var balance = _store.CreditBalance(_current.Id);
            int amount;
            string note;
            if (!PromptAmount("进货抵扣（扣减余额，当前 " + balance + " 元）", out amount, out note)) return;
            _store.Entries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = _current.Id,
                Kind = EntryKind.Deduct,
                Amount = -amount,
                Note = note,
                Source = "手工"
            });
            _store.Save();
            RefreshList();
            RefreshDetail();
            SetStatus("进货抵扣 -" + amount + " 元");
        }

        private void UndoSelected()
        {
            if (_current == null) return;
            if (_lvHistory.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先在下方历史里选中要撤销的记录。", "生化盘插件");
                return;
            }
            var e = _lvHistory.SelectedItems[0].Tag as LedgerEntry;
            var what = "";
            if (e.Kind == EntryKind.RefundCredit || e.Kind == EntryKind.ManualCredit || e.Kind == EntryKind.Deduct)
            {
                _store.Entries.Remove(e);
                what = "已撤销一笔金额记录";
            }
            else if (e.Kind == EntryKind.ManualBupan && !e.Processed)
            {
                _store.Entries.Remove(e);
                what = "已删除手工补盘条目";
            }
            else if (e.Kind == EntryKind.ManualBupan && e.Processed)
            {
                e.Processed = false;
                e.ProcessedAt = null;
                what = "已撤销“已发”标记";
            }
            else
            {
                e.Processed = false;
                e.ProcessedAt = null;
                what = "已撤销补发标记";
            }
            _store.Save();
            RefreshList();
            RefreshDetail();
            SetStatus(what);
        }

        // ---------- 输入对话框 ----------

        private bool PromptBupan(out DiscType type, out int qty, out string note)
        {
            type = DiscType.QuanNeng; qty = 1; note = "";
            using (var f = new Form
            {
                Text = "手工新增补盘",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(320, 170),
                MaximizeBox = false
            })
            {
                var lblT = new Label { Text = "盘型：", AutoSize = true, Location = new Point(15, 18) };
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(70, 14), Width = 220 };
                cmb.Items.AddRange(new object[] { DiscTypes.DisplayName(DiscType.QuanNeng), DiscTypes.DisplayName(DiscType.ShuQian), DiscTypes.DisplayName(DiscType.YiChong) });
                cmb.SelectedIndex = 0;

                var lblQ = new Label { Text = "张数：", AutoSize = true, Location = new Point(15, 52) };
                var num = new NumericUpDown { Location = new Point(70, 48), Width = 100, Minimum = 1, Maximum = 999 };

                var lblN = new Label { Text = "备注：", AutoSize = true, Location = new Point(15, 86) };
                var txt = new TextBox { Location = new Point(70, 82), Width = 220 };

                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(130, 125), Width = 75 };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(215, 125), Width = 75 };
                f.Controls.AddRange(new Control[] { lblT, cmb, lblQ, num, lblN, txt, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;

                if (f.ShowDialog(this) != DialogResult.OK) return false;
                type = cmb.SelectedIndex == 1 ? DiscType.ShuQian : cmb.SelectedIndex == 2 ? DiscType.YiChong : DiscType.QuanNeng;
                qty = (int)num.Value;
                note = txt.Text.Trim();
                return true;
            }
        }

        private bool PromptAmount(string title, out int amount, out string note)
        {
            amount = 0; note = "";
            using (var f = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(320, 140),
                MaximizeBox = false
            })
            {
                var lblA = new Label { Text = "金额（元）：", AutoSize = true, Location = new Point(15, 18) };
                var num = new NumericUpDown { Location = new Point(90, 14), Width = 120, Minimum = 1, Maximum = 100000 };
                var lblN = new Label { Text = "备注：", AutoSize = true, Location = new Point(15, 52) };
                var txt = new TextBox { Location = new Point(90, 48), Width = 200 };
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(130, 95), Width = 75 };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(215, 95), Width = 75 };
                f.Controls.AddRange(new Control[] { lblA, num, lblN, txt, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;

                if (f.ShowDialog(this) != DialogResult.OK) return false;
                amount = (int)num.Value;
                note = txt.Text.Trim();
                return true;
            }
        }
    }
}
