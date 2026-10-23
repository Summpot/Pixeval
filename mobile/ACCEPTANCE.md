# 移动端迁移验收记录

更新：2026-10-24。**不得切换正式移动端入口或删除旧端**。本文件的“接入”表示代码已经调用真实 Rust 服务，不能代替联网账号、设备或完整业务验收。

## 已取得的构建与测试证据

| 项目 | 结果 | 证据/限制 |
| --- | --- | --- |
| 16 组件 Kotlin/JVM、Android 绑定编译 | 通过 | Gradle 自动生成；非手工复制绑定 |
| Windows x64 Compose 调试宿主编译 | 通过 | 与 Android 共享 `sharedApp` |
| Android arm64/x64 Debug、Release APK/AAB | 通过 | 本地 Gradle；正式 Release 未使用 debug 签名 |
| Android x64 模拟器 Debug 测试 | 5 通过 | `MediaAndBindingsTest` 3、`LifecycleTest` 2；含真实系统返回；Android 17/API 37 |
| Android x64 模拟器 Release 测试 | 5 通过 | 对未签名 Release 的本地测试签名副本运行同一测试 APK；不作为发布产物 |
| MP4 MediaCodec/MediaMuxer | 通过局部设备测试 | 3 帧时间戳、500ms 总时长、末帧颜色与坏输入；下载引擎完整链路仍待验收 |
| Rust 下载单元测试 | 24 通过 | 下载队列、恢复、宏与提交行为 |
| Rust 应用单元测试 | 10 通过 | `cargo test -p pixeval_native --lib app:: -- --test-threads=1` |
| Kotlin 绑定 JVM 测试 | 7 通过 | Unicode 集合/字符串、真实下载与渐进缓存回调、原始字节、目录所有权、String 错误、异常、在途取消后的存活 |
| sharedApp JVM 测试 | 5 通过 | 本地化、BGRA 通道/透明度、长图解码内存上限、并发偏好更新与退出后状态 |
| Fluent 编译器测试 | 2 通过 | 变量与转义；全部现有语言均由原资源生成 |
| Avalonia Windows 桌面 Release 构建 | 通过 | 保留领域 UniFFISharp/BoltFFI 调用；已采用优化后的 MSVC Rust 库 |
| .NET 测试 | 358 通过、1 跳过 | 359 总计；缺少本机扩展宿主 native 库导致插件安装测试跳过 |
| Android arm64 真机、API 23 | 未验证 | 当前没有相应设备 |
| iOS 真机、模拟器、Swift/Kotlin Native 编译 | 未验证 | 当前主机为 Windows；内部 CI 尚未运行 |

日志在忽略目录 `mobile/build/`，设备 JUnit 报告在 `androidApp/build/outputs/androidTest-results/`，JVM 报告在各模块 `build/test-results/`。更改对应实现后必须重新验证。

## 功能清单

以下按 Avalonia 的页面、命令和服务审查；Flutter 仅作行为参考。`sharedApp` 页面目前集中在 `PixevalApp.kt`；控制器为 `AppController.kt`。领域 record/枚举直接采用生成类型。

