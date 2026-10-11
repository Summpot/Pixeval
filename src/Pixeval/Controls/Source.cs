// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Pixeval.Pixiv;
using Pixeval.Services.Media;
using Pixeval.Utilities;

namespace Pixeval.Controls;

public static class Source
{
    public static readonly AttachedProperty<IImageProviderService?> ImageProviderProperty =
        AvaloniaProperty.RegisterAttached<Control, IImageProviderService?>(
            "ImageProvider",
            typeof(Source),
            defaultValue: null,
            inherits: true);

    public static readonly AttachedProperty<string?> CacheProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>(
            "Cache",
            typeof(Source),
            defaultValue: null);

    public static readonly AttachedProperty<string?> BackgroundCacheProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>(
            "BackgroundCache",
            typeof(Source),
            defaultValue: null);

    public static readonly AttachedProperty<bool> LoadedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "Loaded",
            typeof(Source),
            defaultValue: false);

    private static readonly AttachedProperty<SourceLoadLifetime?> CacheLoadLifetimeProperty =
        AvaloniaProperty.RegisterAttached<Control, SourceLoadLifetime?>(
            "CacheLoadLifetime",
            typeof(Source),
            defaultValue: null);

    private static readonly AttachedProperty<SourceLoadLifetime?> BackgroundCacheLoadLifetimeProperty =
        AvaloniaProperty.RegisterAttached<Control, SourceLoadLifetime?>(
            "BackgroundCacheLoadLifetime",
            typeof(Source),
            defaultValue: null);

    // TODO: 如何确保Platform顺序高于Cache
    public static readonly AttachedProperty<string> PlatformProperty =
        AvaloniaProperty.RegisterAttached<Control, string>(
            "Platform",
            typeof(Source),
            defaultValue: PlatformConstants.Pixiv);

    private static readonly AttachedProperty<bool> IsDetachedHookedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsDetachedHooked",
            typeof(Source),
            defaultValue: false);

    static Source()
    {
        CacheProperty.Changed.AddClassHandler<Control>(OnCacheChanged);
        BackgroundCacheProperty.Changed.AddClassHandler<Control>(OnBackgroundCacheChanged);
        ImageProviderProperty.Changed.AddClassHandler<Control>(OnImageProviderChanged);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IImageProviderService? GetImageProvider(Control element) => element.GetValue(ImageProviderProperty);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetImageProvider(Control element, IImageProviderService? value) => element.SetValue(ImageProviderProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetCache(Control element) => element.GetValue(CacheProperty);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetCache(Control element, string? value) => element.SetValue(CacheProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetBackgroundCache(Control element) => element.GetValue(BackgroundCacheProperty);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetBackgroundCache(Control element, string? value) => element.SetValue(BackgroundCacheProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool GetLoaded(Control element) => element.GetValue(LoadedProperty);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetLoaded(Control element, bool value) => element.SetValue(LoadedProperty, value);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string GetPlatform(Control element) => element.GetValue(PlatformProperty);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetPlatform(Control element, string value) => element.SetValue(PlatformProperty, value);

    private static void OnImageProviderChanged(Control element, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not IImageProviderService images)
        {
            AbandonLoad(element, CacheLoadLifetimeProperty);
            AbandonLoad(element, BackgroundCacheLoadLifetimeProperty);
            return;
        }

        if (GetCache(element) is { } cacheKey && !GetLoaded(element))
        {
            LoadCacheCore(element, cacheKey, images);
        }

        if (GetBackgroundCache(element) is { } bgKey && !GetLoaded(element))
        {
            LoadBackgroundCacheCore(element, bgKey, images);
        }
    }

    private static void OnCacheChanged(Control element, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<string>() is not { } value)
        {
            ClearImageSource(element);
            AbandonLoad(element, CacheLoadLifetimeProperty);
            SetLoaded(element, false);
            return;
        }

        ClearImageSource(element);
        SetLoaded(element, false);

        if (GetImageProvider(element) is not { } images)
            return;

        LoadCacheCore(element, value, images);
    }

    private static async void LoadCacheCore(Control element, string value, IImageProviderService images)
    {
        var lifetime = BeginLoad(element, CacheLoadLifetimeProperty);
        try
        {
            IDisposable bitmap;
            var platform = GetPlatform(element);
            if (element is AnimatedImage or AvatarImage)
            {
                bitmap = await images.GetAnimatedBitmapAsync(platform, value, token: lifetime.Token);
            }
            else if (element is Image)
            {
                bitmap = await images.GetBitmapAsync(platform, value, token: lifetime.Token);
            }
            else
            {
                return;
            }

            if (!lifetime.TrySetSource(bitmap))
                return;
            if (GetCache(element) != value)
            {
                lifetime.Dispose();
                return;
            }

            SetImageSource(element, bitmap);
            SetLoaded(element, true);
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
        }
    }

    private static void OnBackgroundCacheChanged(Control element, AvaloniaPropertyChangedEventArgs e)
    {
        var backgroundProperty = GetBackgroundProperty(element);
        if (backgroundProperty is null)
            return;

        if (e.GetNewValue<string>() is not { } value)
        {
            element.ClearValue(backgroundProperty);
            AbandonLoad(element, BackgroundCacheLoadLifetimeProperty);
            SetLoaded(element, false);
            return;
        }

        element.ClearValue(backgroundProperty);
        SetLoaded(element, false);

        if (GetImageProvider(element) is not { } images)
            return;

        LoadBackgroundCacheCore(element, value, images);
    }

    private static async void LoadBackgroundCacheCore(Control element, string value, IImageProviderService images)
    {
        var backgroundProperty = GetBackgroundProperty(element);
        if (backgroundProperty is null)
            return;

        var lifetime = BeginLoad(element, BackgroundCacheLoadLifetimeProperty);
        try
        {
            var bitmap = await images.GetBitmapAsync(GetPlatform(element), value, token: lifetime.Token);
            if (!lifetime.TrySetSource(bitmap))
                return;
            if (GetBackgroundCache(element) != value)
            {
                lifetime.Dispose();
                return;
            }

            var o = element.GetValue(backgroundProperty);
            if (o is ImageBrush brush)
            {
                brush.Source = bitmap;
            }
            else
            {
                brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }

            element.SetValue(backgroundProperty, brush);
            SetLoaded(element, true);
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
        }
    }

    private static void ClearImageSource(Control element)
    {
        switch (element)
        {
            case AnimatedImage animatedImage:
                animatedImage.Source = null;
                break;
            case AvatarImage avatarImage:
                avatarImage.Source = null;
                break;
            case Image image:
                image.Source = null;
                break;
        }
    }

    private static void SetImageSource(Control element, IDisposable bitmap)
    {
        switch (element)
        {
            case AnimatedImage animatedImage:
                animatedImage.Source = (IAnimatedBitmap)bitmap;
                break;
            case AvatarImage avatarImage:
                avatarImage.Source = (IAnimatedBitmap)bitmap;
                break;
            case Image image:
                image.Source = (Bitmap)bitmap;
                break;
        }
    }

    private static AvaloniaProperty<IBrush?>? GetBackgroundProperty(Control element) =>
        element switch
        {
            Border => Border.BackgroundProperty,
            Panel => Panel.BackgroundProperty,
            TemplatedControl => TemplatedControl.BackgroundProperty,
            _ => null
        };

    private static SourceLoadOperation BeginLoad(
        Control element,
        AttachedProperty<SourceLoadLifetime?> lifetimeProperty)
    {
        if (element.GetValue(lifetimeProperty) is not { } lifetime)
        {
            lifetime = new SourceLoadLifetime();
            element.SetValue(lifetimeProperty, lifetime);
        }

        if (!element.GetValue(IsDetachedHookedProperty))
        {
            element.SetValue(IsDetachedHookedProperty, true);
            element.DetachedFromVisualTree += OnElementDetached;
        }

        return lifetime.BeginLoad();
    }

    private static void AbandonLoad(
        Control element,
        AttachedProperty<SourceLoadLifetime?> lifetimeProperty)
    {
        element.GetValue(lifetimeProperty)?.AbandonCurrent();
    }

    private static void OnElementDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control element)
            return;

        DisposeLifetime(element, CacheLoadLifetimeProperty);
        DisposeLifetime(element, BackgroundCacheLoadLifetimeProperty);
    }

    private static void DisposeLifetime(
        Control element,
        AttachedProperty<SourceLoadLifetime?> lifetimeProperty)
    {
        if (element.GetValue(lifetimeProperty) is { } lifetime)
        {
            lifetime.Dispose();
            element.ClearValue(lifetimeProperty);
        }
    }
}

