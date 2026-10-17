using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.Common;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Pixeval.Tests;

[TestClass]
public static class TestAssemblyInitializer
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = LanguageHelper.DefaultLanguage;
        AppLocalization.Instance.SetLanguage("zh-Hans");
    }
}
