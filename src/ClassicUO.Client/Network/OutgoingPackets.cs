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

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.IO;
using ClassicUO.IO.Encoders;
using ClassicUO.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Network;

#nullable enable

internal static class OutgoingPackets
{
    private const int MAX_INT_STR_LEN = 19;

    public static void SendSeedOld(this NetClient socket, uint v)
    {
        using FixedSpanWriter writer = new(stackalloc byte[4]);
        writer.WriteUInt32BE(v);

        socket.Send(writer, true);
    }

    // 0x00
    public static void SendCreateCharacter(this NetClient socket, PlayerMobile character, int cityIndex, uint clientIP, uint slot, byte profession)
    {
        byte id = 0x00;
        ushort length = 104;
        int skillcount = 3;
        ClientVersion version = Client.Game.UO.Version;

        if (version >= ClientVersion.CV_70160)
        {
            id = 0xF8;
            length = 106;
            skillcount++;
        }

        using FixedSpanWriter writer = new(id, stackalloc byte[length]);
        writer.WriteUInt32BE(0xEDED_EDED);
        writer.WriteUInt32BE(0xFFFF_FFFF);
        writer.WriteUInt8(0x00);
        writer.WriteFixedString<ASCIICP1215>(character.Name, 30);
        writer.WriteZero(2);

        writer.WriteUInt32BE((uint)Client.Game.UO.Protocol);
        writer.WriteUInt32BE(0x01);
        writer.WriteUInt32BE(0x00);
        writer.WriteUInt8(profession);
        writer.WriteZero(15);

        byte val;

        if (version < ClientVersion.CV_4011D)
        {
            val = (byte)(character.Flags.HasFlag(Flags.Female) ? 0x01 : 0x00);
        }
        else
        {
            val = (byte)character.Race;

            if (version < ClientVersion.CV_7000)
                val--;

            val = (byte)(val * 2 + (byte)(character.Flags.HasFlag(Flags.Female) ? 0x01 : 0x00));
        }

        writer.WriteUInt8(val);
        writer.WriteUInt8((byte)character.Strength);
        writer.WriteUInt8((byte)character.Dexterity);
        writer.WriteUInt8((byte)character.Intelligence);

        IEnumerable<Skill> skills = character.Skills.OrderByDescending(o => o.Value).Take(skillcount);

        foreach (Skill skill in skills)
        {
            writer.WriteUInt8((byte)skill.Index);
            writer.WriteUInt8((byte)skill.ValueFixed);
        }

        writer.WriteUInt16BE(character.Hue);

        Item? hair = character.FindItemByLayer(Layer.Hair);

        if (hair is not null)
        {
            writer.WriteUInt16BE(hair.Graphic);
            writer.WriteUInt16BE(hair.Hue);
        }
        else
        {
            writer.WriteZero(2 * 2);
        }

        Item? beard = character.FindItemByLayer(Layer.Beard);

        if (beard is not null)
        {
            writer.WriteUInt16BE(beard.Graphic);
            writer.WriteUInt16BE(beard.Hue);
        }
        else
        {
            writer.WriteZero(2 * 2);
        }

        writer.WriteUInt16BE((ushort)cityIndex);
        writer.WriteZero(2);
        writer.WriteUInt16BE((ushort)slot);
        writer.WriteUInt32BE(clientIP);

        Item? shirt = character.FindItemByLayer(Layer.Shirt);

        if (shirt is not null)
            writer.WriteUInt16BE(shirt.Hue);
        else
            writer.WriteZero(2);

        Item? pants = character.FindItemByLayer(Layer.Pants);

        if (pants is not null)
            writer.WriteUInt16BE(pants.Hue);
        else
            writer.WriteZero(2);

        socket.Send(writer);
    }

    // 0x02
    public static void SendWalkRequest(this NetClient socket, Direction direction, byte seq, bool run, uint fastWalk)
    {
        using FixedSpanWriter writer = new(0x02, stackalloc byte[7]);

        if (run)
            direction |= Direction.Running;

        writer.WriteUInt8((byte)direction);
        writer.WriteUInt8(seq);
        writer.WriteUInt32BE(fastWalk);

        socket.Send(writer);
    }

    // 0x03
    public static void SendACKTalk(this NetClient socket)
    {
        socket.Send([
            0x03, // packet Id
            0x00, 0x28, // packet size
            0x20, // Speech type
            0x00, 0x34, // Color
            0x00, 0x03, // Speech font
            0xdb, 0x13, 0x14, 0x3f, 0x45, 0x2c, 0x58, 0x0f,
            0x5d, 0x44, 0x2e, 0x50, 0x11, 0xdf, 0x75, 0x5c,
            0xe0, 0x3e, 0x71, 0x4f, 0x31, 0x34, 0x05, 0x4e,
            0x18, 0x1e, 0x72, 0x0f, 0x59, 0xad, 0xf5, 0x00
        ]);
    }

