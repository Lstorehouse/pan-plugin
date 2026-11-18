using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using PanPlugin.Mobile.Services;

namespace PanPlugin.Mobile.Views
{
    public partial class SettingsPage : ContentPage
    {
        private static readonly FilePickerFileType JsonType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "application/json", "*/*" } }
            });

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            var store = StoreService.Current;
            StatusLabel.Text = "版本 M2 · 客户 " + store.Customers.Count +
                               " 家 · 账目 " + store.Entries.Count +
                               " 条 · 备份 " + StoreService.ListBackups().Count + " 份";
        }

        private async void OnExportBackup(object sender, EventArgs e)
        {
            ExportButton.IsEnabled = false;
            try
            {
                StoreService.Save();
                var path = StoreService.Backup(3);
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "生化盘插件账本备份",
                    File = new ShareFile(path)
                });
                RefreshStatus();
            }
            catch (Exception ex)
            {
                await DisplayAlert("导出失败", ex.Message, "知道了");
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }

        private async void OnRestoreBackup(object sender, EventArgs e)
        {
            ImportButton.IsEnabled = false;
            try
            {
                var file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "选择账本备份（json）",
                    FileTypes = JsonType
                });
                if (file == null) return;

                var confirm = await DisplayAlert(
                    "恢复备份",
                    "将用「" + file.FileName + "」覆盖当前账本。恢复前会自动另存一份现场备份。继续？",
                    "恢复",
                    "取消");
                if (!confirm) return;

                var temp = Path.Combine(StoreService.DataDirectory,
                    "restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                using (var input = await file.OpenReadAsync())
                using (var output = File.Create(temp))
                {
                    await input.CopyToAsync(output);
                }

                StoreService.RestoreFrom(temp);
                RefreshStatus();
                await DisplayAlert("恢复完成", "账本已恢复，共 " +
                    StoreService.Current.Customers.Count + " 家客户。", "知道了");
            }
            catch (Exception ex)
            {
                await DisplayAlert("恢复失败", ex.Message, "知道了");
            }
            finally
            {
                ImportButton.IsEnabled = true;
            }
        }
    }
}