internal sealed class SourceLoadLifetime : IDisposable
{
    private readonly Lock _gate = new();
    private readonly HashSet<SourceLoadOperation> _operations = [];
    private SourceLoadOperation? _current;

    public bool IsDisposed { get; private set; }

    public SourceLoadOperation BeginLoad()
    {
        var operation = new SourceLoadOperation(OnOperationDisposed);
        SourceLoadOperation? previous = null;
        var isDisposed = false;
        lock (_gate)
        {
            if (IsDisposed)
                isDisposed = true;
            else
            {
                previous = _current;
                _current = operation;
                _operations.Add(operation);
            }
        }

        if (isDisposed)
        {
            operation.Dispose();
            return operation;
        }

        // Recycling a container only makes its old result obsolete. Let the cache fill finish so
        // virtualized scrolling doesn't turn every binding change into a canceled HTTP request.
        previous?.Abandon();
        return operation;
    }

    public void AbandonCurrent()
    {
        SourceLoadOperation? current;
        lock (_gate)
        {
            current = _current;
            _current = null;
        }

        current?.Abandon();
    }

    public void Dispose()
    {
        SourceLoadOperation[] operations;
        lock (_gate)
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _current = null;
            operations = [.. _operations];
            _operations.Clear();
        }

        foreach (var operation in operations)
            operation.Dispose();
    }

    private void OnOperationDisposed(SourceLoadOperation operation)
    {
        lock (_gate)
        {
            _operations.Remove(operation);
            if (ReferenceEquals(_current, operation))
                _current = null;
        }
    }
}

