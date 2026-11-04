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
        /// <summary>账目转移日志（可整批撤销）。</summary>
        public List<TransferBatch> TransferBatches { get; set; } = new List<TransferBatch>();
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
                        if (s.TransferBatches == null) s.TransferBatches = new List<TransferBatch>();
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

        /// <summary>列出可用备份（程序目录 + 回退目录，按文件名倒序＝最新在前）。</summary>
        public static List<string> ListBackups()
        {
            var dirs = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups"),
                Path.Combine(Dir, "Backups")
            };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d, "store-*.json"))
                    if (seen.Add(Path.GetFullPath(f))) list.Add(f);
            }
            list.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));
            return list;
        }

        /// <summary>
        /// 从备份恢复：先校验 JSON，再把当前账本另存为 before-restore-*.json
        /// （不匹配 store-*.json，不会被 3 份轮转删掉），最后原子替换 store.json。
        /// 返回恢复前的账本路径。
        /// </summary>
        public static string RestoreFrom(string backupPath)
        {
            return RestoreFromTo(backupPath, FilePath, BackupDir());
        }

        /// <summary>
        /// 恢复逻辑本体，路径可注入（便于在临时目录里验证，不触碰真实账本）。
        /// targetFile 是目标账本，backupDir 是「恢复前现场」的保存目录。
        /// </summary>
        public static string RestoreFromTo(string backupPath, string targetFile, string backupDir)
        {
            if (!File.Exists(backupPath)) throw new Exception("备份文件不存在：" + backupPath);
            var json = File.ReadAllText(backupPath);
            var probe = JsonConvert.DeserializeObject<Store>(json);
            if (probe == null || probe.Customers == null || probe.Entries == null)
                throw new Exception("备份文件不是有效的账本。");

            Directory.CreateDirectory(backupDir);
            string safety = null;
            if (File.Exists(targetFile))
            {
                safety = Path.Combine(backupDir, "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.Copy(targetFile, safety, false);
                // before-restore 也只留最近 3 份，避免无限堆积
                var olds = Directory.GetFiles(backupDir, "before-restore-*.json")
                    .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .Skip(3).ToList();
                foreach (var f in olds) { try { File.Delete(f); } catch { } }
            }

            var tmp = targetFile + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(targetFile)) File.Delete(targetFile);
            File.Move(tmp, targetFile);
            return safety;
        }

        /// <summary>
        /// 把 source 客户并入 target：别名并过去、账目逐条转过去、移除 source 档案。
        /// 账目转移会生成 TransferBatch 日志；档案移除不可逆，调用方需先自动备份。
        /// </summary>
        public TransferBatch MergeCustomer(Customer source, Customer target)
        {
            if (source == null || target == null || source.Id == target.Id) return null;
            var ids = Entries.Where(e => e.CustomerId == source.Id).Select(e => e.Id).ToList();
            var batch = MoveEntries(ids, source, target);
            target.AddAlias(source.Name);
            if (source.Aliases != null)
                foreach (var a in source.Aliases) target.AddAlias(a);
            Customers.Remove(source);
            ExcludedLinks.RemoveWhere(k => k.EndsWith("|" + source.Id, StringComparison.Ordinal));
            return batch;
        }

        /// <summary>「名字↔客户」排除键：名字归一化小写 + 客户 Id。</summary>
        public static string LinkKey(string rawName, string customerId)
        {
            return (rawName ?? "").Trim().ToLowerInvariant() + "|" + (customerId ?? "");
        }

        /// <summary>逐条把账目从 from 搬到 to，生成一条可整批撤销的转移日志。</summary>
        public TransferBatch MoveEntries(IEnumerable<string> entryIds, Customer from, Customer to)
        {
            var batch = new TransferBatch
            {
                Id = Guid.NewGuid().ToString("N"),
                At = DateTime.Now,
                FromCustomerId = from.Id,
                ToCustomerId = to.Id,
                FromName = from.Name,
                ToName = to.Name
            };
            foreach (var id in entryIds)
            {
                var e = Entries.FirstOrDefault(x => x.Id == id);
                if (e == null || e.CustomerId != from.Id) continue;   // 已删除或不在源客户名下
                e.CustomerId = to.Id;
                batch.EntryIds.Add(id);
            }
            batch.Count = batch.EntryIds.Count;
            if (batch.Count > 0) TransferBatches.Add(batch);
            return batch;
        }

        /// <summary>
        /// 整批撤销一次转移。已删除、已不在目标客户名下、或被更晚的转移批次重新处理过的
        /// 条目会跳过，避免覆盖后续操作。返回实际搬回的条数。
        /// </summary>
        public int UndoTransfer(TransferBatch batch)
        {
            if (batch == null || batch.Undone) return 0;
            int myIndex = TransferBatches.IndexOf(batch);
            int n = 0;
            foreach (var id in batch.EntryIds)
            {
                var e = Entries.FirstOrDefault(x => x.Id == id);
                if (e == null || e.CustomerId != batch.ToCustomerId) continue;
                bool superseded = myIndex >= 0 &&
                    TransferBatches.Skip(myIndex + 1).Any(o => o.EntryIds.Contains(id));
                if (superseded) continue;
                e.CustomerId = batch.FromCustomerId;
                n++;
            }
            batch.Undone = true;
            batch.UndoneAt = DateTime.Now;
            batch.UndoneCount = n;
            return n;
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
