# 隔离验证宿主

`tools/Pixeval.MigrationValidation` 只用于迁移验收，不是第二套产品 UI。页面、注册表、设置模型、资源、样式、导航容器和字体全部来自主项目。

在仓库根目录使用系统 `dotnet`，由 `global.json` 的版本和 `rollForward` 策略解析 SDK；无需独立安装 10.0.401。记录 `dotnet --info` 中的实际 SDK/runtime 后运行：

```powershell
dotnet build tools/Pixeval.MigrationValidation/Pixeval.MigrationValidation.csproj -c Release
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll inventory artifacts/m3e/my-run/inventory
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll capture artifacts/m3e/my-run/capture
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll benchmark artifacts/m3e/my-run/performance
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll navigation artifacts/m3e/my-run/navigation
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll search artifacts/m3e/my-run/search
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll search-options artifacts/m3e/my-run/search-options
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll search-page artifacts/m3e/my-run/search-page
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll saucenao artifacts/m3e/my-run/saucenao
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll user-pages artifacts/m3e/my-run/user-pages
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll user-details artifacts/m3e/my-run/user-details
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll series-page artifacts/m3e/my-run/series-page
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll series-details artifacts/m3e/my-run/series-details
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-info artifacts/m3e/my-run/work-info
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll comment-send artifacts/m3e/my-run/comment-send
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll comment-list artifacts/m3e/my-run/comment-list
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-series artifacts/m3e/my-run/work-series
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll download-pages artifacts/m3e/my-run/download-pages
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll subscription-editor artifacts/m3e/my-run/subscription-editor
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll download-folders artifacts/m3e/my-run/download-folders
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-feeds artifacts/m3e/my-run/work-feeds
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-sources artifacts/m3e/my-run/work-sources
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-related artifacts/m3e/my-run/work-related
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll history-pages artifacts/m3e/my-run/history-pages
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll home-cards artifacts/m3e/my-run/home-cards
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll home-parameters artifacts/m3e/my-run/home-parameters
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll home-sources artifacts/m3e/my-run/home-sources
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll spotlight artifacts/m3e/my-run/spotlight
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll login artifacts/m3e/my-run/login
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-cards artifacts/m3e/my-run/work-cards
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll work-selection artifacts/m3e/my-run/work-selection
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll extension-management artifacts/m3e/my-run/extension-management
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll bookmark-tags artifacts/m3e/my-run/bookmark-tags
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll bookmark-page artifacts/m3e/my-run/bookmark-page
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll ranking-page artifacts/m3e/my-run/ranking-page
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll documentation artifacts/m3e/my-run/documentation
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll dialogs artifacts/m3e/my-run/dialogs
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll ui artifacts/m3e/my-run/native
```

每个输出路径必须不存在。宿主在任何 App/AppInfo 初始化之前设置独立数据目录；设置、账户数据库、扩展、历史、下载目标均指向输出目录下的 `data/`。不读取个人配置，不清理旧证据；不要向此宿主输入真实账号或令牌。验证结束保留数据用于诊断，分享前检查内容。

默认语言为 `zh-Hans`。可在启动前设置进程环境变量 `PIXEVAL_MIGRATION_CULTURE`，例如 PowerShell 的 `$env:PIXEVAL_MIGRATION_CULTURE = 'fr-FR'`，使用另一个全新输出目录运行同一模式；结束后用 `Remove-Item Env:PIXEVAL_MIGRATION_CULTURE` 恢复默认。当前应用提供中文、英语、法语和俄语翻译。inventory/headless 模式检查请求语言是否有对应翻译，并在 `validation-context.txt` 记录实际 UI 语言、runtime 和操作系统；无对应翻译则失败，不用回退语言冒充验证。某种语言缺失的具体资源仍按生产回退规则处理，文化名称存在不代表翻译全部齐全。固定业务样本文本仍可包含中文，语言参数只切换真实应用资源。

## inventory

初始化真实 App 服务和资源，导出导航/页面/设置/首页来源元数据到 `inventory.json`。设置只读取名称、类型、范围及层次，不读取 Value。不会启动产品窗口、登录、MCP 服务或更新检查。

固定基线为 22 个导航入口、33 个页面类型、6 个主设置组、76 个设置节点（61 个叶节点）和 19 类首页来源。小说阅读器私有设置与扩展注册项另行验收，不能用主设置清单替代。

## capture

使用真实 Skia 绘制的 Avalonia headless 窗口，1000×720，语言由上述环境变量指定，分别给窗口设置 Light/Dark 局部主题。数据仍是默认浅色设置，因此深色截图中的主题选择值保持“浅色”；这是局部主题覆盖样本，没有改写配置。

装载真实 NavigationPage 后捕获：设置主页、六个设置分组、导航 YAML 编辑器、帮助、空扩展列表、空登录页。导航标题和附加操作栏一并呈现。对可滚动页面保存首尾位置；展开当前已生成且启用的 SettingsExpander/Expander 后补充顶部截图。不会点击下载、登录、导入/导出、重置、更新、文件选择、复制或外部链接。

每次布局后给真实动画 500 ms 的捕获稳定窗口，再布局/绘制。`captures.json` 记录尺寸、视觉控件数、文件 SHA256 和累计捕获耗时；**该耗时包含动画等待、布局、编码和磁盘写入，不是首屏性能或帧时间**。检查帧非空、尺寸和有限布局边界；不将这些自动检查等同视觉或交互验收。初始页面用例为 42 张截图，实际数量随页面布局可能变化。

