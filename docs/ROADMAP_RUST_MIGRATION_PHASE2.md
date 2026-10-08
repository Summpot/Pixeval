# Pixeval -> Rust Core 深度迁移路线图 (Phase 2 & Phase 3)

本文档承接 Phase 1 基础架构落地，基于全量代码审查报告中所确证的技术差距与缺陷清单，制定将 Pixeval **几乎所有业务与领域逻辑全面下沉至 Rust Core**，并在此过程中**全量修复既有存量缺陷、性能隐患与协议退化**的终局演进路线。

---

## 1. 架构现状与核心工程原则

### 1.1 Phase 1 成果小结 (已完成)
Phase 1 成功构建了跨语言基础底座并下沉了底层密集型子系统：
- **基础设施**：UniFFISharp 聚合导出与 MSBuild 增量代码生成 (`pixeval_native`)
- **核心模块**：DSL 过滤匹配 (`pixeval_filters`)、MMF 内存映射缓存 (`pixeval_cache`)、SQLite 基础存储 (`pixeval_storage`)、配置原子迁移 (`pixeval_config`)、Tokio 下载调度与路径宏 (`pixeval_download`)、TLS SNI 分片传输 (`pixeval_maho`)、Pixiv API SDK (`pixeval_mako`)、增量订阅同步机 (`pixeval_subscription`)、MCP 协议服务器 (`pixeval_mcp`)、动态库插件宿主 (`pixeval_plugin`)。

### 1.2 剩余距离与存量缺陷清单 (Gap Analysis & Review Findings)
通过对当前代码库的静态复核，不仅 C# 表现层依然残留大量重度计算与业务管道，而且第一阶段在跨语言桥接过程中引入了若干**高危功能失效 (H1~H11)** 与行为不一致缺陷。所有待解决问题完整映射如下：

```
                 【未下沉核心业务领域】                         【全量复核确证缺陷 (Review Findings)】
┌───────────────────────────────────────────────┐ ┌────────────────────────────────────────────────────────┐
│ 1. 媒体后处理/转码 : 动图拆合帧、格式转换      │ │ H1: 订阅下载 JSON 命名不匹配致反序列化空指针与静默丢弃 │
│ 2. 小说解析与排版   : 标记语言 AST、HTML/MD/EPUB│ │ H2: 多页插画/小说 DownloadState 编码错位致从不入队   │
│ 3. 图像解码与预览   : Progressive 边下边解     │ │ H3: MCP 服务退化 (49->19，6个工具无实现，分页丢失)   │
│ 4. 业务仓储与持久化 : 26个管理类、JSON序列化   │ │ H4: 原生插件宿主在生产环境中零调用未激活             │
│ 5. 多图站/以图搜图 : Imouto (Booru), SauceNao │ │ H5: 订阅宏丢失系列信息，is_r18 丢失 R18G 语义        │
│ 6. 导航配置/网格算法: Navigation YAML, 二维装箱 │ │ H6: 小说正文结构化数据不可达 (正文插图与前后篇丢失)   │
│ 7. 版本更新与代理   : GitHub Releases, SemVer │ │ H7: 关键 API 端点被改错 (系列上下文/评论端点)       │
└───────────────────────────────────────────────┘ │ H8: 用户卡片横幅背景恒为空 (未填充代表作品)          │
                                                  │ H9: 文件缓存每次重启无条件 truncate 被清空           │
                                                  │ H10: 会话鉴权 401 失效未通知 UI 导致假死在登录态     │
                                                  │ H11: PixevalSettings.MyId==0 与 MyUser! 缺少空值防护   │
                                                  │ 2.1~2.6: 并发缩容、Maho传输裸奔、静态多IP覆盖等缺陷    │
                                                  └────────────────────────────────────────────────────────┘
```

### 1.3 终极目标：“Pure Presentation UI + Full Rust Core”
- **C# / Avalonia 角色**：蜕变为纯粹的**声明式 UI 渲染器**。只保留 XAML、Views、自定义外观控件、以及轻量的 `ObservableProperty` 响应式状态桥接。
- **Rust Core 角色**：承担 **100% 领域计算、文本解析、媒体编解码、网络协议、业务仓储状态机与文件 I/O**。
- **实施准则**：**新功能下沉与存量缺陷修复深度捆绑**。属于阶段二领域的问题，必须在对应小阶段落地时一并修复并补齐测试；无法归入阶段二的问题，单独设立专项节点严格收敛。

### 1.4 架构铁律：UniFFISharp `partial` 原生类型扩展与“零包装原则” (Zero-Wrapper Rule)

