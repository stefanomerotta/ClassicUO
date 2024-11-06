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

using System;

namespace ClassicUO.Modules.Gumps;

#nullable enable
public ref struct GumpLayoutEnumerator
{
    private const byte OPEN = (byte)'{';
    private const byte CLOSE = (byte)'}';
    private const byte SPACE = (byte)' ';
    private const byte TAG = (byte)'@';

    private ReadOnlySpan<byte> _buffer;

    public ReadOnlySpan<byte> Current { get; private set; }

    public GumpLayoutEnumerator(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
    }

    public bool MoveNext()
    {
        int start = _buffer.IndexOf(OPEN);
        if (start == -1)
        {
            _buffer = [];
            return false;
        }

        _buffer = _buffer[start..];

        int end = _buffer.IndexOfAny(CLOSE, TAG);
        if (end == -1)
        {
            _buffer = [];
            return false;
        }

        if (_buffer[end] == TAG)
        {
            end++;

            int tagEnd = _buffer[end..].IndexOf(TAG);
            if (tagEnd == -1)
            {
                _buffer = [];
                return false;
            }

            end += tagEnd + 1;

            int close = _buffer[end..].IndexOf(CLOSE);
            if (close == -1)
            {
                _buffer = [];
                return false;
            }

            end += close;
        }

        Current = _buffer[1..end].Trim(SPACE);
        _buffer = _buffer[end..];
        return true;
    }

    public readonly GumpLayoutEnumerator GetEnumerator() => this;
}