没有禁用动画或重写产品模板。仅捕获展开后的首屏，不证明每个嵌套条目都出现在图片中；需要逐条设置验收时按 `ACCEPTANCE_CASES.md` 操作。窗口没有完整标签栏、登录态及平台装饰，headless 输入不等同 IME、触摸、DPI 或屏幕阅读器。

## ui

使用真实 App 桌面生命周期与外壳，在隔离目录正常启动。产品本身已有的更新检查等联网行为仍然存在。用于原生交互验收时记录操作、主题、缩放、输入设备和正常退出；启动成功或 accessibility tree 可见不算验收完成。

可选第三参数为自己构建并信任的固定原生扩展库，复用 `extensions` 的单库复制流程，在 App 初始化前复制到本次全新数据目录。不安装到个人应用，不扫描外部扩展，也不绕过实际页面入口：

```powershell
$fixture = Get-Content artifacts/m3e/my-fixture/fixture.json -Raw | ConvertFrom-Json
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll ui artifacts/m3e/my-native-extensions $fixture.library
```

通过真实设置页中注册的扩展设置组验证八类编辑器；关闭后检查该次 `data/Settings/settings.yaml`。记录每种输入、主题与未覆盖矩阵，不因官方 demo 的界面可操作就宣称全部第三方扩展或导出已验收。

2026-11-07 早间/晚间的窗口激活与截图阻塞，在夜间恢复后的隔离进程中已消除：可以操作真实设置页并正常退出。每项原生验收仍须独立记录；不可由通道恢复或 capture 结果解除 P00→P01 门控。

## benchmark

在没有同时构建/测试的机器上运行 Release 宿主。固定 1000×720、浅深主题，每个样本 5 次：WorkListBox + 原 VirtualizingWrapPanel 的 1,000/10,000 个文本 WorkItem；NovelMarkdownBox 的 500 段、40,890 字符中日英/emoji/粗斜体/内联代码文档。没有网络图片、真实账号或扩展。

保存 30 组首帧及 11 个往返滚动位置的逐帧原始值到 `performance.json`，首轮另存 6 张 PNG。计时包含对象/固定数据构造、布局、Skia CPU 绘制与帧读回；滚动使用程序化偏移，不能推导原生 GPU FPS、触摸平滑度、网络图像或完整小说页面性能。每个场景第一轮单列，后四轮取中位数；只有进程最初一轮包含完整进程内缓存/JIT 冷启动。

验证列表首尾项目确实进入视口、回收后文本绑定正确、清空后卡片 DataContext 释放；固定视口和零缓存下实现化容器必须在 1–120 内。原基线均为 42；120 是该固定样本的结构上限，不是整个应用的通用预算。

每轮关闭后在计时区外收集 GC，并记录窗口/内容 WeakReference；全组结束后最多推进 3 个渲染 tick 再收集，区分短暂框架保留与最终仍存活。内存值是托管分配/收集后存量，不包含 GPU 与全部原生资源。弱引用释放不等价于所有资源泄漏检查通过。

性能预算：以同设备、同 SDK/依赖/构建/样本/主题的中位数为比较对象。恶化超过 10% 触发调查和受影响控件回退评估；先复核后台负载和采样方差，不用硬性跨设备毫秒阈值替代分析。真实图片/动画、平台手势、原生 GPU 和长时间运行仍另行验收。

## navigation

在隔离真实 App 服务中打开 NavigationSettingsPage + NavigationPage，分别验证浅深主题和 1000/420/360 DIP 窗宽。通过 headless 指针命中真实命令栏按钮及平台保存快捷键执行格式化/应用/重置/插入文件夹；验证无效 YAML 显示诊断、禁用应用、保留编辑文本和最后一次有效文件的原始字节。重置/插入/格式化只改变待编辑内容，等待显式应用。固定样本包含中文文件夹、页面键及国际化资源引用。

参考面板通过键盘展开、滚动和真实复制按钮读取首尾页面/图标项，检查内存剪贴板中的页面定义是有效 YAML。窗口跨布局阈值往返时编辑器实例、选区和撤销历史保持；窄窗参考面板限制高度，编辑器仍可输入和滚动。输出 `navigation-contracts.txt` 及状态截图；任何断言失败使进程失败退出。写入的 `data/Settings/navigation_menu.yaml` 仅属于该次验证，不使用个人导航文件。没有通过私有方法/反射绕过页面命令，也不将 headless 快捷键或内存剪贴板当作原生 Cmd/IME/系统剪贴板的通过证据。

## work-selection

使用真实 WorkContainer、WorkListBox 和固定插画数据，覆盖浅深主题、360/1000 DIP 窗宽及 LinedFlow/Grid/Masonry 布局。输入筛选表达式并按 Enter、切换既有本地排序、清除筛选，检查窄窗筛选框不覆盖选择命令。通过真实菜单执行全选、反选、清空，检查切换按钮与模型双向同步，以及禁用动作在主栏和溢出菜单中一致。

替换来源时旧选择清除，刷新命令移除/恢复后只执行一次；附加主/次命令保留对象和单次执行。输出 `work-selection-contracts.txt` 和页面截图。固定来源不证明真实分页、网络请求、滚动恢复或批量收藏/下载/浏览器动作。键盘选择、可见焦点和滚动回收另由 `WorkListKeyboardTest` 覆盖，不用页面截图替代这些断言。