> [!CAUTION]
> **执行智能体与开发者强制遵循：严禁手写任何形式的包装类型（Wrapper / Adapter / Shim / Proxy / DTO）！**
> UniFFISharp 代码生成器导出的所有领域模型（Record）和引擎句柄（Class）均为 `public partial`。表现层若需扩充功能，**必须且只能**通过在 `src/Pixeval/NativeExtensions/<SubNamespace>/` 下使用 `public partial record/class` 原地扩展，业务层 100% 直连消费原生类型！

#### 1.4.1 为什么要 Partial 化？
在早期开发中，部分模块手写了冗余胶水层（例如 `CacheTable` 包装 `CacheEngine`、`CoreDownloadManager` 包装 `DownloadManager`、手写中间 DTO 与模型映射），导致：
1. **多重内存分配与跨语言对象损耗**：同一份数据在 Rust 实体、FFI 生成实体、C# 包装实体之间反复复制，失去零拷贝优势。
2. **代码膨胀与状态不同步**：两套模型字段不同步、事件穿透断裂，带来严重的隐蔽 Bug。
3. **维护地狱**：每次 Rust 修改结构体，都需要同步修改 C# DTO 和手写转换器。

自 UniFFISharp 0.2.7 / 0.2.9 起，生成的 C# 类型天然带有 `partial` 关键字。整个项目已经通过重构（如提交 `600f6b42`、`d09cb60b`）彻底消除了过渡包装层，建立了统一的 **NativeExtensions 原生扩展模式**。后续迁移必须严格继承该模式。

#### 1.4.2 Partial 化的标准工程范式
所有新下沉的 Rust 领域实体和核心引擎，在 C# 表现层的对接**统一遵循以下四条铁律**：

1. **命名空间严格对齐**：
   在 `src/Pixeval/NativeExtensions/<SubNamespace>/` 下创建扩展文件，命名空间严格与 UniFFI 生成的代码保持一致（例如 `namespace Pixeval.Native.Novel;`、`namespace Pixeval.Native.Media;`、`namespace Pixeval.Native.Storage;`）。
2. **声明 Partial 类型并就地实现接口**：
   若领域实体需要对齐 Avalonia / Misaki 表现层接口（如 `IArtworkInfo`、`ISerializable`、`ISingleImage`、`IImageSize`），或者需要追加只读计算属性（如 `DateTimeOffset` 解析、标签投影），**直接在 partial 类型中就地实现**：
   ```csharp
   // 规范示例：src/Pixeval/NativeExtensions/Novel/NovelArticle.cs
   namespace Pixeval.Native.Novel;

   // 语法严律：直接 partial 声明 UniFFI 生成的 Record 或 Class
   public partial record NovelArticle : IArtworkInfo, ISerializable
   {
       // 仅追加接口实现所需的投影属性，不持有重复字段！
       [JsonIgnore]
       public long RawId => Id;

       string IIdentityInfo.Id => Id.ToString();
       string IPlatformInfo.Platform => IPlatformInfo.Pixiv;

       // 严禁在此类中嵌套 private NovelArticle _inner 字段！
       // 严禁为此类编写多重包装构造函数！
   }
   ```
3. **零胶水 DTO，消除类型互转**：
   - 严禁手写 `ToEntity()`、`FromNative()`、`ToDto()`、`MapTo()` 等映射算法。
   - 严禁命名 `Core*`、`*Wrapper`、`*Adapter`、`*Model` 的双轨制并行类型。
4. **表现层全面直连原始强类型**：
   - Views、ViewModels、Services 必须直接以 `Pixeval.Native.*` 中的原始强类型作为数据源与参数。
   - 集合直接绑定 `ObservableCollection<Illustration>` 或通过 `IncrementalLoadingCollection` 流式驱动原生实体。

---

## 2. 深度迁移演进路线图

