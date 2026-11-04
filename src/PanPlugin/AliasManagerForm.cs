using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>
    /// 管理客户主名与别名：改名、重命名别名、增删别名、把误绑的别名转移给另一个客户。
    /// 所有改动先在对话框内暂存，点「确定」才写入；取消不留痕迹。
    /// 从本客户移除/改掉的名字会记入 Store.ExcludedLinks，防止下次导入又被模糊候选绑回来。
    /// </summary>
    public class AliasManagerForm : Form
    {
        public bool Changed;

        private readonly Store _store;
        private readonly Customer _customer;
        private readonly List<Customer> _others;
        private readonly List<string> _aliases = new List<string>();
        private readonly List<string> _removed = new List<string>();
        private readonly List<KeyValuePair<string, string>> _transfers =
            new List<KeyValuePair<string, string>>();   // 别名 → 目标客户 Id

        private readonly TextBox _txtName = new TextBox();
        private readonly ListBox _lst = new ListBox();
        private readonly TextBox _txtEdit = new TextBox();
        private readonly TextBox _txtNew = new TextBox();
        private readonly ComboBox _cmb = new ComboBox();

        public AliasManagerForm(Customer customer, Store store)
        {
            _customer = customer;
            _store = store;
            if (customer.Aliases != null) _aliases.AddRange(customer.Aliases);
            _others = store.Customers.Where(c => c.Id != customer.Id)
                                     .OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();

            Text = "管理客户名称与别名";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 452);

            var lblName = new Label { Text = "客户主名：", AutoSize = true, Location = new Point(14, 18) };
            _txtName.Text = customer.Name;
            _txtName.Location = new Point(100, 14);
            _txtName.Width = 410;

            var lblList = new Label { Text = "别名（先选中，再改名／删除／转移）：", AutoSize = true, Location = new Point(14, 52) };
            _lst.Location = new Point(14, 76);
            _lst.Size = new Size(508, 140);
            _lst.SelectedIndexChanged += (s, e) =>
            {
                _txtEdit.Text = (_lst.SelectedIndex >= 0 && _lst.SelectedIndex < _aliases.Count)
                    ? _aliases[_lst.SelectedIndex] : "";
            };

            var btnDel = new Button { Text = "删除所选别名", Location = new Point(14, 222), Width = 122 };
            btnDel.Click += (s, e) => DeleteSelected();

            _cmb.Location = new Point(144, 222);
            _cmb.Width = 224;
            _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var c in _others)
                _cmb.Items.Add(c.Name + (c.Archived ? "　（已归档）" : ""));
            if (_cmb.Items.Count > 0) _cmb.SelectedIndex = 0;

            var btnMove = new Button { Text = "转移给该客户", Location = new Point(376, 222), Width = 146 };
            btnMove.Click += (s, e) => MoveSelected();

            var lblEdit = new Label { Text = "改名为：", AutoSize = true, Location = new Point(14, 262) };
            _txtEdit.Location = new Point(76, 258);
            _txtEdit.Width = 254;
            var btnRename = new Button { Text = "应用改名", Location = new Point(338, 257), Width = 100 };
            btnRename.Click += (s, e) => RenameSelected();

            var lblNew = new Label { Text = "新增别名：", AutoSize = true, Location = new Point(14, 296) };
            _txtNew.Location = new Point(76, 292);
            _txtNew.Width = 254;
            var btnAdd = new Button { Text = "添加", Location = new Point(338, 291), Width = 100 };
            btnAdd.Click += (s, e) => AddAlias();

            var hint = new Label
            {
                Text = "改名／移除后，旧名字下次导入会重新询问归属，不会再静默绑回本客户。\n"
                     + "若另一个客户其实是同一家（例如「黄强」独立成户），请用主界面的「合并到另一客户…」。",
                AutoSize = true,
                MaximumSize = new Size(508, 0),
                Location = new Point(14, 332),
                ForeColor = Color.DimGray
            };

            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(332, 408), Width = 92 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(432, 408), Width = 92 };

            Controls.AddRange(new Control[] { lblName, _txtName, lblList, _lst, btnDel, _cmb, btnMove,
                                             lblEdit, _txtEdit, btnRename, lblNew, _txtNew, btnAdd,
                                             hint, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (_txtName.Text.Trim().Length == 0)
                {
                    MessageBox.Show(this, "客户主名不能为空。", "生化盘插件");
                    e.Cancel = true;
                    return;
                }
                Apply();
            };

            ReloadList();
        }

        private void ReloadList()
        {
            int keep = _lst.SelectedIndex;
            _lst.Items.Clear();
            foreach (var a in _aliases) _lst.Items.Add(a);
            if (keep >= 0 && keep < _lst.Items.Count) _lst.SelectedIndex = keep;
            else if (_lst.Items.Count > 0) _lst.SelectedIndex = 0;
        }

        private void DeleteSelected()
        {
            if (_lst.SelectedIndex < 0) return;
            var a = _aliases[_lst.SelectedIndex];
            if (!_removed.Contains(a, StringComparer.OrdinalIgnoreCase)) _removed.Add(a);
            _aliases.RemoveAt(_lst.SelectedIndex);
            ReloadList();
        }

        /// <summary>直接修改别名文本：旧名字记为已解除关联，新名字成为别名。</summary>
        private void RenameSelected()
        {
            if (_lst.SelectedIndex < 0 || _lst.SelectedIndex >= _aliases.Count) return;
            int idx = _lst.SelectedIndex;
            var oldName = _aliases[idx];
            var newName = _txtEdit.Text.Trim();
            if (newName.Length == 0)
            {
                MessageBox.Show(this, "请输入新的别名。", "生化盘插件");
                return;
            }
            if (string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(newName, _txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "这个名字已经是客户主名了。", "生化盘插件");
                return;
            }
            if (_aliases.Any(a => string.Equals(a, newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "该别名已存在。", "生化盘插件");
                return;
            }

            _aliases[idx] = newName;
            if (!_removed.Contains(oldName, StringComparer.OrdinalIgnoreCase)) _removed.Add(oldName);
            _removed.RemoveAll(x => string.Equals(x, newName, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < _transfers.Count; i++)
                if (string.Equals(_transfers[i].Key, oldName, StringComparison.OrdinalIgnoreCase))
                    _transfers[i] = new KeyValuePair<string, string>(newName, _transfers[i].Value);

            ReloadList();
            _txtEdit.Text = "";
            SetStatusHint("已暂存改名：「" + oldName + "」→「" + newName + "」");
        }

        private void MoveSelected()
        {
            if (_lst.SelectedIndex < 0) return;
            if (_cmb.SelectedIndex < 0 || _cmb.SelectedIndex >= _others.Count)
            {
                MessageBox.Show(this, "请先在下拉框里选择要转移到的客户。", "生化盘插件");
                return;
            }
            var alias = _aliases[_lst.SelectedIndex];
            var target = _others[_cmb.SelectedIndex];
            _aliases.RemoveAt(_lst.SelectedIndex);
            if (!_removed.Contains(alias, StringComparer.OrdinalIgnoreCase)) _removed.Add(alias);
            _transfers.Add(new KeyValuePair<string, string>(alias, target.Id));
            ReloadList();
            SetStatusHint("已暂存：把「" + alias + "」转移给「" + target.Name + "」，点确定后生效。");
        }

        private void AddAlias()
        {
            var a = _txtNew.Text.Trim();
            if (a.Length == 0) return;
            if (string.Equals(a, _txtName.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "这个名字已经是客户主名了。", "生化盘插件");
                return;
            }
            if (_aliases.Contains(a, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "该别名已存在。", "生化盘插件");
                return;
            }
            _aliases.Add(a);
            _removed.RemoveAll(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));
            _txtNew.Text = "";
            ReloadList();
        }

        private void SetStatusHint(string text) { Text = "管理客户名称与别名 — " + text; }

        /// <summary>确定时一次性写回；改主名时旧主名自动保留为别名，历史导入仍能命中。</summary>
        private void Apply()
        {
            var newName = _txtName.Text.Trim();
            var oldName = _customer.Name;
            bool renamed = !string.Equals(newName, oldName, StringComparison.Ordinal);

            _customer.Aliases.Clear();
            foreach (var a in _aliases)
                if (!_customer.Aliases.Contains(a, StringComparer.OrdinalIgnoreCase))
                    _customer.Aliases.Add(a);

            if (renamed)
            {
                _customer.Name = newName;
                if (!_removed.Any(x => string.Equals(x, oldName, StringComparison.OrdinalIgnoreCase)))
                    _customer.AddAlias(oldName);
            }

            // 从本客户解除的名字：加入排除名单，下次导入不再模糊推荐回本客户
            foreach (var a in _removed)
                _store.ExcludedLinks.Add(Store.LinkKey(a, _customer.Id));

            // 转移目标：加上别名，并清掉该目标上可能存在的排除
            foreach (var t in _transfers)
            {
                var target = _store.Customers.FirstOrDefault(c => c.Id == t.Value);
                if (target == null) continue;
                target.AddAlias(t.Key);
                _store.ExcludedLinks.Remove(Store.LinkKey(t.Key, target.Id));
                _customer.Aliases.RemoveAll(a => string.Equals(a, t.Key, StringComparison.OrdinalIgnoreCase));
            }

            Changed = true;
        }
    }
}
