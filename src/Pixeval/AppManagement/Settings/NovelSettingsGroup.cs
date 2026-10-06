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

namespace Pixeval.AppManagement.Settings;

public record NovelSettingsGroup
{
    [JsonConverter(typeof(JsonColorConverter))]
    [SettingsEntry(Symbol.TextColor, AppSettingsResources.NovelSettingsFontColorEntry.Header, AppSettingsResources.NovelSettingsFontColorEntry.Description)]
    public uint NovelFontColor { get; set; }

    [JsonConverter(typeof(JsonColorConverter))]
    [SettingsEntry(Symbol.ColorBackground, AppSettingsResources.NovelSettingsBackgroundEntry.Header, AppSettingsResources.NovelSettingsBackgroundEntry.Description)]
    public uint NovelBackground { get; set; }

    [SettingsEntry(Symbol.LineThickness, AppSettingsResources.NovelSettingsFontWeightEntry.Header, AppSettingsResources.NovelSettingsFontWeightEntry.Description, AppSettingsResources.NovelSettingsFontWeightEntry.Placeholder)]
    public FontWeight NovelFontWeight { get; set; } = FontWeight.Normal;

    [SettingsEntry(Symbol.TextFont, AppSettingsResources.NovelSettingsFontFamilyEntry.Header, AppSettingsResources.AppFontFamilyEntry.Description, AppSettingsResources.AppFontFamilyEntry.Placeholder)]
    public ObservableCollection<string> NovelFontFamily { get; set; } = [];

    [SettingsEntry(Symbol.TextFontSize, AppSettingsResources.NovelSettingsFontSizeEntry.Header, AppSettingsResources.NovelSettingsFontSizeEntry.Description)]
    public int NovelFontSize { get; set; } = 14;

    [SettingsEntry(Symbol.TextLineSpacing, AppSettingsResources.NovelSettingsLineHeightEntry.Header, AppSettingsResources.NovelSettingsLineHeightEntry.Description)]
    public int NovelLineHeight { get; set; } = 28;

    [SettingsEntry(Symbol.AutoFitWidth, AppSettingsResources.NovelSettingsMaxWidthEntry.Header, AppSettingsResources.NovelSettingsMaxWidthEntry.Description)]
    public int NovelMaxWidth { get; set; } = 1000;
}

public class JsonColorConverter : JsonConverter<uint>
{
    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetUInt32();

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