```mermaid
flowchart TD
    subgraph Phase2 ["Phase 2: 内容计算与媒体管线原生化 (高收益/零 UI 耦合)"]
        P2_1["2.1 小说解析与排版引擎 (pixeval_novel) [已完成]<br/>【附带修复】H6(正文插图/前后篇), H7(小说端点), 2.4(ratio谓词误判), 高级搜索参数丢失<br/>【Partial规范】NovelArticle/NovelContent 在 NativeExtensions/Novel 原地实现接口"]
        P2_2["2.2 媒体后处理与动图转码 (pixeval_media) [已完成]<br/>【附带修复】H2(任务组入队), H5(系列宏与R18G), 2.1(订阅下载探错与孤儿行), 2.2(下载并发缩容/多IP/取消感知)<br/>【Partial规范】DownloadManager 原地扩展，严禁手写任务包装类"]
        P2_3["2.3 零拷贝图片抓取与流式预览管线<br/>【附带修复】H9(缓存重启清空与持久索引), 2.3(Mako全面接驳Maho抗审查/超时), 2.4(缓存实时限额/内存对齐)<br/>【Partial规范】维持 CacheEngine 在 NativeExtensions/Cache 原地扩展"]
    end

    subgraph Phase3 ["Phase 3: 业务仓储闭环与辅助服务下沉 (去胶水/去依赖)"]
        P3_1["3.1 领域仓储与状态机闭环 [已完成]<br/>【吸收修复】H1(JSON大小写与老数据水合兼容), 2.5(存储枚举错位/稳定ID/唯一索引)<br/>【Partial规范】仓储实体在 NativeExtensions/Storage 原地扩展，清退 26 个 Manager"]
        P3_2["3.2 多图站聚合与 SauceNao 搜图 (清退 Imouto.BooruParser)<br/>【Partial规范】Booru/SauceNao 模型在 NativeExtensions 原地实现 IArtworkInfo"]
        P3_3["3.3 导航 YAML 诊断与主页网格算法 (清退 SharpYaml) [已完成]"]
        P3_4["3.4 原生应用更新与 GitHub 代理引擎 (pixeval_update)"]
        P3_5["3.5 [专项] MCP 协议服务器全量恢复与插件宿主激活<br/>【吸收修复】H3(补齐49工具与游标分页), H4(生产激活插件宿主), 2.6(信号量释放)"]
        P3_6["3.6 [专项] 网络韧性与会话安全防护<br/>【吸收修复】H7(通用API端点), H8(用户横幅), H10(鉴权失效同步), H11(MyId防护), 2.3(429限流串行化)"]
    end

    subgraph Phase4 ["Phase 4: 表现层极致瘦身与跨平台交付 (Thin UI 终局)"]
        P4_1["4.1 ViewModel 彻底去业务化 (纯 Reactive Binding)"]
        P4_2["4.2 统一跨平台 CI/CD (Windows / Linux / macOS)"]
    end

    Phase2 --> Phase3
    Phase3 --> Phase4
```

---

## 3. 分阶段实施规划

### 阶段 2：内容计算与媒体管线原生化 (Phase 2)

#### 2.1 Pixiv 小说标记语法解析与多格式排版引擎 (`crates/pixeval_novel`) (已完成)
- **功能目标**：
  - 新建 `crates/pixeval_novel`，基于 Rust 高性能零拷贝 Tokenizer 实现解析。
  - 支持完整的 Pixiv 标记语法：`[newpage]`、`[[rb:汉字 > 注音]]`、`[jumpuri:文本 > 链接]`、`[jump:页面]`、`[chapter:章节名]`、`[uploadimage:id]`、`[pixivimage:id-page]`。
  - 原生提供多目标输出能力：分页 AST / 富文本流（供 Avalonia UI 分页渲染）、标准 Markdown 格式化、语义化 HTML 生成、EPUB 电子书一键打包。
- **Partial 化工程规范**：
  - Rust 导出的 `NovelArticle`、`NovelChapter`、`NovelContent` 等强类型对象，统一在 `src/Pixeval/NativeExtensions/Novel/` 下声明 `public partial record` 补齐 `IArtworkInfo`、`ISerializable` 接口与只读投影属性。
  - **严禁新建 `NovelWrapper` 或 C# 侧中间 DTO！ViewModels 直接消费原生 Record！**
- **附带修复审查缺陷**：
  - **【修复 H6】小说正文结构化数据与前后篇导航失效**：
    - 在 `pixeval_mako` 中补充抓取 Pixiv Web 端点（`/webview/v2/novel`），原生解析内嵌 JSON 结构体 `NovelContent`。
    - 完整提取 `SeriesNavigation`（`prev_novel` / `next_novel`）与内嵌插图列表，修复前后篇导航失效与正文插图永久空白。
  - **【修复 H7(小说部分)】小说评论回复与小说相关作品端点**：
    - 在 `pixeval_mako` 中补齐小说评论回复端点 `/v2/novel/comment/replies`，并在 C# `MakoHelper` 转发时按作品类型正确分派。
    - 恢复小说相关作品端点 `/v1/novel/related`，替代当前硬编码的空集合。
  - **【修复 2.4(DSL过滤)】`ratio:` 谓词对小说作品误排除**：
    - 修复 `crates/pixeval_filters/src/eval.rs`：当作品无尺寸信息（`height == 0`）时，遵循“比例过滤忽略小说”的标准规范返回 `true`，防止小说在 `WorkContainer` 中被静默排除。
  - **【修复 2.3(高级搜索)】高级搜索参数丢弃问题**：
    - 在原生客户端中补全作品高级过滤参数透传：`content_type`、`ratio`、`width/height` 区间、`tool`、`start_date`、`end_date` 等，恢复小说 `content_length` 筛选与 `/v1/search/options` 动态选项获取。
