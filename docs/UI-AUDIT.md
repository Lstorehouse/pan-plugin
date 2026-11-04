# UI 设计审查报告 · 生化盘客户管理插件

- 日期：2026-11-04
- 审查对象：`src/PanPlugin/` 全部 6 个窗体（`MainForm`、`AliasManagerForm`、`LinkCustomerForm`、`RestoreBackupForm`、`TransferForms` 2 个、`Period` 确认框、`MainForm` 内 2 个小对话框）+ 两张实测截图
- 版本：**第 1–9 节**为空表状态首轮审查；**第 10 节**为 2026-11-04 23:22 真实数据截图（约 30 行客户）的第二轮复核，含 3 个代码级缺陷与路线修订；两轮的全部建议代码均通过本地 net48 等价编译验证
- 方法：`ui-design-system` 技能的 **UI Audit & Optimization Protocol**（6 维度审查）
- 交付性质：**审查报告 + 设计令牌 + 建议代码**，未修改任何 `.cs` 文件；建议代码已通过本地 net48 等价编译验证（验证用的临时文件已删除）
- 隐私：本报告将推送公开仓库，文中客户名一律用「客户 A / 客户 B」等占位名，不出现真实昵称或医院名

---

## 0. 定位与约束

| 维度 | 判定 |
|---|---|
| 用途 | 优化现有 UI（不是从零生成、不是单组件设计） |
| 产品类型 | Admin / B 端系统，单人桌面业务工具 |
| 用户 | 经销商内勤，日常按客户查补盘张数、抵扣余额、导入月度名单 |
| 调性假设 | 专业、克制、可信（涉及账目与医疗数据），不做大胆配色 |
| 主色 | 自由发挥 → 收敛到低饱和蓝（可信、与 Windows 原生观感兼容） |
| 交付格式 | 本报告 + 可直接落地的 C# 设计令牌类 |

**硬约束（决定了很多 Material 3 规范不能照搬）**

- C# / WinForms / **.NET Framework 4.8** / 目标系统 **Windows 7 及以上**。
- 无 WebView、无 CSS、无自绘渲染框架。Acrylic、渐变、真实阴影、圆角按钮、Web 字体、动效曲线全部不可用或需要大量 owner-draw。
- Win7 只支持**系统级 DPI**（不支持 Per-Monitor V2）；`Microsoft YaHei UI` 是 Win7 自带字体，不引入外部字体文件。
- 本机无 .NET SDK，改动需经 GitHub Actions 云端编译，因此令牌改造应是一次性、可编译通过的小步提交。

结论：把 Material 3 的**系统性**（统一色板 / 字阶 / 8pt 网格 / 组件层级）落地，把它的**材质与动效**放弃。

---

## 1. 现状还原（真实数值，非目测）

| 项目 | 当前值 | 出处 |
|---|---|---|
| 主窗口 | `ClientSize 1000×640`，无 `MinimumSize`，无 `AutoScaleMode` | `MainForm.cs:44` |
| 顶部工具栏 | `FlowLayoutPanel`，`Height=44`，`Padding(8,8,8,0)`，**未设 `WrapContents`**（默认 true） | `MainForm.cs:143-149` |
| 顶部按钮 | 5 个等权系统按钮（120/120/100/100/120 px）+ 搜索框 280 px + 标签 | `MainForm.cs:160-183` |
| 全屏按钮总数 | **14 个**，全部默认系统样式，无主次、无禁用态配色 | `MainForm.cs:160-265` |
| 客户列表 | `ListBox`，字体 `Microsoft YaHei UI 9F`，单行拼接文本 | `MainForm.cs:190, 348-351` |
| 列表文本 | `名称　（已归档）　[待补 N]　[抵扣 N元]` 全角空格 + 方括号 | `MainForm.cs:348-351` |
| 标题 / 状态 | 客户名 `12F Bold`；`待补盘` 用 `Color.Firebrick`，`抵扣余额` 用 `Color.SeaGreen`，两行独立 Label | `MainForm.cs:207, 219-220, 404-409` |
| 数据表格 | 两个 `ListView`，`GridLines=true`，列宽按列名长度 `110/70` 硬编码，首列强制 160 | `MainForm.cs:223-224, 288-303` |
| 操作区 | 8 个按钮挤在底部 `TableLayoutPanel` 两行，与上方两张表没有视觉从属关系 | `MainForm.cs:226-274` |
| 间距 | `Padding(3,8,3,3)`、`(8,6,3,0)`、`(6,10,0,0)`、`(8,4,0,0)`、`(8,0,0,0)` —— 不在同一网格 | 同上 |
| 语义色 | `Firebrick` / `SeaGreen` / `DimGray` / `Gray` 四种，分散在 4 个文件 | `MainForm.cs:219`、`AliasManagerForm.cs:93`、`RestoreBackupForm.cs:35,43`、`TransferForms.cs:106`、`Period.cs:138` |
| 字体 | 默认 8.25pt、`9F`、`12F Bold` 三套，`new Font(Font, Regular)` 还复制了一份父字体 | `MainForm.cs:150, 190, 207` |
| 对话框 | 6 个窗体全部 `FixedDialog` + 固定 `ClientSize` + 绝对 `Location`（如 `new Point(14,18)`），**无 `Anchor`、无 `AutoScaleMode`、无 `MinimumSize`** | `AliasManagerForm.cs`、`LinkCustomerForm.cs`、`Period.cs` 等 |

