// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using UniFFISharp.BoltFfi;

namespace Pixeval.Novel;

public partial union NovelNode
{
    internal static NovelNode Decode(WireReader reader) =>
        reader.ReadU32() switch
        {
            0 => new NovelNode(new NovelNode.Text(reader.ReadString())),
            1 => new NovelNode(new NovelNode.Ruby(reader.ReadString(), reader.ReadString())),
            2 => new NovelNode(new NovelNode.JumpUri(reader.ReadString(), reader.ReadString())),
            3 => new NovelNode(new NovelNode.JumpPage(reader.ReadU32())),
            4 => new NovelNode(new NovelNode.Chapter(reader.ReadString())),
            5 => new NovelNode(new NovelNode.UploadImage(reader.ReadI64())),
            6 => new NovelNode(new NovelNode.PixivImage(reader.ReadI64(), reader.ReadI32())),
            uint tag => throw new InvalidOperationException($"Invalid NovelNode tag: {tag}")
        };

    internal void Encode(WireWriter writer)
    {
        switch (this)
        {
            case NovelNode.Text t:
                writer.WriteU32(0);
                writer.WriteString(t.Content);
                break;
            case NovelNode.Ruby r:
                writer.WriteU32(1);
                writer.WriteString(r.Kanji);
                writer.WriteString(r.RubyText);
                break;
            case NovelNode.JumpUri u:
                writer.WriteU32(2);
                writer.WriteString(u.Title);
                writer.WriteString(u.Uri);
                break;
            case NovelNode.JumpPage p:
                writer.WriteU32(3);
                writer.WriteU32(p.Page);
                break;
            case NovelNode.Chapter c:
                writer.WriteU32(4);
                writer.WriteString(c.Title);
                break;
            case NovelNode.UploadImage ui:
                writer.WriteU32(5);
                writer.WriteI64(ui.ImageId);
                break;
            case NovelNode.PixivImage pi:
                writer.WriteU32(6);
                writer.WriteI64(pi.IllustId);
                writer.WriteI32(pi.Page);
                break;
        }
    }
}