- **验收标准**：
  - C# 物理删除 `PixivNovelParser.cs`、`PixivNovelHtmlParser.cs`、`PixivNovelMdParser.cs`、`PixivNovelMdDisplayParser.cs`。
  - 小说正文插图与前后篇导航正常显示；单元测试覆盖 AST 解析与结构化模型反序列化。

#### 2.2 媒体后处理与动图转码管道 (`crates/pixeval_media` 或扩展 `pixeval_download`) (已完成)
- **功能目标**：
  - 构建原生媒体转码与打包管线：
    1. **Ugoira 动图合成**：原生流式解压 zip 帧包，结合帧延迟数组，直接调用原生编解码库生成高质量 GIF、APNG、WebP 或 MP4 视频。
    2. **漫画归档打包**：原生整合多页作品为 CBZ / ZIP 归档，无需落地零散临时文件。
    3. **格式转码**：基于 `image` crate 原生支持 JPEG、PNG、WebP、AVIF 无损/有损转码。
- **Partial 化工程规范**：
  - 下载任务管理直接在 `src/Pixeval/NativeExtensions/Download/DownloadManager.cs` 中 partial 声明，使用已建立的 `ProgressCallbackAdapter` 桥接 UI 事件。
  - 媒体转码引擎接口直接在 `src/Pixeval/NativeExtensions/Media/` 下 partial 扩展，严禁编写双轨制的 `DownloadTaskAdapter`！
- **附带修复审查缺陷**：
  - **【修复 H2】多页插画与小说的订阅下载任务组从不入队**：
    - 统一 UniFFI `DownloadState` 枚举编码（1起），在 `SubscriptionDownloadHistoryEntry` 构造时显式初始化为 `Queued = 1`。
    - 修复 C# `DownloadManager.QueueTask` 中子任务 `DownloadState` 为 0 时被跳过的问题，打通多页插画与小说订阅下载的全链路。
  - **【修复 H5】订阅路径宏系列丢失与 `is_r18` 语义恢复**：
    - 补齐订阅路径 `MacroContext`：在 `crates/pixeval_subscription/src/engine.rs` 中完整赋值 `has_series`、`series_id`、`series_title`，杜绝生成 `0/` 目录。
    - 恢复 `is_r18` 涵盖 R18G 的逻辑：在 `crates/pixeval_download/src/metapath/eval.rs` 与 C# 侧将判定对齐为 `is_r18 || is_r18g`。
    - 统一两路径的评级判定基准，确保同一作品在订阅与交互下载时落入相同目录。
  - **【修复 2.1(订阅下载)】探错路径与孤儿历史行**：
    - 本地已下载判定由模板探测改为针对真实目标文件（如 `novel.txt`），多页作品对齐真实文件名探测。
    - 对齐 Rust 与 C# 订阅历史写入路径格式，消除三串身份匹配差异产生的重复孤儿行。
    - 完善 `CancelAndWaitAsync`：使任务取消能够真正等待 Tokio 工作协程安全停止，避免关闭时出现并发析构。
    - 补齐 Series（type 2）订阅的元数据定时刷新；将订阅同步错误正确记录到日志与状态。
  - **【修复 2.2(下载引擎)】网络与并发缺陷**：
    - 修复 reqwest 静态多 IP 配置逻辑，确保同一域名的多个 IP 全部注册生效，而非仅最后一个生效。
    - 修复并发缩容时新 `Semaphore` 未能替换已派发任务引用的问题。
    - 完善取消感知，使任务暂停/取消能直接中止正在进行的 HTTP 响应体读取。
    - 桥接并保留字节级进度（`downloaded_bytes` / `total_bytes`），修复历史恢复出的空任务组在出错时无法恢复的问题。
    - 统一客户端与请求级的 User-Agent / Referer 头。
- **验收标准**：
  - 下载任务在 Rust 内部完成“下载 -> 解压 -> 帧合成/打包 -> 原子重命名”闭环。
  - C# `UgoiraDownloadTaskGroup`、`MangaDownloadTaskGroup` 瘦身为纯展示进度的轻量句柄。

#### 2.3 零拷贝图片流式抓取与渐进式预览管线 (`pixeval_cache` / `pixeval_maho`) (已完成)
- **功能目标**：
  - 将图片网络抓取直接交由 Rust 原生网络栈（Tokio/Maho），命中直接返回 MMF 内存切片；未命中后台抓取并原子写入 MMF 缓存。
  - 在 Rust 端实现流式首帧与渐进式预览嗅探，清退 C# 端的二值嗅探代码。
- **Partial 化工程规范**：
  - 维持当前在 `src/Pixeval/NativeExtensions/Cache/CacheEngine.cs` 中的 `public partial class CacheEngine` 模式，严禁复活 `CacheTable` 包装类！