internal sealed class SourceLoadOperation : IDisposable
{
    private readonly Action<SourceLoadOperation> _onDisposed;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IDisposable? _source;
    private bool _isAbandoned;
    private bool _isDisposed;

    public SourceLoadOperation(Action<SourceLoadOperation> onDisposed)
    {
        _onDisposed = onDisposed;
        Token = _cancellationTokenSource.Token;
    }

    public CancellationToken Token { get; }

    public bool TrySetSource(IDisposable source)
    {
        var shouldComplete = false;
        lock (_gate)
        {
            if (!_isDisposed && !_isAbandoned)
            {
                _source = source;
                return true;
            }

            if (!_isDisposed)
            {
                _isDisposed = true;
                shouldComplete = true;
            }
        }

        source.Dispose();
        if (shouldComplete)
            Complete(cancel: false);
        return false;
    }

    public void Abandon()
    {
        IDisposable? source;
        lock (_gate)
        {
            if (_isDisposed || _isAbandoned)
                return;

            _isAbandoned = true;
            source = _source;
            _source = null;
            if (source is null)
                return;

            _isDisposed = true;
        }

        source.Dispose();
        Complete(cancel: false);
    }

    public void Dispose()
    {
        IDisposable? source;
        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            source = _source;
            _source = null;
        }

        source?.Dispose();
        Complete(cancel: true);
    }

    private void Complete(bool cancel)
    {
        if (cancel)
            _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _onDisposed(this);
    }
}