    // 0x03
    public static void SendASCIISpeechRequest(this NetClient socket, string text, MessageType type, byte font, ushort hue)
    {
        using FixedSpanWriter writer = new(0x03, 8 + text.Length + 1, true);

        if (Client.Game.UO.FileManager.Speeches.HasAnyKeyword(text))
            type |= MessageType.Encoded;

        writer.WriteUInt8((byte)type);
        writer.WriteUInt16BE(hue);
        writer.WriteUInt16BE(font);
        writer.WriteString<ASCIICP1215>(text, StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x05
    public static void SendAttackRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x05, stackalloc byte[5]);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x06
    public static void SendDoubleClick(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x06, stackalloc byte[5]);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x07
    public static void SendPickUpRequest(this NetClient socket, uint serial, ushort count)
    {
        using FixedSpanWriter writer = new(0x07, stackalloc byte[7]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(count);

        socket.Send(writer);
    }

    // 0x08
    public static void SendDropRequestOld(this NetClient socket, uint serial, ushort x, ushort y, sbyte z, uint container)
    {
        using FixedSpanWriter writer = new(0x08, stackalloc byte[14]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(x);
        writer.WriteUInt16BE(y);
        writer.WriteInt8(z);
        writer.WriteUInt32BE(container);

        socket.Send(writer);
    }

    // 0x08
    public static void SendDropRequest(this NetClient socket, uint serial, ushort x, ushort y, sbyte z, byte slot, uint container)
    {
        using FixedSpanWriter writer = new(0x08, stackalloc byte[15]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(x);
        writer.WriteUInt16BE(y);
        writer.WriteInt8(z);
        writer.WriteUInt8(slot);
        writer.WriteUInt32BE(container);

        socket.Send(writer);
    }

    // 0x09
    public static void SendClickRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x09, stackalloc byte[5]);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x12
    public static void SendCastSpellFromBook(this NetClient socket, int idx, uint serial)
    {
        using FixedSpanWriter writer = new(0x12, stackalloc byte[21 + MAX_INT_STR_LEN * 2], true);
        writer.WriteUInt8(0x27);
        writer.WriteString<ASCIICP1215>($"{idx} {serial}", StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x12
    public static void SendUseSkill(this NetClient socket, int idx)
    {
        using FixedSpanWriter writer = new(0x12, stackalloc byte[14 + MAX_INT_STR_LEN], true);
        writer.WriteUInt8(0x24);
        writer.WriteString<ASCIICP1215>($"{idx} 0", StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x12
    public static void SendOpenDoor(this NetClient socket)
    {
        socket.Send([0x12, 0x00, 0x05, 0x58, 0x00]);
    }

    // 0x12
    public static void SendOpenSpellBook(this NetClient socket, byte type)
    {
        socket.Send([0x12, 0x00, 0x05, 0x43, type]);
    }

    // 0x12
    public static void SendEmoteAction(this NetClient socket, string action)
    {
        using FixedSpanWriter writer = new(0x12, 3 + action.Length + 1, true);
        writer.WriteUInt8(0xC7);
        writer.WriteString<ASCIICP1215>(action, StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x12
    public static void SendInvokeVirtueRequest(this NetClient socket, byte id)
    {
        using VariableSpanWriter writer = new(0x12, stackalloc byte[3 + 7], true);
        writer.WriteUInt8(0xF4);
        writer.WriteString<ASCIICP1215>(id.ToString(), StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x13
    public static void SendEquipRequest(this NetClient socket, uint serial, Layer layer, uint container)
    {
        using FixedSpanWriter writer = new(0x13, stackalloc byte[10]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8((byte)layer);
        writer.WriteUInt32BE(container);

        socket.Send(writer);
    }

    // 0x22
    public static void SendResync(this NetClient socket)
    {
        socket.Send([0x22, 0x00, 0x00]);
    }

    // 0x34
    public static void SendStatusRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x34, stackalloc byte[10]);
        writer.WriteUInt32BE(0xEDEDEDED);
        writer.WriteUInt8(0x04);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x34
    public static void SendSkillsRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x34, stackalloc byte[10]);
        writer.WriteUInt32BE(0xEDEDEDED);
        writer.WriteUInt8(0x05);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x3A
    public static void SendSkillsStatusRequest(this NetClient socket, ushort skillIndex, byte lockState)
    {
        using FixedSpanWriter writer = new(0x3A, stackalloc byte[6], true);
        writer.WriteUInt16BE(skillIndex);
        writer.WriteUInt8(lockState);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x3A
    public static void SendSkillStatusChangeRequest(this NetClient socket, ushort skillindex, byte lockstate)
    {
        using FixedSpanWriter writer = new(0x3A, stackalloc byte[3 + 3], true);
        writer.WriteUInt16BE(skillindex);
        writer.WriteUInt8(lockstate);

        writer.WritePacketLength();

        socket.Send(writer);
    }


    // 0x3B
    public static void SendBuyRequest(this NetClient socket, uint serial, ReadOnlySpan<(uint, ushort)> items)
    {
        using FixedSpanWriter writer = new(0x3B, 3 + 5 + 7 * items.Length, true);
        writer.WriteUInt32BE(serial);

        if (items.Length > 0)
        {
            writer.WriteUInt8(0x02);

            for (int i = 0; i < items.Length; i++)
            {
                writer.WriteUInt8(0x1A);
                writer.WriteUInt32BE(items[i].Item1);
                writer.WriteUInt16BE(items[i].Item2);
            }
        }
        else
        {
            writer.WriteUInt8(0x00);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x3F
    public static void SendUOLiveHashResponse(this NetClient socket, uint block, byte mapIndex, ReadOnlySpan<ushort> checksums)
    {
        using FixedSpanWriter writer = new(0x3F, 3 + 12 + 2 * checksums.Length, true);
        writer.WriteUInt32BE(block);
        writer.WriteZero(6);
        writer.WriteUInt8(0xFF);
        writer.WriteUInt8(mapIndex);

        for (int i = 0; i < checksums.Length; i++)
        {
            writer.WriteUInt16BE(checksums[i]);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x56
    public static void SendMapMessage(this NetClient socket, uint serial, byte action, byte pin, ushort x, ushort y)
    {
        using FixedSpanWriter writer = new(0x56, stackalloc byte[11]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8(action);
        writer.WriteUInt8(pin);
        writer.WriteUInt16BE(x);
        writer.WriteUInt16BE(y);

        socket.Send(writer);
    }

    // 0x5D
    public static void SendSelectCharacter(this NetClient socket, uint index, string name, uint ipclient)
    {
        using FixedSpanWriter writer = new(0x5D, stackalloc byte[73]);
        writer.WriteUInt32BE(0xEDEDEDED);
        writer.WriteFixedString<ASCIICP1215>(name, 30);
        writer.WriteZero(2);
        writer.WriteUInt32BE((uint)Client.Game.UO.Protocol);
        writer.WriteZero(24);
        writer.WriteUInt32BE(index);
        writer.WriteUInt32BE(ipclient);

        socket.Send(writer);
    }

    // 0x66
    public static void SendBookPageData(this NetClient socket, uint serial, string[] texts, int page)
    {
        int textsLength = 0;

        for (int i = 0; i < texts.Length; i++)
        {
            textsLength += texts[i].Length + 1; // include null termination
        }

        using FixedSpanWriter writer = new(0x66, 3 + 11 + textsLength, true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(0x01);
        writer.WriteUInt16BE((ushort)page);
        writer.WriteUInt16BE((ushort)texts.Length);

        for (int i = 0; i < texts.Length; i++)
        {
            ReadOnlySpan<char> text = texts[i];

            if (text.IsEmpty)
            {
                writer.WriteUInt8(0x00);
                continue;
            }

            foreach (Range range in text.Split('\n'))
            {
                writer.WriteString<UTF8>(text[range]);
            }

            writer.WriteUInt8(0x00);
        }

        writer.WriteUInt8(0x00);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x66
    public static void SendBookPageDataRequest(this NetClient socket, uint serial, ushort page)
    {
        using FixedSpanWriter writer = new(0x66, stackalloc byte[3 + 10], true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(0x01);
        writer.WriteUInt16BE(page);
        writer.WriteUInt16BE(0xFFFF);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x6C
    public static void SendTargetObject(this NetClient socket, uint entity, ushort graphic,
        ushort x, ushort y, sbyte z, uint cursorId, byte cursorType)
    {
        using FixedSpanWriter writer = new(0x6C, stackalloc byte[19]);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(cursorId);
        writer.WriteUInt8(cursorType);
        writer.WriteUInt32BE(entity);
        writer.WriteUInt16BE(x);
        writer.WriteUInt16BE(y);
        writer.WriteUInt16BE((ushort)z);
        writer.WriteUInt16BE(graphic);

        socket.Send(writer);
    }

    // 0x6C
    public static void SendTargetXYZ(this NetClient socket, ushort graphic, ushort x, ushort y, sbyte z, uint cursorID, byte cursorType)
    {
        using FixedSpanWriter writer = new(0x6C, stackalloc byte[19]);
        writer.WriteUInt8(0x01);
        writer.WriteUInt32BE(cursorID);
        writer.WriteUInt8(cursorType);
        writer.WriteUInt32BE(0x00);
        writer.WriteUInt16BE(x);
        writer.WriteUInt16BE(y);
        writer.WriteUInt16BE((ushort)z);
        writer.WriteUInt16BE(graphic);

        socket.Send(writer);
    }

    // 0x6C
    public static void SendTargetCancel(this NetClient socket, CursorTarget type, uint cursorId, byte cursorType)
    {
        using FixedSpanWriter writer = new(0x6C, stackalloc byte[19]);
        writer.WriteUInt8((byte)type);
        writer.WriteUInt32BE(cursorId);
        writer.WriteUInt8(cursorType);
        writer.WriteUInt32BE(0x00);
        writer.WriteUInt32BE(0xFFFF_FFFF);
        writer.WriteUInt32BE(0x0000_0000);

        socket.Send(writer);
    }

    // 0x6C
    public static void SendASCIIPromptResponse(this NetClient socket, World world, string text, bool cancel)
    {
        using FixedSpanWriter writer = new(0x6C, 3 + 12 + text.Length + 1, true);
        writer.WriteUInt64BE(world.MessageManager.PromptData.Data);
        writer.WriteUInt32BE((uint)(cancel ? 0 : 1));
        writer.WriteString<ASCIICP1215>(text, StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x6F
    public static void SendTradeResponse(this NetClient socket, uint serial, int code, bool state)
    {
        if (code == 1)
        {
            using FixedSpanWriter writer = new(0x6F, stackalloc byte[3 + 5], true);
            writer.WriteUInt8(0x01);
            writer.WriteUInt32BE(serial);

            writer.WritePacketLength();

            socket.Send(writer);
        }
        else if (code == 2)
        {
            using FixedSpanWriter writer = new(0x6F, stackalloc byte[3 + 9], true);
            writer.WriteUInt8(0x02);
            writer.WriteUInt32BE(serial);
            writer.WriteUInt32BE((uint)(state ? 1 : 0));

            writer.WritePacketLength();

            socket.Send(writer);
        }
    }

    // 0x6F
    public static void SendTradeUpdateGold(this NetClient socket, uint serial, uint gold, uint platinum)
    {
        using FixedSpanWriter writer = new(0x6F, stackalloc byte[3 + 13], true);
        writer.WriteUInt8(0x03);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(gold);
        writer.WriteUInt32BE(platinum);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x71
    public static void SendBulletinBoardRequestMessage(this NetClient socket, uint serial, uint msgSerial)
    {
        using FixedSpanWriter writer = new(0x71, stackalloc byte[3 + 9], true);
        writer.WriteUInt8(0x03);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(msgSerial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x71
    public static void SendBulletinBoardRequestMessageSummary(this NetClient socket, uint serial, uint msgSerial)
    {
        using FixedSpanWriter writer = new(0x71, stackalloc byte[3 + 9], true);
        writer.WriteUInt8(0x04);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(msgSerial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x71
    public static void SendBulletinBoardPostMessage(this NetClient socket, uint serial, uint msgSerial, string subject, string text)
    {
        using FixedSpanWriter writer = new(0x71, 3 + 14 + subject.Length * 2 + text.Length * 2, true);
        writer.WriteUInt8(0x05);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(msgSerial);

        if (subject == "")
        {
            writer.WriteUInt8(1);
        }
        else
        {
            subject = subject.Replace("\r\n", "\n");

            writer.WriteUInt8((byte)(subject.Length + 1));
            writer.WriteString<UTF8>(subject);
        }

        writer.WriteUInt8(0x00);

        if (text == "")
        {
            writer.WriteUInt8(0x01);
            writer.WriteUInt8(1);
            writer.WriteUInt8(0x00);
        }
        else
        {
            string[] lines = text.Split('\n');
            writer.WriteUInt8((byte)lines.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                ReadOnlySpan<char> line = lines[i];

                if (line[^1] == '\r')
                    line = line[..^2];

                writer.WriteString<UTF8>(line, StringOptions.PrependByteSize | StringOptions.NullTerminated);
            }
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x71
    public static void SendBulletinBoardRemoveMessage(this NetClient socket, uint serial, uint msgSerial)
    {
        using FixedSpanWriter writer = new(0x71, stackalloc byte[3 + 9], true);
        writer.WriteUInt8(0x06);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(msgSerial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x72
    public static void SendChangeWarMode(this NetClient socket, bool state)
    {
        using FixedSpanWriter writer = new(0x72, stackalloc byte[5]);
        writer.WriteBool(state);
        writer.WriteUInt8(0x00);
        writer.WriteUInt8(0x32);
        writer.WriteUInt8(0x00);

        socket.Send(writer);
    }

    // 0x73
    public static void SendPing(this NetClient socket, byte idx)
    {
        socket.Send([0x73, idx]);
    }

    // 0x75
    public static void SendRenameRequest(this NetClient socket, uint serial, string name)
    {
        using FixedSpanWriter writer = new(0x75, stackalloc byte[35]);
        writer.WriteUInt32BE(serial);
        writer.WriteFixedString<ASCIICP1215>(name, 30);

        socket.Send(writer);
    }

    // 0x7D
    public static void SendMenuResponse(this NetClient socket, uint serial, ushort graphic, int code, ushort itemGraphic, ushort itemHue)
    {
        using FixedSpanWriter writer = new(0x7D, stackalloc byte[3 + 15], true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(graphic);

        if (code != 0)
        {
            writer.WriteUInt16BE((ushort)code);
            writer.WriteUInt16BE(itemGraphic);
            writer.WriteUInt16BE(itemHue);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x7D
    public static void SendGrayMenuResponse(this NetClient socket, uint serial, ushort graphic, ushort code)
    {
        using FixedSpanWriter writer = new(0x7D, stackalloc byte[3 + 8], true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(graphic);
        writer.WriteUInt16BE(code);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0x80
    public static void SendFirstLogin(this NetClient socket, string user, string psw)
    {
        using FixedSpanWriter writer = new(0x80, stackalloc byte[62]);
        writer.WriteFixedString<ASCIICP1215>(user, 30);
        writer.WriteFixedString<ASCIICP1215>(psw, 30);
        writer.WriteUInt8(0xFF);

        socket.Send(writer);
    }

    // 0x83
    public static void SendDeleteCharacter(this NetClient socket, byte index, uint ipclient)
    {
        using FixedSpanWriter writer = new(0x83, stackalloc byte[39]);
        writer.WriteZero(30);
        writer.WriteUInt32BE(index);
        writer.WriteUInt32BE(ipclient);

        socket.Send(writer);
    }

    // 0x91
    public static void SendSecondLogin(this NetClient socket, string user, string psw, uint seed)
    {
        using FixedSpanWriter writer = new(0x91, stackalloc byte[65]);
        writer.WriteUInt32BE(seed);
        writer.WriteFixedString<ASCIICP1215>(user, 30);
        writer.WriteFixedString<ASCIICP1215>(psw, 30);

        socket.Send(writer);
    }

    // 0x93
    public static void SendBookHeaderChangedOld(this NetClient socket, uint serial, string title, string author)
    {
        using FixedSpanWriter writer = new(0x93, 99);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8(0x00);
        writer.WriteUInt8(0x01);
        writer.WriteUInt16BE(0);
        writer.WriteFixedString<UTF8>(title, 60);
        writer.WriteFixedString<UTF8>(author, 30);

        socket.Send(writer);
    }

    // 0x95
    public static void SendDyeDataResponse(this NetClient socket, uint serial, ushort graphic, ushort hue)
    {
        using FixedSpanWriter writer = new(0x95, stackalloc byte[9]);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(0);
        writer.WriteUInt16BE(hue);

        socket.Send(writer);
    }

    // 0x98
    public static void SendNameRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0x98, stackalloc byte[5]);
        writer.WriteUInt32BE(serial);

        socket.Send(writer);
    }

    // 0x9B
    public static void SendHelpRequest(this NetClient socket)
    {
        using FixedSpanWriter writer = new(0x9B, 258);
        writer.WriteZero(257);

        socket.Send(writer);
    }

    // 0x9F
    public static void SendSellRequest(this NetClient socket, uint serial, ReadOnlySpan<(uint, ushort)> items)
    {
        using FixedSpanWriter writer = new(0x9F, 3 + 6 + 6 * items.Length, true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE((ushort)items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            writer.WriteUInt32BE(items[i].Item1);
            writer.WriteUInt16BE(items[i].Item2);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xA0
    public static void SendSelectServer(this NetClient socket, byte index)
    {
        socket.Send([0xA0, 0x00, index]);
    }

    // 0xA7
    public static void SendTipRequest(this NetClient socket, ushort id, byte flag)
    {
        using FixedSpanWriter writer = new(0xA7, stackalloc byte[4]);
        writer.WriteUInt16BE(id);
        writer.WriteUInt8(flag);

        socket.Send(writer);
    }

    // 0xAC
    public static void SendTextEntryDialogResponse(this NetClient socket, uint serial, byte parentId, byte button, string text, bool code)
    {
        using FixedSpanWriter writer = new(0xAC, 3 + 9 + text.Length + 1, true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8(parentId);
        writer.WriteUInt8(button);
        writer.WriteBool(code);
        writer.WriteUInt16BE((ushort)(text.Length + 1));
        writer.WriteFixedString<ASCIICP1215>(text, text.Length + 1);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xAD
    public static void SendUnicodeSpeechRequest(this NetClient socket, string text, MessageType type, byte font, ushort hue, string lang)
    {
        using VariableSpanWriter writer = new(0xAD, 5 + text.Length * 2, true);

        Span<SpeechEntry> keywords = Client.Game.UO.FileManager.Speeches.GetKeywords(text);

        bool encoded = !keywords.IsEmpty;
        if (encoded)
            type |= MessageType.Encoded;

        writer.WriteUInt8((byte)type);
        writer.WriteUInt16BE(hue);
        writer.WriteUInt16BE(font);
        writer.WriteFixedString<ASCIICP1215>(lang, 4);

        if (encoded)
        {
            int length = keywords.Length;
            Span<byte> codeBytes = stackalloc byte[(length + 1) * 2];
            int codeBytesLength = 0;

            codeBytes[codeBytesLength++] = (byte)(length >> 4);

            int num3 = length & 15;
            bool flag = false;

            for (int index = 0; index < length; index++)
            {
                int keywordId = keywords[index].KeywordID;

                if (flag)
                {
                    codeBytes[codeBytesLength++] = (byte)(keywordId >> 4);
                    num3 = keywordId & 15;
                }
                else
                {
                    codeBytes[codeBytesLength++] = (byte)((num3 << 4) | ((keywordId >> 8) & 15));
                    codeBytes[codeBytesLength++] = (byte)keywordId;
                }

                flag = !flag;
            }

            if (!flag)
                codeBytes[codeBytesLength++] = (byte)(num3 << 4);

            writer.Write(codeBytes[..codeBytesLength]);
            writer.WriteString<UTF8>(text);
            writer.WriteUInt8(0x00);
        }
        else
        {
            writer.WriteString<UnicodeBE>(text);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB1
    public static void SendGumpResponse(this NetClient socket, uint local, uint server, int button,
        ReadOnlySpan<uint> switches, ReadOnlySpan<(ushort, string)> entries)
    {
        using VariableSpanWriter writer = new(0xB1, stackalloc byte[3 + 20 + switches.Length * 4], true);
        writer.WriteUInt32BE(local);
        writer.WriteUInt32BE(server);
        writer.WriteUInt32BE((uint)button);
        writer.WriteUInt32BE((uint)switches.Length);

        for (int i = 0; i < switches.Length; i++)
        {
            writer.WriteUInt32BE(switches[i]);
        }

        writer.WriteUInt32BE((uint)entries.Length);

        for (int i = 0; i < entries.Length; i++)
        {
            (ushort id, string str) = entries[i];

            writer.WriteUInt16BE(id);

            ReadOnlySpan<char> text = str;
            if (text.Length > 239)
                text = text[..239];

            writer.WriteString<UnicodeBE>(text, StringOptions.PrependByteSize);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB1
    public static void SendVirtueGumpResponse(this NetClient socket, uint serial, uint code)
    {
        using FixedSpanWriter writer = new(0xB1, stackalloc byte[3 + 12], true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(0x000001CD);
        writer.WriteUInt32BE(code);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB3
    public static void SendChatJoinCommand(this NetClient socket, string name, string? password = null)
    {
        using FixedSpanWriter writer = new(0xB3, 3 + 8 + name.Length * 2 + (password?.Length ?? 0) * 2, true);
        writer.WriteFixedString<ASCIICP1215>(Settings.GlobalSettings.Language, 4);
        writer.WriteUInt16BE(0x62);
        writer.WriteUInt16BE(0x22);
        writer.WriteString<UnicodeBE>(name);
        writer.WriteUInt16BE(0x22);
        writer.WriteUInt16BE(0x020);

        if (!string.IsNullOrEmpty(password))
            writer.WriteString<UnicodeBE>(password);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB3
    public static void SendChatCreateChannelCommand(this NetClient socket, string name, string? password = null)
    {
        using FixedSpanWriter writer = new(0xB3, 3 + 10 + name.Length * 2 + (password?.Length ?? 0) * 2, true);
        writer.WriteFixedString<ASCIICP1215>(Settings.GlobalSettings.Language, 4);
        writer.WriteUInt16BE(0x63);
        writer.WriteString<UnicodeBE>(name);

        if (!string.IsNullOrEmpty(password))
        {
            writer.WriteUInt16BE(0x7B);
            writer.WriteString<UnicodeBE>(password);
            writer.WriteUInt16BE(0x07D);
        }

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB3
    public static void SendChatLeaveChannelCommand(this NetClient socket)
    {
        using FixedSpanWriter writer = new(0xB3, stackalloc byte[3 + 6], true);
        writer.WriteFixedString<ASCIICP1215>(Settings.GlobalSettings.Language, 4);
        writer.WriteUInt16BE(0x43);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB3
    public static void SendChatMessageCommand(this NetClient socket, string msg)
    {
        using FixedSpanWriter writer = new(0xB3, 3 + 6 + msg.Length * 2, true);
        writer.WriteFixedString<ASCIICP1215>(Settings.GlobalSettings.Language, 4);
        writer.WriteUInt16BE(0x61);
        writer.WriteString<UnicodeBE>(msg);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB5
    public static void SendOpenChat(this NetClient socket, string name)
    {
        using FixedSpanWriter writer = new(0xB5, stackalloc byte[64]);
        writer.WriteUInt8(0x00);
        writer.WriteFixedString<UnicodeBE>(name, 62);

        socket.Send(writer);
    }

    // 0xB8
    public static void SendProfileRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xB8, stackalloc byte[3 + 5], true);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xB8
    public static void SendProfileUpdate(this NetClient socket, uint serial, string text)
    {
        using FixedSpanWriter writer = new(0xB8, 3 + 9 + text.Length * 2, true);
        writer.WriteUInt8(0x01);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(0x01);
        writer.WriteString<UnicodeBE>(text, StringOptions.PrependByteSize);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBD
    public static void SendClientVersion(this NetClient socket, string version)
    {
        using FixedSpanWriter writer = new(0xBD, stackalloc byte[3 + version.Length + 1], true);
        writer.WriteString<ASCIICP1215>(version, StringOptions.NullTerminated);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendCastSpell(this NetClient socket, int idx)
    {
        if (socket.ClientVersion >= ClientVersion.CV_60142)
        {
            using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
            writer.WriteUInt16BE(0x1C);
            writer.WriteUInt16BE(0x02);
            writer.WriteUInt16BE((ushort)idx);

            writer.WritePacketLength();

            socket.Send(writer);
        }
        else
        {
            using VariableSpanWriter writer = new(0x12, stackalloc byte[3 + 6], true);
            writer.WriteUInt8(0x56);
            writer.WriteString<ASCIICP1215>($"{idx}", StringOptions.NullTerminated);

            writer.WritePacketLength();

            socket.Send(writer);
        }
    }

    // 0xBF
    public static void SendClickQuestArrow(this NetClient socket, bool righClick)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 3], true);
        writer.WriteUInt16BE(0x07);
        writer.WriteBool(righClick);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendCloseStatusBarGump(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
        writer.WriteUInt16BE(0x0C);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPartyInviteRequest(this NetClient socket)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 7], true);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(1);
        writer.WriteUInt32BE(0);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPartyRemoveRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 7], true);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(2);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPartyChangeLootTypeRequest(this NetClient socket, bool type)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 4], true);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(0x06);
        writer.WriteBool(type);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPartyAccept(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 7], true);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(0x08);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void Send_PartyDecline(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 7], true);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(0x09);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPartyMessage(this NetClient socket, string text, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, 3 + 7 + text.Length * 2, true);
        writer.WriteUInt16BE(0x06);

        if (SerialHelper.IsValid(serial))
        {
            writer.WriteUInt8(0x03);
            writer.WriteUInt32BE(serial);
        }
        else
        {
            writer.WriteUInt8(0x04);
        }

        writer.WriteString<UnicodeBE>(text);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendGameWindowSize(this NetClient socket, uint w, uint h)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 10], true);
        writer.WriteUInt16BE(0x05);
        writer.WriteUInt32BE(w);
        writer.WriteUInt32BE(h);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendLanguage(this NetClient socket, string lang)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
        writer.WriteUInt16BE(0x0B);
        writer.WriteFixedString<ASCIICP1215>(lang, 3);
        writer.WriteUInt8(0x00);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendClientType(this NetClient socket)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 7], true);
        writer.WriteUInt16BE(0x0F);
        writer.WriteUInt8(0x0A);
        writer.WriteUInt32BE((uint)Client.Game.UO.Protocol);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendRequestPopupMenu(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
        writer.WriteUInt16BE(0x13);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendPopupMenuSelection(this NetClient socket, uint serial, ushort menuid)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 8], true);
        writer.WriteUInt16BE(0x15);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt16BE(menuid);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendMegaClilocRequestOld(this NetClient socket, uint serial)
    {
        using VariableSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
        writer.WriteUInt16BE(0x10);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendStatLockStateRequest(this NetClient socket, byte stat, Lock state)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 4], true);
        writer.WriteUInt16BE(0x1A);
        writer.WriteUInt8(stat);
        writer.WriteUInt8((byte)state);

        writer.WritePacketLength();

        socket.Send(writer);
    }


    // 0xBF
    public static void SendTargetSelectedObject(this NetClient socket, uint serial, uint targetSerial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 10], true);
        writer.WriteUInt16BE(0x2C);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt32BE(targetSerial);

        writer.WritePacketLength();

        socket.Send(writer.Buffer);
    }

    // 0xBF
    public static void SendToggleGargoyleFlying(this NetClient socket)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 8], true);
        writer.WriteUInt16BE(0x32);
        writer.WriteUInt16BE(0x01);
        writer.WriteUInt32BE(0);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendCustomHouseDataRequest(this NetClient socket, uint serial)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 6], true);
        writer.WriteUInt16BE(0x1E);
        writer.WriteUInt32BE(serial);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendStunRequest(this NetClient socket)
    {
        socket.Send([0xBF, 0x00, 0x05, 0x00, 0x09]);
    }

    // 0xBF
    public static void SendDisarmRequest(this NetClient socket)
    {
        socket.Send([0xBF, 0x00, 0x05, 0x00, 0x0A]);
    }

    // 0xBF
    public static void SendChangeRaceRequest(this NetClient socket, ushort skinHue,
        ushort hairStyle, ushort hairHue, ushort beardStyle, ushort beardHue)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 12], true);
        writer.WriteUInt16BE(0x2A);
        writer.WriteUInt16BE(skinHue);
        writer.WriteUInt16BE(hairStyle);
        writer.WriteUInt16BE(hairHue);
        writer.WriteUInt16BE(beardStyle);
        writer.WriteUInt16BE(beardHue);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xBF
    public static void SendMultiBoatMoveRequest(this NetClient socket, uint serial, Direction dir, byte speed)
    {
        using FixedSpanWriter writer = new(0xBF, stackalloc byte[3 + 9], true);
        writer.WriteUInt16BE(0x33);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8((byte)dir);
        writer.WriteUInt8((byte)dir);
        writer.WriteUInt8(speed);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xC2
    public static void SendUnicodePromptResponse(this NetClient socket, World world, string text, string lang, bool cancel)
    {
        using FixedSpanWriter writer = new(0xC2, 3 + 16 + text.Length * 2, true);
        writer.WriteUInt64BE(world.MessageManager.PromptData.Data);
        writer.WriteUInt32BE((uint)(cancel ? 0 : 1));
        writer.WriteFixedString<ASCIICP1215>(lang, 3);
        writer.WriteUInt8(0x00);
        writer.WriteString<UnicodeLE>(text);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xC8
    public static void SendClientViewRange(this NetClient socket, byte range)
    {
        socket.Send([0xC8, Math.Clamp(range, (byte)Constants.MIN_VIEW_RANGE, (byte)Constants.MAX_VIEW_RANGE)]);
    }

    // 0xD1
    public static void SendLogoutNotification(this NetClient socket)
    {
        socket.Send([0xD1, 0x00]);
    }

    // 0xD4
    public static void SendBookHeaderChanged(this NetClient socket, uint serial, string title, string author)
    {
        using FixedSpanWriter writer = new(0xD4, 3 + 8 + title.Length * 2 + author.Length * 2, true);
        writer.WriteUInt32BE(serial);
        writer.WriteUInt8(0x00);
        writer.WriteUInt8(0x00);
        writer.WriteUInt16BE(0);
        writer.WriteString<UTF8>(title, StringOptions.PrependByteSize);
        writer.WriteString<UTF8>(author, StringOptions.PrependByteSize);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD6
    public static void SendMegaClilocRequest(this NetClient socket, List<uint> serials)
    {
        int count = Math.Min(15, serials.Count);

        using FixedSpanWriter writer = new(0xD6, 3 + count * 4, true);

        for (int i = 0; i < count; i++)
        {
            writer.WriteUInt32BE(serials[i]);
        }

        serials.RemoveRange(0, count);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendGuildMenuRequest(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 11], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x28);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer.Buffer);
    }

    // 0xD7
    public static void SendQuestMenuRequest(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x32);
        writer.WriteUInt8(0x00);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendEquipLastWeapon(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x1E);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendUseCombatAbility(this NetClient socket, World world, byte idx)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 12], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x19);
        writer.WriteUInt32BE(0);
        writer.WriteUInt8(idx);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }


    #region Custom Houses
    // 0xD7
    public static void SendCustomHouseBackup(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x02);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseRestore(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x03);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseCommit(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x04);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseBuildingExit(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x0C);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseGoToFloor(this NetClient socket, World world, byte floor)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 12], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x12);
        writer.WriteUInt32BE(0);
        writer.WriteUInt8(floor);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseSync(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x0E);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseClear(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x10);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseRevert(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x1A);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseResponse(this NetClient socket, World world)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 7], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x0A);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseAddItem(this NetClient socket, World world, ushort graphic, int x, int y)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 22], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x06);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(graphic);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)x);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)y);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseDeleteItem(this NetClient socket, World world, ushort graphic, int x, int y, int z)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 27], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x05);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(graphic);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)x);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)y);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)z);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseAddRoof(this NetClient socket, World world, ushort graphic, int x, int y, int z)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 27], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x13);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(graphic);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)x);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)y);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)z);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseDeleteRoof(this NetClient socket, World world, ushort graphic, int x, int y, int z)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 27], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x14);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(graphic);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)x);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)y);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)z);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }

    // 0xD7
    public static void SendCustomHouseAddStair(this NetClient socket, World world, ushort graphic, int x, int y)
    {
        using FixedSpanWriter writer = new(0xD7, stackalloc byte[3 + 22], true);
        writer.WriteUInt32BE(world.Player.Serial);
        writer.WriteUInt16BE(0x0D);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE(graphic);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)x);
        writer.WriteUInt8(0x00);
        writer.WriteUInt32BE((uint)y);
        writer.WriteUInt8(0x0A);

        writer.WritePacketLength();

        socket.Send(writer);
    }
    #endregion Custom Houses


    // 0xEF
    public static void SendSeed(this NetClient socket, uint seed, byte major, byte minor, byte build, byte extra)
    {
        using FixedSpanWriter writer = new(0xEF, stackalloc byte[21]);
        writer.WriteUInt32BE(seed);
        writer.WriteUInt32BE(major);
        writer.WriteUInt32BE(minor);
        writer.WriteUInt32BE(build);
        writer.WriteUInt32BE(extra);

        socket.Send(writer, true);
    }

    // 0xF0
    public static void SendQueryPartyPosition(this NetClient socket)
    {
        socket.Send([0xF0, 0x00]);
    }

    // 0xF0
    public static void SendQueryGuildPosition(this NetClient socket)
    {
        socket.Send([0xF0, 0x01, 0x01]);
    }

    // 0xF0
    public static void SendRazorACK(this NetClient socket)
    {
        socket.Send([0xF0, 0xFF]);
    }

    // 0xFA
    public static void SendOpenUOStore(this NetClient socket)
    {
        socket.Send([0xFA]);
    }

    // 0xFB
    public static void SendShowPublicHouseContent(this NetClient socket, bool show)
    {
        socket.Send([0xFB, (byte)(show ? 0x01 : 0x00)]);
    }

    public static void SendToPluginsAllSpells(this NetClient socket)
    {
        VariableSpanWriter writer = new(0xBF, stackalloc byte[3 + 3], true);
        writer.WriteUInt16BE(0xBEEF);
        writer.WriteUInt8(0x00);

        writeDef(SpellsMagery.GetAllSpells, ref writer);
        writeDef(SpellsNecromancy.GetAllSpells, ref writer);
        writeDef(SpellsBushido.GetAllSpells, ref writer);
        writeDef(SpellsNinjitsu.GetAllSpells, ref writer);
        writeDef(SpellsChivalry.GetAllSpells, ref writer);
        writeDef(SpellsSpellweaving.GetAllSpells, ref writer);
        writeDef(SpellsMastery.GetAllSpells, ref writer);

        int len = writer.BytesWritten;

        // TEMPORARY
        byte[] temp = new byte[len];
        writer.Buffer.CopyTo(temp);
        Plugin.ProcessRecvPacket(temp, ref len);

        writer.Dispose();

        static void writeDef(IReadOnlyDictionary<int, SpellDefinition> dict, ref VariableSpanWriter writer)
        {
            writer.WriteUInt16BE((ushort)dict.Count);

            foreach (KeyValuePair<int, SpellDefinition> pair in dict)
            {
                SpellDefinition def = pair.Value;

                writer.WriteUInt16BE((ushort)pair.Key); // spell id
                writer.WriteUInt16BE((ushort)def.ManaCost); // mana cost
                writer.WriteUInt16BE((ushort)def.MinSkill); // min skill
                writer.WriteUInt8((byte)def.TargetType); // target type
                writer.WriteString<UnicodeBE>(def.Name, StringOptions.PrependByteSize); // spell name
                writer.WriteString<UnicodeBE>(def.PowerWords, StringOptions.PrependByteSize); // power of word

                writer.WriteUInt16BE((ushort)def.Regs.Length); // reagents

                for (int i = 0; i < def.Regs.Length; i++)
                {
                    writer.WriteUInt8((byte)def.Regs[i]);
                }
            }
        }
    }

    public static void SendToPluginsAllSkills(this NetClient socket)
    {
        List<SkillEntry> skills = Client.Game.UO.FileManager.Skills.SortedSkills;

        using VariableSpanWriter writer = new(0xBF, stackalloc byte[3 + 3], true);
        writer.WriteUInt16BE(0xBEEF);
        writer.WriteUInt8(0x01);
        writer.WriteUInt16BE((ushort)skills.Count);

        foreach (SkillEntry s in skills)
        {
            writer.WriteUInt16BE((ushort)s.Index);
            writer.WriteBool(s.HasAction);
            writer.WriteString<UnicodeBE>(s.Name, StringOptions.PrependByteSize);
        }

        int len = writer.BytesWritten;

        // TEMPORARY
        byte[] temp = new byte[len];
        writer.Buffer.CopyTo(temp);

        Plugin.ProcessRecvPacket(temp, ref len);
    }
}