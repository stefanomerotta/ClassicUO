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

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO.Buffers;
using ClassicUO.IO.Encoders;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClassicUO.Network;

#nullable enable

internal sealed partial class PacketHandlers
{
    public delegate void OnPacketBufferReader(World world, ref SpanReader p);

    private static uint _requestedGridLoot;

    private static readonly TextFileParser _parser = new(string.Empty, [' '], [], ['{', '}']);
    private static readonly TextFileParser _cmdparser = new(string.Empty, [' ', ','], [], ['@', '@']);

    private readonly OnPacketBufferReader?[] _handlers = new OnPacketBufferReader[0x100];
    private readonly List<uint> _clilocRequests = [];
    private readonly List<uint> _customHouseRequests = [];
    private readonly PacketLogger _packetLogger = new();
    private readonly CircularBuffer _buffer = new();
    private readonly CircularBuffer _pluginsBuffer = new();
    private byte[] _readingBuffer = new byte[4096];

    public static PacketHandlers Handler { get; } = new PacketHandlers();

    public void Add(byte id, OnPacketBufferReader handler)
    {
        _handlers[id] = handler;
    }

    public int ParsePackets(NetClient socket, World world)
    {
        Span<byte> data = socket.CollectAvailableData();
        if (!data.IsEmpty)
        {
            _buffer.Enqueue(data);
            socket.CommitReadData(data.Length);
        }

        return ParsePackets(socket, world, _buffer, true) + ParsePackets(socket, world, _pluginsBuffer, false);
    }

    private int ParsePackets(NetClient socket, World world, CircularBuffer stream, bool allowPlugins)
    {
        int packetsCount = 0;

        lock (stream)
        {
            ref byte[] packetBuffer = ref _readingBuffer;

            while (stream.Length > 0)
            {
                if (!GetPacketInfo(socket, stream, stream.Length, out byte packetId, out int offset, out int packetlength))
                {
                    Log.Warn($"Invalid ID: {packetId:X2} | off: {offset} | len: {packetlength} | stream.pos: {stream.Length}");
                    break;
                }

                if (stream.Length < packetlength) // need more data
                {
                    Log.Warn($"need more data ID: {packetId:X2} | off: {offset} | len: {packetlength} | stream.pos: {stream.Length}");
                    break;
                }

                while (packetlength > packetBuffer.Length)
                {
                    Array.Resize(ref packetBuffer, packetBuffer.Length * 2);
                }

                _ = stream.Dequeue(packetBuffer, 0, packetlength);

                PacketLogger.Default?.Log(packetBuffer.AsSpan(0, packetlength), false);

                // TODO: the pluging function should allow Span<byte> or unsafe type only.
                // The current one is a bad style decision.
                // It will be fixed once the new plugin system is done.
                if (!allowPlugins || Plugin.ProcessRecvPacket(packetBuffer, ref packetlength))
                {
                    AnalyzePacket(world, packetBuffer.AsSpan(0, packetlength), offset);
                    packetsCount++;
                }
            }
        }

        return packetsCount;
    }

    public void Append(Span<byte> data)
    {
        if (data.IsEmpty)
            return;

        _pluginsBuffer.Enqueue(data);
    }

    private void AnalyzePacket(World world, ReadOnlySpan<byte> data, int offset)
    {
        if (data.IsEmpty)
            return;

        OnPacketBufferReader? bufferReader = _handlers[data[0]];
        if (bufferReader is null)
            return;

        SpanReader buffer = new(data);
        buffer.Seek(offset);
        bufferReader(world, ref buffer);
    }

    private static bool GetPacketInfo(NetClient socket, CircularBuffer buffer, int bufferLen, out byte packetId,
        out int packetOffset, out int packetLen)
    {
        if (bufferLen <= 0)
        {
            packetId = 0xFF;
            packetLen = 0;
            packetOffset = 0;

            return false;
        }

        packetLen = socket.PacketsTable.GetPacketLength(packetId = buffer[0]);
        packetOffset = 1;

        if (packetLen == -1)
        {
            if (bufferLen < 3)
                return false;

            byte b0 = buffer[1];
            byte b1 = buffer[2];

            packetLen = (b0 << 8) | b1;
            packetOffset = 3;
        }

        return true;
    }

    public static void SendMegaClilocRequests(World world)
    {
        if (world.ClientFeatures.TooltipsEnabled && Handler._clilocRequests.Count != 0)
            NetClient.Socket.SendMegaClilocRequest(Handler._clilocRequests);

        if (Handler._customHouseRequests.Count > 0)
        {
            for (int i = 0; i < Handler._customHouseRequests.Count; i++)
            {
                NetClient.Socket.SendCustomHouseDataRequest(Handler._customHouseRequests[i]);
            }

            Handler._customHouseRequests.Clear();
        }
    }

    public static void AddMegaClilocRequest(uint serial)
    {
        foreach (uint s in Handler._clilocRequests)
        {
            if (s == serial)
                return;
        }

        Handler._clilocRequests.Add(serial);
    }

    

    

    

    





    

    

    

    

    





    

    

    

    private static void Unknown_0x32(World world, ref SpanReader p) { }

    private static void SetTime(World world, ref SpanReader p)
    { }

    

    

    

    private static void GraphicEffect(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        GraphicEffectType type = (GraphicEffectType)p.ReadUInt8();

        if (type > GraphicEffectType.FixedFrom)
        {
            if (type == GraphicEffectType.ScreenFade && p[0] == 0x70)
            {
                p.Skip(8);
                ushort val = p.ReadUInt16BE();

                if (val > 4)
                {
                    val = 4;
                }

                Log.Warn("Effect not implemented");
            }

            return;
        }

        uint source = p.ReadUInt32BE();
        uint target = p.ReadUInt32BE();
        ushort graphic = p.ReadUInt16BE();
        ushort srcX = p.ReadUInt16BE();
        ushort srcY = p.ReadUInt16BE();
        sbyte srcZ = p.ReadInt8();
        ushort targetX = p.ReadUInt16BE();
        ushort targetY = p.ReadUInt16BE();
        sbyte targetZ = p.ReadInt8();
        byte speed = p.ReadUInt8();
        byte duration = p.ReadUInt8();
        ushort unk = p.ReadUInt16BE();
        bool fixedDirection = p.ReadBool();
        bool doesExplode = p.ReadBool();
        uint hue = 0;
        GraphicEffectBlendMode blendmode = 0;

        if (p[0] == 0x70) { }
        else
        {
            hue = p.ReadUInt32BE();
            blendmode = (GraphicEffectBlendMode)(p.ReadUInt32BE() % 7);

            if (p[0] == 0xC7)
            {
                var tileID = p.ReadUInt16BE();
                var explodeEffect = p.ReadUInt16BE();
                var explodeSound = p.ReadUInt16BE();
                var serial = p.ReadUInt32BE();
                var layer = p.ReadUInt8();
                p.Skip(2);
            }
        }

        world.SpawnEffect(
            type,
            source,
            target,
            graphic,
            (ushort)hue,
            srcX,
            srcY,
            srcZ,
            targetX,
            targetY,
            targetZ,
            speed,
            duration,
            fixedDirection,
            doesExplode,
            false,
            blendmode
        );
    }

    

    

    

    

    

    private static void UpdateCharacter(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();
        Mobile mobile = world.Mobiles.Get(serial);

        if (mobile == null)
        {
            return;
        }

        ushort graphic = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        Direction direction = (Direction)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        NotorietyFlag notoriety = (NotorietyFlag)p.ReadUInt8();

        mobile.NotorietyFlag = notoriety;

        if (serial == world.Player)
        {
            mobile.Flags = flags;
            mobile.Graphic = graphic;
            mobile.CheckGraphicChange();
            mobile.FixHue(hue);
            // TODO: x,y,z, direction cause elastic effect, ignore 'em for the moment
        }
        else
        {
            UpdateGameObject(world, serial, graphic, 0, 0, x, y, z, direction, hue, flags, 0, 1, 1);
        }
    }

    private static void UpdateObject(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();
        ushort graphic = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        Direction direction = (Direction)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        NotorietyFlag notoriety = (NotorietyFlag)p.ReadUInt8();
        bool oldDead = false;
        //bool alreadyExists =world.Get(serial) != null;

        if (serial == world.Player)
        {
            oldDead = world.Player.IsDead;
            world.Player.Graphic = graphic;
            world.Player.CheckGraphicChange();
            world.Player.FixHue(hue);
            world.Player.Flags = flags;
        }
        else
        {
            UpdateGameObject(world, serial, graphic, 0, 0, x, y, z, direction, hue, flags, 0, 0, 1);
        }

        Entity obj = world.Get(serial);

        if (obj == null)
        {
            return;
        }

        if (!obj.IsEmpty)
        {
            LinkedObject o = obj.Items;

            while (o != null)
            {
                LinkedObject next = o.Next;
                Item it = (Item)o;

                if (!it.Opened && it.Layer != Layer.Backpack)
                {
                    world.RemoveItem(it.Serial, true);
                }

                o = next;
            }
        }

        if (SerialHelper.IsMobile(serial) && obj is Mobile mob)
        {
            mob.NotorietyFlag = notoriety;

            UIManager.GetGump<PaperDollGump>(serial)?.RequestUpdateContents();
        }

        if (p[0] != 0x78)
        {
            p.Skip(6);
        }

        uint itemSerial = p.ReadUInt32BE();

        while (itemSerial != 0 && p.Position < p.Length)
        {
            //if (!SerialHelper.IsItem(itemSerial))
            //    break;

            ushort itemGraphic = p.ReadUInt16BE();
            byte layer = p.ReadUInt8();
            ushort item_hue = 0;

            if (Client.Game.UO.Version >= Utility.ClientVersion.CV_70331)
            {
                item_hue = p.ReadUInt16BE();
            }
            else if ((itemGraphic & 0x8000) != 0)
            {
                itemGraphic &= 0x7FFF;
                item_hue = p.ReadUInt16BE();
            }

            Item item = world.GetOrCreateItem(itemSerial);
            item.Graphic = itemGraphic;
            item.FixHue(item_hue);
            item.Amount = 1;
            world.RemoveItemFromContainer(item);
            item.Container = serial;
            item.Layer = (Layer)layer;

            item.CheckGraphicChange();

            obj.PushToBack(item);

            itemSerial = p.ReadUInt32BE();
        }

        if (serial == world.Player)
        {
            if (oldDead != world.Player.IsDead)
            {
                if (world.Player.IsDead)
                {
                    // NOTE: This packet causes some weird issue on sphere servers.
                    //       When the character dies, this packet trigger a "reset" and
                    //       somehow it messes up the packet reading server side
                    //NetClient.Socket.Send_DeathScreen();
                    world.ChangeSeason(Game.Managers.Season.Desolation, 42);
                }
                else
                {
                    world.ChangeSeason(world.OldSeason, world.OldMusicIndex);
                }
            }

            UIManager.GetGump<PaperDollGump>(serial)?.RequestUpdateContents();

            world.Player.UpdateAbilities();
        }
    }

    

    

    

