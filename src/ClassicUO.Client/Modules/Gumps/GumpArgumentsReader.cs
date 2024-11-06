// Copyright (c) 2024, andreakarasho
// All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without
//  modification, are permitted provided that the following conditions are met:
//  1. Redistributions of source code must retain the above copyright
//     notice, this list of conditions and the following disclaimer.
//  2. Redistributions in binary form must reproduce the above copyright
//     notice, this list of conditions and the following disclaimer in the
//     documentation and/or other materials provided with the distribution.
//  3. All advertising materials mentioning features or use of this software
//     must display the following acknowledgement:
//     This product includes software developed by andreakarasho - https://github.com/andreakarasho
//  4. Neither the name of the copyright holder nor the
//     names of its contributors may be used to endorse or promote products
//     derived from this software without specific prior written permission.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
//  EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//  DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
//  DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES

using ClassicUO.Core;
using ClassicUO.IO.Encoders;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ClassicUO.Modules.Gumps;

#nullable enable

public ref struct GumpArgumentsReader
{
    private const byte SPACE = (byte)' ';
    private const byte TAG = (byte)'@';

    private static readonly byte[] hexPrefix = "0x"u8.ToArray();
    private static readonly byte[] hueAttr = "hue="u8.ToArray();
    private static readonly byte[] virtueClassAttr = "class=VirtueGumpItem"u8.ToArray();

    private ReadOnlySpan<byte> _buffer;

    public GumpArgumentsReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
    }

    public ReadOnlySpan<byte> ReadSpan()
    {
        ReadOnlySpan<byte> span = ReadOptionalSpan();
        if (span.IsEmpty)
            throw new Exception();

        return span;
    }

    public ReadOnlySpan<byte> ReadOptionalSpan()
    {
        int index = _buffer.IndexOfAnyExcept(SPACE);
        if (index < 0)
            return [];

        _buffer = _buffer[index..];

        index = _buffer.IndexOf(SPACE);

        ReadOnlySpan<byte> toRet;

        if (index < 0)
        {
            toRet = _buffer;
            _buffer = [];
        }
        else
        {
            toRet = _buffer[..index];
            _buffer = _buffer[(index + 1)..];
        }

        return toRet;
    }

    public T ReadInteger<T>() where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<byte> chunk = ReadSpan();
        return ParseInteger<T>(chunk);
    }

    public T ReadOptionalInteger<T>(T @default = default) where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<byte> chunk = ReadOptionalSpan();
        if (chunk.IsEmpty)
            return @default;

        return ParseInteger<T>(chunk);
    }

    public unsafe T ReadEnum<T>() where T : unmanaged, Enum
    {
        ReadOnlySpan<byte> chunk = ReadSpan();

        long value = long.Parse(chunk);
        T @enum = *(T*)&value;

        if (Enum.IsDefined(@enum))
            return @enum;

        return default;
    }

    public bool TryReadHueAttribute<T>(out T value) where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<byte> chunk = ReadOptionalSpan();
        if (chunk.IsEmpty)
        {
            value = default;
            return false;
        }

        if (!chunk.StartsWith(hueAttr))
            throw new ArgumentException($"Invalid syntax for gump attribute: {ASCIICP1215.GetString(chunk)}");

        chunk = chunk[hueAttr.Length..];
        value = ParseInteger<T>(chunk);

        return true;
    }

    public bool ReadBool()
    {
        ReadOnlySpan<byte> chunk = ReadSpan();
        Debug.Assert(chunk is { Length: 1 } val && val[0] is (byte)'1' or (byte)'0');
        return chunk.SequenceEqual([(byte)'1']);
    }

    public int ReadCliloc()
    {
        ReadOnlySpan<byte> chunk = ReadSpan();

        if (chunk[0] == (byte)'#')
            return int.Parse(chunk[1..], null);

        return int.Parse(chunk, null);
    }

    public Serial ReadSerial()
    {
        ReadOnlySpan<byte> chunk = ReadSpan();
        return new(ParseInteger<uint>(chunk));
    }

    public bool ReadVirtueClassAttribute()
    {
        ReadOnlySpan<byte> chunk = ReadOptionalSpan();
        if (chunk.IsEmpty)
            return false;

        return chunk.SequenceEqual(virtueClassAttr);
    }

    public ReadOnlySpan<byte> ReadArguments()
    {
        ReadOnlySpan<byte> rest = _buffer.Trim(SPACE);

        if (rest is not [TAG, .., TAG])
            throw new Exception();

        _buffer = [];

        return rest[1..^1];
    }

    private static T ParseInteger<T>(ReadOnlySpan<byte> chunk) where T : unmanaged, IBinaryInteger<T>
    {
        if (!chunk.StartsWith(hexPrefix))
            return T.Parse(chunk, null);

        chunk = chunk[2..];
        return T.Parse(chunk, NumberStyles.HexNumber, null);
    }
}
