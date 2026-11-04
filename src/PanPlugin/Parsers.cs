using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace PanPlugin
{
    /// <summary>解析结果：一个客户名 + 其条目/金额。</summary>
    public class ParsedGroup
    {
        public string RawName;
        public List<LedgerEntry> Entries = new List<LedgerEntry>();
    }

    public class ImportResult
    {
        public List<ParsedGroup> Groups = new List<ParsedGroup>();
        public int SkippedVs2;
        public int SkippedUnknown;
        public int SkippedDuplicate;
        public List<string> UnparsedLines = new List<string>();
    }

    /// <summary>补盘名单 xlsx 解析器（明细底表：一行 = 一张工单，数量恒 1）。</summary>
    public static class BupanExcelParser
    {
        public static ImportResult Parse(string path)
        {
            var result = new ImportResult();
            // 先读进内存再解析：用户常把月度名单开在 Excel 里，ClosedXML 直接按路径打开
            // 会因共享模式冲突抛「文件正由另一进程使用」。
            // 显式 FileShare.ReadWrite 允许与 Excel 的读写句柄共存；内存流只读、源文件不被改动。
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                       System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            using (var ms = new System.IO.MemoryStream())
            {
                fs.CopyTo(ms);
                ms.Position = 0;
                using (var wb = new XLWorkbook(ms))
                {
                int headerRowNum;
                IXLWorksheet sheet = FindDetailSheet(wb, out headerRowNum);
                if (sheet == null)
                    throw new Exception("找不到明细底表：需要一个同时含「工单编号」和「耗材种类」两列的工作表。\n"
                        + DescribeSheets(wb));

                // 表头按「归一化后的表头名 → 列号」收集；表头行可能不是第 1 行。
                var header = new List<KeyValuePair<string, int>>();
                int headerLastCol = Math.Min(sheet.LastColumnUsed().ColumnNumber(), 100);
                for (int c = 1; c <= headerLastCol; c++)
                {
                    var key = NormalizeKey(sheet.Cell(headerRowNum, c).GetString());
                    if (key.Length == 0) continue;
                    header.Add(new KeyValuePair<string, int>(key, c));
                }

                int Col(params string[] names)
                {
                    // 先精确匹配，再退化为「包含」匹配（容忍全角括号、表头附注等差异）
                    foreach (var n in names)
                        foreach (var kv in header)
                            if (kv.Key == n) return kv.Value;
                    foreach (var n in names)
                        foreach (var kv in header)
                            if (kv.Key.Contains(n)) return kv.Value;
                    return 0;
                }

                string Text(int r, int c) { return c > 0 ? sheet.Cell(r, c).GetString() : ""; }

                int cOrder = Col("工单编号");
                int cDate = Col("创建日期");
                int cType = Col("申请补偿的耗材种类", "耗材种类");
                int cQty = Col("申请补偿的耗材数量", "耗材数量");
                int cHospital = Col("提交时装机设备医院(提交时系统匹配)", "提交时装机设备医院", "装机设备医院");
                int cStdName = Col("标准医院名称");
                int cStdCode = Col("标准医院代码");
                int cStatus = Col("状态");

                var fileName = System.IO.Path.GetFileName(path);
                var byCustomer = new Dictionary<string, ParsedGroup>(StringComparer.OrdinalIgnoreCase);

                int lastRow = sheet.LastRowUsed().RowNumber();
                for (int r = headerRowNum + 1; r <= lastRow; r++)
                {
                    var typeText = Text(r, cType);
                    var orderNo = Text(r, cOrder).Trim();
                    if (string.IsNullOrWhiteSpace(typeText) && string.IsNullOrWhiteSpace(orderNo))
                        continue; // 空行

                    var dt = DiscTypes.FromExcelName(typeText);
                    if (dt == DiscType.VS2) { result.SkippedVs2++; continue; }
                    if (dt == DiscType.Unknown) { result.SkippedUnknown++; continue; }

                    // 客户名：优先标准医院名称，其次装机医院
                    var name = Text(r, cStdName).Trim();
                    if (string.IsNullOrEmpty(name)) name = Text(r, cHospital).Trim();
                    if (string.IsNullOrEmpty(name)) { result.SkippedUnknown++; continue; }

                    var dedupRef = "bupan|" + orderNo;
                    if (orderNo.Length == 0) dedupRef = "bupanrow|" + fileName + "|" + r;

                    int qty = 1;
                    if (cQty > 0)
                    {
                        var q = Text(r, cQty).Trim();
                        int qv;
                        if (int.TryParse(q, out qv) && qv > 0) qty = qv;
                    }

                    ParsedGroup g;
                    if (!byCustomer.TryGetValue(name, out g))
                    {
                        g = new ParsedGroup { RawName = name };
                        byCustomer[name] = g;
                        result.Groups.Add(g);
                    }

                    var status = cStatus > 0 ? Text(r, cStatus).Trim() : "";
                    var entry = new LedgerEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = EntryKind.BupanOrder,
                        DiscType = dt,
                        Qty = qty,
                        Ref = dedupRef,
                        Source = fileName,
                        Note = "状态:" + status,
                        CreatedAt = ParseDate(cDate > 0 ? sheet.Cell(r, cDate) : null)
                    };
                    g.Entries.Add(entry);
                }
                } // using (wb)
            } // using (fs/ms)
            return result;
        }

        /// <summary>归一化表头：去空白、全角括号转半角，容忍表头里的换行。</summary>
        private static string NormalizeKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return Regex.Replace(raw.Replace('（', '(').Replace('）', ')'), @"\s+", "");
        }

        /// <summary>
        /// 找明细底表：同一「表头行」里同时出现「工单编号」和「耗材种类」两列。
        /// 这两个词分属不同单元格——旧版误写成要求同一个单元格同时含二者，导致永远找不到底表。
        /// 表头行允许在第 1~10 行（兼容顶部带标题行/说明行的导出文件）。
        /// </summary>
        private static IXLWorksheet FindDetailSheet(XLWorkbook wb, out int headerRowNum)
        {
            headerRowNum = 0;
            foreach (var ws in wb.Worksheets)
            {
                var first = ws.FirstRowUsed();
                if (first == null) continue;
                int firstRow = first.RowNumber();
                int maxRow = Math.Min(ws.LastRowUsed().RowNumber(), 10);
                int maxCol = Math.Min(ws.LastColumnUsed().ColumnNumber(), 100);
                for (int r = firstRow; r <= maxRow; r++)
                {
                    bool hasOrder = false, hasType = false;
                    for (int c = 1; c <= maxCol; c++)
                    {
                        var key = NormalizeKey(ws.Cell(r, c).GetString());
                        if (key.Contains("工单编号")) hasOrder = true;
                        if (key.Contains("耗材种类")) hasType = true;
                    }
                    if (hasOrder && hasType)
                    {
                        headerRowNum = r;
                        return ws;
                    }
                }
            }
            return null;
        }

        /// <summary>找不到底表时给出可诊断的信息（工作表名 + 首行表头片段）。</summary>
        private static string DescribeSheets(XLWorkbook wb)
        {
            var sb = new StringBuilder();
            sb.AppendLine("文件内的工作表：");
            foreach (var ws in wb.Worksheets)
            {
                var first = ws.FirstRowUsed();
                var parts = new List<string>();
                if (first != null)
                {
                    int maxCol = Math.Min(ws.LastColumnUsed().ColumnNumber(), 30);
                    for (int c = 1; c <= maxCol && parts.Count < 8; c++)
                    {
                        var v = NormalizeKey(ws.Cell(first.RowNumber(), c).GetString());
                        if (v.Length > 0) parts.Add(v);
                    }
                }
                sb.AppendLine("  [" + ws.Name + "] " + string.Join(" | ", parts));
            }
            return sb.ToString();
        }

        private static DateTime ParseDate(IXLCell cell)
        {
            try
            {
                if (cell == null) return DateTime.Now;
                if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
                var s = cell.GetString();
                DateTime d;
                if (DateTime.TryParse(s, out d)) return d;
            }
            catch { }
            return DateTime.Now;
        }
    }

    /// <summary>
    /// 退款名单 txt 解析器。格式如：
    ///   西平裕阳 6张78元，25张68元。3张30元。
    ///   乐乐11张68元。
    /// 标点不规范（逗号句号混用、可能无分隔符），按 "N张NN元" 模式容错提取。
    /// </summary>
    public static class RefundTextParser
    {
        private static readonly Regex Anchor = new Regex(@"\d+\s*张", RegexOptions.Compiled);
        private static readonly Regex Pair = new Regex(@"(\d+)\s*张\s*(\d+)\s*元", RegexOptions.Compiled);

        public static ImportResult Parse(string[] lines, string sourceName)
        {
            var result = new ImportResult();
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var anchor = Anchor.Match(line);
                if (!anchor.Success)
                {
                    result.UnparsedLines.Add(line);
                    continue;
                }
                var name = line.Substring(0, anchor.Index).Trim(' ', '\t', '，', '。', ',', '.', '：', ':');
                if (name.Length == 0)
                {
                    result.UnparsedLines.Add(line);
                    continue;
                }

                var pairs = Pair.Matches(line);
                if (pairs.Count == 0)
                {
                    result.UnparsedLines.Add(line);
                    continue;
                }

                var g = new ParsedGroup { RawName = name };
                foreach (Match m in pairs)
                {
                    int qty = int.Parse(m.Groups[1].Value);
                    int tier = int.Parse(m.Groups[2].Value);
                    var type = Pricing.TypeFromTier(tier);
                    g.Entries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = EntryKind.RefundCredit,
                        DiscType = type,
                        Qty = qty,
                        Tier = tier,
                        Amount = Pricing.CreditPerDisc(type, tier) * qty,
                        Source = sourceName,
                        Ref = "refund|" + name + "|" + qty + "|" + tier,
                        Note = qty + "张" + tier + "元档"
                    });
                }
                result.Groups.Add(g);
            }
            return result;
        }
    }
}