    private static void DisplayMap(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
        ushort gumpid = p.ReadUInt16BE();
        ushort startX = p.ReadUInt16BE();
        ushort startY = p.ReadUInt16BE();
        ushort endX = p.ReadUInt16BE();
        ushort endY = p.ReadUInt16BE();
        ushort width = p.ReadUInt16BE();
        ushort height = p.ReadUInt16BE();

        MapGump gump = new MapGump(world, serial, gumpid, width, height);
        SpriteInfo multiMapInfo;

        if (p[0] == 0xF5 || Client.Game.UO.Version >= Utility.ClientVersion.CV_308Z)
        {
            ushort facet = 0;

            if (p[0] == 0xF5)
            {
                facet = p.ReadUInt16BE();
            }

            multiMapInfo = Client.Game.UO.MultiMaps.GetMap(facet, width, height, startX, startY, endX, endY);
        }
        else
        {
            multiMapInfo = Client.Game.UO.MultiMaps.GetMap(null, width, height, startX, startY, endX, endY);
        }

        if (multiMapInfo.Texture != null)
            gump.SetMapTexture(multiMapInfo.Texture);

        UIManager.Add(gump);

        Item it = world.Items.Get(serial);

        if (it != null)
        {
            it.Opened = true;
        }
    }

    private static void OpenBook(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
        bool oldpacket = p[0] == 0x93;
        bool editable = p.ReadBool();

        if (!oldpacket)
        {
            editable = p.ReadBool();
        }
        else
        {
            p.Skip(1);
        }

        ModernBookGump bgump = UIManager.GetGump<ModernBookGump>(serial);

        if (bgump == null || bgump.IsDisposed)
        {
            ushort page_count = p.ReadUInt16BE();
            string title = oldpacket
                ? p.ReadFixedString<UTF8>(60, true)
                : p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true);
            string author = oldpacket
                ? p.ReadFixedString<UTF8>(30, true)
                : p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true);

            UIManager.Add(
                new ModernBookGump(world, serial, page_count, title, author, editable, oldpacket)
                {
                    X = 100,
                    Y = 100
                }
            );

