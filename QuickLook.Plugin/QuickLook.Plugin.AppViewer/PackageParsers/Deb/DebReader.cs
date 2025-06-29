﻿// Copyright © 2017-2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using PureSharpCompress.Archives;
using PureSharpCompress.Compressors;
using PureSharpCompress.Compressors.BZip2;
using PureSharpCompress.Compressors.Deflate;
using PureSharpCompress.Compressors.Deflate64;
using PureSharpCompress.Compressors.LZMA;
using PureSharpCompress.Compressors.Xz;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuickLook.Plugin.AppViewer.PackageParsers.Deb;

public class DebReader
{
    public ArEntry[] ArEntries { get; set; } = [];

    public string Control { get; set; }

    public Dictionary<string, string> ControlDict { get; set; } = [];

    public DebReader(string path)
    {
        Open(path);
    }

    public void Open(string path)
    {
        ArEntry[] ar = ArReader.Read(path);
        var controlEntry = ar.Where(entry => entry.FileName.StartsWith("control.tar"))
            .FirstOrDefault();

        if (controlEntry != null)
        {
            ZipCompressionMethod method = GetCompressionMethodFromFileName(controlEntry.FileName);
            string control = ExtractControl(controlEntry.Data, method);

            if (!string.IsNullOrWhiteSpace(control))
            {
                Control = control;

                TextReader reader = new StringReader(control);
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (line.StartsWith(" ") || line.IndexOf(':') == -1)
                            continue;

                        var split = line.Split([':'], 2);
                        ControlDict.Add(split[0], split[1].Trim());
                    }
                }
            }
        }
    }

    private static string ExtractControl(byte[] data, ZipCompressionMethod method)
    {
        using var stream = new MemoryStream(data);
        using var decompressedTar = new MemoryStream();

        using (var decompressor = CreateDecompressionStream(stream, method))
        {
            decompressor?.CopyTo(decompressedTar);
        }

        decompressedTar.Position = 0;

        using var archive = ArchiveFactory.Open(decompressedTar);
        foreach (var entry in archive.Entries)
        {
            if (!entry.IsDirectory)
            {
                if (entry.Key == "./control")
                {
                    using var reader = new StreamReader(entry.OpenEntryStream());
                    string content = reader.ReadToEnd();

                    return content;
                }
            }
        }

        return null;
    }

    private static ZipCompressionMethod GetCompressionMethodFromFileName(string fileName)
    {
        fileName = fileName.ToLowerInvariant();
        if (fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tgz"))
            return ZipCompressionMethod.Deflate;
        else if (fileName.EndsWith(".tar.xz"))
            return ZipCompressionMethod.Xz;
        else if (fileName.EndsWith(".tar.bz2"))
            return ZipCompressionMethod.BZip2;
        else if (fileName.EndsWith(".tar.lzma"))
            return ZipCompressionMethod.LZMA;
        else if (fileName.EndsWith(".tar.zst"))
            return ZipCompressionMethod.ZStd;

        return ZipCompressionMethod.None;
    }

    private static Stream CreateDecompressionStream(Stream stream, ZipCompressionMethod method)
    {
        switch (method)
        {
            case ZipCompressionMethod.Deflate:
                {
                    return new DeflateStream(stream, CompressionMode.Decompress);
                }
            case ZipCompressionMethod.Deflate64:
                {
                    return new Deflate64Stream(stream, CompressionMode.Decompress);
                }
            case ZipCompressionMethod.BZip2:
                {
                    return new BZip2Stream(stream, CompressionMode.Decompress, false);
                }
            case ZipCompressionMethod.LZMA:
                {
                    var reader = new BinaryReader(stream);
                    reader.ReadUInt16(); //LZMA version
                    var props = new byte[reader.ReadUInt16()];
                    reader.Read(props, 0, props.Length);
                    return new LzmaStream(
                        props,
                        stream,
                        stream.Length - 4,
                        -1
                    );
                }
            case ZipCompressionMethod.Xz:
                {
                    return new XZStream(stream);
                }
            case ZipCompressionMethod.ZStd:
                {
                    // Not supported for ZStd
                    return default;
                }
        }

        return stream;
    }

    private enum ZipCompressionMethod
    {
        None = 0,
        Shrink = 1,
        Reduce1 = 2,
        Reduce2 = 3,
        Reduce3 = 4,
        Reduce4 = 5,
        Explode = 6,
        Deflate = 8,
        Deflate64 = 9,
        BZip2 = 12,
        LZMA = 14,
        ZStd = 93,
        Xz = 95,
        PPMd = 98,
        WinzipAes = 0x63,
    }
}
