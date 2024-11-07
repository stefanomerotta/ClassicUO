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

using System.Globalization;
using System.Runtime.CompilerServices;

namespace ClassicUO.Core;

public readonly struct Serial : IComparable<Serial>, IComparable<uint>, IEquatable<Serial>
{
    public const uint ITEM_OFFSET = 0x40000000;
    public const uint VIRTUAL_OFFSET = 0x80000000;
    public const uint MAX_ITEM_SERIAL = VIRTUAL_OFFSET - 1;
    public const uint MAX_MOBILE_SERIAL = ITEM_OFFSET - 1;

    public static readonly Serial MaxMobileSerial = new Serial(MAX_MOBILE_SERIAL);

    public static readonly Serial MinusOne = new(0xFFFFFFFF);
    public static readonly Serial Zero = new(0);

    public uint Value { get; }

    public bool IsEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsValid && !IsVirtual;
    }

    public bool IsVirtual
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Value & ~MAX_ITEM_SERIAL) != 0;
    }

    public bool IsMobile
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value > 0 && Value < ITEM_OFFSET;
    }

    public bool IsItem
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= ITEM_OFFSET && Value < MAX_ITEM_SERIAL;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value > 0;
    }

    public Serial(uint serial)
    {
        Value = serial;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => Value.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Serial other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(uint other) => Value.CompareTo(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            Serial serial => this == serial,
            uint u => Value == u,
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Serial l, Serial r) => l.Value == r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Serial l, uint r) => l.Value == r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Serial l, Serial r) => l.Value != r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Serial l, uint r) => l.Value != r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Serial l, Serial r) => l.Value > r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Serial l, uint r) => l.Value > r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Serial l, Serial r) => l.Value < r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Serial l, uint r) => l.Value < r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Serial l, Serial r) => l.Value >= r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Serial l, uint r) => l.Value >= r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Serial l, Serial r) => l.Value <= r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Serial l, uint r) => l.Value <= r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator +(Serial l, Serial r) => (Serial)(l.Value + r.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator +(Serial l, uint r) => (Serial)(l.Value + r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator ++(Serial l) => (Serial)(l.Value + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator -(Serial l, Serial r) => (Serial)(l.Value - r.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator -(Serial l, uint r) => (Serial)(l.Value - r);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Serial operator --(Serial l) => (Serial)(l.Value - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"0x{Value:X8}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(Serial a) => a.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Serial(uint a) => new(a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Serial other) => Value == other.Value;

    public Serial ToVirtual() => new(Value | VIRTUAL_OFFSET);
    public Serial ToEntity() => new(Value & MAX_ITEM_SERIAL);

    public static Serial Parse(ReadOnlySpan<char> span)
    {
        return new(uint.Parse(span[2..], NumberStyles.HexNumber));
    }

    public static bool TryParse(ReadOnlySpan<char> span, out Serial result)
    {
        if (uint.TryParse(span[2..], NumberStyles.HexNumber, null, out uint raw))
        {
            result = new(raw);
            return true;
        }

        result = MinusOne;
        return false;
    }
}
