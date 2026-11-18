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
            var history = store.Entries.Where(e => e.CustomerId == customer.Id)
                .OrderByDescending(e => e.CreatedAt);
            foreach (var e in history)
            {
                HistoryRows.Children.Add(new Label
                {
                    Text = e.CreatedAt.ToString("MM-dd HH:mm") + " · " +
                           CustomerService.HistoryType(e) + " · " +
                           CustomerService.HistoryContent(e) + " · " +
                           (string.IsNullOrEmpty(e.Period) ? "—" : e.Period),
                    FontSize = 12,
                    TextColor = (Color)Application.Current.Resources["TextSecondaryColor"]
                });
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