## extension-management

在真实 ExtensionsPage/ExtensionService 中注册托管固定宿主元数据，验证浅深主题和宽窄窗：长名称/作者/描述、管理栏主项与溢出项标签、启停键盘操作及模型回写、上移/下移/置顶/置底、安排卸载/取消卸载。通过真实设置文件重载验证启停、优先级及待卸载目标；隔离目录内的占位文件必须在安排/取消时保持存在。有效帮助地址保留原入口及目标，空串/纯空白地址隐藏入口，不调用真实 URI launcher。

这些占位文件只是文本哨兵，绝不作为原生库加载；此模式不需要第三个参数。输出 `extension-management-contracts.txt` 和页面截图。原生 ABI/设置编辑器仍由 `extensions` 模式验证；安装包、外部帮助/目录打开、批量过期扩展及重启后实际删除另行验收。

## bookmark-tags

复用完全离线 HTTP 拦截器，返回固定插画/小说收藏详情及标签数据。真实 TagSelector 在浅深主题和宽窄窗中验证加载、已注册标签/隐私、选择上限、长标签输入与去空白、重复标签、新增取消及确认载荷。新增/确认/取消入口使用本地化自动化名称；未知 HTTP 请求仍直接失败。

再通过 BookmarkTagSelectorFlyoutHelper 打开真实 Flyout，验证窄窗及打开时缩放、异步提交期间重复确认只执行一次、成功关闭、待处理时 Escape 关闭、迟到完成不重新打开、关闭后退订尺寸事件，以及回调失败后保留输入和恢复焦点以便重试。失败样本会经过现有 App 异常边界写日志，这是有意触发的固定异常；断言确认恢复后再次提交成功。输出 `bookmark-tag-contracts.txt`、请求记录及截图。没有真实收藏写入，且此模式不能证明原生 IME、系统弹出层或服务端保存。

## bookmark-page

打开真实 WorkBookmarksPage + WorkContainer，使用隔离假账号和手动完成的收藏标签 HTTP 响应，作品请求返回固定空页。验证浅深主题、360/1000 DIP 窗宽、长标签及下拉层/Escape、选择器名称、首次显式标签、全部/未分类的原 API 值，以及类型/隐私/标签改变后的精确请求参数。完成的新列表缺少当前标签时保留筛选，但不能显示旧来源的数量。

新旧标签响应逆序完成，旧成功/失败不得覆盖当前列表或显示当前错误；刷新失败保留筛选并可重试，加载期间的新选择不能被请求发起时的旧值覆盖。卸载后完成旧请求，再挂载必须重新加载且保留筛选。输出 `bookmark-page-contracts.txt`、请求日志和页面/错误截图。Mako 的 EngineHandle 取消只约束枚举及响应归属，不能中断已发送的 HTTP；固定迟到非空页验证不继续下一页，不推导所有内部分页情形均已取消。订阅入口、真实作品分页、账号切换及服务端数据权限仍另行验收。

## ranking-page

使用真实 WorkRankingPage 和离线榜单引擎请求，覆盖两类作品的全部受支持模式、不同/相同模式时的类型切换、默认小说设置、原日期参数和单次刷新。浅深主题与宽窄窗检查所有本地化榜单名称、具名字段及编辑器保留。

从真实日期编辑器键入本地化日期、以 Ctrl+Down 打开日历并通过 headless 指针点选日期；未来/无效文本必须显示本地化警告、保留先前日期且不发送无效请求，清空仍回退最新榜单日期。输出 `ranking-page-contracts.txt`、`offline-requests.txt`、日期状态与日历/警告截图。使用当前进程时区，不能代表原生弹层、其他时区、完整分页或服务端结果已验收。

## user-pages

通过真实用户推荐、用户搜索、自身/他人关注、粉丝、好P友和相关用户页面验证浅深主题及 360/1000 DIP 窗宽。固定 HTTP 捕获路径、用户/关键词/隐私参数；驱动具名刷新、分页、快速切隐私、空查询、注入模型及替换/关闭，断言顺序、去重、旧源清理和迟到结果不继续 next_url。

卡片通过 Enter/Space/指针单次打开原用户 ID；实际 Tab/Shift+Tab 进入/离开头像菜单。菜单执行公开关注/取消并核对请求次数/载荷与标签，复制使用内存剪贴板，屏蔽写入隔离数据库后清理固定记录。资料目标只观察类型/ID 并立即释放，验证其待完成请求取消。输出 `user-page-contracts.txt`、`user-page-requests.txt` 和 PNG。未验证系统浏览器/剪贴板、原生读屏、关注失败及完整账号切换。

## user-details

使用真实用户详情页和受控 HTTP 验证宽窄窗口/浅深主题：完整长名称、PageDown 到介绍末行、实际选择 ID 与内存剪贴板复制、原应用/网页链接、五个列表目的地和隔离数据库屏蔽。公开/私密关注与取消共用待完成状态，核对原载荷、失败保留/重试；故意迟到的详情/关注响应不得更新已关闭页面或切换后的账号。

输出 `user-detail-contracts.txt`、`user-detail-requests.txt` 和 PNG。不验证系统浏览器/剪贴板、原生焦点/读屏、UserBasicInfoPresenter 或完整账号自动刷新；固定失败响应可产生预期应用日志。

## series-page

