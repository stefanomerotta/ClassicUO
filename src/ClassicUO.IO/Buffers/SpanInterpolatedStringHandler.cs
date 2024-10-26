using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ClassicUO.IO.Buffers;

#nullable enable

[InterpolatedStringHandler]
public ref struct SpanInterpolatedStringHandler
{
    private static readonly char[] buffer = GC.AllocateUninitializedArray<char>(0x2000);

    [SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "mutable struct")]
    private MemoryExtensions.TryWriteInterpolatedStringHandler handler;

    public readonly ReadOnlySpan<char> Span => buffer.AsSpan(0, HandlerFields.GetHandlerBufferPosition(in handler));
    public readonly bool Success => HandlerFields.GetHandlerSuccessStatus(in handler);

    public SpanInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
    {
        handler = new(literalLength, formattedCount, buffer.AsSpan(), out isEnabled);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendLiteral(string value)
        => handler.AppendLiteral(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted<T>(T value)
        => handler.AppendFormatted(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted<T>(T value, string? format)
        => handler.AppendFormatted(value, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted<T>(T value, int alignment)
        => handler.AppendFormatted(value, alignment);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted<T>(T value, int alignment, string? format)
        => handler.AppendFormatted(value, alignment, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted(scoped ReadOnlySpan<char> value)
        => handler.AppendFormatted(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
        => handler.AppendFormatted(value, alignment, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted(string? value)
        => handler.AppendFormatted(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted(string? value, int alignment = 0, string? format = null)
        => handler.AppendFormatted(value, alignment, format);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AppendFormatted(object? value, int alignment = 0, string? format = null)
        => handler.AppendFormatted(value, alignment, format);
}

static file class HandlerFields
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pos")]
    public static extern ref int GetHandlerBufferPosition(in MemoryExtensions.TryWriteInterpolatedStringHandler @this);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_success")]
    public static extern ref bool GetHandlerSuccessStatus(in MemoryExtensions.TryWriteInterpolatedStringHandler @this);
}
