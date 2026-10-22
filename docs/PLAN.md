# 纯 Dart 双引擎 FFI 库与 Pixeval 完整迁移

## 目标与边界

实现可复用的 UniFFI/BoltFFI Dart 绑定库，并将 Pixeval 完整迁出 flutter-rust-bridge。首版验收 Windows x64、Android arm64 的 Debug 与 Release。

- 生成器、二进制解析器、运行时和构建集成全部使用 Dart，不使用 Rust bindgen、.NET 工具或 C/C++ shim。
- Rust 仅存在于 native 库和 native 测试夹具中，允许补充导出、异步方法及事件接口。
- UniFFI 与 BoltFFI 在同一版本交付，包括独立使用和同库共存。
- 使用稳定 Dart API；不依赖实验性共享状态或 `isolateGroupBound`。
- 初期支持当前使用的 UniFFI 0.32、BoltFFI 0.31.1；不兼容的协议版本明确报错。

## 包结构与 build_runner 工作流

在 Pixeval 内整理三个可独立发布的包：

- `packages/uniffi_dart`：公共类型、双引擎运行时与资源管理。
- `packages/uniffi_dart_generator`：纯 Dart 二进制解析、元数据模型、代码生成及 Builder。
- `packages/uniffi_dart_build`：Cargo 构建配置、缓存及 Flutter Hooks 集成。

保留已有回调实验骨架，将 Rust 夹具置于 uniffi_dart 内部测试目录（`packages/uniffi_dart/test/fixtures`）；修正 `.gitignore` 对上述 packages 的忽略规则。

