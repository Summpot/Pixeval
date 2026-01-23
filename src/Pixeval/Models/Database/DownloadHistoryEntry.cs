// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Diagnostics.CodeAnalysis;
using Misaki;
using Pixeval.Download;
using SQLite;

namespace Pixeval.Models.Database;

public class DownloadHistoryEntry : HistoryEntry
{
    public DownloadHistoryEntry(string destination, DownloadItemType type, IArtworkInfo entry)
    {
        if (entry is not ISerializable serializable)
            throw new InvalidCastException($"{nameof(entry)} should be {nameof(ISerializable)}");

        Destination = destination;
        Type = type;
        Entry = entry;
        SerializeKey = serializable.SerializeKey;
        EntryString = serializable.Serialize();
    }

    public DownloadHistoryEntry()
    {
    }

    /// <summary>
    /// 此属性不会变为<see cref="DownloadState.Running"/>或<see cref="DownloadState.Paused"/>或<see cref="DownloadState.Pending"/>（而是由外部ViewModel使用）
    /// </summary>
    public DownloadState State { get; set; }

    /// <summary>
    /// 表示文件所在的地址，可能无法被直接解析，且不能包含未解析的宏@{...}，<br/>
    /// 当是一个文件时必须是一个有效的地址（不能是token&lt;...&gt;）<br/>
    /// 当是多个文件时，文件名可以包含token&lt;...&gt;，但其文件夹路径不能包含token&lt;...&gt;
    /// </summary>
    // private set 反序列化使用
    public string Destination { get; private set; } = null!;

    public DownloadItemType Type { get; set; }

    [Ignore]
    [field: AllowNull, MaybeNull]
    public IArtworkInfo Entry => field ??= (IArtworkInfo) ArtworkSerializerTable.ArtworkTypeMethodsTable[SerializeKey](EntryString);

    // private set 反序列化使用
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public string SerializeKey { get; private set; } = null!;

    // private set 反序列化使用
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    public string EntryString { get; private set; } = null!;
}
