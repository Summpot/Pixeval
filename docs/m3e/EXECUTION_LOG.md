# M3E 执行台账

## 2026-11-07 夜间轮次起点与恢复

首次开始 19:15，完成计划/台账/依赖盘点后于原生窗口绑定时被中断，未修改受版本控制文件；该段和后续等待不计入连续执行时长。用户于 22:28（Asia/Shanghai）要求继续。HEAD `da251010`、当前迁移分支和干净工作区重新核验；原隔离进程已不在。SDK 使用既有独立 10.0.401；Avalonia 12.1.5、M3E `859a69e5`、AutoSettingsPage `dd3ab795` 与全部 gitlink 未改。沿用前版 Windows/Linux 各 400/400，不先重复未变代码的全套测试。

证据根目录 `artifacts/m3e/2026-11-07-night/`。新版 computer-use 通道下创建新的隔离进程（48184），窗口激活、真实截图、鼠标导航和键盘输入均成功；早前工具阻塞不再作为当前全局阻塞。只操作该宿主 `native-resumed/data/`，未登录真实账号、读取个人配置或修改其他工作区。窗口通过自身关闭按钮退出并确认从窗口列表消失。

## 2026-11-07 夜间批次收尾

时间：22:28 恢复执行，23:30 收尾，执行窗口约 62 分钟。中途一次 Escape 停止桌面通道，用户要求继续后恢复；短暂停顿不计实质工作，19:15 的早先盘点段也不计本次窗口。后续发现计划并行新增了默认不占用桌面、集中记账等规则，停止开启新的桌面验收，余下全部使用后台验证。

| 功能单元 | 提交 / 结论 |
| --- | --- |
| 标签提交/候选否决 | `808e946e`；P00.05u 自动契约通过，原生候选/IME 待验收 |
| 固定扩展原生入口与首组验收 | `e27b6614`；P00.04d 限定子集通过 |
| 颜色预览与分量标签 | `7b020bd6`、`bcc3005a`；P00.05v/w 通过，不替换专用控件 |
| 双窗口登录所有权 | `e58daf58`；P00.05x 离线成功/失败迟到通过，平台 OAuth/刷新竞争待验收 |
| 离线页面 CI | `c7eeb4da`；导航/搜索/参数/登录四种模式加入既有双平台工作流，失败退出码传播，上传日志/图片/契约文本，不上传隔离账号/设置目录 |
| IP 输入与旧重复提示 | `471dcde6`；P00.05y 的浅深键盘、值保留和换源契约通过 |
| 兼容规则审计 | 本批最后提交（由 Git 历史定位）；P00.05z 保留当前四条必要规则，补自动对照并校正旧版本注释 |

新增 IP 证据：`ip-list-before.log` 复现修改地址后旧重复错误仍显示。只在输入改变、条目换源和成功删除后清除旧提示；解析/规范化、集合格式、网络策略及控件均保留。四个参数化用例覆盖 Tab/Shift+Tab、IPv6、禁用规范化、撤销、只读、清空、无效添加、重复拒绝和换源后删除不影响旧集合；Windows/Linux 相关各 18/18。订阅只连接控件自身和它持有的子控件，无外部集合订阅、后台任务或额外设置模型；未声明长期内存/原生输入设备已验收。

兼容审计：`MaterialCompatibilityTest` 在同一完整主题中移除兼容 Styles 做对照，确认当前 M3E 仍将 24 DIP BodyLarge 行高传给 32 DIP 文本和回退 TabStripItem；启用兼容后文本自动行高、标签 14/20 DIP 正常，浅深均通过。两条颜色规则的失败/修复证据见 P00.05v/w。没有删除必要兼容样式，也没有新增全局视觉规则。此为 P01.04 的基线准备，不表示整个主题阶段完成。

最终验证：Windows/Linux 完整测试各 **418/418，0 失败、0 跳过**，两个最终进程退出 0；最终 Release 构建均 0 错误/201 个既有警告，未屏蔽警告。证据目录为 `windows-final/`、`linux-final/`（桌面 Debug、依赖/SDK、首轮 412/412）；`windows-ip-final.log`、`linux-ip-final.log`（IP 修复后各 416/416）；最终源码使用 `windows-final-source*`、`linux-final-source*`。重跑均对应新增产品修改/测试，不重跑无关性能基准。Windows 系统 Temp 权限问题仅通过本次子进程 TEMP/TMP 隔离解决，未修改机器 ACL。

页面契约：Windows `offline-ci-windows/` 与 Linux `linux-final/` 的 navigation/search/search-options 分别 10/12/9 组（高级搜索含 19 个真实引擎请求）；login 两平台各 8 组；固定原生扩展两平台 15 个接口/8 类设置、文件重载及退出均通过。工作流 YAML 已解析、Windows 同等 PowerShell 循环实际通过；远程 CI 未运行，Linux 本机为同等 Bash 命令。SDK 10.0.401、Avalonia 12.1.5、全部 gitlink 未改；11 个本轮 C#/XAML 文件在 Windows 与 Linux 验证树 SHA-256 一致（`source-comparison.json`）。

仓库交接：全部本轮功能本地提交，不推送/发布。并行产生的计划规则修改原样留在工作区；发现误入登录提交后，只重新整理刚创建的本地提交索引，将它们与自己的改动分离，保留文件内容（最终登录提交为 `e58daf58`）。最终计划提交只包含本轮 y/z 状态条目及兼容资源/SDK/CI 现状校正，不包含并行规则修改。所有本轮原生窗口已通过自身关闭入口退出；不修改其他工作区。

未覆盖/下一步：P00 整体仍在进行；IME、DPI/触摸/读屏、原生 OAuth/自动刷新竞争及完整扩展安装/导出仍单列。现有局部资源/输入基线已可供后续独立功能单元使用，按工作区更新后的具体契约依赖选范围；不把原生矩阵缺口视为全部后台工作的阻塞，也不把本次通过推定为全局主题迁移完成。

## P00.05u：标签输入的单次提交与候选否决

状态：待验收（自动契约通过，普通标签有 Windows 原生证据，完整候选指针/IME 矩阵待补）；关联 F03/F23/F24，基线 `da251010`。

契约：保留现有 TokenizingBox、AutoCompleteBox、集合绑定与命令；普通 Enter 一次添加、空 Enter 不添加；Unicode/emoji 和多字符分隔符保留未提交后缀；重复项消费但不重复通知；只读/固定长度集合不消费输入；Backspace 先编辑文本、空输入再删除，删除按钮与键盘共用否决；选中候选只调用一次 TokenAdding，拒绝不改成字符串重试，成功使用事件转换后的 Item 并清空候选和输入。

证据与修复：首轮 7 项中 6 过、候选否决失败（`token-input-before.log`，被拒绝对象转成原文本写入集合）。Enter 对已选候选单独处理并消费事件，不再落入普通文本提交。补测成功路径发现 AutoCompleteBox 在 SelectedItem=null 时恢复旧文本（`token-final.log`）；将清空输入移到解除选择/关闭弹层之后。原有专用控件、数据类型、去重规则、最大数量和分隔符算法均保留。

验证：9 个输入场景（含两组浅深参数）与既有 TokenizingBoxLifetime/MaterialWrapper/KeyboardShortcut 合计 25/25，0 跳过（`token-selection.log`）；Release 构建 0 错误，既有警告保留（`token-selection-build.log`）。初次 CLI 筛选参数不支持，改用现有 MTP 的 `--filter` 后实际执行，未把帮助输出算测试通过。相关 C# whitespace 格式化、diff --check 通过；无 XAML/资源变更。

原生基线：`native-resumed` 使用修改前已构建宿主，在浅色真实外壳通过设置→浏览→固定标签，输入 `迁移验证 🌸,second,unfinished`，观察两项与剩余输入；Enter 添加第三项，Backspace 删除第三项，重复 `second,` 不重复添加，焦点持续留在编辑器。正常退出后 YAML 精确保留两个标签。这验证未改的普通输入分支，不冒充候选修复的原生复测；真实截图在本次工具记录中，未伪造 headless 图片为原生证据。

性能/生命周期：仅调整同步提交/清理顺序，无新增任务/订阅；既有弱事件回收用例通过。没有做 GPU/长期内存测量。IME 组合、候选鼠标、DPI/触摸/读屏及复杂重入仍未覆盖，P01 门控保持。回退仅本步骤提交，持久化格式无需回退迁移。

提交：`808e946e`（`fix: honor token suggestion veto and clear committed input`）。补充 Linux：独立树同步本步两个源文件，SDK 10.0.401 Release 构建 0 错误/201 个既有警告，相关 25/25（`linux-token-build.log`、`linux-token.log`）。下一步：给隔离原生宿主显式注入固定官方扩展，补日期/小数/基础编辑器真实输入与浅深主题验收。

## P00.04d：固定扩展的原生桌面验收入口与首组结果

状态：已验收（仅本文所列 Windows 固定操作子集；P00.05k/l/s、F24 完整矩阵仍待验收）。关联 F01/F03/F23/F24。基线 `808e946e`，只修改非发布的验证宿主参数，不修改产品或依赖。

实施：`ui <新目录> [固定库]` 复用现有 extensions 模式的库存在检查与单库复制，在初始化 App 前放进该次隔离 Extensions 目录；extensions 仍强制需要库，其他模式不接受第三参数。宿主 Release 构建 0 错误/205 个既有警告（`native-fixture-host-build.log`）。通过真实设置页注册组进入八类编辑器，没有新造演示页面。

原生操作（进程 43196，`native-extensions/`）：

| 范围 | 实际输入与观察 |
| --- | --- |
| 日期 | 原值 `2025-02-07T08:00:00.0000000+08:00` → `2025-02-07T05:30:00.1234567+05:30`；追加 invalid 出现完整本地化错误/红色下划线；Ctrl+Z 恢复正确值并去掉错误 |
| 小数 | 1.5 正确显示；Ctrl+A 输入 2.75，Up 按固定 0.25 步长变为 3；离开焦点后仍为 3 |
| 枚举 | 鼠标展开显示 Disabled/Preview/Enabled，点击 Preview，弹层关闭且焦点回 ComboBox；重新进入仍为 Preview |
| 颜色 | 打开真实 ColorPicker，RGBA 十六进制页输入 `11223380`、Tab 提交，R/G/B/A 分别为 17/34/51/128；Escape 关闭且焦点回按钮 |
| 主题/布局 | 从真实应用设置切深色，重新进入扩展页保留值；浅色/深色、最大化与 802×601 窗口均实际观察；深色日期追加 x 的错误说明在滚动到底后两行完整可读 |
| 保存/关闭 | 保持无效日期后关闭窗口，确认进程窗口消失、stderr 空；YAML 仍是最后合法日期（完整 ticks/offset）、scale=3、mode=1、color=2148606515（0x80112233）、Theme=Dark |

证据：工具记录中的原生截图/AX 状态、`native-fixture-hash.json`、`native-extensions-stdout.log`/`stderr.log`、隔离 YAML。更新说明曾因网络慢在编辑中迟到显示；观察到焦点变化后先关闭说明并重新确认输入焦点，没有把发给错误焦点的按键算通过。初次设置点击缺坐标映射，刷新窗口截图后恢复；未因此修改产品。

发现：ColorPicker 弹层能够修改颜色，但关闭后的按钮始终显示 Material 普通按钮/箭头，看不到当前色块；浅深均如此。颜色页的 RGB/HSV 标签亦有局部裁切。与日期/小数的自动通过结论分开记录，下一步以固定资源模板/行为用例定位，不宣称颜色完整视觉已通过。

生命周期/性能：两个原生宿主均由窗口关闭入口结束，未崩溃，无新生产订阅/任务；不等价于长期内存/GPU 测量。未覆盖系统剪贴板、IME 组合、DPI 切换、触摸、读屏设备、原生 OAuth、所有扩展安装/导出，也未据此解除 P01 门控。

提交：`e27b6614`（`test: enable isolated native extension UI acceptance`）。下一步：颜色选择器的专用模板被 M3E 通用按钮覆盖的问题。

## P00.05v：颜色选择器当前值预览

状态：已验收（当前色块与所列行为子集），不等价于全部颜色弹层/平台矩阵；关联 F01/F03/F23。基线 `e27b6614`。中途用户 Escape 停止电脑操作后按要求暂停；用户再次要求继续，重新核对三项未提交文件与隔离进程后完成本步，等待不计工作时长。

证据：新增 ColorPickerThemeTest 在修改前断言当前颜色 Border 实际为 `0×0`（`color-before.log`）。M3E ButtonContentRow 将带尾图标的内容按自然尺寸排列，而 Avalonia ColorPicker 的色块只有 Stretch、无自然尺寸。仅捕获 Fluent DropDownButton 主题并通过 `ColorPicker /template/ DropDownButton` 限定作用域，保留原 ColorPicker、Flyout、颜色转换与绑定，不改通用按钮、子模块或依赖。

验证：浅深主题、实色/半透明改变后色块两轴至少 16 DIP；扩展到 MaterialThemeIntegration/MaterialWrapper 共 21/21（`color-fixed.log`），Release 构建 0 错误。另补空格打开原有 Flyout 的断言，1/1（`color-popup.log`）；首次测试参数遗漏的编译错误已修正，没有弱化断言。XAML-Styler 与 C# whitespace、diff 检查通过。Windows/Linux 完整回归将在本轮公共资源修改结束后执行。

原生：`native-color-preview` 的真实设置页已出现蓝色色块；打开分量页输入 RGBA `11223380`，确认 R/G/B/A=17/34/51/128；Escape 关闭后焦点回原按钮，滚动到该条目观察到半透明色块。窗口正常关闭，YAML 保留 uint 2148606515。最初 AX 主树未包含弹层，误疑打开失败；后续截图的独立弹层区域证明其已打开并可编辑，未根据不完整 AX 判定产品失败。真实截图在工具记录，日志/YAML 在忽略的证据目录。

