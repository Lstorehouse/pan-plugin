using System;
using System.Drawing;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>
    /// 导入前确认账期。显示从文件名识别出的月份，允许修改；
    /// 取消则完全不导入。账期相同才判定为重复导入。
    /// 桌面专属文件，不链接到移动端。
    /// </summary>
    public class ImportPeriodForm : Form
    {
        public string PeriodValue;
        private readonly TextBox _txt = new TextBox();

        public ImportPeriodForm(string fileName, string suggested, string kindLabel)
        {
            Text = "确认导入账期";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(450, 205);

            var lbl1 = new Label
            {
                Text = kindLabel + "：" + fileName,
                AutoSize = true,
                Location = new Point(16, 18)
            };
            var lbl2 = new Label
            {
                Text = "这份名单属于哪个月？账期相同才会判定为重复导入。",
                AutoSize = true,
                Location = new Point(16, 50)
            };
            var lbl3 = new Label { Text = "账期（yyyy-MM）：", AutoSize = true, Location = new Point(16, 88) };
            _txt.Text = suggested;
            _txt.Location = new Point(132, 84);
            _txt.Width = 120;
            var lblHint = new Label
            {
                Text = "文件名未写月份时按数据日期／今天推断，请核对后再导入。",
                AutoSize = true,
                Location = new Point(16, 122),
                ForeColor = Theme.TextSecond
            };
            var ok = new Button
            {
                Text = "确认导入", DialogResult = DialogResult.OK,
                Location = new Point(246, 162), Width = 92
            };
            var cancel = new Button
            {
                Text = "取消", DialogResult = DialogResult.Cancel,
                Location = new Point(346, 162), Width = 88
            };

            Controls.AddRange(new Control[] { lbl1, lbl2, lbl3, _txt, lblHint, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            Theme.FinishDialog(this);

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                var p = Period.Normalize(_txt.Text);
                if (p == null)
                {
                    MessageBox.Show("账期格式应为 yyyy-MM，例如 2026-10。", "生化盘插件");
                    e.Cancel = true;
                    return;
                }
                PeriodValue = p;
            };
        }
    }
}
