using System;
using System.Collections.Generic;
using System.Linq;
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
            using (var wb = new XLWorkbook(path))
            {
                IXLWorksheet sheet = FindDetailSheet(wb);
                if (sheet == null)
                    throw new Exception("找不到明细底表：没有哪个工作表包含“申请补偿的耗材种类”表头。");

                var header = new Dictionary<string, int>();
                var headerRow = sheet.FirstRowUsed();
                var lastCol = headerRow.LastCellUsed().Address.ColumnNumber;
                for (int c = 1; c <= lastCol; c++)
                {
                    var raw = sheet.Cell(1, c).GetString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    // 表头可能内含换行/空白（如 "\n处理人"）
                    var key = Regex.Replace(raw, @"\s+", "");
                    header[key] = c;
                }

                int Col(string name)
                {
                    int v;
                    return header.TryGetValue(name, out v) ? v : 0;
                }

                int cOrder = Col("工单编号");
                int cDate = Col("创建日期");
                int cType = Col("申请补偿的耗材种类");
                int cQty = Col("申请补偿的耗材数量");
                int cHospital = Col("提交时装机设备医院(提交时系统匹配)");
                int cStdName = Col("标准医院名称");
                int cStdCode = Col("标准医院代码");
                int cStatus = Col("状态");

                var fileName = System.IO.Path.GetFileName(path);
                var byCustomer = new Dictionary<string, ParsedGroup>(StringComparer.OrdinalIgnoreCase);

                var lastRow = sheet.LastRowUsed().RowNumber();
                for (int r = 2; r <= lastRow; r++)
                {
                    var typeText = sheet.Cell(r, cType).GetString();
                    if (string.IsNullOrWhiteSpace(typeText) && string.IsNullOrWhiteSpace(sheet.Cell(r, cOrder).GetString()))
                        continue; // 空行

                    var dt = DiscTypes.FromExcelName(typeText);
                    if (dt == DiscType.VS2) { result.SkippedVs2++; continue; }
                    if (dt == DiscType.Unknown) { result.SkippedUnknown++; continue; }

                    // 客户名：优先标准医院名称，其次装机医院
                    var name = sheet.Cell(r, cStdName).GetString().Trim();
                    if (string.IsNullOrEmpty(name)) name = sheet.Cell(r, cHospital).GetString().Trim();
                    if (string.IsNullOrEmpty(name)) { result.SkippedUnknown++; continue; }

                    var orderNo = sheet.Cell(r, cOrder).GetString().Trim();
                    var dedupRef = "bupan|" + orderNo;
                    if (orderNo.Length == 0) dedupRef = "bupanrow|" + fileName + "|" + r;

                    int qty = 1;
                    if (cQty > 0)
                    {
                        var q = sheet.Cell(r, cQty).GetString().Trim();
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

                    var status = cStatus > 0 ? sheet.Cell(r, cStatus).GetString().Trim() : "";
                    var entry = new LedgerEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = EntryKind.BupanOrder,
                        DiscType = dt,
                        Qty = qty,
                        Ref = dedupRef,
                        Source = fileName,
                        Note = "状态:" + status,
                        CreatedAt = ParseDate(sheet.Cell(r, cDate))
                    };
                    g.Entries.Add(entry);
                }
            }
            return result;
        }

        private static IXLWorksheet FindDetailSheet(XLWorkbook wb)
        {
            foreach (var ws in wb.Worksheets)
            {
                var headerRow = ws.FirstRowUsed();
                if (headerRow == null) continue;
                var lastCol = headerRow.LastCellUsed().Address.ColumnNumber;
                for (int c = 1; c <= lastCol; c++)
                {
                    var key = Regex.Replace(headerRow.Cell(c).GetString() ?? "", @"\s+", "");
                    if (key.Contains("申请补偿的耗材种类") && key.Contains("工单编号"))
                        return ws;
                }
            }
            return null;
        }

        private static DateTime ParseDate(IXLCell cell)
        {
            try
            {
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
