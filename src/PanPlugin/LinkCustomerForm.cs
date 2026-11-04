using System;
using System.Drawing;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>
    /// 搜不到客户时的"关联 or 新建"对话框（GLOSSARY：关联客户）。
    /// Result = 已有客户（关联）；Name == null 且新建选中 → 新建；取消 → null。
    /// </summary>
    public class LinkCustomerForm : Form
    {
        public Customer Result;
        public bool CreateNew;

        private readonly ListBox _list = new ListBox();
        private readonly RadioButton _rbLink = new RadioButton();
        private readonly RadioButton _rbNew = new RadioButton();
        private readonly TextBox _txtNewName = new TextBox();

        public LinkCustomerForm(string rawName, System.Collections.Generic.List<Customer> candidates)
        {
            Text = "未找到客户：" + rawName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 380);

            var lbl = new Label
            {
                Text = "搜索不到“" + rawName + "”。请选择处理方式：",
                AutoSize = true,
                Location = new Point(12, 12)
            };

            _rbLink.Text = "关联到已有客户（该名字将存为别名，以后自动识别）";
            _rbLink.AutoSize = true;
            _rbLink.Location = new Point(12, 40);
            _rbLink.CheckedChanged += (s, e) => { _list.Enabled = _rbLink.Checked; _txtNewName.Enabled = _rbNew.Checked; };

            _list.Location = new Point(30, 65);
            _list.Size = new Size(410, 170);
            foreach (var c in candidates)
                _list.Items.Add(c.Name + (c.Aliases.Count > 0 ? "　（别名：" + string.Join("、", c.Aliases) + "）" : ""));
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;

            _rbNew.Text = "新建客户";
            _rbNew.AutoSize = true;
            _rbNew.Location = new Point(12, 250);

            var lblNew = new Label { Text = "客户名：", AutoSize = true, Location = new Point(30, 280) };
            _txtNewName.Location = new Point(90, 276);
            _txtNewName.Width = 340;
            _txtNewName.Text = rawName;
            _txtNewName.Enabled = false;

            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(270, 330), Width = 80 };
            var cancel = new Button { Text = "跳过此条", DialogResult = DialogResult.Cancel, Location = new Point(360, 330), Width = 80 };

            Controls.AddRange(new Control[] { lbl, _rbLink, _list, _rbNew, lblNew, _txtNewName, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            if (candidates.Count == 0)
            {
                _rbLink.Enabled = false;
                _rbNew.Checked = true;
            }
            else
            {
                _rbLink.Checked = true;
            }

            FormClosing += (s, e) =>
            {
                if (DialogResult == DialogResult.OK)
                {
                    if (_rbLink.Checked)
                    {
                        if (_list.SelectedIndex < 0) { e.Cancel = true; return; }
                        Result = candidates[_list.SelectedIndex];
                        CreateNew = false;
                    }
                    else
                    {
                        var n = _txtNewName.Text.Trim();
                        if (n.Length == 0) { e.Cancel = true; return; }
                        Result = null;
                        CreateNew = true;
                        NewName = n;
                    }
                }
            };
        }

        public string NewName;
    }
}
