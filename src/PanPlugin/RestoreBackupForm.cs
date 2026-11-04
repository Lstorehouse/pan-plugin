using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace PanPlugin
{
    /// <summary>从备份列表里选一份恢复；显示备份内容概要，恢复前二次确认。</summary>
    public class RestoreBackupForm : Form
    {
        public string SelectedPath;

        private readonly List<string> _paths = new List<string>();
        private readonly ListBox _list = new ListBox();
        private readonly Label _detail = new Label();

        public RestoreBackupForm(IEnumerable<string> backups)
        {
            Text = "从备份恢复";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(580, 420);

            var lbl = new Label
            {
                Text = "选择要恢复的备份（最新在前）。恢复会覆盖当前全部数据，\n"
                     + "程序会先把当前账本另存为 before-restore-*.json。",
                AutoSize = true,
                MaximumSize = new Size(550, 0),
                Location = new Point(14, 12),
                ForeColor = Color.DimGray
            };
            _list.Location = new Point(14, 62);
            _list.Size = new Size(550, 210);
            _list.SelectedIndexChanged += (s, e) => ShowDetail();

            _detail.Location = new Point(14, 284);
            _detail.Size = new Size(550, 54);
            _detail.ForeColor = Color.DimGray;

            var ok = new Button { Text = "恢复所选备份", Location = new Point(330, 366), Width = 130 };
            ok.Click += (s, e) => ConfirmRestore();
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(470, 366), Width = 94 };
            Controls.AddRange(new Control[] { lbl, _list, _detail, ok, cancel });
            CancelButton = cancel;

            foreach (var p in backups) _paths.Add(p);
            foreach (var p in _paths)
            {
                var fi = new FileInfo(p);
                _list.Items.Add(fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    + "   " + fi.Name + "   " + (fi.Length / 1024 + 1) + " KB");
            }
            if (_list.Items.Count == 0) _list.Items.Add("（没有可用备份）");
            else _list.SelectedIndex = 0;
            ShowDetail();
        }

        private void ShowDetail()
        {
            if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _paths.Count)
            {
                _detail.Text = "";
                return;
            }
            var p = _paths[_list.SelectedIndex];
            try
            {
                var o = JObject.Parse(File.ReadAllText(p));
                int customers = o["Customers"] == null ? 0 : ((JArray)o["Customers"]).Count;
                int entries = o["Entries"] == null ? 0 : ((JArray)o["Entries"]).Count;
                _detail.Text = "内容：客户 " + customers + " 个 / 账目 " + entries + " 条\n" + p;
            }
            catch
            {
                _detail.Text = "（无法读取该备份的内容概要）\n" + p;
            }
        }

        private void ConfirmRestore()
        {
            if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _paths.Count)
            {
                MessageBox.Show(this, "请先选择一份备份。", "生化盘插件");
                return;
            }
            SelectedPath = _paths[_list.SelectedIndex];
            if (MessageBox.Show(this,
                    "确定用这份备份覆盖当前数据吗？\n\n" + Path.GetFileName(SelectedPath)
                    + "\n\n当前账本会先自动另存一份，之后仍可通过「恢复备份…」找回。",
                    "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
