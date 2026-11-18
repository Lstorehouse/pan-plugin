using System;
using System.Collections.Generic;
using System.Linq;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public partial class AliasManagerPage : ContentPage
    {
        private readonly Customer _customer;
        private readonly List<string> _aliases = new List<string>();
        private readonly List<string> _removed = new List<string>();
        private readonly List<KeyValuePair<string, string>> _transfers =
            new List<KeyValuePair<string, string>>();
        private string _selected;

        public AliasManagerPage(string customerId)
        {
            InitializeComponent();
            _customer = StoreService.Current.Customers.FirstOrDefault(c => c.Id == customerId);
            if (_customer == null)
            {
                _customer = new Customer { Id = customerId, Name = "（客户不存在）" };
            }
            NameEntry.Text = _customer.Name;
            if (_customer.Aliases != null) _aliases.AddRange(_customer.Aliases);
            Reload();
        }

        private void Reload()
        {
            AliasList.ItemsSource = _aliases.ToList();
            _selected = null;
        }

        private void OnAliasSelected(object sender, SelectionChangedEventArgs e)
        {
            _selected = e.CurrentSelection.FirstOrDefault() as string;
        }

        private async void OnAdd(object sender, EventArgs e)
        {
            var name = await DisplayPromptAsync("新增别名", "别名：", accept: "添加", cancel: "取消");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (string.Equals(name, NameEntry.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("无法添加", "这个名字已经是客户主名。", "知道了");
                return;
            }
            if (_aliases.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                await DisplayAlert("无法添加", "该别名已存在。", "知道了");
                return;
            }
            _aliases.Add(name);
            _removed.RemoveAll(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            Reload();
        }

        private async void OnRename(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("改名", "请先选中一个别名。", "知道了");
                return;
            }
            var oldName = _selected;
            var name = await DisplayPromptAsync("别名改名", "把「" + oldName + "」改为：",
                accept: "确定", cancel: "取消", initialValue: oldName);
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(name, NameEntry.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await DisplayAlert("无法改名", "这个名字已经是客户主名。", "知道了");
                return;
            }
            if (_aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
            {
                await DisplayAlert("无法改名", "该别名已存在。", "知道了");
                return;
            }

            int idx = _aliases.FindIndex(a => string.Equals(a, oldName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _aliases[idx] = name;
            if (!_removed.Contains(oldName, StringComparer.OrdinalIgnoreCase)) _removed.Add(oldName);
            _removed.RemoveAll(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < _transfers.Count; i++)
                if (string.Equals(_transfers[i].Key, oldName, StringComparison.OrdinalIgnoreCase))
                    _transfers[i] = new KeyValuePair<string, string>(name, _transfers[i].Value);
            Reload();
        }

        private async void OnDelete(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("删除", "请先选中一个别名。", "知道了");
                return;
            }
            if (!_removed.Contains(_selected, StringComparer.OrdinalIgnoreCase)) _removed.Add(_selected);
            _aliases.RemoveAll(a => string.Equals(a, _selected, StringComparison.OrdinalIgnoreCase));
            Reload();
        }

        private async void OnTransfer(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("转移", "请先选中一个别名。", "知道了");
                return;
            }
            var others = StoreService.Current.Customers
                .Where(c => c.Id != _customer.Id)
                .OrderBy(c => c.Name, StringComparer.CurrentCulture).ToList();
            if (others.Count == 0)
            {
                await DisplayAlert("无法转移", "目前没有其他客户。", "知道了");
                return;
            }
            var choices = others.Take(8).Select(c => c.Name + (c.Archived ? "（已归档）" : "")).ToArray();
            var action = await DisplayActionSheet("把「" + _selected + "」转移给：", "取消", null, choices);
            if (action == null) return;
            var target = others.FirstOrDefault(c => c.Name + (c.Archived ? "（已归档）" : "") == action);
            if (target == null) return;

            if (!_removed.Contains(_selected, StringComparer.OrdinalIgnoreCase)) _removed.Add(_selected);
            _transfers.Add(new KeyValuePair<string, string>(_selected, target.Id));
            _aliases.RemoveAll(a => string.Equals(a, _selected, StringComparison.OrdinalIgnoreCase));
            Reload();
        }

        private async void OnSave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                await DisplayAlert("无法保存", "客户主名不能为空。", "知道了");
                return;
            }
            MaintenanceService.ApplyAliasChanges(
                StoreService.Current, _customer, NameEntry.Text.Trim(),
                _aliases, _removed, _transfers);
            StoreService.Save();
            await Navigation.PopAsync();
        }

        private async void OnCancel(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
