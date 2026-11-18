using System.Collections.Generic;
using System.IO;
using Microsoft.Maui.Storage;

namespace PanPlugin.Mobile.Services
{
    /// <summary>
    /// Android 端账本入口：把桌面 Store 的路径注入为 App 私有目录。
    /// 业务规则本身全部复用 ../PanPlugin/Store.cs，不在移动端重写。
    /// </summary>
    public static class StoreService
    {
        private static Store _current;

        public static Store Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        public static string DataDirectory => FileSystem.AppDataDirectory;
        public static string LedgerPath => Path.Combine(DataDirectory, "store.json");
        public static string BackupDirectory => Path.Combine(DataDirectory, "Backups");

        public static void EnsureLoaded()
        {
            if (_current != null) return;
            Directory.CreateDirectory(DataDirectory);
            _current = Store.LoadFrom(LedgerPath);
            _current.BackupDirectory = BackupDirectory;
        }

        public static void Save()
        {
            EnsureLoaded();
            Current.Save();
        }

        public static string Backup(int maxKeep = 3)
        {
            EnsureLoaded();
            return Current.Backup(maxKeep);
        }

        public static List<string> ListBackups()
        {
            EnsureLoaded();
            return Current.ListBackupsForCurrent();
        }

        /// <summary>从备份文件恢复，并立即重新加载，保证后续页面拿到新账本。</summary>
        public static void RestoreFrom(string backupPath)
        {
            EnsureLoaded();
            Current.RestoreFromCurrent(backupPath);
            Reload();
        }

        public static void Reload()
        {
            _current = Store.LoadFrom(LedgerPath);
            _current.BackupDirectory = BackupDirectory;
        }
    }
}