从真实 SeriesPage 打开漫画和小说监视列表，验证默认类型、键盘切类型、刷新、next_url/顺序/去重、空结果和旧源响应归属。卡片、作者、最新内容分别通过键盘/指针进入原详情页面，检查目标类型、ID、工作类型和详情请求取消；不把目标挂载为在线页面。

实际 Tab/Shift+Tab 遍历内部按钮，使用 headless 平台配置的菜单键（Apps）和右键打开上下文菜单，验证复制、作者/最新内容及 Escape。完整标题通过名称/提示保留，窄窗动作不得被裁掉。输出 `series-contracts.txt`、`series-requests.txt` 和 PNG。平台默认键不代表 Windows Shift+F10 已通过；实际外链、完整详情、系列监视管理与订阅另行验收。

## series-details

使用真实漫画/小说系列详情页，验证原详情端点/ID、完整长标题/介绍、键盘滚动到末行、追更及取消的实际键盘入口和精确载荷。命令切入溢出后仍须保有数据上下文；失败允许重试，直接竞争命令不能绕过共同待完成状态。页面通过既有寿命事件注册，调用注册的关闭路由后已加载作品源清空、内容解除且 HTTP 取消；故意迟到及旧账号响应不能回写。

输出 `series-detail-contracts.txt`、`series-detail-requests.txt` 和 PNG。另断言追更没有创建本地下载订阅；不将其作为完整订阅、WorkSeriesInfoPane 导航或真实服务权限验收。

## work-info

实际构建完整版/精简版 WorkInfoPane，覆盖插画/小说、浅深主题和宽窄尺寸。验证完整作者/标题边界、统计换行、介绍和最后标签的键盘可达性、具名 ID 复制不触发外层作者按钮、长译文标签的原文搜索/原文与译文复制/屏蔽；作者导航保留 ID，立即关闭目标后详情请求取消。复制使用内存剪贴板，屏蔽只写本次隔离设置并清理固定标签。

另挂载真实 IllustrationViewerInfoPane，验证固定、键盘展开/收起、同一作品、原信息/评论/相关作品三个页面与保留区域，检查原收藏/下载/稍后看命令实例。输出 `work-info-contracts.txt`、`work-info-requests.txt` 和 PNG。不执行该面板的收藏/下载业务、完整翻译服务或 OS 外链。

## comment-send

真实 CommentContainer/PixivReplyBar 覆盖插画/小说的顶层评论和回复、浅深主题及 360/1000 DIP。固定服务核对工作 ID、父评论 ID、文本和贴图 ID；真实输入验证原 140 字限制、Enter 不发送、光标处表情占位、方向键分类及非零尺寸的完整标题。贴图使用本地生成的 160×80 固定色图，检查截图中色块的实际 2:1 比例，而非只检查 Stretch 属性。

发送过程中输入仍可编辑，文本与贴图共用忙状态；失败不丢草稿，重试成功只清除仍相同的已发送文本，贴图成功不清除文本。卸载/替换上下文及直接 Window.Close 均取消传给请求的令牌；旧响应不能解锁、插入或清除新编辑器，旧账号成功响应也被隔离。输出 `comment-send-contracts.txt`、`comment-send-requests.txt` 和 PNG。所有发布请求均为固定 HTTP，不发送真实评论；headless Enter 不代表原生 IME 已通过，完整评论分页、删除权限和真实服务仍待验收。

## work-feeds

验证真实 WorkFollowingPage 和 WorkMyPixivPage 的插画/小说、公开/私密初始参数，浅深主题和宽窄布局。选择器用途名称和完整文本必须可读；键盘切换、溢出刷新、分页去重、快速切隐私、切类型释放旧模型、固定失败后刷新重试/空结果和关闭后的迟到结果均有断言。输出 `work-feed-contracts.txt`、`work-feed-requests.txt` 和 PNG。固定失败会进入既有应用日志，不能把它与未预期错误混淆。

上述用户/系列/作品源及下方特辑的 HTTP 故意允许迟到完成，用于检验源所有权。现有 Mako EngineHandle 取消枚举和后续分页，不能据此宣称中断已发出的 HTTP；非空旧页不外推为空页内部 next_url 的所有行为。真实服务权限、账号切换和完整网络失败矩阵仍需补齐。

## work-sources

验证推荐、新作、指定作者投稿三类真实页面的插画/漫画/小说、宽窄窗口与浅深主题。具名类型字段显示完整文本；键盘切换、恢复旧类型时的迟到结果、next_url/顺序/去重、刷新/空结果及关闭后归属都有断言。核对原端点、默认 filter、推荐标志、新作子类型和投稿作者 ID，跨小说边界检查旧模型释放；投稿仅检查空隔离数据库下的原订阅按钮可见性，不执行订阅业务。

输出 `work-source-contracts.txt`、`work-source-requests.txt` 和 PNG。Mako 当前漫画推荐与插画推荐使用相同端点，本模式保留这一既有映射，不能证明服务端漫画过滤。相关作品见 `work-related`；完整失败/权限、订阅同步与账号切换另行验收。

## work-cards

在真实作品列表中验证插画/小说卡片的浅深主题和宽窄布局，包含长标题、统计、标签、缺图、收藏与焦点。脚本驱动指针、Enter/Space、行模式 Enter、Shift 范围选择和平台 Apps 菜单键；实际收藏请求核对 ID/隐私/标签，禁用收藏命中不得打开作品，复用后迟到结果不得回写新模型。上下文菜单的标签编辑/Escape、稍后看、屏蔽、复制使用隔离服务与数据库。保存只验证原命令绑定，未执行下载。

