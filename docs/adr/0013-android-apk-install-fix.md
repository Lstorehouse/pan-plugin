# ADR-0013 · Android APK 安装失败（packageInfo is null）修复

- 状态：已实施
- 日期：2026-11-18
- 依据：M4 APK 在手机上提示「解析软件包时出现问题（33）」，详细信息 `packageInfo is null`

## 背景

M4 的构建产物下载到手机后无法安装，系统报「解析软件包时出现问题」，详情为 `packageInfo is null`。用 `aapt dump badging` 检查本地产出的两个文件，确认结构完整、manifest 可解析：

- `com.wuhou.panplugin.mobile.apk`：**未签名**的中间产物
- `com.wuhou.panplugin.mobile-Signed.apk`：已签名的可安装产物

CI 的 `build-android` 上传路径写的是 `*.apk`，把两个都打进了同一个 artifact。用户解压后如果点了未签名那个，就会在安装解析阶段失败——这是本次报错的直接原因。

同时发现两处会在装对包之后继续出问题的隐患：

1. `targetSdkVersion='36'`（.NET 10 默认，Android 16）。旧系统及部分国产 ROM 对高于自身 API 的 targetSdk 解析/安装兼容性差。
2. Debug 构建默认使用快速部署（fast deployment），.NET 程序集不进 APK，靠调试通道下发；手动安装的包在手机上会因缺程序集而闪退。

## 决策

**从「产物、目标版本、打包方式」三处一起修，保证 artifact 里只有一个可安装的包。**

1. CI 只上传已签名包：
   - 构建参数加 `-p:EmbedAssembliesIntoApk=true`，关闭快速部署，把程序集打进包。
   - 上传路径从 `*.apk` 收紧为 `*-Signed.apk`，artifact 改名 `PanPlugin-Android`。
   - 上传前新增校验步骤：找不到 `*-Signed.apk` 直接 fail，避免再次产出「能下载但装不上」的包。
2. `PanPlugin.Mobile.csproj` 显式设 `<TargetSdkVersion>34</TargetSdkVersion>`（Android 14），不追 .NET 10 默认的 36；`minSdk` 保持 24（Android 7.0）。内测分发不需要新 API，覆盖面优先。
3. `MainActivity` 的 `[Activity]` 加 `Exported = true`，满足 targetSdk ≥ 31 时 launcher Activity 必须显式声明导出的要求。

## 后果

- artifact 里只有 `*-Signed.apk` 一个文件，用户不可能再选错； README 明确写了「只装 Signed 包」。
- APK 自带全部程序集，脱离开发机也能启动，不再依赖快速部署通道。
- targetSdk 34 在 Android 7.0～16 上均可安装；代价是拿不到 Android 15/16 的新行为，对内测无影响。
- 仍使用调试签名，仅适合内测；正式发布需引入自有 keystore 并在 CI 里用 secrets 管理密码（本次不做）。
- 修复属于构建/打包层，共享业务源码与账本格式未改，不涉及数据迁移。