性能/生命周期：只保留现有主题资源引用，无新业务实现、事件订阅或任务；未测长期工作集/GPU。原生本次为浅色，深色自动布局通过；RGB/HSV 仍有可见裁切，将作为下一独立步骤；DPI/触摸/读屏不标为已验收。回退仅本提交，无设置格式迁移。

提交：`7b020bd6`（`fix: preserve the native color picker preview layout`）。

## P00.05w：颜色模型标签的可读布局

状态：已验收（指定原生部件），关联 F01/F03/F23；基线 `7b020bd6`。原生 ColorPicker 的 RGB/HSV 标签可见裁切；新增失败断言记录 32 DIP 行中，20 DIP TextLayout 只获 12 DIP 标签高度（`color-label-before.log`）。

实施：只对上游两个命名 RadioButton 设置 Padding=8,0，保留其现有 M3E ToggleButton 主题、颜色/选中状态和原生 ColorModel 绑定。首次选择器限定 ColorView 模板祖先，只修好独立视图；弹层已移入不同树，测试仍失败（`color-label-fixed.log`），最终以该专用模板的两个部件名限定，不改变通用 RadioButton/ToggleButton。没有复制模板或更换行为控件。

验证：新增独立 ColorView 与 ColorPicker 弹层两例，浅深均检查两个标签非空、完整 TextLayout 与控件边界；真实空格切换 RGB/HSV 更新 ColorModel，当前颜色精确保持。连同色块/MaterialTheme/MaterialWrapper 共 23/23（`color-label-final.log`），Release 构建 0 错误（`color-label-final-build.log`），宿主增量构建 0 错误。XAML-Styler、C# 空白及 diff 检查通过。

原生：新隔离宿主 `native-color-label`（进程 51056）从应用设置选择深色，802×601 窗口打开颜色分量页；截图确认 RGB/HSV 文字完整。鼠标切 HSV 后 H/S/V/A=220/75/80/100，十六进制仍 3366CCFF，切回 RGB 后 Escape 关闭，焦点返回颜色按钮；深色当前色块亦可见。由关闭按钮退出，窗口列表确认消失。浅色自动通过；真实浅色修复后截图、DPI/触摸/读屏仍未覆盖，不能由深色结论推定。

生命周期/性能：只有局部布局值，无新对象模型、订阅或后台任务；完整双平台回归随本轮最终资源验证执行，不重复无关列表性能。P00/P01 门控保持。

提交：`bcc3005a`（`fix: keep color model labels readable in compact rows`）。Linux 相同 23/23、Release 构建 0 错误/201 个既有警告（`linux-color.log`、`linux-color-build.log`）。后续优先补两个登录页面共享客户端时的取消/迟到响应契约。

## P00.05x：跨登录页面的尝试所有权

状态：待验收（离线双窗口自动契约通过；平台 OAuth/自动 token 刷新竞争未覆盖），关联 F05/F06。基线 `bcc3005a`，Mako/其他依赖未改。

证据：扩充既有 login 宿主，新建两个真实 Window/LoginPage，共用应用客户端；旧请求尚未返回时第二页登录，旧请求没有取消、旧页面仍忙碌（`login-multi-before.log`）。每页自己的取消标记不足以保护全应用共享的客户端。

修复：LoginPage 在 UI 线程记录当前登录页面，新尝试同步取消前一页面；取消清除旧所有权/忙碌状态，异步 finally 仍只处理其原尝试，CTS 终结后释放。卸载通过同一取消方法处理；完成/取消均清除全局页面引用，避免静态引用长期保留页面。空刷新令牌在尝试开始前拒绝，不取消其他窗口有效登录。原生 OAuth 回调沿用同一尝试取消守卫，没有增加第二套认证或改动账号格式。

验证：Windows `login-multi-final` 与 Linux `login-multi-night` 各 8 组通过，包括浅深单页已有 6 组、双窗口旧成功/旧失败两组；旧账号不进 SQLite、旧错误不通知任一窗口、旧页面不导航、关闭旧窗口不取消新请求、新页面仅导航一次。最终宿主增量构建 0 错误；日志 `login-multi-final.log`、`linux-login.log`。补测通知计数时的编译错误已修正，未修改产品错误处理。

相关测试首次 14/15，失败在 Path.GetTempFileName 被当前机器系统 Temp 权限拒绝，尚未进入待测业务（`login-multi-tests.log`）。使用进程内 TEMP/TMP 指向本轮隔离 `test-temp` 后相同二进制/相同筛选 15/15（`login-multi-tests-isolated.log`）；不改 ACL、系统设置或测试断言，finally 恢复环境变量。

性能/生命周期：不新增服务、定时器或锁；全局只保留一个正在进行的登录页面，卸载/被替代/成功/失败均解除。未实测原生认证窗口或长期内存，不声称取消会关闭平台 WebView（现 API 无取消参数）；新页面发起后旧请求不遵守 transport cancellation 的迟到路径已覆盖。用户真实账号未读取或使用。回退仅本提交，无数据迁移。

提交：随 `fix: supersede login attempts across windows` 独立提交；后续补准确哈希。下一步把已建立的离线页面契约接入常规 CI，再完成本轮双平台完整回归。

## 2026-11-07 晚间轮次起点

开始：17:38（Asia/Shanghai），HEAD `42105f00`，分支 `feat/material3expressive-migration`；只有既有未跟踪 `compose/`，本轮不修改。M3E `859a69e5`、AutoSettingsPage `dd3ab795` 及其他 gitlink 与上轮一致。系统 dotnet 仍不解析 10.0.401，使用上轮安装的 `%LOCALAPPDATA%/PixevalMigration/dotnet-10.0.401/dotnet.exe`；不改 global.json。

沿用上轮 Windows/Linux 368/368 的已提交结果，不重复未变代码的全套测试。日期/小数模板、业务取消样本及原生 UI 仍是缺口；P00 实施中、P01 门控不变。证据根目录 `artifacts/m3e/2026-11-07-evening/`。原生宿主 PID 28068 在新隔离目录启动，能读取登录/更新说明 accessibility tree，但两次重新选择窗口后激活均报 `failed to activate captured window`；未操作个人账号或设置，已停止该独立进程。原生输入/缩放/焦点不标为通过。

## P00.05j：扩展日期值的精确时区相等性

状态：已验收（模型契约，非日期编辑 UI）；关联 F03/F23/F24。基线 `42105f00`，依赖/SDK 不变。

发现：`ExtensionSettingsEntry` 的通用 EqualityComparer 对 DateTimeOffset 只比较 UTC 时刻。使用相同瞬间、+05:30 → -03:30 的固定样本，编辑被当成未改变，偏移、回调和保存值均不能更新。`date-contract-before.log` 16 项中该项失败，其余 15 项日期解析用例通过；此前构造测试替身误用属性而非 ABI Get 方法的编译错误已修正，没有改 SDK。

修复：仅 DateTimeOffset 使用 EqualsExact；其他设置类型保持原比较规则。同值不重复回调，偏移变化正常通知，保存仍为 DateTimeOffset，重置保留默认 offset 和 100ns ticks。测试替身只提供固定元数据，不加载原生库、真实账户或网络。

验证：测试 Release 构建 0 错误/201 个既有警告；日期模型、解析、扩展 YAML、嵌套设置和 i18n 合计 31/31（`date-value-fixed-build.log`、`date-value-fixed.log`）。解析/UI 注册属于下一独立步骤，不以这份结果宣称 UI 已验收。此步无视觉变化、无新增任务/事件订阅；未测原生性能。回退只撤本步，不变文件格式或默认值。

提交：`318e4893`（`fix: preserve offset-only edits in extension date settings`）。下一步：真实设置页完整日期编辑、无效输入和浅深主题验证。

## P00.05k：完整日期时间编辑及局部 Material 验证反馈

状态：待验收（实现与 headless 自动验证完成；原生 UI 条件仍缺失）。关联 F03/F23/F24/F26。基线 `318e4893`，依赖 gitlink 无变化。

契约与实现：通过现有设置值工厂注册 DateTimeOffset 编辑器，沿用 Avalonia TextBox 的文本选择/输入/撤销和当前 Material 主题。显示完整 ISO 日期时间，允许 0–7 位小数秒且要求显式 ±HH:mm；不推断本机时区、不截成日期。无效/空/缺时区输入保留原文本和最后有效模型，显示本地化错误文字及读屏帮助。原生扩展 setter 抛错显示在同一错误区域，不静默忽略。DateTimeOffset 同一时刻不同 offset 在 Avalonia 双向绑定处也会被去重（`date-ui.log`），因此控件对解析后的值直接写入现有设置，并在挂载/解绑时管理唯一通知订阅；不修改 SDK、持久化和其他控件模板。

自动证据：`date-before.log` 复现缺少编辑入口；15 例解析/边界/格式拒绝、精确模型回调和浅深主题重绑/退订/重新挂载/错误恢复用例，连同 Material 与 i18n 共 45/45（`date-visible-error-tests.log`）。错误 setter 用例曾失败，修复后通过；期间有一次在编译器已读源文件之后编辑导致输出时间较新而二进制仍旧的增量构建，最终在停止编辑后重新构建核验，没有降低断言。

固定原生宿主 `extensions` 浅深均退出 0：键盘文本输入、Ctrl+Z、offset-only 编辑、无效文本保护、模型更新、保存和原有颜色/YAML 重载全部通过。最终 `date-accepted-build.log` 0 错误/201 个既有警告，`date-accepted.log` 退出 0。已查看浅色错误和深色正常截图：第八个条目可见，错误文字完整显示；图片在 `date-accepted/`。这属于真实页面的 headless 绘制/输入，不是原生窗口验收。

Linux：同 SDK 下日期行为/Material/i18n 45/45、固定宿主退出 0（`linux-date/`）；此结果早于最后加入 Material 可见错误文字，最后视觉反馈的 Linux 覆盖留给本轮最终回归。Windows 最终已包含该反馈。

工具与资源：独立安装 dotnet-t4 3.0.0、XamlStyler 3.2501.8 至忽略的 artifacts/m3e/tooling，不变应用依赖。系统临时目录拒绝 t4 写入，改用本轮独立 temp 后生成成功；Language.cs 为实际 T4 产物，只有生成器 runtime 标头及新常量变动。仅默认 zh-Hans 添加提示；JSON 解析、XAML-Styler、相关 C# 空白格式化及 diff --check 通过。

性能/生命周期：没有新增后台任务；控件脱离页面后不再接收模型通知，替换/清空 DataContext 会解除旧订阅，重新挂载读取最新值。未做长期原生内存或 GPU 基准；既有列表/小说基准未受影响，不重复运行。回退仅本提交，用户数据类型/默认值/文件格式保持。

未覆盖：原生 IME、系统剪贴板、读屏设备、触摸、DPI/窄窗；尚未因此勾选 P00.05k 或解除 P01 门控。日期时间完整编辑器作为专用值控件保留，不计划以日期选择器替换。

提交：`d164f6b2`（`fix: restore complete extension date editing with validation`）。下一步：独立补小数显示/步进契约或列表切源取消样本，不依赖原生验收结论。

## P00.05l：生成小数设置的显示与输入绑定

状态：待验收（实现/自动验证完成，原生完整输入矩阵未覆盖）；关联 F03/F23/F24。基线 `d164f6b2`，依赖不变。

复现：普通 NumberSettingsEntry<double> 的真实 SettingsCard 在浅色 en-US 与深色 fr-FR 分别应显示 1.5 / 1,5，实际都显示 2。`number-before.log` 2/2 失败；不能把 Value 属性仍为 1.5 当成显示正确。

实施：应用现有值工厂为 INumberSettingsEntry<double> 注册薄的 DoubleSettingsValue（原 NumericUpDown），使用 G 格式；Min/Max/Step/Placeholder/双向 Value 继续使用原绑定和 DoubleDecimalConverter，未改解析算法、数值类型、默认值、原生 SDK 或 AutoSettingsPage gitlink。按固定依赖的工厂规则在预设前注册，覆盖普通/扩展小数，整数仍走 F0 预设。只有一份应用小数值模板，无第二套设置模型。

验证：Release 测试构建 0 错误/201 个既有警告。普通生成设置在浅深主题、小数点/逗号下，实际 TextBox 键盘输入 2.75/2,75 → Enter、向上步进到 3、模型设回 0.5 均正确；范围/步进与整数 F0/整数值保持。连同 NestedSettings/Material 共 36/36（`number-fixed.log`）。XAML-Styler、diff --check 通过。宿主增量构建 0 错误/1 个既有警告，`number-host.log` 退出 0，实际原生扩展的 1.25 显示、2.75 编辑、double 字典保存、range/step、重置与日期/颜色/YAML 重载全部通过，浅深截图在 `number-host/`；已查看深色图，Scale 显示 1.25。

性能/生命周期：只改变小数值模板的格式，无新增任务/事件订阅；不重跑列表/小说性能。用户文件格式和回退兼容保持，撤回本提交恢复旧模板即可。未验证 decimal 表示范围外的 double、极端精度、所有无效输入/清空路径、原生 IME/剪贴板/DPI/触摸；不宣称完整 P03 或设置 UI 已验收。Linux 最终同轮回归覆盖，不以 Windows 结果替代。

提交：`87c17443`（`fix: preserve fractional values in generated numeric editors`）。下一步：P00.03/P00.05 列表分页中途取消、切源及共享源资源释放样本。

## P00.05m：分页未交付条目与共享源生命周期

状态：已验收（数据提供器的确定性行为基线；不代表列表原生 UI/性能验收）。关联 F07/F08/F09/F13，基线 `87c17443`，依赖未变。