输出 `work-card-contracts.txt`、请求记录与 PNG；内存剪贴板和 headless 菜单不能证明系统剪贴板、Windows Shift+F10、原生读屏或完整列表性能。相邻选择工具栏及完整标签弹层继续由 `work-selection`、`bookmark-tags` 验证。

## work-related

验证真实相关作品页的插画/小说端点、ID、分页顺序/去重、刷新时迟到结果、失败重试/空状态、关闭与折叠后的归属。零 ID 必须得到对应作品类型的空源；注入首页共享预览时检查引用与释放，关闭详情页不得破坏其他所有者。

输出 `work-related-contracts.txt`、`work-related-requests.txt` 和 PNG。现有引擎串行加载，旧在途 HTTP 完成后才观察新源请求；断言证明旧结果不发布、不继续分页，不能证明传输中断。完整账号切换和真实服务权限另行验收。

## history-pages

使用真实隔离 SQLite 和历史/稍后观看页面，覆盖初始两类作品、键盘切换、排序、同 ID 不同类型、重复访问更新时间、数据库重开、作品导航和删除。另有超过单次数据库页大小的固定记录，真实滚动到末尾验证增量分页与虚拟化可达性。检查数据源清空不影响另一数据库，页面关闭后的排队及后续 Changed 通知不再触发加载。

输出 `history-page-contracts.txt`、请求记录与 PNG。稍后看通过真实菜单删除，浏览记录通过原 API 删除，清空通过数据源执行；清空确认弹窗、完整应用重启和原生输入仍待验收，不将数据库重开等同应用重启。

## home-cards

验证首页插画/小说/用户/特辑四类预览、标题与占位文字、自定义背景、加载/失败、具名打开/删除及编辑态键盘选择。待完成的打开动作在进入/退出编辑、卸载重挂或关闭后不得迟到导航；单图/单小说详情失败必须显示失败。真实首页工具栏进入编辑、键盘选择卡片、修改数值位置、快速删除并重读原 YAML，核对位置/参数/颜色与删除持久化。

输出 `home-card-contracts.txt`、请求记录与 PNG。标题/占位前景与实际表面具有对比度断言，真实编辑面板的长译文标题检查完整换行；不据此宣称所有图片内容或系统高对比模式通过。四类预览并不覆盖全部来源引擎，原生拖动/缩放、完整网格碰撞、账号切换和应用重启另行验收。

## home-parameters

在真实首页卡片库中用键盘逐项选择全部来源，独立核对参数字段集合、默认当前账号、类型/隐私/ID/搜索/标签/日期的精确草稿映射。具名字段与长译文须在宽窄窗口可读；无效 ID/空搜索通过真实添加按钮拒绝且不发请求、不保存。排行榜未来日期输入保留上一有效值并显示原本地化警告，草稿模型也必须拒绝；自动日期保留但不应用禁用的手工日期。离开编辑不隐式添加。

输出 `home-parameter-contracts.txt`、`home-parameter-requests.txt` 和 PNG。有效参数只验证草稿，未点击全部有效来源的添加按钮；因此实际添加后的来源 HTTP、完整账号切换、跨时区、原生日历/IME 仍待验收。日期边界沿用产品的当前排行榜可用日期，不改变序列化格式。

## home-sources

在真实首页卡片库中逐项键盘添加全部 19 类有效来源，分别选择两种作品选项；检查原首请求的端点及来源参数、一个实际预览、原 YAML 重读、原目标类型/ID、共享预览与单次请求、键盘删除和资源释放。普通来源与系列详情均走真实工厂，不用并行的模拟映射替换产品实现。单图/单小说/单用户/单系列另测失败默认记录、关闭时 HTTP 取消和迟到成功隔离。

输出 `home-source-contracts.txt`、`home-source-requests.txt` 和 PNG。这是首请求/非空预览和保存重读的验证，不是完整分页、服务权限、账号切换、原生网格操作或应用重启。单小说打开后的正文仍使用独立共享缓存，其取消不由首页详情令牌证明；普通 Mako 引擎在途 HTTP 的边界保持前述说明。

## spotlight

真实特辑列表加载三种分类、长标题/分类和缺图样本，验证原请求、next_url/顺序/去重、模型替换及关闭后旧源隔离。实际 Tab/Shift+Tab 跨卡片，Enter/Space/指针各产生一次 Click；完整文章 URL 的语言路径与查询参数保留在共享外链动作绑定中。

宿主显式确认使用 `NoopLauncher`，不打开系统浏览器。因此输出的 `spotlight-contracts.txt`、`spotlight-requests.txt` 和 PNG 只证明布局、绑定、输入事件和来源契约，不能证明 OS 外链启动成功。分类原有颜色保留，深色主题使用可读的分类前景；完整标题/分类可通过提示取得。

## extensions

使用官方 Pixeval.Extensions C++ demo，固定在 Common 5.0.0 包元数据的源码提交 `20c900a76cb427ad1dac38ed3f64463be2fd496b`。不修改 SDK 或应用依赖；不是 Rust 迁移。需要 Git、CMake 3.24+、C++20 编译器和项目指定 .NET SDK。Windows 本机 VS 2026 示例：

