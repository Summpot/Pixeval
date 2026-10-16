# 贡献指南

## 参与开发的要求

1. 对Avalonia的基本了解，要了解更多相关信息请看 [Avalonia文档](https://docs.avaloniaui.net/)（或拥有WPF/UWP/WinUI 3相关经验）
2. 对C#和.NET开发的一定了解以及开发经验
3. 具有不依赖文档阅读代码的能力

## 环境要求

* 拥有[Git](https://git-scm.com)环境
* 1. 安装[Visual Studio 2026](https://visualstudio.microsoft.com/vs)或更高，并在**工具-获取工具与功能**的**工作负载**中选择 **.NET 桌面开发**
  1. 或最新版[JetBrains Rider](https://www.jetbrains.com/rider/)，并安装[.NET10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)。

## 运行项目

```sh
git clone --recurse-submodules https://github.com/Pixeval/Pixeval.git
```

1. 用以上Git命令克隆本项目
2. 如果 *Pixeval.Desktop* 不是启动项目，请将
3. 其设置为启动项目
4. 构建并运行

## 项目结构

领域逻辑在 Rust crate 中，通过 `pixeval_native` 用 UniFFI 导出；C# 侧在 crate 对齐的命名空间里扩展生成的 `partial` 类型，不再另包一层 DTO。

### crates目录

本目录是 Rust workspace，由 `pixeval_native` 统一导出给 C#：

1. *pixeval_native* UniFFI 导出伞（cdylib + staticlib）
2. *pixeval_mako* Pixiv API
3. *pixeval_maho* TLS / SNI / domain-fronting 传输
4. *pixeval_filters* 过滤语句解析与补全
5. *pixeval_download* 下载编排与路径宏
6. *pixeval_cache* 文件缓存
7. *pixeval_storage* SQLite 存储
8. *pixeval_subscription* 订阅
9. *pixeval_config* 配置、导航菜单、首页卡片布局
10. *pixeval_booru* Booru 系列 API
11. *pixeval_saucenao* SauceNAO
12. *pixeval_media* 动图 / 漫画转码
13. *pixeval_novel* 小说解析与渲染
14. *pixeval_plugin* 插件
15. *pixeval_mcp* MCP 服务
16. *pixeval_update* 应用更新

### src目录

本目录是 Avalonia / .NET 呈现层与平台宿主：

1. *Pixeval* 共享应用：UI、ViewModel，以及 UniFFI 生成类型的 `partial` 扩展（命名空间与 crate 对齐，例如 `Pixeval.Mako`、`Pixeval.Filters`）
2. *Pixeval.Desktop* 桌面宿主，包括 Windows / macOS / Linux 打包
3. *Pixeval.Android* Android 宿主
4. *Pixeval.iOS* iOS 宿主
5. *Pixeval.Browser* 实验性 Browser WASM 宿主
6. *Pixeval.I18N* 多语言基础设施（字符串资源在 *Pixeval* 中）
7. *Pixeval.Utilities* 公用工具类库
8. *Pixeval.SourceGen* *Pixeval* 的源生成器
9. *Pixeval.Tests* 测试项目

### lib目录

本目录是控件库，为了方便随时修改以子模块或直接粘贴形式保存。**一定不会引用 src 目录的项目**

1. *FluentAvalonia* Fluent 主题
2. *AutoSettingsPage* / *AutoSettingsPage.Avalonia* / *SettingsControls.Avalonia* 自动生成设置页的控件库
3. *AnimatedControls.Avalonia* Avalonia 动图控件库
4. *SmoothScroll.Avalonia* 平滑滚动控件库

## 项目版本控制

本项目采用一个简单的Git分支模型：当您在进行开发的时候，请基于[`main`](https://github.com/Pixeval/Pixeval/tree/main)创建新的分支，新的分支格式应该遵循`{user}/{qualifier}/{desc}`，其中`{user}`是您的用户名。

| 代码内容 | qualifier | desc |
| - | - | - |
| 漏洞修复 | fix | 漏洞的简要叙述 |
| 新功能 | feature | 新特性的简要叙述 |
| 重构或者代码质量提升 | refactor | 重构部分的简要叙述 |

如果您的贡献包含不止一种上面提到的类型，则应当遵循和您的贡献最为相关的一项，并在commit信息中提及其他类型上的贡献

在开发完成后，请发布 [Pull Request](https://github.com/Pixeval/Pixeval/pulls) 请求合并到`main`分支

## 证书

GPL-3.0 License，详情请参阅 [LICENSE](../LICENSE)