证据：用不响应取消、可手动完成第二次 MoveNext 的固定枚举器，先创建一个条目但不交付。切源后条目 DisposeCount 为 0，分页失败也泄漏；直接关闭 IncrementalSource 后迟到成功/末页仍返回结果。`provider-before.log` 5 项中 4 项失败，共享源原有语义通过。原 ACCEPTANCE_CASES 把图片 SourceLoadLifetimeTest 当作列表切源证据，现已区分。

实施：IncrementalSource 在每次枚举前后同时检查调用和自身寿命令牌，包含 false/末页分支；页面成功交付前拥有已创建条目，失败/取消/工厂异常则逐一释放。清理异常继续释放其余条目并聚合原始异常，没有吞掉错误。保留原工厂、分页大小、去重、计数、限额、被中断枚举器重试及共享提供器实现。

验证：Release 构建 0 错误/201 个既有警告；7 个新增场景及已有集合、重试、WorkListBox、图片源生命周期合计 35/35（`provider-fixed-build.log`、`provider-fixed.log`）。固定旧响应不写入新源，新源仍可加载；共享视图换源不取消另一持有者，最后关闭才释放源/已交付条目一次；错误回调收到原对象，两个释放异常均保留。无任意计时延迟、外网或个人账户。相关格式化与 diff --check 通过。

生命周期：修复已创建但未显示条目的确定性泄漏；正常分页不新增后台任务，不改变列表容器。未测原生 GPU/工作集，既有性能结果不重复套用。UI 线程接收已完成整页期间的取消窗口另行补测；该项不以本步骤自动通过替代。回退仅本提交，无数据格式变化。

提交：`528b0989`（`fix: release unpublished entries when incremental pages fail`）。下一步：完整页面从源到集合的交付取消及通知重入边界。

## P00.05n：整页结果交付时的取消与关闭重入

状态：已验收（集合交付契约，无视觉改动）。关联 F07/F09/F13，基线 `528b0989`。

基线证据：通过排队 SynchronizationContext，源 Task 已成功完成但 UI 续体未执行时关闭集合，整页两个对象均未释放；实际 SimpleViewDataProvider 在首个 Add 通知内关闭时，仍继续添加第二项。`delivery-before.log` 两项均失败，独立于前一分页内部修复。

实施：在源完成、集合接收和逐项交付边界检查取消，只释放未被提供器接收的部分。已交付对象由原提供器释放，避免双重 Dispose；关闭后的清理异常向等待者传播，不能因 OnError 已解绑而当作正常 0 项结果。抽出内部 IncrementalPageLifetime 供源和集合共用同一异常聚合清理逻辑，无全局行为替换。

验证：新增 UI 排队取消、释放异常、首项通知关闭三例，与前步/集合/重试/WorkListBox/图片源共 38/38（`delivery-fixed.log`）；Release 构建 0 错误/201 个既有警告（`delivery-fixed-build.log`）。正常共享加载与取消单个等待者契约仍通过；模拟两个线程之间的交付不用固定延迟。格式化和 diff --check 通过。

生命周期/性能：取消页所有条目恰好释放一次，列表关闭后不再接收剩余结果；正常交付新增廉价 token 检查，Skip 枚举仅在取消路径创建。未重新测不经过分页的静态列表/小说 headless 基准，不宣称吞吐量、GPU 或原生窗口已验收。任意第三方 CollectionChanged 处理器自行抛错、枚举器 DisposeAsync 抛错的组合仍未覆盖；本步骤不改变这些既有异常语义。

提交：`05ca95b3`（`fix: release canceled pages during collection delivery`）。下一步：搜索补全的迟到请求、页面卸载与键盘提交基线；继续保持 P00/P01 门控。

## P00.05o：搜索补全、Enter 与离页取消

状态：待验收（真实页面的离线 headless 行为完成；原生 IME/指针/系统焦点未覆盖）。关联 F10/F07，基线 `05ca95b3`，Avalonia/M3E/Mako gitlink 均未变。

固定样本：新增 `search` 宿主模式，复用真实 App、SearchPage、AutoCompleteBox、NavigationPage 和假账号持久化路径。全部 Mako 命名 HTTP 客户端由本地 Handler 接管，只响应固定认证/趋势/选项/补全，其余请求记录并拒绝。补全响应手动反序完成且故意忽略 transport cancellation，未发真实登录或 API 请求。内部 App endpoint 不可见导致首次工具编译失败；核对锁定 WebApiClientCore 的实际命名实现后按公开命名配置接管，没有添加反射或改 SDK。

基线缺口：`search-before.log` 复现有选中补全时 Enter 不替换文本；锁定 AutoCompleteBox 的 class handler 在页面 bubbling handler 前消费 Enter。第一轮局部修复后 `search-unload-before.log` 再复现页面卸载未取消请求；随后 `search-plain-before.log` 复现无选中项的 Enter 也未搜索。三份失败证据保留，没有通过只设置模型属性替代键盘路径。

实施：同一个既有 Enter 处理器在 tunnel 阶段处理，保留已处理事件守卫、补全各类别与普通搜索入口，不保留第二套处理入口；其他按键继续交给 AutoCompleteBox。卸载使版本失效、取消/释放 CTS、清空候选；挂载读取保留查询并恢复补全；脱离视觉树的绑定更新不启动请求；排队的指针提交检查页面/版本。没有替换控件或修改一秒 debounce。

验证：最终 `search-final.log` 退出 0，浅深共 10 个交互组：最后关键词、反序响应、方向键选择、Enter 保留前缀/光标且不搜索、四类数字入口、清空、离页取消/迟到无回写/重挂载、普通 Enter 恰好导航一次及已处理 Enter 不重复。所有未知请求列表为空，实际请求清单与截图在 `search-final/`。已查看深色父窗口页面；父窗口截图不包含独立补全 popup，不宣称弹层视觉验收。最终宿主构建 0 错误/201 个既有警告，相关 Material/筛选/快捷键 65/65（`search-related.log`），测试增量构建 0 错误/1 个既有警告。XAML-Styler、相关 C# 格式化、diff --check 通过。

生命周期/风险：输入替换/清空/卸载均取消旧请求并释放 CTS，重新挂载不丢查询；真实网络失败、旧失败通知隔离、指针提交、原生 IME/触摸/焦点仍待补，不能据此勾选整项或解除 P01 门控。无列表/小说布局变化，不重复静态基准。回退只撤本提交，账号数据仅在本次隔离目录。

提交：`72e8f889`（`fix: preserve search completion keyboard and page lifetime contracts`）。下一步：固定补全失败反馈与恢复，然后高级搜索请求参数/权限范围基线。

## P00.05p：搜索补全失败反馈与恢复

状态：待验收（浅深 headless 错误通知/恢复通过；原生通知交互未覆盖）。关联 F10/F04，基线 `72e8f889`。

证据：锁定 Mako 的补全调用在传输异常后记录日志并返回 null，SearchPage 原逻辑随后枚举该值，后台 Task 失败且没有用户反馈。固定 HttpRequestException 下 `search-error-before.log` 失败于当前查询必须显示错误通知，保留旧行为失败记录。

实施：对现有 SDK 的 null 失败结果显示本地化错误，保留输入和即时用户搜索入口；额外解析/派发异常记录 FileLogger 后走相同反馈。错误提示派发时检查加载状态、取消、版本和原查询，旧失败不能打扰当前查询；没有改 Mako 的日志/HTTP 行为。仅 zh-Hans 新增提示，Language.cs 由 T4 生成，不补其他语言。

验证：`search-error-fixed.log` 退出 0，浅深共 12 个交互组，含前步全部契约、当前失败、下一查询恢复、旧请求失败不新增通知。`search-error-fixed-build.log` 0 错误/201 个既有警告；i18n/MaterialWrapper 11/11（`search-error-tests.log`），测试增量构建 0 错误/1 个既有警告。已查看浅色错误图：通知明确可读、原查询与即时搜索候选保留；浅深截图位于 `search-error-fixed/`。JSON 解析、T4、格式化及 diff --check 通过。

生命周期/边界：沿用上一提交的 CTS/版本控制，无新定时器或长期订阅；底层原始传输异常仍在隔离日志中，未以空候选伪装成功。没有原生 IME、通知关闭按钮、系统读屏或慢网长期资源指标；不解除门控。可单独回退，用户配置格式不变。

提交：`b8b8a689`（`fix: report search completion failures without losing input`）。下一步：F11 真实高级搜索参数与非会员长度区间基线。

## P00.05q：高级搜索参数、权限与范围保持

状态：待验收（模型/真实引擎请求及浅深页面纠错通过；原生完整编辑矩阵未覆盖）。关联 F11，基线 `b8b8a689`，依赖未改。

证据：新 `search-options` 模式使用实际表单/Mako 引擎和完全离线 HTTP，验证合法参数后，`search-options-before.log` 明确输出 `form accepted 123..456, engine produced null..null`。非会员表单允许自定义长度，但锁定 SDK 构造引擎时静默删除上下限，不能把返回结果当成用户要求的过滤结果。

实施：仅在表单 TryValidate 中拒绝 SDK 不支持的非会员组合，提供明确允许区间，保持用户值供修改；字数/词数与阅读时间分别对应 SDK 的已有规则，会员任意区间仍通过。未改 Mako、请求序列化、查询默认值或数据格式。新增默认语言提示并通过 T4 生成；不添加非默认翻译。

验证：19 个实际引擎请求覆盖插画全部参数、三单位各五个非会员范围及三个会员自定义范围；未来日期、反向区间、会员排序、禁用字段/原值保持也有断言。实际浅深 SearchPage 的数字键盘编辑、按钮警告、保留输入、纠正后唯一导航通过。`search-options-accepted.log` 9 组，`search-options-shared-fixture.log` 补全 12 组均退出 0；构建 0 错误/201 个既有警告（最终工具增量构建 1 个既有警告）。i18n/数字/嵌套设置 11/11（`search-options-tests.log`）。相关格式化、JSON/T4、diff --check 通过。

视觉校正：第一次实际截图显示长区间说明被通知模板截断；加入 TextLayout.HasCollapsed 断言后较短版本仍失败（`search-options-visible.log`）。最终改为三行紧凑区间，浅深的字数/阅读时间提示均不省略；已查看最终浅色图，全部四个允许区间可读。没有放宽断言或修改公共通知模板。

生命周期/边界：没有新生产任务/订阅；假引擎枚举器与页面 WorkView 释放，认证只写隔离假账号。HTTP 参数正确不证明服务端结果、网络权限或原生输入已验收；特殊保留字符的关键词/工具、所有语言和完整选择器交互仍待补。不解除 P01 门控。

提交：`4d6a9d1b`（`fix: reject unsupported novel search ranges before submission`）。下一步：登录页面关闭/重复提交的固定慢认证与迟到响应基线，再执行本轮双平台回归。

## P00.05r：登录尝试的关闭、重挂载与迟到响应

状态：待验收（真实 Token 页面/HTTP/SQLite 的浅深基线通过；原生 OAuth 未覆盖）。关联 F05，基线 `4d6a9d1b`。

证据：`login-before.log` 复现卸载登录页面后实际认证 HTTP token 未取消。当前 Mako.ValidateTokenAsync/SetTokenAsync/SetCodeAsync 已支持 CancellationToken；锁定 OAuth TokenProvider 将该 token 传给真实认证，无需修改 SDK。原生 WebAuthenticationBroker 的现有 API 没有取消参数，未假定可以关闭其平台窗口。

实施：每次尝试独占原视图模型和 CTS，忙碌时拒绝重复提交；传递 token 到设置/校验接口。卸载取消尝试并释放 UI 忙碌状态，任务 finally 才释放 CTS；旧 finally 不得清空新尝试状态。OAuth 回调取得后也检查取消，过期回调不设置凭据；非取消异常继续记录，只有当前尝试显示通知。保留原登录导航、错误分类、账号格式和后台订阅同步入口。

验证：`login-final.log` 6 组浅深契约，含空输入不请求、忙碌禁用、重复点击不重复认证、卸载令牌取消、旧 HTTP 未返回即重挂载开始新登录、迟到旧账号不写 SQLite/当前用户、旧 finally 不干扰新忙碌状态、新账号刷新令牌持久化和重新选择、唯一 Home 导航、认证失败保留输入/已存账号。固定 handler 故意忽略取消并成功返回旧响应，不依赖真实网络或不确定延迟。宿主构建 0 错误/201 个既有警告；LoginFailure/MaterialTheme/ViewModelDisposal 15/15（`login-tests.log`），增量测试构建 0 错误/1 个既有警告。已查看深色错误截图，原令牌输入与两种登录入口可用；格式化和 diff --check 通过。

生命周期/边界：CTS 在异步操作终结后释放，取消不抛用户错误；旧非取消异常仍入日志但不打扰新的登录界面。没有测试原生 WebView 的打开/关闭、两个独立登录窗口的并发切换或自动 token 刷新竞争；后台 token 的完整账户生命周期仍需后续场景，不将 F05 整体标为完成。无新永久服务/订阅，无真实账号操作。

提交：`3e0a38d6`（`fix: cancel login attempts when their page unloads`）。下一步：本轮完整 Windows/Linux 回归与固定生成设置剩余类型的可执行基线。

## P00.05s：固定扩展的基础值编辑器契约

状态：待验收（浅深 headless 自动验证通过，原生输入仍未覆盖）；关联 F23/F24，基线 `3e0a38d6`。

范围：只扩充现有固定官方 C++ 宿主，不改生产模板或依赖。字符串经真实 TextBox 键盘输入 Unicode/emoji 与 Ctrl+Z；布尔经 Space 切换；整数经 Up 使用原生范围和 Step；枚举选择项写回原始键值而非显示标签，模型重置同步控件。枚举非默认键新增文件保存/第二个原生宿主重载以及默认重置检查，补齐原文件重载模式遗漏的第八类设置。

