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
using ClassicUO.Core;
using ClassicUO.Extensions;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO.Buffers;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;
using System.Buffers;
using Lock = System.Threading.Lock;

namespace ClassicUO.Network.Packets;

#nullable enable

internal static partial class IncomingPackets
{
    private static Serial _requestedGridLoot;

    private static readonly PacketHandlerData[] _handlers = new PacketHandlerData[0x100];
    private static readonly ExtendedPacketHandlerData[] _extendedHandlers = new ExtendedPacketHandlerData[0x100];
    private static readonly PacketLogger _packetLogger = new();
    private static readonly CircularBuffer _buffer = new();
    private static readonly CircularBuffer _pluginsBuffer = new();
    private static byte[] _readingBuffer = new byte[4096];
    private static readonly Lock _pluginLock = new();

    public static int ParsePackets(NetClient socket, World world)
    {
        Span<byte> data = socket.CollectAvailableData();
        if (!data.IsEmpty)
        {
            _buffer.Enqueue(data);
            socket.CommitReadData(data.Length);
        }

        return ParsePackets(world, _buffer, true) + ParsePackets(world, _pluginsBuffer, false);
    }

    private static unsafe int ParsePackets(World world, CircularBuffer stream, bool allowPlugins)
    {
        int packetsCount = 0;

        lock (stream)
        {
            ref byte[] packetBuffer = ref _readingBuffer;

            while (stream.Length > 0)
            {
                if (!GetPacketInfo(stream, out int packetlength, out bool dynamicLength, out delegate*<World, ref SpanReader, void> handler))
                    break;

                while (packetlength > packetBuffer.Length)
                {
                    Array.Resize(ref packetBuffer, packetBuffer.Length * 2);
                }

                _ = stream.Dequeue(packetBuffer, 0, packetlength);

                PacketLogger.Default?.Log(packetBuffer.AsSpan(0, packetlength), false);

                // TODO: the pluging function should allow Span<byte> or unsafe type only.
                // The current one is a bad style decision.
                // It will be fixed once the new plugin system is done.
                if (allowPlugins && !Plugin.ProcessRecvPacket(packetBuffer, ref packetlength))
                    continue;

                SpanReader reader = new(packetBuffer.AsSpan(0, packetlength));
                reader.Skip(dynamicLength ? 3 : 1);

                handler(world, ref reader);
                packetsCount++;
            }
        }

        return packetsCount;
    }

    public static void Append(Span<byte> data)
    {
        if (data.IsEmpty)
            return;

        lock (_pluginLock)
        {
            _pluginsBuffer.Enqueue(data);
        }
    }

    public static unsafe short GetPacketLength(byte id)
    {
        return _handlers[id].Length;
    }

    private static unsafe bool GetPacketInfo(CircularBuffer buffer, out int packetLength, out bool dynamicLength,
        out delegate*<World, ref SpanReader, void> handler)
    {
        packetLength = 0;
        dynamicLength = false;
        handler = default;

        if (buffer.Length <= 0)
            return false;

        int packetId = buffer[0];
        ref readonly PacketHandlerData handlerData = ref _handlers[packetId];

        if (handlerData.Handler is null)
        {
            Log.Warn($"Invalid packet ID: {packetId:X2} | stream.pos: {buffer.Length}");
            return false;
        }

        packetLength = handlerData.Length;

        if (packetLength == 0)
        {
            dynamicLength = true;

            if (buffer.Length < 3)
            {
                Log.Warn($"need more data ID: {packetId:X2} | off: 3 | len: dynamic | stream.pos: {buffer.Length}");
                return false;
            }

            packetLength = (buffer[1] << 8) | buffer[2];
        }

        if (packetLength > buffer.Length)
        {
            Log.Warn($"need more data ID: {packetId:X2} | off: {(dynamicLength ? 3 : 1)} | len: {packetLength} | stream.pos: {buffer.Length}");
            return false;
        }

        handler = handlerData.Handler;

        return true;
    }

