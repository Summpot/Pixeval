using Pixeval.Mako;
using System.Collections.Generic;
using Pixeval.Pixiv;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.ViewModels.Home;
using Pixeval.Views.Home;

namespace Pixeval.Tests;

[TestClass]
public sealed class HomeCardParameterEditorViewModelTest
{
    [TestMethod]
    public void Value_InvalidBindingWrite_KeepsLastValidChoice()
    {
        IReadOnlyList<SymbolComboBoxItem> items =
        [
            new(SimpleWorkKind.Illustration, "Illustration", default),
            new(SimpleWorkKind.Novel, "Novel", default)
        ];
        var editor = new HomeCardChoiceParameterEditorViewModel(
            HomeCardParameterKinds.SimpleWorkKind,
            "Work type",
            items,
            SimpleWorkKind.Illustration);
        var changeCount = 0;
        editor.ValueChanged += (_, _) => changeCount++;

        editor.Value = null!;
        editor.Value = WorkKind.Novel;

        Assert.AreEqual(SimpleWorkKind.Illustration, editor.GetValue<SimpleWorkKind>());
        Assert.AreEqual(0, changeCount);

        editor.Value = SimpleWorkKind.Novel;

        Assert.AreEqual(SimpleWorkKind.Novel, editor.GetValue<SimpleWorkKind>());
        Assert.AreEqual(1, changeCount);
    }
}
