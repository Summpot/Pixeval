using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.I18N;
using Pixeval.Models.Database;
using Pixeval.ViewModels;
using Pixeval.Views.Login;

namespace Pixeval.Tests.Ui;

internal static class LoginLayoutValidation
{
    internal static async Task RunAsync(string directory)
    {
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 1000, 360 })
            {
                var page = new LoginPage();
                var model = (LoginPageViewModel) page.DataContext!;
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = navigation };
                try
                {
                    window.Show();
                    await model.LoadUsersAsync();
                    await Task.Delay(400); // Preserve the real navigation transition before inspecting its final layout.
                    model.Users.Clear();
                    model.RefreshToken = "";
                    await RenderAsync(window);
                    var input = page.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
                    var tokenButton = page.GetVisualDescendants().OfType<Button>().Single(control =>
                        Equals(control.Content, I18NManager.GetResource(LoginPageResources.RefreshTokenButton.Content)));
                    var webButton = page.GetVisualDescendants().OfType<Button>().Single(control =>
                        Equals(control.Content, I18NManager.GetResource(LoginPageResources.WebViewButton.Content)));
                    InWindow(window, input);
                    InWindow(window, tokenButton);
                    InWindow(window, webButton);
                    foreach (var label in new[] { tokenButton, webButton }.SelectMany(button => button.GetVisualDescendants().OfType<TextBlock>()))
                        Require(label.TextLayout.Width <= label.Bounds.Width + 1 && label.TextLayout.Height <= label.Bounds.Height + 1
                            && label.TextLayout.TextLines.All(line => !line.HasCollapsed), "Localized login actions remain fully readable");
                    Save(window, directory, $"login-empty-{theme.Key}-{width}");

                    var first = new LoginUserEntry { UserId = 990001, Name = "保存账号 中文 with a deliberately long display name", Account = "a_long_saved_account_name", RefreshToken = "fixture-saved-first" };
                    var second = new LoginUserEntry { UserId = 990002, Name = "", Account = "", RefreshToken = "fixture-saved-second" };
                    model.Users.Add(first);
                    model.Users.Add(second);
                    var editor = input.GetVisualDescendants().OfType<TextBox>().Single();
                    var inputPeer = ControlAutomationPeer.CreatePeerForElement(input);
                    var editorPeer = ControlAutomationPeer.CreatePeerForElement(editor);
                    List<string> exposedChanges = [];
                    HashSet<AutomationPeer> observedPeers = [];
                    Observe(inputPeer);
                    Observe(editorPeer);
                    void Observe(AutomationPeer peer)
                    {
                        if (!observedPeers.Add(peer)) return;
                        peer.PropertyChanged += (_, change) =>
                        {
                            if (change.OldValue is string oldValue) exposedChanges.Add(oldValue);
                            if (change.NewValue is string newValue) exposedChanges.Add(newValue);
                        };
                    }
                    Require(editor.Focus(), "Saved account input can receive keyboard focus");
                    var expandProvider = inputPeer.GetProvider<IExpandCollapseProvider>()!;
                    expandProvider.Expand();
                    await RenderAsync(window);
                    Require(expandProvider.ExpandCollapseState == ExpandCollapseState.Expanded, "Accessible expansion retains account selection");
                    var accountPopup = input.GetVisualDescendants().OfType<Popup>().Single();
                    Require(accountPopup.Child!.GetVisualDescendants().OfType<ListBoxItem>().Count() == 2,
                        "Both saved account containers are included in the automation audit");
                    Require(accountPopup.Child!.GetVisualDescendants().OfType<ListBoxItem>().Any(item =>
                        ControlAutomationPeer.CreatePeerForElement(item).GetName() == second.UserId.ToString()),
                        "An unnamed saved account uses its numeric ID as its accessible label");
                    AssertProtectedAutomation();
                    Press(window, Key.Down, PhysicalKey.ArrowDown);
                    await RenderAsync(window);
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await RenderAsync(window);
                    Require(model.SelectedUser is not null && model.RefreshToken == model.SelectedUser.RefreshToken,
                        "Keyboard account selection restores exactly the selected account token");
                    Require(!model.IsLoginInProgress, "Choosing an account does not implicitly authenticate");
                    expandProvider.Collapse();
                    Require(expandProvider.ExpandCollapseState == ExpandCollapseState.Collapsed, "Accessible collapse retains account selection");
                    Require(editor.PasswordChar != '\0' && !editor.RevealPassword, "Refresh tokens remain masked in the editor");
                    AssertProtectedAutomation();
                    Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(input)), "Account/token input has a localized accessible name");
                    model.SelectedUser = first;
                    await RenderAsync(window);
                    Require(editor.Text == first.RefreshToken && ReferenceEquals(input.SelectedItem, first), "Model account restoration also restores the real editor and its selection");
                    AssertProtectedAutomation();
                    InWindow(window, input);
                    Save(window, directory, $"login-selected-{theme.Key}-{width}");

                    Require(editor.Focus(), "Manual token input can receive keyboard focus");
                    editor.SelectAll();
                    window.KeyTextInput("fixture-manual-token");
                    await RenderAsync(window);
                    Require(model.RefreshToken == "fixture-manual-token" && model.SelectedUser is null,
                        $"Manual editing clears stale account selection without altering saved accounts (editor={editor.Text}, input={input.Text}, selected={model.SelectedUser?.UserId})");
                    Require(model.Users.Count == 2 && first.RefreshToken == "fixture-saved-first", "Manual token entry does not mutate saved credentials");
                    AssertProtectedAutomation();
                    inputPeer.GetProvider<IValueProvider>()!.SetValue("fixture-accessible-token");
                    await RenderAsync(window);
                    Require(model.RefreshToken == "fixture-accessible-token", "Accessible token editing retains the actual input binding");
                    AssertProtectedAutomation();
                    editor.SelectAll();
                    window.KeyTextInput(second.RefreshToken);
                    await RenderAsync(window);
                    Require(ReferenceEquals(model.SelectedUser, second), "Typing an exact saved token restores its account preview");
                    AssertProtectedAutomation();
                    editor.SelectAll();
                    Press(window, Key.Back, PhysicalKey.Backspace);
                    await RenderAsync(window);
                    Require(model.RefreshToken == "" && model.SelectedUser is null, "Clearing the token clears only the selected preview");
                    window.KeyTextInput("fixture-manual-token");
                    await RenderAsync(window);
                    Press(window, Key.Escape, PhysicalKey.Escape);
                    model.IsLoginInProgress = true;
                    await RenderAsync(window);
                    var progress = page.GetVisualDescendants().OfType<ProgressBar>().Single();
                    Require(progress.IsEffectivelyVisible && progress.IsIndeterminate && !input.IsEffectivelyEnabled && !tokenButton.IsEffectivelyEnabled && !webButton.IsEffectivelyEnabled,
                        "The busy state has visible progress and gates all login entry points");
                    Save(window, directory, $"login-busy-{theme.Key}-{width}");
                    model.IsLoginInProgress = false;
                    await RenderAsync(window);
                    Require(!progress.IsEffectivelyVisible && !progress.IsIndeterminate, "Leaving busy state hides and stops progress");

                    window.Height = 240;
                    await RenderAsync(window);
                    Require(webButton.Focus(NavigationMethod.Tab), "Web login stays keyboard reachable in a short window");
                    webButton.BringIntoView();
                    await Task.Delay(500); // Let the production smooth-scroll animation reach its requested offset.
                    await RenderAsync(window);
                    Save(window, directory, $"login-short-{theme.Key}-{width}");
                    InWindow(window, webButton);
                    window.Height = 720;
                    window.Width = width == 360 ? 1000 : 360;
                    await RenderAsync(window);
                    Require(ReferenceEquals(input, page.GetVisualDescendants().OfType<AutoCompleteBox>().Single()) && model.RefreshToken == "fixture-manual-token",
                        "Resizing preserves the editor instance and draft token");
                    InWindow(window, input);
                    AssertProtectedAutomation();
                    report.Add($"PASS {theme.Key}-{width}: empty/selected/long account, keyboard selection, masked input/automation values and events, safe account labels, accessible editing/expansion, busy state, short-window scroll and resize preservation");

                    void AssertProtectedAutomation()
                    {
                        var exposed = new List<string>(exposedChanges);
                        var popupControls = accountPopup.Child!.GetVisualDescendants().OfType<Control>();
                        foreach (var control in window.GetVisualDescendants().OfType<Control>().Concat(popupControls))
                        {
                            var peer = ControlAutomationPeer.CreatePeerForElement(control);
                            Observe(peer);
                            exposed.Add(peer.GetName() ?? "");
                            if (peer.GetProvider<IValueProvider>() is { } value)
                                exposed.Add(value.Value ?? "");
                        }
                        Require(!exposed.Any(value => value.Contains("fixture-", StringComparison.Ordinal)),
                            "Saved accounts, preview and token value/change automation must not expose credential text");
                        var maskedToken = new string('•', model.RefreshToken?.Length ?? 0);
                        Require((inputPeer.GetProvider<IValueProvider>()!.Value ?? "") == maskedToken
                            && (editorPeer.GetProvider<IValueProvider>()!.Value ?? "") == maskedToken,
                            "Both token automation values expose only password masks");
                    }
                }
                finally { window.Content = null; window.Close(); }
            }
        File.WriteAllLines(Path.Combine(directory, "login-layout-contracts.txt"), report);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(80);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, "");
        window.KeyRelease(key, RawInputModifiers.None, physical, "");
    }

    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Require(point is { } p && p.X >= -1 && p.Y >= -1 && p.X + control.Bounds.Width <= window.Bounds.Width + 1
            && p.Y + control.Bounds.Height <= window.Bounds.Height + 1, $"{control.GetType().Name} remains inside the {window.Width}x{window.Height} viewport ({point}, {control.Bounds.Size})");
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No login layout frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
