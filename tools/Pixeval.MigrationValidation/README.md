# UI 导出与隔离启动工具

行为测试已经迁入 `src/Pixeval.Tests/Ui`，统一由 MSTest 执行。不要在这里新增页面测试或复制产品导航、生命周期。执行入口与场景边界见 [UI 测试说明](../../docs/m3e/VALIDATION_HOST.md)。

本工具只保留 `inventory`（注册清单）、`capture`（页面截图）、`benchmark`（性能采样）与 `ui`（隔离数据的正式桌面启动）：

```powershell
dotnet build tools/Pixeval.MigrationValidation/Pixeval.MigrationValidation.csproj -c Release
dotnet tools/Pixeval.MigrationValidation/bin/Release/net10.0/Pixeval.MigrationValidation.dll inventory artifacts/m3e/inventory-new
```

输出目录必须尚不存在。初始化前隔离设置、账户、扩展、数据库、缓存和下载位置。`ui` 会打开原生窗口并保留正式更新检查，不能当成离线测试；禁止登录真实账号或复制个人配置。工具不进入发布包。
