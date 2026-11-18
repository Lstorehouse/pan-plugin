using System.Collections.Generic;
using System.Linq;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public class CustomerListItem
    {
        public string Name { get; set; }
        public string Detail { get; set; }
        public Customer Customer { get; set; }
    }

    public partial class CustomerPage : ContentPage
    {
        private bool _showArchived;

        public CustomerPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RefreshList();
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private void OnToggleArchived(object sender, System.EventArgs e)
        {
            _showArchived = !_showArchived;
            ArchivedButton.Text = _showArchived ? "仅看在用" : "显示已归档";
            RefreshList();
        }

        private void RefreshList()
        {
            var store = StoreService.Current;
            var list = CustomerService.Search(store, SearchBar.Text, _showArchived);
            var items = new List<CustomerListItem>();
            foreach (var c in list)
            {
                int pending = CustomerService.PendingQty(store, c.Id);
                int balance = store.CreditBalance(c.Id);
                items.Add(new CustomerListItem
                {
                    Name = c.Archived ? c.Name + "（已归档）" : c.Name,
                    Detail = pending + " 张待补 · ¥" + balance +
                             (c.Aliases.Count > 0 ? " · 别名 " + string.Join("、", c.Aliases.Take(2)) : ""),
                    Customer = c
                });
            }
            CustomerList.ItemsSource = items;
            SummaryLabel.Text = "共 " + items.Count + " 家客户" + (_showArchived ? "（含归档）" : "");
        }

        private async void OnCustomerSelected(object sender, SelectionChangedEventArgs e)
        {
            var item = e.CurrentSelection.FirstOrDefault() as CustomerListItem;
            if (item == null) return;
            ((CollectionView)sender).SelectedItem = null;
            await Navigation.PushAsync(new CustomerDetailPage(item.Customer));
        }
    }
}
