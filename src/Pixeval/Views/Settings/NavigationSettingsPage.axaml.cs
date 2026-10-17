// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Utilities;

namespace Pixeval.Views.Settings;

public sealed partial class NavigationSettingsPage : ContentPage
{
    private readonly IDisposable _languageSubscription;
    private readonly NavigationYamlEditorColorizer _colorizer = new();
    private bool _isSynchronizingText;

    public NavigationSettingsPage(NavigationMenuDocument document)
    {
        InitializeComponent();
        DataContext = this;
        YamlEditor.TextArea.TextView.LineTransformers.Add(_colorizer);
        SetEditorText(document.Text);
        ValidateCurrentText();
        _languageSubscription = AppLocalization.Instance.Subscribe(() => ValidateCurrentText());
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (CanApplyButton.IsEffectivelyEnabled
            && KeyboardShortcut.MatchesPlatformCommand(e, Key.S)
            && TryApplyChanges())
            e.Handled = true;
    }

    private void ApplyButton_OnClicked(object? sender, RoutedEventArgs e) => _ = TryApplyChanges();

    private bool TryApplyChanges()
    {
        var result = ValidateCurrentText();
        if (result.Configuration is null)
            return false;

        this.GetRequiredUiService<NavigationMenuDocument>().Text = YamlEditor.Text ?? "";
        AppInfo.SaveNavigationMenuYaml(this.GetRequiredUiService<NavigationMenuDocument>().Text);
        this.Notifications.ShowSuccess(
            Strings.NavigationSettingsPageApplied());

        this.AppNavigation?.ReloadNavigation();

        return true;
    }

    private void FormatButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        var result = ValidateCurrentText();
        if (result.Configuration is not { } configuration)
            return;

        SetEditorText(NavigationYamlFormatter.Format(configuration));
        ValidateCurrentText();
    }

    private void ResetButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        SetEditorText(NavigationMenuYaml.DefaultYaml);
        ValidateCurrentText();
    }

    private void InsertFolderButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        InsertYaml(
            $"""

               - folder: "$NavigationSettingsPage.DefaultFolderTitle"
                 icon: DocumentFolder
                 children:
                   - page: Home

             """);
    }

    private void YamlEditor_OnTextChanged(object? sender, EventArgs e)
    {
        if (!_isSynchronizingText)
            ValidateCurrentText();
    }

    private NavigationParseResult ValidateCurrentText()
    {
        var result = NavigationYamlParser.Parse(YamlEditor.Text);

        _colorizer.Update(result.Diagnostics, YamlEditor.Text?.Length ?? 0);
        YamlEditor.TextArea.TextView.InvalidateVisual();
        CanApplyButton.IsEnabled = result.Configuration is not null;
        StatusInfoBar.Text = GetStatusText(result);
        StatusInfoBar.IsVisible = !result.IsValid;
        return result;
    }

    private void SetEditorText(string text)
    {
        if (string.Equals(YamlEditor.Text, text, StringComparison.Ordinal))
            return;

        _isSynchronizingText = true;
        try
        {
            YamlEditor.Text = text;
        }
        finally
        {
            _isSynchronizingText = false;
        }
    }

    private void InsertYaml(string snippet)
    {
        var textArea = YamlEditor.TextArea;
        var caret = textArea.Caret.Offset;
        YamlEditor.Document.Insert(caret, snippet);
        textArea.Caret.Offset = caret + snippet.Length;
        ValidateCurrentText();
    }

    private static string? GetStatusText(NavigationParseResult result)
    {
        if (result.IsValid)
            return null;

        var text = Strings.NavigationSettingsPageStatusInvalidFormatted(count: result.Diagnostics.Count);

        var builder = new StringBuilder(text);
        foreach (var diagnostic in result.Diagnostics)
            _ = builder.AppendLine().Append(FormatDiagnostic(diagnostic));

        return builder.ToString();
    }

    private static string FormatDiagnostic(NavigationDiagnostic diagnostic)
    {
        var position = string.IsNullOrEmpty(diagnostic.PositionText)
            ? ""
            : $" {diagnostic.PositionText}";
        return $"{position}  {diagnostic.Message}".TrimStart();
    }
}
