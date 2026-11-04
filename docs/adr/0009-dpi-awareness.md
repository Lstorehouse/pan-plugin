# ADR-0009 · 声明系统 DPI 感知并等比缩放，消除高 DPI 下的字体模糊

- 状态：已实施
- 日期：2026-11-05
- 依据：CI 成功后的真机截图（1956×1326 物理 / 1000×640 逻辑）与用户反馈「字体有些模糊」
- 取代：ADR-0008「刻意不设 `AutoScaleMode.Dpi`」一条

## 背景

ADR-0008 观察到截图物理像素约为逻辑窗口的 2 倍，判断「应用未声明 DPI 感知、由 Windows 位图缩放」，并据此刻意不开 `AutoScaleMode.Dpi`，担心二次缩放破坏绝对布局。该观察正确，但结论混淆了两件不同的事：

| | 作用 | 缺失后果 |
|---|---|---|
| 进程 DPI 感知（`app.manifest` / `SetProcessDPIAware`） | 决定 Windows 是否把窗口当 96 DPI 位图渲染后再放大 | 200% 下 ClearType 次像素边缘被双线性插值摊开，文字整体发虚 |
| WinForms `AutoScaleMode.Dpi` | 在**已感知真实 DPI** 的前提下，把控件几何按 DPI 倍数放大 | 字体会按点数自动变大，但按钮/行高/列宽仍是设备像素，比例失衡、裁字更严重 |

ADR-0008 为了规避第二个风险而连第一个也不做，等于「为了避免缩放而接受模糊」。真机截图放大后能看到红蓝次像素边被糊成灰边，且系统 `AppliedDPI=192`（200% 缩放），确认是 DPI 虚拟化而非 ClearType 关闭。

## 决策

**两者成对引入，不做其一。**

1. 新增 `src/PanPlugin/app.manifest`：
   - `<dpiAware>true</dpiAware>` —— Win7 起支持，系统级 DPI 感知，Win7 只认这个节点。
   - `<dpiAwareness>System</dpiAwareness>` —— Win10 1607+ 认这个节点。
   - `PanPlugin.csproj` 加 `<ApplicationManifest>app.manifest</ApplicationManifest>`。
   - **不用 `PerMonitorV2`**：net48 WinForms 不处理运行时 DPI 变化，跨不同缩放的显示器拖动窗口会残影/错位；系统级感知在本项目「单机桌面插件、单一显示器」场景下足够。
2. `Theme.EnableDpiScaling(Form)` 统一设 `AutoScaleDimensions = (96,96)`、`AutoScaleMode = Dpi`。主窗体在 `BuildUi()` 之后调用，对话框由 `ApplyDialog()`（经 `FinishDialog`）在构造末尾调用，保证子控件已全部创建。
3. `Application.SetCompatibleTextRenderingDefault(false)` 保持不变（GDI / ClearType 本来就是对的）。

### AutoScale 管不到的写死像素

`Scale(px) = Round(px × DeviceDpi / 96)`，`DeviceDpi` 取自桌面 DC（感知后 200% 下为 192，未感知为 96，自动退化为恒等）。需要手动换算的只有以下几类：

- `ListBox.ItemHeight`（owner-draw 属性，不随 AutoScale 缩放）。
- `Theme.LayoutColumns()` 的 `minWidths`：字体变大后最小列宽必须同步放大。
- `SplitContainer.SplitterDistance` 与 `Panel1/2MinSize`：在 `Shown` 里写绝对值，避免构造期设置与 AutoScale 结果不一致。
- owner-draw 里的所有间距、徽标槽宽、选中行左侧色条（`DrawBadge` / `DrawBadgeSlot` / `LstCustomers_DrawItem`）。
- `ToolStripItem.Padding`：状态栏项不是普通 Control，AutoScale 不缩放它。

其余 `Width/Height/Padding/Margin/Location/RowStyles` 都是 Control 属性，由 `AutoScaleMode.Dpi` 统一处理，**不要重复调用 `Scale()`**，否则会二次放大。

### 一个实测踩到的坑

`TextRenderer.MeasureText` 用进程 DPI 把 9pt 字体换算成设备像素：200% 下量出的文字宽度是 96 DPI 的 2 倍，且 `Bitmap.SetResolution(96,96)` **无效**（它不看内存 DC 的分辨率）。如果直接拿这个结果当按钮宽度，`AutoScaleMode.Dpi` 会再放大一次，按钮宽出约 3.3 倍，顶部「更多…」被挤出窗口（探针实测 X=2156 > 2000）。

因此 `Theme.MeasureLogicalText()` 把测量结果除以 `ScaleFactor` 还原成逻辑像素，按钮工厂只产出逻辑宽度，交给 AutoScale 统一放大。`Theme.ButtonWidthFor()` 在 96 / 192 DPI 下都返回 122，探针实测按钮宽度分别为 122 / 244，正好 2 倍。

## 刻意不做

- 不升级 .NET / 不引入 `Application.SetHighDpiMode`（那是 .NET Core 3.0+ 的 API，本项目锁 net48 + Win7）。
- 不处理运行时 DPI 变化（换显示器、拖动到不同缩放的屏幕）；系统级感知下窗口按登录时 DPI 渲染。
- 不改成 `AutoScaleMode.Font`：字阶与控件尺寸会互相牵连，Dpi 模式更可预测。
- 不改业务逻辑、账本结构与 `store.json`。

## 验证

- 本地 net48 响应文件重放编译 `exit 0`；`csc` 带 `/win32manifest:app.manifest` 编译 `exit 0`，manifest XML 解析通过。
- **离屏布局探针（带清单，真实 192 DPI）**：`DeviceDpi=192`、`ScaleFactor=2.00`、窗口 `2000×1280`（100% 下为 `1000×640`）、`AutoScaleFactor=1`（无二次缩放）；`ItemHeight=56`、表格行绝对高度 68/120/56、列宽合计 1240/1276；`SplitterDistance=640`；12 个按钮文字全部容得下，顶部最后一个按钮结束于 1748 < 2000，底部 4 个按钮单行排完；3 个对话框（账期确认、关联客户、别名管理）等比放大为 900×410 / 920×760 / 1080×904 且按钮文字不裁。
- **离屏布局探针（不带清单，96 DPI 基线）**：窗口仍 `1000×640`、按钮宽度与第 1 步完全一致、0 处裁字。
- 本地 8 个行为测试（解析器、账期、归档、别名、备份、转移、恢复、UI）全部通过；`test_ui.py` 新增「12 个按钮无裁字」断言。
- 125% / 150% 未在本地复现（无法伪造系统 DPI），按 `AutoScaleMode.Dpi` 的线性特性推断；CI 产物需在真机四档缩放下人工回归。

## 参考

- `docs/UI-AUDIT.md` 第 11 节（真机截图逐块复核与修复清单）
- ADR-0008（被取代的 DPI 判断）
