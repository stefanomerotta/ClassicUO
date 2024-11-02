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
using ClassicUO.Core;
using ClassicUO.Extensions;
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

namespace ClassicUO.Network.Packets;

#nullable enable

internal sealed partial class IncomingPackets
{
    private static readonly ushort[] _gumpTranscodes =
    [
        0x06E9, // 0x003E - 0
        0, 0, 0, 0, 0,
        0x9CE3, // 0x0044 - 6
        0, 0, 0,
        0x06E8, // 0x0048 - 10
        0x9CDF, // 0x0049 - 11
        0x9CDD, // 0x004A - 12
        0, 0,
        0x06EA, // 0x004D - 15
        0x06E6, // 0x004E - 16
        0x06E5, // 0x004F - 17
        0,
        0x06E7, // 0x0051 - 19
    ];

    // 0x03
    // TODO: REMOVE, seems sent only by clients
    private static void ClientTalk(World world, ref SpanReader p)
    {
        p.ReadUInt8();
    }

    // 0x0B
    private static void Damage(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Entity? entity = world.Get(p.ReadSerial());
        if (entity is null)
            return;

        ushort damage = p.ReadUInt16BE();

        if (damage > 0)
            world.WorldTextManager.AddDamage(entity, damage);
    }

    // 0x11
    private static void CharacterStatus(World world, ref SpanReader p)
    {
        if (world.Player is not { } player)
            return;

        Serial serial = p.ReadSerial();

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        string? oldName = entity.Name;
        entity.Name = p.ReadFixedString<ASCIICP1215>(30);
        entity.Hits = p.ReadUInt16BE();
        entity.HitsMax = p.ReadUInt16BE();

        if (entity.HitsRequest == HitsRequestStatus.Pending)
            entity.HitsRequest = HitsRequestStatus.Received;

        if (!serial.IsMobile)
            return;

        if (entity is not Mobile mobile)
            return;

        mobile.IsRenamable = p.ReadBool();
        byte type = p.ReadUInt8();

        if (type > 0 && p.Position + 1 <= p.Length)
        {
            mobile.IsFemale = p.ReadBool();

            if (mobile == player)
            {
                string? name = player.Name;
                if (!string.IsNullOrEmpty(name) && oldName != name)
                    Client.Game.SetWindowTitle(name);

                ushort str = p.ReadUInt16BE();
                ushort dex = p.ReadUInt16BE();
                ushort @int = p.ReadUInt16BE();
                player.Stamina = p.ReadUInt16BE();
                player.StaminaMax = p.ReadUInt16BE();
                player.Mana = p.ReadUInt16BE();
                player.ManaMax = p.ReadUInt16BE();
                player.Gold = p.ReadUInt32BE();
                player.PhysicalResistance = (short)p.ReadUInt16BE();
                player.Weight = p.ReadUInt16BE();

                if (player.Strength != 0 && ProfileManager.CurrentProfile is { ShowStatsChangedMessage: true })
                {
                    int deltaStr = str - player.Strength;
                    int deltaDex = dex - player.Dexterity;
                    int deltaInt = @int - player.Intelligence;

                    if (deltaStr != 0)
                    {
                        GameActions.Print(world, string.Format(ResGeneral.Your0HasChangedBy1ItIsNow2, ResGeneral.Strength, deltaStr, str),
                            0x0170, MessageType.System, 3, false);
                    }

                    if (deltaDex != 0)
                    {
                        GameActions.Print(world, string.Format(ResGeneral.Your0HasChangedBy1ItIsNow2, ResGeneral.Dexterity, deltaDex, dex),
                            0x0170, MessageType.System, 3, false);
                    }

                    if (deltaInt != 0)
                    {
                        GameActions.Print(world, string.Format(ResGeneral.Your0HasChangedBy1ItIsNow2, ResGeneral.Intelligence, deltaInt, @int),
                            0x0170, MessageType.System, 3, false);
                    }
                }

                player.Strength = str;
                player.Dexterity = dex;
                player.Intelligence = @int;

                if (type >= 5) //ML
                {
                    player.WeightMax = p.ReadUInt16BE();
                    byte race = p.ReadUInt8();

                    if (race == 0)
                        race = 1;

                    player.Race = (RaceType)race;
                }
                else
                {
                    if (Client.Game.UO.Version >= Utility.ClientVersion.CV_500A)
                        player.WeightMax = (ushort)(7 * (player.Strength >> 1) + 40);
                    else
                        player.WeightMax = (ushort)(player.Strength * 4 + 25);
                }

                if (type >= 3) //Renaissance
                {
                    player.StatsCap = (short)p.ReadUInt16BE();
                    player.Followers = p.ReadUInt8();
                    player.FollowersMax = p.ReadUInt8();
                }

                if (type >= 4) //AOS
                {
                    player.FireResistance = (short)p.ReadUInt16BE();
                    player.ColdResistance = (short)p.ReadUInt16BE();
                    player.PoisonResistance = (short)p.ReadUInt16BE();
                    player.EnergyResistance = (short)p.ReadUInt16BE();
                    player.Luck = p.ReadUInt16BE();
                    player.DamageMin = (short)p.ReadUInt16BE();
                    player.DamageMax = (short)p.ReadUInt16BE();
                    player.TithingPoints = p.ReadUInt32BE();
                }

                if (type >= 6)
                {
                    setValue(ref player.MaxPhysicResistence, ref p);
                    setValue(ref player.MaxFireResistence, ref p);
                    setValue(ref player.MaxColdResistence, ref p);
                    setValue(ref player.MaxPoisonResistence, ref p);
                    setValue(ref player.MaxEnergyResistence, ref p);
                    setValue(ref player.DefenseChanceIncrease, ref p);
                    setValue(ref player.MaxDefenseChanceIncrease, ref p);
                    setValue(ref player.HitChanceIncrease, ref p);
                    setValue(ref player.SwingSpeedIncrease, ref p);
                    setValue(ref player.DamageIncrease, ref p);
                    setValue(ref player.LowerReagentCost, ref p);
                    setValue(ref player.SpellDamageIncrease, ref p);
                    setValue(ref player.FasterCastRecovery, ref p);
                    setValue(ref player.FasterCasting, ref p);
                    setValue(ref player.LowerManaCost, ref p);
                }
            }
        }

        if (mobile == player)
        {
            world.UoAssist.SignalHits();
            world.UoAssist.SignalStamina();
            world.UoAssist.SignalMana();
        }

        static void setValue(ref short field, ref SpanReader reader)
        {
            if (reader.Position + 2 > reader.Length)
                field = 0;
            else
                field = (short)reader.ReadUInt16BE();
        }
    }

    // 0x15
    private static void FollowR(World world, ref SpanReader p)
    {
        p.ReadUInt32BE();
        p.ReadUInt32BE();
    }

    // 0x16 & 0x17
    private static void NewHealthbarUpdate(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        if (p[0] == 0x16 && Client.Game.UO.Version < ClientVersion.CV_500A)
            return;

        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

        ushort count = p.ReadUInt16BE();

        for (int i = 0; i < count; i++)
        {
            ushort type = p.ReadUInt16BE();
            bool enabled = p.ReadBool();

            if (type == 1)
            {
                if (enabled)
                {
                    if (Client.Game.UO.Version >= Utility.ClientVersion.CV_7000)
                        mobile.SetSAPoison(true);
                    else
                        mobile.Flags |= Flags.Poisoned;
                }
                else
                {
                    if (Client.Game.UO.Version >= Utility.ClientVersion.CV_7000)
                        mobile.SetSAPoison(false);
                    else
                        mobile.Flags &= ~Flags.Poisoned;
                }
            }
            else if (type == 2)
            {
                if (enabled)
                    mobile.Flags |= Flags.YellowBar;
                else
                    mobile.Flags &= ~Flags.YellowBar;
            }
            else if (type == 3)
            {
                // ???
            }
        }
    }

    // 0x1A
    private static void UpdateItem(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        ushort count = 0;
        byte graphicInc = 0;
        byte direction = 0;
        ushort hue = 0;
        byte flags = 0;
        byte type = 0;

        if (serial.IsVirtual)
        {
            serial = serial.ToEntity();
            count = 1;
        }

        ushort graphic = p.ReadUInt16BE();

        if ((graphic & 0x8000) != 0)
        {
            graphic &= 0x7FFF;
            graphicInc = p.ReadUInt8();
        }

        if (count > 0)
            count = p.ReadUInt16BE();
        else
            count++;

        ushort x = p.ReadUInt16BE();

        if ((x & 0x8000) != 0)
        {
            x &= 0x7FFF;
            direction = 1;
        }

        ushort y = p.ReadUInt16BE();

        if ((y & 0x8000) != 0)
        {
            y &= 0x7FFF;
            hue = 1;
        }

        if ((y & 0x4000) != 0)
        {
            y &= 0x3FFF;
            flags = 1;
        }

        if (direction != 0)
            direction = p.ReadUInt8();

        sbyte z = p.ReadInt8();
        if (hue != 0)
            hue = p.ReadUInt16BE();

        if (flags != 0)
            flags = p.ReadUInt8();

        if (graphic >= 0x4000)
            type = 2;

        UpdateGameObject(world, serial, graphic, graphicInc, count, x, y, z, (Direction)direction, hue, (Flags)flags, type);
    }

    // 0x1B
    private static void EnterWorld(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();

        world.CreatePlayer(serial);

        p.Skip(4);
        world.Player!.Graphic = p.ReadUInt16BE();
        world.Player.CheckGraphicChange();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = (sbyte)p.ReadUInt16BE();

        if (world.Map is null)
            world.MapIndex = 0;

        world.Player.SetInWorldTile(x, y, z);
        world.Player.Direction = (Direction)(p.ReadUInt8() & 0x7);
        world.RangeSize.X = x;
        world.RangeSize.Y = y;

        ClientVersion version = Client.Game.UO.Version;

        if (ProfileManager.CurrentProfile is { UseCustomLightLevel: true })
        {
            world.Light.Overall = ProfileManager.CurrentProfile.LightLevelType == 1
                    ? Math.Min(world.Light.Overall, ProfileManager.CurrentProfile.LightLevel)
                    : ProfileManager.CurrentProfile.LightLevel;
        }

        Client.Game.Audio.UpdateCurrentMusicVolume();

        if (version >= ClientVersion.CV_200)
        {
            if (ProfileManager.CurrentProfile is not null)
            {
                Rectangle bounds = Client.Game.Scene.Camera.Bounds;
                NetClient.Socket.SendGameWindowSize((uint)bounds.Width, (uint)bounds.Height);
            }

            NetClient.Socket.SendLanguage(Settings.GlobalSettings.Language);
        }

        NetClient.Socket.SendClientVersion(Settings.GlobalSettings.ClientVersion);

        GameActions.SingleClick(world, world.Player);
        NetClient.Socket.SendSkillsRequest(world.Player.Serial);

        if (world.Player.IsDead)
            world.ChangeSeason(Game.Managers.Season.Desolation, 42);

        if (version >= ClientVersion.CV_70796 && ProfileManager.CurrentProfile is not null)
            NetClient.Socket.SendShowPublicHouseContent(ProfileManager.CurrentProfile.ShowHouseContent);

        NetClient.Socket.SendToPluginsAllSkills();
        NetClient.Socket.SendToPluginsAllSpells();
    }

    // 0x1C
    private static void Talk(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        Entity? entity = world.Get(serial);
        ushort graphic = p.ReadUInt16BE();
        MessageType type = (MessageType)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        ushort font = p.ReadUInt16BE();
        string name = p.ReadFixedString<ASCIICP1215>(30);
        string text;

        if (p.Length > 44)
        {
            p.Seek(44);
            text = p.ReadString<ASCIICP1215>();
        }
        else
        {
            text = "";
        }

        if (serial == 0 && graphic == 0 && type == MessageType.Regular && font == 0xFFFF && hue == 0xFFFF && name.StartsWith("SYSTEM"))
        {
            NetClient.Socket.SendACKTalk();
            return;
        }

        TextType textType = TextType.SYSTEM;

        if (type == MessageType.System || serial == 0xFFFF_FFFF || serial == 0 ||
            name.Equals("system", StringComparison.CurrentCultureIgnoreCase) && entity is null)
        {
            // do nothing
        }
        else if (entity is not null)
        {
            textType = TextType.OBJECT;

            if (string.IsNullOrEmpty(entity.Name))
                entity.Name = string.IsNullOrEmpty(name) ? text : name;
        }

        world.MessageManager.HandleMessage(entity, text, name, hue, type, (byte)font, textType);
    }

