using System;
using System.Drawing;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>
    /// 全局设计令牌：颜色、字阶、间距与组件工厂。所有窗体只从这里取具体数值。
    /// WinForms / net48 / Win7 约束：不用自绘圆角、渐变、阴影、Web 字体，
    /// 用 FlatStyle + 1px 边框 + 4pt 网格近似现代层级。
    /// </summary>
    internal static class Theme
    {
        private static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        // ---------- 颜色（正文色对白底均 ≥ 4.5:1） ----------
        public static readonly Color Primary      = Rgb(0x18, 0x5F, 0xA5);  // 6.52
        public static readonly Color PrimaryHover = Rgb(0x0C, 0x44, 0x7C);
        public static readonly Color PrimaryTint  = Rgb(0xE6, 0xF1, 0xFB);
        public static readonly Color BorderStrong = Rgb(0xB5, 0xD4, 0xF4);
        public static readonly Color Page         = Rgb(0xF6, 0xF8, 0xFA);
        public static readonly Color Surface      = Color.White;
        public static readonly Color Border       = Rgb(0xD8, 0xDE, 0xE6);
        public static readonly Color TextPrimary  = Rgb(0x1F, 0x23, 0x28);  // 15.80
        public static readonly Color TextSecond   = Rgb(0x5F, 0x6B, 0x7A);  // 5.43
        public static readonly Color TextHint     = Rgb(0x8A, 0x94, 0xA0);  // 占位符专用
        public static readonly Color Danger       = Rgb(0xB3, 0x26, 0x1E);  // 6.54
        public static readonly Color DangerTint   = Rgb(0xFC, 0xEB, 0xEB);
        public static readonly Color Success      = Rgb(0x0F, 0x6E, 0x56);  // 6.20
        public static readonly Color SuccessTint  = Rgb(0xE1, 0xF5, 0xEE);
        public static readonly Color DisabledBg   = Rgb(0xD8, 0xDE, 0xE6);
        public static readonly Color DisabledFg   = Rgb(0x8A, 0x94, 0xA0);
        public static readonly Color HoverNeutral = Rgb(0xF1, 0xEF, 0xE8);

        // ---------- 字阶 ----------
        public const string FontName = "Microsoft YaHei UI";
        private static readonly Font _title   = new Font(FontName, 14F, FontStyle.Bold);
        private static readonly Font _section = new Font(FontName, 10.5F, FontStyle.Bold);
        private static readonly Font _body    = new Font(FontName, 9F);
        private static readonly Font _small   = new Font(FontName, 8.5F);
        private static readonly Font _metric  = new Font(FontName, 18F, FontStyle.Bold);

        public static Font Title   { get { return _title; } }
        public static Font Section { get { return _section; } }
        public static Font Body    { get { return _body; } }
        public static Font Small   { get { return _small; } }
        public static Font Metric  { get { return _metric; } }

        // ---------- 间距（4pt 网格） ----------
        public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S6 = 24, S8 = 32;
        public const int RowHeight = 28, ButtonHeight = 30;

        // ---------- 窗体 ----------
        /// <summary>
        /// 主窗体基础样式。刻意不设 AutoScaleMode.Dpi：实测截图是 200% 缩放下
        /// Windows 对整个应用做位图缩放，强行声明 DPI 感知反而会二次缩放导致错位。
        /// </summary>
        public static void ApplyMain(Form f)
        {
            f.Font = Body;
            f.BackColor = Page;
            f.ForeColor = TextPrimary;
            f.MinimumSize = new Size(960, 620);
        }

        /// <summary>对话框统一外观；保留调用方原有的 ClientSize 与绝对布局。</summary>
        public static void ApplyDialog(Form f)
        {
            f.Font = Body;
            f.BackColor = Surface;
            f.ForeColor = TextPrimary;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowIcon = false;
            // 不设 Padding：现有对话框用绝对 Location 布局，改内边距会挪动控件。
        }

        /// <summary>
        /// 对话框构造结束后调用一次：统一窗体外观与按钮层级。
        /// 只改 Button 的样式，不动绝对 Location 与 ClientSize，避免 200% 缩放下错位。
        /// </summary>
        public static void FinishDialog(Form f)
        {
            ApplyDialog(f);
            TuneButtons(f);
        }

        private static void TuneButtons(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var b = c as Button;
                if (b != null)
                {
                    bool primary = b.DialogResult == DialogResult.OK;
                    b.Font = Body;
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.Cursor = Cursors.Hand;
                    if (primary)
                    {
                        b.BackColor = Primary;
                        b.ForeColor = Color.White;
                        b.FlatAppearance.BorderSize = 0;
                        b.FlatAppearance.MouseOverBackColor = PrimaryHover;
                    }
                    else
                    {
                        b.BackColor = Surface;
                        b.ForeColor = TextSecond;
                        b.FlatAppearance.BorderSize = 1;
                        b.FlatAppearance.BorderColor = Border;
                        b.FlatAppearance.MouseOverBackColor = HoverNeutral;
                    }
                }
                if (c.HasChildren) TuneButtons(c);
            }
        }

        // ---------- 按钮 ----------
        /// <summary>
        /// 按钮内 GDI 文字预留：TextRenderer 的字形外延、Flat 按钮内部边距与少量余量。
        /// 旧实现只有左右各 12px 内边距，中文长文案被裁成「导入补盘名」这类半截文字。
        /// </summary>
        private const int TextGutter = 24;

        /// <summary>客户行徽标的固定槽宽（逻辑像素）。</summary>
        public const int BadgeSlot = 92;

        /// <summary>
        /// 用 96 DPI 的内存 DC 量文字，得到「逻辑像素」宽度。
        /// 不能用屏幕 DC：声明 DPI 感知后在 200% 下会量出 2 倍宽，
        /// 而 AutoScaleMode.Dpi 又会整体放大一次，按钮会过宽挤出工具条。
        /// </summary>
        private static int MeasureLogicalText(string text, Font font)
        {
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                return TextRenderer.MeasureText(g, text, font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            }
        }

        /// <summary>按文字实测宽度定宽；传入值退化为「最小宽度」而非固定宽度。</summary>
        public static int ButtonWidthFor(string text, int minWidth)
        {
            return Math.Max(minWidth,
                MeasureLogicalText(text, Body) + S2 * 2 + TextGutter);
        }

        private static Button Base(string text, int minWidth)
        {
            return new Button
            {
                Text = text,
                Width = ButtonWidthFor(text, minWidth),
                Height = ButtonHeight,
                Font = Body,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, S2, 0),
                Padding = new Padding(S2, 0, S2, 0)
            };
        }

        private static void TrackEnabled(Button b, Color on, Color fgOn)
        {
            b.EnabledChanged += delegate
            {
                b.BackColor = b.Enabled ? on : DisabledBg;
                b.ForeColor = b.Enabled ? fgOn : DisabledFg;
            };
        }

        public static Button Filled(string text, int width = 108)
        {
            var b = Base(text, width);
            b.BackColor = Primary;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = PrimaryHover;
            b.FlatAppearance.MouseDownBackColor = PrimaryHover;
            TrackEnabled(b, Primary, Color.White);
            return b;
        }

        public static Button Outlined(string text, int width = 108)
        {
            var b = Base(text, width);
            b.BackColor = Surface;
            b.ForeColor = PrimaryHover;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = BorderStrong;
            b.FlatAppearance.MouseOverBackColor = PrimaryTint;
            TrackEnabled(b, Surface, PrimaryHover);
            return b;
        }

        /// <summary>
        /// 文本按钮。emphasize=true 用主色链接样式：底部操作条原先全用灰蓝色 TextSecond，
        /// 在 200% 位图缩放下看起来像「禁用」，实际可点。
        /// </summary>
        public static Button TextButton(string text, int width = 88, bool emphasize = false)
        {
            var b = Base(text, width);
            var fg = emphasize ? PrimaryHover : TextSecond;
            b.BackColor = Color.Transparent;
            b.ForeColor = fg;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = HoverNeutral;
            TrackEnabled(b, Color.Transparent, fg);
            return b;
        }

        public static Button DangerButton(string text, int width = 120)
        {
            var b = Base(text, width);
            b.BackColor = Surface;
            b.ForeColor = Danger;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Rgb(0xF0, 0x95, 0x95);
            b.FlatAppearance.MouseOverBackColor = DangerTint;
            TrackEnabled(b, Surface, Danger);
            return b;
        }

        /// <summary>筛选胶囊；active 时填充语义色。</summary>
        public static Button Chip(string text, int width)
        {
            var b = Base(text, width);
            b.Height = 22;
            b.Margin = new Padding(0, 0, S1, 0);
            b.Padding = new Padding(S2, 0, S2, 0);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        public static void StyleChip(Button b, bool active, Color activeBack, Color activeFore)
        {
            b.BackColor = active ? activeBack : Surface;
            b.ForeColor = active ? activeFore : TextSecond;
            b.FlatAppearance.BorderColor = active ? activeBack : Border;
            b.FlatAppearance.MouseOverBackColor = active ? activeBack : HoverNeutral;
        }

        // ---------- 容器 ----------
        public static FlowLayoutPanel ToolStrip(int height)
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,          // 禁止换行被固定高度裁掉
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
        }

        public static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text, Font = Section, ForeColor = TextPrimary,
                AutoSize = true, Margin = new Padding(0, S2 + 2, S2, 0)
            };
        }

        public static Label HintLabel(string text)
        {
            return new Label
            {
                Text = text, Font = Small, ForeColor = TextSecond,
                AutoSize = true, Margin = new Padding(S2, S2 + 2, 0, 0)
            };
        }

        /// <summary>指标卡：小标题 + 大数值；通过 out 暴露数值 Label 便于刷新。</summary>
        public static Panel MetricCard(string caption, Color valueColor,
                                      out Label value, out Label captionLabel)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 56,
                BackColor = Surface,
                Padding = new Padding(S3, S1, S3, S1),
                Margin = new Padding(0, 0, S2, 0)
            };
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle,
                    Border, ButtonBorderStyle.Solid);
            };
            value = new Label
            {
                Text = "—", Font = Metric, ForeColor = valueColor,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            var cap = new Label
            {
                Text = caption, Font = Small, ForeColor = TextSecond,
                Dock = DockStyle.Top, Height = 18
            };
            p.Controls.Add(value);
            p.Controls.Add(cap);
            captionLabel = cap;
            return p;
        }

        // ---------- 表格 ----------
        public static void StyleGrid(ListView lv)
        {
            lv.Font = Body;
            lv.BackColor = Surface;
            lv.ForeColor = TextPrimary;
            lv.BorderStyle = BorderStyle.None;
            lv.GridLines = false;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        }

        /// <summary>按权重把列宽铺满可用宽度；Resize 时重算，太窄则交水平滚动条。</summary>
        public static void LayoutColumns(ListView lv, int[] weights, int[] minWidths)
        {
            int n = lv.Columns.Count;
            if (n == 0 || weights.Length != n || minWidths.Length != n) return;
            int avail = lv.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            if (avail <= 0) return;
            int minSum = 0, total = 0;
            for (int i = 0; i < n; i++) { minSum += minWidths[i]; total += weights[i]; }
            if (avail < minSum) avail = minSum;
            int used = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int w = Math.Max(minWidths[i], avail * weights[i] / total);
                lv.Columns[i].Width = w;
                used += w;
            }
            lv.Columns[n - 1].Width = Math.Max(minWidths[n - 1], avail - used);
        }

        /// <summary>表格 + 空状态文案的宿主面板。</summary>
        public static Panel GridHost(ListView lv, out Label empty, string emptyText)
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(0) };
            empty = new Label
            {
                Text = emptyText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Body,
                ForeColor = TextHint,
                BackColor = Surface,
                Visible = false
            };
            host.Controls.Add(lv);
            host.Controls.Add(empty);
            return host;
        }

        // ---------- 客户列表 owner-draw ----------
        /// <summary>从右往左画一个徽标，返回下一个徽标的右边界。</summary>
        public static int DrawBadge(Graphics g, int right, string text,
                                   Color back, Color fore, Rectangle row)
        {
            var sz = TextRenderer.MeasureText(g, text, Small,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int w = sz.Width + S2 * 2, h = sz.Height + S1;
            var r = new Rectangle(right - w, row.Top + (row.Height - h) / 2, w, h);
            using (var b = new SolidBrush(back)) g.FillRectangle(b, r);
            TextRenderer.DrawText(g, text, Small, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
            return r.Left - S1;
        }

        /// <summary>在固定宽度槽位里右对齐画徽标，保证同行两个徽标的位置稳定不跳动。</summary>
        public static void DrawBadgeSlot(Graphics g, int slotRight, int slotWidth,
                                        string text, Color back, Color fore, Rectangle row)
        {
            var sz = TextRenderer.MeasureText(g, text, Small,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int w = Math.Min(slotWidth, sz.Width + S2 * 2);
            int h = sz.Height + S1;
            var r = new Rectangle(slotRight - w, row.Top + (row.Height - h) / 2, w, h);
            using (var b = new SolidBrush(back)) g.FillRectangle(b, r);
            TextRenderer.DrawText(g, text, Small, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }
    }
}