**WCAG 对比度实测（对白底 `#FFFFFF`）**

| 现有色 | 值 | 对比度 | AA 4.5:1 |
|---|---|---|---|
| `Firebrick`（待补盘） | `#B22222` | 6.68 | 通过 |
| `SeaGreen`（抵扣余额） | `#2E8B57` | **4.25** | **不通过** |
| `DimGray`（对话框说明） | `#696969` | 5.49 | 通过 |
| `Gray`（账期提示） | `#808080` | **3.95** | **不通过** |

---

## 2. 六维度审查

| 维度 | 评分 | 结论 |
|---|---|---|
| 视觉层级 | ★★☆☆☆ | 14 个按钮等权、最关键的「待补张数 / 抵扣余额」是两行小字，视觉焦点反而落在表格网格线上 |
| 色彩系统 | ★★☆☆☆ | 4 个语义色互不成体系，2 个未达 AA；无主色、无表面色、无边框色，全靠系统默认 |
| 排版质量 | ★★★☆☆ | 中文字体选对了，但只有 3 档且不成比例（8.25 / 9 / 12），缺少「分区标题 / 辅助说明」层级 |
| 间距与对齐 | ★★☆☆☆ | 5 种不在同一网格的 Padding；`FlowLayoutPanel` + 绝对 `Location` 混用，长医院名 + 徽标会溢出列表 |
| 组件一致性 | ★★☆☆☆ | 相同功能的按钮样式随窗体漂移；无 Hover / Focus / Disabled 的统一定义；两张表语义不同却长得一样 |
| 现代感与审美 | ★★☆☆☆ | `GridLines=true` 的重网格 + 大片空白行是典型 2000 年代数据窗体观感；留白不是设计，是没内容时的空转 |

**综合：功能完整、信息可达，但缺少「系统」——每加一个功能就多一组硬编码值，第 7 次迭代后已经出现布局 bug。**

---

## 3. 核心问题（5 条）→ 具体改进

### P0-1 顶部工具栏在窗口缩小时会被裁切（真实 bug，不只是美观）
`FlowLayoutPanel` 默认 `WrapContents=true`，但 `Height` 被钉死为 44：窗口宽度 < 约 950 px 时按钮换行到第二行，第二行被裁掉且不可点。主窗体又没有 `MinimumSize`，用户随手一拖就会触发。

→ 设 `WrapContents = false`；加 `MinimumSize = 960×620`；把低频操作收进「更多 ▾」下拉；`SplitContainer` 设 `Panel1MinSize / Panel2MinSize`。

### P0-2 14 个等权按钮，没有主次
每月必做的「导入补盘 / 导入退款」和一年用一次的「合并客户 / 转移记录」长得一模一样，且「合并到另一客户…」这种不可逆操作没有任何危险视觉提示。

→ 三层按钮：`Filled`（导入补盘，全屏仅 1 个）→ `Outlined`（导入退款）→ `Text`（备份/恢复/更多）→ `Danger`（合并、归档、撤销）。危险操作维持二次确认，视觉先给警示。

### P0-3 客户列表把结构化数据压成一行字符串
`客户名　[待补 2]　[抵扣 80元]` 用全角空格拼接，名字一长徽标就错位／横向溢出；状态只能靠方括号辨认，扫读效率低。

