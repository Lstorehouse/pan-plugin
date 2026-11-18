using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public partial class CustomerDetailPage : ContentPage
    {
        private readonly Customer _customer;
        private readonly Dictionary<CheckBox, string> _checks = new Dictionary<CheckBox, string>();
        private readonly Dictionary<CheckBox, string> _historyChecks = new Dictionary<CheckBox, string>();

        public CustomerDetailPage(Customer customer)
        {
            InitializeComponent();
            _customer = customer;
            Title = customer.Name;
            Refresh();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Refresh();
        }

        private void Refresh()
        {
            var store = StoreService.Current;
            var live = store.Customers.FirstOrDefault(c => c.Id == _customer.Id);
            var customer = live ?? _customer;

            NameLabel.Text = customer.Archived ? customer.Name + "（已归档）" : customer.Name;
            AliasLabel.Text = customer.Aliases.Count > 0
                ? "别名：" + string.Join("、", customer.Aliases)
                : "无别名";
            var pending = CustomerService.PendingOf(store, customer.Id);
            PendingLabel.Text = CustomerService.PendingQty(store, customer.Id) + " 张";
            BalanceLabel.Text = "¥" + store.CreditBalance(customer.Id);
            ArchiveButton.Text = customer.Archived ? "恢复该客户" : "归档客户";
            MarkDoneButton.IsEnabled = pending.Count > 0;

            _checks.Clear();
            PendingRows.Clear();
            foreach (var e in pending.OrderBy(e => e.CreatedAt))
            {
                var check = new CheckBox { AutomationId = e.Id, VerticalOptions = LayoutOptions.Center };
                _checks.Add(check, e.Id);
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(check);
                row.Children.Add(new Label
                {
                    Text = DiscTypes.DisplayName(e.DiscType) + " " + e.Qty + " 张 · " +
                           (string.IsNullOrEmpty(e.Source) ? "手工" : e.Source),
                    FontSize = 13,
                    TextColor = (Color)Application.Current.Resources["TextPrimaryColor"],
                    VerticalOptions = LayoutOptions.Center
                });
                PendingRows.Children.Add(row);
            }
            if (pending.Count == 0)
            {
                PendingRows.Children.Add(new Label
                {
                    Text = "没有待补记录。",
                    FontSize = 13,
                    TextColor = (Color)Application.Current.Resources["TextSecondaryColor"]
                });
            }

            HistoryRows.Clear();
            _historyChecks.Clear();
            var history = store.Entries.Where(e => e.CustomerId == customer.Id)
                .OrderByDescending(e => e.CreatedAt).ToList();
            foreach (var e in history)
            {
                var check = new CheckBox { AutomationId = e.Id, VerticalOptions = LayoutOptions.Center };
                _historyChecks.Add(check, e.Id);
                var row = new HorizontalStackLayout { Spacing = 6 };
                row.Children.Add(check);
                row.Children.Add(new Label
                {
                    Text = e.CreatedAt.ToString("MM-dd HH:mm") + " · " +
                           CustomerService.HistoryType(e) + " · " +
                           CustomerService.HistoryContent(e) + " · " +
                           (string.IsNullOrEmpty(e.Period) ? "—" : e.Period),
                    FontSize = 12,
                    TextColor = (Color)Application.Current.Resources["TextSecondaryColor"],
                    VerticalOptions = LayoutOptions.Center
                });
                HistoryRows.Children.Add(row);
            }
            if (!history.Any())
            {
                HistoryRows.Children.Add(new Label
                {
                    Text = "暂无账目。",
                    FontSize = 13,
                    TextColor = (Color)Application.Current.Resources["TextSecondaryColor"]
                });
            }
        }

        private async void OnMarkDone(object sender, EventArgs e)
        {
            var store = StoreService.Current;
            int n = 0;
            foreach (var pair in _checks.Where(p => p.Key.IsChecked))
            {
                var entry = store.Entries.FirstOrDefault(x => x.Id == pair.Value);
                if (CustomerService.MarkProcessed(entry)) n++;
            }
            if (n == 0)
            {
                await DisplayAlert("标记补发", "请先勾选要标记的条目。", "知道了");
                return;
            }
            StoreService.Save();
            Refresh();
            await DisplayAlert("标记补发", "已标记 " + n + " 条。", "知道了");
        }

        private async void OnAddBupan(object sender, EventArgs e)
        {
            var action = await DisplayActionSheet("盘型", "取消", null,
                "全能34项", "术前14项", "异宠15项");
            if (action == null) return;
            var type = action == "术前14项" ? DiscType.ShuQian
                : action == "异宠15项" ? DiscType.YiChong : DiscType.QuanNeng;

            var qtyText = await DisplayPromptAsync("手工新增补盘", "张数：", accept: "下一步", cancel: "取消");
            if (qtyText == null) return;
            int qty;
            if (!int.TryParse(qtyText, out qty) || qty <= 0)
            {
                await DisplayAlert("输入无效", "张数必须是正整数。", "知道了");
                return;
            }
            var note = await DisplayPromptAsync("手工新增补盘", "备注（可留空）：", accept: "确定", cancel: "取消");
            if (note == null) return;

            var store = StoreService.Current;
            var entry = CustomerService.NewManualBupan(_customer, type, qty, note);
            if (entry == null) return;
            store.Entries.Add(entry);
            StoreService.Save();
            Refresh();
        }

        private async void OnAddCredit(object sender, EventArgs e)
        {
            await AddAmountAsync(false);
        }

        private async void OnDeduct(object sender, EventArgs e)
        {
            await AddAmountAsync(true);
        }

        private async System.Threading.Tasks.Task AddAmountAsync(bool deduct)
        {
            var title = deduct ? "进货抵扣" : "手工增加抵扣";
            var balance = StoreService.Current.CreditBalance(_customer.Id);
            var text = await DisplayPromptAsync(title,
                "金额（元，当前余额 " + balance + "）：", accept: "下一步", cancel: "取消");
            if (text == null) return;
            int amount;
            if (!int.TryParse(text, out amount) || amount <= 0)
            {
                await DisplayAlert("输入无效", "金额必须是正整数。", "知道了");
                return;
            }
            var note = await DisplayPromptAsync(title, "备注（可留空）：", accept: "确定", cancel: "取消");
            if (note == null) return;

            var store = StoreService.Current;
            var entry = deduct
                ? CustomerService.NewDeduct(_customer, amount, note)
                : CustomerService.NewManualCredit(_customer, amount, note);
            if (entry == null) return;
            store.Entries.Add(entry);
            StoreService.Save();
            Refresh();
        }

        private List<LedgerEntry> SelectedHistoryEntries()
        {
            var store = StoreService.Current;
            return _historyChecks.Where(p => p.Key.IsChecked)
                .Select(p => store.Entries.FirstOrDefault(e => e.Id == p.Value))
                .Where(e => e != null)
                .ToList();
        }

        private async void OnUndo(object sender, EventArgs e)
        {
            var selected = SelectedHistoryEntries();
            if (selected.Count == 0)
            {
                await DisplayAlert("撤销账目", "请先勾选要撤销的历史记录。", "知道了");
                return;
            }
            var confirm = await DisplayAlert("撤销账目",
                "确定撤销选中的 " + selected.Count + " 条记录吗？\n\n金额记录会整条删除；补发标记会退回待发。",
                "撤销", "取消");
            if (!confirm) return;

            var store = StoreService.Current;
            int n = 0;
            foreach (var entry in selected)
                if (!string.IsNullOrEmpty(MaintenanceService.UndoEntry(store, entry))) n++;
            StoreService.Save();
            Refresh();
            await DisplayAlert("撤销完成", "已处理 " + n + " 条。", "知道了");
        }

        private async void OnTransfer(object sender, EventArgs e)
        {
            var selected = SelectedHistoryEntries();
            if (selected.Count == 0)
            {
                await DisplayAlert("转移账目", "请先勾选要转移的历史记录。", "知道了");
                return;
            }
            var target = await PickTargetAsync("把所选 " + selected.Count + " 条记录转移到：");
            if (target == null) return;

            var source = StoreService.Current.Customers.FirstOrDefault(c => c.Id == _customer.Id) ?? _customer;
            var confirm = await DisplayAlert("确认转移",
                "确定把这 " + selected.Count + " 条记录从「" + source.Name + "」转移到「" + target.Name +
                "」吗？\n\n转移后待补盘与抵扣余额会跟着变；可在「转移记录」里整批撤销。",
                "转移", "取消");
            if (!confirm) return;

            var store = StoreService.Current;
            var batch = store.MoveEntries(selected.Select(x => x.Id), source, target);
            StoreService.Save();
            Refresh();
            await DisplayAlert("转移完成", "已转移 " + batch.Count + " 条到「" + target.Name + "」。", "知道了");
        }

        private async void OnTransferLog(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new TransferLogPage());
        }

        private async void OnManageAliases(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AliasManagerPage(_customer.Id));
        }

        private async void OnMerge(object sender, EventArgs e)
        {
            var target = await PickTargetAsync("把客户「" + _customer.Name + "」并入：");
            if (target == null) return;
            var store = StoreService.Current;
            var source = store.Customers.FirstOrDefault(c => c.Id == _customer.Id) ?? _customer;
            int count = store.Entries.Count(x => x.CustomerId == source.Id);
            var confirm = await DisplayAlert("确认合并客户",
                "确定把「" + source.Name + "」并入「" + target.Name + "」吗？\n\n" +
                "· 源客户名与别名会成为目标别名\n" +
                "· " + count + " 条账目转到目标名下\n" +
                "· 原客户档案会被移除\n\n合并前会自动备份，可用「我的 → 从备份恢复」还原。",
                "合并", "取消");
            if (!confirm) return;

            MaintenanceService.MergeWithBackup(store, source, target);
            await Navigation.PopAsync();
        }

        private async System.Threading.Tasks.Task<Customer> PickTargetAsync(string title)
        {
            var store = StoreService.Current;
            var others = store.Customers.Where(c => c.Id != _customer.Id)
                .OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();
            if (others.Count == 0)
            {
                await DisplayAlert("无法继续", "目前没有可选择的另一个客户。", "知道了");
                return null;
            }

            var choices = others.Take(8).Select(DisplayName).ToList();
            bool hasMore = others.Count > 8;
            if (hasMore) choices.Add("输入名称筛选…");
            var action = await DisplayActionSheet(title, "取消", null, choices.ToArray());
            if (action == null) return null;

            if (action == "输入名称筛选…")
            {
                var query = await DisplayPromptAsync(title, "输入客户名或别名：", accept: "筛选", cancel: "取消");
                if (query == null) return null;
                others = others.Where(c => c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.Aliases.Any(a => a.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (others.Count == 0)
                {
                    await DisplayAlert("没有匹配", "没有找到匹配的客户。", "知道了");
                    return null;
                }
                var filtered = others.Take(8).Select(DisplayName).ToArray();
                action = await DisplayActionSheet(title, "取消", null, filtered);
                if (action == null) return null;
            }
            return others.FirstOrDefault(c => DisplayName(c) == action);
        }

        private static string DisplayName(Customer c)
        {
            return c.Name + (c.Archived ? "（已归档）" : "");
        }

        private async void OnArchive(object sender, EventArgs e)
        {
            var store = StoreService.Current;
            var customer = store.Customers.FirstOrDefault(c => c.Id == _customer.Id) ?? _customer;
            if (customer.Archived)
            {
                CustomerService.Unarchive(customer);
                StoreService.Save();
                await Navigation.PopAsync();
                return;
            }

            int pending = CustomerService.PendingQty(store, customer.Id);
            int balance = store.CreditBalance(customer.Id);
            var warn = "";
            if (pending > 0) warn += "还有 " + pending + " 张待补盘；";
            if (balance != 0) warn += "抵扣余额还有 " + balance + " 元；";
            var confirm = await DisplayAlert("归档客户",
                "确定归档「" + customer.Name + "」吗？\n\n" +
                (warn.Length > 0 ? "⚠ " + warn + "\n\n" : "") +
                "归档后默认不再显示，但历史数据完整保留；下月导入补盘时会自动恢复。",
                "归档", "取消");
            if (!confirm) return;

            CustomerService.Archive(customer);
            StoreService.Save();
            await Navigation.PopAsync();
        }
    }
}
