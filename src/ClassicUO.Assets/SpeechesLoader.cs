#region license

// Copyright (c) 2024, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.IO;
using ClassicUO.Utility;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClassicUO.Assets;

#nullable enable

public sealed class SpeechesLoader : UOFileLoader
{
    private SpeechEntry[] _speech;

    public SpeechesLoader(UOFileManager fileManager)
        : base(fileManager)
    { }

    public override unsafe void Load()
    {
        string path = FileManager.GetUOFilePath("speech.mul");

        if (!File.Exists(path))
        {
            _speech = Array.Empty<SpeechEntry>();

            return;
        }

        var file = new UOFileMul(path);
        var entries = new List<SpeechEntry>();

        var buf = new byte[256];
        while (file.Position < file.Length)
        {
            file.Read(buf.AsSpan(0, sizeof(ushort) * 2));
            var id = BinaryPrimitives.ReadUInt16BigEndian(buf);
            var length = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(sizeof(ushort)));

            if (length > 0)
            {
                if (length > buf.Length)
                    buf = new byte[length];

                file.Read(buf.AsSpan(0, length));
                var text = string.Intern(Encoding.UTF8.GetString(buf.AsSpan(0, length)));

                entries.Add(new SpeechEntry(id, text));
            }
        }

        _speech = entries.ToArray();
        file.Dispose();
    }

    public static bool IsMatch(ReadOnlySpan<char> input, in SpeechEntry entry)
    {
        string[] split = entry.Keywords;

        for (int i = 0; i < split.Length; i++)
        {
            string value = split[i];
            int splitLength = value.Length;

            if (splitLength > input.Length || splitLength == 0)
                continue;

            if (!entry.CheckStart)
            {
                if (input[..splitLength].Contains(value, StringComparison.InvariantCultureIgnoreCase))
                    continue;
            }

            if (!entry.CheckEnd)
            {
                if (input[^splitLength..].Contains(value, StringComparison.InvariantCultureIgnoreCase))
                    continue;
            }

            int idx = input.IndexOf(value, StringComparison.InvariantCultureIgnoreCase);

            while (idx >= 0)
            {
                // "bank" or " bank" or "bank " or " bank " or "!bank" or "bank!"
                if ((idx - 1 < 0
                        || char.IsWhiteSpace(input[idx - 1])
                        || !char.IsLetter(input[idx - 1])
                    ) 
                    && 
                    (idx + splitLength >= input.Length
                        || char.IsWhiteSpace(input[idx + splitLength])
                        || !char.IsLetter(input[idx + splitLength])
                    ))
                {
                    return true;
                }

                idx = input[(idx + 1)..].IndexOf(value, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        return false;
    }

    public bool HasAnyKeyword(ReadOnlySpan<char> text)
    {
        if (FileManager.Version < ClientVersion.CV_305D)
            return false;

        text = text.Trim(' ');

        for (int i = 0; i < _speech.Length; i++)
        {
            ref SpeechEntry entry = ref _speech[i];

            if (IsMatch(text, in entry))
                return true;
        }

        return false;
    }

    public Span<SpeechEntry> GetKeywords(ReadOnlySpan<char> text)
    {
        if (FileManager.Version < ClientVersion.CV_305D)
            return [];

        List<SpeechEntry> list = [];
        text = text.Trim(' ');

        for (int i = 0; i < _speech.Length; i++)
        {
            ref SpeechEntry entry = ref _speech[i];

            if (IsMatch(text, in entry))
                list.Add(entry);
        }

        list.Sort();

        return CollectionsMarshal.AsSpan(list);
    }
}

public readonly struct SpeechEntry : IComparable<SpeechEntry>
{
    public SpeechEntry(int id, string keyword)
    {
        KeywordID = (short)id;

        Keywords = keyword.Split
        (
            new[]
            {
                '*'
            },
            StringSplitOptions.RemoveEmptyEntries
        );

        CheckStart = keyword.Length > 0 && keyword[0] == '*';
        CheckEnd = keyword.Length > 0 && keyword[keyword.Length - 1] == '*';
    }

    public string[] Keywords { get; }

    public short KeywordID { get; }

    public bool CheckStart { get; }

    public bool CheckEnd { get; }

    public int CompareTo(SpeechEntry obj)
    {
        if (KeywordID < obj.KeywordID)
        {
            return -1;
        }

        return KeywordID > obj.KeywordID ? 1 : 0;
    }
}