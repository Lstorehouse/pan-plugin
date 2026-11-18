using System;
using System.Windows.Forms;

namespace PanPlugin
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Store.ErrorReporter = msg => MessageBox.Show(msg, "生化盘插件",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Application.Run(new MainForm());
        }
    }
}
