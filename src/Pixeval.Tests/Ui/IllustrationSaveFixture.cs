using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;

namespace Pixeval.Tests.Ui;

internal sealed class IllustrationSaveFixture : IStorageProvider, IStorageProviderFactory
{
    private TaskCompletionSource<IStorageFile?>? _pending;
    internal FilePickerSaveOptions? Options { get; private set; }
    internal int Calls { get; private set; }
    public bool CanSave { get; set; } = true;
    public bool CanOpen => false;
    public bool CanPickFolder => false;
    public IStorageProvider CreateProvider(TopLevel topLevel) => this;

    public Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
    {
        if (_pending is not null)
            throw new InvalidOperationException("A save picker is already pending.");
        Calls++;
        Options = options;
        _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.Task;
    }

    internal void Complete(IStorageFile? file)
    {
        var pending = _pending ?? throw new InvalidOperationException("No save picker is pending.");
        _pending = null;
        pending.SetResult(file);
    }

    internal void Fail()
    {
        var pending = _pending ?? throw new InvalidOperationException("No save picker is pending.");
        _pending = null;
        pending.SetException(new IOException("Fixed save picker failure"));
    }

    public async Task<SaveFilePickerResult> SaveFilePickerWithResultAsync(FilePickerSaveOptions options) =>
        new() { File = await SaveFilePickerAsync(options) };
    public Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options) => throw new NotSupportedException();
    public Task<OpenFilePickerResult> OpenFilePickerWithResultAsync(FilePickerOpenOptions options) => throw new NotSupportedException();
    public Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options) => throw new NotSupportedException();
    public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark) => throw new NotSupportedException();
    public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark) => throw new NotSupportedException();
    public Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath) => throw new NotSupportedException();
    public Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath) => throw new NotSupportedException();
    public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder) => throw new NotSupportedException();

    internal sealed class File : IStorageFile
    {
        internal bool Disposed { get; private set; }
        internal int Opens { get; private set; }
        internal bool FailOpen { get; init; }
        internal TaskCompletionSource<Stream>? PendingOpen { get; init; }
        internal OutputStream Output { get; } = new();
        public string Name => "displayed.png";
        public Uri Path => new("file:///m3e-fixture/displayed.png");
        public bool CanBookmark => false;
        public Task<Stream> OpenWriteAsync()
        {
            Opens++;
            if (FailOpen)
                throw new IOException("Fixed output access failure");
            return PendingOpen?.Task ?? Task.FromResult<Stream>(Output);
        }
        public void Dispose() => Disposed = true;
        public Task<Stream> OpenReadAsync() => throw new NotSupportedException();
        public Task<StorageItemProperties> GetBasicPropertiesAsync() => throw new NotSupportedException();
        public Task<string?> SaveBookmarkAsync() => throw new NotSupportedException();
        public Task<IStorageFolder?> GetParentAsync() => throw new NotSupportedException();
        public Task DeleteAsync() => throw new NotSupportedException();
        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => throw new NotSupportedException();
    }

    internal sealed class OutputStream : MemoryStream
    {
        internal bool Disposed { get; private set; }
        internal bool FailWrite { get; set; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (FailWrite) throw new IOException("Fixed output write failure");
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (FailWrite) throw new IOException("Fixed output write failure");
            base.Write(buffer);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FailWrite) return ValueTask.FromException(new IOException("Fixed output write failure"));
            return base.WriteAsync(buffer, cancellationToken);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (FailWrite) return Task.FromException(new IOException("Fixed output write failure"));
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
        }
    }
}