**以 `build_runner` 为绑定生成入口，不另设日常开发包装命令。** Builder 可等待异步构建任务完成后输出 Dart 文件，符合其执行模型。[Builder 文档](https://pub.dev/documentation/build/latest/build/Builder-class.html)

- 每个绑定配置对应固定输出：公共绑定文件、两套底层引擎绑定文件和契约清单；不根据元数据临时创建未声明的输出路径。
- 配置包含 native manifest、package、features、组件名称映射及引擎选择规则。通过 Dart `Process` 参数数组调用 Cargo，不拼接 shell 命令。
- Builder 先构建用于提取元数据的 host 产物，再解析、合并和生成；产物使用独立缓存目录，关闭该阶段的 strip 与 LTO。
- 通过 BuildStep 读取 native 源文件、manifest、lockfile、build script 和构建配置，登记依赖；`watch` 能响应文件修改、增加和删除。忽略 target、缓存及生成输出。
- 同一构建配置的 Cargo 操作合并并加锁；输入变化而契约未变化时，不改写绑定文件。
- 日常流程为 `dart run build_runner build` 或 `watch`，然后使用普通 Flutter 命令。
- Flutter Hooks 编译并打包目标平台库，校验目标契约与已生成绑定一致；不在 Hooks 中生成 Dart 源码。Hooks 与 Dart 编译并行，不能承担前置源码生成。[Hooks 文档](https://dart.dev/tools/hooks)
- CI 重新生成并检查差异，确保提交中的绑定与 native 源码同步。

## 双引擎生成与运行时

**元数据与类型**

- 使用 Dart 解析 PE/COFF、ELF、Mach-O 及 `.lib`/`.a` archive，离线读取元数据，不加载目标平台动态库。
- UniFFI 解析元数据、校验信息和跨 crate 引用；BoltFFI 解析 `BFFIMD01` 记录及 JSON 契约。
- 合并为共享语义模型，同时保留各引擎的 ABI、所有权和调用信息；不通过函数名猜测 ABI。
- 覆盖标量、字符串、字节、可空值、集合、record、枚举、带数据枚举、错误、对象及异步调用。`u64` 使用 `BigInt`，字节使用 `Uint8List`。
- 生成普通 Dart 数据类与 sealed 类型，不让 FFI 类型依赖 Freezed。
- 通过组件前缀及显式名称映射消除跨 crate 重名；损坏、冲突或不支持的契约输出定位明确的诊断，禁止静默遗漏接口。

**引擎选择**

- 提供 `auto`、`uniffi`、`boltffi`、`dual` 模式，以及按完整组件与符号名称配置的覆盖规则。
- `auto`：双方都有兼容导出时，无错误的纯标量调用和已验证兼容的直接布局 record 使用 BoltFFI，其余默认 UniFFI；仅有一个引擎导出时使用该引擎。
- 强制选择不存在或不支持的引擎时报错；`dual` 将两套接口放在独立命名空间，便于验证和基准比较。
- 公开值类型可共享，各引擎分别编解码；对象句柄及其方法固定归属同一引擎，禁止交叉传递。
- 不移植 UniFFISharp 基于 .NET 基准得出的性能比例或选择阈值。

**调用与生命周期**

- 两个引擎分别实现 buffer/status、编解码、对象保活和释放，严格区分借用内存与转移所有权的内存。
- 异步方法返回 Dart `Future<T>`，接受可选 `NativeCancellationToken`；运行时实现 start/poll/complete/cancel/free 状态机，处理取消、迟到回调及重复完成。
- Future 唤醒使用 `NativeCallable.listener` 和整数请求标识；先登记状态再调用 native，避免立即完成时丢失通知。
- 对象提供幂等 `dispose()`，finalizer 仅兜底；释放后的调用抛出明确异常，销毁期间等待在途操作安全退出。
- 默认复制返回字节到 Dart 所有的 `Uint8List`；首版不暴露无所有权约束的零拷贝视图。
- Dart FFI 声明表达平台布局，分别在 Windows 和 Android 验证尺寸、对齐、调用约定与整数边界。

## 回调、事件与 Pixeval 迁移

稳定 API 下，`NativeCallable.listener` 只能异步通知，无法直接实现要求同步返回的任意线程回调。[Dart 回调文档](https://api.dart.dev/dart-ffi/NativeCallable/NativeCallable.listener.html)

- 通用库对无法安全绑定的同步跨线程 callback 明确报错，不生成不安全代理。
- Pixeval 在 native 侧实现会话及下载事件接收对象，提供 `async next() -> Result<Option<Event>, Error>` 和幂等 `cancel()`，并由两个引擎导出。
- Dart 提供显式流适配器，将错误、结束、暂停及取消分别处理；不单凭 `next` 方法名称推断流语义。
- 支持 BoltFFI 原生流协议，使用其订阅、唤醒、读取与释放机制；等待操作不得阻塞 Flutter UI isolate。
- 下载进度可按任务合并；状态变化、完成和会话失效事件不得静默丢弃，无法继续交付时明确报告错误。

迁移前建立现有 57 个公开函数及事件接口的对应清单，逐项保留行为：

- native 核心对象和模型由绑定直接生成；展示模型转换放在 Dart 应用服务层。
- 将现有登录持久化、网络配置和共享状态中的必要逻辑整理进 native 应用服务，保持已有 C# 导出兼容。
- 分页改为显式 feed 对象，保留错误和取消语义，消除 Flutter bridge 的全局分页会话注册表。
- ZIP 解压、动图导出及可能阻塞的存储调用在 native 侧提供异步入口，避免替换 FRB 后阻塞 UI。
- 迁移认证、搜索、推荐与排行、图片缓存、下载、历史、过滤器、小说、Booru、SauceNAO、深链接和动图功能，并更新对应测试。
- 完成验收后删除 FRB bridge crate、生成代码、专用事件适配、配置和依赖；Freezed 仅按实际剩余使用情况保留。

## 测试与完成标准

按“元数据与双引擎基础 → 对象、异步和流 → 构建集成 → 应用迁移 → 删除旧桥接”顺序实施；双引擎测试贯穿全部阶段。

- **解析与生成**：各二进制格式、archive、多组件、重复元数据、截断、非法长度、未知版本；生成结果确定、可分析、跨 crate 类型正确。
- **双引擎运行**：分别强制 UniFFI/BoltFFI，覆盖标量、直接 record、嵌套集合、枚举、错误、字节、对象、异步和流；验证共存及禁止句柄混用。
- **资源与竞态**：立即完成、后台唤醒、取消与完成竞争、迟到回调、反复订阅销毁、在途对象释放、double-free 和资源泄漏。
- **构建体验**：干净 checkout 可生成；增删改 native 文件触发 watch；无契约变化时输出不变；缓存、跨平台契约不一致和构建失败诊断正确。
- **Pixeval 回归**：认证持久化、分页隔离、图片与动图、下载控制和事件、退出重登及其他现有功能均通过。
- **平台验收**：Windows x64、Android arm64 的 Debug/Release 构建、启动和集成测试通过；未取得运行设备的测试不得标为通过。
- **性能记录**：删除 FRB 前留存基线，比较两引擎的调用、列表和图片传输、事件延迟、内存及 UI 响应；不预设性能提升。
- **最终检查**：绑定工具链无 Rust、无 native shim；Pixeval 无 FRB 残留；生成文件与契约一致；全部迁移清单项均有验证结果。
