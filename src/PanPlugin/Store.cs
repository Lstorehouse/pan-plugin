using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PanPlugin
{
    /// <summary>
    /// 本地持久化（ADR-0001：源文件只读，一切增减记本地账本）。
    /// 数据存于 %APPDATA%\PanPlugin\store.json。
    /// </summary>
    public class Store
    {
        public List<Customer> Customers { get; set; } = new List<Customer>();
        public List<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
        /// <summary>已导入过的外部记录指纹（防重复导入）。</summary>
        public HashSet<string> ImportedRefs { get; set; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>被用户解除的「名字↔客户」关联，防止下次导入又被模糊候选重新绑错。</summary>
        public HashSet<string> ExcludedLinks { get; set; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>上次使用的导入文件夹。</summary>
        public string LastFolder { get; set; }

        [JsonIgnore]
        private static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PanPlugin"); }
        }

        private static string FilePath
        {
            get { return Path.Combine(Dir, "store.json"); }
        }

        public static Store Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var s = JsonConvert.DeserializeObject<Store>(json);
                    if (s != null)
                    {
                        if (s.Customers == null) s.Customers = new List<Customer>();
                        if (s.Entries == null) s.Entries = new List<LedgerEntry>();
                        if (s.ImportedRefs == null) s.ImportedRefs = new HashSet<string>(StringComparer.Ordinal);
                        if (s.ExcludedLinks == null) s.ExcludedLinks = new HashSet<string>(StringComparer.Ordinal);
                        s.MigratePeriods();
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("读取本地数据失败，将使用空数据启动。\n\n" + ex.Message,
                    "生化盘插件", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            return new Store();
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        /// <summary>「名字↔客户」排除键：名字归一化小写 + 客户 Id。</summary>
        public static string LinkKey(string rawName, string customerId)
        {
            return (rawName ?? "").Trim().ToLowerInvariant() + "|" + (customerId ?? "");
        }

        /// <summary>
        /// 备份目录：优先程序目录下的 Backups（用户容易找到、可拷走）；
        /// 程序装在只读位置时回退到 %APPDATA%\PanPlugin\Backups。
        /// </summary>
        public static string BackupDir()
        {
            try
            {
                var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                Directory.CreateDirectory(local);
                var probe = Path.Combine(local, ".writetest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return local;
            }
            catch { }
            return Path.Combine(Dir, "Backups");
        }

        /// <summary>备份当前账本；最多保留 maxKeep 份，超出删除最早的。返回备份文件路径。</summary>
        public string Backup(int maxKeep = 3)
        {
            if (!File.Exists(FilePath)) Save();   // 全新安装还没落盘时先写一份
            var dir = BackupDir();
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "store-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.Copy(FilePath, dest, false);
            RotateBackups(dir, maxKeep);
            return dest;
        }

        /// <summary>按文件名（yyyyMMdd-HHmmss-fff，字典序即时间序）保留最新 maxKeep 份，删掉更早的。</summary>
        public static int RotateBackups(string dir, int maxKeep)
        {
            if (!Directory.Exists(dir) || maxKeep < 1) return 0;
            var files = Directory.GetFiles(dir, "store-*.json")
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
            int removed = 0;
            for (int i = maxKeep; i < files.Count; i++)
            {
                try { File.Delete(files[i]); removed++; }
                catch { }   // 备份文件被占用时不影响主流程
            }
            return removed;
        }

        /// <summary>
        /// 兼容旧账本：旧指纹不含账期（如 refund|客户|4|68），按记录的来源文件名／日期
        /// 补出账期并生成新指纹。旧指纹同时保留，方便回退到旧版本使用同一份账本。
        /// 幂等：每次启动重算结果一致，无需写盘即可保证重复导入仍被拦住。
        /// </summary>
        private void MigratePeriods()
        {
            foreach (var e in Entries)
            {
                if (string.IsNullOrEmpty(e.Period))
                    e.Period = Period.FromFileName(e.Source, e.CreatedAt);
                if (string.IsNullOrEmpty(e.Ref)) continue;

                var oldRef = e.Ref;
                var newRef = Period.WithPeriod(oldRef, e.Period);
                if (newRef == oldRef) continue;   // 已是新格式
                ImportedRefs.Add(oldRef);
                ImportedRefs.Add(newRef);
                e.Ref = newRef;
            }
        }

        public int CreditBalance(string customerId)
        {
            int sum = 0;
            foreach (var e in Entries)
            {
                if (e.CustomerId != customerId) continue;
                if (e.Kind == EntryKind.RefundCredit || e.Kind == EntryKind.ManualCredit) sum += e.Amount;
                else if (e.Kind == EntryKind.Deduct) sum += e.Amount; // Deduct 存负数
            }
            return sum;
        }
    }
}
