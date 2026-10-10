// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.I18N;

namespace Pixeval.Models.Download;

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

public sealed record TransducerMacroDefinition(string Name, string Description) : ITransducer
{
    public System.Type ContextType => typeof(object);
}

public sealed record PredicateMacroDefinition(string Name, string Description) : IPredicate
{
    public System.Type ContextType => typeof(object);
}

public static class MacroDefinitions
{
    public static IReadOnlyList<IMacro> All { get; } =
    [
        new TransducerMacroDefinition("id", I18NManager.GetResource(MacroParserResources.MacroDescription.Id)),
        new TransducerMacroDefinition("artist_id", I18NManager.GetResource(MacroParserResources.MacroDescription.ArtistId)),
        new TransducerMacroDefinition("artist_name", I18NManager.GetResource(MacroParserResources.MacroDescription.ArtistName)),
        new TransducerMacroDefinition("work_title", I18NManager.GetResource(MacroParserResources.MacroDescription.Title)),
        new TransducerMacroDefinition("title", I18NManager.GetResource(MacroParserResources.MacroDescription.Title)),
        new TransducerMacroDefinition("work_publish_time", I18NManager.GetResource(MacroParserResources.MacroDescription.PublishTime)),
        new TransducerMacroDefinition("publish_time", I18NManager.GetResource(MacroParserResources.MacroDescription.PublishTime)),
        new TransducerMacroDefinition("ext", I18NManager.GetResource(MacroParserResources.MacroDescription.Ext)),
        new TransducerMacroDefinition("group_id", I18NManager.GetResource(MacroParserResources.MacroDescription.GroupId)),
        new TransducerMacroDefinition("series_id", I18NManager.GetResource(MacroParserResources.MacroDescription.SeriesId)),
        new TransducerMacroDefinition("series_title", I18NManager.GetResource(MacroParserResources.MacroDescription.SeriesTitle)),
        new TransducerMacroDefinition("pic_set_index", I18NManager.GetResource(MacroParserResources.MacroDescription.PicSetIndex)),
        new PredicateMacroDefinition("is_group", I18NManager.GetResource(MacroParserResources.MacroDescription.IsGroup)),
        new PredicateMacroDefinition("is_bookmark_group", I18NManager.GetResource(MacroParserResources.MacroDescription.IsBookmarkGroup)),
        new PredicateMacroDefinition("is_post_group", I18NManager.GetResource(MacroParserResources.MacroDescription.IsPostGroup)),
        new PredicateMacroDefinition("is_series_group", I18NManager.GetResource(MacroParserResources.MacroDescription.IsSeriesGroup)),
        new PredicateMacroDefinition("is_series", I18NManager.GetResource(MacroParserResources.MacroDescription.IsSeries)),
        new PredicateMacroDefinition("is_pic_set", I18NManager.GetResource(MacroParserResources.MacroDescription.IsPicSet)),
        new PredicateMacroDefinition("is_pic_one", I18NManager.GetResource(MacroParserResources.MacroDescription.IsPicOne)),
        new PredicateMacroDefinition("is_pic_gif", I18NManager.GetResource(MacroParserResources.MacroDescription.IsPicGif)),
        new PredicateMacroDefinition("is_r18", I18NManager.GetResource(MacroParserResources.MacroDescription.IsR18)),
        new PredicateMacroDefinition("is_r18g", I18NManager.GetResource(MacroParserResources.MacroDescription.IsR18G)),
        new PredicateMacroDefinition("is_ai", I18NManager.GetResource(MacroParserResources.MacroDescription.IsAi)),
        new PredicateMacroDefinition("is_novel", I18NManager.GetResource(MacroParserResources.MacroDescription.IsNovel))
    ];
}
