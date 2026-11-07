# M3E 验证宿主

使用 `global.json` 指定的 SDK，在仓库根目录构建：

```powershell
dotnet build tools/Pixeval.MigrationValidation/Pixeval.MigrationValidation.csproj -c Release
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll inventory artifacts/m3e/inventory-new
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll ui artifacts/m3e/ui-new
```

输出目录必须尚不存在，每次运行生成独立的 `data/`。两种模式都使用真实应用资源/设置注册，预先隔离账号、扩展、数据库、导航、缓存和下载目录；没有复制业务实现，没有读取当前用户配置。不得登录真实账号或将个人配置复制进去。退出后保留目录供诊断，不自动删除证据。

- `inventory`：通过 headless 平台导出实际导航注册、包含隐藏页的标题映射、主设置页的递归条目元数据及首页来源；不启动登录、窗口、MCP 或作品加载。不会读取设置值，因此不输出凭据。小说阅读设置在阅读器私有面板注册，维护/导航操作也不是主设置条目，需配合 `docs/m3e/ACCEPTANCE_CASES.md` 单独验收。
- `ui`：使用真实 `App` 生命周期和 TabViewContainer 外壳打开空账号应用。可选第三参数为自己构建并信任的原生扩展库，只复制到本次新数据目录。与桌面程序相同字体与渲染配置；不运行安装器/Store 导入。保留应用现有的 GitHub 更新检查和首次启动说明，因此此模式不是离线模拟器。用于无账号页面和原生输入验收，不等价于真实 OAuth、发布包或平台安装验收。

本工具不是第二套产品 UI，也不会进入发布包。需要已登录数据的用例应通过固定服务样本验证；不能为演示而取消生产登录门控。
