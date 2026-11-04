# ADR-0008 · 引入 WinForms 可用的设计令牌与信息密度改造

- 状态：已实施
- 日期：2026-11-04
- 依据：`docs/UI-AUDIT.md` 首轮审查 + 第 10 节真实数据截图复核

## 背景

前 7 次迭代每次新增功能都在 `MainForm.cs` 里硬编码颜色、字体、间距与按钮。到实测截图时已暴露：

1. `FlowLayoutPanel` 默认 `WrapContents=true` + 固定高度，窗口变窄时按钮换行被裁掉；底部「手工新增补盘…」在截图里**完全不可见**（功能不可达）。
2. `NewList()` 按列名长度给列宽（`110/70` + 首列强制 160），待补表合计 440px、历史表 370px，而右栏约 660px —— **文字被截断的同时右侧空掉近半宽度**。
3. `MainForm.cs` 仍用 `e.Ref.Substring(6)` 剥 `bupan|`，但 ADR-0003 之后指纹是 `bupan|账期|工单号`，工单号列显示成 `2026-10|20261020001`。
4. 备注列把 `Note`（含 `状态:xxx`）与 `Source` 拼进 70px 格子，只能看到 `状态:…`。
5. 客户列表约 30 行，徽标用全角空格拼在名字后面，起点随名字长度漂移，待补/抵扣等权。
6. 14 个按钮等权；红绿两行小字承载了最关键的「待补盘 / 抵扣余额」。

## 决策

**新增 `src/PanPlugin/Theme.cs`** 作为唯一数值来源：色板（WCAG AA 达标）、5 档字阶、4pt 间距网格、按钮工厂（Filled / Outlined / Text / Danger / Chip）、组件样式与工具方法。所有窗体只从 `Theme` 取值。

具体改造：

- **P0-a 缺陷修复**：`OrderNoOf()` 兼容旧 `bupan|工单号`、新 `bupan|账期|工单号`、`bupanrow|文件|行` 与手工条目；两张表拆列（待补 6 列、历史 5 列），`Theme.LayoutColumns()` 按权重铺满并在 `Resize`/`Shown` 时重算；所有 `FlowLayoutPanel` 设 `WrapContents=false` 或改用 `AutoSize` 操作条。
- **P0-b 层级**：顶部 1 Filled + 1 Outlined + 3 Text + 「更多 ▾」菜单（归档/恢复、合并、转移记录）；`MinimumSize = 960×620`。
- **P1 密度**：两张指标卡、分区标题 + 计数、空状态文案、操作按钮下沉到所属表格；左栏筛选胶囊（全部/待补/有余额，实时计数）+ 待补优先排序；`ListBox` 改 `OwnerDrawFixed`，名称超长省略、徽标右对齐且带语义色。
- **P2 收尾**：6 个对话框 + 2 个内联输入窗体统一走 `Theme.FinishDialog()`；`Color.DimGray` / `Color.Gray` 全部换成 `TextSecond`；状态栏常驻账期/客户数/待补家数/最近备份；`Ctrl+F` 聚焦搜索、`F5` 刷新。

## 刻意不做

- **不设 `AutoScaleMode.Dpi`**：实测截图为 1970×1326 物理像素、逻辑窗口 1000×640，说明应用未声明 DPI 感知、由 Windows 做位图缩放（200% 下整体等比放大、无错位）。此时强行声明 DPI 感知会二次缩放并破坏绝对布局。保留原状，仅通过 `MinimumSize` 与 `WrapContents` 保证小窗口下的可达性。
- 不引入第三方 UI 库（net48 + Win7 + 公开仓库，依赖与授权风险）。
- 不追求真实圆角/阴影/渐变/动效：全部需 owner-draw，收益低于维护成本。
- 不做深色模式：WinForms 原生控件无法系统性换肤。
- 不改业务逻辑、账本结构与 `store.json` 格式。

## 验证

- 本地 net48 响应文件重放编译 `exit 0`。
- **运行时布局探针**：离屏构建 `MainForm` 并导出控件树，确认 12 个按钮全部落在可见区域（含此前消失的「手工新增补盘…」）、两张表列宽合计 619/638px（余 19px 为垂直滚动条预留）、无异常。探针同时抓到并修正了构造期设置 `Panel2MinSize` 导致的 `InvalidOperationException`（改为 `Shown` 后设置）。
- 本地 `bindiag/test_ui.py` 回归（该目录按 `.gitignore` 约定不入库）：新旧指纹工单号解析、列宽铺满且最小宽度生效。
- 本地 `bindiag/` 全部既有行为测试（解析器、账期、归档、别名、备份、转移、恢复）全部通过。

## 参考

- 审查报告与完整设计稿：[docs/UI-AUDIT.md](../UI-AUDIT.md)
