// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using AutoSettingsPage.Avalonia;
using CommunityToolkit.Avalonia.Controls;
using Pixeval.Localization;
using Pixeval.Download;
using Pixeval.Config;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Common.IO;

namespace Pixeval.Views.Settings;

public partial class DownloadMacroSettingsExpander : SettingsExpander, IEntryControl<DownloadMacroSettingsEntry>
{
    private readonly IDisposable _languageSubscription;
    private readonly MacroEditorColorizer _colorizer = new();
    private DownloadMacroSettingsEntry? _entry;
    private bool _isSynchronizingText;

    private static readonly Illustration _SingleImage = DesignHelper.DownloadParserSampleSingleIllustration;
    private static readonly Illustration _SingleAnimatedImage = DesignHelper.DownloadParserSampleAnimatedIllustration;
    private static readonly Illustration _ImageSet = DesignHelper.DownloadParserSampleImageSetIllustration;
    private static readonly Mako.Novel _Novel = DesignHelper.DownloadParserSampleNovel;

    public DownloadMacroSettingsEntry Entry
    {
        set
        {
            DataContext = value;
            _entry = value;
            SyncTextFromEntry(value.Value);
            ApplyText(value.Value);
        }
    }

    public DownloadMacroSettingsExpander()
    {
        InitializeComponent();
        _languageSubscription = AppLocalization.Instance.Subscribe(() => RefreshDiagnostic());
        DownloadPathMacroTextBox.TextArea.TextView.LineTransformers.Add(_colorizer);
    }

    private void DownloadPathMacroTextBox_OnTextChanged(object? sender, EventArgs e)
    {
        if (_entry is null || _isSynchronizingText)
            return;

        ApplyText(DownloadPathMacroTextBox.Text?.ReplaceLineEndings("")?.Trim());
    }

    private void RefreshDiagnostic()
    {
        var text = DownloadPathMacroTextBox.Text ?? "";
        var analysis = DownloadPathMacroParser.Analyze(text);
        if (string.IsNullOrWhiteSpace(text))
            DownloadMacroInvalidInfoBar.Text = Strings.SettingsMainViewDownloadMacroInvalidInfoBarInputCannotBeBlank();
        else if (analysis.Diagnostics.Count > 0)
            DownloadMacroInvalidInfoBar.Text = FormatDiagnostic(analysis.Diagnostics[0]);
    }

    private void ApplyText(string? text)
    {
        var normalized = text ?? "";
        var analysis = DownloadPathMacroParser.Analyze(normalized);
        _colorizer.Update(analysis, normalized.Length);
        DownloadPathMacroTextBox.TextArea.TextView.InvalidateVisual();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            DownloadMacroInvalidInfoBar.Text = Strings.SettingsMainViewDownloadMacroInvalidInfoBarInputCannotBeBlank();
            DownloadMacroInvalidInfoBar.IsVisible = true;
            return;
        }

        if (analysis.Diagnostics.Count > 0)
        {
            DownloadMacroInvalidInfoBar.Text = FormatDiagnostic(analysis.Diagnostics[0]);
            DownloadMacroInvalidInfoBar.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            DownloadMacroInvalidInfoBar.Text = Strings.SettingsMainViewDownloadMacroInvalidInfoBarInputCannotBeBlank();
            DownloadMacroInvalidInfoBar.IsVisible = true;
            return;
        }

        try
        {
            SetExamplePaths(normalized);
            DownloadMacroInvalidInfoBar.IsVisible = false;
            _entry?.Value = normalized;
        }
        catch (Exception exception)
        {
            DownloadMacroInvalidInfoBar.Text = Strings.SettingsMainViewDownloadMacroInvalidInfoBarMacroInvalidFormatted(message: exception.Message);
            DownloadMacroInvalidInfoBar.IsVisible = true;
        }
    }

    private void SyncTextFromEntry(string text)
    {
        if (string.Equals(DownloadPathMacroTextBox.Text, text, StringComparison.Ordinal))
            return;

        _isSynchronizingText = true;
        try
        {
            DownloadPathMacroTextBox.Text = text;
        }
        finally
        {
            _isSynchronizingText = false;
        }
    }

    private void SetExamplePaths(string text)
    {
        SinglePathBlock.Text = GetPath(_SingleImage);
        AnimatedPathBlock.Text = GetPath(_SingleAnimatedImage);
        SetPathBlock.Text = IoHelper.ReplaceTokenSetIndex(GetPath(_ImageSet), _ImageSet.SetIndex);
        NovelPathBlock.Text = GetPath(_Novel);
        return;

        string GetPath(object info) =>
            IoHelper.ChangeExtension(
                IoHelper.NormalizePath(DownloadPathMacroParser.Reduce(text, new ParserContext(info))),
                ".png");
    }

    private static string FormatDiagnostic(MacroDiagnostic diagnostic)
    {
        var message = diagnostic.Kind switch
        {
            MacroDiagnosticKind.UnexpectedToken => Strings.MacroParserUnexpectedToken(token: diagnostic.Arguments[0]),
            MacroDiagnosticKind.ExpectedLeftBraceAfterAt => Strings.MacroParserExpectedLeftBraceAfterAt(),
            MacroDiagnosticKind.ExpectedMacroName => Strings.MacroParserExpectedMacroName(),
            MacroDiagnosticKind.MissingRightBrace => Strings.MacroParserMissingRightBrace(),
            MacroDiagnosticKind.MissingConditionalSeparator => Strings.MacroParserMissingConditionalSeparator(),
            MacroDiagnosticKind.UnknownMacroName => Strings.MacroParserUnknownMacroName(macroName: diagnostic.Arguments[0]),
            MacroDiagnosticKind.NonParameterizedMacroBearingParameter => Strings.MacroParserNonParameterizedMacroBearingParameter(macroName: diagnostic.Arguments[0]),
            MacroDiagnosticKind.ConditionalBranchesMissing => Strings.MacroParserConditionalBranchesMissing(macroName: diagnostic.Arguments[0]),
            MacroDiagnosticKind.InvalidFormatter => Strings.MacroParserInvalidFormatter(macroName: diagnostic.Arguments[0], formatter: diagnostic.Arguments[1]),
            MacroDiagnosticKind.MacroShouldBeInLastSegment => Strings.MacroParserMacroShouldBeInLastSegment(macroName: diagnostic.Arguments[0]),
            MacroDiagnosticKind.MacroContextRestrictionNotSatisfied => Strings.MacroParserMacroContextRestrictionNotSatisfied(macroName: diagnostic.Arguments[0]),
            _ => diagnostic.Kind.ToString()
        };
        return Strings.MacroParserMessageWithPositionFormatted(message: message, position: diagnostic.Span.Start + 1);
    }


}