| 功能/对应 Avalonia 来源 | Rust 接口或组件 | Compose 入口 | 状态与验收场景 |
| --- | --- | --- | --- |
| 登录/令牌刷新：`Services/Core/IUserSessionService` | `MakoClient.refresh_token/exchange_code` | Login | 已接入；有效/失效令牌、账号切换联网验收待做 |
| OAuth/退出：`Services/Core/AppViewModel` | PKCE、`mobile_oauth_code` | Login、用户按钮 | 已接入；已校验回调来源，冷启动实际 OAuth 待验收 |
| 推荐/榜单/关注新作：`ViewModels/Works/*ViewViewModel` | unified work feeds | browse | 已接入；分页隔离、刷新、弱网与全部榜单模式待验收 |
| 新作/好P友作品 | unified work feeds | browse | 已接入；联网权限差异待验收 |
| 收藏与隐私/标签管理：`WorkCommands` | `set_bookmark/work_bookmarks_unified` | browse、Detail | 部分；公开收藏可用，私密收藏和标签编辑未实现 |
| 用户资料/关注/投稿：`UserViewerPageViewModel` | `get_user_detail/set_follow/work_posted_unified` | 作者弹窗 | 部分；关注者/关注列表、相关推荐未实现 |
| 系列与追更：`SeriesViewerPageViewModel` | Mako series、Storage | 无 | 未实现 |
| 相关作品：`IllustrationViewerPageViewModel` | Mako related feeds | 无 | 未实现 |
| 特辑与关注动态：`SpotlightViewViewModel`、Feed 页 | Mako spotlight/activity | 无 | 未实现 |
| 稍后再看：`WorkCommands` | Storage watch-later | 无 | 未实现 |
| 作品评论：`PagedViewerViewModel` | `get_work_comments/add_work_comment` | Detail | 部分；首屏、发送已接入，翻页/回复/删除未实现 |
| 插画/小说搜索：`SearchPageViewModel` | `work_search` | browse Search | 已接入基础查询；账号真实搜索待验收 |
| 高级搜索：`IllustrationSearchFormViewModel`、`NovelSearchFormViewModel` | Mako search options | 无 | 未实现日期、AI、范围、语言等选项表单 |
| 标签补全与搜索历史：Search 页 | Mako tag autocomplete、Storage history | 无 | 未实现 |
| 过滤语言与补全：`Filters/FilterSession` | `FilterSession/FilterStorageProvider` | browse 筛选框 | 已接入；返回集合回调已测；完整语法/候选交互待验收 |
| 浏览历史：`Services/Core/ArtworkUiStateStore` | `record_browse`、history feed | browse History | 部分；写入已接入，离线展示/账户边界待验收 |
| 订阅增删：`WorkSubscriptionsSettingsExpander` | Storage subscriptions | 作者弹窗、Subscriptions | 部分；用户订阅已接入，系列/批量/完整编辑未实现 |
| 订阅同步与自动下载 | `SubscriptionSync` | Subscriptions | 部分；手动同步接入 Android 前台服务；自动同步配置/重启恢复待验收 |
| 插画放大/漫画：`IllustrationViewerPageViewModel` | Rust ImageCache | Detail | 部分；多页与缩放已接入，渐进预览已接入 Rust 回调；平移和手势冲突待完成 |
| 动图播放：`ImageViewerViewModel` | Media ugoira | 无 | 未实现；当前详情显示静态预览 |
| 小说阅读：`NovelViewerPageViewModel` | `get_novel_content_structured/parse_novel/add_novel_marker` | NovelReader | 已接入分页、章节、ruby 文本、插图/链接、阅读标记；阅读定制与真实作品验收待做 |
| Booru | `pixeval_booru` | 无 | 未实现 |
| SauceNAO：`SauceNaoSearchPageViewModel` | `pixeval_saucenao` | 无 | 未实现 |
| 下载队列/暂停/恢复/取消/重试：Download 页 | `DownloadManager` | Downloads | 普通任务与订阅分组已接入，Rust 回调驱动 StateFlow；离线/重启/取消竞争待验收 |
| 路径宏：`DownloadPathMacroParser` | Rust macro analyzer | Settings | 已接入校验与保存；错误定位 UI 待完善 |
| 原图/漫画 ZIP/CBZ | Download native policy | Downloads | 原图已接入；漫画打包格式选择未实现，全部格式内容待验收 |
| 动图 Original/GIF/APNG/WebP | Media、Download native policy | Settings | 已接入选项；全部格式/帧时序端到端待验收 |
| 动图 MP4 | 平台 `DownloadFormatEncoder` | Settings | Android 编码局部测试通过；iOS AVFoundation 与真实下载待验收 |
| 小说 TXT/Markdown/HTML/EPUB | Novel、Download native policy | Settings | 已接入选项；图片、目录、字符集与文件打开待验收 |
| 系统文件与分享：`IStoragePickerService/IArtworkActionService` | 私有下载工作目录 | Downloads 导出/分享 | Android SAF/FileProvider 已接入；iOS 已写宿主接口；授权撤销/空间不足待验收 |
| 相册导出 | 平台宿主 | 无 | 未实现 |
| 持续下载/后台通知：`INotificationService` | Rust 队列/检查点 | Android DownloadService | Android 服务持有/结束已测；通知拒绝、系统超时、进程终止待设备验收 |
| 深链接/外部分享：导航服务 | `parse_pixiv_url`、OAuth 路由 | AppController | 部分；作品冷启动暂存已有，用户/系列路由、完整去重及 iOS 分享扩展未完成 |
| 网络设置：`Services/Network/NetworkRuntime` | Mako config、cache/download network | Settings | 部分；代理/镜像/域名前置已接入，IP 编辑与系统代理策略待完成 |
| 缓存管理：`Services/Media/ImageProviderService` | ImageCache | RustImage | 已按尺寸解码并限制并发/LRU；清理/统计 UI、iOS 尺寸优化待完成 |
| 主题/语言：Settings | MobilePreferences | Settings | 已接入、独立字段串行持久化；布局/阅读主题未完善 |
| 导航 YAML：`NavigationSettingsPage` | Config validation | 无 | 未实现 |
| 首页卡片/YAML：`Services/Core/HomePageCardSession` | Config home layout | 无 | 未实现 |
| 本地化：`src/Pixeval/messages` | 构建时 Fluent compiler | Messages | 四语言与中文回退已接入；全部移动端文案审查待完成 |
| Markdown 帮助/图片资源 | `MobileRuntime.help_document` | 无独立帮助页 | Rust 提供原帮助文本；阅读 UI 与其余资源复用未完成 |
| MCP：`Services/Background/PixevalMcpService` | `McpServer/McpSessionBridge` | Settings | 默认关闭、手动前台启用、后台停止已接入；协议/写工具完整设备回归待做 |
| 手机/平板导航：`INavigationService` | 页面 StateFlow | sharedApp | 单/双栏已有；Android 系统返回与列表滚动保存已接入；完整返回栈与每页筛选恢复尚未完成 |
| 桌面动态插件、自更新、多窗口/托盘 | 原 Avalonia 服务 | 不显示移动端操作 | 约定平台差异；不纳入移动端内置格式验收 |

## 最终切换门槛（全部仍需完成）

- [ ] 清单没有未实现项；原 Avalonia 的其余命令/设置完成逐项复核。
- [ ] 全部标量边界、集合、带数据枚举、错误、跨组件、回调异常及释放竞争均覆盖；缓存/下载事件桥验证。
- [ ] 有效账号完成全部业务验收，账号切换、搜索分页和订阅/下载恢复不存在状态串用。
- [ ] 全部内置导出格式可打开，帧时序、内容、图片和章节正确。
- [ ] 弱网/离线、长列表/大图、存储不足、授权撤销、后台/杀进程、深链接冷启动均通过。
- [ ] Windows x64 Debug/Release 调试宿主，Android arm64 真机/x64 模拟器，iOS arm64 真机/模拟器完成 Debug/Release 矩阵。
- [ ] Avalonia 原构建/发布流程、下载/订阅/过滤/插件/MCP 完整回归；当前插件跳过项补验。
- [ ] 内部 CI 实际成功产物经验收，签名使用已有正式凭据。
- [ ] 才启用 Compose 移动端正式构建，移除 Flutter、其专用 Dart 工具与 Hooks、旧 Avalonia Android/iOS 引用；保留 Avalonia Desktop 和 Browser。
