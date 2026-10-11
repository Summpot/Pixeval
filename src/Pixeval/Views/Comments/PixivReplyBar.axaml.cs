// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Views.Comments;

public partial class PixivReplyBar : UserControl
{
    public static readonly StyledProperty<string> ReplyTextProperty =
        AvaloniaProperty.Register<PixivReplyBar, string>(
            nameof(ReplyText),
            "",
            defaultBindingMode: BindingMode.TwoWay);

    public PixivReplyBar() => InitializeComponent();

    public string ReplyText
    {
        get => GetValue(ReplyTextProperty);
        set => SetValue(ReplyTextProperty, value);
    }

    public event Action<string>? SendButtonClick;

    public event Func<int, CancellationToken, Task<bool>>? StickerClick;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelSubmission();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        CancelSubmission();
        base.OnDataContextChanged(e);
    }

    private void CancelSubmission()
    {
        var submission = _submission;
        _submission = null;
        submission?.Cancel();
        IsSubmitting = false;
    }

    private void EmojiImage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsSubmitting || sender is not Control { DataContext: int emojiId })
            return;
        var textBox = ReplyContentTextBox;
        var caretIndex = textBox.CaretIndex;
        var placeholder = ((PixivReplyEmoji) emojiId).PlaceholderKey;
        textBox.Text = (textBox.Text ?? "").Insert(caretIndex, placeholder);
        textBox.CaretIndex = caretIndex + placeholder.Length;
    }

    private async void StickerImage_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsSubmitting || sender is not Control { DataContext: int id } || StickerClick is not { } send)
            return;
        EmojiButton.Flyout?.Hide();
        await SubmitAsync(token => send(id, token), null);
    }

    private async void SendButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (IsSubmitting || SendButtonClick is not { } send)
            return;
        var content = ReplyContentTextBox.Text;
        if (string.IsNullOrEmpty(content) || content.Length > 140)
        {
            _ = this.Dialogs.CreateAcknowledgementAsync(
                I18NManager.GetResource(PixivReplyBarResources.CommentIsTooShortOrTooLongToastTitle),
                I18NManager.GetResource(PixivReplyBarResources.CommentIsTooShortOrTooLongToastContentFormatted,
                    content?.Length ?? 0));
            return;
        }

        await SubmitAsync(token => send(content, token), content);
    }

    private async Task SubmitAsync(Func<CancellationToken, Task<bool>> send, string? originalText)
    {
        using var submission = new CancellationTokenSource();
        _submission = submission;
        IsSubmitting = true;
        try
        {
            var succeeded = await send(submission.Token);
            if (succeeded && !submission.IsCancellationRequested && ReferenceEquals(_submission, submission)
                && originalText is not null && ReplyContentTextBox.Text == originalText)
                ReplyContentTextBox.Clear();
        }
        catch (OperationCanceledException) when (submission.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!submission.IsCancellationRequested && ReferenceEquals(_submission, submission))
                TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(I18NManager.GetResource(PixivReplyBarResources.SendFailed));
        }
        finally
        {
            if (ReferenceEquals(_submission, submission))
            {
                _submission = null;
                IsSubmitting = false;
            }
        }
    }
}
