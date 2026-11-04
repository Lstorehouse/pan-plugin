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
        private Button _btnBackup;
        private Button _btnArchive;
        private Button _btnAliases;
        private CheckBox _chkShowArchived;
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
            FormClosing += MainForm_FormClosing;
        }

        // ---------- 备份 ----------

        /// <summary>关闭时询问是否备份；备份失败可放弃关闭，避免数据没备份就退出。</summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var ans = MessageBox.Show(this,
                "关闭前是否备份数据？\n\n备份位置：" + Store.BackupDir()
                + "\n最多保留 3 份，超出自动删除最早的备份。",
                "生化盘插件", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (ans != DialogResult.Yes) return;

            string path, err;
            if (DoBackup(out path, out err)) return;

            var keepClosing = MessageBox.Show(this,
                "备份失败：" + err + "\n\n仍要关闭软件吗？",
                "生化盘插件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (keepClosing != DialogResult.Yes) e.Cancel = true;
        }

        private void BackupNow()
        {
            string path, err;
            if (!DoBackup(out path, out err))
            {
                MessageBox.Show(this, "备份失败：" + err, "生化盘插件",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SetStatus("已备份：" + path);
            MessageBox.Show(this,
                "备份完成：\n" + path + "\n\n最多保留 3 份，超出会自动删除最早的备份。",
                "生化盘插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>先落盘再复制，保证备份包含本次会话的全部改动（含旧账本迁移结果）。</summary>
        private bool DoBackup(out string path, out string err)
        {
            path = null;
            err = null;
            try
            {
                _store.Save();
                path = _store.Backup(3);
                return true;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
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
            _btnBackup = new Button { Text = "立即备份…", Width = 100 };
            _btnBackup.Click += (s, e) => BackupNow();
            _btnArchive = new Button { Text = "归档所选客户…", Width = 120, Enabled = false };
            _btnArchive.Click += (s, e) => ToggleArchive();
            _chkShowArchived = new CheckBox { Text = "显示已归档", AutoSize = true, Margin = new Padding(8, 10, 3, 3) };
            _chkShowArchived.CheckedChanged += (s, e) => RefreshList();

            top.Controls.Add(new Label { Text = "搜索客户：", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            top.Controls.Add(_txtSearch);
            top.Controls.Add(_btnImportBupan);
            top.Controls.Add(_btnImportRefund);
            top.Controls.Add(_btnBackup);
            top.Controls.Add(_btnArchive);
            top.Controls.Add(_chkShowArchived);

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
            _btnAliases = new Button { Text = "管理别名…", Width = 100, Height = 24, Enabled = false, Margin = new Padding(6, 10, 0, 0) };
            _btnAliases.Click += (s, e) => ManageAliases();
            var namePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            namePanel.Controls.Add(_lblName);
            namePanel.Controls.Add(_btnAliases);
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

            right.Controls.Add(namePanel, 0, 0);
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
            // 归档客户默认从列表和搜索里隐藏；勾上「显示已归档」才可见（用于恢复/核对）。
            IEnumerable<Customer> q = (_chkShowArchived != null && _chkShowArchived.Checked)
                ? _store.Customers
                : _store.Customers.Where(c => !c.Archived);
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
                    c.Name + (c.Archived ? "　（已归档）" : "")
                           + (pending > 0 ? "　[待补 " + pending + "]" : "")
                           + (balance != 0 ? "　[抵扣 " + balance + "元]" : "")));
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

            if (_btnArchive != null)
            {
                _btnArchive.Enabled = _current != null;
                _btnArchive.Text = (_current != null && _current.Archived) ? "恢复该客户" : "归档所选客户…";
            }
            if (_btnAliases != null) _btnAliases.Enabled = _current != null;

            if (_current == null)
            {
                _lblName.Text = "（未选择客户）";
                _lblPending.Text = "";
                _lblBalance.Text = "";
                return;
            }

            _lblName.Text = _current.Name + (_current.Archived ? "　（已归档）" : "");

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
            int linked = 0, created = 0, added = 0, skipped = 0, revived = 0;

            foreach (var g in parsed.Groups)
            {
                Customer target = _store.Customers.FirstOrDefault(c => c.Matches(g.RawName));
                if (target == null)
                {
                    // 模糊候选：互相包含，且排除用户已明确解除过关联的客户
                    var candidates = _store.Customers.Where(c =>
                        !_store.ExcludedLinks.Contains(Store.LinkKey(g.RawName, c.Id)) &&
                        (c.Name.IndexOf(g.RawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         g.RawName.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         c.Aliases.Any(a => a.IndexOf(g.RawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            g.RawName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();

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

                bool revivedThisGroup = false;
                foreach (var e in g.Entries)
                {
                    e.CustomerId = target.Id;
                    e.Period = period;
                    e.Ref = Period.WithPeriod(e.Ref, period);
                    if (e.Ref != null && _store.ImportedRefs.Contains(e.Ref)) { skipped++; continue; }
                    if (e.Ref != null) _store.ImportedRefs.Add(e.Ref);
                    _store.Entries.Add(e);
                    added++;
                    // 归档客户下月又有补盘 → 自动重新出现（只有真正入账才算，重复导入不算）
                    if (!revivedThisGroup && target.Archived &&
                        (e.Kind == EntryKind.BupanOrder || e.Kind == EntryKind.ManualBupan))
                    {
                        ReviveIfArchived(target);
                        revivedThisGroup = true;
                        revived++;
                    }
                }
            }

            _store.Save();
            RefreshList();
            RefreshDetail();

            var msg = sourceLabel + "（账期 " + Period.Display(period) + "）：入账 " + added + " 条，自动/关联归属 " + linked + " 组，新建客户 " + created + " 个，跳过 " + skipped + " 条";
            if (parsed.SkippedVs2 > 0) msg += "（含 VS2 综合盘 " + parsed.SkippedVs2 + " 条）";
            if (revived > 0) msg += "；↻ " + revived + " 个归档客户因新补盘已自动恢复显示";
            if (parsed.UnparsedLines.Count > 0)
                msg += "；⚠ " + parsed.UnparsedLines.Count + " 行无法解析：" + string.Join(" / ", parsed.UnparsedLines.Take(3));
            SetStatus(msg);
            if (parsed.UnparsedLines.Count > 0)
                MessageBox.Show(msg, "导入完成（有告警）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---------- 操作 ----------

        /// <summary>打开别名管理；取消不改动，确定后落盘并刷新。</summary>
        private void ManageAliases()
        {
            if (_current == null)
            {
                MessageBox.Show(this, "请先在左侧列表选中一个客户。", "生化盘插件");
                return;
            }

            using (var dlg = new AliasManagerForm(_current, _store))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.Changed) return;
                var name = _current.Name;
                _store.Save();
                RefreshList();
                RefreshDetail();
                SetStatus("已更新「" + name + "」的名称与别名；被移除的别名不会再自动绑回该客户。");
            }
        }

        /// <summary>
        /// 归档/恢复。归档只加标记，档案、别名、账目全部保留；
        /// 下月导入该客户的补盘时会自动解除归档（见 CommitImport）。
        /// </summary>
        private void ToggleArchive()
        {
            if (_current == null)
            {
                MessageBox.Show(this, "请先在左侧列表选中一个客户。", "生化盘插件");
                return;
            }

            var name = _current.Name;
            bool archiving = !_current.Archived;
            if (archiving)
            {
                int pending = PendingOf(_current.Id).Sum(e => e.Qty);
                int balance = _store.CreditBalance(_current.Id);
                var warn = "";
                if (pending > 0) warn += "还有 " + pending + " 张待补盘；";
                if (balance != 0) warn += "抵扣余额还有 " + balance + " 元；";
                var text = "确定归档客户「" + name + "」吗？\n\n"
                    + (warn.Length > 0 ? "⚠ " + warn + "\n\n" : "")
                    + "归档后默认不再显示、也不参与搜索，但历史数据完整保留；\n"
                    + "下月导入该客户的补盘时会自动重新出现。";
                if (MessageBox.Show(this, text, "生化盘插件",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                _current.Archived = true;
                _current.ArchivedAt = DateTime.Now;
            }
            else
            {
                _current.Archived = false;
                _current.ArchivedAt = null;
            }

            _store.Save();
            RefreshList();      // 归档后该客户会离开默认列表，_current 可能变 null
            RefreshDetail();
            SetStatus(archiving ? "已归档：" + name : "已恢复显示：" + name);
        }

        /// <summary>归档客户出现新的补盘条目时自动恢复显示。</summary>
        private static void ReviveIfArchived(Customer c)
        {
            if (c == null || !c.Archived) return;
            c.Archived = false;
            c.ArchivedAt = null;
        }

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
            ReviveIfArchived(_current);   // 归档客户手工新增补盘也会重新出现
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
