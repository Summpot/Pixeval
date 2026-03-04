# Pixeval AI Agent Guidelines

This document provides instructions and project-specific conventions for AI Agents (such as Cascade or related assistants) working on the Pixeval project.

## 🏗️ Architecture & Tech Stack
- **Framework:** .NET 10 
- **Language:** C# (Preview/13) with strict `<Nullable>enable</Nullable>`
- **UI Framework:** Avalonia UI (Cross-platform)
- **MVVM Pattern:** `CommunityToolkit.Mvvm` (Heavily relying on source generators like `[ObservableProperty]` and `[RelayCommand]`)
- **Structure:**
  - `src/Pixeval`: Core application logic and shared UI (Views/ViewModels/Models).
  - `src/Pixeval.Desktop` / `src/Pixeval.Android` / `src/Pixeval.Browser` / `src/Pixeval.iOS`: Platform-specific entry points.
  - `src/Pixeval.Utilities`, `src/Pixeval.Caching`, `src/Pixeval.Download`, `src/Pixeval.Filters`: Domain-specific class libraries.

---

## ✅ Modification & Verification Process (CRITICAL)

When you are asked to modify the project, you **MUST** follow this verification process before concluding your task. Do not assume your code works without validation.

### 1. Build Verification (First Line of Defense)
Pixeval uses strict compilation settings (`AvaloniaUseCompiledBindingsByDefault` is true, Nullable enabled). You **must** verify that your code compiles successfully.
- **Action:** Run the build command for the Desktop project (or the specific library you modified).
  ```bash
  dotnet build src/Pixeval.Desktop/Pixeval.Desktop.csproj
  ```
- **Requirement:** Resolve any compiler errors, warnings (especially nullable reference type warnings), or XAML compiled binding errors introduced by your changes.

### 2. XAML & MVVM Validation
- If you modify `.axaml` files, ensure you are using `<UserControl x:DataType="vm:YourViewModel">` properly. Since compiled bindings are enabled by default, incorrect bindings will fail the build.
- Do not use traditional `INotifyPropertyChanged` boilerplates. Use the `CommunityToolkit.Mvvm` source generators (e.g., `partial class` + `[ObservableProperty]`).

### 3. Automated Testing Context
- Currently, the main Pixeval app does not maintain a comprehensive automated unit/UI test suite for the UI layer directly.
- **Action:** If modifying logic in external libraries (like `Imouto.BooruParser`), check if tests exist (e.g., `lib/Imouto/Imouto.BooruParser.Tests`). Run them using `dotnet test <test-project-path>`.
- For the main application, logic verification relies on code review, architecture adherence, and successful build compilation.

### 4. Internationalization (i18n)
- If your change introduces new UI texts, you must check the `i18n/Language.tt` and related localization files.
- The `Language.cs` file is auto-generated using T4 templates (`TextTemplatingFileGenerator`). Never manually edit `Language.cs` directly.

### 5. Git & Branching
When instructed to create a branch or commit:
- Branch names **must** follow: `{user}/{qualifier}/{desc}` (Qualifiers: `fix`, `feature`, `refactor`).
- Write clear and descriptive commit messages summarizing the technical details of the change.

---

**Remember:** Your priority is to produce compile-safe, Avalonia-compatible, and idiomatic C# code. Never skip the `dotnet build` verification step after a code modification.