验证：`primitive-final.log` 退出 0，浅深两轮全部断言通过，同时保留日期/颜色/小数/YAML 等既有断言，图像与类型清单在 `primitive-final/`。Release 宿主最终串行构建 0 错误/1 个既有警告。早先同时构建宿主和全套测试导致共享 obj 的 DLL/PDB 文件锁冲突，失败日志保留 `primitive-build.log`、`windows-final/`；停止并改为串行后，Windows 完整测试 398/398、零跳过（`windows-final-serial/`）。不是产品回归，没有删除文件、屏蔽错误或弱化断言。Linux 同步验证进行中，结果记在本轮交接。

性能/生命周期：无生产修改，无新增长期订阅；宿主窗口 finally 关闭、App 统一释放。枚举弹层指针/读屏、所有原生输入以及字符串集合编辑/页面退订仍未覆盖，不把八项数据保存等同完整 UI 验收。

提交：`2d6c5d73`（`test: verify primitive native extension editors`）。下一步：确认 TokenizingBox 外部集合订阅在离页时的释放契约。

## P00.05t：字符串集合编辑器的外部事件寿命

状态：已验收（确定性对象保留及既有同步契约；非原生输入验收），关联 F03/F23/F24。基线 `2d6c5d73`。

证据：TokenizingBox 直接订阅外部 ObservableCollection。只保留设置集合、丢弃编辑器，经隔离 helper 与最多四轮 GC 后编辑器 WeakReference 仍存活。`token-lifetime-before.log` 2 项中回收用例失败，挂载前更新/换源/脱离后仍持有编辑器/重挂载的既有同步契约通过。

实现：复用工程 WeakEventListener，不引入另一套通知实现；换源/清空时立即 Detach 旧监听器，静态事件回调不捕获编辑器，解除监听回调只捕获集合。保留仍被调用方持有但尚未挂载/暂时脱离的编辑器同步，因此不简单在 Unloaded 停止全部模型更新。没有改 TokenizingBox 模板、输入提交/去重/删除/事件否决、最大数量或持久化格式。

验证：新增回收与同步两例，Windows 完整 400/400、零失败/跳过（`windows-lifetime-final.log`、对应 TRX），包括原 Material 包装控件真实键盘/删除回归；Release 构建 0 错误/201 个既有警告（`token-lifetime-fixed-build.log`）。格式化与 diff --check 通过。Linux 同步回归的最终结果记在本轮交接。

生命周期结论：外部集合不再强持有编辑器；弱监听器在编辑器收集后的下一次源事件自退订，换源会立即退订。没有宣称整个窗口弱引用、原生 GPU/长期进程工作集均通过；这些仍需真实窗口验收。回退只撤本提交，不影响用户文件。

提交：`13232d62`（`fix: avoid retaining token editors through settings collections`）。下一步：收尾 Linux 字体下搜索范围提示截断修正及双平台验证交接。

## P00.05q 补充：Linux 字体下的范围提示完整显示

状态：待验收（两平台浅深 headless 已通过，原生输入仍未覆盖），基线 `13232d62`。

Linux 最终页面验证在 `linux-final-restored/search-options.log` 捕获 `All allowed ranges are visible without ellipsis` 失败，虽然同版 Windows 通过。现有通知内容 MaxHeight=80，较宽的 Linux 回退字体让原区间措辞换为额外一行，发生省略；不能只凭 Windows 截图接受本轮新提示。

修复仅缩短默认 zh-Hans 区间表述：≤ / ≥ 表示开放端，保留全部四个范围并明确可选不限，仍为三行。不改共享通知几何、会员权限或校验范围。实际执行 T4，键不变所以 Language.cs 无新差异；没有手工改生成文件或补其他语言。

验证：Windows `hints-final.log`、Linux `linux-lifetime-final/search-options.log` 均退出 0，浅深两个长度提示的 TextLayout 都无 collapsed 行，9 组契约/19 个实际离线引擎请求通过。已查看最终 Windows 浅色及 Linux 深色截图，完整范围可读。两平台完整 400/400、零跳过包含同版资源，原失败断言原样保留。原生缩放/读屏仍不据此通过。

提交：`6abde27a`（`fix: keep search range hints readable across platforms`）。下一步：本轮最终构建、源文件一致性和仓库状态交接。

## 2026-11-07 晚间轮次交接

时间：17:38:41 开始，19:09:20 完成验证并整理交接，约 90.7 分钟（Asia/Shanghai）。本轮连续执行 P00 行为基线/局部编辑器修复，没有用等待、重复无关测试或扩展到其他工作区填时长。最后两项为已证实的集合对象保留和 Linux 字体截断，完成后不再开启大步骤。

### 本轮提交

| 范围 | 独立提交 |
| --- | --- |
| 日期 offset 精确相等、完整编辑与错误反馈 | `318e4893`、`d164f6b2` |
| 小数真实显示与键盘输入 | `87c17443` |
| 分页构造/交付取消及未交付资源释放 | `528b0989`、`05ca95b3` |
| 搜索 Enter/离页取消、失败反馈 | `72e8f889`、`b8b8a689` |
| 高级搜索范围/权限、Linux 提示完整显示 | `4d6a9d1b`、`6abde27a` |
| 登录尝试取消与旧请求隔离 | `3e0a38d6` |
| 基础原生扩展编辑器验证 | `2d6c5d73` |
| TokenizingBox 外部集合弱事件寿命 | `13232d62` |

以上 12 个功能/验证提交之外，本节与计划状态校正独立文档提交。不推送、不发布；保留原专用控件、业务服务与数据格式，没有依赖升级或 Rust 后端工作。

### 最终验证

- 固定 SDK 10.0.401 / runtime 10.0.12，Windows x64 与 WSL Debian 13 x64。桌面 Debug 最终均 0 错误，Windows 10 / Linux 6 个既有警告：`desktop-lifetime-final.log`、`linux-lifetime-final/desktop-build.log`。
- 测试 Release 最终均 0 错误/201 个既有警告；完整 **各 400/400，零失败、零跳过**，显式提供固定官方原生 DLL/SO，TRX 在 `windows-lifetime-final/`、`linux-lifetime-final/`。之前 398/398 是 TokenizingBox 修复前结果，因新增两个生命周期契约及生产修复才重跑完整回归。
- Windows/Linux 宿主 Release 最终增量构建各 0 错误/1 个既有警告。extensions 浅深契约全部通过，八种设置文件重载、日期/颜色/小数及新增基础编辑器见 `primitive-final/`、`linux-lifetime-final/extensions/`。
- 搜索补全 12 组浅深离线交互：Windows `search-error-fixed/`、Linux `linux-final-restored/search/`；高级搜索 9 组/19 个真实引擎离线请求：Windows `hints-final/`、Linux `linux-lifetime-final/search-options/`；登录 6 组浅深认证/持久化/取消/错误恢复：Windows `login-final/`、Linux `linux-lifetime-final/login/`。没有真实账号或网络副作用。
- 同步到独立 Linux 源码树的本轮 31 个 src/tools 文件均与当前工作区 SHA256 相同（`linux-lifetime-final/source-sha256.txt`）；gitlinks、global.json 和依赖声明未变。截图已按各步骤查看，但 **headless 绘制/输入不代表原生窗口人工验收**。
- 一次同树并行构建 DLL/PDB 锁冲突已改串行；一次 Linux Debug 误用 Release 的还原资产导致 AttachDeveloperTools 缺失，按配置重新还原后通过。失败记录保留，没有更改包版本。跨平台范围提示失败通过实际文本修复消除，未降低断言。未重复未受影响的静态列表/小说性能基准。

### 状态、限制与下一轮

P00 仍为实施中。P00.05j/m/n/t 的模型/资源寿命契约已验收；k/l/o/p/q/r/s 的实现和上述自动验证完成，原生交互未覆盖，保持待验收。日期模板缺失、小数四舍五入、搜索/登录迟到请求等已由本轮证据消除，不再列为未实施；它们的原生输入仍单独跟踪。P01 继续受 P00 门控，未声称完成任何后续视觉阶段。

原生独立窗口两次激活均报 `failed to activate captured window`，已关闭本轮专用宿主；缺 IME/系统剪贴板/鼠标弹层/触摸/读屏设备/DPI 与窄窗的实际验收。OAuth 原生 WebView 没有取消令牌接口，本轮只增加回调取消守卫，未证明原生对话框关闭；多登录窗口与自动刷新竞态仍需固定样本。极端 double 范围/精度、任意集合通知处理器异常、枚举器 DisposeAsync 异常组合、完整扩展导出/失败、真实图片/动画/GPU 长期性能及其他平台/远程 CI 仍未覆盖。

下一轮优先：沿 P00.05 补 TokenizingBox 字符串集合实际 Enter/分隔符/重复/删除及 IME 边界，并建立多窗口登录切换/后台 token 刷新迟到响应样本；不依赖原生窗口能力的下载/评论失败取消样本也可继续。可用原生交互环境时，先按 F05/F10/F23/F24 补齐本轮页面验收，再评估 P01 前置条件。

仓库：当前迁移分支 `feat/material3expressive-migration`；收尾检查没有未提交的实现或测试。初始未跟踪 `compose/` 在后续检查中已不存在；本轮未对该路径读取内容、编辑、移动或删除，也未纳入提交，不推断其外部变动来源。原始证据保留在忽略的 `artifacts/m3e/2026-11-07-evening/`，所有正在运行的本轮验证进程已正常结束。

## 2026-11-07 本轮起点

- 开始：2026-11-07 04:59（Asia/Shanghai）；分支 `feat/material3expressive-migration`，HEAD `289a89ab`。
- 工作区只有未跟踪的 `compose/`；本轮不读取、不修改、不纳入提交。
- M3E 固定在 `859a69e563b2a01fc87a26255ee11c1d89959677`，其余 gitlink 与索引一致，没有升级依赖。
- 已有 Material 命令栏、包装控件、内容布局及字体兼容测试属于局部实现与自动验证，不能据此将 P01–P04 标为已验收。所有后续阶段仍受 P00 的验收门控。
- 工具：未发现 Rider/Visual Studio MCP，使用 .NET CLI、PowerShell 和精确补丁；未发现已安装的 XAML-Styler。本轮开始时没有根级或上级 `AGENTS.md`。
- 正式 SDK 缺失已解决：从 Microsoft .NET 10 发布元数据取得 10.0.401 win-x64 ZIP，校验 SHA512 后解压到 `%LOCALAPPDATA%/PixevalMigration/dotnet-10.0.401`，未改 `global.json` 或系统 SDK。校验值：`24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430`。
- 本地原始证据目录：`artifacts/m3e/2026-11-07/`（git 忽略；本台账保留可审阅摘要）。

## P00.03a：测试宿主应用数据隔离

状态：已验收（仅本子步骤；P00.03 固定样本与下载隔离未全部验收）。

基线/契约：F01/F23/F24/F25。审计发现 `ExtensionServiceTest` 枚举 `AppInfo.ExtensionsFolder`，`AppInfo` 静态初始化会清理 `TempFolder`；因此全套基线测试之前必须先隔离，不能直接访问真实用户数据。普通启动、Store 路径选择、设置 YAML、数据库格式保持原契约。

实施：增加宿主可在初始化前设置的 `AppContext` 项 `Pixeval.ApplicationDataDirectory`。仅接受绝对路径，无效值抛错，不能静默退回真实目录。未指定时仍用原路径选择。隔离宿主不执行 Store 数据导入。测试程序集通过模块初始化器为每次进程生成独立目录 `bin/<配置>/net10.0/TestResults/ApplicationData/<随机 ID>/`，保留诊断材料且不加载个人扩展。

自动验证（SDK 10.0.401 / Windows x64）：

- 桌面 Debug 构建成功，0 错误、14 个基线警告；日志 `desktop-debug.log`。
- 测试 Debug 构建成功，0 错误、6 个警告；日志 `tests-debug.log`。
- `dotnet src/Pixeval.Tests/bin/Debug/net10.0/Pixeval.Tests.dll --filter 'FullyQualifiedName~ApplicationDataDirectoryTest' --no-ansi --progress off`：5/5 通过；日志 `data-isolation.log`。
- 覆盖未配置回退、空值/相对路径/非字符串拒绝、全部数据子目录归属、真实设置序列化保存/读取。

人工/UI：本步无视觉变化，不宣称实际窗口验收。隔离启动的实际 UI 验收在 P00.04 记录。

生命周期/性能：路径只在 `AppInfo` 初始化时选取；没有新增常驻服务、后台任务或订阅。保留测试目录作为证据；未测 UI 性能。

未覆盖：原生扩展测试没有固定原生宿主时仍应报告 inconclusive，不代表扩展通过；下载目标须在测试/验证宿主显式设为临时目录，应用数据隔离不会改变普通用户的下载默认值。

提交：`a07f8de9`（`test: isolate migration validation from personal application data`）。

回退：仅撤回该提交；未迁移或删除任何用户数据。撤回后不要直接执行会访问 `AppInfo` 的测试。

下一步：P00.01 指定 SDK 完整基线，P00.02 运行时入口盘点，P00.03 固定样本与 P00.05 高风险契约测试。

## P00.01a：校正上游已修复的字体基线

状态：已验收（完整基线汇总另记）。关联 F01/F26。

基线发现：首次 Release 全套测试 330 项，327 通过、2 失败、1 inconclusive。两项失败均假定 `Anita  Semi-squre` 会在 `Typeface.Normalize` 中抛错；当前锁定的 Avalonia 12.1.5 已不再有此问题。使用本机 ILSpy 读取实际解析的 `Avalonia.Base.dll`，`Typeface.Normalize` 调用 `TryReadWord` 跳过连续空白，没有旧的空 token 异常。这是旧测试假设失效，不能为了维持假设重新禁止合法字体名。

