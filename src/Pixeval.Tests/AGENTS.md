# Pixeval 测试约定

本文路径均相对仓库根目录。

UI 行为测试使用 `Ui/UiTestSession.cs` 初始化正式 App，窗口承载 `App.CreateViewContainer()` 或组合驱动器的 `Shell`。网络和外部更新检查在依赖组合入口替换；每个用例隔离数据，应用级 UI 测试标记 `DoNotParallelize`。

`TabViewContainer` 已封闭继承，`UiArchitectureTests` 禁止测试程序集定义 `ViewContainerBase` 派生类。测试驱动器是普通对象，只组合产品外壳，不实现另一套导航、生命周期或 UI 模板。新增测试沿用这条入口，不绕过架构检查。

导航观察只记录结果。目标页必须保留实际选中、加载和存活状态；需要继续操作原页时，在场景断言之后显式关闭目标页并选择原页。不要在事件观察器、统一 Render/输入辅助方法或断言 getter 中自动关闭、返回或重建页面。

页面/窗口关闭契约通过正式 `ClosePage`、`CloseAllPages` 或 `Window.Close` 验证。单独的模型 Dispose 单测可以保留，但不得用测试手动 Dispose 模型来证明 UI 关闭正确。临时卸载/重新挂载与永久关闭应分别验证。

行为契约放在本测试项目，由 MSTest/CI 执行。`tools/Pixeval.MigrationValidation` 只负责导出和手动启动。检查按影响范围选择：单场景使用 FullyQualifiedName 筛选；外壳或公共驱动器变更覆盖 UI 分类及相邻集成测试。运行方式和证据边界见 `docs/m3e/VALIDATION_HOST.md`；不因截图成功宣称行为通过。
