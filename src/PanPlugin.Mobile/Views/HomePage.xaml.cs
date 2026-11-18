using System.Linq;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            var store = StoreService.Current;
            var pending = store.Customers.Sum(c => CustomerService.PendingQty(store, c.Id));
            var balance = store.Customers.Sum(c => store.CreditBalance(c.Id));
            PendingLabel.Text = pending + " 张";
            BalanceLabel.Text = "¥" + balance;
            SubtitleLabel.Text = "本地账本：" + store.Customers.Count + " 家客户 · 搜索与列表将在 M3 接入";
        }
    }
}
