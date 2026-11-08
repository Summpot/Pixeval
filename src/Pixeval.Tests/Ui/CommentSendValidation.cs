using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Mako.Net.Responses;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using SkiaSharp;

namespace Pixeval.Tests.Ui;

internal static class CommentSendValidation
{
    private const long WorkId = 996001;
    private const long ParentId = 996002;
    private const string ImageUri = "https://i.pximg.net/m3e/comment-send.png";
    private const string Draft = "保留评论草稿 / Keep this draft 😀";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-comment-send");
        await client.ValidateTokenAsync();
        byte[] image;
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            image = buffer.ToArray();
        }
        fixture.ImageResponses[ImageUri] = image;
        foreach (var id in CommentImageHelper.EmojiIds)
            fixture.ImageResponses[string.Format(CommentImageHelper.EmojiDownloadUrl, id)] = image;
        using (var stickerBitmap = new SKBitmap(160, 80))
        using (var canvas = new SKCanvas(stickerBitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            using var encoded = SKImage.FromBitmap(stickerBitmap);
            using var png = encoded.Encode(SKEncodedImageFormat.Png, 100);
            foreach (var id in CommentImageHelper.StickerIds)
                fixture.ImageResponses[string.Format(CommentImageHelper.StickerDownloadUrl, id)] = png.ToArray();
        }
        var requests = Channel.CreateUnbounded<Pending>();
        List<Pending> pending = [];
        fixture.CommentResponseFactory = async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/add", StringComparison.Ordinal))
                return "{\"comments\":[],\"next_url\":null}";
            var operation = new Pending(request.RequestUri, await request.Content!.ReadAsStringAsync(token), token);
            pending.Add(operation);
            await requests.Writer.WriteAsync(operation);
            return await operation.Response.Task;
        };
        List<string> report = [];
        try
        {
            using (var closed = new CommentsViewViewModel(SimpleWorkType.Illustration, WorkId))
            {
                var page = new CommentContainer { DataContext = closed };
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = 360, Height = 600, Content = host.Shell };
                try
                {
                    window.Show();
                    await RenderAsync(window);
                    var bar = page.FindControl<PixivReplyBar>("ReplyBar")!;
                    var input = bar.FindControl<TextBox>("ReplyContentTextBox")!;
                    input.Text = "Window closing draft";
                    Require(bar.FindControl<Button>("SendButton")!.Focus(NavigationMethod.Tab), "The closing-window send action is reachable");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    var operation = await NextAsync();
                    window.Close();
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                    Require(operation.Token.IsCancellationRequested, "Closing the window itself cancels its comment request without first removing content");
                    operation.Response.SetResult(Response(996099, "Late window response"));
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                    Require(closed.Source.Count == 0 && input.Text == "Window closing draft", "A closed window rejects its late send result and preserves the draft");
                    report.Add("PASS window close: actual headless Window.Close cancels transport and rejects late completion");
                }
                finally
                {
                    host.Content = null;
                    window.Close();
                }
            }
            foreach (var type in new[] { SimpleWorkType.Illustration, SimpleWorkType.Novel })
                foreach (var reply in new[] { false, true })
                    foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                        foreach (var width in new[] { 360, 1000 })
                        {
                            using CommentsViewViewModel model = reply
                                ? new CommentItemViewModel(Comment(ParentId, "Parent comment"), type, WorkId, true)
                                : new CommentsViewViewModel(type, WorkId);
                            var page = new CommentContainer { DataContext = model };
                            var host = new PageTestHost { Content = page };
                            var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                            var key = $"{type}-{(reply ? "Reply" : "Comment")}-{theme.Key}-{width}";
                            try
                            {
                                window.Show();
                                await RenderAsync(window);
                                var bar = page.FindControl<PixivReplyBar>("ReplyBar")!;
                                var input = bar.FindControl<TextBox>("ReplyContentTextBox")!;
                                var send = bar.FindControl<Button>("SendButton")!;
                                var emoji = bar.FindControl<Button>("EmojiButton")!;
                                Require(AutomationProperties.GetName(input) == I18NManager.GetResource(PixivReplyBarResources.ReplyContentRichEditBox.PlaceholderText)
                                    && AutomationProperties.GetName(send) == I18NManager.GetResource(PixivReplyBarResources.SendButton.ToolTip)
                                    && AutomationProperties.GetName(emoji) == I18NManager.GetResource(PixivReplyBarResources.OpenEmoji),
                                    "The editor and icon actions expose localized purposes");
                                Type(new string('x', 140));
                                window.KeyTextInput("overflow");
                                Require(input.Text?.Length == 140, "The existing 140-character input limit remains in force");
                                Type(Draft);
                                Press(window, Key.Enter, PhysicalKey.Enter);
                                Require(!requests.Reader.TryPeek(out _) && input.Text == Draft, "Enter in the editor never submits a comment");
                                input.CaretIndex = 1;
                                await ActivateAsync(emoji);
                                var emojiTabs = (TabControl) ((Flyout) emoji.Flyout!).Content!;
                                var emojiChoice = emojiTabs.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is int);
                                var emojiText = ((PixivReplyEmoji) (int) emojiChoice.DataContext!).PlaceholderKey;
                                await ActivateAsync(emojiChoice);
                                Require(input.Text == Draft.Insert(1, emojiText) && !requests.Reader.TryPeek(out _), "Emoji selection inserts its original placeholder at the caret without sending");
                                emoji.Flyout!.Hide();
                                await RenderAsync(window);
                                Type(Draft);
                                await ActivateAsync(send);
                                var operation = await NextAsync();
                                CheckPayload(operation, "comment", Draft);
                                Require(bar.IsSubmitting && !send.IsEnabled && !emoji.IsEnabled && input.Text == Draft,
                                    "A pending send retains the draft and disables competing text/sticker actions");
                                send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(!requests.Reader.TryPeek(out _), "Even a direct repeated click cannot bypass the pending guard");
                                operation.Response.SetException(new HttpRequestException("Fixed comment send failure"));
                                await UntilAsync(() => !bar.IsSubmitting);
                                Require(model.Source.Count == 0 && input.Text == Draft && send.IsEnabled,
                                    "A failed response preserves the draft and allows retry");
                                Require(host.Notifications.Any(notification => notification.Title == I18NManager.GetResource(PixivReplyBarResources.SendFailed)),
                                    "Failure displays the localized error");
                                Save(window, directory, "comment-failed-" + key);
                                await ActivateAsync(send);
                                (await NextAsync()).Response.SetResult(Response(996101, Draft));
                                await UntilAsync(() => !bar.IsSubmitting);
                                Require(model.Source.Count == 1 && input.Text is "" or null, "Successful retry adds one comment and clears only its submitted draft");
                                Type("Original pending draft");
                                await ActivateAsync(send);
                                operation = await NextAsync();
                                Type("Newer unsent draft");
                                operation.Response.SetResult(Response(996102, "Original pending draft"));
                                await UntilAsync(() => !bar.IsSubmitting);
                                Require(model.Source.Count == 2 && input.Text == "Newer unsent draft", "Successful completion never clears a newer edit");

                                await ActivateAsync(emoji);
                                var tabs = (TabControl) ((Flyout) emoji.Flyout!).Content!;
                                var firstTab = tabs.Items.OfType<TabItem>().First();
                                Require(firstTab.Focus(NavigationMethod.Tab), "The emoji/sticker selector is keyboard reachable");
                                Press(window, Key.Right, PhysicalKey.ArrowRight);
                                await RenderAsync(window);
                                Require(tabs.SelectedIndex == 1, "Arrow navigation selects the sticker category");
                                foreach (var tab in tabs.Items.OfType<TabItem>())
                                {
                                    var label = tab.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == tab.Header?.ToString());
                                    Require(label.IsEffectivelyVisible && label.Bounds.Width >= 10 && label.Bounds.Height >= 10
                                        && label.TextLayout.Width <= label.Bounds.Width + 1 && label.TextLayout.TextLines.All(line => !line.HasCollapsed),
                                        "The picker category labels remain fully readable");
                                }
                                var sticker = tabs.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is int id && id == 301);
                                var picture = sticker.GetVisualDescendants().OfType<Image>().Single();
                                await UntilAsync(() => picture.Source is not null);
                                Require(picture.Stretch == Stretch.Uniform && picture.Source is not null, "Sticker selection preserves its source aspect ratio");
                                await RenderAsync(window);
                                VerifyStickerPixels(window, picture);
                                Save(window, directory, "comment-stickers-" + key);
                                await ActivateAsync(sticker);
                                operation = await NextAsync();
                                CheckPayload(operation, "stamp_id", "301");
                                Require(bar.IsSubmitting && input.Text == "Newer unsent draft", "Sticker submission shares the text pending guard and preserves the draft");
                                operation.Response.SetResult(Response(996103, "", true));
                                await UntilAsync(() => !bar.IsSubmitting);
                                Require(model.Source.Count == 3 && model.Source[0].IsStamp && input.Text == "Newer unsent draft",
                                    "A successful sticker appears once without clearing text");
                                Save(window, directory, "comment-success-" + key);

                                await ActivateAsync(send);
                                operation = await NextAsync();
                                host.Content = null;
                                await RenderAsync(window);
                                Require(operation.Token.IsCancellationRequested, "Removing the reply bar cancels the actual submission request");
                                operation.Response.SetResult(Response(996104, "Late comment"));
                                await Task.Delay(100);
                                Dispatcher.UIThread.RunJobs();
                                Require(model.Source.Count == 3 && input.Text == "Newer unsent draft", "Closed editors reject late comments and retain their draft");
                                report.Add($"PASS {key}: names/Enter, exact work/parent/text/sticker payload, pending guard, visible failure/retry, newer draft, sticker ratio and detach cancellation");

                                void Type(string value)
                                {
                                    Require(input.Focus(NavigationMethod.Tab), "The reply editor receives keyboard focus");
                                    input.SelectAll();
                                    window.KeyTextInput(value);
                                    Require(input.Text == value, "The real editor retains the complete typed draft");
                                }
                                async Task ActivateAsync(Button button)
                                {
                                    Require(button.Focus(NavigationMethod.Tab), "The send/emoji/sticker action is keyboard reachable");
                                    Press(window, Key.Enter, PhysicalKey.Enter);
                                    await RenderAsync(window);
                                }
                                void CheckPayload(Pending request, string field, string value)
                                {
                                    var body = HttpUtility.ParseQueryString(request.Body);
                                    Require(request.Uri.AbsolutePath == $"/v1/{(type == SimpleWorkType.Illustration ? "illust" : "novel")}/comment/add"
                                        && body[type == SimpleWorkType.Illustration ? "illust_id" : "novel_id"] == WorkId.ToString()
                                        && body["parent_comment_id"] == (reply ? ParentId.ToString() : null) && body[field] == value,
                                        "The original work, parent, text and sticker payload is preserved: " + request.Body);
                                }
                            }
                            finally
                            {
                                host.Content = null;
                                window.Close();
                            }
                        }
            using (var previous = new CommentsViewViewModel(SimpleWorkType.Illustration, WorkId))
            using (var current = new CommentsViewViewModel(SimpleWorkType.Novel, WorkId + 1))
            {
                var page = new CommentContainer { DataContext = previous };
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = 360, Height = 600, Content = host.Shell };
                try
                {
                    window.Show();
                    await RenderAsync(window);
                    var bar = page.FindControl<PixivReplyBar>("ReplyBar")!;
                    var input = bar.FindControl<TextBox>("ReplyContentTextBox")!;
                    var send = bar.FindControl<Button>("SendButton")!;
                    await SendAsync("Previous context draft");
                    var old = await NextAsync();
                    page.DataContext = current;
                    await RenderAsync(window);
                    Require(old.Token.IsCancellationRequested && !bar.IsSubmitting, "Replacing the reply context cancels its pending transport and permits the new context");
                    await SendAsync("Current context draft");
                    var replacement = await NextAsync();
                    old.Response.SetResult(Response(996201, "Late old context"));
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                    Require(bar.IsSubmitting && previous.Source.Count == 0 && current.Source.Count == 0 && input.Text == "Current context draft",
                        "An old completion cannot unlock, populate or clear the replacement editor");
                    replacement.Response.SetResult(Response(996202, "Current context draft"));
                    await UntilAsync(() => !bar.IsSubmitting);
                    Require(current.Source.Count == 1 && input.Text is "" or null, "The replacement editor can complete normally");
                    report.Add("PASS context replacement: actual cancellation, new request, late completion ownership and recovery");
                    await SendAsync("Account switch draft");
                    var switched = await NextAsync();
                    fixture.AuthResponseFactory = (_, _) => Task.FromResult(JsonSerializer.Serialize(TokenResponse.CreateDefault() with
                    {
                        AccessToken = "fixture-second-access",
                        RefreshToken = "fixture-second-refresh",
                        ExpiresIn = 3600,
                        User = TokenUser.CreateDefault() with { Id = 900002, Name = "Fixture second account", Account = "fixture-second" }
                    }));
                    await client.SetTokenAsync("fixture-second-account");
                    await client.ValidateTokenAsync();
                    Require(client.Me?.Id == 900002, "The comment sample actually changes the isolated account");
                    switched.Response.SetResult(Response(996203, "Previous account comment"));
                    await UntilAsync(() => !bar.IsSubmitting);
                    Require(current.Source.Count == 1 && input.Text == "Account switch draft", "A previous account cannot insert a late comment or clear the draft");
                    report.Add("PASS account replacement: late server success cannot populate the next account's comments");

                    async Task SendAsync(string text)
                    {
                        Require(input.Focus(NavigationMethod.Tab), "The replacement editor accepts focus");
                        input.SelectAll();
                        window.KeyTextInput(text);
                        Require(send.Focus(NavigationMethod.Tab), "The replacement send action is reachable");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                    }
                }
                finally
                {
                    host.Content = null;
                    window.Close();
                }
            }
            Require(fixture.UnexpectedRequests.IsEmpty, "Comment requests stay inside the fixed matrix: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "comment-send-contracts.txt"), report);
            Console.WriteLine($"Validated {report.Count} comment-send groups.");
        }
        finally
        {
            foreach (var operation in pending)
                operation.Response.TrySetCanceled();
            File.WriteAllLines(Path.Combine(directory, "comment-send-requests.txt"), fixture.RequestLog);
        }
        return;
        Task<Pending> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static Comment Comment(long id, string content) => Mako.Model.Comment.CreateDefault() with
    {
        Id = id,
        Content = content,
        Date = DateTimeOffset.UnixEpoch,
        User = AvatarUser.CreateDefault() with { Id = 900001, Name = "Fixture author", ProfileImageUrls = new() { Medium = ImageUri } }
    };
    private static string Response(long id, string content, bool sticker = false) => JsonSerializer.Serialize(new
    {
        comment = Comment(id, content) with { Stamp = sticker ? Stamp.CreateDefault() with { StampId = 301, StampUrl = string.Format(CommentImageHelper.StickerDownloadUrl, 301) } : null }
    });
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The comment operation did not reach its expected state");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
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
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No comment frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void VerifyStickerPixels(Window window, Image picture)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No sticker frame");
        using var buffer = new MemoryStream();
        frame.Save(buffer, new PngBitmapEncoderOptions());
        using var pixels = SKBitmap.Decode(buffer.ToArray());
        var origin = picture.TranslatePoint(default, window)!.Value;
        var left = (int) Math.Ceiling(origin.X);
        var top = (int) Math.Ceiling(origin.Y);
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        for (var y = Math.Max(0, top); y < Math.Min(pixels.Height, origin.Y + picture.Bounds.Height); y++)
            for (var x = Math.Max(0, left); x < Math.Min(pixels.Width, origin.X + picture.Bounds.Width); x++)
            {
                var color = pixels.GetPixel(x, y);
                if (Math.Abs(color.Red - 100) > 4 || Math.Abs(color.Green - 149) > 4 || Math.Abs(color.Blue - 237) > 4)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        Require(maxX > minX && maxY > minY && (double) (maxX - minX + 1) / (maxY - minY + 1) is > 1.85 and < 2.15,
            "The actual rendered 2:1 sticker retains its aspect ratio inside the square target");
    }
    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
    private sealed record Pending(Uri Uri, string Body, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
