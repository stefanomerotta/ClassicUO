﻿// Copyright (c) 2024, andreakarasho
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

using ClassicUO.IO.Encoders;
using System;
using System.Buffers.Binary;

namespace ClassicUO.Modules.Gumps;

#nullable enable

public readonly ref struct GumpTextArray
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly ReadOnlySpan<Range> _ranges;

    public string this[int index] => UnicodeBE.GetString(_buffer[_ranges[index]]);
    public int Length => _ranges.Length;

    public GumpTextArray(ReadOnlySpan<byte> buffer, Span<Range> ranges)
    {
        _buffer = buffer;
        _ranges = ranges;

        int position = 0;

        for (int i = 0; i < ranges.Length; i++)
        {
            if (position >= buffer.Length - 1)
                break;

            int length = BinaryPrimitives.ReadUInt16BigEndian(buffer[position..]);
            length <<= UnicodeBE.ByteShift;
            position += 2;

            ReadOnlySpan<byte> bytes = buffer.Slice(position, length);

            int terminationIndex = UnicodeBE.GetNullTerminatorIndex(bytes);
            if (terminationIndex >= 0)
                length = terminationIndex;

            ranges[i] = position..(position + length);
            position += length;
        }
    }
}