using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Utilities;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Pixeval.Tests;

[TestClass]
public static class TestAssemblyInitializer
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval"));
        if (Directory.Exists(projectPath))
            I18NManager.CandidatePaths.Add(projectPath);

        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = LanguageHelper.DefaultLanguage;
        I18NManager.Initialize();
    }
}