实施：保留生产字体筛选实现；将旧断言改为连续空白名称必须保留，增加空设置回退和实际 Material 主题下 CJK fallback 文本渲染验证。没有降低为“只不抛异常”，仍断言字体名称保留、有效布局和可生成渲染帧。

自动验证：Release 构建成功；字体与三个 Material 测试类合计 34/34 通过，`font-tests-build.log` / `font-theme-tests.log`。原失败证据 `tests-release.log` 和 `full-tests/Pixeval.Tests_net10.0_x64.trx` 保留。

人工/UI：本步仅校正测试；实际 UI 字体切换尚未验收。性能/生命周期：没有生产逻辑变化；新增窗口测试在 finally 关闭、渲染帧 using 释放。

未覆盖：没有要求机器安装 Anita 字体；验证的是该字体名称可解析且可回退，不声称特定系统字体可用。

提交：`cd95845d`（`test: align font baseline with Avalonia whitespace normalization`）；可单独 revert，无用户数据变动。

## P00.01：Windows 指定 SDK 自动验证基线与共用入口

状态：已验收（基线建立，不等于 P00 全功能/跨平台验收）。基线代码 `cd95845d`，M3E gitlink 未变。

环境：Windows 11 Pro for Workstations，内核 `10.0.26300`，x64，16 个测试 worker，约 64 GiB 内存；SDK `10.0.401`，运行时 `10.0.12`。桌面 Debug、测试 Debug/Release 均成功。首次 Release 构建 209 个基线警告，其中 195 个 PE1002 未使用本地化资源警告，另外包括 CS3021、CS0693、CS8604、CS8625、CS9113、AVA3001、CA1416 和 4 个 AVLN3001 无公开构造器资源警告；没有以抑制警告完成验收。

最终完整测试：335 项，334 通过、0 失败、1 inconclusive（隔离目录没有原生扩展宿主）。证据 `tests-release-direct-corrected.log` / `full-tests-corrected/Pixeval.Tests_net10.0_x64.trx`。基线字体失败已在上个独立提交处理。

重要解析版本（源项目的 PackageId 不能误读为已切换到 NuGet）：

| 依赖 | 版本 / 来源 |
| --- | --- |
| Avalonia / Fluent / Headless | 12.1.5 / NuGet |
| AvaloniaEdit / WebView | 12.0.0 / 12.1.0 |
| M3E | 0.0.6 项目标识，实际为 gitlink `859a69e5` 源项目 |
| ComposeAnimation / Shadow / MaterialShapes | 0.3.1-alpha / 0.0.2 / 0.1.3-alpha，均为源项目 |
| MaterialColorUtilities | 0.4.0-alpha / NuGet |
| MaterialSymbols / Rounded | 0.1.2-alpha / NuGet |
| SmoothScroll / Interaction | 12.0.0.1 / NuGet |
| SkiaSharp | 4.154.0 |
| TabView.Avalonia | 1.0.0 |
| MSTest.Sdk | 4.4.1 |

实施：`scripts/verify-m3e.ps1` 验证锁定 SDK，顺序构建桌面 Debug 与测试 Release，直接执行 MTP，保存 git/submodule/SDK/解析依赖/TRX/日志；证据目录必须为空，避免覆盖旧记录。CI 改为 Windows/Linux 矩阵使用同一脚本，并上传证据。Windows 上用 `-SkipBuild` 验证脚本的证据生成和测试调用，334 通过、1 跳过；目录 `script-validation/`。CI 尚未推送/运行，不能称远端矩阵通过。

工具限制：`dotnet test --project ... --configuration Release --no-build` 在当前 Windows 自动化宿主的 MTP named-pipe 连接处抛 `UnauthorizedAccessException`，0 测试执行；日志 `tests-release-corrected.log`。直接执行相同测试 DLL 正常，不改测试断言或降低权限要求。后续统一用脚本/直接 MTP 入口。

人工/UI 与性能：此提交无视觉变化，实际 UI 与性能留给 P00.04；全套测试耗时约 12 秒，只是测试运行耗时，不是应用性能预算。

未覆盖：原生扩展宿主、Linux/macOS/移动端/Browser 实际运行、发布裁剪/AOT 和完整人工矩阵。WSL Debian 13 可用但未预装 .NET / PowerShell，将继续尝试独立 Linux 基线。

提交：`3863e404`（`build: add reproducible Windows and Linux migration validation`）；回退仅脚本/CI/台账，无依赖版本和用户数据变化。

## P00.02a：运行时清单、操作用例及隔离验证宿主

状态：已验收（清单/宿主本身；P00.02 的完整固定扩展入口及所有真实操作仍待执行）。关联 F01–F27。

实施：新增仅开发时使用的 `tools/Pixeval.MigrationValidation`。`inventory` 通过真实 App 初始化与 `SettingsPageViewModel.LocalGroups` / `NavigationPageRegistry` 导出元数据，禁止读取设置 Value。`ui` 使用真实 App 生命周期和外壳；两模式均要求新建证据目录，在 App 初始化之前隔离数据，下载宏也写入该目录。没有修改产品导航、设置注册或用户数据格式，没有复制业务控件。

自动验证：SDK 10.0.401 Release 宿主构建成功（205 个既有依赖/本地化警告，0 错误）；`inventory` 退出 0。导出 22 个导航定义、33 个页面、6 个主设置组、76 个设置节点（61 个叶节点）、19 类首页来源；快照提交在 `BASELINE_INVENTORY.json`。检查键唯一性和每种首页来源均有映射。证据 `validation-host-build.log` / `inventory.log` / `inventory/inventory.json`。

校正：小说设置在阅读器私有侧栏，主设置模型没有 Novel 组；自动下载更新字段标为未使用，未注册编辑入口；History/WatchLater/Download 注册为需要登录，保持原样。`ACCEPTANCE_CASES.md` 覆盖 27 类功能的具体操作/负向条件、动态页面参数、已知快捷键、非主设置入口及自动证据和人工缺口；不是通过报告。

实际 UI 尝试：隔离宿主打开真实 Windows 窗口，UI Automation 观察到未登录导航禁用、Settings/Extensions 可用、刷新令牌输入和 WebView 按钮、首次更新说明与确认按钮。未输入任何凭据。隐藏启动与正常窗口启动各尝试一次，截图均只有桌面背景，`sky.activate_window` 均返回 `failed to activate captured window`；重新枚举窗口后仍无法激活。停止本轮创建的两个独立进程，没有触碰其他应用。**鼠标/键盘、真实浅深主题、窗口关闭保存/焦点与截图验收全部未通过，不用 accessibility tree 冒充交互证据。**

生命周期/性能：inventory 结束调用真实 AppViewModel.DisposeAsync；没有新增产品常驻服务。原生验证进程因工具阻塞而终止，不能据此判断正常退出生命周期。未采样性能。

未覆盖：固定原生扩展、已登录业务页面、实际 IME/触摸/DPI、窗口交互与性能；P00/P01 的门控继续保留。后续选择不依赖此窗口验收的 P00.03 固定样本和 P00.05 异步行为。

提交：`f3ba3701`（`test: export runtime migration inventory from an isolated host`）。回退删除验证工具/清单/记录即可，不影响产品数据。

## P00.04a：Linux 构建与完整测试基线

状态：已验收（自动基线）；P00.04 的原生交互/截图/性能仍待验收。代码基线 `3863e404`；与 Windows 的生产代码/测试相同，尚不含后续验证宿主工具。

环境：WSL2 Debian 13，Linux `6.6.87.2-microsoft-standard-WSL2`，x64；Microsoft 官方 Linux SDK 10.0.401 / runtime 10.0.12。SDK SHA512：`51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b`。

隔离方式：用 `git ls-files --recurse-submodules` 枚举 3329 个受版本控制文件，归档后在 `/home/summpot/.cache/pixeval-m3e-20261107/source` 构建；未复制 `compose/`、Windows bin/obj、个人设置或账户。SDK 和输出也在该专用缓存目录，没有改用户的其他源码工作区。

结果：测试 Release 构建 0 错误、209 个同类基线警告；完整测试 335 项，334 通过、0 失败、1 个固定原生扩展宿主缺失跳过；桌面 Debug 构建 0 错误、14 个同类基线警告。未修改测试来适应 Linux。日志与 TRX 已复制到 Windows 证据目录 `artifacts/m3e/2026-11-07/linux/`；测试耗时 13.3 秒，不代表 UI 性能。

平台矩阵：

| 平台 | 构建/自动测试 | 实际交互/发布 |
| --- | --- | --- |
| Windows x64 | 桌面 Debug、测试 Debug/Release、完整测试通过（原生扩展样本跳过） | 隔离窗口启动，激活/截图失败；IME、触摸、多屏 DPI、焦点和正常退出未验收；裁剪/AOT/安装器未运行 |
| Debian 13 / WSL2 x64 | 桌面 Debug、测试 Release、完整测试通过（同一跳过） | 没有 Linux 桌面输入验收；WSLg 不等价于 Linux 发布环境 |
| macOS | 未执行，缺对应宿主 | 文件选择/剪贴板/窗口/Cmd 修饰键和发布未验收 |
| Android / iOS | 未安装对应工作负载，未执行 | 安全区、返回键、触摸、文件选择、生命周期未验收 |
| Browser | 未执行 wasm 构建/运行 | 保持项目实验状态；不能声称新平台支持 |

回退/生命周期：只新增证据记录，无生产改动。独立 Linux 缓存/测试文件保留用于复现；不进行用户目录清理。

提交：`3306e602`（`docs: record pinned SDK Linux migration baseline`）。下一步 P00.05 对话框异步错误和取消契约，不进入依赖原生 UI 验收的 P01 资源迁移。

## P00.05a：修正异步 headless 测试的等待边界

状态：已验收。关联 F04/F07/F21；仅测试基础设施与调用点，没有生产逻辑修改。

发现：新建对话框错误测试在明显应超时的代码上瞬间“通过”。核对实际 Avalonia.Headless 12.1.5 API 和编译后的测试 IL，`Dispatch` 没有 `Func<Task>` 重载，`Dispatch(async () => ...)` 选择同步泛型重载 `TResult=Task`，只等待外层 `Task<Task>`。这导致 `await` 之后的断言可能没有交回测试框架。不能将这个结果用作行为等价证据。

实施：增加 `DispatchAsync`，显式调用 `Dispatch<bool>(Func<Task<bool>>)`，让 UI dispatcher 存活到整个异步委托完成。修正全部既有异步调用点：对话框（浅/深两例）、Material 列表滚动回收、动画迟到 preview。新增用例在 `Task.Yield` 后故意触发断言失败并由外层断言捕获，证明失败能传回框架且续体仍在 UI 线程。

验证：SDK 10.0.401 Release 构建成功；`HeadlessTestSessionTest|MaterialThemeIntegrationTest|UpdatableAnimatedBitmapTest` 合计 24/24 通过（6.93 秒）。日志 `async-dispatch-build.log` / `async-dispatch-tests.log`。没有删除、放宽现有生产行为断言。

台账校正：之前 Windows/Linux 的 335 项运行总数是实际框架报告，但其中上述 4 个异步用例的后半段证据不可靠；Windows 已由本步重新验证，Linux 待同步该修正后重跑相关类。之前新对话框 6/6 的 `dialog-failure-before.log` 也无效，不计验收，使用修正后的测试结果另行处理。

人工/性能/生命周期：没有原生交互变化；测试 session 现在在异步验证结束后才释放，不再遗留未完成的 UI 测试续体。没有应用性能结论。

提交：`8256f094`（`test: await complete headless UI operations and assertions`）。回退会重新失去异步断言证据，不建议在后续迁移中绕开此入口。

## P00.05b：对话框异常后的重试与释放

状态：已验收（自动行为，未替换控件或视觉）；关联 F04/F21。原生焦点/键盘仍未验收。

契约：按钮和 Closing 事件抛异常必须传回 HideAsync 调用方，当前 Show 保持待完成且可以重试；Closed 抛异常必须传回 ShowAsync，但不能使实例永久处于“已显示”。deferral 等待期间重复请求只处理一次，Cancel 阻止完成后允许重试。

证据：修正异步测试边界后，原实现 6 项中 5 项失败（4 个重试超时、1 个复用时 already shown），1 个取消 deferral 通过。日志 `dialog-failure-reliable-before.log`；之前错误等待方式的绿色结果作废。

实施：RequestCloseAsync 在 finally 中仅为同一未完成显示请求释放关闭门闩；成功请求仍由 CompleteClose 清理。CompleteClose 在通知 Closed 之前清理内部引用和门闩。没有 catch/吞异常，没有改变结果枚举、按钮入口、deferral 等待或 Closing.Cancel。

验证：Release 构建 0 错误、205 个既有警告；新回归 6 项加 Material 集成 10 项，16/16 通过。证据 `dialog-failure-build.log` / `dialog-failure-after.log`。取消令牌和宿主退出仍需单独确认，不据本步声称全部对话框契约通过。

生命周期/性能：异常不再遗留不可重试的关闭状态；关闭后清除 host/TCS 引用，即使 Closed 失败也可重用。无视觉或长期对象新增，无性能结论。

提交：`aed2fb1a`（`fix: recover dialog lifecycle after event handler failures`）。回退仅对话框此处逻辑、对应测试和记录。

## P00.05c：对话框既有取消、嵌套和键盘契约

状态：已验收（headless 自动输入）；关联 F04/F21。没有生产代码变更。

固定的既有行为：预先取消/显示后取消令牌均以 None 完成并触发 Closing/Closed；Closing.Cancel 仍能否决令牌取消，随后显式关闭可以重试。重复 Show 抛异常且不破坏首个显示。嵌套只允许顶层命中，关闭顶层恢复前一层命中和焦点。无修饰 Enter 执行可用默认按钮，Escape 执行可用 Close；Ctrl+Enter 和无 Close 按钮时 Escape 不关闭。

