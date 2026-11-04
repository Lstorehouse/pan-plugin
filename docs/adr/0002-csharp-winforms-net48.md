# 技术栈：C# + WinForms（.NET Framework 4.8）

硬性约束是 Windows 7 及以上可用、单机桌面程序、单人使用。选择 C# + WinForms + .NET Framework 4.8：单 exe 分发、无浏览器内核依赖、Excel 读写生态成熟（EPPlus/ClosedXML/NPOI）、后续维护门槛低。

**Considered Options**:
- Python + tkinter —— 否决：分发需打包运行时，体积大、Win7 兼容坑多。
- Electron/Tauri —— 否决：Win7 支持差或已停止支持。
- Rust/Go —— 否决：Excel 生态弱，用户后续自行修改门槛高。

**Consequences**: 新 API 不可用（.NET Framework 冻结）；如未来放弃 Win7 可迁移到 .NET 8 + WinForms，代码改动小。
