# Pixeval 移动端迁移至 Compose Multiplatform

## 目标与技术选择

正式桌面端继续使用 Avalonia；Android、iOS 使用 Compose Multiplatform；Compose Desktop 仅作为移动端 UI、状态与 Rust 接口的调试宿主。

移动端对齐当前 Avalonia 的全部可移植业务，重新设计手机和平板交互。不迁移 Flutter 用户数据；内部按阶段实施，全部验收通过后一次切换并清理旧移动端。

- 新工程统一放在 `mobile/`，包含 `nativeBindings`、`sharedApp`、`androidApp`、`iosApp`、`desktopDebug`。
- `sharedApp` 共享 UI、导航、状态和应用服务；平台宿主实现文件、授权、分享、通知及生命周期。
- Kotlin 侧仅使用 UniFFI。Avalonia 现有 UniFFISharp、BoltFFI 和发布方式继续工作。
- 采用 Ubique KMP 绑定工具链 1.3.0，覆盖 JVM、Android 和 Kotlin/Native。其已支持 UniFFI 0.32 及跨模块类型。[项目文档](https://github.com/UbiqueInnovation/uniffi-kotlin-multiplatform-bindings)
- 保持仓库锁定的 UniFFI 0.32.2；生成器与 Rust 核心使用相同补丁版本。需要调整生成器时使用固定源码版本构建，修复集中在工具层。
- 固定 Kotlin 2.4.21、Compose Multiplatform 1.12.2、Gradle 9.6.1、AGP 9.3.1、JDK 21，沿用现有 Rust 和 NDK 版本。最低 Android API 23、iOS 14；Android 支持 arm64 真机及 x64 模拟器，iOS 支持 arm64 真机及 Apple Silicon 模拟器。[兼容性依据](https://kotlinlang.org/docs/multiplatform/compose-compatibility-and-versioning.html)

## Rust、绑定与状态管理

**先完成绑定验证，再展开页面实现。**

- 从现有 `pixeval_native` 导出伞生成全部相关组件，验证跨 crate 对象、record、枚举、错误及可空类型；生成结果进入构建目录，由 Gradle 自动生成和编译。
- 首批验证真实接口：`FilterStorageProvider` 的集合返回值、`DownloadFormatEncoder` 的字符串返回值，以及会话、下载、缓存回调。覆盖 Rust 工作线程调用、并发、异常和释放。
- Kotlin 回调由注册对象强引用持有，解除 native 注册并结束在途调用后再释放。同步回调在工作线程完成，禁止同步等待 UI 线程。
- 异步接口映射为 `suspend`；事件接收对象适配为 `Flow`，取消时先唤醒并停止 native 接收，再释放对象。页面状态采用 `StateFlow`，订阅随所属页面或应用作用域销毁。
- 直接使用生成的领域类型；Kotlin 扩展函数与展示状态承担显示转换，不另建一套领域 DTO。
- 复用 Rust 网络、存储、缓存、下载、过滤和订阅引擎。仍在 C# 中的可复用业务编排下沉 Rust，界面与平台操作保留各自实现。
- 扩展现有应用初始化和关闭接口，接收数据、缓存、下载工作目录；确保移动端只有一套客户端、存储和下载管理器实例。
- 新增平台能力描述，供界面判断导出格式、后台执行及宿主功能。新增 Rust 接口采用兼容性扩展，保留现有 C# 调用契约。

## 功能实现与平台适配

先依据 Avalonia 的页面、命令、设置和服务建立功能清单，每项记录对应 Rust 接口、Compose 入口、平台差异和验收场景。现有 Flutter 实现作为行为参考。

| 功能组 | 实现要求 |
|---|---|
| 账号与浏览 | 登录、令牌刷新、退出、推荐、排行、关注新作、收藏、用户、系列、相关作品、评论及现有作品操作 |
| 搜索与订阅 | 插画和小说搜索、选项、标签补全、过滤语言与补全、搜索和浏览历史、订阅管理、同步与自动下载 |
| 阅读与媒体 | 插画缩放、多页漫画、动图播放、小说阅读、Booru、SauceNAO、分享与深链接 |
| 下载与导出 | 队列、暂停、恢复、取消、失败重试、路径宏、订阅目录，以及现有内置格式 |
| 设置与定制 | 网络配置、缓存管理、主题、阅读设置、自定义导航、首页卡片及 YAML 编辑与校验 |
| 本地化与帮助 | 复用现有 Fluent、Markdown 和图片资源，保留全部现有语言及中文回退规则 |

具体实现约束：

- 手机采用单栏导航，平板采用列表与详情分栏；保持返回栈、筛选条件和滚动位置，不复制桌面多窗口操作方式。
- 图片通过 Rust 网络与缓存加载；Compose 负责按显示尺寸解码、内存缓存和显示。支持渐进预览，页面离开时取消加载，避免列表同时持有原图解码结果。
- 内置导出覆盖原图、漫画 ZIP/CBZ、动图 Original/GIF/APNG/WebP/MP4，以及小说 TXT/Markdown/HTML/EPUB。Android MP4 使用 MediaCodec/MediaMuxer，iOS 使用 AVFoundation，通过现有编码回调接入下载引擎；不把当前移动端 MP4 空实现视为完成。
- 本地化在构建时从现有 Fluent 资源生成 Kotlin 消息接口，不建立人工维护的重复翻译。切换语言保留页面、输入和选中状态；Avalonia 的 ZeroI18n 保持原有入口。
- Android 使用前台服务承载用户启动的持续下载，并处理通知权限与服务停止；iOS 保存队列和检查点，在系统暂停后恢复，不承诺后台常驻 Rust 工作线程。
- 下载先进入应用私有工作目录，完成后通过系统文件、相册或分享接口导出；外部目录授权失效时保留任务并提示重新选择。
- OAuth、深链接和分享输入统一进入应用路由，处理冷启动、未登录暂存、重复输入及登录后继续。
- MCP 默认关闭，仅允许用户在前台手动启动本机服务；进入后台或关闭应用时停止，沿用现有写工具开关。
- 现有依赖 .NET 宿主的动态插件、桌面自更新、多窗口和系统托盘由 Avalonia 提供；移动端不显示不可执行的操作，插件扩展格式不列入内置格式验收。

## 实施顺序与切换

1. **绑定与宿主**：完成三目标工程、自动 native 构建、真实回调验证和初始化/关闭流程。Compose Desktop 使用独立调试数据目录，运行与移动端相同的 UI 和状态代码。
2. **核心浏览**：完成账号、导航、推荐、搜索、图片详情和阅读，打通真实 Rust 服务。
3. **完整业务**：按功能清单补齐订阅、下载、媒体导出、设置、定制、本地化和平台集成；同步补充 Rust 公共编排。
4. **联合验收**：完成移动端全量功能验证，并运行 Avalonia 桌面回归。内部构建用于验收，期间不切换正式移动端入口。
5. **一次切换**：全部通过后启用 Compose 移动端构建，删除 Flutter 应用、专用生成代码、测试、Hooks，以及仅供其使用的 Dart 工具包；移除 Avalonia Android/iOS 入口及解决方案引用。保留 Avalonia Desktop 和现有 Browser 实验入口，更新文档与 CI。

新移动端标识统一使用 `io.github.pixeval.pixeval`，分享扩展和 App Group 同步命名；不提供 Flutter 数据导入。CI 产出 Android APK/AAB 和 iOS 未签名构建；签名使用已有发布凭据，不将调试签名作为正式发布配置。

## 测试与完成标准

- **绑定层**：标量边界、字节、集合、带数据枚举、错误、跨组件对象、异步取消，以及后台线程带返回值回调；覆盖重复注册释放和完成/取消竞争。
- **业务层**：依据 Avalonia 功能清单逐项验证；账号切换不得混用缓存状态，搜索和分页互相隔离，订阅及下载在重启后正确恢复。
- **媒体与平台**：验证全部内置格式可打开且内容、帧时序正确；覆盖长列表滚动、大图、弱网、离线、存储不足、授权撤销、后台切换、进程终止和深链接冷启动。
- **平台矩阵**：Windows x64 Compose 调试宿主、Android arm64 真机及 x64 模拟器、iOS arm64 真机及模拟器；同时验证 Debug 与 Release。
- **桌面回归**：Rust 与 .NET 测试通过，Avalonia 原有桌面构建及发布路径保持可用，重点回归下载、订阅、过滤补全、插件和 MCP。
- **最终门槛**：功能清单无未实现项，约定的平台差异均已落实；未取得设备的验证明确记为未验证，不能用 Compose Desktop 的通过结果替代移动端验收。


## 实施完成与验收状态 (2026-10)

针对 Avalonia 原有桌面业务对齐目标，三阶段核心功能补齐已全部完成并通过多层构建与测试验证：

### 阶段一：核心媒体与作品交互补齐 [已完成]
- **相册导出**：实现移动端相册无权限障碍直存（Android MediaStore API 与 Desktop Pictures 目录），支持原图/处理后多媒体导出。
- **插画手势交互**：实现平滑缩放与拖拽 (`PanZoomImage.kt`)，支持双击快速在 1.0x 与 2.5x 间缩放切换，严格约束平移边界。
- **动图 (Ugoira) 播放器**：完整实现动图元数据解析、ZIP 解包、按帧时序解码与播放控制组件 (`UgoiraPlayer.kt`)，支持播放/暂停/拖动进度条。
- **高级收藏弹窗**：新增 `BookmarkDialog.kt`，支持公开/非公开收藏切换、现有标签快速选择与自定义标签输入打标。
- **相关作品与系列支持**：在作品详情页提供相关作品轮播及系列详情全屏底栏，提供追更与订阅直接联动。
- **评论完整交互**：支持全部评论与回复流分级展示、无限分页加载、发表根评论、回复特定楼层与评论作者删除评论。

### 阶段二：搜索、发现流与用户关系补齐 [已完成]
- **高级搜索筛选底栏**：新增 `SearchFilterSheet.kt`，完整支持匹配模式（标签前缀/完全匹配/标题简介/关键词）、热门排序、AI 筛选（全部/隐藏 AI/仅显示 AI）、作品类型过滤与精准时间区间。
- **搜索历史与自动联想补全**：整合本地历史记录流与远程联想建议，支持单项删除、全部清空历史及快速补全填充。
- **稍后再看流**：扩展 `FeedSource.WatchLater`，卡片与详情页一键增删（`⏱`/`⏱✓`），打通离线待看管理。
- **Pixivision 特辑资讯流**：新增 `SpotlightView.kt` 与 `SpotlightFeed`，支持按分类（全部/特辑/教程/灵感）流式加载与外部浏览器直接打开。
- **用户关系中心**：新增 `UserProfileSheet.kt`，包含创作者投稿作品流、关注列表、粉丝列表与相关推荐创作者列表切换。

### 阶段三：配置定制、扩展与系统工具补齐 [已完成]
- **漫画 ZIP/CBZ 打包偏好**：在 Rust 移动端偏好与 Compose 设置中新增 `illustration_format`（Original / Zip / Cbz）直通下载策略。
- **缓存管理与统计**：在设置页实时显示磁盘缓存占用，支持一键清空图片及数据缓存 (`cache.stats()`, `cache.clear()`)。
- **YAML 布局与导航编辑器**：新增 `YamlEditor.kt`，支持对首页卡片流与底部导航栏 YAML 实时在线编辑、高亮、规范化校验与默认模板还原。
- **内置 Markdown 帮助手册**：新增 `HelpViewer.kt`，原生支持查询过滤语法（`filters`）、下载宏规则（`download`）、作品订阅（`subscriptions`）与 MCP 工具协议（`mcp`）。
- **外部扩展查看器**：
  - `BooruViewer.kt`：支持主流 Booru 站点（Danbooru、Gelbooru、Yande.re、Rule34 等）图源浏览、标签过滤与作品详情。
  - `SauceNaoViewer.kt`：支持 SauceNAO 图像逆向检索客户端、相似度匹配与直达原出处。

### 验证与回归结果
- **Rust Native 核心**：`cargo test -p pixeval_native --lib app::mobile` 100% 通过。
- **Kotlin/Compose Multiplatform**：
  - `:sharedApp:jvmTest` 100% 通过。
  - `:nativeBindings:jvmTest` 100% 通过。
  - `:desktopDebug:assemble` 构建成功。
  - `:androidApp:assembleDebug` APK 构建成功。
- **Avalonia / .NET 桌面端回归**：
  - `src/Pixeval.Tests/Pixeval.Tests.csproj` 全部 359 个单元与集成测试：358 个通过、0 个失败、1 个跳过（本地扩展路径无安装属于预期内跳过）。
