using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.Common;

namespace Pixeval.Tests;

[TestClass]
public sealed class NavigationYamlParserTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = LanguageHelper.DefaultLanguage;
        AppLocalization.Instance.SetLanguage("zh-Hans");
    }

    [TestMethod]
    public void ParserShouldSupportNestedFoldersUpToMaxDepth()
    {
        var result = NavigationYamlParser.Parse(
            """
            newTab: Search

            header:
              - folder: Level1
                icon: Folder
                children:
                  - folder: Level2
                    icon: FolderOpen
                    children:
                      - page: Search

            footer:
              - page: Settings
            """);

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Configuration);
        var level1 = Assert.IsInstanceOfType<NavigationFolderItem>(result.Configuration.HeaderItems[0]);
        var level2 = Assert.IsInstanceOfType<NavigationFolderItem>(level1.Children[0]);
        var page = Assert.IsInstanceOfType<NavigationPageItem>(level2.Children[0]);
        Assert.AreEqual("Search", page.PageKey);
    }

    [TestMethod]
    public void FormatterShouldEmitValidCanonicalYaml()
    {
        var result = NavigationYamlParser.Parse(
            """
            newTab: Search

            header:
              - folder: Level1
                icon: Folder
                children:
                  - folder: Level2
                    icon: FolderOpen
                    children:
                      - page: Search

            footer:
              - page: Settings
            """);
        Assert.IsNotNull(result.Configuration);

        var yaml = NavigationYamlFormatter.Format(result.Configuration);
        var roundTrip = NavigationYamlParser.Parse(yaml);

        Assert.IsTrue(roundTrip.IsValid);
        Assert.Contains("- folder: Level1", yaml);
        Assert.Contains("- folder: Level2", yaml);
        Assert.Contains("- page: Search", yaml);
    }

    [TestMethod]
    public void ParserShouldRejectUnknownField()
    {
        var result = NavigationYamlParser.Parse(
            """
            newTab: Search

            header:
              - page: Search
                visible: false

            footer:
              - page: Settings
            """);

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Configuration);
        Assert.Contains(static diagnostic =>
            diagnostic.Message.Contains("visible", StringComparison.OrdinalIgnoreCase), result.Diagnostics);
    }

    [TestMethod]
    public void FormatterShouldPreserveI18NResourceReferences()
    {
        var result = NavigationYamlParser.Parse(
            """
            newTab: Search

            header:
              - page: Search
                title: $MainPage.Tab.Search
              - folder: $NavigationSettingsPage.WorkFolderTitle
                icon: Folder
                children:
                  - page: Search

            footer:
              - page: Settings
            """);
        Assert.IsNotNull(result.Configuration);

        var yaml = NavigationYamlFormatter.Format(result.Configuration);
        var roundTrip = NavigationYamlParser.Parse(yaml);

        Assert.IsTrue(roundTrip.IsValid);
        Assert.Contains("$MainPage.Tab.Search", yaml);
        Assert.Contains("$NavigationSettingsPage.WorkFolderTitle", yaml);
        Assert.IsFalse(yaml.Contains("搜索", StringComparison.Ordinal));
        Assert.IsFalse(yaml.Contains("作品", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DefaultYamlShouldUseI18NResourceReferences()
    {
        Assert.Contains("$NavigationSettingsPage.WorkFolderTitle", NavigationMenuYaml.DefaultYaml);
        Assert.Contains("$NavigationSettingsPage.UserFolderTitle", NavigationMenuYaml.DefaultYaml);
    }

    [TestMethod]
    public void ParserShouldRejectFoldersDeeperThanMaxDepth()
    {
        var result = NavigationYamlParser.Parse(
            """
            newTab: Search

            header:
              - folder: Level1
                children:
                  - folder: Level2
                    children:
                      - folder: Level3
                        children:
                          - page: Search

            footer:
              - page: Settings
            """);

        Assert.IsNull(result.Configuration);
        Assert.Contains(static diagnostic =>
            diagnostic.Message.Contains("超过最大层级", StringComparison.Ordinal), result.Diagnostics);
    }
}
