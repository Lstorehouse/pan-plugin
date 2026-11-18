using System;
using System.Collections.Generic;
using System.Linq;

namespace PanPlugin
{
    /// <summary>一次导入入账的统计结果，供桌面/移动端各自展示文案。</summary>
    public class ImportOutcome
    {
        public int Linked;
        public int Created;
        public int Added;
        public int Skipped;
        public int Revived;

        public string Summary(string sourceLabel, ImportResult parsed, string period)
        {
            var msg = sourceLabel + "（账期 " + Period.Display(period) + "）：入账 " + Added +
                      " 条，自动/关联归属 " + Linked + " 组，新建客户 " + Created +
                      " 个，跳过 " + Skipped + " 条";
            if (parsed.SkippedVs2 > 0) msg += "（含 VS2 综合盘 " + parsed.SkippedVs2 + " 条）";
            if (Revived > 0) msg += "；↻ " + Revived + " 个归档客户因新补盘已自动恢复显示";
            if (parsed.UnparsedLines.Count > 0)
                msg += "；⚠ " + parsed.UnparsedLines.Count + " 行无法解析：" +
                       string.Join(" / ", parsed.UnparsedLines.Take(3));
            return msg;
        }
    }

    /// <summary>
    /// 导入入账的共享规则：账期写入、跨月去重、别名补录、归档客户自动恢复。
    /// 客户归属决策由调用方通过 resolver 回调完成（桌面弹 WinForms，移动端弹 MAUI）。
    /// </summary>
    public static class ImportService
    {
        /// <summary>模糊候选：互相包含，且排除用户已明确解除过关联的客户。</summary>
        public static List<Customer> CandidatesFor(Store store, string rawName)
        {
            if (store == null || string.IsNullOrWhiteSpace(rawName)) return new List<Customer>();
            return store.Customers.Where(c =>
                !store.ExcludedLinks.Contains(Store.LinkKey(rawName, c.Id)) &&
                (c.Name.IndexOf(rawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 rawName.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 c.Aliases.Any(a => a.IndexOf(rawName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    rawName.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();
        }

        /// <summary>归档客户出现新的补盘条目时自动恢复显示。</summary>
        public static void ReviveIfArchived(Customer c)
        {
            if (c == null || !c.Archived) return;
            c.Archived = false;
            c.ArchivedAt = null;
        }

        /// <summary>
        /// resolver 返回 null 表示跳过该组；返回一个新 Customer 时由本方法加入账本。
        /// 调用方负责在返回前 Save()。
        /// </summary>
        public static ImportOutcome Commit(Store store, ImportResult parsed, string period,
            Func<string, List<Customer>, Customer> resolver)
        {
            var outcome = new ImportOutcome();
            if (store == null || parsed == null) return outcome;
            if (resolver == null) resolver = (name, candidates) => null;

            foreach (var g in parsed.Groups)
            {
                Customer target = store.Customers.FirstOrDefault(c => c.Matches(g.RawName));
                if (target == null)
                {
                    var candidates = CandidatesFor(store, g.RawName);
                    target = resolver(g.RawName, candidates);
                    if (target == null)
                    {
                        outcome.Skipped += g.Entries.Count;
                        continue;
                    }
                    if (!store.Customers.Contains(target))
                    {
                        store.Customers.Add(target);
                        outcome.Created++;
                    }
                    else outcome.Linked++;
                }
                else outcome.Linked++;

                target.AddAlias(g.RawName);

                bool revivedThisGroup = false;
                foreach (var e in g.Entries)
                {
                    e.CustomerId = target.Id;
                    e.Period = period;
                    e.Ref = Period.WithPeriod(e.Ref, period);
                    if (e.Ref != null && store.ImportedRefs.Contains(e.Ref))
                    {
                        outcome.Skipped++;
                        continue;
                    }
                    if (e.Ref != null) store.ImportedRefs.Add(e.Ref);
                    store.Entries.Add(e);
                    outcome.Added++;

                    // 归档客户下月又有补盘 → 自动重新出现（只有真正入账才算，重复导入不算）
                    if (!revivedThisGroup && target.Archived &&
                        (e.Kind == EntryKind.BupanOrder || e.Kind == EntryKind.ManualBupan))
                    {
                        ReviveIfArchived(target);
                        revivedThisGroup = true;
                        outcome.Revived++;
                    }
                }
            }
            return outcome;
        }
    }
}