            NetClient.Socket.SendBookPageDataRequest(serial, 1);
        }
        else
        {
            p.Skip(2);
            bgump.IsEditable = editable;
            bgump.SetTile(
                oldpacket ? p.ReadFixedString<UTF8>(60, true) : p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true),
                editable
            );
            bgump.SetAuthor(
                oldpacket ? p.ReadFixedString<UTF8>(30, true) : p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true),
                editable
            );
            bgump.UseNewHeader = !oldpacket;
            bgump.SetInScreen();
            bgump.BringOnTop();
        }
    }

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    private static void Help(World world, ref SpanReader p) { }

    private static void CharacterProfile(World world, ref SpanReader p)
    {
        if (!world.InGame)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();
        string header = p.ReadString<ASCIICP1215>();
        string footer = p.ReadString<UnicodeBE>();

        string body = p.ReadString<UnicodeBE>();

        UIManager.GetGump<ProfileGump>(serial)?.Dispose();

        UIManager.Add(
            new ProfileGump(world, serial, header, footer, body, serial == world.Player.Serial)
        );
    }

    private static void EnableLockedFeatures(World world, ref SpanReader p)
    {
        LockedFeatureFlags flags = 0;

        if (Client.Game.UO.Version >= Utility.ClientVersion.CV_60142)
        {
            flags = (LockedFeatureFlags)p.ReadUInt32BE();
        }
        else
        {
            flags = (LockedFeatureFlags)p.ReadUInt16BE();
        }

        world.ClientLockedFeatures.SetFlags(flags);

        world.ChatManager.ChatIsEnabled = world.ClientLockedFeatures.Flags.HasFlag(
            LockedFeatureFlags.T2A
        )
            ? ChatStatus.Enabled
            : 0;

        BodyConvFlags bcFlags = 0;
        if (flags.HasFlag(LockedFeatureFlags.UOR))
            bcFlags |= BodyConvFlags.Anim1 | BodyConvFlags.Anim2;
        if (flags.HasFlag(LockedFeatureFlags.LBR))
            bcFlags |= BodyConvFlags.Anim1;
        if (flags.HasFlag(LockedFeatureFlags.AOS))
            bcFlags |= BodyConvFlags.Anim2;
        if (flags.HasFlag(LockedFeatureFlags.SE))
            bcFlags |= BodyConvFlags.Anim3;
        if (flags.HasFlag(LockedFeatureFlags.ML))
            bcFlags |= BodyConvFlags.Anim4;

        Client.Game.UO.Animations.UpdateAnimationTable(bcFlags);
    }

    private static void DisplayQuestArrow(World world, ref SpanReader p)
    {
        bool display = p.ReadBool();
        ushort mx = p.ReadUInt16BE();
        ushort my = p.ReadUInt16BE();

        uint serial = 0;

        if (Client.Game.UO.Version >= Utility.ClientVersion.CV_7090)
        {
            serial = p.ReadUInt32BE();
        }

        QuestArrowGump arrow = UIManager.GetGump<QuestArrowGump>(serial);

        if (display)
        {
            if (arrow == null)
            {
                UIManager.Add(new QuestArrowGump(world, serial, mx, my));
            }
            else
            {
                arrow.SetRelativePosition(mx, my);
            }
        }
        else
        {
            if (arrow != null)
            {
                arrow.Dispose();
            }
        }
    }

    private static void UltimaMessengerR(World world, ref SpanReader p) { }

    private static void Season(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        byte season = p.ReadUInt8();
        byte music = p.ReadUInt8();

        if (season > 4)
        {
            season = 0;
        }

        if (world.Player.IsDead && season == 4)
        {
            return;
        }

        world.OldSeason = (Season)season;
        world.OldMusicIndex = music;

        if (world.Season == Game.Managers.Season.Desolation)
        {
            world.OldMusicIndex = 42;
        }

        world.ChangeSeason((Season)season, music);
    }

    private static void SendClientVersion(World world, ref SpanReader p)
    {
        NetClient.Socket.SendClientVersion(Settings.GlobalSettings.ClientVersion);
    }

    private static void AssistVersion(World world, ref SpanReader p)
    {
        //uint version = p.ReadUInt32BE();

        //string[] parts = Service.GetByLocalSerial<Settings>().ClientVersion.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        //byte[] clientVersionBuffer =
        //    {byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]), byte.Parse(parts[3])};

        //NetClient.Socket.Send(new PAssistVersion(clientVersionBuffer, version));
    }

    private static void ExtendedCommand(World world, ref SpanReader p)
    {
        ushort cmd = p.ReadUInt16BE();

        switch (cmd)
        {
            case 0:
                break;

            //===========================================================================================
            //===========================================================================================
            case 1: // fast walk prevention
                for (int i = 0; i < 6; i++)
                {
                    world.Player.Walker.FastWalkStack.SetValue(i, p.ReadUInt32BE());
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 2: // add key to fast walk stack
                world.Player.Walker.FastWalkStack.AddValue(p.ReadUInt32BE());

                break;

            //===========================================================================================
            //===========================================================================================
            case 4: // close generic gump
                uint ser = p.ReadUInt32BE();
                int button = (int)p.ReadUInt32BE();

                LinkedListNode<Gump> first = UIManager.Gumps.First;

                while (first != null)
                {
                    LinkedListNode<Gump> nextGump = first.Next;

                    if (first.Value.ServerSerial == ser && first.Value.IsFromServer)
                    {
                        if (button != 0)
                        {
                            (first.Value as Gump)?.OnButtonClick(button);
                        }
                        else
                        {
                            if (first.Value.CanMove)
                            {
                                UIManager.SavePosition(ser, first.Value.Location);
                            }
                            else
                            {
                                UIManager.RemovePosition(ser);
                            }
                        }

                        first.Value.Dispose();
                    }

                    first = nextGump;
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 6: //party
                world.Party.ParsePacket(ref p);

                break;

            //===========================================================================================
            //===========================================================================================
            case 8: // map change
                world.MapIndex = p.ReadUInt8();

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x0C: // close statusbar gump
                UIManager.GetGump<HealthBarGump>(p.ReadUInt32BE())?.Dispose();

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x10: // display equip info
                Item item = world.Items.Get(p.ReadUInt32BE());

                if (item == null)
                {
                    return;
                }

                uint cliloc = p.ReadUInt32BE();
                string str = string.Empty;

                if (cliloc > 0)
                {
                    str = Client.Game.UO.FileManager.Clilocs.GetString((int)cliloc, true);

                    if (!string.IsNullOrEmpty(str))
                    {
                        item.Name = str;
                    }

                    world.MessageManager.HandleMessage(
                        item,
                        str,
                        item.Name,
                        0x3B2,
                        MessageType.Regular,
                        3,
                        TextType.OBJECT,
                        true
                    );
                }

                str = string.Empty;
                ushort crafterNameLen = 0;
                uint next = p.ReadUInt32BE();

                Span<char> span = stackalloc char[256];
                ValueStringBuilder strBuffer = new ValueStringBuilder(span);
                if (next == 0xFFFFFFFD)
                {
                    crafterNameLen = p.ReadUInt16BE();

                    if (crafterNameLen > 0)
                    {
                        strBuffer.Append(ResGeneral.CraftedBy);
                        strBuffer.Append(p.ReadFixedString<ASCIICP1215>(crafterNameLen));
                    }
                }

                if (crafterNameLen != 0)
                {
                    next = p.ReadUInt32BE();
                }

                if (next == 0xFFFFFFFC)
                {
                    strBuffer.Append("[Unidentified");
                }

                byte count = 0;

                while (p.Position < p.Length - 4)
                {
                    if (count != 0 || next == 0xFFFFFFFD || next == 0xFFFFFFFC)
                    {
                        next = p.ReadUInt32BE();
                    }

                    short charges = (short)p.ReadUInt16BE();
                    string attr = Client.Game.UO.FileManager.Clilocs.GetString((int)next);

                    if (attr != null)
                    {
                        if (charges == -1)
                        {
                            if (count > 0)
                            {
                                strBuffer.Append("/");
                                strBuffer.Append(attr);
                            }
                            else
                            {
                                strBuffer.Append(" [");
                                strBuffer.Append(attr);
                            }
                        }
                        else
                        {
                            strBuffer.Append("\n[");
                            strBuffer.Append(attr);
                            strBuffer.Append(" : ");
                            strBuffer.Append(charges.ToString());
                            strBuffer.Append("]");
                            count += 20;
                        }
                    }

                    count++;
                }

                if (count < 20 && count > 0 || next == 0xFFFFFFFC && count == 0)
                {
                    strBuffer.Append(']');
                }

                if (strBuffer.Length != 0)
                {
                    world.MessageManager.HandleMessage(
                        item,
                        strBuffer.ToString(),
                        item.Name,
                        0x3B2,
                        MessageType.Regular,
                        3,
                        TextType.OBJECT,
                        true
                    );
                }

                strBuffer.Dispose();

                NetClient.Socket.SendMegaClilocRequest(item);

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x11:
                break;

            //===========================================================================================
            //===========================================================================================
            case 0x14: // display popup/context menu
                UIManager.ShowGamePopup(
                    new PopupMenuGump(world, PopupMenuData.Parse(ref p))
                    {
                        X = world.DelayedObjectClickManager.LastMouseX,
                        Y = world.DelayedObjectClickManager.LastMouseY
                    }
                );

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x16: // close user interface windows
                uint id = p.ReadUInt32BE();
                uint serial = p.ReadUInt32BE();

                switch (id)
                {
                    case 1: // paperdoll
                        UIManager.GetGump<PaperDollGump>(serial)?.Dispose();

                        break;

                    case 2: //statusbar
                        UIManager.GetGump<HealthBarGump>(serial)?.Dispose();

                        if (serial == world.Player.Serial)
                        {
                            StatusGumpBase.GetStatusGump()?.Dispose();
                        }

                        break;

                    case 8: // char profile
                        UIManager.GetGump<ProfileGump>()?.Dispose();

                        break;

                    case 0x0C: //container
                        UIManager.GetGump<ContainerGump>(serial)?.Dispose();

                        break;
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x18: // enable map patches

                if (Client.Game.UO.FileManager.Maps.ApplyPatches(ref p))
                {
                    //List<GameObject> list = new List<GameObject>();

                    //foreach (int i in World.Map.GetUsedChunks())
                    //{
                    //    Chunk chunk = World.Map.Chunks[i];

                    //    for (int xx = 0; xx < 8; xx++)
                    //    {
                    //        for (int yy = 0; yy < 8; yy++)
                    //        {
                    //            Tile tile = chunk.Tiles[xx, yy];

                    //            for (GameObject obj = tile.FirstNode; obj != null; obj = obj.Right)
                    //            {
                    //                if (!(obj is Static) && !(obj is Land))
                    //                {
                    //                    list.Add(obj);
                    //                }
                    //            }
                    //        }
                    //    }
                    //}


                    int map = world.MapIndex;
                    world.MapIndex = -1;
                    world.MapIndex = map;

                    Log.Trace("Map Patches applied.");
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x19: //extened stats
                byte version = p.ReadUInt8();
                serial = p.ReadUInt32BE();

                switch (version)
                {
                    case 0:
                        Mobile bonded = world.Mobiles.Get(serial);

                        if (bonded == null)
                        {
                            break;
                        }

                        bool dead = p.ReadBool();
                        bonded.IsDead = dead;

                        break;

                    case 2:

                        if (serial == world.Player)
                        {
                            byte updategump = p.ReadUInt8();
                            byte state = p.ReadUInt8();

                            world.Player.StrLock = (Lock)((state >> 4) & 3);
                            world.Player.DexLock = (Lock)((state >> 2) & 3);
                            world.Player.IntLock = (Lock)(state & 3);

                            StatusGumpBase.GetStatusGump()?.RequestUpdateContents();
                        }

                        break;

                    case 5:

                        int pos = p.Position;
                        byte zero = p.ReadUInt8();
                        byte type2 = p.ReadUInt8();

                        if (type2 == 0xFF)
                        {
                            byte status = p.ReadUInt8();
                            ushort animation = p.ReadUInt16BE();
                            ushort frame = p.ReadUInt16BE();

                            if (status == 0 && animation == 0 && frame == 0)
                            {
                                p.Seek(pos);
                                goto case 0;
                            }

                            Mobile mobile = world.Mobiles.Get(serial);

                            if (mobile != null)
                            {
                                mobile.SetAnimation(
                                    Mobile.GetReplacedObjectAnimation(mobile.Graphic, animation)
                                );
                                mobile.ExecuteAnimation = false;
                                mobile.AnimIndex = (byte)frame;
                            }
                        }
                        else if (world.Player != null && serial == world.Player)
                        {
                            p.Seek(pos);
                            goto case 2;
                        }

                        break;
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x1B: // new spellbook content
                p.Skip(2);
                Item spellbook = world.GetOrCreateItem(p.ReadUInt32BE());
                spellbook.Graphic = p.ReadUInt16BE();
                spellbook.Clear();
                ushort type = p.ReadUInt16BE();

                for (int j = 0; j < 2; j++)
                {
                    uint spells = 0;

                    for (int i = 0; i < 4; i++)
                    {
                        spells |= (uint)(p.ReadUInt8() << (i * 8));
                    }

                    for (int i = 0; i < 32; i++)
                    {
                        if ((spells & (1 << i)) != 0)
                        {
                            ushort cc = (ushort)(j * 32 + i + 1);
                            // FIXME: should i call Item.Create ?
                            Item spellItem = Item.Create(world, cc); // new Item()
                            spellItem.Serial = cc;
                            spellItem.Graphic = 0x1F2E;
                            spellItem.Amount = cc;
                            spellItem.Container = spellbook;
                            spellbook.PushToBack(spellItem);
                        }
                    }
                }

                UIManager.GetGump<SpellbookGump>(spellbook)?.RequestUpdateContents();

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x1D: // house revision state
                serial = p.ReadUInt32BE();
                uint revision = p.ReadUInt32BE();

                Item multi = world.Items.Get(serial);

                if (multi == null)
                {
                    world.HouseManager.Remove(serial);
                }

                if (
                    !world.HouseManager.TryGetHouse(serial, out House house)
                    || !house.IsCustom
                    || house.Revision != revision
                )
                {
                    Handler._customHouseRequests.Add(serial);
                }
                else
                {
                    house.Generate();
                    world.BoatMovingManager.ClearSteps(serial);

                    UIManager.GetGump<MiniMapGump>()?.RequestUpdateContents();

                    if (world.HouseManager.EntityIntoHouse(serial, world.Player))
                    {
                        Client.Game.GetScene<GameScene>()?.UpdateMaxDrawZ(true);
                    }
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x20:
                serial = p.ReadUInt32BE();
                type = p.ReadUInt8();
                ushort graphic = p.ReadUInt16BE();
                ushort x = p.ReadUInt16BE();
                ushort y = p.ReadUInt16BE();
                sbyte z = p.ReadInt8();

                switch (type)
                {
                    case 1: // update
                        break;

                    case 2: // remove
                        break;

                    case 3: // update multi pos
                        break;

                    case 4: // begin
                        HouseCustomizationGump gump = UIManager.GetGump<HouseCustomizationGump>();

                        if (gump != null)
                        {
                            break;
                        }

                        gump = new HouseCustomizationGump(world, serial, 50, 50);
                        UIManager.Add(gump);

                        break;

                    case 5: // end
                        UIManager.GetGump<HouseCustomizationGump>(serial)?.Dispose();

                        break;
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x21:

                for (int i = 0; i < 2; i++)
                {
                    world.Player.Abilities[i] &= (Ability)0x7F;
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x22:
                p.Skip(1);

                Entity en = world.Get(p.ReadUInt32BE());

                if (en != null)
                {
                    byte damage = p.ReadUInt8();

                    if (damage > 0)
                    {
                        world.WorldTextManager.AddDamage(en, damage);
                    }
                }

                break;

            case 0x25:

                ushort spell = p.ReadUInt16BE();
                bool active = p.ReadBool();

                foreach (Gump g in UIManager.Gumps)
                {
                    if (!g.IsDisposed && g.IsVisible)
                    {
                        if (g is UseSpellButtonGump spellButton && spellButton.SpellID == spell)
                        {
                            if (active)
                            {
                                spellButton.Hue = 38;
                                world.ActiveSpellIcons.Add(spell);
                            }
                            else
                            {
                                spellButton.Hue = 0;
                                world.ActiveSpellIcons.Remove(spell);
                            }

                            break;
                        }
                    }
                }

                break;

            //===========================================================================================
            //===========================================================================================
            case 0x26:
                byte val = p.ReadUInt8();

                if (val > (int)CharacterSpeedType.FastUnmountAndCantRun)
                {
                    val = 0;
                }

                world.Player.SpeedMode = (CharacterSpeedType)val;

                break;

            case 0x2A:
                bool isfemale = p.ReadBool();
                byte race = p.ReadUInt8();

                UIManager.GetGump<RaceChangeGump>()?.Dispose();
                UIManager.Add(new RaceChangeGump(world, isfemale, race));
                break;

            case 0x2B:
                serial = p.ReadUInt16BE();
                byte animID = p.ReadUInt8();
                byte frameCount = p.ReadUInt8();

                foreach (Mobile m in world.Mobiles.Values)
                {
                    if ((m.Serial & 0xFFFF) == serial)
                    {
                        m.SetAnimation(animID);
                        m.AnimIndex = frameCount;
                        m.ExecuteAnimation = false;

                        break;
                    }
                }

                break;

            case 0xBEEF: // ClassicUO commands

                type = p.ReadUInt16BE();

                break;

            default:
                Log.Warn($"Unhandled 0xBF - sub: {cmd.ToHex()}");

                break;
        }
    }

    private static void DisplayClilocString(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();
        Entity entity = world.Get(serial);
        ushort graphic = p.ReadUInt16BE();
        MessageType type = (MessageType)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        ushort font = p.ReadUInt16BE();
        uint cliloc = p.ReadUInt32BE();
        AffixType flags = p[0] == 0xCC ? (AffixType)p.ReadUInt8() : 0x00;
        string name = p.ReadFixedString<ASCIICP1215>(30);
        string affix = p[0] == 0xCC ? p.ReadString<ASCIICP1215>() : string.Empty;

        string arguments = null;

        if (cliloc == 1008092 || cliloc == 1005445) // value for "You notify them you don't want to join the party" || "You have been added to the party"
        {
            for (LinkedListNode<Gump> g = UIManager.Gumps.Last; g != null; g = g.Previous)
            {
                if (g.Value is PartyInviteGump pg)
                {
                    pg.Dispose();
                }
            }
        }

        int remains = p.Remaining;

        if (remains > 0)
        {
            if (p[0] == 0xCC)
            {
                arguments = p.ReadFixedString<UnicodeBE>(remains);
            }
            else
            {
                arguments = p.ReadFixedString<UnicodeLE>(remains / 2);
            }
        }

        string text = Client.Game.UO.FileManager.Clilocs.Translate((int)cliloc, arguments);

        if (text == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(affix))
        {
            if ((flags & AffixType.Prepend) != 0)
            {
                text = $"{affix}{text}";
            }
            else
            {
                text = $"{text}{affix}";
            }
        }

        if ((flags & AffixType.System) != 0)
        {
            type = MessageType.System;
        }

        if (!Client.Game.UO.FileManager.Fonts.UnicodeFontExists((byte)font))
        {
            font = 0;
        }

        TextType text_type = TextType.SYSTEM;

        if (
            serial == 0xFFFF_FFFF
            || serial == 0
            || !string.IsNullOrEmpty(name)
                && string.Equals(name, "system", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            // do nothing
        }
        else if (entity != null)
        {
            //entity.Graphic = graphic;
            text_type = TextType.OBJECT;

            if (string.IsNullOrEmpty(entity.Name))
            {
                entity.Name = name;
            }
        }
        else
        {
            if (type == MessageType.Label)
                return;
        }

        world.MessageManager.HandleMessage(
            entity,
            text,
            name,
            hue,
            type,
            (byte)font,
            text_type,
            true
        );
    }

    private static void UnicodePrompt(World world, ref SpanReader p)
    {
        if (!world.InGame)
        {
            return;
        }

        world.MessageManager.PromptData = new PromptData
        {
            Prompt = ConsolePrompt.Unicode,
            Data = p.ReadUInt64BE()
        };
    }

    private static void Semivisible(World world, ref SpanReader p) { }

    private static void InvalidMapEnable(World world, ref SpanReader p) { }

    private static void ParticleEffect3D(World world, ref SpanReader p) { }

    private static void GetUserServerPingGodClientR(World world, ref SpanReader p) { }

    private static void GlobalQueCount(World world, ref SpanReader p) { }

    private static void ConfigurationFileR(World world, ref SpanReader p) { }

    private static void Logout(World world, ref SpanReader p)
    {
        // http://docs.polserver.com/packets/index.php?Packet=0xD1

        if (
            Client.Game.GetScene<GameScene>().DisconnectionRequested
            && (
                world.ClientFeatures.Flags
                & CharacterListFlags.CLF_OWERWRITE_CONFIGURATION_BUTTON
            ) != 0
        )
        {
            if (p.ReadBool())
            {
                // client can disconnect
                NetClient.Socket.Disconnect();
                Client.Game.SetScene(new LoginScene(world));
            }
            else
            {
                Log.Warn("0x1D - client asked to disconnect but server answered 'NO!'");
            }
        }
    }

    private static void MegaCliloc(World world, ref SpanReader p)
    {
        if (!world.InGame)
        {
            return;
        }

        ushort unknown = p.ReadUInt16BE();

        if (unknown > 1)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();

        p.Skip(2);

        uint revision = p.ReadUInt32BE();

        Entity entity = world.Mobiles.Get(serial);

        if (entity == null)
        {
            if (SerialHelper.IsMobile(serial))
            {
                Log.Warn("Searching a mobile into World.Items from MegaCliloc packet");
            }

            entity = world.Items.Get(serial);
        }

        List<(int, string, int)> list = new List<(int, string, int)>();
        int totalLength = 0;

        while (p.Position < p.Length)
        {
            int cliloc = (int)p.ReadUInt32BE();

            if (cliloc == 0)
            {
                break;
            }

            ushort length = p.ReadUInt16BE();

            string argument = string.Empty;

            if (length != 0)
            {
                argument = p.ReadFixedString<UnicodeLE>(length / 2);
            }

            string str = Client.Game.UO.FileManager.Clilocs.Translate(cliloc, argument, true);

            if (str == null)
            {
                continue;
            }

            int argcliloc = 0;

            string[] argcheck = argument.Split(
                new[] { '#' },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (argcheck.Length == 2)
            {
                int.TryParse(argcheck[1], out argcliloc);
            }

            // hardcoded colors lol
            switch (cliloc)
            {
                case 1080418:
                    if (Client.Game.UO.Version >= Utility.ClientVersion.CV_60143)
                        str = "<basefont color=#40a4fe>" + str + "</basefont>";
                    break;
                case 1061170:
                    if (int.TryParse(argument, out var strength) && world.Player.Strength < strength)
                        str = "<basefont color=#FF0000>" + str + "</basefont>";
                    break;
                case 1062613:
                    str = "<basefont color=#FFCC33>" + str + "</basefont>";
                    break;
                case 1159561:
                    str = "<basefont color=#b66dff>" + str + "</basefont>";
                    break;
            }


            for (int i = 0; i < list.Count; i++)
            {
                if (
                    list[i].Item1 == cliloc
                    && string.Equals(list[i].Item2, str, StringComparison.Ordinal)
                )
                {
                    list.RemoveAt(i);

                    break;
                }
            }

            list.Add((cliloc, str, argcliloc));

            totalLength += str.Length;
        }

        Item container = null;

        if (entity is Item it && SerialHelper.IsValid(it.Container))
        {
            container = world.Items.Get(it.Container);
        }

        bool inBuyList = false;

        if (container != null)
        {
            inBuyList =
                container.Layer == Layer.ShopBuy
                || container.Layer == Layer.ShopBuyRestock
                || container.Layer == Layer.ShopSell;
        }

        bool first = true;

        string name = string.Empty;
        string data = string.Empty;
        int namecliloc = 0;

        if (list.Count != 0)
        {
            Span<char> span = stackalloc char[totalLength];
            ValueStringBuilder sb = new ValueStringBuilder(span);

            foreach (var s in list)
            {
                string str = s.Item2;

                if (first)
                {
                    name = str;

                    if (entity != null && !SerialHelper.IsMobile(serial))
                    {
                        entity.Name = str;
                        namecliloc = s.Item3 > 0 ? s.Item3 : s.Item1;
                    }

                    first = false;
                }
                else
                {
                    if (sb.Length != 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(str);
                }
            }

            data = sb.ToString();

            sb.Dispose();
        }

        world.OPL.Add(serial, revision, name, data, namecliloc);

        if (inBuyList && container != null && SerialHelper.IsValid(container.Serial))
        {
            UIManager.GetGump<ShopGump>(container.RootContainer)?.SetNameTo((Item)entity, name);
        }
    }

    private static void GenericAOSCommandsR(World world, ref SpanReader p) { }

    private static unsafe void ReadUnsafeCustomHouseData(
        ReadOnlySpan<byte> source,
        int sourcePosition,
        int dlen,
        int clen,
        int planeZ,
        int planeMode,
        short minX,
        short minY,
        short maxY,
        Item item,
        House house
    )
    {
        //byte* decompressedBytes = stackalloc byte[dlen];
        bool ismovable = item.ItemData.IsMultiMovable;

        byte[] buffer = null;
        Span<byte> span =
            dlen <= 1024
                ? stackalloc byte[dlen]
                : (buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(dlen));

        try
        {
            var result = ZLib.Decompress(source.Slice(sourcePosition, clen), span.Slice(0, dlen));
            var reader = new SpanReader(span.Slice(0, dlen));

            ushort id = 0;
            sbyte x = 0,
                y = 0,
                z = 0;

            switch (planeMode)
            {
                case 0:
                    int c = dlen / 5;

                    for (uint i = 0; i < c; i++)
                    {
                        id = reader.ReadUInt16BE();
                        x = reader.ReadInt8();
                        y = reader.ReadInt8();
                        z = reader.ReadInt8();

                        if (id != 0)
                        {
                            house.Add(
                                id,
                                0,
                                (ushort)(item.X + x),
                                (ushort)(item.Y + y),
                                (sbyte)(item.Z + z),
                                true,
                                ismovable
                            );
                        }
                    }

                    break;

                case 1:

                    if (planeZ > 0)
                    {
                        z = (sbyte)((planeZ - 1) % 4 * 20 + 7);
                    }
                    else
                    {
                        z = 0;
                    }

                    c = dlen >> 2;

                    for (uint i = 0; i < c; i++)
                    {
                        id = reader.ReadUInt16BE();
                        x = reader.ReadInt8();
                        y = reader.ReadInt8();

                        if (id != 0)
                        {
                            house.Add(
                                id,
                                0,
                                (ushort)(item.X + x),
                                (ushort)(item.Y + y),
                                (sbyte)(item.Z + z),
                                true,
                                ismovable
                            );
                        }
                    }

                    break;

                case 2:
                    short offX = 0,
                        offY = 0;
                    short multiHeight = 0;

                    if (planeZ > 0)
                    {
                        z = (sbyte)((planeZ - 1) % 4 * 20 + 7);
                    }
                    else
                    {
                        z = 0;
                    }

                    if (planeZ <= 0)
                    {
                        offX = minX;
                        offY = minY;
                        multiHeight = (short)(maxY - minY + 2);
                    }
                    else if (planeZ <= 4)
                    {
                        offX = (short)(minX + 1);
                        offY = (short)(minY + 1);
                        multiHeight = (short)(maxY - minY);
                    }
                    else
                    {
                        offX = minX;
                        offY = minY;
                        multiHeight = (short)(maxY - minY + 1);
                    }

                    c = dlen >> 1;

                    for (uint i = 0; i < c; i++)
                    {
                        id = reader.ReadUInt16BE();
                        x = (sbyte)(i / multiHeight + offX);
                        y = (sbyte)(i % multiHeight + offY);

                        if (id != 0)
                        {
                            house.Add(
                                id,
                                0,
                                (ushort)(item.X + x),
                                (ushort)(item.Y + y),
                                (sbyte)(item.Z + z),
                                true,
                                ismovable
                            );
                        }
                    }

                    break;
            }
        }
        finally
        {
            if (buffer != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static void CustomHouse(World world, ref SpanReader p)
    {
        bool compressed = p.ReadUInt8() == 0x03;
        bool enableReponse = p.ReadBool();
        uint serial = p.ReadUInt32BE();
        Item foundation = world.Items.Get(serial);
        uint revision = p.ReadUInt32BE();

        if (foundation == null)
        {
            return;
        }

        Rectangle? multi = foundation.MultiInfo;

        if (!foundation.IsMulti || multi == null)
        {
            return;
        }

        p.Skip(4);

        if (!world.HouseManager.TryGetHouse(foundation, out House house))
        {
            house = new House(world, foundation, revision, true);
            world.HouseManager.Add(foundation, house);
        }
        else
        {
            house.ClearComponents(true);
            house.Revision = revision;
            house.IsCustom = true;
        }

        short minX = (short)multi.Value.X;
        short minY = (short)multi.Value.Y;
        short maxY = (short)multi.Value.Height;

        if (minX == 0 && minY == 0 && maxY == 0 && multi.Value.Width == 0)
        {
            Log.Warn(
                "[CustomHouse (0xD8) - Invalid multi dimentions. Maybe missing some installation required files"
            );

            return;
        }

        byte planes = p.ReadUInt8();

        house.ClearCustomHouseComponents(0);

        for (int plane = 0; plane < planes; plane++)
        {
            uint header = p.ReadUInt32BE();
            int dlen = (int)(((header & 0xFF0000) >> 16) | ((header & 0xF0) << 4));
            int clen = (int)(((header & 0xFF00) >> 8) | ((header & 0x0F) << 8));
            int planeZ = (int)((header & 0x0F000000) >> 24);
            int planeMode = (int)((header & 0xF0000000) >> 28);

            if (clen <= 0)
            {
                continue;
            }

            ReadUnsafeCustomHouseData(
                p.Buffer,
                p.Position,
                dlen,
                clen,
                planeZ,
                planeMode,
                minX,
                minY,
                maxY,
                foundation,
                house
            );

            p.Skip(clen);
        }

        if (world.CustomHouseManager != null)
        {
            world.CustomHouseManager.GenerateFloorPlace();

            UIManager.GetGump<HouseCustomizationGump>(house.Serial)?.Update();
        }

        UIManager.GetGump<MiniMapGump>()?.RequestUpdateContents();

        if (world.HouseManager.EntityIntoHouse(serial, world.Player))
        {
            Client.Game.GetScene<GameScene>()?.UpdateMaxDrawZ(true);
        }

        world.BoatMovingManager.ClearSteps(serial);
    }

    private static void CharacterTransferLog(World world, ref SpanReader p) { }

    private static void OPLInfo(World world, ref SpanReader p)
    {
        if (world.ClientFeatures.TooltipsEnabled)
        {
            uint serial = p.ReadUInt32BE();
            uint revision = p.ReadUInt32BE();

            if (!world.OPL.IsRevisionEquals(serial, revision))
            {
                AddMegaClilocRequest(serial);
            }
        }
    }

    private static void OpenCompressedGump(World world, ref SpanReader p)
    {
        uint sender = p.ReadUInt32BE();
        uint gumpID = p.ReadUInt32BE();
        uint x = p.ReadUInt32BE();
        uint y = p.ReadUInt32BE();
        uint clen = p.ReadUInt32BE() - 4;
        int dlen = (int)p.ReadUInt32BE();
        byte[] decData = System.Buffers.ArrayPool<byte>.Shared.Rent(dlen);
        string layout;

        try
        {
            ZLib.Decompress(p.Buffer.Slice(p.Position, (int)clen), decData.AsSpan(0, dlen));

            layout = Encoding.UTF8.GetString(decData.AsSpan(0, dlen));
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(decData);
        }

        p.Skip((int)clen);

        uint linesNum = p.ReadUInt32BE();
        string[] lines = new string[linesNum];

        try
        {
            if (linesNum != 0)
            {
                clen = p.ReadUInt32BE() - 4;
                dlen = (int)p.ReadUInt32BE();
                decData = System.Buffers.ArrayPool<byte>.Shared.Rent(dlen);

                try
                {
                    ZLib.Decompress(p.Buffer.Slice(p.Position, (int)clen), decData.AsSpan(0, dlen));
                    p.Skip((int)clen);

                    var reader = new SpanReader(decData.AsSpan(0, dlen));

                    for (int i = 0; i < linesNum; ++i)
                    {
                        int remaining = reader.Remaining;

                        if (remaining >= 2)
                        {
                            int length = reader.ReadUInt16BE();

                            if (length > 0)
                            {
                                lines[i] = reader.ReadFixedString<UnicodeBE>(length);
                            }
                            else
                            {
                                lines[i] = string.Empty;
                            }
                        }
                        else
                        {
                            lines[i] = string.Empty;
                        }
                    }

                    //for (int i = 0, index = 0; i < linesNum && index < dlen; i++)
                    //{
                    //    int length = ((decData[index++] << 8) | decData[index++]) << 1;
                    //    int true_length = 0;

                    //    for (int k = 0; k < length && true_length < length && index + true_length < dlen; ++k, true_length += 2)
                    //    {
                    //        ushort c = (ushort)(((decData[index + true_length] << 8) | decData[index + true_length + 1]) << 1);

                    //        if (c == '\0')
                    //        {
                    //            break;
                    //        }
                    //    }

                    //    lines[i] = Encoding.BigEndianUnicode.GetString(decData, index, true_length);

                    //    index += length;
                    //}
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(decData);
                }
            }

            CreateGump(world, sender, gumpID, (int)x, (int)y, layout, lines);
        }
        finally
        {
            //System.Buffers.ArrayPool<string>.Shared.Return(lines);
        }
    }

    private static void UpdateMobileStatus(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
        byte status = p.ReadUInt8();

        if (status == 1)
        {
            uint attackerSerial = p.ReadUInt32BE();
        }
    }

    private static void BuffDebuff(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        const ushort BUFF_ICON_START = 0x03E9;
        const ushort BUFF_ICON_START_NEW = 0x466;

        uint serial = p.ReadUInt32BE();
        BuffIconType ic = (BuffIconType)p.ReadUInt16BE();

        ushort iconID =
            (ushort)ic >= BUFF_ICON_START_NEW
                ? (ushort)(ic - (BUFF_ICON_START_NEW - 125))
                : (ushort)((ushort)ic - BUFF_ICON_START);

        if (iconID < BuffTable.Table.Length)
        {
            BuffGump gump = UIManager.GetGump<BuffGump>();
            ushort count = p.ReadUInt16BE();

            if (count == 0)
            {
                world.Player.RemoveBuff(ic);
                gump?.RequestUpdateContents();
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    ushort source_type = p.ReadUInt16BE();
                    p.Skip(2);
                    ushort icon = p.ReadUInt16BE();
                    ushort queue_index = p.ReadUInt16BE();
                    p.Skip(4);
                    ushort timer = p.ReadUInt16BE();
                    p.Skip(3);

                    uint titleCliloc = p.ReadUInt32BE();
                    uint descriptionCliloc = p.ReadUInt32BE();
                    uint wtfCliloc = p.ReadUInt32BE();

                    ushort arg_length = p.ReadUInt16BE();
                    var str = p.ReadFixedString<UnicodeLE>(2);
                    var args = str + p.ReadString<UnicodeLE>();
                    string title = Client.Game.UO.FileManager.Clilocs.Translate(
                        (int)titleCliloc,
                        args,
                        true
                    );

                    arg_length = p.ReadUInt16BE();
                    string args_2 = p.ReadString<UnicodeLE>();
                    string description = string.Empty;

                    if (descriptionCliloc != 0)
                    {
                        description =
                            "\n"
                            + Client.Game.UO.FileManager.Clilocs.Translate(
                                (int)descriptionCliloc,
                                String.IsNullOrEmpty(args_2) ? args : args_2,
                                true
                            );

                        if (description.Length < 2)
                        {
                            description = string.Empty;
                        }
                    }

                    arg_length = p.ReadUInt16BE();
                    string args_3 = p.ReadString<UnicodeLE>();
                    string wtf = string.Empty;

                    if (wtfCliloc != 0)
                    {
                        wtf = Client.Game.UO.FileManager.Clilocs.Translate(
                            (int)wtfCliloc,
                            String.IsNullOrEmpty(args_3) ? args : args_3,
                            true
                        );

                        if (!string.IsNullOrWhiteSpace(wtf))
                        {
                            wtf = $"\n{wtf}";
                        }
                    }

                    string text = $"<left>{title}{description}{wtf}</left>";
                    bool alreadyExists = world.Player.IsBuffIconExists(ic);
                    world.Player.AddBuff(ic, BuffTable.Table[iconID], timer, text);

                    if (!alreadyExists)
                    {
                        gump?.RequestUpdateContents();
                    }
                }
            }
        }
    }

    private static void NewCharacterAnimation(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        Mobile mobile = world.Mobiles.Get(p.ReadUInt32BE());

        if (mobile == null)
        {
            return;
        }

        ushort type = p.ReadUInt16BE();
        ushort action = p.ReadUInt16BE();
        byte mode = p.ReadUInt8();
        byte group = Mobile.GetObjectNewAnimation(mobile, type, action, mode);

        mobile.SetAnimation(
            group,
            repeatCount: 1,
            repeat: (type == 1 || type == 2) && mobile.Graphic == 0x0015,
            forward: true,
            fromServer: true
        );
    }

    private static void KREncryptionResponse(World world, ref SpanReader p) { }

    private static void DisplayWaypoint(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        byte map = p.ReadUInt8();
        WaypointsType type = (WaypointsType)p.ReadUInt16BE();
        bool ignoreobject = p.ReadUInt16BE() != 0;
        uint cliloc = p.ReadUInt32BE();
        string name = p.ReadString<UnicodeLE>();
    }

    private static void RemoveWaypoint(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
    }

    private static void KrriosClientSpecial(World world, ref SpanReader p)
    {
        byte type = p.ReadUInt8();

        switch (type)
        {
            case 0x00: // accepted
                Log.Trace("Krrios special packet accepted");
                world.WMapManager.SetACKReceived();
                world.WMapManager.SetEnable(true);

                break;

            case 0x01: // custom party info
            case 0x02: // guild track info
                bool locations = type == 0x01 || p.ReadBool();

                uint serial;

                while ((serial = p.ReadUInt32BE()) != 0)
                {
                    if (locations)
                    {
                        ushort x = p.ReadUInt16BE();
                        ushort y = p.ReadUInt16BE();
                        byte map = p.ReadUInt8();
                        int hits = type == 1 ? 0 : p.ReadUInt8();

                        world.WMapManager.AddOrUpdate(
                            serial,
                            x,
                            y,
                            hits,
                            map,
                            type == 0x02,
                            null,
                            true
                        );
                    }
                }

                world.WMapManager.RemoveUnupdatedWEntity();

                break;

            case 0x03: // runebook contents
                break;

            case 0x04: // guardline data
                break;

            case 0xF0:
                break;

            case 0xFE:

                Client.Game.EnqueueAction(5000, () =>
                {
                    Log.Info("Razor ACK sent");
                    NetClient.Socket.SendRazorACK();
                });

                break;
        }
    }

    private static void FreeshardListR(World world, ref SpanReader p) { }

    private static void UpdateItemSA(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        p.Skip(2);
        byte type = p.ReadUInt8();
        uint serial = p.ReadUInt32BE();
        ushort graphic = p.ReadUInt16BE();
        byte graphicInc = p.ReadUInt8();
        ushort amount = p.ReadUInt16BE();
        ushort unk = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        Direction dir = (Direction)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        ushort unk2 = p.ReadUInt16BE();

        if (serial != world.Player)
        {
            UpdateGameObject(
                world,
                serial,
                graphic,
                graphicInc,
                amount,
                x,
                y,
                z,
                dir,
                hue,
                flags,
                unk,
                type,
                unk2
            );

            if (graphic == 0x2006 && ProfileManager.CurrentProfile.AutoOpenCorpses)
            {
                world.Player.TryOpenCorpses();
            }
        }
        else if (p[0] == 0xF7)
        {
            UpdatePlayer(world, serial, graphic, graphicInc, hue, flags, x, y, z, 0, dir);
        }
    }

    private static void BoatMoving(World world, ref SpanReader p)
    {
        if (!world.InGame)
        {
            return;
        }

        uint serial = p.ReadUInt32BE();
        byte boatSpeed = p.ReadUInt8();
        Direction movingDirection = (Direction)p.ReadUInt8() & Direction.Mask;
        Direction facingDirection = (Direction)p.ReadUInt8() & Direction.Mask;
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        ushort z = p.ReadUInt16BE();

        Item multi = world.Items.Get(serial);

        if (multi == null)
        {
            return;
        }

        //multi.LastX = x;
        //multi.LastY = y;

        //if (World.HouseManager.TryGetHouse(serial, out var house))
        //{
        //    foreach (Multi component in house.Components)
        //    {
        //        component.LastX = (ushort) (x + component.MultiOffsetX);
        //        component.LastY = (ushort) (y + component.MultiOffsetY);
        //    }
        //}

        bool smooth =
            ProfileManager.CurrentProfile != null
            && ProfileManager.CurrentProfile.UseSmoothBoatMovement;

        if (smooth)
        {
            world.BoatMovingManager.AddStep(
                serial,
                boatSpeed,
                movingDirection,
                facingDirection,
                x,
                y,
                (sbyte)z
            );
        }
        else
        {
            //UpdateGameObject(serial,
            //                 multi.Graphic,
            //                 0,
            //                 multi.Amount,
            //                 x,
            //                 y,
            //                 (sbyte) z,
            //                 facingDirection,
            //                 multi.Hue,
            //                 multi.Flags,
            //                 0,
            //                 2,
            //                 1);

            multi.SetInWorldTile(x, y, (sbyte)z);

            if (world.HouseManager.TryGetHouse(serial, out House house))
            {
                house.Generate(true, true, true);
            }
        }

        int count = p.ReadUInt16BE();

        for (int i = 0; i < count; i++)
        {
            uint cSerial = p.ReadUInt32BE();
            ushort cx = p.ReadUInt16BE();
            ushort cy = p.ReadUInt16BE();
            ushort cz = p.ReadUInt16BE();

            if (cSerial == world.Player)
            {
                world.RangeSize.X = cx;
                world.RangeSize.Y = cy;
            }

            Entity ent = world.Get(cSerial);

            if (ent == null)
            {
                continue;
            }

            //if (SerialHelper.IsMobile(cSerial))
            //{
            //    Mobile m = (Mobile) ent;

            //    if (m.Steps.Count != 0)
            //    {
            //        ref var step = ref m.Steps.Back();

            //        step.X = cx;
            //        step.Y = cy;
            //    }
            //}

            //ent.LastX = cx;
            //ent.LastY = cy;

            if (smooth)
            {
                world.BoatMovingManager.PushItemToList(
                    serial,
                    cSerial,
                    x - cx,
                    y - cy,
                    (sbyte)(z - cz)
                );
            }
            else
            {
                if (cSerial == world.Player)
                {
                    UpdatePlayer(
                        world,
                        cSerial,
                        ent.Graphic,
                        0,
                        ent.Hue,
                        ent.Flags,
                        cx,
                        cy,
                        (sbyte)cz,
                        0,
                        world.Player.Direction
                    );
                }
                else
                {
                    UpdateGameObject(
                        world,
                        cSerial,
                        ent.Graphic,
                        0,
                        (ushort)(ent.Graphic == 0x2006 ? ((Item)ent).Amount : 0),
                        cx,
                        cy,
                        (sbyte)cz,
                        SerialHelper.IsMobile(ent) ? ent.Direction : 0,
                        ent.Hue,
                        ent.Flags,
                        0,
                        0,
                        1
                    );
                }
            }
        }
    }

    private static void PacketList(World world, ref SpanReader p)
    {
        if (world.Player == null)
        {
            return;
        }

        int count = p.ReadUInt16BE();

        for (int i = 0; i < count; i++)
        {
            byte id = p.ReadUInt8();

            if (id == 0xF3)
            {
                UpdateItemSA(world, ref p);
            }
            else
            {
                Log.Warn($"Unknown packet ID: [0x{id:X2}] in 0xF7");

                break;
            }
        }
    }

    private static void ServerListReceived(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.ServerListReceived(ref p);
        }
    }

    private static void ReceiveServerRelay(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.HandleRelayServerPacket(ref p);
        }
    }

    private static void UpdateCharacterList(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.UpdateCharacterList(ref p);
        }
    }

    private static void ReceiveCharacterList(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.ReceiveCharacterList(ref p);
        }
    }

    private static void LoginDelay(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.HandleLoginDelayPacket(ref p);
        }
    }

    private static void ReceiveLoginRejection(World world, ref SpanReader p)
    {
        if (world.InGame)
        {
            return;
        }

        LoginScene scene = Client.Game.GetScene<LoginScene>();

        if (scene != null)
        {
            scene.HandleErrorCode(ref p);
        }
    }

    private static void AddItemToContainer(
        World world,
        uint serial,
        ushort graphic,
        ushort amount,
        ushort x,
        ushort y,
        ushort hue,
        uint containerSerial
    )
    {
        if (Client.Game.UO.GameCursor.ItemHold.Serial == serial)
        {
            if (Client.Game.UO.GameCursor.ItemHold.Dropped)
            {
                Console.WriteLine("ADD ITEM TO CONTAINER -- CLEAR HOLD");
                Client.Game.UO.GameCursor.ItemHold.Clear();
            }

            //else if (ItemHold.Graphic == graphic && ItemHold.Amount == amount &&
            //         ItemHold.Container == containerSerial)
            //{
            //    ItemHold.Enabled = false;
            //    ItemHold.Dropped = false;
            //}
        }

        Entity container = world.Get(containerSerial);

        if (container == null)
        {
            Log.Warn($"No container ({containerSerial}) found");

            //container = world.GetOrCreateItem(containerSerial);
            return;
        }

        Item item = world.Items.Get(serial);

        if (SerialHelper.IsMobile(serial))
        {
            world.RemoveMobile(serial, true);
            Log.Warn("AddItemToContainer function adds mobile as Item");
        }

        if (item != null && (container.Graphic != 0x2006 || item.Layer == Layer.Invalid))
        {
            world.RemoveItem(item, true);
        }

        item = world.GetOrCreateItem(serial);
        item.Graphic = graphic;
        item.CheckGraphicChange();
        item.Amount = amount;
        item.FixHue(hue);
        item.X = x;
        item.Y = y;
        item.Z = 0;

        world.RemoveItemFromContainer(item);
        item.Container = containerSerial;
        container.PushToBack(item);

        if (SerialHelper.IsMobile(containerSerial))
        {
            Mobile m = world.Mobiles.Get(containerSerial);
            Item secureBox = m?.GetSecureTradeBox();

            if (secureBox != null)
            {
                UIManager.GetTradingGump(secureBox)?.RequestUpdateContents();
            }
            else
            {
                UIManager.GetGump<PaperDollGump>(containerSerial)?.RequestUpdateContents();
            }
        }
        else if (SerialHelper.IsItem(containerSerial))
        {
            Gump gump = UIManager.GetGump<BulletinBoardGump>(containerSerial);

            if (gump != null)
            {
                NetClient.Socket.SendBulletinBoardRequestMessageSummary(
                    containerSerial,
                    serial
                );
            }
            else
            {
                gump = UIManager.GetGump<SpellbookGump>(containerSerial);

                if (gump == null)
                {
                    gump = UIManager.GetGump<ContainerGump>(containerSerial);

                    if (gump != null)
                    {
                        ((ContainerGump)gump).CheckItemControlPosition(item);
                    }

                    if (ProfileManager.CurrentProfile.GridLootType > 0)
                    {
                        GridLootGump grid_gump = UIManager.GetGump<GridLootGump>(
                            containerSerial
                        );

                        if (
                            grid_gump == null
                            && SerialHelper.IsValid(_requestedGridLoot)
                            && _requestedGridLoot == containerSerial
                        )
                        {
                            grid_gump = new GridLootGump(world, _requestedGridLoot);
                            UIManager.Add(grid_gump);
                            _requestedGridLoot = 0;
                        }

                        grid_gump?.RequestUpdateContents();
                    }
                }

                if (gump != null)
                {
                    if (SerialHelper.IsItem(containerSerial))
                    {
                        ((Item)container).Opened = true;
                    }

                    gump.RequestUpdateContents();
                }
            }
        }

        UIManager.GetTradingGump(containerSerial)?.RequestUpdateContents();
    }

    private static void UpdateGameObject(
        World world,
        uint serial,
        ushort graphic,
        byte graphic_inc,
        ushort count,
        ushort x,
        ushort y,
        sbyte z,
        Direction direction,
        ushort hue,
        Flags flagss,
        int UNK,
        byte type,
        ushort UNK_2
    )
    {
        Mobile mobile = null;
        Item item = null;
        Entity obj = world.Get(serial);

        if (
            Client.Game.UO.GameCursor.ItemHold.Enabled
            && Client.Game.UO.GameCursor.ItemHold.Serial == serial
        )
        {
            if (SerialHelper.IsValid(Client.Game.UO.GameCursor.ItemHold.Container))
            {
                if (Client.Game.UO.GameCursor.ItemHold.Layer == 0)
                {
                    UIManager
                        .GetGump<ContainerGump>(Client.Game.UO.GameCursor.ItemHold.Container)
                        ?.RequestUpdateContents();
                }
                else
                {
                    UIManager
                        .GetGump<PaperDollGump>(Client.Game.UO.GameCursor.ItemHold.Container)
                        ?.RequestUpdateContents();
                }
            }

            Client.Game.UO.GameCursor.ItemHold.UpdatedInWorld = true;
        }

        bool created = false;

        if (obj == null || obj.IsDestroyed)
        {
            created = true;

            if (SerialHelper.IsMobile(serial) && type != 3)
            {
                mobile = world.GetOrCreateMobile(serial);

                if (mobile == null)
                {
                    return;
                }

                obj = mobile;
                mobile.Graphic = (ushort)(graphic + graphic_inc);
                mobile.CheckGraphicChange();
                mobile.Direction = direction & Direction.Up;
                mobile.FixHue(hue);
                mobile.X = x;
                mobile.Y = y;
                mobile.Z = z;
                mobile.Flags = flagss;
            }
            else
            {
                item = world.GetOrCreateItem(serial);

                if (item == null)
                {
                    return;
                }

                obj = item;
            }
        }
        else
        {
            if (obj is Item item1)
            {
                item = item1;

                if (SerialHelper.IsValid(item.Container))
                {
                    world.RemoveItemFromContainer(item);
                }
            }
            else
            {
                mobile = (Mobile)obj;
            }
        }

        if (obj == null)
        {
            return;
        }

        if (item != null)
        {
            if (graphic != 0x2006)
            {
                graphic += graphic_inc;
            }

            if (type == 2)
            {
                item.IsMulti = true;
                item.WantUpdateMulti =
                    (graphic & 0x3FFF) != item.Graphic
                    || item.X != x
                    || item.Y != y
                    || item.Z != z
                    || item.Hue != hue;
                item.Graphic = (ushort)(graphic & 0x3FFF);
            }
            else
            {
                item.IsDamageable = type == 3;
                item.IsMulti = false;
                item.Graphic = graphic;
            }

            item.X = x;
            item.Y = y;
            item.Z = z;
            item.LightID = (byte)direction;

            if (graphic == 0x2006)
            {
                item.Layer = (Layer)direction;
            }

            item.FixHue(hue);

            if (count == 0)
            {
                count = 1;
            }

            item.Amount = count;
            item.Flags = flagss;
            item.Direction = direction;
            item.CheckGraphicChange(item.AnimIndex);
        }
        else
        {
            graphic += graphic_inc;

            if (serial != world.Player)
            {
                Direction cleaned_dir = direction & Direction.Up;
                bool isrun = (direction & Direction.Running) != 0;

                if (world.Get(mobile) == null || mobile.X == 0xFFFF && mobile.Y == 0xFFFF)
                {
                    mobile.X = x;
                    mobile.Y = y;
                    mobile.Z = z;
                    mobile.Direction = cleaned_dir;
                    mobile.IsRunning = isrun;
                    mobile.ClearSteps();
                }

                if (!mobile.EnqueueStep(x, y, z, cleaned_dir, isrun))
                {
                    mobile.X = x;
                    mobile.Y = y;
                    mobile.Z = z;
                    mobile.Direction = cleaned_dir;
                    mobile.IsRunning = isrun;
                    mobile.ClearSteps();
                }
            }

            mobile.Graphic = (ushort)(graphic & 0x3FFF);
            mobile.FixHue(hue);
            mobile.Flags = flagss;
        }

        if (created && !obj.IsClicked)
        {
            if (mobile != null)
            {
                if (ProfileManager.CurrentProfile.ShowNewMobileNameIncoming)
                {
                    GameActions.SingleClick(world, serial);
                }
            }
            else if (graphic == 0x2006)
            {
                if (ProfileManager.CurrentProfile.ShowNewCorpseNameIncoming)
                {
                    GameActions.SingleClick(world, serial);
                }
            }
        }

        if (mobile != null)
        {
            mobile.SetInWorldTile(mobile.X, mobile.Y, mobile.Z);

            if (created)
            {
                // This is actually a way to get all Hp from all new mobiles.
                // Real UO client does it only when LastAttack == serial.
                // We force to close suddenly.
                GameActions.RequestMobileStatus(world, serial);

                //if (TargetManager.LastAttack != serial)
                //{
                //    GameActions.SendCloseStatus(serial);
                //}
            }
        }
        else
        {
            if (
                Client.Game.UO.GameCursor.ItemHold.Serial == serial
                && Client.Game.UO.GameCursor.ItemHold.Dropped
            )
            {
                // we want maintain the item data due to the denymoveitem packet
                //ItemHold.Clear();
                Client.Game.UO.GameCursor.ItemHold.Enabled = false;
                Client.Game.UO.GameCursor.ItemHold.Dropped = false;
            }

            if (item.OnGround)
            {
                item.SetInWorldTile(item.X, item.Y, item.Z);

                if (graphic == 0x2006 && ProfileManager.CurrentProfile.AutoOpenCorpses)
                {
                    world.Player.TryOpenCorpses();
                }
            }
        }
    }

    private static void UpdatePlayer(
        World world,
        uint serial,
        ushort graphic,
        byte graph_inc,
        ushort hue,
        Flags flags,
        ushort x,
        ushort y,
        sbyte z,
        ushort serverID,
        Direction direction
    )
    {
        if (serial == world.Player)
        {
            world.RangeSize.X = x;
            world.RangeSize.Y = y;

            bool olddead = world.Player.IsDead;
            ushort old_graphic = world.Player.Graphic;

            world.Player.CloseBank();
            world.Player.Walker.WalkingFailed = false;
            world.Player.Graphic = graphic;
            world.Player.Direction = direction & Direction.Mask;
            world.Player.FixHue(hue);
            world.Player.Flags = flags;
            world.Player.Walker.DenyWalk(0xFF, -1, -1, -1);

            GameScene gs = Client.Game.GetScene<GameScene>();

            if (gs != null)
            {
                world.Weather.Reset();
                gs.UpdateDrawPosition = true;
            }

            // std client keeps the target open!
            /*if (old_graphic != 0 && old_graphic != world.Player.Graphic)
            {
                if (world.Player.IsDead)
                {
                    TargetManager.Reset();
                }
            }*/

            if (olddead != world.Player.IsDead)
            {
                if (world.Player.IsDead)
                {
                    world.ChangeSeason(Game.Managers.Season.Desolation, 42);
                }
                else
                {
                    world.ChangeSeason(world.OldSeason, world.OldMusicIndex);
                }
            }

            world.Player.Walker.ResendPacketResync = false;
            world.Player.CloseRangedGumps();
            world.Player.SetInWorldTile(x, y, z);
            world.Player.UpdateAbilities();
        }
    }

    private static void ClearContainerAndRemoveItems(
        World world,
        Entity container,
        bool remove_unequipped = false
    )
    {
        if (container == null || container.IsEmpty)
        {
            return;
        }

        LinkedObject first = container.Items;
        LinkedObject new_first = null;

        while (first != null)
        {
            LinkedObject next = first.Next;
            Item it = (Item)first;

            if (remove_unequipped && it.Layer != 0)
            {
                if (new_first == null)
                {
                    new_first = first;
                }
            }
            else
            {
                world.RemoveItem(it, true);
            }

            first = next;
        }

        container.Items = remove_unequipped ? new_first : null;
    }

    private static Gump CreateGump(
        World world,
        uint sender,
        uint gumpID,
        int x,
        int y,
        string layout,
        string[] lines
    )
    {
        List<string> cmdlist = _parser.GetTokens(layout);
        int cmdlen = cmdlist.Count;

        if (cmdlen <= 0)
        {
            return null;
        }

        Gump gump = null;
        bool mustBeAdded = true;

        if (UIManager.GetGumpCachePosition(gumpID, out Point pos))
        {
            x = pos.X;
            y = pos.Y;

            for (
                LinkedListNode<Gump> last = UIManager.Gumps.Last;
                last != null;
                last = last.Previous
            )
            {
                Control g = last.Value;

                if (!g.IsDisposed && g.LocalSerial == sender && g.ServerSerial == gumpID)
                {
                    g.Clear();
                    gump = g as Gump;
                    mustBeAdded = false;

                    break;
                }
            }
        }
        else
        {
            UIManager.SavePosition(gumpID, new Point(x, y));
        }

        if (gump == null)
        {
            gump = new Gump(world, sender, gumpID)
            {
                X = x,
                Y = y,
                CanMove = true,
                CanCloseWithRightClick = true,
                CanCloseWithEsc = true,
                InvalidateContents = false,
                IsFromServer = true
            };
        }

        int group = 0;
        int page = 0;

        bool textBoxFocused = false;

        for (int cnt = 0; cnt < cmdlen; cnt++)
        {
            List<string> gparams = _cmdparser.GetTokens(cmdlist[cnt], false);

            if (gparams.Count == 0)
            {
                continue;
            }

            string entry = gparams[0];

            if (string.Equals(entry, "button", StringComparison.InvariantCultureIgnoreCase))
            {
                gump.Add(new Button(gparams), page);
            }
            else if (
                string.Equals(
                    entry,
                    "buttontileart",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                gump.Add(new ButtonTileArt(gparams), page);
            }
            else if (
                string.Equals(
                    entry,
                    "checkertrans",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                var checkerTrans = new CheckerTrans(gparams);
                gump.Add(checkerTrans, page);
                ApplyTrans(
                    gump,
                    page,
                    checkerTrans.X,
                    checkerTrans.Y,
                    checkerTrans.Width,
                    checkerTrans.Height
                );
            }
            else if (
                string.Equals(entry, "croppedtext", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(new CroppedText(gparams, lines), page);
            }
            else if (
                string.Equals(entry, "gumppic", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                GumpPic pic;
                var isVirtue = gparams.Count >= 6
                    && gparams[5].IndexOf(
                        "virtuegumpitem",
                        StringComparison.InvariantCultureIgnoreCase
                    ) >= 0;

                if (isVirtue)
                {
                    pic = new VirtueGumpPic(world, gparams);
                    pic.ContainsByBounds = true;

                    string s,
                        lvl;

                    switch (pic.Hue)
                    {
                        case 2403:
                            lvl = "";

                            break;

                        case 1154:
                        case 1547:
                        case 2213:
                        case 235:
                        case 18:
                        case 2210:
                        case 1348:
                            lvl = "Seeker of ";

                            break;

                        case 2404:
                        case 1552:
                        case 2216:
                        case 2302:
                        case 2118:
                        case 618:
                        case 2212:
                        case 1352:
                            lvl = "Follower of ";

                            break;

                        case 43:
                        case 53:
                        case 1153:
                        case 33:
                        case 318:
                        case 67:
                        case 98:
                            lvl = "Knight of ";

                            break;

                        case 2406:
                            if (pic.Graphic == 0x6F)
                            {
                                lvl = "Seeker of ";
                            }
                            else
                            {
                                lvl = "Knight of ";
                            }

                            break;

                        default:
                            lvl = "";

                            break;
                    }

                    switch (pic.Graphic)
                    {
                        case 0x69:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 2);

                            break;

                        case 0x6A:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 7);

                            break;

                        case 0x6B:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 5);

                            break;

                        case 0x6D:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 6);

                            break;

                        case 0x6E:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 1);

                            break;

                        case 0x6F:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 3);

                            break;

                        case 0x70:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + 4);

                            break;

                        case 0x6C:
                        default:
                            s = Client.Game.UO.FileManager.Clilocs.GetString(1051000);

                            break;
                    }

                    if (string.IsNullOrEmpty(s))
                    {
                        s = "Unknown virtue";
                    }

                    pic.SetTooltip(lvl + s, 100);
                }
                else
                {
                    pic = new GumpPic(gparams);
                }

                gump.Add(pic, page);
            }
            else if (
                string.Equals(
                    entry,
                    "gumppictiled",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                gump.Add(new GumpPicTiled(gparams), page);
            }
            else if (
                string.Equals(entry, "htmlgump", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(new HtmlControl(gparams, lines), page);
            }
            else if (
                string.Equals(entry, "xmfhtmlgump", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(
                    new HtmlControl(
                        int.Parse(gparams[1]),
                        int.Parse(gparams[2]),
                        int.Parse(gparams[3]),
                        int.Parse(gparams[4]),
                        int.Parse(gparams[6]) == 1,
                        int.Parse(gparams[7]) != 0,
                        gparams[6] != "0" && gparams[7] == "2",
                        Client.Game.UO.FileManager.Clilocs.GetString(int.Parse(gparams[5].Replace("#", ""))),
                        0,
                        true
                    )
                    {
                        IsFromServer = true
                    },
                    page
                );
            }
            else if (
                string.Equals(
                    entry,
                    "xmfhtmlgumpcolor",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                int color = int.Parse(gparams[8]);

                if (color == 0x7FFF)
                {
                    color = 0x00FFFFFF;
                }

                gump.Add(
                    new HtmlControl(
                        int.Parse(gparams[1]),
                        int.Parse(gparams[2]),
                        int.Parse(gparams[3]),
                        int.Parse(gparams[4]),
                        int.Parse(gparams[6]) == 1,
                        int.Parse(gparams[7]) != 0,
                        gparams[6] != "0" && gparams[7] == "2",
                        Client.Game.UO.FileManager.Clilocs.GetString(int.Parse(gparams[5].Replace("#", ""))),
                        color,
                        true
                    )
                    {
                        IsFromServer = true
                    },
                    page
                );
            }
            else if (
                string.Equals(entry, "xmfhtmltok", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                int color = int.Parse(gparams[7]);

                if (color == 0x7FFF)
                {
                    color = 0x00FFFFFF;
                }

                StringBuilder sb = null;

                if (gparams.Count >= 9)
                {
                    sb = new StringBuilder();

                    for (int i = 9; i < gparams.Count; i++)
                    {
                        sb.Append('\t');
                        sb.Append(gparams[i]);
                    }
                }

                gump.Add(
                    new HtmlControl(
                        int.Parse(gparams[1]),
                        int.Parse(gparams[2]),
                        int.Parse(gparams[3]),
                        int.Parse(gparams[4]),
                        int.Parse(gparams[5]) == 1,
                        int.Parse(gparams[6]) != 0,
                        gparams[5] != "0" && gparams[6] == "2",
                        sb == null
                            ? Client.Game.UO.FileManager.Clilocs.GetString(
                                int.Parse(gparams[8].Replace("#", ""))
                            )
                            : Client.Game.UO.FileManager.Clilocs.Translate(
                                int.Parse(gparams[8].Replace("#", "")),
                                sb.ToString().Trim('@').Replace('@', '\t')
                            ),
                        color,
                        true
                    )
                    {
                        IsFromServer = true
                    },
                    page
                );
            }
            else if (string.Equals(entry, "page", StringComparison.InvariantCultureIgnoreCase))
            {
                if (gparams.Count >= 2)
                {
                    page = int.Parse(gparams[1]);
                }
            }
            else if (
                string.Equals(entry, "resizepic", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(new ResizePic(gparams), page);
            }
            else if (string.Equals(entry, "text", StringComparison.InvariantCultureIgnoreCase))
            {
                if (gparams.Count >= 5)
                {
                    gump.Add(new Label(gparams, lines), page);
                }
            }
            else if (
                string.Equals(
                    entry,
                    "textentrylimited",
                    StringComparison.InvariantCultureIgnoreCase
                )
                || string.Equals(
                    entry,
                    "textentry",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                StbTextBox textBox = new StbTextBox(gparams, lines);

                if (!textBoxFocused)
                {
                    textBox.SetKeyboardFocus();
                    textBoxFocused = true;
                }

                gump.Add(textBox, page);
            }
            else if (
                string.Equals(entry, "tilepichue", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(entry, "tilepic", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(new StaticPic(gparams), page);
            }
            else if (
                string.Equals(entry, "noclose", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.CanCloseWithRightClick = false;
            }
            else if (
                string.Equals(entry, "nodispose", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.CanCloseWithEsc = false;
            }
            else if (
                string.Equals(entry, "nomove", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.CanMove = false;
            }
            else if (
                string.Equals(entry, "group", StringComparison.InvariantCultureIgnoreCase)
                || string.Equals(entry, "endgroup", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                group++;
            }
            else if (string.Equals(entry, "radio", StringComparison.InvariantCultureIgnoreCase))
            {
                gump.Add(new RadioButton(group, gparams, lines), page);
            }
            else if (
                string.Equals(entry, "checkbox", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.Add(new Checkbox(gparams, lines), page);
            }
            else if (
                string.Equals(entry, "tooltip", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                string text = null;

                if (gparams.Count > 2 && gparams[2].Length != 0)
                {
                    string args = gparams[2];

                    for (int i = 3; i < gparams.Count; i++)
                    {
                        args += '\t' + gparams[i];
                    }

                    if (args.Length == 0)
                    {
                        text = Client.Game.UO.FileManager.Clilocs.GetString(int.Parse(gparams[1]));
                        Log.Error(
                            $"String '{args}' too short, something wrong with gump tooltip: {text}"
                        );
                    }
                    else
                    {
                        text = Client.Game.UO.FileManager.Clilocs.Translate(
                            int.Parse(gparams[1]),
                            args,
                            false
                        );
                    }
                }
                else
                {
                    text = Client.Game.UO.FileManager.Clilocs.GetString(int.Parse(gparams[1]));
                }

                Control last =
                    gump.Children.Count != 0 ? gump.Children[gump.Children.Count - 1] : null;

                if (last != null)
                {
                    if (last.HasTooltip)
                    {
                        if (last.Tooltip is string s)
                        {
                            s += '\n' + text;
                            last.SetTooltip(s);
                        }
                    }
                    else
                    {
                        last.SetTooltip(text);
                    }

                    last.Priority = ClickPriority.High;
                    last.AcceptMouseInput = true;
                }
            }
            else if (
                string.Equals(
                    entry,
                    "itemproperty",
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                if (world.ClientFeatures.TooltipsEnabled && gump.Children.Count != 0)
                {
                    gump.Children[gump.Children.Count - 1].SetTooltip(
                        SerialHelper.Parse(gparams[1])
                    );

                    if (
                        uint.TryParse(gparams[1], out uint s)
                        && (!world.OPL.TryGetRevision(s, out uint rev) || rev == 0)
                    )
                    {
                        AddMegaClilocRequest(s);
                    }
                }
            }
            else if (
                string.Equals(entry, "noresize", StringComparison.InvariantCultureIgnoreCase)
            ) { }
            else if (
                string.Equals(entry, "mastergump", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                gump.MasterGumpSerial = gparams.Count > 0 ? SerialHelper.Parse(gparams[1]) : 0;
            }
            else if (
                string.Equals(entry, "picinpic", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                if (gparams.Count > 7)
                {
                    gump.Add(new GumpPicInPic(gparams), page);
                }
            }
            else if (string.Equals(entry, "\0", StringComparison.InvariantCultureIgnoreCase))
            {
                //This gump is null terminated: Breaking
                break;
            }
            else if (string.Equals(entry, "gumppichued", StringComparison.InvariantCultureIgnoreCase) ||
                     string.Equals(entry, "gumppicphued", StringComparison.InvariantCultureIgnoreCase))
            {
                if (gparams.Count >= 3)
                    gump.Add(new GumpPic(gparams));
            }
            else if (string.Equals(entry, "togglelimitgumpscale", StringComparison.InvariantCultureIgnoreCase))
            {
                // ??
            }
            else
            {
                Log.Warn($"Invalid Gump Command: \"{gparams[0]}\"");
            }
        }

        if (mustBeAdded)
        {
            UIManager.Add(gump);
        }

        gump.Update();
        gump.SetInScreen();

        return gump;
    }

    private static void ApplyTrans(
        Gump gump,
        int current_page,
        int x,
        int y,
        int width,
        int height
    )
    {
        int x2 = x + width;
        int y2 = y + height;
        for (int i = 0; i < gump.Children.Count; i++)
        {
            Control child = gump.Children[i];
            bool canDraw = child.Page == 0 || current_page == child.Page;

            bool overlap =
                (x < child.X + child.Width)
                && (child.X < x2)
                && (y < child.Y + child.Height)
                && (child.Y < y2);

            if (canDraw && child.IsVisible && overlap)
            {
                child.Alpha = 0.5f;
            }
        }
    }

    [Flags]
    private enum AffixType
    {
        Append = 0x00,
        Prepend = 0x01,
        System = 0x02
    }
}
