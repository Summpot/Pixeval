# M3E 功能验收用例

本文件把迁移计划 F01–F27 转成可执行的检查入口；**用例定义不表示用例已通过**。结果、构建、平台、输入设备、样本版本和未覆盖项写入 [执行台账](EXECUTION_LOG.md)。2026-11-07 夜间 Windows 窗口激活/截图通道恢复，已补部分真实输入；仍不能用 headless 测试替代 IME、触摸、文件选择器或窗口管理验收。

## 执行环境与样本

- `tools/Pixeval.MigrationValidation` 的 `ui` 模式创建空账号/扩展/数据库和独立下载目录，使用真实 App 和外壳。只在新目录运行；不导入个人令牌、Cookie、API Key 或用户数据库。
- `inventory` 模式从实际运行时注册导出 [BASELINE_INVENTORY.json](BASELINE_INVENTORY.json)。当前 22 个导航定义、33 个标题映射页面、6 个主设置组、76 个设置节点（含组/容器）、61 个叶节点、19 类首页来源。快照可与后续版本重导出的 JSON 比较；CPU 相关并发上限随设备变化。
- 既有固定测试数据：`Fixtures/ProgressiveImages` 的 JPEG/GIF/WebP；`LoginFailureTest` 的内存 SQLite/HTTP 响应；下载与订阅测试的临时目录/假服务；列表测试的假 fetch engine；翻译测试的受控 Task。各用例引用的测试需核对断言，不能仅凭类名宣布通过。
- 有网络写操作的用例使用假服务捕获调用参数和次数，不执行真实收藏、关注、评论、删除。真实 OAuth、WebView、安装器和系统权限流程单列为人工待验收。
- 每个相关页面重复浅/深主题、窄窗、长中文/emoji、Tab/Shift+Tab、禁用、取消/错误。DPI 100/125/150/200%、系统主题切换和触摸必须另记设备条件。

## 运行时导航与动态页面

快照的 `NeedLogin` 是真实注册值。未登录只允许 Login、Settings、Extensions；历史、下载等虽然本地展示也注册为需要登录，不能因主题迁移擅自开放。About/Help 在设置入口下，详情/搜索结果由上级操作传参数进入，不是丢失的侧栏项。

| 页面族 | 固定参数与操作 | 必须保持 |
| --- | --- | --- |
| WorkRecommended / WorkNew | Illustration、Manga、Novel 分别打开，切类型再刷新 | `WorkRecommended(workType)` / `WorkNew(workType)`；旧源退出和加载状态 |
| WorkPosts | 当前账号及样本作者，三种 workType，增加/移除订阅 | `WorkPosted(workType, user.Id)`；订阅按钮与当前作者/类型一致 |
| WorkFollowing / WorkMyPixiv | 当前账号、类型/隐私等现有选择逐项切换 | 沿用现有 fetch engine 参数，不添加新范围 |
| WorkBookmarks | 当前/他人作者、插画/小说、公开/私密、全部/未分类/具名标签 | 他人收藏隐藏私密选择；切类型刷新标签；初始化标签保留；订阅键不变 |
| WorkRanking | 日期、插画/小说榜单类型、AI 条件 | 可用榜单与请求日期一致；既有隐藏条件保持 |
| WorkRelated | 从作品/首页参数打开，原作品 ID 与类型 | 不加入无参侧栏；相关源参数必须原样传递 |
| Series / Spotlight | 空/正常/末页、系列监视和特辑打开 | 作品与小说系列语义、特辑外链保持 |
| UserRecommended / UserSearchResult | 推荐、中文/空查询、快速换查询 | 后者不在侧栏；无搜索文本返回空源；请求只属于当前查询 |
| UserFollowing | 当前与他人 ID、公开/私密 | 他人 ID 隐藏私密选择；当前账号权限与参数保持 |
| UserFollower / UserMyPixiv / RelatedUsers | 粉丝（当前账号）、好P友/相关用户的显式 ID | 保留各自参数与默认 MyId，不把不同数据源合并 |
| Home / Search / History / WatchLater / Download | 依照下表对应 F 编号 | 所有既有导航 YAML 页面键及图标值兼容 |
| 11 个非侧栏标题映射页面 | About、Help、WorkRelated、WorkSearchResult、UserSearchResult、IllustrationViewer、NovelViewer、UserViewer、SeriesViewer、WorkInfo、Comments | 由上级入口和参数打开；不因注册表枚举而实例化无效详情页面 |

