// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoSettingsPage;
using Avalonia.Media;
using FluentIcons.Common;

namespace Pixeval.Config;

public record NovelSettingsGroup
{
    [JsonConverter(typeof(JsonColorConverter))]
    [SettingsEntry(Symbol.TextColor, nameof(Strings.AppSettingsNovelSettingsFontColorEntryHeader), nameof(Strings.AppSettingsNovelSettingsFontColorEntryDescription))]
    public uint NovelFontColor { get; set; }

    [JsonConverter(typeof(JsonColorConverter))]
    [SettingsEntry(Symbol.ColorBackground, nameof(Strings.AppSettingsNovelSettingsBackgroundEntryHeader), nameof(Strings.AppSettingsNovelSettingsBackgroundEntryDescription))]
    public uint NovelBackground { get; set; }

    [SettingsEntry(Symbol.LineThickness, nameof(Strings.AppSettingsNovelSettingsFontWeightEntryHeader), nameof(Strings.AppSettingsNovelSettingsFontWeightEntryDescription), nameof(Strings.AppSettingsNovelSettingsFontWeightEntryPlaceholder))]
    public FontWeight NovelFontWeight { get; set; } = FontWeight.Normal;

    [SettingsEntry(Symbol.TextFont, nameof(Strings.AppSettingsNovelSettingsFontFamilyEntryHeader), nameof(Strings.AppSettingsAppFontFamilyEntryDescription), nameof(Strings.AppSettingsAppFontFamilyEntryPlaceholder))]
    public ObservableCollection<string> NovelFontFamily { get; set; } = [];

    [SettingsEntry(Symbol.TextFontSize, nameof(Strings.AppSettingsNovelSettingsFontSizeEntryHeader), nameof(Strings.AppSettingsNovelSettingsFontSizeEntryDescription))]
    public int NovelFontSize { get; set; } = 14;

    [SettingsEntry(Symbol.TextLineSpacing, nameof(Strings.AppSettingsNovelSettingsLineHeightEntryHeader), nameof(Strings.AppSettingsNovelSettingsLineHeightEntryDescription))]
    public int NovelLineHeight { get; set; } = 28;

    [SettingsEntry(Symbol.AutoFitWidth, nameof(Strings.AppSettingsNovelSettingsMaxWidthEntryHeader), nameof(Strings.AppSettingsNovelSettingsMaxWidthEntryDescription))]
    public int NovelMaxWidth { get; set; } = 1000;
}

public class JsonColorConverter : JsonConverter<uint>
{
    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is not ['#', .. { Length: 8 } color])
            throw new FormatException("Invalid color format.");
        return uint.Parse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"#{value:X8}");
    }
}
