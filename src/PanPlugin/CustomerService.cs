using System;
using System.Collections.Generic;
using System.Linq;

namespace PanPlugin
{
    /// <summary>
    /// 客户列表、待补聚合与手工记账的共享规则。
    /// 桌面与 Android 都调用这里，避免 M3 出现两套口径。
    /// </summary>
    public static class CustomerService
    {
        /// <summary>客户搜索：主名/别名模糊命中，归档默认隐藏，开头命中优先。</summary>
        public static List<Customer> Search(Store store, string query, bool includeArchived = false)
        {
            if (store == null) return new List<Customer>();
            var q = (query ?? "").Trim();
            IEnumerable<Customer> list = includeArchived
                ? store.Customers
                : store.Customers.Where(c => !c.Archived);
            if (q.Length > 0)
            {
                list = list.Where(c =>
                    c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.Aliases.Any(a => a.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            return list
                .OrderByDescending(c => q.Length > 0 &&
                    c.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(c => c.Name, StringComparer.CurrentCulture)
                .ToList();
        }

        public static List<LedgerEntry> PendingOf(Store store, string customerId)
        {
            if (store == null) return new List<LedgerEntry>();
            return store.Entries.Where(e => e.CustomerId == customerId
                && (e.Kind == EntryKind.BupanOrder || e.Kind == EntryKind.ManualBupan)
                && !e.Processed).ToList();
        }

        public static Dictionary<DiscType, int> PendingByType(Store store, string customerId)
        {
            var dict = new Dictionary<DiscType, int>();
            foreach (var e in PendingOf(store, customerId))
            {
                if (!dict.ContainsKey(e.DiscType)) dict[e.DiscType] = 0;
                dict[e.DiscType] += e.Qty;
            }
            return dict;
        }

        public static int PendingQty(Store store, string customerId)
        {
            return PendingOf(store, customerId).Sum(e => e.Qty);
        }

        /// <summary>历史「内容」列：业务描述在外，金额放括号内；无描述时只显示金额。</summary>
        public static string MoneyText(string note, string money)
        {
            return string.IsNullOrWhiteSpace(note) ? money : note.Trim() + "（" + money + "）";
        }

        public static string HistoryType(LedgerEntry e)
        {
            if (e == null) return "其他";
            switch (e.Kind)
            {
                case EntryKind.BupanOrder:
                case EntryKind.ManualBupan:
                    return e.Processed ? "补盘·已发" : "补盘·待发";
                case EntryKind.RefundCredit: return "退款入账";
                case EntryKind.ManualCredit: return "手工入账";
                case EntryKind.Deduct: return "进货抵扣";
                default: return "其他";
            }
        }

        public static string HistoryContent(LedgerEntry e)
        {
            if (e == null) return "";
            switch (e.Kind)
            {
                case EntryKind.BupanOrder:
                case EntryKind.ManualBupan:
                    return DiscTypes.DisplayName(e.DiscType) + " " + e.Qty + " 张";
                case EntryKind.RefundCredit:
                    return MoneyText(e.Note, "+" + e.Amount + " 元");
                case EntryKind.ManualCredit:
                    return MoneyText(e.Note, "+" + e.Amount + " 元");
                case EntryKind.Deduct:
                    return MoneyText(e.Note, e.Amount + " 元");
                default: return "";
            }
        }

        /// <summary>勾选补发：只有原本未补发的条目才改动，保证计数不虚高。</summary>
        public static bool MarkProcessed(LedgerEntry e)
        {
            if (e == null || e.Processed) return false;
            e.Processed = true;
            e.ProcessedAt = DateTime.Now;
            return true;
        }

        public static LedgerEntry NewManualBupan(Customer c, DiscType type, int qty, string note)
        {
            if (c == null || qty <= 0) return null;
            ImportService.ReviveIfArchived(c);   // 归档客户手工新增补盘也会重新出现
            return new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = c.Id,
                Kind = EntryKind.ManualBupan,
                DiscType = type,
                Qty = qty,
                Note = note,
                Source = "手工",
                Ref = null
            };
        }

        public static LedgerEntry NewManualCredit(Customer c, int amount, string note)
        {
            if (c == null || amount <= 0) return null;
            return new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = c.Id,
                Kind = EntryKind.ManualCredit,
                Amount = amount,
                Note = note,
                Source = "手工"
            };
        }

        public static LedgerEntry NewDeduct(Customer c, int amount, string note)
        {
            if (c == null || amount <= 0) return null;
            return new LedgerEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CustomerId = c.Id,
                Kind = EntryKind.Deduct,
                Amount = -amount,
                Note = note,
                Source = "手工"
            };
        }

        public static void Archive(Customer c)
        {
            if (c == null) return;
            c.Archived = true;
            c.ArchivedAt = DateTime.Now;
        }

        public static void Unarchive(Customer c)
        {
            if (c == null) return;
            c.Archived = false;
            c.ArchivedAt = null;
        }
    }
}