- **附带修复审查缺陷**：
  - **【修复 H9】文件缓存每次重启后被清空**：
    - 移除 `crates/pixeval_cache/src/mmap_chunk.rs` 中打开文件时的无条件 `.truncate(true)`。
    - 为 `CacheEngine` 实现持久化的轻量元数据索引（或在启动时扫描既有 chunk 文件头自动恢复内存 entries 与 LRU 队列），使缓存真正跨进程生命周期持久存活。
  - **【修复 2.3(网络抗审查)】Rust API 客户端全面接驳 Maho 传输**：
    - 激活 `crates/pixeval_mako` 中被标为 `dead_code` 的 `maho_config`，将 API 请求全面接入 TLS ClientHello 分片（`pixeval_maho`）与动态 DNS 解析，杜绝 API 请求携带真实 SNI 裸奔。
    - 为 `reqwest::Client` 配置合理的连接超时（Connect Timeout）与请求超时（Request Timeout），防止挂起的网络调用无限期阻塞。
    - 修复流式抓取引擎中空中间页导致分页流提前意外终止的问题。
  - **【修复 2.4(缓存容量)】运行时动态限额与内存对齐**：
    - 在 `CacheEngine::put` 中加入运行时容量限制检查，对超过单项上限的请求提供拦截语义。
    - 修复重复 `put` 更大数据槽位时导致的旧槽碎片泄漏问题；按 8 字节对齐纠正 `total_used_bytes` 用量统计；改进 `purge_compact` 策略。
- **验收标准**：
  - 物理删除 C# `IOHelper.Download.cs` 中的 `DownloadMemoryStreamCoreAsync` 系列方法。
  - 重启客户端后已缓存图片无需重新通过网络下载。

---

### 阶段 3：业务仓储闭环与辅助服务下沉 (Phase 3)

#### 3.1 领域仓储与状态机闭环 (清退 C# 26 个 Database 类) (已完成)
- **功能目标**：
  - 在 `pixeval_storage` 内部封装高阶业务仓储（Repository Pattern）：`HistoryRepository`、`WatchLaterRepository`、`DownloadRepository`。
  - 实体在 Rust 端以强类型结构（`WorkMetadata`）直接读写 SQLite，无需在 C# 端中转 JSON。
  - 通过 UniFFI 导出高阶仓储接口与变更通知回调（`IStorageObserver`）。
- **Partial 化工程规范**：
  - 历史记录和仓储实体直接在 `src/Pixeval/NativeExtensions/Storage/` 下声明 `public partial record`，消除目前在 C# 维护的 26 个 `*PersistentManager` 包装类！
- **吸收修复审查缺陷**：
  - **【修复 H1】订阅触发的下载 JSON 命名风格不匹配与历史水合兼容性**：
    - Rust 实体序列化为 payload 时统一对齐驼峰命名，或在 C# 反序列化选项中统一启用 `PropertyNameCaseInsensitive = true`。
    - 针对本地数据库中老版本遗留的 snake_case 既有数据增加兼容水合逻辑，杜绝反序列化静默空值与老用户数据丢失。
  - **【修复 2.5(存储缺陷)】枚举错位、删除插与分页退化**：
    - 在数据库升级迁移中平移修复旧版 `DownloadHistory.State` 编码错位（0起与1起对齐）。
    - 将 `upsert_login_user` 改回原位更新已有行，保持 `HistoryEntryId` 稳定，防止 `login_context.yaml` 失效导致用户被动注销。
    - 恢复载荷外键索引的唯一约束（`UNIQUE`），并对损坏/无载荷的孤儿行进行自愈清理。
    - 优化分页策略，针对高并发场景避免 OFFSET 分页在增删时出现的跳页问题。
- **验收标准**：
  - 彻底清退 C# `Models/Database/Managers/` 中的所有 PersistentManager 及 `HistoryPersistHelper` 繁杂逻辑。

#### 3.2 多图站聚合与 SauceNao 搜图引擎 (`crates/pixeval_booru` / `crates/pixeval_saucenao`)
- **功能目标**：
  - 新建 `crates/pixeval_saucenao`：原生承揽 SauceNao 搜图请求、错误重试与结果类型映射。
  - 新建 `crates/pixeval_booru`：统一主流 Booru 图站（Danbooru/Gelbooru/Yandere/Sankaku/Rule34）的 API 抓取与模型解析。
- **Partial 化工程规范**：
  - Booru 与 SauceNao 原生模型在 `src/Pixeval/NativeExtensions/Booru/` 和 `SauceNao/` 下 partial 实现 `IArtworkInfo` / `IWorkEntry`，严禁再写 C# 适配层！
- **验收标准**：
  - 彻底移除 `src/lib/Imouto` C# 项目及相关引用。
  - C# 搜图页面直接调用原生 `SauceNaoClient.search(file_bytes)`。

