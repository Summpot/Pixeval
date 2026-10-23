# Pixeval Compose 移动端（内部验收中）

正式桌面端仍为 Avalonia。Android/iOS 使用 `sharedApp`，`desktopDebug` 只承载同一套移动 UI、状态与 Rust 接口。Flutter 和 Avalonia Android/iOS 入口保留到 [验收门槛](ACCEPTANCE.md) 全部通过之后；当前尚不满足切换条件，也没有 Flutter 数据导入。

## 工程

| 模块 | 职责 |
| --- | --- |
| `nativeBindings` | 自动编译 Rust，生成全部 16 个组件的 UniFFI Kotlin 绑定；生成文件只进入 `build/` |
| `sharedApp` | Compose UI、领域类型展示、应用状态、Rust 服务和图片内存缓存 |
| `androidApp` | 进程级会话、前台下载服务、OAuth/分享输入、系统文件导出与分享 |
| `iosApp` | SwiftUI/XcodeGen 宿主与 AVFoundation MP4 编码 |
| `desktopDebug` | Windows 等桌面上的移动端调试宿主；数据位于 `~/.pixeval/compose-debug` |
| `tooling/localization` | 从现有 Fluent AST 编译 Kotlin 消息接口，缺失翻译回退中文 |

固定 Kotlin 2.4.21、Compose 1.12.2、Gradle 9.6.1、AGP 9.3.1、JDK 21、Rust 1.99.0、NDK 28.2.13676358。Android 最低 API 23；Compose 的 Android 依赖要求 **compile SDK 37**，target SDK 为 36。iOS 最低 14，真机和模拟器均为 arm64。iOS CI 选择 Xcode 26.4，与 [Kotlin 版本兼容表](https://kotlinlang.org/docs/multiplatform/multiplatform-compatibility-guide.html) 一致；平台下限见 [Compose 兼容表](https://kotlinlang.org/docs/multiplatform/compose-compatibility-and-versioning.html)。

Release 任务或 Xcode Release 配置自动选择优化后的 Rust；可通过 `-PnativeRelease=true` 显式验证。AGP KMP 库使用单一 native 变体，同一 Gradle 调用同时构建 Debug/Release 时，两者都会使用优化后的 native 库。绑定插件的任务名称可能仍带 `Debug` 后缀，实际 Cargo profile 与输出目录按本工程配置选择。

Ubique 插件固定 1.3.0；生成器从提交 `20c1190053cb29c6c5e8bb07d366323baa2ed96b` 构建，统一使用 UniFFI **0.32.2**。`prepareBindgen` 在 `.tools/` 中维护 String 错误处理补丁，生成任务还处理上游的 `message` 属性和 `close` 方法冲突，不改动 Rust/C# 领域契约或 Cargo registry 源码。Windows NDK `.cmd` 启动器和 API 23 修正在 Gradle 的工具层完成。

## 本地构建

先安装 JDK 21、Android SDK/NDK；iOS 必须使用 macOS/Xcode，并安装 XcodeGen。首次生成器编译需要下载 Rust 依赖。

Windows PowerShell，在本目录执行：

```powershell
$env:JAVA_HOME = '你的 JDK 21 目录'
./gradlew.bat :desktopDebug:run
./gradlew.bat :androidApp:assembleDebug
./gradlew.bat :nativeBindings:jvmTest :sharedApp:jvmTest
./gradlew.bat :androidApp:connectedDebugAndroidTest
./gradlew.bat :androidApp:assembleRelease :androidApp:bundleRelease
```

macOS：

```sh
xcodegen generate --spec iosApp/project.yml --project iosApp
open iosApp/Pixeval.xcodeproj
```

内部 CI 为 `.github/workflows/mobile-internal.yml`，仅手动触发，产出 APK/AAB、Windows 测试报告及 iOS 未签名构建。工作流尚未在远程执行；iOS 源码与框架导出仍需 macOS 编译验证。

Release 默认不配置签名。仅当显式设置 `PIXEVAL_ANDROID_KEYSTORE`、`ANDROID_KEYSTORE_PASSWORD`、`ANDROID_KEY_PASSWORD` 时使用已有 `pixeval` 别名的发布凭据；不回退到 debug keystore。内部 CI 的签名选项复用现有 release secrets，不发布商店版本。

## 生命周期与数据

`MobileRuntime.open` 接收应用私有数据、缓存、下载目录。同一进程只允许一份运行时；同目录重开复用，另一个目录被拒绝。Android `Application` 持有控制器，Activity 重建不销毁 Rust；iOS 后台暂停订阅与下载，重启从 Rust 存储恢复暂停队列。

Android 用户开始/恢复下载、重试或同步订阅前启动 `dataSync` 前台服务；通知可暂停，空队列和系统超时结束服务并保存暂停状态。服务结束期间的新下载等待旧服务完成暂停，避免新任务被旧生命周期回调暂停。MCP 独立于下载服务，始终在进入后台时停止。

完成文件留在私有工作目录。Android 通过系统创建文档导出，分享通过只读 FileProvider URI；目录分享先打包 ZIP。导出失败保留私有文件和原任务。iOS 已接入文件选择器与分享表单，尚未经设备验证。账号切换先取消并等待页面任务，再清空展示状态；偏好设置独立串行更新。

Avalonia 的 `desktop-frontend` Cargo feature 不编译 `app` 中的 Flutter/Compose 宿主适配层，仍导出全部原有领域组件。这样避免宿主适配层中的同名类型与领域类型混入 C# 绑定。`.NET` 构建明确传入 Cargo target 目录，使 UniFFISharp 读取路径与实际产物一致。

桌面回归还修复了下载状态文案的语言切换：每个绑定持有自己的语言订阅，避免已销毁 UI 宿主的弱事件监听阻断当前控件刷新；消息仍来自 ZeroI18n。

完整功能差距与验收结果见 [ACCEPTANCE.md](ACCEPTANCE.md)。