```powershell
./scripts/build-m3e-extension-fixture.ps1 -OutputDirectory artifacts/m3e/my-fixture -Generator 'Visual Studio 18 2026' -Architecture x64
$fixture = Get-Content artifacts/m3e/my-fixture/fixture.json -Raw | ConvertFrom-Json
./scripts/verify-m3e.ps1 -OutputDirectory artifacts/m3e/my-tests -ExtensionFixture $fixture.library
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll extensions artifacts/m3e/my-extensions $fixture.library
```

Linux 可省略 Generator/Architecture，由 CMake 选择本机工具链。脚本只在新目录克隆精确提交、生成并构建官方 demo，记录日志和 DLL/SO 的 SHA256；IDL 生成器 net8.0 通过临时 DOTNET_ROLL_FORWARD=Major 使用已固定的 .NET 10 runtime，脚本 finally 恢复环境变量。

仅向验证工具传入自己构建并信任的原生库。宿主与测试通过显式参数将单个文件复制到隔离数据目录，不扫描个人扩展。测试脚本不继承已有的 PIXEVAL_TEST_EXTENSION_FIXTURE；直接运行测试 DLL 时，可将该环境变量设为可信库的绝对路径。没有固定库时原生用例如实跳过，指定的路径缺失则失败。

宿主验证 15 个扩展、8 个设置元数据、图标、Unicode 文本转换、字节流转换、启停以及设置保存/默认值，并保存浅深页面截图。颜色 uint 已恢复现有 ColorPicker 映射，验证 ARGB/alpha 双向绑定和 uint 字典保存。DateTimeOffset 通过原生 TextBox 编辑完整 ISO 日期时间及显式时区：headless 文本输入/撤销、offset-only 编辑、无效文本保留、修正及模型重置有断言和错误状态截图。double 保留 NumericUpDown 和既有转换器，以 G 格式显示小数，验证 range/step、模型同步及 double 保存。基础值还验证字符串 Unicode 键盘/撤销、布尔 Space、整数 Up 步进和枚举项/键值双向同步；枚举键也经过文件重载和默认值恢复。**完整生成设置 UI 尚未验收：原生日期/数字输入、颜色弹层和缩放尚未操作。** ABI/转换通过不能替代这些 UI 缺口，也不能代表安装压缩包、原生帮助链接、异步失败/取消和所有导出格式通过。

宿主还将非默认值写入现有 YAML，再由真实加载路径创建第二个原生宿主，比较字符串、布尔、整数、小数、ARGB、DateTimeOffset 的完整时间/时区及字符串集合顺序。`persisted-setting-types.txt` 记录未指定类型的 YAML 字典实际 CLR 类型，避免把进程内字典赋值误当成重启恢复证据。

官方 demo 的图像转换主动忽略 `Stream.copy_to` 返回的 HRESULT，三种格式提供器是空操作。因此该样本不能证明 I/O 错误传播或导出内容正确；这些契约需要下一轮单独的可控失败/导出样本，不能用此处成功完成的 Task 代替验收。

## search

在真实 App/页面/AutoCompleteBox 中验证补全，仅使用隔离目录中的固定假账号。宿主覆盖全部 Mako 命名 HTTP 客户端的消息处理器；认证、趋势、搜索选项和补全由本地固定响应提供，未知请求记入失败列表并拒绝，不能退回真实网络。App 内部接口的客户端名根据锁定的 WebApiClientCore 2.1.6 的命名规则指定，不访问私有成员或修改 SDK。

浅深主题分别执行最后关键词请求、新旧响应逆序、方向键选择/Enter 替换、数字立即入口、清空、卸载取消/迟到返回/重新挂载及普通搜索 Enter。路由容器只记录导航目标并释放其 WorkView，不把搜索结果页挂载到网络列表。生产的一秒 debounce 保持原值；完成旧 HTTP 响应后使用短暂异步派发稳定窗口，不将这段时间记作性能。

还以固定 HttpRequestException 验证当前补全失败的错误通知、保留输入/直接搜索、下一查询恢复及旧失败隔离，浅深共 12 个交互组。输出 `search-validation.txt`、`offline-requests.txt`、页面及错误截图。截图不能证明原生弹出层、鼠标命中、IME 或系统焦点已验收。原生输入条件和完整直接 ID/用户结果页矩阵仍见 ACCEPTANCE_CASES；标签首页另由 `search-page` 验证。

## search-page

通过真实 SearchPage、固定热门标签/图片和隔离 SQLite/设置文件验证浅深主题及 360/1000 DIP 窗宽。长固定/历史标签与翻译必须完整显示；键盘上下文菜单执行复制、固定/取消固定与删除，并核对数据库和设置文件往返。复制仅使用 headless 内存剪贴板。

固定、历史和热门动作分别只路由一次，保留完整关键词及热门作品类型；高级选项折叠后普通搜索仍使用当前结果类型，窗口调整保留同一输入框。输出 `search-page-contracts.txt`、请求日志和页面截图。只记录真实结果页的导航目标并释放其列表，不装载在线结果；完整直接 ID/用户搜索及原生输入仍待验收。

## saucenao

使用真实 SauceNaoSearchPage、结果页及嵌入它的 SearchPage。固定存储提供器和 headless 内存剪贴板验证选图/取消/重复打开、打开失败、无效图片替换、旧读取迟到、预览、资源所有权、API Key 门控、短窗滚动及粘贴快捷键；只使用内置图像和 `fixture-*` Key。存储假对象需要实现 Avalonia 12 平台接口，因此只有验证可执行项目启用 `AvaloniaAccessUnstablePrivateApis`，产品项目没有新增该配置；升级 Avalonia 时需要重新核验接口。

