// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Localization;

namespace Pixeval.Download;

public interface IMacro
{
    string Name { get; }
    string Description { get; }
}

public interface ITransducer : IMacro
{
    System.Type ContextType { get; }
}

public interface IPredicate : IMacro
{
    System.Type ContextType { get; }
}

public abstract class MacroDefinition : IMacro, System.ComponentModel.INotifyPropertyChanged
{
    private readonly System.IDisposable _languageSubscription;
    protected MacroDefinition(string name, string resourceKey)
    {
        Name = name;
        ResourceKey = resourceKey;
        _languageSubscription = AppLocalization.Instance.Subscribe(() => PropertyChanged?.Invoke(this,
            new System.ComponentModel.PropertyChangedEventArgs(nameof(Description))));
    }
    public string Name { get; }
    public string ResourceKey { get; }
    public string Description => Strings.GetString(ResourceKey);
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TransducerMacroDefinition(string name, string resourceKey) : MacroDefinition(name, resourceKey), ITransducer
{
    public System.Type ContextType => typeof(object);
}

public sealed class PredicateMacroDefinition(string name, string resourceKey) : MacroDefinition(name, resourceKey), IPredicate
{
    public System.Type ContextType => typeof(object);
}

public static class MacroDefinitions
{
    public static IReadOnlyList<IMacro> All { get; } =
    [
        new TransducerMacroDefinition("id", nameof(Strings.MacroParserMacroDescriptionId)),
        new TransducerMacroDefinition("artist_id", nameof(Strings.MacroParserMacroDescriptionArtistId)),
        new TransducerMacroDefinition("artist_name", nameof(Strings.MacroParserMacroDescriptionArtistName)),
        new TransducerMacroDefinition("work_title", nameof(Strings.MacroParserMacroDescriptionTitle)),
        new TransducerMacroDefinition("title", nameof(Strings.MacroParserMacroDescriptionTitle)),
        new TransducerMacroDefinition("work_publish_time", nameof(Strings.MacroParserMacroDescriptionPublishTime)),
        new TransducerMacroDefinition("publish_time", nameof(Strings.MacroParserMacroDescriptionPublishTime)),
        new TransducerMacroDefinition("ext", nameof(Strings.MacroParserMacroDescriptionExt)),
        new TransducerMacroDefinition("group_id", nameof(Strings.MacroParserMacroDescriptionGroupId)),
        new TransducerMacroDefinition("series_id", nameof(Strings.MacroParserMacroDescriptionSeriesId)),
        new TransducerMacroDefinition("series_title", nameof(Strings.MacroParserMacroDescriptionSeriesTitle)),
        new TransducerMacroDefinition("pic_set_index", nameof(Strings.MacroParserMacroDescriptionPicSetIndex)),
        new PredicateMacroDefinition("is_group", nameof(Strings.MacroParserMacroDescriptionIsGroup)),
        new PredicateMacroDefinition("is_bookmark_group", nameof(Strings.MacroParserMacroDescriptionIsBookmarkGroup)),
        new PredicateMacroDefinition("is_post_group", nameof(Strings.MacroParserMacroDescriptionIsPostGroup)),
        new PredicateMacroDefinition("is_series_group", nameof(Strings.MacroParserMacroDescriptionIsSeriesGroup)),
        new PredicateMacroDefinition("is_series", nameof(Strings.MacroParserMacroDescriptionIsSeries)),
        new PredicateMacroDefinition("is_pic_set", nameof(Strings.MacroParserMacroDescriptionIsPicSet)),
        new PredicateMacroDefinition("is_pic_one", nameof(Strings.MacroParserMacroDescriptionIsPicOne)),
        new PredicateMacroDefinition("is_pic_gif", nameof(Strings.MacroParserMacroDescriptionIsPicGif)),
        new PredicateMacroDefinition("is_r18", nameof(Strings.MacroParserMacroDescriptionIsR18)),
        new PredicateMacroDefinition("is_r18g", nameof(Strings.MacroParserMacroDescriptionIsR18G)),
        new PredicateMacroDefinition("is_ai", nameof(Strings.MacroParserMacroDescriptionIsAi)),
        new PredicateMacroDefinition("is_novel", nameof(Strings.MacroParserMacroDescriptionIsNovel))
    ];
}