    // 0x1D
    private static void DeleteObject(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        if (world.Player.Serial == serial)
            return;

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        bool updateAbilities = false;

        if (entity is Item item)
        {
            if (item.Container.IsEntity)
            {
                Entity? top = world.Get(item.RootContainer);

                if (top is not null && top == world.Player)
                {
                    updateAbilities = item.Layer is Layer.OneHanded or Layer.TwoHanded;

                    Item? tradeBoxItem = world.Player.GetSecureTradeBox();
                    if (tradeBoxItem is not null)
                        UIManager.GetTradingGump(tradeBoxItem)?.RequestUpdateContents();
                }

                Serial cont = new(item.Container.Value & Serial.MAX_MOBILE_SERIAL);

                if (cont == world.Player && item.Layer == Layer.Invalid)
                    Client.Game.UO.GameCursor.ItemHold.Enabled = false;

                if (item.Layer != Layer.Invalid)
                    UIManager.GetGump<PaperDollGump>(cont)?.RequestUpdateContents();

                UIManager.GetGump<ContainerGump>(cont)?.RequestUpdateContents();

                if (top is not null && top.Graphic == 0x2006 && ProfileManager.CurrentProfile.GridLootType is 1 or 2)
                    UIManager.GetGump<GridLootGump>(cont)?.RequestUpdateContents();

                if (item.Graphic == 0x0EB0)
                {
                    UIManager.GetGump<BulletinBoardItem>(serial)?.Dispose();

                    BulletinBoardGump? bbgump = UIManager.GetGump<BulletinBoardGump>();
                    bbgump?.RemoveBulletinObject(serial);
                }
            }
        }

        if (world.CorpseManager.Exists(Serial.Zero, serial))
            return;

        if (entity is Mobile m)
        {
            world.RemoveMobile(serial, true);
        }
        else
        {
            item = (Item)entity;

            if (item.IsMulti)
                world.HouseManager.Remove(serial);

            Entity? cont = world.Get(item.Container);

            if (cont is not null)
            {
                cont.Remove(item);

                if (item.Layer != Layer.Invalid)
                    UIManager.GetGump<PaperDollGump>(cont.Serial)?.RequestUpdateContents();
            }
            else if (item.IsMulti)
            {
                UIManager.GetGump<MiniMapGump>()?.RequestUpdateContents();
            }

            world.RemoveItem(serial, true);

            if (updateAbilities)
                world.Player.UpdateAbilities();
        }
    }

    // 0x20
    private static void UpdatePlayer(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        byte graphicInc = p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        _ = p.ReadUInt16BE();
        Direction direction = (Direction)p.ReadUInt8();
        sbyte z = p.ReadInt8();

        UpdatePlayer(world, serial, graphic, graphicInc, hue, flags, x, y, z, direction);
    }

    // 0x21
    private static void DenyWalk(World world, ref SpanReader p)
    {
        if (world.Player is not { } player)
            return;

        byte seq = p.ReadUInt8();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        Direction direction = (Direction)p.ReadUInt8();
        direction &= Direction.Up;
        sbyte z = p.ReadInt8();

        player.Walker.DenyWalk(seq, x, y, z);
        player.Direction = direction;

        world.Weather.Reset();
    }

    // 0x22
    private static void ConfirmWalk(World world, ref SpanReader p)
    {
        if (world.Player is not { } player)
            return;

        byte seq = p.ReadUInt8();
        byte noto = (byte)(p.ReadUInt8() & ~0x40);

        if (noto == 0 || noto >= 8)
            noto = 0x01;

        player.NotorietyFlag = (NotorietyFlag)noto;
        player.Walker.ConfirmWalk(seq);
        player.AddToTile();
    }

    // 0x23
    private static void DragAnimation(World world, ref SpanReader p)
    {
        ushort graphic = p.ReadUInt16BE();
        graphic += p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        _ = p.ReadUInt16BE();
        Serial source = p.ReadSerial();
        ushort sourceX = p.ReadUInt16BE();
        ushort sourceY = p.ReadUInt16BE();
        sbyte sourceZ = p.ReadInt8();
        Serial dest = p.ReadSerial();
        ushort destX = p.ReadUInt16BE();
        ushort destY = p.ReadUInt16BE();
        sbyte destZ = p.ReadInt8();

        switch (graphic)
        {
            case 0x0EED: graphic = 0x0EEF; break;
            case 0x0EEA: graphic = 0x0EEC; break;
            case 0x0EF0: graphic = 0x0EF2; break;
        }

        Mobile? entity = world.Mobiles.Get(source);

        if (entity is null)
        {
            source = Serial.Zero;
        }
        else
        {
            sourceX = entity.X;
            sourceY = entity.Y;
            sourceZ = entity.Z;
        }

        Mobile? destEntity = world.Mobiles.Get(dest);

        if (destEntity is null)
        {
            dest = Serial.Zero;
        }
        else
        {
            destX = destEntity.X;
            destY = destEntity.Y;
            destZ = destEntity.Z;
        }

        GraphicEffectType effect = !source.IsEntity || !dest.IsEntity
                ? GraphicEffectType.Moving
                : GraphicEffectType.DragEffect;

        world.SpawnEffect(
            effect,
            source,
            dest,
            graphic,
            hue,
            sourceX,
            sourceY,
            sourceZ,
            destX,
            destY,
            destZ,
            5,
            5000,
            true,
            false,
            false,
            GraphicEffectBlendMode.Normal
        );
    }

    // 0x24
    private static void OpenContainer(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();

        if (graphic == 0xFFFF)
        {
            Item? spellBookItem = world.Items.Get(serial);
            if (spellBookItem is null)
                return;

            UIManager.GetGump<SpellbookGump>(serial)?.Dispose();

            SpellbookGump spellbookGump = new(world, spellBookItem);

            if (!UIManager.GetGumpCachePosition(spellBookItem, out Point location))
                location = new Point(64, 64);

            spellbookGump.Location = location;
            UIManager.Add(spellbookGump);

            Client.Game.Audio.PlaySound(0x0055);
        }
        else if (graphic == 0x0030)
        {
            Mobile? vendor = world.Mobiles.Get(serial);
            if (vendor is null)
                return;

            UIManager.GetGump<ShopGump>(serial)?.Dispose();

            ShopGump gump = new(world, serial, true, 150, 5);
            UIManager.Add(gump);

            for (Layer layer = Layer.ShopBuyRestock; layer < Layer.ShopBuy + 1; layer++)
            {
                Item? item = vendor.FindItemByLayer(layer);
                if (item is not { Items: { } first })
                    continue;

                bool reverse = item.Graphic != 0x2AF8; //hardcoded logic in original client that we must match

                if (reverse)
                {
                    while (first is { Next: not null })
                    {
                        first = first.Next;
                    }
                }

                while (first is not null)
                {
                    Item it = (Item)first;

                    gump.AddItem(it.Serial, it.Graphic, it.Hue, it.Amount, it.Price, it.Name, false);

                    if (reverse)
                        first = first.Previous;
                    else
                        first = first.Next;
                }
            }
        }
        else
        {
            Item? item = world.Items.Get(serial);

            if (item is not null)
            {
                if (item.IsCorpse && ProfileManager.CurrentProfile.GridLootType is 1 or 2)
                {
                    _requestedGridLoot = serial;

                    if (ProfileManager.CurrentProfile.GridLootType == 1)
                        return;
                }

                ContainerGump? container = UIManager.GetGump<ContainerGump>(serial);
                bool playsound = false;
                int x;
                int y;

                if (Client.Game.UO.Version >= ClientVersion.CV_706000
                    && ProfileManager.CurrentProfile is { UseLargeContainerGumps: true })
                {
                    Renderer.Gumps.Gump gumps = Client.Game.UO.Gumps;

                    int transcodeIndex = graphic - 0x003E;

                    if (transcodeIndex >= 0 && transcodeIndex < _gumpTranscodes.Length)
                    {
                        ushort toTranscode = _gumpTranscodes[transcodeIndex];

                        if (toTranscode != 0 && gumps.GetGump(toTranscode).Texture is not null)
                            graphic = toTranscode;
                    }
                }

                if (container is not null)
                {
                    x = container.ScreenCoordinateX;
                    y = container.ScreenCoordinateY;
                    container.Dispose();
                }
                else
                {
                    world.ContainerManager.CalculateContainerPosition(serial, graphic);
                    x = world.ContainerManager.X;
                    y = world.ContainerManager.Y;
                    playsound = true;
                }

                UIManager.Add(new ContainerGump(world, item, graphic, playsound)
                {
                    X = x,
                    Y = y,
                    InvalidateContents = true
                });

                UIManager.RemovePosition(serial);
            }
            else
            {
                Log.Error("[OpenContainer]: item not found");
            }
        }

        if (graphic != 0x0030)
        {
            Item? it = world.Items.Get(serial);

            if (it is null)
                return;

            it.Opened = true;

            if (!it.IsCorpse && graphic != 0xFFFF)
                ClearContainerAndRemoveItems(world, it);
        }
    }

    // 0x25
    private static void UpdateContainedItem(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        ushort graphic = (ushort)(p.ReadUInt16BE() + p.ReadUInt8());
        ushort amount = Math.Max((ushort)1, p.ReadUInt16BE());
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();

        if (Client.Game.UO.Version >= ClientVersion.CV_6017)
            p.Skip(1);

        Serial containerSerial = p.ReadSerial();
        ushort hue = p.ReadUInt16BE();

        AddItemToContainer(world, serial, graphic, amount, x, y, hue, containerSerial);
    }

    // 0x27
    private static void DenyMoveItem(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ItemHold itemHold = Client.Game.UO.GameCursor.ItemHold;
        Item? firstItem = world.Items.Get(itemHold.Serial);

        if (!itemHold.Enabled && (!itemHold.Dropped || firstItem is { AllowedToDraw: true }))
        {
            Log.Warn("There was a problem with ItemHold object. It was cleared before :|");
        }
        else
        {
            if (world.ObjectToRemove == itemHold.Serial)
                world.ObjectToRemove = Serial.Zero;

            if (!itemHold.Serial.IsEntity || itemHold.Graphic == 0xFFFF)
            {
                Log.Error($"Wrong data: serial = {itemHold.Serial:X8}  -  graphic = {itemHold.Graphic:X4}");
            }
            else
            {
                if (!itemHold.UpdatedInWorld)
                {
                    if (itemHold.Layer == Layer.Invalid && itemHold.Container.IsEntity)
                    {
                        // Server should send an UpdateContainedItem after this packet.
                        Console.WriteLine("=== DENY === ADD TO CONTAINER");

                        AddItemToContainer(world, itemHold.Serial, itemHold.Graphic, itemHold.TotalAmount,
                            itemHold.X, itemHold.Y, itemHold.Hue, itemHold.Container);

                        UIManager.GetGump<ContainerGump>(itemHold.Container)?.RequestUpdateContents();
                    }
                    else
                    {
                        Item item = world.GetOrCreateItem(itemHold.Serial);

                        item.Graphic = itemHold.Graphic;
                        item.Hue = itemHold.Hue;
                        item.Amount = itemHold.TotalAmount;
                        item.Flags = itemHold.Flags;
                        item.Layer = itemHold.Layer;
                        item.X = itemHold.X;
                        item.Y = itemHold.Y;
                        item.Z = itemHold.Z;
                        item.CheckGraphicChange();

                        Entity? container = world.Get(itemHold.Container);

                        if (container is not null)
                        {
                            if (container.Serial.IsMobile)
                            {
                                Console.WriteLine("=== DENY === ADD TO PAPERDOLL");

                                world.RemoveItemFromContainer(item);
                                container.PushToBack(item);
                                item.Container = container.Serial;

                                UIManager.GetGump<PaperDollGump>(item.Container)?.RequestUpdateContents();
                            }
                            else
                            {
                                Console.WriteLine("=== DENY === SOMETHING WRONG");
                                world.RemoveItem(item.Serial, true);
                            }
                        }
                        else
                        {
                            Console.WriteLine("=== DENY === ADD TO TERRAIN");
                            world.RemoveItemFromContainer(item);
                            item.SetInWorldTile(item.X, item.Y, item.Z);
                        }
                    }
                }
            }

            UIManager.GetGump<SplitMenuGump>(itemHold.Serial)?.Dispose();

            itemHold.Clear();
        }

        byte code = p.ReadUInt8();

        if (code < 5)
        {
            world.MessageManager.HandleMessage(null, ServerErrorMessages.GetError(p[0], code),
                "", 0x03b2, MessageType.System, 3, TextType.SYSTEM);
        }
    }

    // 0x28
    private static void EndDraggingItem(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ItemHold itemHold = Client.Game.UO.GameCursor.ItemHold;
        itemHold.Enabled = false;
        itemHold.Dropped = false;
    }

    // 0x29
    private static void DropItemAccepted(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ItemHold itemHold = Client.Game.UO.GameCursor.ItemHold;
        itemHold.Enabled = false;
        itemHold.Dropped = false;

        Console.WriteLine("PACKET - ITEM DROP OK!");
    }

    // 0x2C
    private static void DeathScreen(World world, ref SpanReader p)
    {
        byte action = p.ReadUInt8();
        if (action == 1)
            return;

        world.Weather.Reset();

        Client.Game.Audio.PlayMusic(Client.Game.Audio.DeathMusicIndex, true);

        if (ProfileManager.CurrentProfile.EnableDeathScreen)
            world.Player.DeathScreenTimer = Time.Ticks + Constants.DEATH_SCREEN_TIMER;

        GameActions.RequestWarMode(world.Player, false);
    }

    // 0x2D
    private static void MobileAttributes(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        entity.HitsMax = p.ReadUInt16BE();
        entity.Hits = p.ReadUInt16BE();

        if (entity.HitsRequest == HitsRequestStatus.Pending)
            entity.HitsRequest = HitsRequestStatus.Received;

        if (!serial.IsMobile)
            return;

        if (entity is not Mobile mobile)
            return;

        mobile.ManaMax = p.ReadUInt16BE();
        mobile.Mana = p.ReadUInt16BE();
        mobile.StaminaMax = p.ReadUInt16BE();
        mobile.Stamina = p.ReadUInt16BE();

        if (mobile == world.Player)
        {
            world.UoAssist.SignalHits();
            world.UoAssist.SignalStamina();
            world.UoAssist.SignalMana();
        }
    }

