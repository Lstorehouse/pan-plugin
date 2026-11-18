using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PanPlugin
{
    /// <summary>
    /// 账期：导入文件所属的月份，格式 yyyy-MM。
    /// 用途：① 跨月去重（10 月与 11 月同样的「4张68元」不该互相吃掉）；
    /// ② 导入前让用户确认，防止误选上月/其他月份的文件。
    /// 注意：账期只作用于去重与备注展示，抵扣余额与待补盘仍是累计账（ADR-0003）。
    /// 本文件只保留纯逻辑，导入弹窗在 Period.UI.cs，移动端可安全链接本文件。
    /// </summary>
    public static class Period
    {
        private static readonly Regex IsoMonth =
            new Regex(@"(20\d{2})\s*[-./年]\s*(\d{1,2})\s*月?");
        private static readonly Regex MonthInName = new Regex(@"(\d{1,2})\s*月");
        private static readonly Regex YearInName = new Regex(@"(20\d{2})");
        private static readonly Regex PeriodPattern = new Regex(@"^20\d{2}-(0?[1-9]|1[0-2])$");

        /// <summary>
        /// 从文件名猜账期；文件名没写月份时退回 fallback 的年月
        /// （补盘用 Excel 明细里最多的创建日期，退款/手工用导入时刻）。
        /// </summary>
        public static string FromFileName(string fileName, DateTime fallback)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                var iso = IsoMonth.Match(fileName);
                if (iso.Success)
                {
                    int y, m;
                    if (int.TryParse(iso.Groups[1].Value, out y) &&
                        int.TryParse(iso.Groups[2].Value, out m) && m >= 1 && m <= 12)
                        return y.ToString("0000", CultureInfo.InvariantCulture) + "-" + m.ToString("00");
                }

                var monthMatch = MonthInName.Match(fileName);
                if (monthMatch.Success)
                {
                    int month;
                    if (int.TryParse(monthMatch.Groups[1].Value, out month) && month >= 1 && month <= 12)
                    {
                        int year = fallback.Year;
                        var yearMatch = YearInName.Match(fileName);
                        int parsedYear;
                        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out parsedYear))
                            year = parsedYear;
                        // 12 月名单常在次年 1 月导入，文件名没带年份时按「不晚于导入月」校正
                        else if (month > fallback.Month && fallback.Month <= 2)
                            year = fallback.Year - 1;
                        return year.ToString("0000", CultureInfo.InvariantCulture) + "-" + month.ToString("00");
                    }
                }
            }
            return fallback.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        public static string Display(string period)
        {
            if (string.IsNullOrEmpty(period)) return "未指定";
            var parts = period.Split('-');
            if (parts.Length != 2) return period;
            return parts[0] + "年" + int.Parse(parts[1]) + "月";
        }

        public static bool IsValid(string period)
        {
            return !string.IsNullOrEmpty(period) && PeriodPattern.IsMatch(period.Trim());
        }

        /// <summary>校验并补零，非法返回 null。</summary>
        public static string Normalize(string period)
        {
            var s = (period ?? "").Trim();
            if (!IsValid(s)) return null;
            var parts = s.Split('-');
            return parts[0] + "-" + int.Parse(parts[1]).ToString("00");
        }

        /// <summary>
        /// 把账期插入去重指纹第二段：
        /// bupan|20261024540 → bupan|2026-10|20261024540
        /// refund|新乡老张|4|68 → refund|2026-10|新乡老张|4|68
        /// 已含 yyyy-MM 的指纹原样返回；无指纹（手工条目）返回 null。
        /// </summary>
        public static string WithPeriod(string refKey, string period)
        {
            if (string.IsNullOrEmpty(refKey)) return refKey;
            var parts = refKey.Split('|');
            if (parts.Length >= 2 && PeriodPattern.IsMatch(parts[1])) return refKey;
            return parts[0] + "|" + period + "|" +
                   string.Join("|", parts, 1, parts.Length - 1);
        }
    }
}