#### 3.3 导航 YAML 解析诊断与主页网格算法 [已完成]
- **功能目标**：
  - 将导航 YAML 解析、Schema 严格校验、行列光标诊断与格式化下沉至 `pixeval_config`。
  - 将主页卡片网格算法（2D Bin-Packing、碰撞检测、重叠修正）下沉至 Rust。
- **验收标准**：
  - 从 `Pixeval.csproj` 物理移除 `SharpYaml` NuGet 依赖。

#### 3.4 原生应用更新与 GitHub 代理引擎 (`crates/pixeval_update`) (已完成)
- **功能目标**：
  - 新建 `crates/pixeval_update`：集成 `semver` 规范化版本判定，复用 Maho 代理通道抓取 GitHub Releases，原生断点续传下载并校验 SHA256。
- **验收标准**：
  - C# 仅暴露轻量 UI 进度通知，版本检测与资产下载全部由 Rust 驱动。

#### 3.5 [专项] MCP 协议服务器全量恢复与插件宿主激活 (`crates/pixeval_mcp` / `crates/pixeval_plugin`)
- **Partial 化工程规范**：
  - 直接消费原生 `McpServer` 与 `PluginHostEngine`，通过 partial 或静态方法扩展，严禁在 C# 建立二次代理类！
- **吸收修复审查缺陷**：
  - **【修复 H3】补齐缺失工具、已公布未实现工具与游标分页**：
    - 补齐已公布但未实现的 6 个核心工具（`search_illustrations`、`recommended_works`、`rankings`、`work_detail`、`add_subscription`、`queue_download`），恢复通过 Mako/Download 访问 Pixiv 的能力。
    - 将工具集由 19 个全面恢复至原有的完整工具集（38 读 + 11 写，包含评论、收藏、关注、小说正文、下载控制等）。
    - 恢复 `PixevalMcpCursorStore` 游标分页契约与 `more` 工具，防止大数据集列表直接溢出。
    - 补齐 `/mcp` 服务端的 `text/event-stream` SSE 流与会话管理支持，回显客户端请求协议版本。
    - 将测试中断言的工具数量阈值恢复为真实值（不再降低阈值固化退化）。
  - **【修复 H4】原生动态库插件宿主在生产中真实激活**：
    - 在 `ExtensionService` 的插件载入主干路径中真正调用 `engine.RegisterMetadata` 与 `engine.LoadPlugin`，使原生插件生命周期管理真正生效，MCP `extensions` 工具可正确返回已装插件。
    - 限制插件扫描目录深度，增加环路检测，防止符号链接死循环。
  - **【修复 2.6(MCP服务)】**：在 `PixevalMcpService.DisposeAsync` 中补全信号量释放，防止资源悬挂。
- **验收标准**：
  - MCP 工具在 AI 客户端中能完整执行搜索、推荐、榜单、下载等 Pixiv 操作并支持分页流转。

#### 3.6 [专项] 网络韧性、会话安全与体验防护 (已完成)
- **吸收修复审查缺陷**：
  - **【修复 H7(通用API)】校正改错的 API 端点**：
    - 校正系列上下文端点为 `/v1/illust-series/illust` 并对齐嵌套响应结构。
    - 校正系列关注区分 `manga|novel` 路径段。
    - 升级插画评论列表至 `/v3/illust/comments`，回复端点至 `/v2/illust/comment/replies`。
  - **【修复 H8】用户卡片横幅背景恢复**：
    - 在 `UserItemViewModel` 或 Rust 对应模型中恢复代表作品缩略图提取逻辑，消除用户卡片横幅的空白渲染。
  - **【修复 H10】会话中途鉴权失效同步与状态回退**：
    - 在 `MakoClient` 中引入 Token 刷新失败与 401 Unauthorized 回调通知，触发 C# 侧 `ClearToken()` 并及时将 UI 切换回未登录态。
  - **【修复 H11】未登录态与空值防护**：
    - 全面审计并防护 `PixevalSettings.MyId == 0` 与 `MyUser!` 导致向后端传入非法 `user_id=0` 或产生空引用异常的路径。
  - **【修复 2.3(限流)】429 处理与请求串行化**：
    - 恢复对 HTTP 429 与 `Retry-After` 头的解析和响应式限流通知；恢复在限流器内持锁跨越请求发送以串行化高频并发请求。
  - **【修复 2.4(DSL边界)】闰日日期过滤器诊断与作者匹配**：
    - 为 `s:2-29` 等非闰年日期匹配提供显式诊断；优化作者匹配逻辑与 Unicode 空白符处理。
  - **【清理项 3】残留目录与死代码物理清理**：
    - 物理移除 `src/Pixeval.Caching/`、`src/Pixeval.Filters/`、`src/Pixeval.Download/` 残留的 `bin/obj` 文件夹。
    - 从 `Pixeval.csproj` 彻底移除死代码 `LegacyAppSettingsMigration.cs`，清理残留 submodule 引用。
