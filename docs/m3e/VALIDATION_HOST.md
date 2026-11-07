# 隔离验证宿主

`tools/Pixeval.MigrationValidation` 只用于迁移验收，不是第二套产品 UI。页面、注册表、设置模型、资源、样式、导航容器和字体全部来自主项目。

在仓库根目录、SDK 10.0.401 环境运行：

```powershell
dotnet build tools/Pixeval.MigrationValidation/Pixeval.MigrationValidation.csproj -c Release
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll inventory artifacts/m3e/my-run/inventory
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll capture artifacts/m3e/my-run/capture
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll benchmark artifacts/m3e/my-run/performance
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll navigation artifacts/m3e/my-run/navigation
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll search artifacts/m3e/my-run/search
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll search-options artifacts/m3e/my-run/search-options
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll login artifacts/m3e/my-run/login
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll ui artifacts/m3e/my-run/native
```

每个输出路径必须不存在。宿主在任何 App/AppInfo 初始化之前设置独立数据目录；设置、账户数据库、扩展、历史、下载目标均指向输出目录下的 `data/`。不读取个人配置，不清理旧证据；不要向此宿主输入真实账号或令牌。验证结束保留数据用于诊断，分享前检查内容。

## inventory

初始化真实 App 服务和资源，导出导航/页面/设置/首页来源元数据到 `inventory.json`。设置只读取名称、类型、范围及层次，不读取 Value。不会启动产品窗口、登录、MCP 服务或更新检查。

固定基线为 22 个导航入口、33 个页面类型、6 个主设置组、76 个设置节点（61 个叶节点）和 19 类首页来源。小说阅读器私有设置与扩展注册项另行验收，不能用主设置清单替代。

## capture

使用真实 Skia 绘制的 Avalonia headless 窗口，1000×720、zh-Hans，分别给窗口设置 Light/Dark 局部主题。数据仍是默认浅色设置，因此深色截图中的主题选择值保持“浅色”；这是局部主题覆盖样本，没有改写配置。

装载真实 NavigationPage 后捕获：设置主页、六个设置分组、导航 YAML 编辑器、帮助、空扩展列表、空登录页。导航标题和附加操作栏一并呈现。对可滚动页面保存首尾位置；展开当前已生成且启用的 SettingsExpander 后补充顶部截图。不会点击下载、登录、导入/导出、重置、更新、文件选择、复制或外部链接。

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

在隔离真实 App 服务中打开 NavigationSettingsPage + NavigationPage，分别验证浅深主题。通过实际命令栏 Click 路由和 headless 平台保存快捷键执行格式化/应用/重置/插入文件夹；验证无效 YAML 显示诊断、禁用应用、保留编辑文本和最后一次有效文件的原始字节。重置/插入/格式化只改变待编辑内容，等待显式应用。固定样本包含中文文件夹、页面键及国际化资源引用。

输出 `navigation-contracts.txt` 的 10 条通过记录和两张无效 YAML 状态截图；任何断言失败使进程失败退出。写入的 `data/Settings/navigation_menu.yaml` 仅属于该次验证，不使用个人导航文件。没有通过私有方法/反射绕过页面命令，也不将 headless 快捷键当作原生 Cmd/IME 的通过证据。

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

还以固定 HttpRequestException 验证当前补全失败的错误通知、保留输入/直接搜索、下一查询恢复及旧失败隔离，浅深共 12 个交互组。输出 `search-validation.txt`、`offline-requests.txt`、页面及错误截图。截图不能证明原生弹出层、鼠标命中、IME 或系统焦点已验收。原生输入条件和完整历史/固定标签/ID 页面矩阵仍见 ACCEPTANCE_CASES。

## search-options

使用同一完全离线 HTTP 处理器和隔离假账号，通过真实插画/小说表单模型与 Mako 引擎发出 19 个固定搜索请求，保存 `search-requests.txt`。校验关键词、日期、排序、AI、翻译/普通关键词/违规标志、匹配、类型、比例、宽高、工具、语言、原创、可替换、题材和三类长度范围。实际返回固定空页，不能用此结果证明服务端返回排序或权限。

三种长度维度分别覆盖非会员五种允许范围（含不限制）和会员任意范围；禁用字段保留可编辑原值，非法/未来范围及非会员热门排序有本地化拒绝。真实 SearchPage 浅深主题通过 NumericUpDown 键盘编辑与搜索按钮路由，验证非会员范围错误不导航、不丢输入，修改到合法范围后仅导航一次；两类区间提示的实际 TextLayout 不得折叠省略。保存 9 组契约及错误截图，原生 IME/DPI/所有选择器仍另行验收。

## login

复用离线 HTTP 处理器，但认证响应由验证步骤手动完成。真实 LoginPage 浅深主题分别验证空输入、忙碌时禁用输入/按钮、重复提交、卸载把取消传递到认证 HTTP、旧响应未完成即重挂载并登录新账号。令旧请求成功返回，确认旧凭据不进入当前用户/SQLite、旧 finally 不结束新尝试；随后新账号正确保存、唯一导航，重新加载账号模型取得刷新后的令牌。

最后让认证失败，确认错误可见、按钮恢复、输入及已有账号保留。另创建两个独立窗口：空输入不打断正在进行的登录，合法新尝试取消旧请求、释放旧页面忙碌状态；旧成功/失败迟到都不写账号、不通知或导航，关闭旧窗口也不取消新请求。输出 8 组契约、请求日志和错误截图。所有令牌为 `fixture-*`，用户 ID 为固定测试 ID，数据仅位于本轮目录。宿主不打开原生 OAuth/WebView；该能力只有生产回调的取消守卫，仍需平台成功/取消/失败验收。自动 token 刷新与账号切换的竞争也不在此模式的通过范围。