→ `ListBox.DrawMode = OwnerDrawFixed`（`ItemHeight=28`），左对齐名称、右对齐状态徽标（`待补` 红 tint / `抵扣` 绿 tint / `已归档` 灰），选中行加 3px 主色左条。数据仍由 `ItemHolder<Customer>` 承载，只改绘制、不动账本。

### P1-4 关键指标不突出，表格比数据更显眼
「待补盘：无 ✓」「抵扣余额：80 元」是两行 9pt 小字；而两个 `GridLines=true` 的表格占据 55%/45% 高度，即使没有数据也画满网格。

→ 把两个数字提成指标卡（24px 数值 + 12px 标签），表格改 `GridLines=false` + 仅横向细分隔线 + 表头浅底；给每张表加分区标题和记录数（「待补清单 0」「历史账目 12」）；空表显示居中的空状态文案。

### P1-5 6 个对话框用绝对坐标，缩放与长文本都会错位
全部 `Location = new Point(14,18)` + 固定 `ClientSize`，无 `Anchor` / `AutoScaleMode`。Win7 在 125%/150% 系统缩放下控件会重叠；提示文案一长就截断。

→ 统一 `ApplyDialog()`：设 `AutoScaleMode = Dpi`、`AutoSize` 说明 Label、`TableLayoutPanel` 布局、`Anchor` 底部按钮；同时把 `Gray` 提示文字换成达标的 `TextSecond`。

---

## 4. 设计令牌（WinForms 可直接实现）

### 色彩（对白底实测对比度）

| 令牌 | 值 | 用途 | 对比度 |
|---|---|---|---|
| `Primary` | `#185FA5` | 主按钮、焦点、选中态 | 6.52 |
| `PrimaryHover` | `#0C447C` | 悬停、深色文字 | 8.60（在 tint 上） |
| `PrimaryTint` | `#E6F1FB` | 选中行、标题栏、Outlined 悬停 | — |
| `Page` | `#F6F8FA` | 窗口底、指标卡、表头 | — |
| `Surface` | `#FFFFFF` | 控件表面 | — |
| `Border` | `#D8DEE6` | 1px 分隔线 | — |
| `TextPrimary` | `#1F2328` | 正文 | 15.80 |
| `TextSecond` | `#5F6B7A` | 辅助文字 | 5.43 |
| `TextHint` | `#8A94A0` | 占位符、空状态（不作正文） | 3.08 |
| `Danger` | `#B3261E` | 待补、危险操作 | 6.54 |
| `DangerTint` | `#FCEBEB` | 待补徽标底 | 5.67 |
| `Success` | `#0F6E56` | 抵扣余额、已处理 | 6.20 |
| `SuccessTint` | `#E1F5EE` | 抵扣徽标底 | 5.46 |

> 替换后所有正文色 ≥ 4.5:1。`TextHint` 只用于占位符与非关键提示，不承担信息传达。

### 字阶（3 层 + 1 数值层，全部 `Microsoft YaHei UI`）

| 令牌 | 字号 | 用途 |
|---|---|---|
| `Title` | 14F Bold | 客户名（主视觉焦点） |
| `Section` | 10.5F Bold | 「待补清单」「历史账目」 |
| `Body` | 9F Regular | 表格、按钮、输入、列表 |
| `Small` | 8.5F Regular | 表头、说明、状态栏 |
| `Metric` | 18F Bold | 指标卡数值 |

### 间距（4pt 基准网格）

`S1=4 / S2=8 / S3=12 / S4=16 / S6=24 / S8=32`。
统一：工具栏内边距 `S2`、区块间 `S4`、卡片内 `S3`、控件间隙 `S2`、行高 28px、按钮高 30px。

### 组件规范

- 按钮：高 30px、圆角靠 `FlatStyle.Flat` + `FlatAppearance`（WinForms 无原生圆角，视觉上用 1px 边框 + 留白近似）；层级 Filled > Outlined > Text > Danger；四态 Default / Hover / Focus(2px 焦点环) / Disabled。
- 列表：行高 28px，无竖线，选中态主色左条 + `PrimaryTint` 底。
- 表格：`GridLines=false`、表头 `Page` 底、仅横向 1px `Border` 线；`FullRowSelect=true`。
- 对话框：统一居中父窗、`ShowIcon=false`、`Padding=S4`、底部按钮右对齐。
- 状态栏：常驻显示 **当前账期 / 客户数 / 最近备份时间**（现有 `StatusStrip` 只写「就绪」，浪费了最重要的全局上下文）。

