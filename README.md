# Pixeval

Pixeval 是一个跨平台的 Pixiv 第三方客户端，包含 Rust 核心、Avalonia 桌面端以及 Compose Multiplatform 移动端/桌面调试宿主。

## 架构组成

- **Rust Core (`crates/`)**：网络请求、认证、缓存、图片与动图编解码、存储、订阅与过滤等核心业务能力。
- **Avalonia Desktop (`src/`)**：基于 .NET 10 / Avalonia 构建的正式桌面端。
- **Compose Multiplatform (`compose/`)**：基于 Kotlin Multiplatform 与 Compose Multiplatform 构建的移动端（Android / iOS）及桌面调试宿主（`:desktop`）。

## 构建与运行

### Compose 端构建（Gradle）

Gradle 已支持在仓库根目录直接运行（Gradle Wrapper 位于根目录）：

```powershell
# 运行 Compose 桌面宿主
./gradlew.bat :desktop:run

# 构建 Android Debug APK
./gradlew.bat :androidApp:assembleDebug

# 运行单元测试
./gradlew.bat :nativeBindings:jvmTest :sharedApp:jvmTest
```

### 桌面端构建（.NET）

```powershell
dotnet build src/Pixeval.Desktop/Pixeval.Desktop.csproj
```

### Rust 核心检查

```powershell
cargo check --workspace
```
