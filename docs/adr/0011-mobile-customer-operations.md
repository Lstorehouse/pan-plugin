# ADR-0011 · 移动端客户搜索、详情与手工记账复用共享规则

- 状态：已实施
- 日期：2026-11-18
- 依据：M2 APK 已能导入并保存本地账本；下一步需要客户搜索、待补清单与手工增减

## 背景

M2 已把账本与导入规则搬到 Android，但客户页仍是列表占位，无法查看某个客户欠多少张盘、余额多少，也不能在手机上勾掉已补发的工单或手工增减抵扣。

桌面端 `MainForm` 里已有这些规则，但直接复制会造成两端口径分叉：待补盘的判定、历史账目文案、手工条目的 `Kind`/`Amount` 正负、归档自动恢复都要保持一致。

## 决策

**新增 `CustomerService.cs` 作为共享规则层，桌面与移动端同时调用。**

1. 共享查询与聚合：
   - `Search(store, query, includeArchived)`：主名/别名模糊命中，归档默认隐藏，开头命中优先。
   - `PendingOf` / `PendingByType` / `PendingQty`：待补条目只认 `BupanOrder` + `ManualBupan` 且 `!Processed`。
   - `HistoryType` / `HistoryContent` / `MoneyText`：历史账目文案统一，金额放括号内。
2. 共享写入：
   - `MarkProcessed(e)`：只有未补发的条目才改动，避免计数虚高。
   - `NewManualBupan` / `NewManualCredit` / `NewDeduct`：统一 `Id`、`Kind`、`Amount` 正负、`Source="手工"`；新增补盘会调用 `ImportService.ReviveIfArchived` 让归档客户自动恢复。
   - `Archive` / `Unarchive`：只改标记与时间，保留档案、别名和账目。
3. 桌面 `MainForm` 的 `PendingOf`、`PendingByType`、`MoneyText`、`MarkDone`、三种手工记账、归档/恢复改为调用 `CustomerService`，显示与交互不变。
4. 移动端：
   - 客户页用 `SearchBar` + `CollectionView` 展示搜索结果，点击进 `CustomerDetailPage`。
   - 详情页用 `CheckBox` 勾选待补条目，调用 `MarkProcessed`；弹窗收集盘型/张数/金额/备注后调用共享 `New*` 方法。
   - 归档前复用桌面同样的待补/余额警告文案。
5. 桌面专属的转移、撤销、合并、别名管理、恢复备份选择等弹窗仍在 M3 范围外，继续留在桌面端。

## 后果

- 两端对「待补」「余额」「归档」的判定完全一致，后续只改一处。
- 移动端 `CustomerService` 依赖 `ImportService.ReviveIfArchived`，所以 M2 已链接的共享文件集合必须继续保留。
- 客户搜索默认隐藏归档客户；归档客户只有切换「显示已归档」或导入新补盘/手工新增补盘时才会重新出现。
- 撤销、转移账目、别名纠错、合并客户尚未进入移动端，M4 再评估。