验证：Release 构建成功；新增 8/8 通过，包含实际 Avalonia headless KeyPress/KeyRelease 与焦点管理器检查。日志 `dialog-contract-build-fixed.log` / `dialog-contract-first.log`。未把 Closing 对令牌的否决行为改为强制取消，避免迁移过程中改变既有契约。

未覆盖：宿主关闭/重新安装时的未完成显示、取消与 deferral 竞态、已关闭显示遗留的取消回调、关闭最后一个对话框后的外部焦点、原生 IME/Tab/屏幕阅读器。后续逐项处理，不因当前 8 项通过将 P04 标记完成。

生命周期/性能：测试在 finally 关闭自己创建的窗口，在 session 完整结束后释放；无产品生命周期变化或性能判断。

提交：`ba19f94b`（`test: preserve dialog cancellation nesting and keyboard contracts`）。

## P00.04b：真实页面的隔离渲染快照

状态：已验收（捕获工具和有限样本）；P00.04 的原生交互及性能继续未验收。关联 F01/F03/F21/F22/F24/F26。

实施：验证宿主新增 `capture` 模式，在真实 App 初始化和主题下使用 Skia headless 绘制。页面装入现有 NavigationPage，使标题和附加命令栏保持可见。覆盖设置主页、6 个真实设置组、导航编辑器、帮助、空扩展列表和空登录页；1000×720、zh-Hans、Light/Dark，首屏/部分末尾/启用 expander 展开首屏合计 42 张。不会编辑设置或触发维护/登录命令。模式说明见 [VALIDATION_HOST.md](VALIDATION_HOST.md)。

验证：Release 构建 0 错误；模式运行退出 0，42 张 PNG 非空、尺寸正确、布局边界有限，保存 SHA256/视觉控件数。证据 `capture-settled-build.log` / `capture-settled.log` / `capture-settled/captures.json`。抽查设置卡片、浅深导航编辑器及操作栏、帮助 Markdown、字体展开内容等图像，中文可见、命令栏可见；没有逐张签署完整视觉验收。

迭代证据：`capture-first/` 直接承载 Page 缺少命令栏，不作为最终快照；`capture-navigation/` 补容器但尚无动画稳定窗口；最终使用 `capture-settled/`。图像只存 ignored artifacts，不将大量二进制加入源码。

生命周期/性能：每页在 finally 脱离内容并关闭窗口，真实 AppViewModel 在 UI session 完成前 DisposeAsync；显式使用异步 Dispatch<bool> 等待完整操作。捕获等待真实动画 500 ms 后再布局和绘制；累计时间包含等待/编码/磁盘写入，明确不是性能基线。

未覆盖：完整外壳/标签、登录态业务页、所有嵌套条目、扩展、原生 DPI/触摸/IME/焦点、全部动画终态及绑定日志审查。深色为窗口局部主题，配置值仍为默认浅色。P01 门控未解除。

提交：`53d7623f`（`test: capture isolated migration pages with real navigation chrome`）。

## P00.05d：宿主退出、未完成 deferral 与旧回调

状态：已验收（headless 生命周期）；关联 F04/F21，仍保留原生退出验收缺口。

发现：宿主窗口关闭没有通知对话框的待完成任务。3 个回归全部超时：嵌套显示、按钮 deferral、Closing deferral；证据 `dialog-host-before.log`。关闭后的旧窗口还可通过 AdornedElement 附加引用保留。

实施：订阅 TopLevel.Closed，宿主消失时以 None 完成所有显示，解除事件/adorner 与窗口引用。已失去 UI 的操作不继续等待用户否决或 deferral；存活宿主的令牌取消仍经过原 Closing.Cancel。按钮/Closing 的等待同时观察当前显示完成，旧 deferral 后续完成不会进入下一次显示。取消回调捕获其所属显示任务，完成后不再对可复用实例发送关闭。等待胜出的任务仍正常传播异常。

验证：新退出/迟到用例由 3/3 失败变为通过，另加“Closed 中复用实例后旧取消回调无效”的竞态用例。Release 构建成功；全部 ContentDialog 三类加 Material 集成共 28/28 通过，见 `dialog-host-final-build.log` / `dialog-host-final.log`。保留结果、正常 deferral、取消/否决、键盘和嵌套焦点用例。

生命周期/性能：所有 Show 的 finally 仍负责面板移除/Closed 通知，窗口关闭事件不直接重入集合清理；RunContinuationsAsynchronously 保持不变。测试断言宿主列表清空、IsOpen=false、AdornedElement=null，并在新窗口复用同一实例验证迟到隔离。没有使用长期双实现或新常驻任务。

未覆盖：原生窗口关闭保存、外部焦点恢复、重新安装正在显示的 host、令牌取消事件处理器抛错的传播路径。此步不宣称这些路径已通过。

提交：`7272fe5e`（`fix: release modal operations when their host window closes`）。

补充平台验证：把截至 `7272fe5e` 的 19 个已提交文件覆盖到独立 WSL 源码树，Linux Release 构建 0 错误/201 个既有警告，完整 354 项：353 通过、1 个原生扩展样本缺失跳过；桌面 Debug 0 错误/1 个增量构建警告。证据 `linux-dialog/`。已重新执行 P00.05a 修复的异步用例，早期 Linux 4 项等待边界缺口至此补齐；不包含后续新输入测试。

## P00.04c：列表、长小说与关闭后回收基线

状态：已验收（固定 headless 样本）；关联 F07/F13/F18。完整图片/动画、原生 GPU/手势预算仍未验收。

实施：验证宿主增加 `benchmark` 模式；真实 WorkListBox / VirtualizingWrapPanel 文本卡片 1,000 和 10,000 项，真实 NovelMarkdownBox 500 段/40,890 字符。浅深主题各 5 轮，总计 30 轮；每轮 11 个首尾往返滚动位置。记录首帧、滚动逐帧、托管分配、实现化容器、模板构造与即时/整组结束的弱引用。源码不引用测试程序集，不加载网络图片，不更换业务面板。

环境：Windows x64 10.0.26300、16 逻辑处理器、约 64 GB RAM；SDK 10.0.401/runtime 10.0.12；Release、1000×720、真实 Skia headless CPU 绘制和帧读回。初次 `benchmark-first/` 与构建重叠，仅作工具调试；最终 `benchmark-isolated/` 运行期间没有同时执行构建或测试，2026-11-07 05:45 完成。原始 JSON 提交在 [PERFORMANCE_BASELINE.json](PERFORMANCE_BASELINE.json)。

| 样本 | 主题 | 第一轮首帧 ms | 后四轮首帧中位 ms | 后四轮滚动中位 ms | 峰值视觉项 / 模板 |
| --- | --- | ---: | ---: | ---: | ---: |
| 1,000 文本卡片 | Light | 889.26 | 332.20 | 15.58 | 42 / 42 |
| 10,000 文本卡片 | Light | 1341.26 | 930.81 | 7.34 | 42 / 42 |
| 1,000 文本卡片 | Dark | 76.56 | 88.86 | 7.66 | 42 / 42 |
| 10,000 文本卡片 | Dark | 802.07 | 889.09 | 6.70 | 42 / 42 |
| 500 段小说 | Light | 1212.33 | 942.21 | 3.14 | 1016 控件 / — |
| 500 段小说 | Dark | 739.93 | 881.16 | 3.28 | 1016 控件 / — |

解释：Light 最先执行，包含进程缓存/JIT 逐步预热；不能把浅深差异归因于主题本身。计时包括 CPU 帧读回，不能换算为真实 GPU FPS。1 万项文本卡片每轮托管分配约 372 MiB、小说约 233 MiB；虚拟容器保持 42，但数据规模仍影响首屏和分配，这些是后续迁移需要对照的已有成本，没有顺手进行性能重构。

验证/生命周期：30 轮全部完成；首尾目标确实进入视口，回收卡片绑定正确，清空 ItemsSource 后每个已建卡片 DataContext 为 null。窗口关闭后的即时 GC 仍能看到部分列表对象，推进剩余渲染工作并在全组结束收集后，30 个窗口及其内容弱引用全部失效；最终托管存量约 4.58 MiB。不能据此推导 GPU/所有原生资源已无泄漏。

证据：`benchmark-final-build.log`（0 错误/1 个既有增量警告）、`benchmark-isolated.log`、原始 JSON 和 6 张代表 PNG；已查看万项列表与长小说图像，文本/emoji/行内样式可见。复现和比较规则见 VALIDATION_HOST.md：同条件中位数恶化 >10% 触发调查，不设置跨设备硬阈值。

提交：`c523cba1`（`test: establish bounded list and long novel rendering baselines`）。未改生产逻辑，没有新增依赖。

## P00.05e：筛选输入、补全与诊断选区

状态：已验收（headless 输入）；关联 F04/F06/F07。保留 AutoCompleteBox 与原补全实现。

确认的契约：没有选中补全时 Enter 一次提交原文本和光标；有补全时先替换指定区间、保留前后文字并关闭弹层，下一次 Enter 才提交；hint-only 不改文本也不搜索；Tab 从无选中项开始依次选择并循环，不修改文本；移除分析器清空旧建议；已处理 Enter 不重复提交。诊断范围需截断到有效边界、选中指定文字，并能清空选区。

发现/修复：现有 HighlightDiagnostic 在设置 SelectionStart/End 后写 CaretIndex；Avalonia 的光标更新会折叠选区。测试正确初始化语言、弹层和真实 Tab 选择后，6 项通过、诊断选区 1 项失败（预期 0..3，实际 3..3），证据 `filter-input-contract-before.log`。生产代码仅将 CaretIndex 移到设置选区之前，保留焦点/边界规则和所有补全逻辑。

测试隔离校正：独立运行相关测试发现 WorkFilterNodeTest 的静态字段早于语言注册构造 WorkFilterLanguage，依赖其他测试先运行。为该类显式注册资源，并在测试方法内首次取得语言；没有修改过滤语言或降低断言。初次 UI fixture 未注册语言，以及直接设置 SelectedItem 不会同步下拉选择器的测试假设，均单独修正，不把这些错误归咎产品。

验证：Release 构建成功；WorkFilterInputTest、FilterLanguageTest、WorkFilterNodeTest、KeyboardShortcutTest 合计 37/37，通过 `filter-input-final-build.log` / `filter-input-final.log`。没有修改 XAML 或国际化资源。

未覆盖：原生 IME 组合文本、鼠标补全、屏幕阅读器和触摸焦点；不把 headless TextBox/键盘事件等同平台输入法验收。

提交：`d68cfdcb`（`fix: preserve filter diagnostic selection and input contracts`）。

原生通道补查：使用 cua_repl 绑定第三个独立验证窗口（`ui-cua/`），可读取完整登录/更新说明 accessibility tree；点击确认返回 `coordinate input geometry is unavailable`，截图仍只有桌面背景，窗口暴露的 Raise 操作返回 `failed to activate captured window`。已停止仅本轮创建的独立进程。证据强化了工具通道阻塞；未输入凭据、未操作个人设置，原生验收继续未通过。

## P00.05f：导航编辑器的真实页面命令和文件保护

状态：已验收（隔离 headless 页面/文件行为）；关联 F03/F22/F23。没有修改产品页面或 YAML 格式。

实施：验证宿主 `navigation` 模式在单独进程中初始化真实 App、NavigationSettingsPage、NavigationPage、AvaloniaEdit 和命令栏，避免测试通过反射伪造全局 App 服务。通过路由 Click 与平台命令修饰键 + S 执行实际页面入口，保留界面可见/可用状态检查。

契约/证据：浅深各 5 组，10/10 通过：格式化保留解析语义且不应用；保存快捷键使内存与隔离文件一致；无效 `header: [` 保留编辑内容、显示诊断、禁用应用，Ctrl+S 不改变有效文件的任何字节；重置只改待编辑文本；插入文件夹保留资源引用与光标位置，等待显式应用。`navigation-host-build.log` 0 错误，`navigation-contracts.log` 退出 0；通过清单、两张无效状态截图和实际 YAML 位于 `navigation-contracts/`。

人工/生命周期/性能：已查看深色无效 YAML 截图，诊断和禁用命令可见；不是原生操作验收。每个窗口 finally 脱离内容并关闭，App 服务在 session 结束前释放。没有性能比较或完整标签栏/快捷键平台结论。

未覆盖：原生剪贴板复制页面定义、macOS Cmd、输入法、多窗口同时编辑、文件系统写入失败提示和重启后的外壳导航重建。

提交：`fdd6a7a2`（`test: verify navigation editor commands and invalid data protection`）。

## P00.03b：固定官方原生扩展样本

状态：样本构建与 ABI/数据烟测已验收；F24 整体、设置 UI 和卸载生命周期没有因此自动通过。

来源：Common 5.0.0 的 NuGet 元数据指定 `Pixeval/Pixeval.Extensions@20c900a76cb427ad1dac38ed3f64463be2fd496b`。只编译该提交现有 C++ demo，不修改 SDK、应用依赖或 Rust 工作区。新增构建脚本输出源码提交、工具链日志、库路径和 SHA256；测试脚本通过显式参数复制可信单个库到独立数据目录，绝不读取个人扩展。

构建：Windows CMake / VS 18 2026 / MSVC 19.52，官方 net8 IDL 生成器通过临时 Major roll-forward 在固定 SDK/runtime 10.0.401/10.0.12 上运行；脚本实际构建成功，见 `fixture-script-verified/`。WSL Debian 安装 Git/CMake/G++/Make 后，同一源码成功构建 `.so`（工具包安装日志 `/tmp/pixeval-m3e-*-apt.log`，构建日志在独立 Linux 源码树同级）。未修改系统 Windows SDK 选择或应用锁定版本。