## F01–F27 操作用例

| 用例 | 入口与操作 | 可观察结果 / 负向用例 | 自动证据入口与仍需条件 |
| --- | --- | --- | --- |
| F01.1 | 启动空宿主，关闭更新说明；设置→应用切浅/深/系统，再开新标签 | 所有代表控件随主题改变；窗口动画作用域先于内容；关闭可结束进程 | MaterialThemeIntegrationTest；系统主题/原生窗口待验收 |
| F01.2 | 设置应用字体为中文字体及含连续空格名称；打开编辑器、对话框、标签 | 文本无裁切；原字体顺序保留；代码编辑器继续等宽 | FontFamilyHelperTest、MaterialContentControlsTest；原生字体选择待验收 |
| F02.1 | 命令栏点击主命令、Tab→Enter/Space、打开溢出再次执行；切 CanExecute | 一次输入一次调用；参数同一；所有入口同步禁用；Toggle 回写一次 | MaterialThemeIntegrationTest、KeyboardShortcutTest；实际窄窗/长标签待验收 |
| F02.2 | 卡片/工具栏右键及 Tooltip；鼠标移出菜单，Escape 关闭 | 菜单条件、顺序、分隔、快捷键提示保留；焦点回入口 | MaterialWrapperControlsTest；原生菜单焦点待验收 |
| F03.1 | TextBox 选择→复制/粘贴/撤销，数字编辑输入越界/空/合法值 | 不丢输入；验证可见；只读/禁用不可写；密码仍遮蔽 | 输入外观已有局部测试；剪贴板/IME/密码安全人工待验收 |
| F03.2 | IP 集合、字体/语言、TokenizingBox 增删/退格/清空，日期取消启用 | 绑定集合与原值模型一致；非法 IP 不被静默接受；可空日期可恢复 | TokenizingBoxInputTest 覆盖提交/候选否决/分隔符/删除/只读；Windows 原生固定标签 Unicode/逗号/Enter/退格/去重及退出保存通过；IME 和完整选择器仍待验收 |
| F04.1 | 显示有主/次/关闭按钮的对话框，分别执行；按钮和 Closing 取得 deferral | 等待期间不提前完成；Cancel 后保持打开且可再操作；结果对应入口 | MaterialThemeIntegrationTest；ContentDialog 高风险测试见台账 |
| F04.2 | 重复点击、嵌套显示、取消 token、请求中关闭窗口、抛错后重试 | 每次 show 完成一次；不会挂起/误确认；旧请求不能关闭下次 show；焦点恢复 | P00.05 优先；真实焦点/外点关闭待验收 |
| F04.3 | 持久错误、警告、短通知、可操作提示；浮层移动、骨架/重试 | 严重度、关闭性、时长、遮挡层级及命中保留 | FloatingDockHostTest、MaterialWrapperControlsTest；原生计时/层级待验收 |
| F05.1 | 假账号库为空/多个/打不开，加载失败后重试、选择已有账号 | 保存账号与当前项一致；存储/网络/认证错误分类；不得重置账号库 | LoginFailureTest；login 宿主覆盖实际假账号保存/重新选择和失败保留 |
| F05.2 | Token/OAuth 发起后取消或切账号，令旧响应迟到 | 只更新当前账号；旧源取消；WebView 成功/取消/错误均有出口 | login 宿主覆盖 Token 关闭取消/立即重挂载/迟到响应/重复提交；多窗口并发与原生 OAuth/WebView 待验收 |
| F06.1 | YAML 导入默认/自定义分组，打开页面、新标签、关闭左/右/其他 | 路由/标题/NeedLogin/图标不变；当前项与所有权正确 | NavigationYamlParserTest、TabClosePlannerTest；原生拖动/多窗口待验收 |
| F06.2 | 窄窗/最大化/还原/拖动屏幕，关闭最后窗口 | 尺寸状态和平台系统按钮保持；请求和订阅不遗留 | 窗口管理与多屏 DPI 人工待验收 |
| F07.1 | 三种列表布局载入 1,000/10,000 项，滚到末尾再返回 | 仅实现视口附近容器；位置恢复；无重复请求；选择身份不随复用变化 | WorkListBoxTest、VirtualizingStackPanelTest、IncrementalSourceRetryTest |
| F07.2 | 多选→全选/反选/清空→批量操作；切排序/筛选/源时让旧请求迟到 | 对正确集合调用一次；旧请求不覆盖当前源；失败可重试，离开取消 | ViewDataProviderLifetimeTest 覆盖分页未交付条目与共享源释放；SourceLoadLifetimeTest 只覆盖图片源；批量 UI 映射待验收 |
| F08.1 | 插画/小说/作者/系列/特辑卡片，长标题/缺图/多页/进度 | 徽章、状态、可读标题、缩略图与菜单入口完整 | MaterialContentControlsTest；数据卡片代表图待建立 |
| F08.2 | 收藏右键标签/公开私密、稍后看、下载、屏蔽、复制与打开 | 服务调用 ID/标签/隐私保持；失败不伪造成功状态 | 假服务操作矩阵待补，不使用真实写请求 |
| F09.1 | 按上方动态页面表逐项切参数、刷新、快速返回 | 请求族/参数相同；空/错误/末页状态仍可操作 | BASELINE_INVENTORY 为盘点；逐路由行为验证仍待完成 |
| F10.1 | 补全等待时继续输入，先完成新响应再完成旧响应；点击/Enter 选项 | 旧响应丢弃；只替换最后关键词；同次动作不重复搜索 | search 宿主浅深主题验证乱序、方向键/Enter、清空、卸载/重挂载、普通搜索与已处理事件；原生点击/IME 待验收 |
| F10.2 | 固定/历史/热门标签，作品/小说/用户 ID，用户结果类型切换 | 所有入口、历史规则和 ID 参数保留；IME 确认不误提交 | WorkFilterNodeTest 仅覆盖筛选语法；IME 人工待验收 |
| F11.1 | 插画高级筛选逐项启停、日期/尺寸非法区间、关键词合并 | 排序/AI/翻译标签/违规选项/匹配/工具/比例/宽高均进入同一请求 | search-options 固定引擎 HTTP 参数、禁用值保留、日期/尺寸/会员排序校验；完整原生编辑矩阵待验收 |
| F11.2 | 小说语言/字数或词数/阅读时间/原创/可替换/题材；共享日期切换 | 请求值/单位和与普通关键词合并不变；非法范围不静默改值 | search-options 覆盖三单位的非会员合法范围、会员自定义、语言/题材启停及浅深页面纠错；原生输入待验收 |
| F12.1 | SauceNao 从临时图像选文件或粘贴，取消选择、模拟限流/失败/重试 | 图片比例正确；错误可理解；结果信息与外链保留；不改变 API Key | 文件选择/剪贴板平台验收待完成 |
| F13.1 | 19 类来源逐个新增，改显式 ID/类型/隐私/标签/日期，取消再重开 | 各来源参数保持；缺参数不误发请求；取消不保存 | HomeCardParameterEditorViewModelTest，完整来源参数清单见计划 §5.2 |
| F13.2 | 小网格拖动/缩放/碰撞，隐藏标题/工具栏，保存后重启 | 位置尺寸与原格式保持；无越界丢卡；旧加载在换源/关闭时取消 | HomeCardLayoutEngineTest；原生拖动/持久化 UI 待验收 |
| F14.1 | 作者资料/作品/收藏/关系入口，复制 ID/网页/关注/私密关注/屏蔽 | 入口和权限一致；错误不丢资料；外部动作只通过假服务计数 | 假账号矩阵待建立 |
| F14.2 | 作品标签/统计/信息折叠、系列导航与监视 | 系列监视与工作订阅状态独立；导航参数正确 | 详情/系列代表页面待验收 |
| F15.1 | 评论分页、作者跳转、回复、删除自己/他人评论 | 仅权限内动作可用；错误/取消恢复；旧响应不回写 | 假评论服务与权限矩阵待补 |
| F15.2 | 输入中文 IME/表情/贴图，发送中连点，失败后重试 | 不误提交；一次发送；失败保留输入；贴图长宽比正确 | IME/真实输入人工待验收 |
| F16.1 | 假下载混合状态，单项/批量暂停/继续/取消/重置/重试/删除记录 | 按任务状态启用；错误详情/目录/作品入口保留；删除仅测试目录 | DownloadManagerTest、ImageDownloadTaskTest、NovelDownloadTaskGroupTest |
| F16.2 | 下载持续时切页/关标签/重开，再关闭应用 | UI 重建不重启下载；关闭/取消与原行为相同；无重复订阅 | UI 与引擎关联需专项验收 |
| F17.1 | 作者投稿/收藏/系列等已有订阅类别添加、单个/全部同步 | 类型/范围参数正确；已下载去重；403/404 单项不阻断整批 | WorkSubscriptionSyncHelperTest、WorkSubscriptionSyncRequestQueueTest |
| F17.2 | 同步中删除订阅、关闭页、关窗口取消确认、重新打开 | 请求取消并等待；迟到响应不写入删除订阅；任务次数不增长 | 订阅 UI/关闭窗口人工待验收 |
| F18.1 | 长小说翻页、目录/页内链接、前后作品、右键翻作品 | 页码/位置/边界正确；图片/ruby/强调/代码块渲染；嵌套滚动归属正确 | MarkdownScrollChainingTest；长文截图/输入待验收 |
| F18.2 | 阅读器设置字体/颜色/字号/字重/行高/宽度，切应用主题 | 阅读自定义值优先；重启保留；收藏/下载/稍后看入口保持 | 小说设置不在主设置页，单独覆盖 |
| F19.1 | 假翻译器切换/禁用、译文切换、取消、快速换小说 | 原译文缓存保留；旧结果不覆盖当前作品；无可用扩展时状态正确 | NovelTranslationTest |
| F19.2 | Html/Md/OriginalTxt 及固定导出扩展保存临时目录 | 格式内容和扩展优先级保持；取消/失败不生成伪成功文件 | NovelDownloadTaskGroupTest；原生保存对话框待验收 |
| F20.1 | 多页图预览滑块/页数/前后作品/信息面板，缺图→重试 | 视口适应和选中页不漂移；所有外层命令可达 | ImageViewerRenderingTest、几何测试；真实页面待验收 |
| F20.2 | 收藏标签/复制/另存/原图/扩展动作，文件选择取消 | 原命令参数/能力门控不变；错误可见；不切换解码实现 | 扩展样本与平台文件能力待验收 |
| F21.1 | Swipe/Continuous，各方向、缩放/平移/适应/原始/左右旋转/镜像 | 图像几何和手势优先级一致；不能用装饰控件替代行为控件 | Geometry/Rendering/ScrollGestureProfiles/SwipeControl 测试 |
| F21.2 | 缩略图→原图、重复更新、GIF/WebP/ugoira、快速关页 | 不重置用户视图；旧加载取消；动画资源释放且内存不持续增长 | ProgressiveImageDecoderTest、SizedImageDecodeTest、UpdatableAnimatedBitmapTest |
| F22.1 | Sequential/Loop × CurrentWork/AllWorks，边界暂停/继续/换作品 | 间隔与边界保持；关闭取消旧播放任务，不再翻页 | 自动播放专项与真实计时待验收 |
| F23.1 | 按快照每个设置叶节点执行旧值→编辑/非法值→生效→重启→导出导入 | 类型/单位/范围/枚举/默认值/副作用保持；敏感值不出证据 | NestedSettingsTest、LegacyAppSettingsMigrationTest、NetworkSettingsResilienceTest |
| F23.2 | 临时配置执行重置/历史与缓存清理的取消分支；更新检查失败/成功 | 取消不改变数据；失败保留有效配置；安装重启单独测试 | 维护/安装器需单独假服务或人工环境 |
| F24.1 | 固定测试扩展文件/压缩包、重复/格式/平台错误，启停、排序、卸载 | 优先级和配置保持；待卸载/过期项状态明确；重启可复现 | 官方固定 C++ 宿主已建立；卸载/终结器回归通过，含存活接口与最终库释放；安装/重启尚未完整验收 |
| F24.2 | 扩展设置/帮助/图像/文本/导出消费入口 | 不改变 ABI；禁用后不可执行；失败隔离；不会卸载个人扩展 | 15 扩展/8 设置、Unicode/流转换、启停、颜色、完整时区日期/小数/基础值的 headless 编辑及八类文件重载已验证；原生输入/弹层未验收，完整 UI 不通过 |
| F25.1 | 临时数据库写浏览/稍后看记录，排序/滚动/重开/删除/清空取消 | 身份和记录语义保持；空状态可用；原作品仍能打开 | ArtworkHistoryPersistentManagerTest；UI 入口需假登录状态 |
| F26.1 | About/许可/支持者/帮助 Markdown、语言切换、长文本 | 原文案与回退保留，链接/复制动作保持；无乱码裁切 | I18NResilienceTest、MarkdownScrollChainingTest；代表窗口待验收 |
| F26.2 | MCP 帮助刷新工具/复制例子，主题/字体变化 | 现有帮助操作保持；不因主题改动变更服务协议或开放写工具 | 条件配置与原生剪贴板待验收 |
| F27.1 | EnableMcp=true/false 分别构建，设置开关/端口/资源限制 | 条件入口正确；JSON/schema/错误与权限不变；服务关闭释放 | 本轮不改 MCP 实现；协议专项依阶段执行 |
| F27.2 | UI Automation 读取标题/按钮/焦点，键盘遍历主要入口 | 自动化名称可读、禁用可辨；不可只依赖颜色；命令可操作 | 夜间 Windows 激活/截图/设置导航/标签输入可用；完整遍历、读屏设备和平台矩阵待验收 |

