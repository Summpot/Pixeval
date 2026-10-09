// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Native.Config;
using Pixeval.Utilities;
using Pixeval.ViewModels.Home;

namespace Pixeval.Views.Home;

public sealed class HomeCardDefinition(
    HomePageCardSourceKind sourceKind,
    Func<HomePageCardLayout, Task<HomeCardPreviewSource>> previewSourceFactory,
    Action<HomePageCardLayout, HomeCardPreviewSource, TopLevel> pageOpener,
    Func<HomePageCardLayout, IReadOnlyList<string>>? titleParameterFactory = null)
{
    private static readonly ConfigEngine ConfigEngine = new();

    private readonly HomeCardMetadata _metadata = ConfigEngine.GetCardMetadata(sourceKind);

    public HomePageCardSourceKind SourceKind { get; } = sourceKind;

    public Symbol Symbol => AvaloniaHelper.GetHomeCardHeader(SourceKind).Symbol;

    public HomeCardParameterKinds Parameters => (HomeCardParameterKinds)_metadata.ParameterFlags;

    public int DefaultColumnSpan => _metadata.DefaultColumnSpan;

    public int DefaultRowSpan => _metadata.DefaultRowSpan;

    public WorkType WorkType => (WorkType)_metadata.DefaultWorkType;

    public SimpleWorkType SimpleWorkType => (SimpleWorkType)_metadata.DefaultSimpleWorkType;

    public PrivacyPolicy PrivacyPolicy => (PrivacyPolicy)_metadata.DefaultPrivacyPolicy;

    public bool UseCurrentUserAsDefault => _metadata.UseCurrentUserAsDefault;

    public string Title => AvaloniaHelper.GetHomeCardHeader(SourceKind).Header;

    public string Description => I18NManager.GetResource($"Enum.HomePageCardSourceKindDescription.{SourceKind}");

    public bool HasParameter(HomeCardParameterKinds parameter) =>
        (Parameters & parameter) is not HomeCardParameterKinds.None;

    public Task<HomeCardPreviewSource> CreatePreviewSourceAsync(HomePageCardLayout card) =>
        previewSourceFactory(card);

    public void OpenCardPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        try
        {
            pageOpener(card, source, topLevel);
        }
        finally
        {
            source.Dispose();
        }
    }

    public string BuildTitle(HomePageCardLayout card)
    {
        var parts = new List<string> { Title };
        if (titleParameterFactory is not null)
            parts.AddRange(titleParameterFactory(card));

        return string.Join(I18NManager.GetResource(HomePageResources.CardTitle.ParameterSeparator), parts);
    }
}