契约：真实 App `extensions` 模式加载 1 个宿主、15 个扩展、8 个设置，验证 SDK ABI、2778 字节图标、Unicode 文本转换、完整字节流复制、禁用/启用、设置保存与 native 默认值重置。浅深各捕获扩展页/设置页。证据 `native-contracts/`、`native-contracts-final/`；宿主正常退出不等于 GC/卸载通过。

发现：设置页只显示 6/8 个条目。`uint` 颜色没有注册 IColorSettingsEntry/value template，DateTimeOffset 与现有 DateTime 模板不匹配；double 通用模板使用 F0，1.5 显示为 2。没有把缺失项标记通过，也没有为了截图改写数据或临时隐藏条目。下一步需要为这些类型确认完整编辑/精度/时区契约后再修复。

完整测试揭示的阻塞：修复卸载前 Linux 361/361 通过（`linux-native-final/`），Windows 运行约 235 项后发生 AccessViolation，调用栈为 ComObject.Finalize → Marshal.Release（`windows-native-final/`）。既有无样本跳过掩盖了 DLL 先释放、COM 包装器后终结的问题。该生命周期缺陷单独在 P00.05g 修复和验证，不能把 Linux 通过外推到 Windows。

性能/未覆盖：未做原生扩展耗时预算；安装压缩包、优先级/重启、下载器/格式输出、异步失败/取消、原生 UI 仍未验收。构建脚本未远程 CI 执行；本地 Linux 使用同等 Bash 命令而非 PowerShell，脚本 Linux 宿主路径由 CI 后续验证。

提交：`5394d441`（`test: add pinned native extension migration fixtures`）。

## P00.05g：原生扩展卸载与包装器终结顺序

状态：固定原生宿主卸载/回收已验收；不是全部第三方扩展或所有失败回调的生命周期保证。

证据：P00.03b 首次注入真实 DLL 后 Windows 完整测试在 COM finalizer 处访问已卸载库崩溃，Linux 同版没有崩溃。确认 ExtensionsHostModel.Dispose 立即 NativeLibrary.Free，但 Host/Extensions 的 COM 包装器仍存活，之后才 Release。

实施：由 ConditionalWeakTable 将同一 SafeHandle 绑定到真实宿主及每个扩展包装器，不永久强持有包装器；Dispose 清空模型的 Host/Extensions/Handle 引用，剩余消费者继续持有的接口仍保留库。无包装器的加载失败立即释放，有包装器的初始化/SDK/接口转换失败也遵守同一寿命。入口原始引用在 finally 释放。没有强制 GC、后台轮询、永久 pin 或第二套扩展加载实现。

顺序依据：[.NET 10 SafeHandle 文档](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle?view=net-10.0) 说明同次收集的普通终结器先于 critical finalizer；COM 包装器正常终结后，SafeHandle 才调用 NativeLibrary.Free。

验证：新增 NativeExtensionLifetimeTest，卸载时仍持有一个原生接口并执行 GC 后再次调用；随后释放引用，最多 4 轮收集断言宿主/句柄弱引用失效，并实际删除 Windows DLL。验证服务移除宿主/设置组、Dispose 幂等以及已释放模型访问 Host 抛 ObjectDisposedException。相关 2/2 通过，Windows/Linux 完整各 362/362、0 跳过（`native-lifetime-tests.log`、`windows-native-fixed/`、`linux-native-fixed/`）。原生测试继续使用单独应用数据目录。

CI：构建固定官方 demo 后把清单中的库传给验证脚本，上传提交/哈希/构建/测试证据。配置已提交但没有推送或执行远程工作流；Windows 本机脚本与 Linux 同等命令分别运行，Linux PowerShell 脚本路径仍待 CI 实际运行。

最终检查补充：入口包装器在接口类型转换前即登记寿命，原始 ppv 由 finally 释放。桌面 Debug 0 错误/10 个既有警告。一次本轮并行构建宿主和测试争用了同一个 Release 输出，出现 CS2012 文件锁（`windows-lifetime-complete/`）；改为串行构建，不调整代码/测试来绕过构建失败，最终结果见 `native-lifetime-serial-build.log`、`windows-lifetime-serial/`。

未覆盖：恶意或 ABI 违约的扩展、OnExtensionUnloaded 抛错、跨库返回对象、原生错误对象/异步后台线程的所有寿命、扩展多次热重载压力。没有声称这些路径安全或已验收。设置模板缺口不依赖此修复，后续独立处理。

提交：`bfdd51d5`（`fix: retain native libraries through extension wrapper finalization`）。

## P00.05h：扩展颜色条目的模板契约

状态：颜色条目和数据绑定已验收（固定 headless）；原生颜色弹层/键盘/触摸未验收。

修复：新增薄的 ExtensionColorSettingsEntry 类型，仅声明已有 IColorSettingsEntry 契约，使现有 AutoSettingsPage ColorPicker 模板匹配。仍继承原 uint 设置、ValueChanged/ValueSaving/ValueReset，不改序列化、ABI、资源、主题或第三方子模块。

验证：先在真实设置页查找颜色控件，旧实现失败 `Sequence contains no elements`（`native-color-before.log`）；修复后 Windows 与 Linux 浅深均通过，见 `native-color-fixed/`、`linux-color/`。检查 native 默认 ARGB、控件→模型、模型→控件、半透明 `0x80224466`、持久化字典仍为 uint；窗口 finally 释放，复用同一组模型。Windows Release 构建 0 错误/205 个既有警告。已查看深色截图，第七个设置条目出现。

自动化：CI 追加串行构建验证宿主与 extensions 页面契约，归档 PNG/报告，不上传隔离设置和账号文件。远程 CI 未运行；两平台已用本地相同模式验收。没有把 DateTimeOffset 缺失和 double F0 格式列为通过，也未用永久分叉的设置编辑器绕过子模块问题。

未覆盖/下一步：颜色弹层的原生交互与色块预览；日期需要保留时区和时间分量，小数格式需要同时覆盖扩展与普通生成设置。后者实现位于 AutoSettingsPage 子模块，需独立依赖变更或有充分行为证据的局部方案，不能顺手改 gitlink。

提交：`aff89292`（`fix: restore native extension color settings binding`）。

## P00.05i：扩展设置经过 YAML/宿主重载后的类型恢复

状态：已验收（现有文件格式与固定原生宿主重载）；原生 UI 另行验收。

发现：进程内 ValueSaving 成功不足以证明重启保持。固定样本先保存非默认值、调用 AppInfo.LoadAppSettings、再创建第二个真实 ExtensionService，旧实现在整数 7 恢复成默认 3 处失败（`native-reload-before.log`）。记录实际 CLR 类型：整数/枚举/颜色读为 Int64，日期读为 String，字符串数组读为 List<object>；旧 TryGetTargetOrAddDefault 只检查 `value is T`，因此默默用默认值替换。独立 YAML round-trip 用例初次 5 项中 4 项失败，见 `extension-persistence-before.log`。

修复：仅在扩展字典读取边界，将写入器产生的表示按声明类型无损恢复。Int64→int/uint 检查范围，Int64→double 检查精确往返及 2^63 上界，DateTimeOffset 按 invariant O 格式保留时间/ticks/offset，字符串集合要求所有元素都是 string。已经匹配的类型仍直接返回；TryGetTarget 的失败读取不改字典。未改 YAML schema、文件位置、Native ABI、默认值、普通设置迁移或用户数据清理策略。

验证：6 个专门用例覆盖 ARGB 全位、日期/ticks/非整小时时区、Unicode/空字符串/顺序、整数与整数形式的 double，以及负数/溢出/小数转整数/超过 double 精度/混合集合拒绝；连同 LegacyAppSettingsMigrationTest 合计 8/8。Release 构建 0 错误/201 个既有警告。真实宿主进一步比较 7 类数据在磁盘与第二个 native host 中的一致性；原宿主随后继续设置回调/显示，兼顾多宿主库引用。

最终结果：Windows/Linux 完整各 368/368、0 失败、0 跳过，均带固定原生样本；桌面 Debug 分别 0 错误/10、6 个既有警告，测试 Release 均 0 错误/201 个既有警告。两平台真实 extensions 模式均退出 0，包含 YAML 重载和浅深颜色断言。证据 `windows-final/`、`linux-final/`；验证宿主 Release 增量构建各 1 个既有警告。没有重复执行未受影响的性能基准。

边界：不承诺任意手写日期格式、任意数值字符串或自定义对象自动转换。官方 demo 的图像实现忽略 CopyTo HRESULT，格式提供器为空操作；已在验证文档注明它不能承担错误传播/导出正确性验收，没有通过弱化断言来宣称这些能力通过。

性能/生命周期：转换只发生在类型不匹配的设置读取；集合生成必要的 string[]，不引入常驻任务或重复设置模型。最终完整测试带原生样本，继续覆盖卸载/GC；原生 UI、时区编辑控件、小数显示和扩展后台线程仍是明确缺口。

提交：`ed19c5c3`（`fix: restore native extension setting types after YAML reload`）。

## 2026-11-07 本轮交接（06:30，Asia/Shanghai）

实际工作窗口：04:59–06:30，约 91 分钟。连续推进 17 个独立实现/验证提交，另以本交接提交收尾；没有等待凑时长、推送、发布、修改 compose 或变更任何依赖 gitlink。

| 已完成的小步骤 | 提交 |
| --- | --- |
| 数据隔离、字体基线校正 | `a07f8de9`、`cd95845d` |
| 可复现验证、运行时清单、Linux 基线 | `3863e404`、`f3ba3701`、`3306e602` |
| 异步测试等待、弹窗异常/取消/键盘/宿主退出 | `8256f094`、`aed2fb1a`、`ba19f94b`、`7272fe5e` |
| 真实页面快照、列表/小说性能与回收基线 | `53d7623f`、`c523cba1` |
| 筛选输入/诊断选区、导航编辑器命令与文件保护 | `d68cfdcb`、`fdd6a7a2` |
| 固定原生扩展、卸载后 COM/库寿命 | `5394d441`、`bfdd51d5` |
| 颜色设置映射、YAML 重载类型恢复 | `aff89292`、`ed19c5c3` |

最终验收证据：`artifacts/m3e/2026-11-07/windows-final/` 与 `linux-final/`。SDK 10.0.401/runtime 10.0.12，桌面 Debug、测试 Release、验证宿主 Release 均构建成功；完整测试各 368/368、零跳过，固定原生宿主页面/重载检查双平台通过。既有 PE1002、AVA3001、CS/CA、AVLN 警告未屏蔽，详细数量见 P00.05i。42 张基础页面快照、30 组性能记录及扩展/导航专项证据保留在本机 artifacts；清单和原始性能 JSON 已入库。

仓库：当前迁移分支；所有本轮实现均已提交。本轮起始就存在的未跟踪 `compose/` 保留原样；无遗留的本轮未完成代码修改。三个原生交互验证宿主均已停止，headless 验证进程正常退出；常规 .NET 构建服务器可能继续驻留。独立 SDK、WSL 源码/工具链和证据目录保留供复现，没有清空个人应用数据。

阶段结论：**P00 仍为实施中，P01–P17 没有因测试通过而自动放行。** 已有 Material 局部实现继续保留；本轮主要补足行为、数据与生命周期证据，并修复验证中暴露的真实缺陷，没有开始全局资源替换。

仍未覆盖或阻塞：

- 原生 UI：三次独立窗口尝试均能读取 accessibility tree，但激活失败、截图只见桌面；CUA 明确返回缺少坐标几何。没有 IME、触摸、DPI、读屏、原生焦点/颜色弹层等通过证据。
- 生成设置：DateTimeOffset 缺少模板；double F0 显示会舍入。数据重载已经恢复，但显示/编辑完整契约未通过。数值模板位于 AutoSettingsPage 子模块，后续依赖修改应独立提交，避免本地未发布 gitlink 破坏复现。
- 固定业务数据：登录/账号切换、权限、慢请求/取消、真实图片/动画和完整查看器、下载恢复等样本尚不齐。headless 文本列表/小说性能不等于 GPU/移动端性能。
- 扩展：官方 demo 不适合验证失败传播/导出内容，安装/重启/优先级、后台线程、跨库对象和热重载压力仍需专门样本。
- 远程 CI 未运行；Linux 本机验证等价 Bash 命令已通过，PowerShell 构建脚本的 Linux CI 执行以及 macOS/Android/iOS/Browser 平台能力未覆盖。

下一轮推荐：先读取本节和 P00.05i，用现有测试/宿主复现已验收边界；优先处理 DateTimeOffset 及小数编辑模板的完整值契约，或补不依赖原生通道的账号切换/增量加载取消与失败样本。为扩展错误/导出另建可控样本。原生通道恢复后补真实页面操作，再评估 P00 是否满足 P01 的前置条件。不要仅因清单中子步骤勾选较多就标记 P00 完成。

## 2026-11-08 无人值守迁移批次

时间：2026-11-07 23:33 至 2026-11-08 01:05:47（Asia/Shanghai），约 93 分钟；基线 `fdd6bfdf`，分支 `feat/material3expressive-migration`，开始时工作区干净。按 §10.3 连续推进六个功能单元，七个代码提交（文档单元在完整回归后追加一次字体修复），最后集中记账。全程后台构建、headless 输入和离屏渲染，没有调用 computer use、全局输入或系统剪贴板，没有推送/发布及依赖 gitlink 变更。

