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

using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;

namespace ClassicUO.Network.Packets;

#nullable enable
internal sealed class PacketLogger
{
    public static PacketLogger Default { get; set; } = new();

    private LogFile? _logFile;

    public bool Enabled { get; set; }

    public LogFile CreateFile()
    {
        _logFile?.Dispose();
        return _logFile = new LogFile(FileSystemHelper.CreateFolderIfNotExists(CUOEnviroment.ExecutablePath, "Logs", "Network"), "packets.log");
    }

    public void Log(ReadOnlySpan<byte> message, bool toServer)
    {
        if (!Enabled)
            return;

        Span<char> span = stackalloc char[256];
        ValueStringBuilder output = new(span);

        int off = sizeof(ulong) + 2;

        output.Append(' ', off);
        output.Append(string.Format("Ticks: {0} | {1} |  ID: {2:X2}   Length: {3}\n", Time.Ticks, toServer ? "Client -> Server" : "Server -> Client", message[0], message.Length));

        if (message[0] == 0x80 || message[0] == 0x91)
        {
            output.Append(' ', off);
            output.Append("[ACCOUNT CREDENTIALS HIDDEN]\n");
        }
        else
        {
            output.Append(' ', off);
            output.Append("0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F\n");

            output.Append(' ', off);
            output.Append("-- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --\n");

            ulong address = 0;

            for (int i = 0; i < message.Length; i += 16, address += 16)
            {
                output.Append($"{address:X8}");

                for (int j = 0; j < 16; ++j)
                {
                    if (j % 8 == 0)
                        output.Append(" ");

                    if (i + j < message.Length)
                        output.Append($" {message[i + j]:X2}");
                    else
                        output.Append("   ");
                }

                output.Append("  ");

                for (int j = 0; j < 16 && i + j < message.Length; j++)
                {
                    byte c = message[i + j];

                    if (c >= 0x20 && c < 0x80)
                        output.Append((char)c);
                    else
                        output.Append('.');
                }

                output.Append('\n');
            }
        }

        output.Append('\n');
        output.Append('\n');

        string s = output.ToString();

        if (_logFile is not null)
            _logFile.Write(s);
        else
            Console.WriteLine(s);

        output.Dispose();
    }
}