    // 0x2E
    private static void EquipItem(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        Item item = world.GetOrCreateItem(serial);

        if (item.Graphic != 0 && item.Layer != Layer.Backpack)
            world.RemoveItemFromContainer(item);

        if (item.Container.IsEntity)
        {
            UIManager.GetGump<ContainerGump>(item.Container)?.RequestUpdateContents();
            UIManager.GetGump<PaperDollGump>(item.Container)?.RequestUpdateContents();
        }

        item.Graphic = (ushort)(p.ReadUInt16BE() + p.ReadInt8());
        item.Layer = (Layer)p.ReadUInt8();
        item.Container = p.ReadSerial();
        item.FixHue(p.ReadUInt16BE());
        item.Amount = 1;

        Entity? entity = world.Get(item.Container);
        entity?.PushToBack(item);

        if (item.Layer is >= Layer.ShopBuyRestock and <= Layer.ShopSell)
        { }
        else if (item.Container.IsEntity && item.Layer < Layer.Mount)
        {
            UIManager.GetGump<PaperDollGump>(item.Container)?.RequestUpdateContents();
        }

        if (entity == world.Player && item.Layer is Layer.OneHanded or Layer.TwoHanded)
            world.Player?.UpdateAbilities();
    }

    // 0x2F
    private static void Swing(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        p.Skip(1);

        Serial attacker = p.ReadSerial();
        if (attacker != world.Player)
            return;

        Serial defender = p.ReadSerial();

        const int TIME_TURN_TO_LASTTARGET = 2000;

        if (world.TargetManager.LastAttack != defender
            || world.Player is not { InWarMode: true, Steps.Count: 0 } player
            || player.Walker.LastStepRequestTime + TIME_TURN_TO_LASTTARGET >= Time.Ticks)
        {
            return;
        }

        Mobile? enemy = world.Mobiles.Get(defender);

        if (enemy is null)
            return;

        Direction pdir = DirectionHelper.GetDirectionAB(player.X, player.Y, enemy.X, enemy.Y);
        int x = player.X;
        int y = player.Y;
        sbyte z = player.Z;

        if (player.Pathfinder.CanWalk(ref pdir, ref x, ref y, ref z) && player.Direction != pdir)
            player.Walk(pdir, false);
    }

