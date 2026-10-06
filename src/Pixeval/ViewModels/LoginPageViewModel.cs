// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Native.Storage;

namespace Pixeval.ViewModels;

public partial class LoginPageViewModel : ViewModelBase
{
    private readonly SemaphoreSlim _loadUsersLock = new(1, 1);
    private readonly int _currentUserKey;
    private readonly StorageEngine _storageEngine;
    private bool _areUsersLoaded;

    public LoginPageViewModel() : this(
        App.AppViewModel.StorageEngine,
        App.AppViewModel.LoginContext.CurrentKey)
    {
    }

    internal LoginPageViewModel(StorageEngine storageEngine, int currentUserKey)
    {
        _storageEngine = storageEngine;
        _currentUserKey = currentUserKey;
        Users = [];
        RefreshToken = "";
    }

    public ObservableCollection<LoginUserRecord> Users { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedUser))]
    public partial LoginUserRecord? SelectedUser { get; set; }

    [ObservableProperty]
    public partial string RefreshToken { get; set; }

    [ObservableProperty]
    public partial bool IsLoginInProgress { get; set; }

    public bool HasSelectedUser => SelectedUser is not null;

    public async Task LoadUsersAsync(CancellationToken token = default)
    {
        await _loadUsersLock.WaitAsync(token);
        try
        {
            if (_areUsersLoaded)
                return;

            Users.Clear();
            foreach (var user in _storageEngine.GetAllLoginUsers())
            {
                token.ThrowIfCancellationRequested();
                Users.Add(user);
            }
            _areUsersLoaded = true;
            SelectedUser = Users.FirstOrDefault(user => user.HistoryEntryId == _currentUserKey);
        }
        finally
        {
            _ = _loadUsersLock.Release();
        }
    }

    public static AutoCompleteFilterPredicate<object> LoginUserFilter { get; } = static (_, item) => item is LoginUserRecord;

    public static AutoCompleteSelector<object> LoginUserTextSelector { get; } = static (_, item) =>
        item is LoginUserRecord user ? user.RefreshToken : item?.ToString() ?? "";

    partial void OnSelectedUserChanged(LoginUserRecord? value)
    {
        if (value is not null && RefreshToken != value.RefreshToken)
            RefreshToken = value.RefreshToken;
    }

    partial void OnRefreshTokenChanged(string value)
    {
        var selected = Users.FirstOrDefault(t => t.RefreshToken == value);
        if (!Equals(SelectedUser, selected))
            SelectedUser = selected;
    }
}