    private static unsafe void ReadUnsafeCustomHouseData(ReadOnlySpan<byte> source, int sourcePosition, int dlen, int clen, int planeZ,
        int planeMode, short minX, short minY, short maxY, Item item, House house)
    {
        bool ismovable = item.ItemData.IsMultiMovable;

        byte[]? buffer = null;
        Span<byte> span = dlen <= 1024 ? stackalloc byte[dlen] : (buffer = ArrayPool<byte>.Shared.Rent(dlen));

        try
        {
            ZLib.ZLibError result = ZLib.Decompress(source.Slice(sourcePosition, clen), span[..dlen]);
            SpanReader reader = new(span[..dlen]);

            ushort id = 0;
            sbyte x = 0;
            sbyte y = 0;
            sbyte z = 0;

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
                            house.Add(id, 0, (ushort)(item.X + x), (ushort)(item.Y + y), (sbyte)(item.Z + z), true, ismovable);
                    }

                    break;

                case 1:

                    if (planeZ > 0)
                        z = (sbyte)((planeZ - 1) % 4 * 20 + 7);
                    else
                        z = 0;

                    c = dlen >> 2;

                    for (uint i = 0; i < c; i++)
                    {
                        id = reader.ReadUInt16BE();
                        x = reader.ReadInt8();
                        y = reader.ReadInt8();

                        if (id != 0)
                            house.Add(id, 0, (ushort)(item.X + x), (ushort)(item.Y + y), (sbyte)(item.Z + z), true, ismovable);
                    }

                    break;

                case 2:
                    short offX = 0;
                    short offY = 0;
                    short multiHeight = 0;

                    if (planeZ > 0)
                        z = (sbyte)((planeZ - 1) % 4 * 20 + 7);
                    else
                        z = 0;

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
                            house.Add(id, 0, (ushort)(item.X + x), (ushort)(item.Y + y), (sbyte)(item.Z + z), true, ismovable);
                    }

