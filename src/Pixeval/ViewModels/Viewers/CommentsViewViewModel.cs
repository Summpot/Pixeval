// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public class CommentsViewViewModel : EntryViewViewModel<Comment, CommentItemViewModel>
{
    public long ParentId { get; }

    public SimpleWorkType ParentType { get; }

    protected readonly MakoClient MakoClient;
    protected readonly IUserSessionService? UserSessionService;

    public override SimpleViewDataProvider<Comment, CommentItemViewModel> DataProvider { get; } = new();

    public CommentsViewViewModel(
        SimpleWorkType parentType,
        long parentId,
        MakoClient? makoClient = null,
        IUserSessionService? userSessionService = null)
    {
        ParentType = parentType;
        ParentId = parentId;
        MakoClient = makoClient ?? App.Services?.GetService<MakoClient>() ?? App.AppViewModel.MakoClient;
        UserSessionService = userSessionService ?? App.Services?.GetService<IUserSessionService>();
    }

    public virtual async Task<Comment> AddCommentAsync(string content)
    {
        return await MakoClient.AddWorkCommentAsync(ParentType, ParentId, content);
    }

    public virtual async Task<Comment> AddStickerAsync(int stampId)
    {
        return await MakoClient.AddWorkCommentAsync(ParentType, ParentId, stampId);
    }

    public virtual void AddComment(Comment comment) => Source.Insert(0, new CommentItemViewModel(comment, ParentType, ParentId, true, MakoClient, UserSessionService));

    public void DeleteComment(CommentItemViewModel viewModel) => Source.Remove(viewModel);

    public virtual void RefreshEngine()
    {
        ResetEngine(MakoClient.WorkComments(ParentType, ParentId),
            (comment, _) => new(comment, ParentType, ParentId, true, MakoClient, UserSessionService));
    }
}