## 不在主设置清单中的入口

- 小说侧栏：NovelBackground、NovelFontColor、NovelFontFamily、NovelFontWeight、NovelFontSize（5–100）、NovelLineHeight（0–150）、NovelMaxWidth（50–10000，步长 50）。位于 `NovelViewerPageViewModel.SettingsPage`，必须通过小说入口验证，不能为了主页统计而复制注册。
- 窗口宽/高/最大化、LastCheckedUpdate 为状态字段。`ApplicationSettingsGroup.DownloadUpdateAutomatically` 标注 `TODO: Not Used`，没有当前主设置编辑入口；保留字段兼容，不凭计划文字擅自开放。
- 主设置维护操作：切换账号、重置、明文导入/导出、缓存与各历史清理、更新检查/说明/安装流程、About/Help、导航编辑器。
- 导航编辑器：应用、格式化、重置、插入文件夹、复制页面定义；非法 YAML 必须保留编辑内容并禁用应用有效配置的动作。
- 扩展设置由已安装的固定宿主注册；空测试环境的 0 个扩展设置不是遗漏，也不是已验收。

## 快捷键与上下文入口补充

| 区域 | 既有行为 |
| --- | --- |
| 图像/小说页面 | 无修饰 Left/Right 翻页、Up/Down 前后作品；按钮右键的跨作品行为单独覆盖 |
| 图像内核 | Ctrl/Cmd+C 复制；Ctrl/Cmd+S 保存；Ctrl/Cmd+Shift+S 另存；Space 播放/暂停（仅命令可执行时） |
| SauceNao | 在相应高级搜索页且配置 API Key 时 Ctrl/Cmd+V 粘贴 |
| 对话框 | 无修饰 Enter 默认可用按钮；Escape 仅在关闭按钮可用时执行；已处理事件不被二次执行 |
| 搜索补全/筛选/标签输入 | Enter、方向键、退格、清空与选中项提交的实际归属；已处理输入/IME 不能被外层快捷键抢占 |
| 列表 | Shift 范围选择及单项复选框的事件归属；按工作身份保持选择 |
| 标签/导航 | 上下文关闭当前/其他/左/右、导航项已有中键/修饰键行为和拖动保持；TabsView 提供的能力必须在 P16 实测 |

## 性能与平台记录要求

每个性能记录写明样本、构建配置、设备、主题、视口、热/冷启动、采样次数及原始数据。至少区分 1,000/10,000 项首屏、滚动、容器数，长小说布局，多页图/动画内存，反复开关后的保留内存和请求次数。只把相对同设备基线恶化超过 10% 作为调查触发点，不作为无条件通过阈值。

Windows 与 Linux 分别执行脚本/直接 MTP；WSL headless 不是 Linux 桌面手势验收。macOS 需要对应硬件，Android/iOS/Browser 需项目工作负载及运行环境。缺失条件明确记未验收，不进入依赖该验收的迁移步骤。