结果页通过受控 HttpClient 运行原请求/解析逻辑，Mako 作品详情仍由共用离线拦截器提供。验证 HTTP 限流、服务错误、无效 JSON、内联重试、不与空成功提示并存、结果顺序、跳过不可用单项、响应释放、替换源和卸载/重挂载的迟到响应，以及首个增量回调尚未运行时离开的取消。传输异常不展示含 Key 的请求 URL。

输出 `saucenao-*.txt`、请求证据及页面 PNG。已有增量行为串行化源加载，固定旧传输若故意忽略取消，需先完成它再观察新请求；不将响应归属验证说成并行抢占 HTTP。系统选图/剪贴板、原生快捷键、真实服务/供应方匹配信息及完整结果外链仍另行验收。

## search-options

使用同一完全离线 HTTP 处理器和隔离假账号，通过真实插画/小说表单模型与 Mako 引擎发出 19 个固定搜索请求，保存 `search-requests.txt`。校验关键词、日期、排序、AI、翻译/普通关键词/违规标志、匹配、类型、比例、宽高、工具、语言、原创、可替换、题材和三类长度范围。实际返回固定空页，不能用此结果证明服务端返回排序或权限。

此外逐页验证 Material 表面、360/1000 DIP 窗宽、完整字段标签和实际可聚焦选择器名称；通过键盘启停日期、编辑本地化日期与数值、操作全部布尔选项和枚举选择器，并检查禁用后值保留、滚动可达及窗口调整后仍是原编辑器。布局契约单独写入 `search-options-layout-contracts.txt` 和首尾截图。

三种长度维度分别覆盖非会员五种允许范围（含不限制）和会员任意范围；禁用字段保留可编辑原值，非法/未来范围及非会员热门排序有本地化拒绝。真实 SearchPage 浅深主题通过 NumericUpDown 键盘编辑与搜索按钮路由，验证非会员范围错误不导航、不丢输入，修改到合法范围后仅导航一次；两类区间提示的实际 TextLayout 不得折叠省略。保存 9 组契约及错误截图，原生 IME/DPI/所有选择器仍另行验收。

## login

先验证真实 LoginPage 的浅深主题、360/1000 DIP 窗宽和短窗口滚动：令牌和内外层无障碍 Value/变更事件遮罩、账号下拉项与预览的无障碍名称（空名称回退账号 ID）、辅助接口写入/展开/折叠、已保存账号键盘选择/模型恢复、手工修改与清空、忙碌提示和禁用状态。窗口调整后保留原输入框及草稿，输出 `login-layout-contracts.txt` 与截图。

复用离线 HTTP 处理器，但认证响应由验证步骤手动完成。真实 LoginPage 浅深主题分别验证空输入、忙碌时禁用输入/按钮、重复提交、卸载把取消传递到认证 HTTP、旧响应未完成即重挂载并登录新账号。令旧请求成功返回，确认旧凭据不进入当前用户/SQLite、旧 finally 不结束新尝试；随后新账号正确保存、唯一导航，重新加载账号模型取得刷新后的令牌。

最后让认证失败，确认错误可见、按钮恢复、输入及已有账号保留。另创建两个独立窗口：空输入不打断正在进行的登录，合法新尝试取消旧请求、释放旧页面忙碌状态；旧成功/失败迟到都不写账号、不通知或导航，关闭旧窗口也不取消新请求。输出 8 组契约、请求日志和错误截图。所有令牌为 `fixture-*`，用户 ID 为固定测试 ID，数据仅位于本轮目录。宿主不打开原生 OAuth/WebView；该能力只有生产回调的取消守卫，仍需平台成功/取消/失败验收。自动 token 刷新与账号切换的竞争也不在此模式的通过范围。

## viewer-toolbar

使用 `dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll viewer-toolbar <新证据目录>`。复用离线 HTTP 拦截器，以内置 Pixiv 图标作为固定图片响应，经过真实图像加载管道打开 IllustrationViewerPage；所有数据和浏览记录仅写入该次隔离目录，未知 HTTP 请求失败，不使用真实账号。

浅深主题分别覆盖翻页式/连续式浏览，1000 和 420 DIP 窗宽。验证浮动栏与缩放百分比、旋转/镜像及模型回写、窄窗溢出缩放、Escape 关闭、键盘聚焦时保持可见、自动播放右键间隔菜单和双向开关。缩放断言等待原动画收敛，不修改生产动画时长。输出 `viewer-toolbar-contracts.txt`、`offline-requests.txt` 和页面 PNG；单元测试另覆盖命令参数、CanExecute、动态隐藏、长标签和溢出前后的同一切换控件。

该模式验证的是查看器外层命令栏；不覆盖原生触摸/触控板、IME、GPU、多屏 DPI、真实动画文件、多页图或完整查看器性能。它保留原查看器和 CommandBar 的事件/命令实现，溢出项执行后可继续操作，Escape 或外部点击关闭菜单。

## documentation

使用默认 `EnableMcp=true` 构建，在真实 AboutPage/HelpPage 中验证浅深主题及 1000/360 DIP 窗宽。只在隔离缓存中预置支持者元数据和内置图标，让 About 的真实加载器走缓存分支。支持者原有名单、URI、键盘 Enter/Space，全部依赖许可与独立外链按钮，以及内置 GPL 均有断言。

