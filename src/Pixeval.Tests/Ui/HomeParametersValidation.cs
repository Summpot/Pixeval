using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Services.Core;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.ViewModels.Home;
using Pixeval.Views.Works;
using Pixeval.Views.Home;

namespace Pixeval.Tests.Ui;

internal static class HomeParametersValidation
{
    internal static async Task RunAsync(string directory)
    {
        var app = App.AppViewModel;
        using var fixture = new OfflineSearchFixture(app.MakoClient);
        app.MakoClient.SetRefreshToken("fixture-home-parameters");
        await FixtureAuthentication.IdentifyAsync(app.MakoClient);
        app.HomePageCards.Clear();
        AppInfo.SaveHomePageCards(app.HomePageCards);
        var cardsPath = Path.Combine(AppInfo.SettingsFolder, "home_page_cards.yaml");
        var originalFile = File.ReadAllBytes(cardsPath);
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                var page = UiTestSession.Create<HomePage>();
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = width, Height = 800, RequestedThemeVariant = theme, Content = host.Shell };
                var key = $"{theme.Key}-{width}";
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    page.ViewModel.IsEditMode = true;
                    await RenderAsync(window);
                    // The real shell loads its own account display preferences first.
                    var initialRequests = fixture.RequestLog.Count;
                    var library = page.FindControl<ItemsControl>("CardLibrary")!;
                    var buttons = library.GetVisualDescendants().OfType<Button>().Where(button => button.DataContext is HomeCardDefinition).ToArray();
                    Require(buttons.Length == 19, "Every existing home source remains in the actual card library");
                    foreach (var definition in App.AppViewModel.AppServiceProvider.GetRequiredService<HomeCardDefinitions>().All)
                    {
                        key = $"{definition.SourceKind}-{theme.Key}-{width}";
                        var button = buttons.Single(button => ReferenceEquals(button.DataContext, definition));
                        Require(AutomationProperties.GetName(button) == definition.Title, "Source library actions expose their localized titles");
                        await ActivateAsync(window, button);
                        Require(ReferenceEquals(page.ViewModel.PendingTemplate, definition), "The library keyboard action selects exactly its source definition");
                        var editors = page.ViewModel.ActiveParameterEditors;
                        var actual = editors.Aggregate(HomeCardParameterKinds.None, (flags, editor) => flags | editor.Kind);
                        Require(actual == Parameters(definition.SourceKind), "The source retains its complete original parameter set: " + definition.SourceKind);
                        var controls = page.FindControl<ItemsControl>("SourceParameterEditors")!;
                        var values = new Dictionary<HomeCardParameterKinds, object>();
                        foreach (var editor in editors)
                        {
                            switch (editor)
                            {
                                case HomeCardChoiceParameterEditorViewModel choice:
                                    var field = controls.GetVisualDescendants().OfType<SymbolComboBox>().Single(control => ReferenceEquals(control.DataContext, choice));
                                    await RevealAsync(window, field);
                                    var combo = field.GetVisualDescendants().OfType<ComboBox>().Single();
                                    Require(AutomationProperties.GetName(combo) == choice.Header, "Choice parameters expose their purpose to the actual ComboBox");
                                    var target = combo.SelectedIndex + 1 < choice.Items.Count ? combo.SelectedIndex + 1 : 0;
                                    var expected = choice.Items[target].Value;
                                    Require(combo.Focus(NavigationMethod.Tab), "Source choices remain keyboard reachable");
                                    Press(window, target == 0 ? Key.Home : Key.Down, target == 0 ? PhysicalKey.Home : PhysicalKey.ArrowDown);
                                    await RenderAsync(window);
                                    Require(Equals(choice.Value, expected), "Keyboard selection updates the original enum value");
                                    values[choice.Kind] = expected;
                                    foreach (var text in combo.GetVisualDescendants().OfType<TextBlock>().Where(text => !string.IsNullOrWhiteSpace(text.Text)))
                                        Require(text.TextLayout.TextLines.All(line => !line.HasCollapsed) && text.TextLayout.Width <= text.Bounds.Width + 1,
                                            "A long selected parameter value remains readable");
                                    Fits(field, window);
                                    break;
                                case HomeCardTextParameterEditorViewModel textEditor:
                                    var textBox = controls.GetVisualDescendants().OfType<TextBox>().Single(control => ReferenceEquals(control.DataContext, textEditor));
                                    Require(AutomationProperties.GetName(textBox) == textEditor.Header, "Text parameters have localized purpose names");
                                    if (textEditor.Kind == HomeCardParameterKinds.UserId && definition.UseCurrentUserAsDefault)
                                        Require(textEditor.Text == "900001", "Existing current-account defaults remain populated");
                                    var id = textEditor.Kind is HomeCardParameterKinds.UserId or HomeCardParameterKinds.EntryId or HomeCardParameterKinds.SeriesId;
                                    if (id || textEditor.Kind == HomeCardParameterKinds.SearchText)
                                    {
                                        await EditTextAsync(window, textBox, id ? "-1" : "   ");
                                        Require(!page.ViewModel.TryCreateCardFromSourceParameters(out _), "Invalid IDs and blank searches cannot create a source draft");
                                        await ActivateAsync(window, page.FindControl<Button>("AddConfiguredCardButton")!);
                                        Require(app.HomePageCards.Count == 0 && fixture.RequestLog.Count == initialRequests,
                                            "The real Add action refuses invalid parameters without saving or starting a source");
                                        Require(host.Notifications.Any(notification => notification.Title == Strings.HomePageInvalidSourceParameterWarningTitle()),
                                            "Rejected parameters retain the existing localized warning");
                                    }
                                    var input = id ? " 991007 " : textEditor.Kind == HomeCardParameterKinds.Tag ? "  标签 / fixture tag  " : "  alpha beta 中文  ";
                                    await EditTextAsync(window, textBox, input);
                                    values[textEditor.Kind] = id ? 991007L : input.Trim();
                                    Fits(textBox, window);
                                    break;
                                case HomeCardRankingDateParameterEditorViewModel date:
                                    var picker = controls.GetVisualDescendants().OfType<CalendarDatePicker>().Single();
                                    var enabled = controls.GetVisualDescendants().OfType<CheckBox>().Single();
                                    Require(!date.UseSpecifiedDate && !picker.IsEnabled, "Ranking date starts in the existing automatic mode");
                                    await ActivateAsync(window, enabled);
                                    Require(date.UseSpecifiedDate && picker.IsEnabled && AutomationProperties.GetName(picker) == date.Header,
                                        "Enabling the ranking date retains a named usable editor");
                                    await RevealAsync(window, picker);
                                    var dateInput = picker.GetVisualDescendants().OfType<TextBox>().Single();
                                    var valid = new DateTime(2021, 4, 5);
                                    await EditTextAsync(window, dateInput, valid.ToString("d", CultureInfo.CurrentCulture));
                                    Require(date.SelectedDate.Date == valid, "Typed ranking dates retain their original value");
                                    await EditTextAsync(window, dateInput, WorkRankingPage.MaxDate.AddDays(10).ToString("d", CultureInfo.CurrentCulture));
                                    Require(picker.SelectedDate?.Date == valid && date.SelectedDate.Date == valid,
                                        "A future home ranking date cannot replace the last valid selection");
                                    Require(host.Notifications.Any(notification => notification.Title == Strings.WorkRankingPageInvalidDateTitle()),
                                        "Invalid ranking input uses the existing localized date warning");
                                    values[date.Kind] = valid;
                                    Fits(picker, window);
                                    break;
                            }
                        }
                        Require(page.ViewModel.TryCreateCardFromSourceParameters(out var draft) && draft is not null && draft.Value.SourceKind == definition.SourceKind,
                            "Valid parameter input produces the same source kind");
                        foreach (var (kind, expected) in values)
                        {
                            object? actualValue = kind switch
                            {
                                HomeCardParameterKinds.WorkKind => draft!.Value.WorkKind,
                                HomeCardParameterKinds.SimpleWorkKind => draft!.Value.SimpleWorkKind,
                                HomeCardParameterKinds.PrivacyRestrict => draft!.Value.PrivacyRestrict,
                                HomeCardParameterKinds.RankMode => draft!.Value.RankMode,
                                HomeCardParameterKinds.UserId => draft!.Value.UserId,
                                HomeCardParameterKinds.EntryId => draft!.Value.EntryId,
                                HomeCardParameterKinds.SeriesId => draft!.Value.SeriesId,
                                HomeCardParameterKinds.SearchText => draft!.Value.SearchText,
                                HomeCardParameterKinds.Tag => draft!.Value.Tag,
                                HomeCardParameterKinds.RankingDate => draft!.Value.GetRankingDate().LocalDateTime.Date,
                                _ => throw new InvalidOperationException("Unexpected parameter kind")
                            };
                            Require(Equals(expected, actualValue), "The configured draft preserves the exact typed parameter: " + kind);
                        }
                        Require(File.ReadAllBytes(cardsPath).SequenceEqual(originalFile) && app.HomePageCards.Count == 0
                            && fixture.RequestLog.Count == initialRequests, "Selecting and editing a source remains a draft until explicit valid Add");
                        Save(window, directory, "home-parameters-" + key);
                        report.Add($"PASS {key}: complete parameter set, names/keyboard, validation, exact draft mapping and no implicit persistence/request");
                    }
                    page.ViewModel.IsEditMode = false;
                    await RenderAsync(window);
                    Require(app.HomePageCards.Count == 0 && File.ReadAllBytes(cardsPath).SequenceEqual(originalFile),
                        "Leaving the editor does not implicitly add or save a configured source");
                }
                catch
                {
                    Save(window, directory, "home-parameters-failure-" + key);
                    throw;
                }
                finally
                {
                    page.Dispose();
                    host.Content = null;
                    window.Close();
                }
            }
        Require(fixture.UnexpectedRequests.IsEmpty, "Parameter editing stays inside the offline account fixture");
        var guarded = UiTestSession.Create<HomePageViewModel>();
        guarded.SelectTemplate(App.AppViewModel.AppServiceProvider.GetRequiredService<HomeCardDefinitions>().Get(HomePageCardSourceKind.WorkRanking));
        var guardedDate = guarded.ActiveParameterEditors.OfType<HomeCardRankingDateParameterEditorViewModel>().Single();
        guardedDate.UseSpecifiedDate = true;
        guardedDate.SelectedDate = WorkRankingPage.MaxDate.AddDays(10);
        Require(!guarded.TryCreateCardFromSourceParameters(out _), "The draft model also rejects future dates before any source is constructed");
        guardedDate.UseSpecifiedDate = false;
        Require(guarded.TryCreateCardFromSourceParameters(out var automatic) && automatic is { UseSpecifiedRankingDate: false },
            "Automatic ranking keeps the disabled custom date without applying it");
        File.WriteAllLines(Path.Combine(directory, "home-parameter-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "home-parameter-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} home-parameter groups.");
    }

    private static HomeCardParameterKinds Parameters(HomePageCardSourceKind kind) => kind switch
    {
        HomePageCardSourceKind.WorkRecommended or HomePageCardSourceKind.WorkNew => HomeCardParameterKinds.WorkKind,
        HomePageCardSourceKind.WorkBookmarks => HomeCardParameterKinds.UserId | HomeCardParameterKinds.SimpleWorkKind | HomeCardParameterKinds.PrivacyRestrict | HomeCardParameterKinds.Tag,
        HomePageCardSourceKind.WorkRanking => HomeCardParameterKinds.SimpleWorkKind | HomeCardParameterKinds.RankMode | HomeCardParameterKinds.RankingDate,
        HomePageCardSourceKind.WorkFollowing => HomeCardParameterKinds.SimpleWorkKind | HomeCardParameterKinds.PrivacyRestrict,
        HomePageCardSourceKind.WorkMyPixiv => HomeCardParameterKinds.SimpleWorkKind,
        HomePageCardSourceKind.WorkRelated => HomeCardParameterKinds.EntryId | HomeCardParameterKinds.SimpleWorkKind,
        HomePageCardSourceKind.WorkPosts => HomeCardParameterKinds.UserId | HomeCardParameterKinds.WorkKind,
        HomePageCardSourceKind.WorkSearch => HomeCardParameterKinds.SimpleWorkKind | HomeCardParameterKinds.SearchText,
        HomePageCardSourceKind.UserSearch => HomeCardParameterKinds.SearchText,
        HomePageCardSourceKind.UserFollowing => HomeCardParameterKinds.UserId | HomeCardParameterKinds.PrivacyRestrict,
        HomePageCardSourceKind.UserMyPixiv or HomePageCardSourceKind.SingleUser => HomeCardParameterKinds.UserId,
        HomePageCardSourceKind.SingleImage or HomePageCardSourceKind.SingleNovel => HomeCardParameterKinds.EntryId,
        HomePageCardSourceKind.SingleSeries => HomeCardParameterKinds.SeriesId | HomeCardParameterKinds.SimpleWorkKind,
        HomePageCardSourceKind.UserRecommended or HomePageCardSourceKind.UserFollower or HomePageCardSourceKind.Spotlight => HomeCardParameterKinds.None,
        _ => throw new InvalidOperationException("Unlisted home source")
    };

    private static async Task EditTextAsync(Window window, TextBox input, string value)
    {
        await RevealAsync(window, input);
        Require(input.Focus(NavigationMethod.Tab), "The parameter input is keyboard reachable");
        input.SelectAll();
        window.KeyTextInput(value);
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
    }

    private static async Task ActivateAsync(Window window, Control control)
    {
        await RevealAsync(window, control);
        Require(control.Focus(NavigationMethod.Tab), "The parameter action is keyboard reachable");
        Press(window, Key.Space, PhysicalKey.Space);
        await RenderAsync(window);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static void Fits(Control field, Window window)
    {
        var origin = field.TranslatePoint(default, window)!.Value;
        Require(origin.X >= -1 && origin.X + field.Bounds.Width <= window.ClientSize.Width + 1, "The source field stays inside the narrow drawer");
    }

    private static async Task RevealAsync(Window window, Control control)
    {
        control.BringIntoView();
        await RenderAsync(window);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(150);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No home parameter frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
