# ADR-0010 · Android 端复用桌面纯业务核心，路径与界面依赖注入

- 状态：已实施
- 日期：2026-11-18
- 依据：M1 APK 已能安装并切换四个 Tab；M2 需要让手机端与桌面端共用同一套补盘/退款/抵扣规则

## 背景

M1 的 `src/PanPlugin.Mobile/` 只是四个占位页。M2 要把桌面端已经验证过的业务规则搬到 Android，但不能复制一份逻辑：价格档位、账期去重、归档恢复、别名排除、备份轮转只要出现两份，以后必然漂移。

直接链接桌面源码有三个障碍：

| 文件 | 障碍 |
|---|---|
| `Period.cs` | 纯账期逻辑与 `ImportPeriodForm : Form` 混在同一文件，顶部 `using System.Windows.Forms` |
| `Store.cs` | `Load()` 直接 `MessageBox.Show`；`Dir`/`FilePath`/`BackupDir()` 写死 `%APPDATA%` 与 `AppDomain.BaseDirectory` |
| `Parsers.cs` | `Parse(string path)` 只接受文件路径；Android 文件选择器给的是 `content://` URI 对应的 `Stream` |

## 决策

**物理拆分纯逻辑与界面，源码共享；平台差异通过构造函数/回调注入。**

1. `Period.cs` 只保留 `Period` 静态类（账期推断、校验、补零、`WithPeriod`）。原 `ImportPeriodForm` 移到新的 `Period.UI.cs`，桌面项目仍自动包含该文件，移动端不链接。
2. `Store.cs` 去掉 `System.Windows.Forms` 依赖：
   - 新增 `ErrorReporter` 回调和 `LastLoadError`，桌面 `Program.cs` 把它接到 `MessageBox`，移动端可以忽略。
   - 新增 `Store(string filePath)` 与实例属性 `FilePath` / `BackupDirectory`；默认行为保持桌面原样。
   - 新增 `LoadFrom(filePath)`、`ListBackupsForCurrent()`、`RestoreFromCurrent(backupPath)`；原静态 API 全部保留，桌面调用点零改动。
   - `Save()` 使用 `Path.GetDirectoryName(FilePath)` 建目录，不再假设 `%APPDATA%`。
3. `Parsers.cs` 增加 `BupanExcelParser.Parse(Stream, fileName)` 与 `RefundTextParser.Parse(Stream, sourceName)`。桌面 `Parse(path)` 内部仍用 `FileShare.ReadWrite` 读入内存后转调流入口，Excel 占用兼容性与跨平台规则一致。
4. 新增 `ImportService.cs`，把 `MainForm.CommitImport` 里的账期写入、跨月去重、别名补录、归档客户自动恢复抽成共享规则。客户归属弹窗抽象为 `Func<string, List<Customer>, Customer>`：桌面用 `LinkCustomerForm`，移动端用 MAUI `DisplayActionSheet`/`DisplayPromptAsync`。桌面 `MainForm.CommitImport` 改为调用该服务，行为不变。
5. `PanPlugin.Mobile.csproj` 用 `<Compile Include="../PanPlugin/..." Link="Shared/...">` 链接五个共享文件，并引用 `ClosedXML 0.102.2`、`Newtonsoft.Json 13.0.3`。
6. 移动端 `Services/StoreService.cs` 把 `FileSystem.AppDataDirectory` 注入 `Store`：
   - 账本：`AppDataDirectory/store.json`
   - 备份：`AppDataDirectory/Backups/store-*.json`，最多 3 份
   - 恢复前现场：`before-restore-*.json`，不参与 3 份轮转

## 后果

- 规则只有一份：改价格、账期或去重逻辑，两端同时生效。
- 共享源码必须保持 **C# 7.3 兼容**，因为桌面 `net48` 项目 `LangVersion=7.3`；不能引入 `using var`、switch 表达式、record 等新语法。
- 桌面端默认路径、备份位置、弹窗行为完全不变；需要回归验证桌面 CI 与既有导入测试。
- Android 最低版本仍是 API 24（ADR M1 升级 net10 时确定）；`content://` 流解析需在真机用真实微信文件复验。
- 客户搜索、客户详情和手工记账未包含在 M2，继续留在 M3；本轮只覆盖导入、本地账本、备份恢复与首页概览。
