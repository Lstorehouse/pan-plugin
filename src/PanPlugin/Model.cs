using System;
using System.Collections.Generic;
using System.Linq;

namespace PanPlugin
{
    /// <summary>盘型。Excel 枚举名与口语名的映射见 GLOSSARY.md。</summary>
    public enum DiscType
    {
        QuanNeng,   // 全能（34项，将升36项）
        ShuQian,    // 核心14项（口语：术前）
        YiChong,    // 异宠15项
        VS2,        // VS2 综合盘 —— 按需求忽略
        Unknown
    }

    public static class DiscTypes
    {
        /// <summary>补盘名单 Excel "申请补偿的耗材种类" 列文本 → 盘型。</summary>
        public static DiscType FromExcelName(string text)
        {
            if (string.IsNullOrEmpty(text)) return DiscType.Unknown;
            var t = text.Trim();
            if (t.IndexOf("综合", StringComparison.Ordinal) >= 0) return DiscType.VS2;
            if (t.IndexOf("异宠", StringComparison.Ordinal) >= 0) return DiscType.YiChong;
            if (t.IndexOf("全能", StringComparison.Ordinal) >= 0
                || t.IndexOf("36项", StringComparison.Ordinal) >= 0
                || t.IndexOf("34项", StringComparison.Ordinal) >= 0) return DiscType.QuanNeng;
            if (t.IndexOf("核心", StringComparison.Ordinal) >= 0
                || t.IndexOf("14项", StringComparison.Ordinal) >= 0
                || t.IndexOf("术前", StringComparison.Ordinal) >= 0) return DiscType.ShuQian;
            return DiscType.Unknown;
        }

        public static string DisplayName(DiscType t)
        {
            switch (t)
            {
                case DiscType.QuanNeng: return "全能34项";
                case DiscType.ShuQian: return "术前14项";
                case DiscType.YiChong: return "异宠15项";
                case DiscType.VS2: return "VS2综合盘";
                default: return "未知";
            }
        }
    }

    /// <summary>价格档位与抵扣规则（ADR 共识：单张抵扣 = 盘型最高价 − 档位）。</summary>
    public static class Pricing
    {
        public static int Highest(DiscType t)
        {
            switch (t)
            {
                case DiscType.QuanNeng: return 88;
                case DiscType.ShuQian: return 38;
                case DiscType.YiChong: return 88;
                default: return 0;
            }
        }

        public static bool IsValidTier(DiscType t, int tier)
        {
            switch (t)
            {
                case DiscType.QuanNeng: return tier == 88 || tier == 78 || tier == 68;
                case DiscType.ShuQian: return tier == 38 || tier == 30;
                case DiscType.YiChong: return tier == 88;
                default: return false;
            }
        }

        public static int CreditPerDisc(DiscType t, int tier)
        {
            if (!IsValidTier(t, tier)) return 0;
            return Highest(t) - tier;
        }

        /// <summary>退款名单里的"NN元"档位 → 折算抵扣的盘型（88 元档抵扣为 0，盘型不影响结果）。</summary>
        public static DiscType TypeFromTier(int tier)
        {
            if (tier == 78 || tier == 68 || tier == 88) return DiscType.QuanNeng;
            if (tier == 38 || tier == 30) return DiscType.ShuQian;
            return DiscType.Unknown;
        }
    }

    public class Customer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        public string HospitalCode { get; set; }
        /// <summary>归档/隐藏：默认列表与搜索不再显示，但档案、别名、账目全部保留。</summary>
        public bool Archived { get; set; }
        /// <summary>归档时间；未归档或已恢复时为 null。</summary>
        public DateTime? ArchivedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool Matches(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim();
            return string.Equals(Name, t, StringComparison.OrdinalIgnoreCase)
                || Aliases.Any(a => string.Equals(a, t, StringComparison.OrdinalIgnoreCase));
        }

        public void AddAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias)) return;
            var a = alias.Trim();
            if (string.Equals(a, Name, StringComparison.OrdinalIgnoreCase)) return;
            if (!Aliases.Contains(a, StringComparer.OrdinalIgnoreCase)) Aliases.Add(a);
        }
    }

    public enum EntryKind
    {
        BupanOrder,    // 补盘名单导入的工单（待补）
        ManualBupan,   // 手工新增的待补条目
        RefundCredit,  // 退款名单导入的抵扣（余额+）
        ManualCredit,  // 手工增加抵扣（余额+）
        Deduct         // 进货抵扣使用（余额-）
    }

    public class LedgerEntry
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public EntryKind Kind { get; set; }
        public DiscType DiscType { get; set; }
        public int Qty { get; set; }              // 补盘张数
        public int Tier { get; set; }             // 档位（退款/抵扣类）
        public int Amount { get; set; }           // 抵扣金额（正=入账，负=扣减）
        public bool Processed { get; set; }       // 补盘条目：是否已补发
        public DateTime? ProcessedAt { get; set; }
        public string Note { get; set; }
        public string Source { get; set; }        // 来源文件名 / "手工"
        public string Period { get; set; }        // 账期 yyyy-MM（导入时确认，用于跨月去重）
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Ref { get; set; }           // 外部主键（工单编号 / 退款行指纹），用于去重
    }
}