                    break;
            }
        }
        finally
        {
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void AddItemToContainer(World world, Serial serial, ushort graphic, ushort amount, ushort x, ushort y,
        ushort hue, Serial containerSerial)
    {
        if (Client.Game.UO.GameCursor.ItemHold is { Dropped: true } itemHold && itemHold.Serial == serial)
        {
            Console.WriteLine("ADD ITEM TO CONTAINER -- CLEAR HOLD");
            itemHold.Clear();
        }

        Entity? container = world.Get(containerSerial);

        if (container is null)
        {
            Log.Warn($"No container ({containerSerial}) found");
            return;
        }

        Item? item = world.Items.Get(serial);

        if (serial.IsMobile)
        {
            world.RemoveMobile(serial, true);
            Log.Warn("AddItemToContainer function adds mobile as Item");
        }

        if (item is not null && (container.Graphic != 0x2006 || item.Layer == Layer.Invalid))
            world.RemoveItem(item.Serial, true);

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

        if (containerSerial.IsMobile)
        {
            Mobile? m = world.Mobiles.Get(containerSerial);
            Item? secureBox = m?.GetSecureTradeBox();

            if (secureBox is not null)
                UIManager.GetTradingGump(secureBox)?.RequestUpdateContents();
            else
                UIManager.GetGump<PaperDollGump>(containerSerial)?.RequestUpdateContents();
        }
        else if (containerSerial.IsItem)
        {
            Gump? gump = UIManager.GetGump<BulletinBoardGump>(containerSerial);

            if (gump is not null)
            {
                NetClient.Socket.SendBulletinBoardRequestMessageSummary(containerSerial, serial);
            }
            else
            {
                gump = UIManager.GetGump<SpellbookGump>(containerSerial);

                if (gump is null)
                {
                    gump = UIManager.GetGump<ContainerGump>(containerSerial);

                    if (gump is not null)
                        ((ContainerGump)gump).CheckItemControlPosition(item);

                    if (ProfileManager.CurrentProfile.GridLootType > 0)
                    {
                        GridLootGump? grid_gump = UIManager.GetGump<GridLootGump>(containerSerial);

                        if (grid_gump is null && _requestedGridLoot.IsEntity && _requestedGridLoot == containerSerial)
                        {
                            grid_gump = new GridLootGump(world, _requestedGridLoot);
                            UIManager.Add(grid_gump);
                            _requestedGridLoot = Serial.Zero;
                        }

                        grid_gump?.RequestUpdateContents();
                    }
                }

                if (gump is not null)
                {
                    if (containerSerial.IsItem)
                        ((Item)container).Opened = true;

                    gump.RequestUpdateContents();
                }
            }
        }

        UIManager.GetTradingGump(containerSerial)?.RequestUpdateContents();
    }

    private static void UpdateGameObject(World world, Serial serial, ushort graphic, byte graphicInc, ushort count,
        ushort x, ushort y, sbyte z, Direction direction, ushort hue, Flags flagss, byte type)
    {
        Mobile? mobile = null;
        Item? item = null;
        Entity? obj = world.Get(serial);

        if (Client.Game.UO.GameCursor.ItemHold is { Enabled: true } itemHold && itemHold.Serial == serial)
        {
            if (itemHold.Container.IsEntity)
            {
                if (itemHold.Layer == 0)
                    UIManager.GetGump<ContainerGump>(itemHold.Container)?.RequestUpdateContents();
                else
                    UIManager.GetGump<PaperDollGump>(itemHold.Container)?.RequestUpdateContents();
            }

            itemHold.UpdatedInWorld = true;
        }

        bool created = false;

        if (obj is not { IsDestroyed: false })
        {
            created = true;

            if (serial.IsMobile && type != 3)
            {
                mobile = world.GetOrCreateMobile(serial);
                if (mobile is null)
                    return;

                obj = mobile;
                mobile.Graphic = (ushort)(graphic + graphicInc);
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
                if (item is null)
                    return;

                obj = item;
            }
        }
        else
        {
            if (obj is Item item1)
            {
                item = item1;

                if (item.Container.IsEntity)
                    world.RemoveItemFromContainer(item);
            }
            else
            {
                mobile = (Mobile)obj;
            }
        }

        if (obj is null)
            return;

        if (item is not null)
        {
            if (graphic != 0x2006)
                graphic += graphicInc;

            if (type == 2)
            {
                item.IsMulti = true;

                item.WantUpdateMulti = (graphic & 0x3FFF) != item.Graphic || item.X != x || item.Y != y
                    || item.Z != z || item.Hue != hue; item.Graphic = (ushort)(graphic & 0x3FFF);
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
                item.Layer = (Layer)direction;

            item.FixHue(hue);

            if (count == 0)
                count = 1;

            item.Amount = count;
            item.Flags = flagss;
            item.Direction = direction;
            item.CheckGraphicChange(item.AnimIndex);
        }
        else
        {
            graphic += graphicInc;

            if (serial != world.Player)
            {
                Direction cleaned_dir = direction & Direction.Up;
                bool isrun = (direction & Direction.Running) != 0;

                if (!world.Has(mobile.Serial) || mobile.X == 0xFFFF && mobile.Y == 0xFFFF)
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
            if (mobile is not null)
            {
                if (ProfileManager.CurrentProfile.ShowNewMobileNameIncoming)
                    GameActions.SingleClick(world, serial);
            }
            else if (graphic == 0x2006)
            {
                if (ProfileManager.CurrentProfile.ShowNewCorpseNameIncoming)
                    GameActions.SingleClick(world, serial);
            }
        }

        if (mobile is not null)
        {
            mobile.SetInWorldTile(mobile.X, mobile.Y, mobile.Z);

            if (created)
            {
                // This is actually a way to get all Hp from all new mobiles.
                // Real UO client does it only when LastAttack == serial.
                // We force to close suddenly.
                GameActions.RequestMobileStatus(world, serial);
            }
        }
        else
        {
            if (Client.Game.UO.GameCursor.ItemHold is { Dropped: true } itmHold && itmHold.Serial == serial)
            {
                // we want maintain the item data due to the denymoveitem packet
                itmHold.Enabled = false;
                itmHold.Dropped = false;
            }

            if (item.OnGround)
            {
                item.SetInWorldTile(item.X, item.Y, item.Z);

                if (graphic == 0x2006 && ProfileManager.CurrentProfile.AutoOpenCorpses)
                    world.Player.TryOpenCorpses();
            }
        }
    }

    private static void UpdatePlayer(World world, Serial serial, ushort graphic, ushort hue, Flags flags, ushort x,
        ushort y, sbyte z, Direction direction)
    {
        if (world.Player is not { } player)
            return;

        if (serial != player)
            return;

        world.RangeSize.X = x;
        world.RangeSize.Y = y;

        bool oldDead = player.IsDead;

        player.CloseBank();
        player.Walker.WalkingFailed = false;
        player.Graphic = graphic;
        player.Direction = direction & Direction.Mask;
        player.FixHue(hue);
        player.Flags = flags;
        player.Walker.DenyWalk(0xFF, -1, -1, -1);

        GameScene? gs = Client.Game.GetScene<GameScene>();
        if (gs is not null)
        {
            world.Weather.Reset();
            gs.UpdateDrawPosition = true;
        }

        if (oldDead != player.IsDead)
        {
            if (player.IsDead)
                world.ChangeSeason(Game.Managers.Season.Desolation, 42);
            else
                world.ChangeSeason(world.OldSeason, world.OldMusicIndex);
        }

        player.Walker.ResendPacketResync = false;
        player.CloseRangedGumps();
        player.SetInWorldTile(x, y, z);
        player.UpdateAbilities();
    }

    private static void ClearContainerAndRemoveItems(World world, Entity container, bool remove_unequipped = false)
    {
        if (container is not { IsEmpty: false })
            return;

        LinkedObject? first = container.Items;
        LinkedObject? new_first = null;

        while (first is not null)
        {
            LinkedObject? next = first.Next;
            Item it = (Item)first;

            if (remove_unequipped && it.Layer != 0)
                new_first ??= first;
            else
                world.RemoveItem(it.Serial, true);

            first = next;
        }

        container.Items = remove_unequipped ? new_first : null;
    }

    [Flags]
    private enum AffixType
    {
        Append = 0x00,
        Prepend = 0x01,
        System = 0x02
    }
}