- **验收标准**：
  - 所有 Pixiv API 端点在抓包与单元测试中均与官方协议一致；Token 失效后 UI 能够优雅下线。

---

### 阶段 4：表现层极致瘦身与跨平台交付 (Phase 4)

#### 4.1 ViewModel 彻底去业务化 (纯 Reactive Binding) (已完成)
- **功能目标**：
  - 终结表现层实体过度包装与逻辑碎片化，将所有 ViewModel 彻底蜕变为纯粹的**声明式响应式绑定器**。
  - 集合直接以原生强类型驱动：`ListBox.ItemsSource` 直连 `ObservableCollection<Illustration>`、`ObservableCollection<Novel>`、`ObservableCollection<User>`，彻底废除二次包装层。
  - 将下载聚合统计、限流倒计时、树形状态维护下沉至 Rust Tokio 运行时；将小说排版与以图搜图格式编解码收敛至原生底层。
  - 拔除 ViewModel 内部实例化的所有 UI 控件（`Page`、`Border`、`SubView` 等），100% 回归 XAML 声明式模板与路由。
- **Partial 化工程规范与零包装铁律**：
  - **原生 Record 原地投影**：在 `src/Pixeval/NativeExtensions/Mako/` 中直接扩展 `Illustration`、`Novel`、`User`、`Series`，原地提供 `AspectRatio`、`PageCount`、`SizeText`、`Tooltip` 等只读计算属性，严禁手写任何 `*ItemViewModel` 包装类！
  - **交互命令视图层级化 (View-Scoped Commands)**：收藏、稍后再看、下载、复制等操作统一提升至 `WorkViewViewModel` 或通过 Attached Behaviors 承接，参数直传原生强类型实体，不再为每个列表项单独分配 Command 实例。
  - **单一事实来源 (Single Source of Truth)**：通过 UniFFI 导出的 `IStorageObserver` 集中监听收藏、稍后再看与历史记录变更，UI 控件通过响应式事件总线原地响应，杜绝局部状态与本地数据库脱节。
- **五大核心重构范围与去业务化映射**：
  1. **作品与实体列表流 (4.1.1)**：
     - 物理删除 `IllustrationItemViewModel`、`NovelItemViewModel`、`UserItemViewModel`、`SeriesItemViewModel`、`SpotlightItemViewModel`、`ThumbnailEntryViewModel`、`WorkEntryViewModel`。
     - 改造 `WorkView.axaml`，DataTemplate 直接绑定 `Pixeval.Native.Mako.Illustration` 与 `Novel`。
     - 改造 `IncrementalSource` 与 `SharableViewDataProvider`，移除创建包装 ViewModel 的工厂委托。
  2. **下载中心与订阅任务树 (4.1.2)**：
     - 在 `pixeval_download` / `pixeval_subscription` 导出 `SubscriptionFolderSnapshot` 与任务树聚合流。
     - 物理删除 `DownloadFolderViewModel` 中的 `_rateLimitTimer`、LINQ 遍历状态汇总与 Sum/Average 求和。
     - 物理删除 `DownloadPageViewModel` 中的双重字典映射（`_lookup`、`_subscriptionFolderLookup`）与手动插入排序，直接绑定原生快照列表。
  3. **查看器体系纯声明式重构 (4.1.3)**：
     - 物理删除 `IllustrationViewerPageViewModel.CreatePanePages` 与 `NovelViewerPageViewModel.SettingsPage` 中的 UI 控件实例化代码，改用 XAML `ContentControl` / `DataTemplate` 声明式呈现。
     - 小说分页组装收敛至 `NovelContent.RenderMarkdownPages()` 底层扩展，移除 ViewModel 内手写 `NovelImageRenderDto` / `NovelIllustRenderDto` 的转换代码。
     - 移除 `_autoPlayTimer`，自动播放移至 View 层 Behavior 驱动。
  4. **搜索、主页与以图搜图轻量化 (4.1.4)**：
     - 移除 `SauceNaoSearchPageViewModel` 的 Skia/Avalonia 内存流与 PNG 编码逻辑，原始字节直传 `SauceNaoClient.Search`。
     - 搜索与主页选项全面直连 `pixeval_config` 与 `pixeval_mako` 原生模型，移除手写集合拼接。
  5. **应用巨石治理与全局状态收敛 (4.1.5)**：
     - 精简 `AppViewModel` (784行 -> ~220行)，移除 C# 脏批次锁、手动防抖字典与持久化同步状态机，全面委托 Rust `StorageEngine` 内部事务。
