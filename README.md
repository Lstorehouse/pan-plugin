# 生化盘客户管理插件

Win7+ 单机桌面程序：读取每月**补盘名单（xlsx）**与**退款名单（txt）**，按客户搜索查询补盘与抵扣情况，本地记账。源数据文件只读（ADR-0001）。

## 使用方法

1. 双击打开 `PanPlugin.exe`（Win7 需先安装 [.NET Framework 4.8 运行时](https://dotnet.microsoft.com/download/dotnet-framework/net48)，Win10/11 已内置）。
2. 顶部 **导入补盘名单…** 选 xlsx（自动识别明细底表，VS2 综合盘自动跳过）。
3. **导入退款名单…** 选 txt（"xx客户 5张78元，3张68元"格式自动解析，档位折算抵扣：78→10 元/张、68→20 元/张、30→8 元/张）。
4. 每次导入会弹出**账期确认**：显示从文件名识别出的月份（如 `2026-10`），请核对后再点"确认导入"；改月份可手工输入，取消则不写入任何数据。账期相同才判定重复导入，因此换月导入不会漏账、误选上月文件也能拦住（见 [ADR-0003](./docs/adr/0003-import-period-in-dedup.md)）。
5. 搜不到客户时按提示**关联到已有客户**（名字存为别名，下次自动识别）或**新建客户**。
6. 客户详情：待补盘按盘型分列张数；补发后勾选 → "标记已补发"；抵扣可手工增减、可撤销。抵扣余额与待补盘是**跨月累计**值，不按月清零。
7. 本地数据在 `%APPDATA%\PanPlugin\store.json`（客户主库 + 调整账本）。

## 获取程序

无需本地安装任何开发环境：push 到 GitHub 后，Actions 自动在云端编译，到仓库 **Actions → build → Artifacts** 下载 `PanPlugin-win7`（解压后运行 `PanPlugin.exe`）。

## 开发

- 技术：C# + WinForms，目标框架 net48（ADR-0002）
- 构建：`dotnet build src/PanPlugin/PanPlugin.csproj -c Release`
- 领域术语见 [GLOSSARY.md](./GLOSSARY.md)；关键决策见 [docs/adr/](./docs/adr/)
