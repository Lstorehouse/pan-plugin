using System.Collections.Generic;
using System.Linq;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public class TransferListItem
    {
        public string Text { get; set; }
        public TransferBatch Batch { get; set; }
    }

    public partial class TransferLogPage : ContentPage
    {
        private TransferBatch _selected;

        public TransferLogPage()
        {
            InitializeComponent();
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
            var items = new List<TransferListItem>();
            foreach (var b in store.TransferBatches.OrderByDescending(b => b.At))
            {
                items.Add(new TransferListItem
                {
                    Text = b.At.ToString("MM-dd HH:mm") + "   " + b.FromName +
                           " → " + b.ToName + "   " + b.Count + " 条" +
                           (b.Undone ? "  （已撤销 " + b.UndoneCount + " 条）" : ""),
                    Batch = b
                });
            }
            TransferList.ItemsSource = items;
            _selected = null;
            UndoButton.IsEnabled = false;
        }

        private void OnBatchSelected(object sender, SelectionChangedEventArgs e)
        {
            var item = e.CurrentSelection.FirstOrDefault() as TransferListItem;
            _selected = item == null ? null : item.Batch;
            UndoButton.IsEnabled = _selected != null && !_selected.Undone;
        }

        private async void OnUndoBatch(object sender, System.EventArgs e)
        {
            if (_selected == null) return;
            var batch = _selected;
            var confirm = await DisplayAlert("确认撤销转移",
                "确定撤销 " + batch.At.ToString("yyyy-MM-dd HH:mm") + " 这次转移吗？\n" +
                batch.FromName + " → " + batch.ToName + "（" + batch.Count + " 条）",
                "撤销", "取消");
            if (!confirm) return;

            int n = StoreService.Current.UndoTransfer(batch);
            StoreService.Save();
            Refresh();
            await DisplayAlert("撤销完成",
                "已撤销 " + n + " 条，退回「" + batch.FromName + "」。", "知道了");
        }
    }
}