- **物理清退清单 (Physical Elimination Checklist)**：
  - **完全物理删除**：
    - `src/Pixeval/ViewModels/Illustration/IllustrationItemViewModel.cs` (.Commands.cs)
    - `src/Pixeval/ViewModels/Novel/NovelItemViewModel.cs` (.Commands.cs)
    - `src/Pixeval/ViewModels/User/UserItemViewModel.cs` (.Commands.cs)
    - `src/Pixeval/ViewModels/Series/SeriesItemViewModel.cs`
    - `src/Pixeval/ViewModels/Entry/SpotlightItemViewModel.cs`
    - `src/Pixeval/ViewModels/Work/WorkEntryViewModel.cs` (.Commands.cs, .Debounce.cs)
    - `src/Pixeval/ViewModels/Work/ThumbnailEntryViewModel.cs`
    - `src/Pixeval/ViewModels/Download/IDownloadListEntryViewModel.cs`
  - **极致瘦身**：
    - `DownloadPageViewModel.cs` (373 行 -> ~70 行)
    - `DownloadFolderViewModel.cs` (209 行 -> ~50 行)
    - `IllustrationViewerPageViewModel.cs` (515 行 -> ~160 行)
    - `NovelViewerPageViewModel.cs` (386 行 -> ~140 行)
    - `AppViewModel.cs` (784 行 -> ~220 行)
- **验收标准**：
  - 表现层 100% 消除 `*ItemViewModel` 实体包装类，XAML DataTemplate 均以 `Pixeval.Native.*` 原生实体为 DataContext。
  - 列表滚动流无任何包装堆分配，长列表 GC 停顿时间显著下降；下载中心与作品查看器无任何 UI 线程卡顿。
  - 全工程编译通过，警告数为 0，现有自动化测试全绿通过。

#### 4.2 统一跨平台 CI/CD 流水线
- **目标**：
  - 配置 GitHub Actions 原生矩阵编译：Windows (`x86_64-pc-windows-msvc`), Linux (`x86_64-unknown-linux-gnu`), macOS (`aarch64-apple-darwin`, `x86_64-apple-darwin`)，实现全平台 Native 动态库与 Avalonia 前端的一键交叉编译与分发。

---

## 4. 下一步行动建议 (Recommended Next Steps)

建议按照**“高解耦、高收益、零 UI 阻抗、顺带修复核心阻塞”**的原则分步启动：

1. **第一优先级：小说解析与排版引擎 (`crates/pixeval_novel`) [已完成]**
   - **理由**：输入为纯文本小说字符串，输出为结构化 AST 与 Markdown/HTML/EPUB，**完全没有 UI 依赖，测试边界极其清晰**。
   - **顺带修复**：小说结构化正文插图与前后篇导航失效（**H6**）、小说评论回复与相关作品端点（**H7**）、DSL 比例过滤对小说的误排除（**2.4**）、高级搜索参数透传（**2.3**）。
   - **严守铁律**：`NovelArticle` / `NovelChapter` / `NovelContent` 必须在 `src/Pixeval/NativeExtensions/Novel/` 中直接通过 `public partial record` 补齐接口，严禁创建包装类！
2. **第二优先级：动图后处理与媒体管线 (`crates/pixeval_media`) [已完成]**
   - **理由**：彻底解决 Ugoira 动图合成与格式转换的跨语言性能损耗，关闭下载模块的最后一段 C# 尾巴。
   - **顺带修复**：多页插画与小说任务组未入队阻塞（**H2**）、订阅路径宏丢失系列与 R18G 语义（**H5**）、下载引擎静态多 IP 覆盖与并发缩容缺陷（**2.2**）、订阅下载探错与孤儿行（**2.1**）。
3. **第三优先级：零拷贝图片抓取与缓存/预览管线 (`pixeval_cache` / `pixeval_maho`) [已完成]**
   - 彻底下沉 foyer 混合存储与 SIMD 平面通道 LZ4 零拷贝管线，Mako 全面接驳 Maho 抗审查传输。
4. **第四优先级：业务仓储闭环、MCP 全量恢复与网络韧性 (Phase 3 全量) [已完成]**
   - 彻底清退 C# 26 个 PersistentManager 与 Imouto/SharpYaml 外部依赖，原生激活 MCP 与动态库插件宿主。
5. **当前最高优先级：表现层极致瘦身——ViewModel 彻底去业务化 (Phase 4.1) [下一步启动]**
   - **理由**：底层能力已 100% 原生就绪，表现层依然残留着实体二次包装、手写字典排序、LINQ 轮询求和与 UI 控件倒置等反模式，严重制约长列表性能并带来状态分叉风险。
   - **核心攻坚**：全面清退所有 `*ItemViewModel` 实体包装类，XAML 直连 `Pixeval.Native.*`；将下载聚合与状态机收敛进 Tokio；拔除 ViewModel 内所有的 UI 控件实例化代码，彻底实现纯粹的 Reactive Binding。