| 步骤 / 功能单元 | 本地代码提交 | 本轮结果 |
| --- | --- | --- |
| P02.04a：查看器浮动命令栏 | `61a4d00b` | 复用 Material 命令主题，保留主项/溢出项身份、切换绑定、缩放及自动播放菜单；本地化溢出入口，键盘焦点下保持可见 |
| P04.03a：通知与 InfoBar | `5c84fad0` | Material 表面、严重度图标与 48 DIP 关闭入口；保留真实通知管理器的持久/超时/容量规则，修复关闭动画中重复执行动作 |
| P05.01a：About/Help/MCP 文档与代码操作 | `e31321ec`、`f6d5ab1f` | 支持者键盘入口、许可独立展开/外链、帮助分区；代码复制不覆盖首行，保留完整文本/滚动链；行内代码使用应用等宽字体回退并响应资源更新 |
| P04.01a：对话框 | `14c04cc4` | 保留结果/deferral/Closing 否决及宿主寿命；Material 表面、长按钮换行和紧凑布局；修复 adorner 锚点导致的不可见/不可命中、Tab 逸出及关闭后入口焦点丢失 |
| P04.04a：查看器状态遮罩 | `bee34777` | 保留原加载/错误绑定及 ProgressBar；错误表面与本地化进度名称，长错误可通过 Tab/PageDown/滚动完整阅读，清除状态恢复底层命中 |
| P04.04b：详情浮动面板键盘可见性 | `879bd0dc` | 焦点进入面板时完全可见，离开后恢复既有距离淡出；锁定、停靠及单次动作保持 |

依赖依据：复用先前 418 项完整基线、已有 Material 主题/字体/命令控件及对话框生命周期契约，只迁移具备对应证据的局部控件。保留专用查看器、通知管理器、ContentDialog、Markdown 渲染与设置生成机制；没有改账号、请求协议、MCP 工具/schema、持久化格式或原生扩展 ABI。P02/P04/P05 更新为实施中，父步骤与 P00/P01 的完整验收不因这些子步骤通过而放行。

验证环境仍为 SDK 10.0.401、Avalonia 12.1.5、M3E `859a69e5`；Windows 与 WSL Debian 13 使用隔离数据和固定原生扩展。最终 `verify-m3e.ps1` 与 Linux 同等命令完成桌面 Debug、测试 Release 构建，完整测试各 450/450、零失败/零跳过。证据根目录：`artifacts/m3e/2026-11-07-unattended/`，最终完整回归见 `windows-complete/`、`linux-complete/`；编译警告保持原有类别，未屏蔽。

页面验证：新增 `viewer-toolbar`、`documentation`、`dialogs` 三种模式均在双平台通过，每种各四组浅深/浏览模式或宽窄窗组合；受共享容器影响的 search/search-options/login 分别通过 12/9/8 组。整页捕获双平台各 42 张，文档专项保留 36 张；Windows 最终相关复测及 Linux 字体修复后的完整捕获分别见 `windows-complete/` 和 `linux-capture-fixed/`。CI 加入新模式，YAML 已解析；没有执行远程 CI。新 XAML 经 XamlStyler，相关 C# 经 whitespace 格式化，文档链接与 diff 检查通过；最终 34 个改动源码文件的跨平台 SHA-256 一致，见 `complete-source-comparison.json` 与 `verified-source-commit.txt`。

关键失败均有修复前后证据：`dialog-before.log` / `dialog-layer.log` / `dialog-expanded.log`、`notifications-before.log`、`overlay-before2.log` / `overlay-final.log`、`pane-before.log` / `pane-fixed.log`。Linux 首次整页捕获因行内代码请求 Consolas 崩溃（`linux-final/capture.log`），通过 CCode 的 MonospaceFontFamily 资源入口修复，新增字体回退/资源更新用例，修复后全部 42 帧成功。关闭 MCP 的 Debug 构建首次沿用 Release 还原产物导致诊断工具扩展缺失；按该配置正常还原后构建通过（`no-mcp-restored-build.log`），没有修改源码绕过错误。

未覆盖：原生 IME、系统焦点/读屏、触摸/触控板、多屏 DPI、GPU，真实外部浏览器/系统剪贴板、在线 MCP 工具获取、完整查看器失败/重试及动画矩阵。headless 托管焦点和内存剪贴板不代表这些平台契约通过。未启动或恢复桌面控制通道；原生缺口继续按具体依赖追踪。

下一轮：从 P02.04 普通 CommandBar 的实际页面入口、长标签/动态溢出/禁用一致性开始，或继续 P04.04 的 Skeleton 与完整浮层生命周期；先补相应基线，再决定视觉改动。当前代码均已提交，最终文档收尾后无本轮遗留修改；不重做已验收部分，不自动恢复 computer use。

## 2026-11-08：清理独立 SDK/构建环境

按用户确认保留 `tools/Pixeval.MigrationValidation` 及应用数据隔离。沿用用户将 `global.json` 的 `rollForward` 改为 `latestMajor` 的修改；`verify-m3e.ps1` 改由 CLI 按该策略解析 SDK，移除精确等于 10.0.401 的重复限制。主计划及宿主用法同步更新；此前固定 SDK 的记录保留为历史证据。

已删除 Windows `%LOCALAPPDATA%/PixevalMigration/dotnet-10.0.401`、临时目录中的 Windows/Linux SDK 10.0.401 安装包，以及 WSL `/home/summpot/.cache/pixeval-m3e-20261107/` 下的 `dotnet`、`source`、`extensions-build`、`extensions-fixture`。删除源码副本前将其中 `artifacts` 移至同级 `source-artifacts`；其他验收产物保留，WSL 专用目录从约 3.1 GiB 降至 8.7 MiB。保留 Debian、系统工具链、验证项目、仓库产物和个人数据；历史记录中的独立 SDK/源码路径不再可用。

验证：系统 SDK `11.0.100-rc.3.26522.148`、应用 runtime `10.0.14`；共用脚本完成桌面 Debug、测试 Release 构建，固定原生扩展参与的完整测试 **450/450，零失败/零跳过**。验证宿主 Release 构建及 `inventory` 模式通过，导出 22 个导航定义、33 个页面、6 个设置组和 19 类首页来源。PowerShell 语法检查及 `git diff --check` 通过。证据：`artifacts/m3e/2026-11-08-sdk-cleanup/`。本轮未重新执行 Linux 或原生 UI 验收。

## 2026-11-08 无人值守迁移批次（01:28 启动）

时间：01:28–03:02（Asia/Shanghai），约 94 分钟；基线 `c8c68ccb`，分支 `feat/material3expressive-migration`，开始时工作区干净。按 §10.3 推进六个功能单元、八个功能提交；跨语言回归发现的导航/扩展页面问题分别追加到对应职责，最后一次收尾集中记账。全程使用后台构建、隔离 headless 输入和离屏渲染，未调用 computer use、全局输入或系统剪贴板；没有推送、远程发布、依赖 gitlink 变更或恢复已删除的独立 SDK/WSL 源码。

| 功能单元 / 步骤 | 本地功能提交 | 本轮完成范围 |
| --- | --- | --- |
| 普通命令栏，P02.04b | `d2090cf0` | 共享本地化溢出入口、完整溢出长标签；保留命令实例/参数、CanExecute、Toggle 双向状态与动态次级项 |
| 导航编辑器，P06.06a | `54aa1895`、`f76daf9b` | Material 编辑边界、宽窄布局、诊断/参考区滚动；保留同一编辑器、选区、撤销、文件保护；页面复制输出有效 YAML；为本地化长命令预留溢出槽宽 |
| Skeleton，P04.04c | `6cc54041` | Material 表面与 shimmer；自身/祖先隐藏、卸载和换模板停止无限动画，重新显示恢复，退订后可回收；保留真实列表/头像加载状态 |
| 作品工具栏/选择，P08.02a/03a/04a | `7f0c2366` | 紧凑排序/筛选布局，选择开关双向同步；全选/反选/清空、刷新、替换源及附加命令；不透明卡片上可见的键盘焦点与回收检查 |
| 扩展管理，P05.02a | `ac175e21`、`fc6bc235` | Material 卡片、长名称/作者、具名启停、优先级和待卸载/取消的文件往返；管理栏长标签可读；空串/空白帮助地址隐藏入口，有效帮助目标保留 |
| 收藏标签，P03.04a | `80c61b81` | 紧凑新增编辑器、具名图标、长标签/去重/取消/隐私和十标签上限；真实 Flyout 尺寸适配、异步确认去重、失败重试、Escape/迟到完成与事件退订 |

收尾同时将新增标签输入的 `Watermark` 改为 Avalonia 12 的等价 `PlaceholderText`，保持同一翻译资源，消除本轮引入的 AVLN5001；该改动已包含在最终四语言标签检查和构建中，不为一行兼容性清理另拆提交。新增字符串写入四语言 JSON，并通过原 `Language.tt` 生成资源标识。T4 工具仅装在 artifacts 工具目录；默认临时目录权限失败后使用本批次临时目录完成生成，没有手改生成文件。

依赖依据：复用 SDK 清理后 450 项完整基线、既有 Material 主题及真实命令/列表/编辑器实现，补对应行为证据后实施局部迁移。没有替换虚拟化、数据源、导航键/图标序列化、扩展 ABI、账号协议或 MCP schema。父步骤及 P00/P01 的完整验收没有因本轮子步骤通过而放行。

证据根目录：`artifacts/m3e/2026-11-08-unattended/`。系统 SDK `11.0.100-rc.3.26522.148`、应用 runtime `10.0.14`，Avalonia `12.1.5`、M3E `859a69e563b2a01fc87a26255ee11c1d89959677`。最终 `./scripts/verify-m3e.ps1 -OutputDirectory artifacts/m3e/2026-11-08-unattended/windows-complete -ExtensionFixture <固定 C++ demo DLL>` 完成桌面 Debug、测试 Release 构建，分别 0 错误/6 和 201 个警告；完整测试 **462/462，零失败、零跳过**，TRX 和工具链/依赖/原生样本记录均在 `windows-complete/`。警告数量受增量构建范围影响；规范化警告文本与 SDK 清理基线相比没有新增项，见 `warning-delta.txt`，未屏蔽既有警告。

最终本地 `dotnet publish src/Pixeval.Desktop/Pixeval.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/m3e/2026-11-08-unattended/native-aot-complete` 成功。`native-aot-complete-header.json` 和 SHA256 记录确认 x64 PE32+、CLR 头 RVA 为零；没有启动该可执行文件。既有依赖的 IL2026/IL3050 等裁剪/动态代码警告仍保留在构建日志中，编译成功不等于原生启动或完整裁剪运行时矩阵通过。

四语言验证使用新增 `PIXEVAL_MIGRATION_CULTURE`，由真实 App 资源加载语言，记录实际文化/runtime/OS；不存在的翻译语言被拒绝，单项缺译仍遵守产品回退规则。`zh-Hans/en-US/fr-FR/ru-RU` 各通过 navigation 42 条契约、extension-management 4 组、work-selection 12 组、bookmark-tags 10 组。最终路径与数量见 `locale-validation-summary.json`；俄语导航在 `localized-final-ru-RU-navigation/`，其他最终样本在 `locale-review-<culture>-<mode>/`。覆盖浅深主题、宽窄窗、作品三种布局和插画/小说标签；托管扩展元数据与原生 ABI 样本分开，文本哨兵没有作为 DLL 加载。

受共享控件影响的页面回归保留在 `final-*`：整页捕获 42 帧、查看器工具栏 4 组、搜索 12 组、高级搜索 9 组/19 个实际引擎请求、登录 8 组均通过。局部语言修正后的导航/扩展截图由上述最终专项目录补充。`native-extension-final/` 验证固定 C++ 样本的 15 个接口、8 类设置、Unicode/字节流及 YAML 类型往返；最终完整测试继续包含原生样本。普通命令栏相关测试 34 项、Skeleton/主题/来源寿命 19 项、作品键盘焦点 2 项亦有专项日志，并已纳入完整回归。未重复运行不使用本轮实际模板的旧文本性能样本；动画时钟/回收有专项断言，GPU 和完整业务页面性能仍待验收。

关键失败与修复证据：导航参考复制原先输出记录对象字符串（`navigation-copy-before.log`），紧凑工具栏筛选框覆盖右侧命令（`work-compact-before.log`），收藏新标签输入超出窄窗（`bookmark-before.log`），提交时禁用焦点控件使 Escape 路由丢失（`bookmark-lifetime2.log`）。跨语言检查又发现俄语“插入文件夹”位于窗口外（`locale-ru-RU-navigation.log`），以及空帮助地址产生无效入口（`localized-final-ru-RU-extension-management.log`）；最终专项均验证修复。收藏标签有意触发回调失败，保留 App 异常日志并断言可恢复重试，不把该固定异常误记为产品失败。

验证宿主新增模式及俄语导航回归已接入 CI；工作流 YAML 和四段 PowerShell 语法检查通过，未执行远程 CI。改动 XAML 经 XamlStyler，相关 C# 经 whitespace 格式化；文档链接及 `git diff --check` 通过。`verified-source-hashes.json` 保存 31 个改动源码/宿主/CI 文件的 SHA256，最终检查与验证时一致。构建/测试/headless 进程正常退出，常规构建服务器可继续驻留；本轮代码和文档均由本地提交收尾，验证产物保留。

未覆盖与下一步：本轮 WSL Debian 没有系统 dotnet/rsync，未重建已删除环境，也没有重新验证 Linux；macOS/移动端/Browser 和远程 CI 同样未运行。原生 IME、系统焦点/剪贴板/读屏、触摸、DPI、GPU、实际收藏或批量业务写入、扩展安装/帮助或目录实际打开/重启删除仍待验收。导航及扩展页的槽宽配置只恢复已测语言/默认字体下的可达性，上游普通命令栏不逐项测量宽度的缺口仍存在，P02.04 不勾选完成。下一批可独立处理普通栏实际测量/字体缩放方案，或为 P08 批量业务、分页/账号切换与取消建立固定服务样本；继续沿用后台验证，不自动恢复 computer use。
