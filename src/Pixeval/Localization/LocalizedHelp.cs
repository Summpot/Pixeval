using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Avalonia.Platform;

namespace Pixeval.Localization;

public sealed class LocalizedHelp : INotifyPropertyChanged
{
    public static LocalizedHelp Instance { get; } = new();
    private readonly Dictionary<(string Locale, string Name), string> _cache = [];
    private readonly IDisposable _subscription;
    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizedHelp() => _subscription = AppLocalization.Instance.Subscribe(
        () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)));

    public string this[string name]
    {
        get
        {
            var key = (Strings.CurrentLocale, name);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            foreach (var locale in new[] { key.Item1, Strings.DefaultLocale })
            {
                var uri = new Uri($"avares://Pixeval/Assets/Help/{locale}/{name}.md");
                if (!AssetLoader.Exists(uri)) continue;
                using var reader = new StreamReader(AssetLoader.Open(uri));
                return _cache[key] = reader.ReadToEnd();
            }
            return string.Empty;
        }
    }
}
