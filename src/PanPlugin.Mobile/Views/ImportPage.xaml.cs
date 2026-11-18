using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public partial class ImportPage : ContentPage
    {
        private static readonly FilePickerFileType XlsxType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "*/*" } }
            });

        private static readonly FilePickerFileType TxtType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "text/plain", "*/*" } }
            });

        public ImportPage()
        {
            InitializeComponent();
        }

        private async void OnPickBupan(object sender, EventArgs e)
        {
            await ImportAsync(true);
        }

        private async void OnPickRefund(object sender, EventArgs e)
        {
            await ImportAsync(false);
        }

        private async System.Threading.Tasks.Task ImportAsync(bool isBupan)
        {
            PickBupanButton.IsEnabled = false;
            PickRefundButton.IsEnabled = false;
            try
            {
                var file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = isBupan ? "选择补盘名单（xlsx）" : "选择退款名单（txt）",
                    FileTypes = isBupan ? XlsxType : TxtType
                });
                if (file == null) return;

                StatusLabel.Text = "正在解析：" + file.FileName;
                ImportResult parsed;
                using (var stream = await file.OpenReadAsync())
                {
                    parsed = isBupan
                        ? BupanExcelParser.Parse(stream, file.FileName)
                        : RefundTextParser.Parse(stream, file.FileName);
                }

                var period = await AskPeriodAsync(parsed.SuggestedPeriod);
                if (period == null) return;

                // 先把需要用户决定的客户归属一次性问完，再把结果交给共享导入规则。
                var resolutions = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in parsed.Groups)
                {
                    if (resolutions.ContainsKey(g.RawName)) continue;
                    if (StoreService.Current.Customers.Any(c => c.Matches(g.RawName))) continue;
                    resolutions[g.RawName] = await ResolveCustomerAsync(g.RawName);
                }

                var outcome = ImportService.Commit(
                    StoreService.Current,
                    parsed,
                    period,
                    (name, candidates) =>
                    {
                        Customer c;
                        return resolutions.TryGetValue(name, out c) ? c : null;
                    });
                StoreService.Save();

                var msg = outcome.Summary(file.FileName, parsed, period);
                StatusLabel.Text = msg;
                await DisplayAlert("导入完成", msg, "知道了");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "导入失败：" + ex.Message;
                await DisplayAlert("导入失败", ex.Message, "知道了");
            }
            finally
            {
                PickBupanButton.IsEnabled = true;
                PickRefundButton.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task<string> AskPeriodAsync(string suggested)
        {
            var initial = Period.Normalize(suggested) ?? suggested ?? DateTime.Now.ToString("yyyy-MM");
            var value = await DisplayPromptAsync(
                "确认导入账期",
                "这份名单属于哪个月？账期相同才会判定为重复导入。",
                accept: "确认导入",
                cancel: "取消",
                placeholder: "yyyy-MM",
                initialValue: initial);
            if (value == null) return null;

            var period = Period.Normalize(value);
            while (period == null)
            {
                value = await DisplayPromptAsync(
                    "账期格式不正确",
                    "请输入 yyyy-MM，例如 2026-10。",
                    accept: "确认导入",
                    cancel: "取消",
                    placeholder: "yyyy-MM",
                    initialValue: initial);
                if (value == null) return null;
                period = Period.Normalize(value);
            }
            return period;
        }

        private async System.Threading.Tasks.Task<Customer> ResolveCustomerAsync(string rawName)
        {
            var store = StoreService.Current;
            var candidates = ImportService.CandidatesFor(store, rawName);
            var choices = candidates.Take(5).Select(c => "关联：" + c.Name).ToList();
            choices.Add("新建客户");
            choices.Add("跳过本组");

            var action = await DisplayActionSheet(
                "客户归属：" + rawName,
                "取消导入",
                null,
                choices.ToArray());
            if (action == null || action == "跳过本组") return null;

            if (action == "新建客户")
            {
                var name = await DisplayPromptAsync(
                    "新建客户",
                    "请输入客户名称：" + rawName,
                    accept: "创建",
                    cancel: "取消",
                    initialValue: rawName);
                if (string.IsNullOrWhiteSpace(name)) return null;
                return new Customer { Id = Guid.NewGuid().ToString("N"), Name = name.Trim() };
            }

            var prefix = "关联：";
            if (!action.StartsWith(prefix, StringComparison.Ordinal)) return null;
            var pickedName = action.Substring(prefix.Length);
            return candidates.FirstOrDefault(c => c.Name == pickedName);
        }
    }
}
