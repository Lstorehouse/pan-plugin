using System;
using System.Collections.Generic;
using System.Linq;

namespace PanPlugin
{
    /// <summary>
    /// 账目撤销、别名改写、合并客户的共享规则。
    /// 桌面弹窗与 MAUI 页面只负责收集用户选择，写回逻辑都在这里。
    /// </summary>
    public static class MaintenanceService
    {
        /// <summary>撤销一条历史账目；返回给用户看的动作说明。</summary>
        public static string UndoEntry(Store store, LedgerEntry e)
        {
            if (store == null || e == null) return "";
            if (e.Kind == EntryKind.RefundCredit || e.Kind == EntryKind.ManualCredit ||
                e.Kind == EntryKind.Deduct)
            {
                store.Entries.Remove(e);
                return "已撤销一笔金额记录";
            }
            if (e.Kind == EntryKind.ManualBupan && !e.Processed)
            {
                store.Entries.Remove(e);
                return "已删除手工补盘条目";
            }
            if (e.Kind == EntryKind.ManualBupan && e.Processed)
            {
                e.Processed = false;
                e.ProcessedAt = null;
                return "已撤销“已发”标记";
            }
            e.Processed = false;
            e.ProcessedAt = null;
            return "已撤销补发标记";
        }

        /// <summary>
        /// 一次性写回主名/别名改动。旧名字进 ExcludedLinks，避免下次导入又绑回来；
        /// 转移出去的别名加到目标客户并清掉目标上的排除。
        /// </summary>
        public static void ApplyAliasChanges(
            Store store,
            Customer customer,
            string newName,
            IEnumerable<string> aliases,
            IEnumerable<string> removed,
            IEnumerable<KeyValuePair<string, string>> transfers)
        {
            if (store == null || customer == null) return;
            var aliasList = (aliases ?? Enumerable.Empty<string>()).ToList();
            var removedList = (removed ?? Enumerable.Empty<string>()).ToList();
            var transferList = (transfers ?? Enumerable.Empty<KeyValuePair<string, string>>()).ToList();

            var name = (newName ?? "").Trim();
            if (name.Length == 0) name = customer.Name;
            var oldName = customer.Name;
            bool renamed = !string.Equals(name, oldName, StringComparison.Ordinal);

            customer.Aliases.Clear();
            foreach (var a in aliasList)
                if (!customer.Aliases.Contains(a, StringComparer.OrdinalIgnoreCase))
                    customer.Aliases.Add(a);

            if (renamed)
            {
                customer.Name = name;
                if (!removedList.Any(x => string.Equals(x, oldName, StringComparison.OrdinalIgnoreCase)))
                    customer.AddAlias(oldName);
            }

            foreach (var a in removedList)
                store.ExcludedLinks.Add(Store.LinkKey(a, customer.Id));

            foreach (var t in transferList)
            {
                var target = store.Customers.FirstOrDefault(c => c.Id == t.Value);
                if (target == null) continue;
                target.AddAlias(t.Key);
                store.ExcludedLinks.Remove(Store.LinkKey(t.Key, target.Id));
                customer.Aliases.RemoveAll(a => string.Equals(a, t.Key, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>合并客户前先留一份可恢复现场，再转移账目并移除源档案。</summary>
        public static TransferBatch MergeWithBackup(Store store, Customer source, Customer target)
        {
            if (store == null || source == null || target == null || source.Id == target.Id) return null;
            store.Save();
            store.Backup(3);
            var batch = store.MergeCustomer(source, target);
            store.Save();
            return batch;
        }
    }
}