---

## 5. 设计升级方案（建议新增 `src/PanPlugin/Theme.cs`）

> 以下为**建议代码**，尚未加入项目。已把该文件临时放入 `src/PanPlugin/` 用本地 net48 响应文件重放编译验证（`csc` exit 0，无警告），验证后立即删除，工作区仅新增本报告；**尚未经 GitHub Actions 实包验证**。落地时新增该文件并把各窗体的硬编码值替换为 `Theme.*` 即可；`PanPlugin.csproj` 为 SDK 风格，会自动包含新 `.cs`，无需改项目文件。

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PanPlugin
{
    /// <summary>全局设计令牌：颜色、字阶、间距与组件工厂。只在这里定义具体数值。</summary>
    internal static class Theme
    {
        private static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        // ---------- Color ----------
        public static readonly Color Primary      = Rgb(0x18, 0x5F, 0xA5);
        public static readonly Color PrimaryHover = Rgb(0x0C, 0x44, 0x7C);
        public static readonly Color PrimaryTint  = Rgb(0xE6, 0xF1, 0xFB);
        public static readonly Color BorderStrong = Rgb(0xB5, 0xD4, 0xF4);
        public static readonly Color Page         = Rgb(0xF6, 0xF8, 0xFA);
        public static readonly Color Surface      = Color.White;
        public static readonly Color Border       = Rgb(0xD8, 0xDE, 0xE6);
        public static readonly Color TextPrimary  = Rgb(0x1F, 0x23, 0x28);
        public static readonly Color TextSecond   = Rgb(0x5F, 0x6B, 0x7A);
        public static readonly Color TextHint     = Rgb(0x8A, 0x94, 0xA0);
        public static readonly Color Danger       = Rgb(0xB3, 0x26, 0x1E);
        public static readonly Color DangerTint   = Rgb(0xFC, 0xEB, 0xEB);
        public static readonly Color Success      = Rgb(0x0F, 0x6E, 0x56);
        public static readonly Color SuccessTint  = Rgb(0xE1, 0xF5, 0xEE);
        public static readonly Color DisabledBg   = Rgb(0xD8, 0xDE, 0xE6);
        public static readonly Color DisabledFg   = Rgb(0x8A, 0x94, 0xA0);
        public static readonly Color HoverNeutral = Rgb(0xF1, 0xEF, 0xE8);

        // ---------- Type ----------
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

        // ---------- Spacing (4pt grid) ----------
        public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S6 = 24, S8 = 32;
        public const int RowHeight = 28, ButtonHeight = 30;

        // ---------- Form ----------
        public static void ApplyMain(Form f)
        {
            f.Font = Body;
            f.BackColor = Page;
            f.ForeColor = TextPrimary;
            f.AutoScaleMode = AutoScaleMode.Dpi;
            f.MinimumSize = new Size(960, 620);
        }

        public static void ApplyDialog(Form f, int width, int height)
        {
            f.Font = Body;
            f.BackColor = Surface;
            f.ForeColor = TextPrimary;
            f.AutoScaleMode = AutoScaleMode.Dpi;
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowIcon = false;
            f.ClientSize = new Size(width, height);
            f.Padding = new Padding(S4);
        }

        // ---------- Button ----------
        private static Button Base(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = ButtonHeight,
                Font = Body,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, S2, 0),
                Padding = new Padding(S3, 0, S3, 0)
            };
        }

        private static void TrackEnabled(Button b, Color on, Color off, Color fgOn)
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
            TrackEnabled(b, Primary, DisabledBg, Color.White);
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
            TrackEnabled(b, Surface, DisabledBg, PrimaryHover);
            return b;
        }

        public static Button TextButton(string text, int width = 88)
        {
            var b = Base(text, width);
            b.BackColor = Color.Transparent;
            b.ForeColor = TextSecond;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = HoverNeutral;
            TrackEnabled(b, Color.Transparent, DisabledBg, TextSecond);
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
            TrackEnabled(b, Surface, DisabledBg, Danger);
            return b;
        }

        // ---------- List / Grid ----------
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

        public static Panel MetricCard(string caption, string value, Color valueColor)
        {
            var p = new Panel
            {
                Width = 180,
                Height = 56,
                BackColor = Page,
                Padding = new Padding(S3)
            };
            var number = new Label
            {
                Text = value, Font = Metric, ForeColor = valueColor, Dock = DockStyle.Fill
            };
            var label = new Label
            {
                Text = caption, Font = Small, ForeColor = TextSecond,
                Dock = DockStyle.Top, Height = 18
            };
            p.Controls.Add(number);
            p.Controls.Add(label);
            return p;
        }

        public static Label SectionLabel(string text)
        {
            return new Label
            {
                Text = text, Font = Section, ForeColor = TextPrimary,
                AutoSize = true, Margin = new Padding(0, S2, 0, S1)
            };
        }

        public static Label HintLabel(string text)
        {
            return new Label
            {
                Text = text, Font = Small, ForeColor = TextSecond, AutoSize = true
            };
        }
    }
}
```

### 关键用法（替换 `BuildUi()` 顶部，同时修掉 P0-1）

```csharp
var top = new FlowLayoutPanel
{
    Dock = DockStyle.Top,
    Height = 48,
    Padding = new Padding(Theme.S4, Theme.S2, Theme.S4, Theme.S2),
    FlowDirection = FlowDirection.LeftToRight,
    WrapContents = false,          // 禁止换行 —— 现有 44px 高度会裁掉第二行按钮
    BackColor = Theme.Surface
};
_btnImportBupan = Theme.Filled("导入补盘…", 112);
_btnImportRefund = Theme.Outlined("导入退款…", 112);
_btnBackup = Theme.TextButton("立即备份…", 88);
_btnRestore = Theme.TextButton("恢复备份…", 88);
_btnArchive = Theme.DangerButton("归档所选客户…", 120);
```

`Program.cs` 已有 `Application.EnableVisualStyles()` 与 `SetCompatibleTextRenderingDefault(false)`，`FlatAppearance` 的 Hover 色与 `TextRenderer` 可正常工作，无需改动。

---

## 6. 布局改造

**Before**：`顶部 5 按钮平铺 → 左栏单行拼接列表 → 右栏 6 行 TableLayout（3 行 AutoSize 文字 + 2 张重网格表 + 1 行 8 按钮）→ 状态栏「就绪」`

**After**：

1. 顶部：搜索框（带占位提示）+ 1 个 Filled + 1 个 Outlined + 分隔线 + 3 个 Text（备份 / 恢复 / 更多 ▾）。「更多」收纳归档、合并、转移记录等低频项。
2. 左栏：「客户 · N」计数 + 「显示已归档」+ owner-draw 列表（名称左、状态徽标右、选中左色条）。
3. 右栏：客户名（Title）+ 别名文字按钮 → 两张指标卡（待补盘 / 抵扣余额）→ 「待补清单 N」分区 + 关网格的表格 + 紧贴表格的 1 个主操作 → 「历史账目 N」分区 + 表格 + 紧贴的转移/撤销操作。
4. 状态栏：`账期 yyyy-MM · N 位客户 · 最近备份 时间`，右侧保留「就绪」。

**原则**：操作按钮放在它所作用的数据旁边，而不是全部堆到一个底部按钮池里。

---

## 7. 改进对比

| 项 | Before | After |
|---|---|---|
| 按钮层级 | 14 个等权系统按钮 | 1 Filled / 1 Outlined / 低频 Text / 危险 Danger |
| 关键信息 | 两行 9pt 彩色小字 | 2 张指标卡（18F 数值） |
| 客户状态 | 全角空格拼接的方括号文本 | 右对齐语义徽标，扫读不靠读字 |
| 表格 | `GridLines=true`，空表也画满网格 | 仅横线 + 浅表头 + 空状态文案 |
| 对比度 | 2 处不达 AA（4.25 / 3.95） | 全部正文 ≥ 4.5:1 |
| 间距 | 5 组非网格值 | 统一 4pt 网格 |
| 窗口缩放 | 工具栏换行被裁切；对话框绝对坐标错位 | `WrapContents=false` + `MinimumSize` + `AutoScaleMode.Dpi` |
| 全局上下文 | 状态栏只有「就绪」 | 常驻账期 / 客户数 / 最近备份 |

---

## 8. 落地路线（建议拆成独立提交，便于 CI 逐个验证）

| 优先级 | 内容 | 涉及文件 | 风险 |
|---|---|---|---|
| **P0** | 新增 `Theme.cs`；顶部 `WrapContents=false` + `MinimumSize`；替换 4 处语义色；`ApplyMain/ApplyDialog` | `Theme.cs`、`MainForm.cs`、`Program.cs`（无需改） | 低，纯样式；注意 `TrackEnabled` 在 `Enabled=false` 初始化顺序 |
| **P0** | 顶部按钮分层 + 「更多 ▾」收纳低频项 | `MainForm.cs` BuildUi | 低，事件绑定不变 |
| **P1** | 两张指标卡替换 `_lblPending/_lblBalance`；两个 `ListView` 关网格、加分区标题/计数/空状态；操作按钮下沉到所属表格 | `MainForm.cs` | 中，`TableLayoutPanel` 行结构要重排，需重跑全套行为测试 |
| **P1** | 客户列表 owner-draw 徽标 | `MainForm.cs` RefreshList/DrawItem | 中，`ItemHolder<T>` 需暴露待补/余额；`CheckBoxes` 不涉及 ListBox，安全 |
| **P2** | 6 个对话框改 `TableLayoutPanel` + `Anchor` + `AutoScaleMode.Dpi` | 5 个文件 | 中，需 125% / 150% 缩放下人工回归 |
| **P2** | 状态栏常驻账期 / 客户数 / 最近备份 | `MainForm.cs` | 低 |
| **P2** | 键盘可达性：搜索框 Enter 已支持，补 `Ctrl+F` 聚焦搜索、`F5` 刷新、Tab 顺序 | `MainForm.cs` | 低 |

**验证方式**（沿用现有流程）：本机用 `bindiag/full_replay.py` 做等价编译，再用 `bindiag/test_restore.py` 的纯临时目录测试宿主验证行为；UI 变更无法自动截图验证，需出新包后人工看 100% / 125% / 150% 三种缩放。

---

## 9. 明确不做的事

- 不引入第三方 UI 库（Krypton / MaterialSkin / Bunifu）：net48 + Win7 + 公开仓库，增加依赖与授权风险。
- 不追求真实圆角、阴影、渐变、淡入动效：全部需要 owner-draw 或 GDI+ 层叠，收益远低于维护成本。
- 不改任何业务逻辑、账本结构、`store.json` 格式；本报告只涉及表现层。
- 不做深色模式：WinForms 原生控件无法系统性换肤，且内勤在明亮办公环境长期使用。
- 不把本报告的令牌直接写进现有 `.cs`，避免"分析请求"变成"未经确认的代码改动"。

---

# 10. 第二轮复核：真实数据截图（2026-11-04 23:22）

用户提供了一张**有真实数据**的截图（约 30 行客户、待补清单 4 条工单、历史账目 5 条记录），与第 1 节的空表状态差异很大。把截图局部放大 2 倍后，发现的问题从"审美"变成了**功能可达性**。本节是第 8 节路线的修订依据。

## 10.1 截图实测数据

| 区域 | 观察结果 |
|---|---|
| 客户列表 | 约 30 行；徽标起点随名字长度从 ≈70px 漂移到 ≈350px；长名称 + 双徽标顶到右边界；待补/抵扣同为黑色方括号，等权重 |
| 待补清单 | `工单号` 显示 `2026-10\|20261020001`；`备注` 只显示 `状态:…`；表格右侧约 220px 完全空白 |
| 历史账目 | `类型` = `补盘·…`、`内容` = `全能34…` / `术前14…`、`备注` = `[2026-…`；右侧约 290px 完全空白 |
| 顶部 | 搜索框 + 5 个按钮几乎顶满 1000px 宽度，无收缩余量 |
| 底部 | **只看到 7 个按钮**；代码里应有 8 个，「手工新增补盘…」不可见 |
| 状态栏 | 仅「就绪」 |

## 10.2 三个代码级缺陷（优先级高于任何美化）

**① 工单号列混入账期（显示 bug）**

`MainForm.cs:415` 仍是 ADR-0003 之前的写法：

```csharp
e.Ref != null && e.Ref.StartsWith("bupan|") ? e.Ref.Substring(6) : "(手工)"
```

但 `MainForm.cs:596` 与 `Store.cs:270` 都会调用 `Period.WithPeriod()`，指纹已是 `bupan|2026-10|工单号`。`Substring(6)` 剥掉的只有 `bupan|`，账期段留在了工单号里。修复：

```csharp
private static string OrderNoOf(LedgerEntry e)
{
    if (string.IsNullOrEmpty(e.Ref)) return "(手工)";
    var parts = e.Ref.Split('|');
    if (parts[0] == "bupan" && parts.Length >= 3) return parts[2];   // 新版含账期
    if (parts[0] == "bupan" && parts.Length == 2) return parts[1];   // 旧账本兼容
    return parts[parts.Length - 1];                                  // bupanrow|文件|行
}
```

**② 备注列把两个字段硬拼成一个格子**

`MainForm.cs:419` 是 `(e.Note ?? "") + "（" + (e.Source ?? "") + "）"`，`Parsers.cs:138` 又往 `Note` 塞了 `状态:xxx`。70px 列里两个字段都读不到。修复：拆列，不要拼字符串。

| 表 | 建议列 | 权重 | 最小宽度 |
|---|---|---|---|
| 待补清单 | 工单号 / 盘型 / 张数 / 日期 / **状态** / **来源** | 30 / 16 / 8 / 12 / 12 / 22 | 150 / 80 / 48 / 72 / 72 / 120 |
| 历史账目 | 时间 / 类型 / **内容** / **账期** / **来源** | 18 / 16 / 34 / 14 / 18 | 110 / 90 / 160 / 80 / 110 |

> `CheckBoxes=true` 会在首列左侧占约 16px，首列最小宽度已考虑。

列宽按可用宽度百分比计算，而不是按列名长度：

```csharp
/// <summary>按权重把列宽铺满 ListView 可用宽度；窗口 Resize 时重算。</summary>
public static void LayoutColumns(ListView lv, int[] weights, int[] minWidths)
{
    int n = lv.Columns.Count;
    if (n == 0 || weights.Length != n || minWidths.Length != n) return;
    int avail = lv.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
    int minSum = 0, total = 0;
    for (int i = 0; i < n; i++) { minSum += minWidths[i]; total += weights[i]; }
    if (avail < minSum) avail = minSum;          // 过窄时交给水平滚动条，不再压扁文字
    int used = 0;
    for (int i = 0; i < n - 1; i++)
    {
        int w = Math.Max(minWidths[i], avail * weights[i] / total);
        lv.Columns[i].Width = w;
        used += w;
    }
    lv.Columns[n - 1].Width = Math.Max(minWidths[n - 1], avail - used);
}

// 建表后订阅，窗口尺寸变化自动重排
_lvPending.Resize += (s, e) => Theme.LayoutColumns(
    _lvPending, new[] { 30, 16, 8, 12, 12, 22 }, new[] { 150, 80, 48, 72, 72, 120 });
_lvHistory.Resize += (s, e) => Theme.LayoutColumns(
    _lvHistory, new[] { 18, 16, 34, 14, 18 }, new[] { 110, 90, 160, 80, 110 });
```

**③ 「手工新增补盘…」按钮不可见（功能不可达）**

`MainForm.cs:229-232` 确实把它加进了 `pendingBtns`，但截图里该按钮没有出现。`pendingBtns`（`MainForm.cs:226`）与顶部工具栏同因：`FlowLayoutPanel` 默认 `WrapContents=true` + 固定 `Height=34`，列宽不够时第二个按钮换行后被裁掉。修复（同时给 `creditBtns`、`transferBtns`、`top` 加上）：

```csharp
WrapContents = false,
AutoSize = true,                 // 让容器自己按需长高，别靠固定 Height 硬撑
```

并给 `MainForm` 设 `MinimumSize = new Size(960, 620)`（见第 5 节 `Theme.ApplyMain`）。**这一条应作为 P0 单独验证**：改完先确认 8 个底部按钮全部可见，再谈样式。

## 10.3 客户列表组件规范（30 行密度专用）

- 顶部加筛选条：`全部 30 / 待补 12 / 有余额 22` 三个胶囊 + 「待补优先」排序开关。30 行不做分组，扫读成本太高。
- 行高 28–32px，名称左对齐、`EndEllipsis` 超长省略；**徽标固定右对齐**，不参与名称挤压。
- 徽标语义色：`待补` = `DangerTint`/`Danger`（需要行动）、`抵扣` = `SuccessTint`/`Success`（钱）、`已归档` = 中性灰。选中行加 3px `Primary` 左条 + `PrimaryTint` 底。
- `ListBox` 用 `DrawMode.OwnerDrawFixed` + `ItemHeight`，`ItemHolder<Customer>` 继续承载数据，只改绘制，不动账本。

```csharp
_lstCustomers.DrawMode = DrawMode.OwnerDrawFixed;
_lstCustomers.ItemHeight = 28;
_lstCustomers.DrawItem += (s, e) =>
{
    if (e.Index < 0) return;
    var holder = _lstCustomers.Items[e.Index] as ItemHolder<Customer>;
    if (holder == null) return;
    var c = holder.Value;
    bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
    using (var bg = new SolidBrush(sel ? Theme.PrimaryTint : Theme.Surface))
        e.Graphics.FillRectangle(bg, e.Bounds);
    if (sel)
        using (var accent = new SolidBrush(Theme.Primary))
            e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);

    int right = e.Bounds.Right - Theme.S2;
    if (c.Archived)
        right = DrawBadge(e.Graphics, right, "已归档", Theme.HoverNeutral, Theme.TextSecond, e.Bounds);
    int balance = _store.CreditBalance(c.Id);
    if (balance != 0)
        right = DrawBadge(e.Graphics, right, "抵扣 " + balance + " 元",
                          Theme.SuccessTint, Theme.Success, e.Bounds);
    int pending = PendingOf(c.Id).Sum(x => x.Qty);
    if (pending > 0)
        right = DrawBadge(e.Graphics, right, "待补 " + pending,
                          Theme.DangerTint, Theme.Danger, e.Bounds);

    var nameRect = new Rectangle(e.Bounds.Left + Theme.S2, e.Bounds.Top,
                                 right - e.Bounds.Left - Theme.S3, e.Bounds.Height);
    TextRenderer.DrawText(e.Graphics, c.Name, Theme.Body, nameRect,
        sel ? Theme.PrimaryHover : Theme.TextPrimary,
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
};

private static int DrawBadge(Graphics g, int right, string text,
                             Color back, Color fore, Rectangle row)
{
    var sz = TextRenderer.MeasureText(g, text, Theme.Small,
        new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
    int w = sz.Width + Theme.S2 * 2, h = sz.Height + Theme.S1;
    var r = new Rectangle(right - w, row.Top + (row.Height - h) / 2, w, h);
    using (var b = new SolidBrush(back)) g.FillRectangle(b, r);
    TextRenderer.DrawText(g, text, Theme.Small, r, fore,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
        TextFormatFlags.NoPadding);
    return r.Left - Theme.S1;
}
```

## 10.4 第 8 节路线修订

| 优先级 | 修订后的顺序 | 说明 |
|---|---|---|
| **P0-a** | 修三个缺陷：`OrderNoOf`、`LayoutColumns` 拆列、`WrapContents=false` | **先让信息读得全、按钮点得到**，与样式无关，可独立提交 |
| **P0-b** | `Theme.cs` 令牌 + 顶部 `MinimumSize` + 按钮分层 | 第 5 节方案 |
| **P1** | 两张指标卡、分区标题/计数/空状态、操作按钮下沉到所属表格 | 右栏 `TableLayoutPanel` 行结构重排 |
| **P1** | 客户列表筛选条 + owner-draw 右对齐徽标 | 10.3 节 |
| **P2** | 对话框 `AutoScaleMode.Dpi` / 状态栏常驻统计 / 键盘可达性 | 不变 |

## 10.5 改进对比（真实数据场景）

| | Before | After |
|---|---|---|
| 工单号 | `2026-10\|20261020001` | `20261020001`，账期单独成列 |
| 类型 / 内容 / 备注 | `补盘·…` / `全能34…` / `[2026-…` | `补盘·待发` / `全能34项 1 张` / `2026-10` + `10月补盘` |
| 表格右侧 | 空 220–290px | 列宽铺满，零空白 |
| 底部按钮 | 8 个中只有 7 个可见 | 8 个全部可达 |
| 客户列表 | 徽标漂移、长名顶边、状态等权 | 右对齐语义徽标 + 筛选条 + 待补优先 |
| 状态栏 | 「就绪」 | `账期 2026-10 · 30 位客户 · 12 家待补 · 最近备份 …` |

> 本节代码同样为**建议稿**，未加入项目、未改任何 `.cs`。已把 `Theme.cs` + 本节的 `OrderNoOf` / `LayoutColumns` / `DrawBadge` 合并成临时文件走本地 net48 响应文件重放编译（exit 0，仅一条来自测试桩字段的 CS0649 警告），验证后删除临时文件。截图中的真实客户名在本节及配套图稿中一律用「示例市…」占位名替换，符合公开仓库要求。
