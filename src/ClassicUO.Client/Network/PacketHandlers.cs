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
    private static void SetTime(World world, ref SpanReader p) { }
    private static void Help(World world, ref SpanReader p) { }
    private static void UltimaMessengerR(World world, ref SpanReader p) { }
    private static void AssistVersion(World world, ref SpanReader p) { }
    private static void Semivisible(World world, ref SpanReader p) { }
    private static void InvalidMapEnable(World world, ref SpanReader p) { }
    private static void GetUserServerPingGodClientR(World world, ref SpanReader p) { }
    private static void GlobalQueCount(World world, ref SpanReader p) { }
    private static void ConfigurationFileR(World world, ref SpanReader p) { }
    private static void GenericAOSCommandsR(World world, ref SpanReader p) { }
    private static void CharacterTransferLog(World world, ref SpanReader p) { }
    private static void KREncryptionResponse(World world, ref SpanReader p) { }
    private static void FreeshardListR(World world, ref SpanReader p) { }

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
