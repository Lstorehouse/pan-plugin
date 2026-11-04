using System;
using System.Collections.Generic;
using System.IO;
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