    // 0x3A
    private static void UpdateSkills(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        byte type = p.ReadUInt8();
        bool haveCap = type != 0u && type <= 0x03 || type == 0xDF;
        bool isSingleUpdate = type == 0xFF || type == 0xDF;

        if (type == 0xFE)
        {
            int count = p.ReadUInt16BE();

            List<SkillEntry> skills = Client.Game.UO.FileManager.Skills.Skills;
            List<SkillEntry> sortedSkills = Client.Game.UO.FileManager.Skills.SortedSkills;

            skills.Clear();
            sortedSkills.Clear();

            for (int i = 0; i < count; i++)
            {
                bool haveButton = p.ReadBool();
                int nameLength = p.ReadUInt8();

                skills.Add(new SkillEntry(i, p.ReadFixedString<ASCIICP1215>(nameLength), haveButton));
            }

            sortedSkills.AddRange(Client.Game.UO.FileManager.Skills.Skills);
            sortedSkills.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCulture));
        }
        else
        {
            StandardSkillsGump? standard = null;
            SkillGumpAdvanced? advanced = null;

            if (ProfileManager.CurrentProfile.StandardSkillsGump)
                standard = UIManager.GetGump<StandardSkillsGump>();
            else
                advanced = UIManager.GetGump<SkillGumpAdvanced>();

            if (!isSingleUpdate && (type == 1 || type == 3 || world.SkillsRequested))
            {
                world.SkillsRequested = false;

                // TODO: make a base class for this gump
                if (ProfileManager.CurrentProfile.StandardSkillsGump)
                {
                    if (standard is null)
                        UIManager.Add(standard = new StandardSkillsGump(world) { X = 100, Y = 100 });
                }
                else
                {
                    if (advanced is null)
                        UIManager.Add(advanced = new SkillGumpAdvanced(world) { X = 100, Y = 100 });
                }
            }

            while (p.Position < p.Length)
            {
                ushort id = p.ReadUInt16BE();

                if (p.Position >= p.Length)
                    break;

                if (id == 0 && type == 0)
                    break;

                if (type == 0 || type == 0x02)
                    id--;

                ushort realVal = p.ReadUInt16BE();
                ushort baseVal = p.ReadUInt16BE();
                Lock locked = (Lock)p.ReadUInt8();
                ushort cap = 1000;

                if (haveCap)
                    cap = p.ReadUInt16BE();

                if (id < world.Player.Skills.Length)
                {
                    Skill? skill = world.Player.Skills[id];

                    if (skill is not null)
                    {
                        if (isSingleUpdate)
                        {
                            float change = realVal / 10.0f - skill.Value;

                            if (change != 0.0f
                                && !float.IsNaN(change)
                                && ProfileManager.CurrentProfile is { ShowSkillsChangedMessage: true } profile
                                && Math.Abs(change * 10) >= profile.ShowSkillsChangedDeltaValue)
                            {
                                string v = change < 0 ? ResGeneral.Decreased : ResGeneral.Increased;

                                GameActions.Print(world,
                                    string.Format(ResGeneral.YourSkillIn0Has1By2ItIsNow3, skill.Name, v, Math.Abs(change), skill.Value + change),
                                    0x58, MessageType.System, 3, false);
                            }
                        }

                        skill.BaseFixed = baseVal;
                        skill.ValueFixed = realVal;
                        skill.CapFixed = cap;
                        skill.Lock = locked;

                        standard?.Update(id);
                        advanced?.ForceUpdate();
                    }
                }

                if (isSingleUpdate)
                    break;
            }
        }
    }

    // 0x38
    private static void Pathfinding(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        ushort z = p.ReadUInt16BE();

        world.Player?.Pathfinder.WalkTo(x, y, z, 0);
    }

    // 0x3C
    private static void UpdateContainedItems(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ushort count = p.ReadUInt16BE();

        for (int i = 0; i < count; i++)
        {
            Serial serial = p.ReadSerial();
            ushort graphic = (ushort)(p.ReadUInt16BE() + p.ReadUInt8());
            ushort amount = Math.Max(p.ReadUInt16BE(), (ushort)1);
            ushort x = p.ReadUInt16BE();
            ushort y = p.ReadUInt16BE();

            if (Client.Game.UO.Version >= ClientVersion.CV_6017)
                p.Skip(1);

            Serial containerSerial = p.ReadSerial();
            ushort hue = p.ReadUInt16BE();

            if (i == 0)
            {
                Entity? container = world.Get(containerSerial);

                if (container is not null)
                    ClearContainerAndRemoveItems(world, container, container.Graphic == 0x2006);
            }

            AddItemToContainer(world, serial, graphic, amount, x, y, hue, containerSerial);
        }
    }

    // 0x3B
    private static void CloseVendorInterface(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        UIManager.GetGump<ShopGump>(serial)?.Dispose();
    }

    // 0x4E
    private static void PersonalLightLevel(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        if (world.Player != p.ReadSerial())
            return;

        byte level = p.ReadUInt8();

        if (level > 0x1E)
            level = 0x1E;

        world.Light.RealPersonal = level;

        if (!ProfileManager.CurrentProfile.UseCustomLightLevel)
            world.Light.Personal = level;
    }

    // 0x4F
    private static void LightLevel(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        byte level = p.ReadUInt8();

        if (level > 0x1E)
            level = 0x1E;

        world.Light.RealOverall = level;

        if (ProfileManager.CurrentProfile is not { } profile)
            return;

        if (profile.UseCustomLightLevel && profile.LightLevelType != 1)
            return;

        world.Light.Overall = profile.LightLevelType == 1 ? Math.Min(level, profile.LightLevel) : level;
    }

    // 0x53 & 0x82 & 0x85
    private static void ReceiveLoginRejection(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.HandleErrorCode(ref p);
    }

    // 0x54
    private static void PlaySoundEffect(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        p.Skip(1);

        ushort index = p.ReadUInt16BE();
        _ = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        _ = (short)p.ReadUInt16BE();

        Client.Game.Audio.PlaySoundWithDistance(world, index, x, y);
    }

    // 0x55
    private static void LoginComplete(World world, ref SpanReader p)
    {
        if (world.Player is null || Client.Game.Scene is not LoginScene)
            return;

        GameScene scene = new GameScene(world);
        Client.Game.SetScene(scene);

        GameActions.RequestMobileStatus(world, world.Player);
        NetClient.Socket.SendOpenChat("");
        NetClient.Socket.SendSkillsRequest(world.Player);

        scene.DoubleClickDelayed(world.Player);

        if (Client.Game.UO.Version >= ClientVersion.CV_306E)
            NetClient.Socket.SendClientType();

        if (Client.Game.UO.Version >= ClientVersion.CV_305D)
            NetClient.Socket.SendClientViewRange(world.ClientViewRange);

        List<Gump> gumps = ProfileManager.CurrentProfile.ReadGumps(world, ProfileManager.ProfilePath);
        if (gumps is null)
            return;

        foreach (Gump gump in gumps)
        {
            UIManager.Add(gump);
        }
    }

    // 0x56
    private static void MapData(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();

        MapGump? gump = UIManager.GetGump<MapGump>(serial);
        if (gump is null)
            return;

        switch ((MapMessageType)p.ReadUInt8())
        {
            case MapMessageType.Add:
                p.Skip(1);
                ushort x = p.ReadUInt16BE();
                ushort y = p.ReadUInt16BE();
                gump.AddPin(x, y);

                break;

            case MapMessageType.Insert: break;
            case MapMessageType.Move: break;
            case MapMessageType.Remove: break;
            case MapMessageType.Clear: gump.ClearContainer(); break;
            case MapMessageType.Edit: break;
            case MapMessageType.EditResponse: gump.SetPlotState(p.ReadUInt8()); break;
        }
    }

    // 0x65
    private static void SetWeather(World world, ref SpanReader p)
    {
        GameScene? scene = Client.Game.GetScene<GameScene>();
        if (scene is null)
            return;

        WeatherType type = (WeatherType)p.ReadUInt8();
        if (world.Weather.CurrentWeather == type)
            return;

        byte count = p.ReadUInt8();
        byte temp = p.ReadUInt8();

        world.Weather.Generate(type, count, temp);
    }
    // 0x66
    private static void BookData(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        ushort pageCnt = p.ReadUInt16BE();

        ModernBookGump? gump = UIManager.GetGump<ModernBookGump>(serial);
        if (gump is not { IsDisposed: false })
            return;

        for (int i = 0; i < pageCnt; i++)
        {
            int pageNum = p.ReadUInt16BE() - 1;
            gump.KnownPages.Add(pageNum);

            if (pageNum < 0 || pageNum >= gump.BookPageCount)
            {
                Log.Error("BOOKGUMP: The server is sending a page number GREATER than the allowed number of pages in BOOK!");
                continue;
            }

            ushort lineCnt = p.ReadUInt16BE();

            for (int line = 0; line < lineCnt; line++)
            {
                int index = pageNum * ModernBookGump.MAX_BOOK_LINES + line;

                if (index < gump.BookLines.Length)
                {
                    gump.BookLines[index] = ModernBookGump.IsNewBook
                        ? p.ReadString<UTF8>(true)
                        : p.ReadString<ASCIICP1215>();
                }
                else
                {
                    Log.Error("BOOKGUMP: The server is sending a page number GREATER than the allowed number of pages in BOOK!");
                }
            }

            if (lineCnt >= ModernBookGump.MAX_BOOK_LINES)
                continue;

            for (int line = lineCnt; line < ModernBookGump.MAX_BOOK_LINES; line++)
            {
                gump.BookLines[pageNum * ModernBookGump.MAX_BOOK_LINES + line] = "";
            }
        }

        gump.ServerSetBookText();
    }

    // 0x6C
    private static void TargetCursor(World world, ref SpanReader p)
    {
        world.TargetManager.SetTargeting((CursorTarget)p.ReadUInt8(), p.ReadUInt32BE(), (TargetType)p.ReadUInt8());

        PartyManager party = world.Party;

        if (party.PartyHealTimer >= Time.Ticks || party.PartyHealTarget == 0)
            return;

        world.TargetManager.Target(party.PartyHealTarget);
        party.PartyHealTimer = 0;
        party.PartyHealTarget = Serial.Zero;
    }

    // 0x6D
    private static void PlayMusic(World world, ref SpanReader p)
    {
        ushort index = p.ReadUInt16BE();
        Client.Game.Audio.PlayMusic(index);
    }

    // 0x6E
    private static void CharacterAnimation(World world, ref SpanReader p)
    {
        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

        ushort action = p.ReadUInt16BE();
        ushort frameCount = p.ReadUInt16BE();
        ushort repeatCount = p.ReadUInt16BE();
        bool forward = !p.ReadBool();
        bool repeat = p.ReadBool();
        byte delay = p.ReadUInt8();

        mobile.SetAnimation(
            Mobile.GetReplacedObjectAnimation(mobile.Graphic, action),
            delay,
            (byte)frameCount,
            (byte)repeatCount,
            repeat,
            forward,
            true
        );
    }

    // 0x6F
    private static void SecureTrading(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        byte type = p.ReadUInt8();
        Serial serial = p.ReadSerial();

        if (type == 0)
        {
            Serial id1 = p.ReadSerial();
            Serial id2 = p.ReadSerial();

            // standard client doesn't allow the trading system if one of the traders is invisible (=not sent by server)
            if (!world.Has(id1) || !world.Has(id2))
                return;

            bool hasName = p.ReadBool();
            string name = string.Empty;

            if (hasName && p.Position < p.Length)
                name = p.ReadString<ASCIICP1215>();

            UIManager.Add(new TradingGump(world, serial, name, id1, id2));
        }
        else if (type == 1)
        {
            UIManager.GetTradingGump(serial)?.Dispose();
        }
        else if (type == 2)
        {
            uint id1 = p.ReadUInt32BE();
            uint id2 = p.ReadUInt32BE();

            TradingGump? trading = UIManager.GetTradingGump(serial);

            if (trading is not null)
            {
                trading.ImAccepting = id1 != 0;
                trading.HeIsAccepting = id2 != 0;
                trading.RequestUpdateContents();
            }
        }
        else if (type == 3 || type == 4)
        {
            TradingGump? trading = UIManager.GetTradingGump(serial);

            if (trading is not null)
            {
                if (type == 4)
                {
                    trading.Gold = p.ReadUInt32BE();
                    trading.Platinum = p.ReadUInt32BE();
                }
                else
                {
                    trading.HisGold = p.ReadUInt32BE();
                    trading.HisPlatinum = p.ReadUInt32BE();
                }
            }
        }
    }

    // 0x70
    private static void GraphicEffect70(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        GraphicEffectType type = (GraphicEffectType)p.ReadUInt8();

        if (type > GraphicEffectType.FixedFrom)
        {
            if (type == GraphicEffectType.ScreenFade)
            {
                p.Skip(10);
                Log.Warn("Effect not implemented");
            }

            return;
        }

        Serial source = p.ReadSerial();
        Serial target = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        ushort srcX = p.ReadUInt16BE();
        ushort srcY = p.ReadUInt16BE();
        sbyte srcZ = p.ReadInt8();
        ushort targetX = p.ReadUInt16BE();
        ushort targetY = p.ReadUInt16BE();
        sbyte targetZ = p.ReadInt8();
        byte speed = p.ReadUInt8();
        byte duration = p.ReadUInt8();
        _ = p.ReadUInt16BE();
        bool fixedDirection = p.ReadBool();
        bool doesExplode = p.ReadBool();
        uint hue = 0;
        GraphicEffectBlendMode blendmode = 0;

        world.SpawnEffect(type, source, target, graphic, (ushort)hue, srcX, srcY, srcZ, targetX, targetY, targetZ,
            speed, duration, fixedDirection, doesExplode, false, blendmode);
    }

    // 0x71
    private static void BulletinBoardData(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        switch (p.ReadUInt8())
        {
            case 0: // open
                {
                    Serial serial = p.ReadSerial();
                    Item? item = world.Items.Get(serial);
                    if (item is null)
                        return;

                    BulletinBoardGump? bulletinBoard = UIManager.GetGump<BulletinBoardGump>(serial);
                    bulletinBoard?.Dispose();

                    int x = (Client.Game.Window.ClientBounds.Width >> 1) - 245;
                    int y = (Client.Game.Window.ClientBounds.Height >> 1) - 205;

                    bulletinBoard = new BulletinBoardGump(world, item, x, y, p.ReadFixedString<UTF8>(22, true));
                    UIManager.Add(bulletinBoard);

                    item.Opened = true;

                    return;
                }
            case 1: // summary msg
                {
                    Serial boardSerial = p.ReadSerial();
                    BulletinBoardGump? bulletinBoard = UIManager.GetGump<BulletinBoardGump>(boardSerial);
                    if (bulletinBoard == null)
                        return;

                    Serial serial = p.ReadSerial();
                    uint parendID = p.ReadUInt32BE();

                    // poster
                    int len = p.ReadUInt8();
                    string text = (len <= 0 ? string.Empty : p.ReadFixedString<UTF8>(len, true)) + " - ";

                    // subject
                    len = p.ReadUInt8();
                    text += (len <= 0 ? string.Empty : p.ReadFixedString<UTF8>(len, true)) + " - ";

                    // datetime
                    len = p.ReadUInt8();
                    text += (len <= 0 ? string.Empty : p.ReadFixedString<UTF8>(len, true));

                    bulletinBoard.AddBulletinObject(serial, text);

                    return;
                }
            case 2: // message
                {
                    Serial boardSerial = p.ReadSerial();
                    BulletinBoardGump? bulletinBoard = UIManager.GetGump<BulletinBoardGump>(boardSerial);
                    if (bulletinBoard is null)
                        return;

                    uint serial = p.ReadUInt32BE();

                    int len = p.ReadUInt8();
                    string poster = len > 0 ? p.ReadFixedString<ASCIICP1215>(len) : "";

                    len = p.ReadUInt8();
                    string subject = len > 0 ? p.ReadFixedString<UTF8>(len, true) : "";

                    len = p.ReadUInt8();
                    string dataTime = len > 0 ? p.ReadFixedString<ASCIICP1215>(len) : "";

                    p.Skip(4);

                    byte unk = p.ReadUInt8();

                    if (unk > 0)
                        p.Skip(unk * 4);

                    byte lines = p.ReadUInt8();

                    using ValueStringBuilder sb = new(stackalloc char[256]);

                    for (int i = 0; i < lines; i++)
                    {
                        byte lineLen = p.ReadUInt8();

                        if (lineLen > 0)
                        {
                            string putta = p.ReadFixedString<UTF8>(lineLen, true);
                            sb.Append(putta);
                            sb.Append('\n');
                        }
                    }

                    string msg = sb.ToString();
                    byte variant = (byte)(1 + (poster == world.Player.Name ? 1 : 0));

                    UIManager.Add(
                        new BulletinBoardItem(
                            world,
                            boardSerial,
                            serial,
                            poster,
                            subject,
                            dataTime,
                            msg.TrimStart(),
                            variant
                        )
                        {
                            X = 40,
                            Y = 40
                        }
                    );

                    return;
                }
        }
    }

    // 0x72
    private static void Warmode(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        world.Player.InWarMode = p.ReadBool();
    }

    // 0x73
    private static void Ping(World world, ref SpanReader p)
    {
        NetClient.Socket.ReceivePing(p.ReadUInt8());
    }

    // 0x74
    private static void BuyList(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Item? container = world.Items.Get(p.ReadSerial());
        if (container is null)
            return;

        Mobile? vendor = world.Mobiles.Get(container.Container);
        if (vendor is null)
            return;

        ShopGump? gump = UIManager.GetGump<ShopGump>();
        if (gump is not null && (gump.LocalSerial != vendor || !gump.IsBuyGump))
        {
            gump.Dispose();
            gump = null;
        }

        if (gump is null)
        {
            gump = new ShopGump(world, vendor, true, 150, 5);
            UIManager.Add(gump);
        }

        if (container.Layer != Layer.ShopBuyRestock && container.Layer != Layer.ShopBuy)
            return;

        byte count = p.ReadUInt8();

        LinkedObject? first = container.Items;
        if (first is null)
            return;

        bool reverse = false;

        if (container.Graphic == 0x2AF8) //hardcoded logic in original client that we must match
        {
            //sort the contents
            first = container.SortContents<Item>((x, y) => x.X - y.X);
        }
        else
        {
            //skip to last item and read in reverse later
            reverse = true;

            while (first?.Next != null)
            {
                first = first.Next;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (first is null)
                break;

            Item it = (Item)first;

            it.Price = p.ReadUInt32BE();
            byte nameLen = p.ReadUInt8();
            string name = p.ReadFixedString<ASCIICP1215>(nameLen);

            if (world.OPL.TryGetNameAndData(it.Serial, out string s, out _))
            {
                it.Name = s;
            }
            else if (int.TryParse(name, out int cliloc))
            {
                it.Name = Client.Game.UO.FileManager.Clilocs.Translate(cliloc, $"\t{it.ItemData.Name}: \t{it.Amount}", true);
            }
            else if (string.IsNullOrEmpty(name))
            {
                it.Name = it.ItemData.Name;
            }
            else
            {
                it.Name = name;
            }

            if (reverse)
                first = first.Previous;
            else
                first = first.Next;
        }
    }

    // 0x77 & 0xD2
    private static void UpdateCharacter(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();

        Mobile? mobile = world.Mobiles.Get(serial);
        if (mobile is null)
            return;

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
            UpdateGameObject(world, serial, graphic, 0, 0, x, y, z, direction, hue, flags, 1);
        }
    }

    // 0x78 & 0xD3
    private static void UpdateObject(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        Direction direction = (Direction)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        NotorietyFlag notoriety = (NotorietyFlag)p.ReadUInt8();
        bool oldDead = false;

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
            UpdateGameObject(world, serial, graphic, 0, 0, x, y, z, direction, hue, flags, 0);
        }

        Entity? obj = world.Get(serial);
        if (obj is null)
            return;

        if (!obj.IsEmpty)
        {
            LinkedObject? o = obj.Items;

            while (o is not null)
            {
                LinkedObject? next = o.Next;
                Item it = (Item)o;

                if (!it.Opened && it.Layer != Layer.Backpack)
                    world.RemoveItem(it.Serial, true);

                o = next;
            }
        }

        if (serial.IsMobile && obj is Mobile mob)
        {
            mob.NotorietyFlag = notoriety;
            UIManager.GetGump<PaperDollGump>(serial)?.RequestUpdateContents();
        }

        if (p[0] != 0x78)
            p.Skip(6);

        Serial itemSerial = p.ReadSerial();

        while (itemSerial != 0 && p.Position < p.Length)
        {
            ushort itemGraphic = p.ReadUInt16BE();
            byte layer = p.ReadUInt8();
            ushort item_hue = 0;

            if (Client.Game.UO.Version >= ClientVersion.CV_70331)
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

            itemSerial = p.ReadSerial();
        }

        if (serial != world.Player)
            return;

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

    // 0x7C
    private static void OpenMenu(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        Serial id = p.ReadSerial();
        string name = p.ReadFixedString<ASCIICP1215>(p.ReadUInt8());
        int count = p.ReadUInt8();

        ushort menuid = p.ReadUInt16BE();
        p.Seek(p.Position - 2);

        if (menuid != 0)
        {
            MenuGump gump = new(world, serial, id, name) { X = 100, Y = 100 };

            int posX = 0;

            for (int i = 0; i < count; i++)
            {
                ushort graphic = p.ReadUInt16BE();
                ushort hue = p.ReadUInt16BE();
                name = p.ReadFixedString<ASCIICP1215>(p.ReadUInt8());

                ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(graphic);

                if (artInfo.UV.Width == 0 || artInfo.UV.Height == 0)
                    continue;

                int posY = artInfo.UV.Height;

                if (posY >= 47)
                    posY = 0;
                else
                    posY = (47 - posY) >> 1;

                gump.AddItem(graphic, hue, name, posX, posY, i + 1);

                posX += artInfo.UV.Width;
            }

            UIManager.Add(gump);
        }
        else
        {
            GrayMenuGump gump = new(world, serial, id, name)
            {
                X = (Client.Game.Window.ClientBounds.Width >> 1) - 200,
                Y = (Client.Game.Window.ClientBounds.Height >> 1) - ((121 + count * 21) >> 1)
            };

            int offsetY = 35 + gump.Height;
            int gumpHeight = 70 + offsetY;

            for (int i = 0; i < count; i++)
            {
                p.Skip(4);
                name = p.ReadFixedString<ASCIICP1215>(p.ReadUInt8());

                int addHeight = gump.AddItem(name, offsetY);

                if (addHeight < 21)
                    addHeight = 21;

                offsetY += addHeight - 1;
                gumpHeight += addHeight;
            }

            offsetY += 5;

            gump.Add(new Button(0, 0x1450, 0x1451, 0x1450)
            {
                ButtonAction = ButtonAction.Activate,
                X = 70,
                Y = offsetY
            });

            gump.Add(new Button(1, 0x13B2, 0x13B3, 0x13B2)
            {
                ButtonAction = ButtonAction.Activate,
                X = 200,
                Y = offsetY
            });

            gump.SetHeight(gumpHeight);
            gump.WantUpdateSize = false;
            UIManager.Add(gump);
        }
    }

    // 0x86
    private static void UpdateCharacterList(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.UpdateCharacterList(ref p);
    }

    // 0x88
    private static void OpenPaperdoll(World world, ref SpanReader p)
    {
        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

        string text = p.ReadFixedString<ASCIICP1215>(60);
        byte flags = p.ReadUInt8();

        mobile.Title = text;

        PaperDollGump? paperdoll = UIManager.GetGump<PaperDollGump>(mobile);

        if (paperdoll is null)
        {
            if (!UIManager.GetGumpCachePosition(mobile, out Point location))
                location = new Point(100, 100);

            UIManager.Add(new PaperDollGump(world, mobile, (flags & 0x02) != 0) { Location = location });
        }
        else
        {
            bool old = paperdoll.CanLift;
            bool newLift = (flags & 0x02) != 0;

            paperdoll.CanLift = newLift;
            paperdoll.UpdateTitle(text);

            if (old != newLift)
                paperdoll.RequestUpdateContents();

            paperdoll.SetInScreen();
            paperdoll.BringOnTop();
        }
    }

    // 0x89
    private static void CorpseEquipment(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();

        Entity? corpse = world.Get(serial);
        if (corpse is null)
            return;

        // if it's not a corpse we should skip this [?]
        if (corpse.Graphic != 0x2006)
            return;

        Layer layer = (Layer)p.ReadUInt8();

        while (layer != Layer.Invalid && p.Position < p.Length)
        {
            Serial itemSerial = p.ReadSerial();

            if (layer - 1 != Layer.Backpack)
            {
                Item item = world.GetOrCreateItem(itemSerial);

                world.RemoveItemFromContainer(item);
                item.Container = serial;
                item.Layer = layer - 1;
                corpse.PushToBack(item);
            }

            layer = (Layer)p.ReadUInt8();
        }
    }

    // 0x8C
    private static void ReceiveServerRelay(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.HandleRelayServerPacket(ref p);
    }

    // 0x90
    private static void DisplayMap90(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        ushort gumpid = p.ReadUInt16BE();
        ushort startX = p.ReadUInt16BE();
        ushort startY = p.ReadUInt16BE();
        ushort endX = p.ReadUInt16BE();
        ushort endY = p.ReadUInt16BE();
        ushort width = p.ReadUInt16BE();
        ushort height = p.ReadUInt16BE();

        MapGump gump = new(world, serial, gumpid, width, height);
        SpriteInfo multiMapInfo;

        if (Client.Game.UO.Version >= ClientVersion.CV_308Z)
            multiMapInfo = Client.Game.UO.MultiMaps.GetMap(0, width, height, startX, startY, endX, endY);
        else
            multiMapInfo = Client.Game.UO.MultiMaps.GetMap(null, width, height, startX, startY, endX, endY);

        if (multiMapInfo.Texture is { } texture)
            gump.SetMapTexture(texture);

        UIManager.Add(gump);

        Item? it = world.Items.Get(serial);
        if (it is not null)
            it.Opened = true;
    }

    // 0x93
    private static void OpenBook93(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        bool editable = p.ReadBool();

        p.Skip(1);

        ModernBookGump? bgump = UIManager.GetGump<ModernBookGump>(serial);
        if (bgump is not { IsDisposed: false })
        {
            ushort pageCount = p.ReadUInt16BE();
            string title = p.ReadFixedString<UTF8>(60, true);
            string author = p.ReadFixedString<UTF8>(30, true);

            UIManager.Add(new ModernBookGump(world, serial, pageCount, title, author, editable, true)
            {
                X = 100,
                Y = 100
            });

            NetClient.Socket.SendBookPageDataRequest(serial, 1);
            return;
        }

        p.Skip(2);
        bgump.IsEditable = editable;
        bgump.SetTile(p.ReadFixedString<UTF8>(60, true), editable);
        bgump.SetAuthor(p.ReadFixedString<UTF8>(30, true), editable);
        bgump.UseNewHeader = false;
        bgump.SetInScreen();
        bgump.BringOnTop();
    }

    // 0x95
    private static void DyeData(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        p.Skip(2);
        ushort graphic = p.ReadUInt16BE();

        ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(0x0906);

        int x = (Client.Game.Window.ClientBounds.Width >> 1) - (gumpInfo.UV.Width >> 1);
        int y = (Client.Game.Window.ClientBounds.Height >> 1) - (gumpInfo.UV.Height >> 1);

        ColorPickerGump? gump = UIManager.GetGump<ColorPickerGump>(serial);

        if (gump is not { IsDisposed: false } || gump.Graphic != graphic)
        {
            gump?.Dispose();
            gump = new ColorPickerGump(world, serial, graphic, x, y, null);
            UIManager.Add(gump);
        }
    }

    // 0x97
    private static void MovePlayer(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Direction direction = (Direction)p.ReadUInt8();
        world.Player.Walk(direction & Direction.Mask, (direction & Direction.Running) != 0);
    }

    // 0x98
    private static void UpdateName(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        string name = p.ReadString<ASCIICP1215>();

        if (name != "")
        {
            WMapEntity? wme = world.WMapManager.GetEntity(serial);
            if (wme is not null)
                wme.Name = name;
        }

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        entity.Name = name;

        if (serial == world.Player.Serial && name != "" && name != world.Player.Name)
            Client.Game.SetWindowTitle(name);

        UIManager.GetGump<NameOverheadGump>(serial)?.SetName();
    }

    // 0x99
    private static void MultiPlacement(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        bool allowGround = p.ReadBool();
        uint targId = p.ReadUInt32BE();
        byte flags = p.ReadUInt8();
        p.Seek(18);
        ushort multiID = p.ReadUInt16BE();
        ushort xOff = p.ReadUInt16BE();
        ushort yOff = p.ReadUInt16BE();
        ushort zOff = p.ReadUInt16BE();
        ushort hue = p.ReadUInt16BE();

        world.TargetManager.SetTargetingMulti(targId, multiID, xOff, yOff, zOff, hue);
    }

    // 0x9A
    private static void ASCIIPrompt(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        world.MessageManager.PromptData = new PromptData
        {
            Prompt = ConsolePrompt.ASCII,
            Data = p.ReadUInt64BE()
        };
    }

    // 0x9E
    private static void SellList(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Mobile? vendor = world.Mobiles.Get(p.ReadSerial());
        if (vendor is null)
            return;

        ushort countItems = p.ReadUInt16BE();
        if (countItems <= 0)
            return;

        ShopGump? gump = UIManager.GetGump<ShopGump>(vendor);
        gump?.Dispose();
        gump = new ShopGump(world, vendor, false, 100, 0);

        for (int i = 0; i < countItems; i++)
        {
            Serial serial = p.ReadSerial();
            ushort graphic = p.ReadUInt16BE();
            ushort hue = p.ReadUInt16BE();
            ushort amount = p.ReadUInt16BE();
            ushort price = p.ReadUInt16BE();
            string name = p.ReadFixedString<ASCIICP1215>(p.ReadUInt16BE());
            bool fromcliloc = false;

            if (int.TryParse(name, out int clilocnum))
            {
                name = Client.Game.UO.FileManager.Clilocs.GetString(clilocnum);
                fromcliloc = true;
            }
            else if (string.IsNullOrEmpty(name))
            {
                bool success = world.OPL.TryGetNameAndData(serial, out name, out _);

                if (!success)
                    name = Client.Game.UO.FileManager.TileData.StaticData[graphic].Name;
            }

            gump.AddItem(serial, graphic, hue, amount, price, name, fromcliloc);
        }

        UIManager.Add(gump);
    }

    // 0xA1
    private static void UpdateHitpoints(World world, ref SpanReader p)
    {
        Entity? entity = world.Get(p.ReadSerial());
        if (entity is null)
            return;

        entity.HitsMax = p.ReadUInt16BE();
        entity.Hits = p.ReadUInt16BE();

        if (entity.HitsRequest == HitsRequestStatus.Pending)
            entity.HitsRequest = HitsRequestStatus.Received;

        if (entity == world.Player)
            world.UoAssist.SignalHits();
    }

    // 0xA2
    private static void UpdateMana(World world, ref SpanReader p)
    {
        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

        mobile.ManaMax = p.ReadUInt16BE();
        mobile.Mana = p.ReadUInt16BE();

        if (mobile == world.Player)
            world.UoAssist.SignalMana();
    }

    // 0xA3
    private static void UpdateStamina(World world, ref SpanReader p)
    {
        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

        mobile.StaminaMax = p.ReadUInt16BE();
        mobile.Stamina = p.ReadUInt16BE();

        if (mobile == world.Player)
            world.UoAssist.SignalStamina();
    }

    // 0xA5
    private static void OpenUrl(World world, ref SpanReader p)
    {
        string url = p.ReadString<ASCIICP1215>();

        if (url != "")
            PlatformHelper.LaunchBrowser(url);
    }

    // 0xA6
    private static void TipWindow(World world, ref SpanReader p)
    {
        byte flag = p.ReadUInt8();

        if (flag == 1)
            return;

        Serial tip = p.ReadSerial();
        string str = p.ReadFixedString<ASCIICP1215>(p.ReadUInt16BE()).Replace('\r', '\n');

        int x = 20;
        int y = 20;

        if (flag == 0)
        {
            x = 200;
            y = 100;
        }

        UIManager.Add(new TipNoticeGump(world, tip, flag, str) { X = x, Y = y });
    }

    // 0xA8
    private static void ServerListReceived(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.ServerListReceived(ref p);
    }

    // 0xA9
    private static void ReceiveCharacterList(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.ReceiveCharacterList(ref p);
    }

    // 0xAA
    private static void AttackCharacter(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();

        GameActions.SendCloseStatus(world, world.TargetManager.LastAttack);
        world.TargetManager.LastAttack = serial;
        GameActions.RequestMobileStatus(world, serial);
    }

    // 0xAB
    private static void TextEntryDialog(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        byte parentId = p.ReadUInt8();
        byte buttonId = p.ReadUInt8();

        ushort textLen = p.ReadUInt16BE();
        string text = p.ReadFixedString<ASCIICP1215>(textLen);

        bool haveCancel = p.ReadBool();
        byte variant = p.ReadUInt8();
        uint maxLength = p.ReadUInt32BE();

        ushort descLen = p.ReadUInt16BE();
        string desc = p.ReadFixedString<ASCIICP1215>(descLen);

        TextEntryDialogGump gump = new(world, serial, 143, 172, variant, (int)maxLength, text, desc, buttonId, parentId)
        {
            CanCloseWithRightClick = haveCancel
        };

        UIManager.Add(gump);
    }

    // 0xAE
    private static void UnicodeTalk(World world, ref SpanReader p)
    {
        if (!world.InGame)
        {
            LoginScene? scene = Client.Game.GetScene<LoginScene>();

            if (scene is not null)
            {
                Log.Warn("UnicodeTalk received during LoginScene");

                if (p.Length > 48)
                {
                    p.Seek(48);
                    Log.PushIndent();
                    Log.Warn("Handled UnicodeTalk in LoginScene");
                    Log.PopIndent();
                }
            }

            return;
        }

        Serial serial = p.ReadSerial();
        Entity? entity = world.Get(serial);
        ushort graphic = p.ReadUInt16BE();
        MessageType type = (MessageType)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        ushort font = p.ReadUInt16BE();
        string lang = p.ReadFixedString<ASCIICP1215>(4);
        string name = p.ReadString<ASCIICP1215>();

        if (serial == 0 && graphic == 0 && type == MessageType.Regular && font == 0xFFFF && hue == 0xFFFF
            && name.Equals("system", StringComparison.CurrentCultureIgnoreCase))
        {
            Span<byte> buffer =
            [
                0x03, 0x00, 0x28, 0x20, 0x00, 0x34, 0x00, 0x03,
                0xdb, 0x13, 0x14, 0x3f, 0x45, 0x2c, 0x58, 0x0f,
                0x5d, 0x44, 0x2e, 0x50, 0x11, 0xdf, 0x75, 0x5c,
                0xe0, 0x3e, 0x71, 0x4f, 0x31, 0x34, 0x05, 0x4e,
                0x18, 0x1e, 0x72, 0x0f, 0x59, 0xad, 0xf5, 0x00
            ];

            NetClient.Socket.Send(buffer);

            return;
        }

        string text = string.Empty;

        if (p.Length > 48)
        {
            p.Seek(48);
            text = p.ReadString<UnicodeBE>();
        }

        TextType textType = TextType.SYSTEM;

        if (type == MessageType.Alliance || type == MessageType.Guild)
        {
            textType = TextType.GUILD_ALLY;
        }
        else if (type == MessageType.System || serial == 0xFFFF_FFFF || serial == 0
            || name.Equals("system", StringComparison.CurrentCultureIgnoreCase) && entity is null)
        {
            // do nothing
        }
        else if (entity is not null)
        {
            textType = TextType.OBJECT;

            if (string.IsNullOrEmpty(entity.Name))
                entity.Name = string.IsNullOrEmpty(name) ? text : name;
        }

        world.MessageManager.HandleMessage(entity, text, name, hue, type, ProfileManager.CurrentProfile.ChatFont, textType, true);
    }

    // 0xAF
    private static void DisplayDeath(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        Serial corpseSerial = p.ReadSerial();
        uint running = p.ReadUInt32BE();

        Mobile? owner = world.Mobiles.Get(serial);
        if (owner is null || serial == world.Player)
            return;

        serial = serial.ToVirtual();

        if (world.Mobiles.Remove(owner.Serial))
        {
            for (LinkedObject? i = owner.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                it.Container = serial;
            }

            world.Mobiles[serial] = owner;
            owner.Serial = serial;
        }

        if (corpseSerial.IsEntity)
            world.CorpseManager.Add(corpseSerial, serial, owner.Direction, running != 0);

        Renderer.Animations.Animations animations = Client.Game.UO.Animations;
        ushort gfx = owner.Graphic;
        animations.ConvertBodyIfNeeded(ref gfx);
        AnimationGroupsType animGroup = animations.GetAnimType(gfx);
        AnimationFlags animFlags = animations.GetAnimFlags(gfx);
        byte group = Client.Game.UO.FileManager.Animations.GetDeathAction(gfx, animFlags, animGroup, running != 0, true);
        owner.SetAnimation(group, 0, 5, 1);
        owner.AnimIndex = 0;

        if (ProfileManager.CurrentProfile.AutoOpenCorpses)
            world.Player.TryOpenCorpses();
    }

    // 0xB0
    private static void OpenGump(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial sender = p.ReadSerial();
        Serial gumpId = p.ReadSerial();
        int x = (int)p.ReadUInt32BE();
        int y = (int)p.ReadUInt32BE();

        ushort cmdLen = p.ReadUInt16BE();
        string cmd = p.ReadFixedString<ASCIICP1215>(cmdLen);

        ushort textLinesCount = p.ReadUInt16BE();

        string[] lines = new string[textLinesCount];

        for (int i = 0; i < textLinesCount; i++)
        {
            int length = p.ReadUInt16BE();

            if (length > 0)
                lines[i] = p.ReadFixedString<UnicodeBE>(length);
            else
                lines[i] = "";
        }

        CreateGump(world, sender, gumpId, x, y, cmd, lines);
    }

    // 0xB2
    private static void ChatMessage(World world, ref SpanReader p)
    {
        ushort cmd = p.ReadUInt16BE();

        string channelName;
        string username;
        ushort userType;

        switch (cmd)
        {
            case 0x03E8: // create conference
                p.Skip(4);
                channelName = p.ReadString<UnicodeBE>();
                bool hasPassword = p.ReadUInt16BE() == 0x31;
                world.ChatManager.CurrentChannelName = channelName;
                world.ChatManager.AddChannel(channelName, hasPassword);

                UIManager.GetGump<ChatGump>()?.RequestUpdateContents();

                break;

            case 0x03E9: // destroy conference
                p.Skip(4);
                channelName = p.ReadString<UnicodeBE>();
                world.ChatManager.RemoveChannel(channelName);

                UIManager.GetGump<ChatGump>()?.RequestUpdateContents();

                break;

            case 0x03EB: // display enter username window
                world.ChatManager.ChatIsEnabled = ChatStatus.EnabledUserRequest;

                break;

            case 0x03EC: // close chat
                world.ChatManager.Clear();
                world.ChatManager.ChatIsEnabled = ChatStatus.Disabled;

                UIManager.GetGump<ChatGump>()?.Dispose();

                break;

            case 0x03ED: // username accepted, display chat
                p.Skip(4);
                username = p.ReadString<UnicodeBE>();
                world.ChatManager.ChatIsEnabled = ChatStatus.Enabled;
                NetClient.Socket.SendChatJoinCommand("General");

                break;

            case 0x03EE: // add user
                p.Skip(4);
                userType = p.ReadUInt16BE();
                username = p.ReadString<UnicodeBE>();

                break;

            case 0x03EF: // remove user
                p.Skip(4);
                username = p.ReadString<UnicodeBE>();

                break;

            case 0x03F0: // clear all players
                break;

            case 0x03F1: // you have joined a conference
                p.Skip(4);
                channelName = p.ReadString<UnicodeBE>();
                world.ChatManager.CurrentChannelName = channelName;

                UIManager.GetGump<ChatGump>()?.UpdateConference();

                GameActions.Print(world, string.Format(ResGeneral.YouHaveJoinedThe0Channel, channelName),
                    ProfileManager.CurrentProfile.ChatMessageHue, MessageType.Regular, 1);

                break;

            case 0x03F4:
                p.Skip(4);
                channelName = p.ReadString<UnicodeBE>();

                GameActions.Print(
                    world,
                    string.Format(ResGeneral.YouHaveLeftThe0Channel, channelName),
                    ProfileManager.CurrentProfile.ChatMessageHue,
                    MessageType.Regular,
                    1
                );

                break;

            case 0x0025:
            case 0x0026:
            case 0x0027:
                p.Skip(4);
                ushort msgType = p.ReadUInt16BE();
                username = p.ReadString<UnicodeBE>();
                string msgSent = p.ReadString<UnicodeBE>();

                if (!string.IsNullOrEmpty(msgSent))
                {
                    int idx = msgSent.IndexOf('{');
                    int idxLast = msgSent.IndexOf('}') + 1;

                    if (idxLast > idx && idx > -1)
                    {
                        msgSent = msgSent.Remove(idx, idxLast - idx);
                    }
                }

                GameActions.Print(world, $"{username}: {msgSent}", ProfileManager.CurrentProfile.ChatMessageHue, MessageType.Regular, 1);
                break;

            default:
                if (cmd >= 0x0001 && cmd <= 0x0024 || cmd >= 0x0028 && cmd <= 0x002C)
                {
                    // TODO: read Chat.enu ?
                    // http://docs.polserver.com/packets/index.php?Packet=0xB2

                    string msg = ChatManager.GetMessage(cmd - 1);

                    if (string.IsNullOrEmpty(msg))
                    {
                        return;
                    }

                    p.Skip(4);
                    string text = p.ReadString<UnicodeBE>();

                    if (!string.IsNullOrEmpty(text))
                    {
                        int idx = msg.IndexOf("%1");

                        if (idx >= 0)
                        {
                            msg = msg.Replace("%1", text);
                        }

                        if (cmd - 1 == 0x000A || cmd - 1 == 0x0017)
                        {
                            idx = msg.IndexOf("%2");

                            if (idx >= 0)
                            {
                                msg = msg.Replace("%2", text);
                            }
                        }
                    }

                    GameActions.Print(world, msg, ProfileManager.CurrentProfile.ChatMessageHue, MessageType.Regular, 1);
                }

                break;
        }
    }

    // 0xB8
    private static void CharacterProfile(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        string header = p.ReadString<ASCIICP1215>();
        string footer = p.ReadString<UnicodeBE>();

        string body = p.ReadString<UnicodeBE>();

        UIManager.GetGump<ProfileGump>(serial)?.Dispose();
        UIManager.Add(new ProfileGump(world, serial, header, footer, body, serial == world.Player.Serial));
    }

    // 0xB9
    private static void EnableLockedFeatures(World world, ref SpanReader p)
    {
        LockedFeatureFlags flags;

        if (Client.Game.UO.Version >= ClientVersion.CV_60142)
            flags = (LockedFeatureFlags)p.ReadUInt32BE();
        else
            flags = (LockedFeatureFlags)p.ReadUInt16BE();

        world.ClientLockedFeatures.SetFlags(flags);

        world.ChatManager.ChatIsEnabled = world.ClientLockedFeatures.Flags.HasFlag(LockedFeatureFlags.T2A) ? ChatStatus.Enabled : 0;

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

    // 0xBA
    private static void DisplayQuestArrow(World world, ref SpanReader p)
    {
        bool display = p.ReadBool();
        ushort mx = p.ReadUInt16BE();
        ushort my = p.ReadUInt16BE();

        Serial serial = Serial.Zero;

        if (Client.Game.UO.Version >= ClientVersion.CV_7090)
            serial = p.ReadSerial();

        QuestArrowGump? arrow = UIManager.GetGump<QuestArrowGump>(serial);

        if (display)
        {
            if (arrow is null)
                UIManager.Add(new QuestArrowGump(world, serial, mx, my));
            else
                arrow.SetRelativePosition(mx, my);
        }
        else
        {
            arrow?.Dispose();
        }
    }

    // 0xBC
    private static void Season(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        byte season = p.ReadUInt8();
        byte music = p.ReadUInt8();

        if (season > 4)
            season = 0;

        if (world.Player.IsDead && season == 4)
            return;

        world.OldSeason = (Season)season;
        world.OldMusicIndex = music;

        if (world.Season == Game.Managers.Season.Desolation)
            world.OldMusicIndex = 42;

        world.ChangeSeason((Season)season, music);
    }

    // 0xBD
    private static void SendClientVersion(World world, ref SpanReader p)
    {
        NetClient.Socket.SendClientVersion(Settings.GlobalSettings.ClientVersion);
    }

    // 0xBF
    private static unsafe void ExtendedCommand(World world, ref SpanReader p)
    {
        p.Skip(1);
        byte extId = p.ReadUInt8();

        delegate*<World, ref SpanReader, void> handler = Instance._extendedHandlers[extId].Handler;
        if (handler is null)
        {
            Log.Warn($"Unhandled 0xBF - sub: {extId:X2}");
            return;
        }

        handler(world, ref p);
    }

    // 0xBF.01
    private static void FastWalkPrevention(World world, ref SpanReader p)
    {
        for (int i = 0; i < 6; i++)
        {
            world.Player.Walker.FastWalkStack.SetValue(i, p.ReadUInt32BE());
        }
    }

    // 0xBF.02
    private static void FastWalkStack(World world, ref SpanReader p)
    {
        world.Player.Walker.FastWalkStack.AddValue(p.ReadUInt32BE());
    }

    // 0xBF.04
    private static void CloseGenericGump(World world, ref SpanReader p)
    {
        Serial ser = p.ReadSerial();
        int button = (int)p.ReadUInt32BE();

        LinkedListNode<Gump>? first = UIManager.Gumps.First;

        while (first is not null)
        {
            LinkedListNode<Gump>? nextGump = first.Next;

            if (first.Value.ServerSerial == ser && first.Value.IsFromServer)
            {
                if (button != 0)
                {
                    first.Value?.OnButtonClick(button);
                }
                else
                {
                    if (first.Value.CanMove)
                        UIManager.SavePosition(ser, first.Value.Location);
                    else
                        UIManager.RemovePosition(ser);
                }

                first.Value.Dispose();
            }

            first = nextGump;
        }
    }

    // 0xBF.06
    private static void PartyCommands(World world, ref SpanReader p)
    {
        world.Party.ParsePacket(ref p);
    }

    // 0xBF.08
    private static void SetMap(World world, ref SpanReader p)
    {
        world.MapIndex = p.ReadUInt8();
    }

    // 0xBF.0C
    private static void CloseStatusbar(World world, ref SpanReader p)
    {
        UIManager.GetGump<HealthBarGump>(p.ReadSerial())?.Dispose();
    }

    // 0xBF.10
    private static void DisplayEquipInfo(World world, ref SpanReader p)
    {
        Item? item = world.Items.Get(p.ReadSerial());
        if (item is null)
            return;

        uint cliloc = p.ReadUInt32BE();
        string? str = "";

        if (cliloc > 0)
        {
            str = Client.Game.UO.FileManager.Clilocs.GetString((int)cliloc, true);

            if (!string.IsNullOrEmpty(str))
                item.Name = str;

            world.MessageManager.HandleMessage(item, str, item.Name, 0x3B2, MessageType.Regular, 3, TextType.OBJECT, true);
        }

        str = "";
        ushort crafterNameLen = 0;
        uint next = p.ReadUInt32BE();

        Span<char> span = stackalloc char[256];
        ValueStringBuilder strBuffer = new(span);
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
            next = p.ReadUInt32BE();

        if (next == 0xFFFFFFFC)
            strBuffer.Append("[Unidentified");

        byte count = 0;

        while (p.Position < p.Length - 4)
        {
            if (count != 0 || next is 0xFFFFFFFC or 0xFFFFFFFD)
                next = p.ReadUInt32BE();

            short charges = (short)p.ReadUInt16BE();
            string? attr = Client.Game.UO.FileManager.Clilocs.GetString((int)next);

            if (attr is null)
            {
                count++;
                continue;
            }

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

            count++;
        }

        if (count is > 0 and < 20 || next == 0xFFFFFFFC && count == 0)
            strBuffer.Append(']');

        if (strBuffer.Length != 0)
            world.MessageManager.HandleMessage(item, strBuffer.ToString(), item.Name, 0x3B2, MessageType.Regular, 3, TextType.OBJECT, true);

        strBuffer.Dispose();

        NetClient.Socket.SendMegaClilocRequest(item);
    }

    // 0xBF.14
    private static void DisplayPopupOrContextMenu(World world, ref SpanReader p)
    {
        UIManager.ShowGamePopup(new PopupMenuGump(world, PopupMenuData.Parse(ref p))
        {
            X = world.DelayedObjectClickManager.LastMouseX,
            Y = world.DelayedObjectClickManager.LastMouseY
        });
    }

    // 0xBF.16
    private static void CloseUserInterfaceWindows(World world, ref SpanReader p)
    {
        uint id = p.ReadUInt32BE();
        Serial serial = p.ReadSerial();

        switch (id)
        {
            case 1: UIManager.GetGump<PaperDollGump>(serial)?.Dispose(); break; // paperdoll

            case 2: //statusbar
                UIManager.GetGump<HealthBarGump>(serial)?.Dispose();

                if (serial == world.Player.Serial)
                    StatusGumpBase.GetStatusGump()?.Dispose();

                break;

            case 8: UIManager.GetGump<ProfileGump>()?.Dispose(); break; // char profile
            case 0x0C: UIManager.GetGump<ContainerGump>(serial)?.Dispose(); break; //container
        }
    }

    // 0xBF.18
    private static void EnableMapPatches(World world, ref SpanReader p)
    {
        if (!Client.Game.UO.FileManager.Maps.ApplyPatches(ref p))
            return;

        int map = world.MapIndex;
        world.MapIndex = -1;
        world.MapIndex = map;

        Log.Trace("Map Patches applied.");
    }

    // 0xBF.19
    private static void ExtendedStats(World world, ref SpanReader p)
    {
        byte version = p.ReadUInt8();
        Serial serial = p.ReadSerial();

        switch (version)
        {
            case 0:
                Mobile? bonded = world.Mobiles.Get(serial);
                if (bonded is null)
                    break;

                bool dead = p.ReadBool();
                bonded.IsDead = dead;

                break;

            case 2:

                if (serial != world.Player)
                    break;

                _ = p.ReadUInt8();
                byte state = p.ReadUInt8();

                world.Player.StrLock = (Lock)((state >> 4) & 3);
                world.Player.DexLock = (Lock)((state >> 2) & 3);
                world.Player.IntLock = (Lock)(state & 3);

                StatusGumpBase.GetStatusGump()?.RequestUpdateContents();

                break;

            case 5:

                int pos = p.Position;
                _ = p.ReadUInt8();
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

                    Mobile? mobile = world.Mobiles.Get(serial);

                    if (mobile is not null)
                    {
                        mobile.SetAnimation(Mobile.GetReplacedObjectAnimation(mobile.Graphic, animation));
                        mobile.ExecuteAnimation = false;
                        mobile.AnimIndex = (byte)frame;
                    }
                }
                else if (world.Player is not null && serial == world.Player)
                {
                    p.Seek(pos);
                    goto case 2;
                }

                break;
        }
    }

    // 0xBF.1B
    private static void NewSpellbookContent(World world, ref SpanReader p)
    {
        p.Skip(2);

        Item spellbook = world.GetOrCreateItem(p.ReadSerial());
        spellbook.Graphic = p.ReadUInt16BE();
        spellbook.Clear();
        _ = p.ReadUInt16BE();

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
                    Serial s = new(cc);
                    // FIXME: should i call Item.Create ?
                    Item spellItem = Item.Create(world, s); // new Item()
                    spellItem.Serial = s;
                    spellItem.Graphic = 0x1F2E;
                    spellItem.Amount = cc;
                    spellItem.Container = spellbook.Serial;
                    spellbook.PushToBack(spellItem);
                }
            }
        }

        UIManager.GetGump<SpellbookGump>(spellbook.Serial)?.RequestUpdateContents();
    }

    // 0xBF.1D
    private static void HouseRevisionState(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        uint revision = p.ReadUInt32BE();

        Item? multi = world.Items.Get(serial);
        if (multi is null)
            world.HouseManager.Remove(serial);

        if (!world.HouseManager.TryGetHouse(serial, out House house) || !house.IsCustom || house.Revision != revision)
        {
            OutgoingPackets.AddHouseClilocRequest(serial);
        }
        else
        {
            house.Generate();
            world.BoatMovingManager.ClearSteps(serial);

            UIManager.GetGump<MiniMapGump>()?.RequestUpdateContents();

            if (world.HouseManager.EntityIntoHouse(serial, world.Player))
                Client.Game.GetScene<GameScene>()?.UpdateMaxDrawZ(true);
        }
    }

    // 0xBF.20
    private static void CustomHousing(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        byte type = p.ReadUInt8();

        switch (type)
        {
            case 1: break; // update
            case 2: break; // remove
            case 3: break; // update multi pos

            case 4: // begin
                HouseCustomizationGump? gump = UIManager.GetGump<HouseCustomizationGump>();
                if (gump is not null)
                    break;

                gump = new HouseCustomizationGump(world, serial, 50, 50);
                UIManager.Add(gump);

                break;

            case 5: // end
                UIManager.GetGump<HouseCustomizationGump>(serial)?.Dispose();
                break;
        }
    }

    // 0xBF.21
    private static void AbilityIcon(World world, ref SpanReader p)
    {
        world.Player.Abilities[0] &= (Ability)0x7F;
        world.Player.Abilities[1] &= (Ability)0x7F;
    }

    // 0xBF.22
    private static void DamageBF(World world, ref SpanReader p)
    {
        p.Skip(1);

        Entity? en = world.Get(p.ReadSerial());
        if (en is null)
            return;

        byte damage = p.ReadUInt8();

        if (damage > 0)
            world.WorldTextManager.AddDamage(en.Serial, damage);
    }

    // 0xBF.25
    private static void ChangeAbility(World world, ref SpanReader p)
    {
        ushort spell = p.ReadUInt16BE();
        bool active = p.ReadBool();

        foreach (Gump g in UIManager.Gumps)
        {
            if (g.IsDisposed || !g.IsVisible || g is not UseSpellButtonGump spellButton || spellButton.SpellID != spell)
                continue;
            
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

    // 0xBF.26
    private static void MountSpeed(World world, ref SpanReader p)
    {
        byte val = p.ReadUInt8();

        if (val > (int)CharacterSpeedType.FastUnmountAndCantRun)
            val = 0;

        world.Player.SpeedMode = (CharacterSpeedType)val;
    }

    // 0xBF.2A
    private static void ChangeRace(World world, ref SpanReader p)
    {
        bool isFemale = p.ReadBool();
        byte race = p.ReadUInt8();

        UIManager.GetGump<RaceChangeGump>()?.Dispose();
        UIManager.Add(new RaceChangeGump(world, isFemale, race));
    }

    // 0xBF.2B
    private static void UnknownBF(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        byte animId = p.ReadUInt8();
        byte frameCount = p.ReadUInt8();

        foreach (Mobile m in world.Mobiles.Values)
        {
            if ((m.Serial.Value & 0xFFFF) != serial.Value)
                continue;
            
            m.SetAnimation(animId);
            m.AnimIndex = frameCount;
            m.ExecuteAnimation = false;

            break;
        }
    }

    // 0xC0
    private static void GraphicEffectC0(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        GraphicEffectType type = (GraphicEffectType)p.ReadUInt8();
        if (type > GraphicEffectType.FixedFrom)
            return;

        Serial source = p.ReadSerial();
        Serial target = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        ushort srcX = p.ReadUInt16BE();
        ushort srcY = p.ReadUInt16BE();
        sbyte srcZ = p.ReadInt8();
        ushort targetX = p.ReadUInt16BE();
        ushort targetY = p.ReadUInt16BE();
        sbyte targetZ = p.ReadInt8();
        byte speed = p.ReadUInt8();
        byte duration = p.ReadUInt8();
        _ = p.ReadUInt16BE();
        bool fixedDirection = p.ReadBool();
        bool doesExplode = p.ReadBool();
        uint hue = p.ReadUInt32BE();
        GraphicEffectBlendMode blendmode = (GraphicEffectBlendMode)(p.ReadUInt32BE() % 7);

        world.SpawnEffect(type, source, target, graphic, (ushort)hue, srcX, srcY, srcZ, targetX, targetY, targetZ,
            speed, duration, fixedDirection, doesExplode, false, blendmode);
    }

    // 0xC1 & 0xCC
    private static void DisplayClilocString(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Serial serial = p.ReadSerial();
        Entity? entity = world.Get(serial);
        _ = p.ReadUInt16BE();
        MessageType type = (MessageType)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        ushort font = p.ReadUInt16BE();
        uint cliloc = p.ReadUInt32BE();
        AffixType flags = p[0] == 0xCC ? (AffixType)p.ReadUInt8() : 0x00;
        string name = p.ReadFixedString<ASCIICP1215>(30);
        string affix = p[0] == 0xCC ? p.ReadString<ASCIICP1215>() : "";

        string? arguments = null;

        // value for "You notify them you don't want to join the party" || "You have been added to the party"
        if (cliloc == 1008092 || cliloc == 1005445)
        {
            for (LinkedListNode<Gump>? g = UIManager.Gumps.Last; g is not null; g = g.Previous)
            {
                if (g.Value is PartyInviteGump pg)
                    pg.Dispose();
            }
        }

        int remains = p.Remaining;
        if (remains > 0)
        {
            if (p[0] == 0xCC)
                arguments = p.ReadFixedString<UnicodeBE>(remains);
            else
                arguments = p.ReadFixedString<UnicodeLE>(remains / 2);
        }

        string? text = Client.Game.UO.FileManager.Clilocs.Translate((int)cliloc, arguments);
        if (text is null)
            return;

        if (!string.IsNullOrWhiteSpace(affix))
        {
            if ((flags & AffixType.Prepend) != 0)
                text = $"{affix}{text}";
            else
                text = $"{text}{affix}";
        }

        if ((flags & AffixType.System) != 0)
            type = MessageType.System;

        if (!Client.Game.UO.FileManager.Fonts.UnicodeFontExists((byte)font))
            font = 0;

        TextType textType = TextType.SYSTEM;

        if (!serial.IsValid || !string.IsNullOrEmpty(name) && string.Equals(name, "system", StringComparison.InvariantCultureIgnoreCase))
        {
            // do nothing
        }
        else if (entity is not null)
        {
            textType = TextType.OBJECT;

            if (string.IsNullOrEmpty(entity.Name))
                entity.Name = name;
        }
        else
        {
            if (type == MessageType.Label)
                return;
        }

        world.MessageManager.HandleMessage(entity, text, name, hue, type, (byte)font, textType, true);
    }

    // 0xC2
    private static void UnicodePrompt(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        world.MessageManager.PromptData = new PromptData()
        {
            Prompt = ConsolePrompt.Unicode,
            Data = p.ReadUInt64BE()
        };
    }

    // 0xC7
    private static void GraphicEffectC7(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        GraphicEffectType type = (GraphicEffectType)p.ReadUInt8();
        if (type > GraphicEffectType.FixedFrom)
            return;

        Serial source = p.ReadSerial();
        Serial target = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        ushort srcX = p.ReadUInt16BE();
        ushort srcY = p.ReadUInt16BE();
        sbyte srcZ = p.ReadInt8();
        ushort targetX = p.ReadUInt16BE();
        ushort targetY = p.ReadUInt16BE();
        sbyte targetZ = p.ReadInt8();
        byte speed = p.ReadUInt8();
        byte duration = p.ReadUInt8();
        _ = p.ReadUInt16BE();
        bool fixedDirection = p.ReadBool();
        bool doesExplode = p.ReadBool();
        uint hue = p.ReadUInt32BE();
        GraphicEffectBlendMode blendmode = (GraphicEffectBlendMode)(p.ReadUInt32BE() % 7);
        p.Skip(11);

        world.SpawnEffect(type, source, target, graphic, (ushort)hue, srcX, srcY, srcZ, targetX, targetY, targetZ,
            speed, duration, fixedDirection, doesExplode, false, blendmode);
    }

    // 0xC8
    private static void ClientViewRange(World world, ref SpanReader p)
    {
        world.ClientViewRange = p.ReadUInt8();
    }

    // 0xD1
    private static void Logout(World world, ref SpanReader p)
    {
        GameScene? gameScene = Client.Game.GetScene<GameScene>();

        if (gameScene is not { DisconnectionRequested: true }
            || (world.ClientFeatures.Flags & CharacterListFlags.CLF_OWERWRITE_CONFIGURATION_BUTTON) == 0)
        {
            return;
        }
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

    // 0xD4
    private static void OpenBookD4(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        _ = p.ReadBool();
        bool editable = p.ReadBool();

        ModernBookGump? bgump = UIManager.GetGump<ModernBookGump>(serial);

        if (bgump is not { IsDisposed: false })
        {
            ushort page_count = p.ReadUInt16BE();
            string title = p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true);
            string author = p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true);

            UIManager.Add(new ModernBookGump(world, serial, page_count, title, author, editable, false)
            {
                X = 100,
                Y = 100
            });

            NetClient.Socket.SendBookPageDataRequest(serial, 1);
            return;
        }

        p.Skip(2);
        bgump.IsEditable = editable;
        bgump.SetTile(p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true), editable);
        bgump.SetAuthor(p.ReadFixedString<UTF8>(p.ReadUInt16BE(), true), editable);
        bgump.UseNewHeader = true;
        bgump.SetInScreen();
        bgump.BringOnTop();
    }

    // 0xD6
    private static void MegaCliloc(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        ushort unknown = p.ReadUInt16BE();
        if (unknown > 1)
            return;

        Serial serial = p.ReadSerial();

        p.Skip(2);

        uint revision = p.ReadUInt32BE();

        Entity? entity = world.Mobiles.Get(serial);

        if (entity is null)
        {
            if (serial.IsMobile)
                Log.Warn("Searching a mobile into World.Items from MegaCliloc packet");

            entity = world.Items.Get(serial);
        }

        List<(int, string, int)> list = [];
        int totalLength = 0;

        while (p.Position < p.Length)
        {
            int cliloc = (int)p.ReadUInt32BE();

            if (cliloc == 0)
                break;

            ushort length = p.ReadUInt16BE();
            string argument = "";

            if (length != 0)
                argument = p.ReadFixedString<UnicodeLE>(length / 2);

            string? str = Client.Game.UO.FileManager.Clilocs.Translate(cliloc, argument, true);
            if (str is null)
                continue;

            int argcliloc = 0;

            string[] argcheck = argument.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (argcheck.Length == 2)
                _ = int.TryParse(argcheck[1], out argcliloc);

            // hardcoded colors lol
            switch (cliloc)
            {
                case 1080418:
                    if (Client.Game.UO.Version >= ClientVersion.CV_60143)
                        str = $"<basefont color=#40a4fe>{str}</basefont>";
                    break;
                case 1061170:
                    if (int.TryParse(argument, out int strength) && world.Player.Strength < strength)
                        str = $"<basefont color=#FF0000>{str}</basefont>";
                    break;
                case 1062613: str = $"<basefont color=#FFCC33>{str}</basefont>"; break;
                case 1159561: str = $"<basefont color=#b66dff>{str}</basefont>"; break;
            }


            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Item1 == cliloc && string.Equals(list[i].Item2, str, StringComparison.Ordinal))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            list.Add((cliloc, str, argcliloc));

            totalLength += str.Length;
        }

        Item? container = null;

        if (entity is Item it && it.Container.IsEntity)
            container = world.Items.Get(it.Container);

        bool inBuyList = false;

        if (container is not null)
            inBuyList = container.Layer is Layer.ShopBuy or Layer.ShopBuyRestock or Layer.ShopSell;

        bool first = true;

        string name = string.Empty;
        string data = string.Empty;
        int namecliloc = 0;

        if (list.Count != 0)
        {
            Span<char> span = stackalloc char[totalLength];
            using ValueStringBuilder sb = new(span);

            foreach ((int, string, int) s in list)
            {
                string str = s.Item2;

                if (first)
                {
                    name = str;

                    if (entity is not null && !serial.IsEntity)
                    {
                        entity.Name = str;
                        namecliloc = s.Item3 > 0 ? s.Item3 : s.Item1;
                    }

                    first = false;
                }
                else
                {
                    if (sb.Length != 0)
                        sb.Append('\n');

                    sb.Append(str);
                }
            }

            data = sb.ToString();
        }

        world.OPL.Add(serial, revision, name, data, namecliloc);

        if (inBuyList && container is not null && container.Serial.IsEntity)
            UIManager.GetGump<ShopGump>(container.RootContainer)?.SetNameTo((Item)entity, name);
    }

    // 0xD8
    private static void CustomHouse(World world, ref SpanReader p)
    {
        _ = p.ReadUInt8();
        _ = p.ReadBool();
        Serial serial = p.ReadSerial();
        Item? foundation = world.Items.Get(serial);
        uint revision = p.ReadUInt32BE();

        if (foundation is not { IsMulti: true, MultiInfo: { } multi })
            return;

        p.Skip(4);

        if (!world.HouseManager.TryGetHouse(foundation.Serial, out House house))
        {
            house = new House(world, foundation.Serial, revision, true);
            world.HouseManager.Add(foundation.Serial, house);
        }
        else
        {
            house.ClearComponents(true);
            house.Revision = revision;
            house.IsCustom = true;
        }

        short minX = (short)multi.X;
        short minY = (short)multi.Y;
        short maxY = (short)multi.Height;

        if (minX == 0 && minY == 0 && maxY == 0 && multi.Width == 0)
        {
            Log.Warn("[CustomHouse (0xD8) - Invalid multi dimentions. Maybe missing some installation required files");
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
                continue;

            ReadUnsafeCustomHouseData(p.Buffer, p.Position, dlen, clen, planeZ, planeMode, minX, minY, maxY, foundation, house);

            p.Skip(clen);
        }

        if (world.CustomHouseManager is not null)
        {
            world.CustomHouseManager.GenerateFloorPlace();
            UIManager.GetGump<HouseCustomizationGump>(house.Serial)?.Update();
        }

        UIManager.GetGump<MiniMapGump>()?.RequestUpdateContents();

        if (world.HouseManager.EntityIntoHouse(serial, world.Player))
            Client.Game.GetScene<GameScene>()?.UpdateMaxDrawZ(true);

        world.BoatMovingManager.ClearSteps(serial);
    }

    // 0xDC
    private static void OPLInfo(World world, ref SpanReader p)
    {
        if (!world.ClientFeatures.TooltipsEnabled)
            return;

        Serial serial = p.ReadSerial();
        uint revision = p.ReadUInt32BE();

        if (!world.OPL.IsRevisionEquals(serial, revision))
            OutgoingPackets.AddMegaClilocRequest(serial);
    }

    // 0xDD
    private static void OpenCompressedGump(World world, ref SpanReader p)
    {
        Serial sender = p.ReadSerial();
        Serial gumpId = p.ReadSerial();
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

        if (linesNum == 0)
        {
            CreateGump(world, sender, gumpId, (int)x, (int)y, layout, lines);
            return;
        }

        clen = p.ReadUInt32BE() - 4;
        dlen = (int)p.ReadUInt32BE();
        decData = System.Buffers.ArrayPool<byte>.Shared.Rent(dlen);

        try
        {
            ZLib.Decompress(p.Buffer.Slice(p.Position, (int)clen), decData.AsSpan(0, dlen));
            p.Skip((int)clen);

            SpanReader reader = new(decData.AsSpan(0, dlen));

            for (int i = 0; i < linesNum; i++)
            {
                int remaining = reader.Remaining;

                if (remaining >= 2)
                {
                    int length = reader.ReadUInt16BE();
                    lines[i] = reader.ReadFixedString<UnicodeBE>(length);
                }
                else
                {
                    lines[i] = "";
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(decData);
        }

        CreateGump(world, sender, gumpId, (int)x, (int)y, layout, lines);
    }

    // 0xDE
    private static void UpdateMobileStatus(World world, ref SpanReader p)
    {
        _ = p.ReadUInt32BE();
        byte status = p.ReadUInt8();

        if (status == 1)
            _ = p.ReadUInt32BE();
    }

    // 0xDF
    private static void BuffDebuff(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        const ushort BUFF_ICON_START = 0x03E9;
        const ushort BUFF_ICON_START_NEW = 0x466;

        uint serial = p.ReadUInt32BE();
        BuffIconType ic = (BuffIconType)p.ReadUInt16BE();

        ushort iconId = (ushort)ic >= BUFF_ICON_START_NEW ? (ushort)(ic - (BUFF_ICON_START_NEW - 125)) : (ushort)((ushort)ic - BUFF_ICON_START);

        if (iconId >= BuffTable.Table.Length)
            return;

        BuffGump? gump = UIManager.GetGump<BuffGump>();
        ushort count = p.ReadUInt16BE();

        if (count == 0)
        {
            world.Player.RemoveBuff(ic);
            gump?.RequestUpdateContents();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            _ = p.ReadUInt16BE();
            p.Skip(2);
            _ = p.ReadUInt16BE();
            _ = p.ReadUInt16BE();
            p.Skip(4);
            ushort timer = p.ReadUInt16BE();
            p.Skip(3);
            uint titleCliloc = p.ReadUInt32BE();
            uint descriptionCliloc = p.ReadUInt32BE();
            uint wtfCliloc = p.ReadUInt32BE();
            _ = p.ReadUInt16BE();
            string str = p.ReadFixedString<UnicodeLE>(2);
            string args = str + p.ReadString<UnicodeLE>();
            string? title = Client.Game.UO.FileManager.Clilocs.Translate((int)titleCliloc, args, true);
            _ = p.ReadUInt16BE();
            string args_2 = p.ReadString<UnicodeLE>();
            string description = "";

            if (descriptionCliloc != 0)
            {
                description = "\n" + Client.Game.UO.FileManager.Clilocs.Translate((int)descriptionCliloc, string.IsNullOrEmpty(args_2)
                    ? args : args_2, true);

                if (description.Length < 2)
                    description = "";
            }

            _ = p.ReadUInt16BE();
            string args_3 = p.ReadString<UnicodeLE>();
            string wtf = "";

            if (wtfCliloc != 0)
            {
                wtf = Client.Game.UO.FileManager.Clilocs.Translate((int)wtfCliloc, String.IsNullOrEmpty(args_3) ? args : args_3, true);

                if (!string.IsNullOrWhiteSpace(wtf))
                    wtf = $"\n{wtf}";
            }

            string text = $"<left>{title}{description}{wtf}</left>";
            bool alreadyExists = world.Player.IsBuffIconExists(ic);
            world.Player.AddBuff(ic, BuffTable.Table[iconId], timer, text);

            if (!alreadyExists)
                gump?.RequestUpdateContents();
        }
    }

    // 0xE2
    private static void NewCharacterAnimation(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        Mobile? mobile = world.Mobiles.Get(p.ReadSerial());
        if (mobile is null)
            return;

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

    // 0xE5
    private static void DisplayWaypoint(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        byte map = p.ReadUInt8();
        WaypointsType type = (WaypointsType)p.ReadUInt16BE();
        bool ignoreobject = p.ReadUInt16BE() != 0;
        uint cliloc = p.ReadUInt32BE();
        string name = p.ReadString<UnicodeLE>();
    }

    // 0xE6
    private static void RemoveWaypoint(World world, ref SpanReader p)
    {
        _ = p.ReadUInt32BE();
    }

    // 0xF0
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

                Serial serial;

                while ((serial = p.ReadSerial()) != 0)
                {
                    if (!locations)
                        continue;

                    ushort x = p.ReadUInt16BE();
                    ushort y = p.ReadUInt16BE();
                    byte map = p.ReadUInt8();
                    int hits = type == 1 ? 0 : p.ReadUInt8();

                    world.WMapManager.AddOrUpdate(serial, x, y, hits, map, type == 0x02, null, true);
                }

                world.WMapManager.RemoveUnupdatedWEntity();

                break;

            case 0x03: break;  // runebook contents
            case 0x04: break;  // guardline data
            case 0xF0: break;
            case 0xFE: Client.Game.EnqueueAction(5000, () => { Log.Info("Razor ACK sent"); NetClient.Socket.SendRazorACK(); }); break;
        }
    }

    // 0xF3
    private static void UpdateItemSA(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

        p.Skip(2);
        byte type = p.ReadUInt8();
        Serial serial = p.ReadSerial();
        ushort graphic = p.ReadUInt16BE();
        byte graphicInc = p.ReadUInt8();
        ushort amount = p.ReadUInt16BE();
        _ = p.ReadUInt16BE();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        sbyte z = p.ReadInt8();
        Direction dir = (Direction)p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        _ = p.ReadUInt16BE();

        if (serial != world.Player)
        {
            UpdateGameObject(world, serial, graphic, graphicInc, amount, x, y, z, dir, hue, flags, type);

            if (graphic == 0x2006 && ProfileManager.CurrentProfile.AutoOpenCorpses)
                world.Player.TryOpenCorpses();
        }
        else if (p[0] == 0xF7)
        {
            UpdatePlayer(world, serial, graphic, graphicInc, hue, flags, x, y, z, dir);
        }
    }

    // 0xF5
    private static void DisplayMapF5(World world, ref SpanReader p)
    {
        Serial serial = p.ReadSerial();
        ushort gumpid = p.ReadUInt16BE();
        ushort startX = p.ReadUInt16BE();
        ushort startY = p.ReadUInt16BE();
        ushort endX = p.ReadUInt16BE();
        ushort endY = p.ReadUInt16BE();
        ushort width = p.ReadUInt16BE();
        ushort height = p.ReadUInt16BE();

        MapGump gump = new(world, serial, gumpid, width, height);
        SpriteInfo multiMapInfo;

        ushort facet = p.ReadUInt16BE();

        multiMapInfo = Client.Game.UO.MultiMaps.GetMap(facet, width, height, startX, startY, endX, endY);

        if (multiMapInfo.Texture is { } texture)
            gump.SetMapTexture(texture);

        UIManager.Add(gump);

        Item? it = world.Items.Get(serial);
        if (it is not null)
            it.Opened = true;
    }

    // 0xF6
    private static void BoatMoving(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        Serial serial = p.ReadSerial();
        byte boatSpeed = p.ReadUInt8();
        Direction movingDirection = (Direction)p.ReadUInt8() & Direction.Mask;
        Direction facingDirection = (Direction)p.ReadUInt8() & Direction.Mask;
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        ushort z = p.ReadUInt16BE();

        Item? multi = world.Items.Get(serial);
        if (multi is null)
            return;

        bool smooth = ProfileManager.CurrentProfile is { UseSmoothBoatMovement: true };

        if (smooth)
        {
            world.BoatMovingManager.AddStep(serial, boatSpeed, movingDirection, facingDirection, x, y, (sbyte)z);
        }
        else
        {
            multi.SetInWorldTile(x, y, (sbyte)z);

            if (world.HouseManager.TryGetHouse(serial, out House house))
                house.Generate(true, true, true);
        }

        int count = p.ReadUInt16BE();

        for (int i = 0; i < count; i++)
        {
            Serial cSerial = p.ReadSerial();
            ushort cx = p.ReadUInt16BE();
            ushort cy = p.ReadUInt16BE();
            ushort cz = p.ReadUInt16BE();

            if (cSerial == world.Player)
            {
                world.RangeSize.X = cx;
                world.RangeSize.Y = cy;
            }

            Entity? ent = world.Get(cSerial);
            if (ent is null)
                continue;

            if (smooth)
            {
                world.BoatMovingManager.PushItemToList(serial, cSerial, x - cx, y - cy, (sbyte)(z - cz));
                return;
            }

            if (cSerial == world.Player)
            {
                UpdatePlayer(world, cSerial, ent.Graphic, 0, ent.Hue, ent.Flags, cx, cy, (sbyte)cz, world.Player.Direction);
                return;
            }

            UpdateGameObject(world, cSerial, ent.Graphic, 0, (ushort)(ent.Graphic == 0x2006 ? ((Item)ent).Amount : 0),
                cx, cy, (sbyte)cz, ent.Serial.IsMobile ? ent.Direction : 0, ent.Hue, ent.Flags, 0);
        }
    }

    // 0xF7
    private static void PacketList(World world, ref SpanReader p)
    {
        if (world.Player is null)
            return;

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

    // 0xFD
    private static void LoginDelay(World world, ref SpanReader p)
    {
        if (world.InGame)
            return;

        LoginScene? scene = Client.Game.GetScene<LoginScene>();
        scene?.HandleLoginDelayPacket(ref p);
    }
}