帮助区逐一用键盘展开/折叠，校验本地化内容、MCP 文档中每个代码块的复制文本、48 DIP 复制按钮及折叠动画完成后隐藏操作。MCP 工具刷新只验证服务关闭时的状态与按钮行为，不启动服务或开放写工具。输出 `documentation-contracts.txt` 与 PNG。另由 `MaterialMarkdownTest` 验证 fenced/缩进代码的 Unicode、引号、既有 tab 归一化、重新设置文档后的复制，以及代码横滚到边界后向正文传递滚轮。

Avalonia headless 使用内存剪贴板和空 URI launcher；操作不会改系统剪贴板或打开浏览器。此模式证明页面命令路由及链接参数，不证明原生剪贴板、外部浏览器、IME、DPI、辅助技术或在线 MCP 工具获取通过。

## dialogs

在真实 App 主题和既有离线 ViewContainerBase 容器中，分别验证浅深主题、1000/360 DIP 的确认/取消、持久错误确认及异步内容更新。保留本地化按钮、Enter/Escape、禁用默认动作和返回值；真实 headless 指针命中遮罩上的按钮，关闭后恢复调用入口的托管焦点。复用离线容器以避免 TabViewContainer 加载时的在线更新检查，不调用真实账号或业务写操作。

输出 `dialog-contracts.txt` 和 PNG。`MaterialDialogTest` 另覆盖长标题/正文/按钮、正文滚动、Tab/Shift+Tab 循环、嵌套关闭和移除下层对话框、外点关闭否决、deferral 期间重复点击、打开时缩放/换主题和入口已卸载。原生 IME、系统焦点、触摸、多屏 DPI 与辅助技术保持待验收。


## comment-list

通过真实插画/小说评论容器与固定分页/删除响应，验证本人权限、主列表及回复删除、重试、刷新/换源、分页边界去重、键盘到末尾、两级回复导航和返回保留列表。行重绑定、卸载/关窗、换账号及旧 finally 不得改变新行；原评论文本、翻译行为和非正方形贴图保持。错误状态不同时显示成功空页。

输出 `comment-list-contracts.txt`、`comment-list-requests.txt` 和浅深/宽窄截图。固定删除只作用于隔离账号；普通分页引擎取消约束枚举/响应归属，不代表在途 HTTP 已中断。真实服务权限、原生 IME/焦点/读屏仍待验收。

## work-series

真实 WorkSeriesInfoPane 覆盖漫画/小说的系列、前后作品、位置、完整名称、缓存邻项及 ID 回退，使用键盘和指针确认单次路由与原类型/ID。插画上下文请求手动乱序完成，校验切换/释放取消及失败保留已知系列；另在真实 IllustrationViewerInfoPane 中检查自然高度和剩余信息空间。

输出 `work-series-contracts.txt`、`work-series-requests.txt` 和 300/360/1000 DIP 截图；只使用固定详情，不验证真实服务内容、原生焦点、窗口外导航或完整阅读器。

## download-pages

真实 DownloadItemView/DownloadItem 配合固定任务对象，覆盖状态/标题/ID 筛选、新任务与状态自动进出、空状态、选择及批量动作、七种状态的主/溢出动作、处理中的禁用、错误对话框和缺失文件反馈。工具栏保留完整提示及独立选择数量，Tab 可进入列表内任务动作；记录删除与可选文件删除仅操作本次产物目录。

另用真实 SingleImageDownloadTaskGroup 和受控 HttpClient，在请求等待期间反复创建、关闭完整 DownloadPage，断言原请求未取消/重启，最终文件字节一致。输出 `download-contracts.txt`、`offline-requests.txt`、PNG 和固定文件。任务状态矩阵使用假任务；受控下载只证明 UI/后台寿命边界，不代表真实网络、系统文件/目录启动、应用退出或重启恢复。

## subscription-editor

把真实 WorkSubscriptionsSettingsExpander 放入设置页同类的滚动堆叠容器，连接原订阅持久化管理器及 WorkSubscriptionDownloadService。覆盖投稿/收藏/系列的七种工作类型组合、原端点/ID/公开收藏参数、无效/不可用目标、共享忙状态、重复添加、草稿保留、换上下文/账号与取消。固定详情响应可延迟；已排队同步跨编辑器关闭继续，删除订阅会取消关联详情并拒绝迟到回写。

初次、单个、全部同步均使用固定空来源；不把其通过外推到真实分页、403/404 整批、限流重试或退出确认。输出 `subscription-contracts.txt`、`offline-requests.txt` 和浅深/宽窄截图。仅本轮独立 SQLite 落库；数据库重读不等于应用重启，未使用真实账号。

## download-folders

真实 DownloadFolderView 导航容器连接原下载页/文件夹模型，展示固定空/非空订阅。验证完整名称、分组文字统计和部分子计数变化、Tab/Shift+Tab、进入/返回的任务身份、新任务即时出现/移除、平台菜单键及批量动作、同步元数据与移除订阅；清空/恢复上下文继续正确显示空状态。

输出 `folder-contracts.txt`、`offline-requests.txt` 和浅深/宽窄截图。任务对象不执行真实下载，菜单同步返回固定空小说来源；真实下载跨页面寿命由 `download-pages` 单独覆盖。原生菜单键、系统焦点/辅助技术和真实服务批次仍待验收。
