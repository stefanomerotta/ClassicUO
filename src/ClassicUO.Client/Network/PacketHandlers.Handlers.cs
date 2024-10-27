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
using ClassicUO.Resources;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Numerics;

namespace ClassicUO.Network;

#nullable enable

internal sealed partial class PacketHandlers
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

    static PacketHandlers()
    {
        Handler.Add(0x1B, EnterWorld);
        Handler.Add(0x55, LoginComplete);
        Handler.Add(0xBD, SendClientVersion);
        Handler.Add(0x03, ClientTalk);
        Handler.Add(0x0B, Damage);
        Handler.Add(0x11, CharacterStatus);
        Handler.Add(0x15, FollowR);
        Handler.Add(0x16, NewHealthbarUpdate);
        Handler.Add(0x17, NewHealthbarUpdate);
        Handler.Add(0x1A, UpdateItem);
        Handler.Add(0x1C, Talk);
        Handler.Add(0x1D, DeleteObject);
        Handler.Add(0x20, UpdatePlayer);
        Handler.Add(0x21, DenyWalk);
        Handler.Add(0x22, ConfirmWalk);
        Handler.Add(0x23, DragAnimation);
        Handler.Add(0x24, OpenContainer);
        Handler.Add(0x25, UpdateContainedItem);
        Handler.Add(0x27, DenyMoveItem);
        Handler.Add(0x28, EndDraggingItem);
        Handler.Add(0x29, DropItemAccepted);
        Handler.Add(0x2C, DeathScreen);
        Handler.Add(0x2D, MobileAttributes);
        Handler.Add(0x2E, EquipItem);
        Handler.Add(0x2F, Swing);
        Handler.Add(0x32, Unknown_0x32);
        Handler.Add(0x38, Pathfinding);
        Handler.Add(0x3A, UpdateSkills);
        Handler.Add(0x3B, CloseVendorInterface);
        Handler.Add(0x3C, UpdateContainedItems);
        Handler.Add(0x4E, PersonalLightLevel);
        Handler.Add(0x4F, LightLevel);
        Handler.Add(0x54, PlaySoundEffect);
        Handler.Add(0x56, MapData);
        Handler.Add(0x5B, SetTime);
        Handler.Add(0x65, SetWeather);
        Handler.Add(0x66, BookData);
        Handler.Add(0x6C, TargetCursor);
        Handler.Add(0x6D, PlayMusic);
        Handler.Add(0x6F, SecureTrading);
        Handler.Add(0x6E, CharacterAnimation);
        Handler.Add(0x70, GraphicEffect);
        Handler.Add(0x71, BulletinBoardData);
        Handler.Add(0x72, Warmode);
        Handler.Add(0x73, Ping);
        Handler.Add(0x74, BuyList);
        Handler.Add(0x77, UpdateCharacter);
        Handler.Add(0x78, UpdateObject);
        Handler.Add(0x7C, OpenMenu);
        Handler.Add(0x88, OpenPaperdoll);
        Handler.Add(0x89, CorpseEquipment);
        Handler.Add(0x90, DisplayMap);
        Handler.Add(0x93, OpenBook);
        Handler.Add(0x95, DyeData);
        Handler.Add(0x97, MovePlayer);
        Handler.Add(0x98, UpdateName);
        Handler.Add(0x99, MultiPlacement);
        Handler.Add(0x9A, ASCIIPrompt);
        Handler.Add(0x9E, SellList);
        Handler.Add(0xA1, UpdateHitpoints);
        Handler.Add(0xA2, UpdateMana);
        Handler.Add(0xA3, UpdateStamina);
        Handler.Add(0xA5, OpenUrl);
        Handler.Add(0xA6, TipWindow);
        Handler.Add(0xAA, AttackCharacter);
        Handler.Add(0xAB, TextEntryDialog);
        Handler.Add(0xAF, DisplayDeath);
        Handler.Add(0xAE, UnicodeTalk);
        Handler.Add(0xB0, OpenGump);
        Handler.Add(0xB2, ChatMessage);
        Handler.Add(0xB7, Help);
        Handler.Add(0xB8, CharacterProfile);
        Handler.Add(0xB9, EnableLockedFeatures);
        Handler.Add(0xBA, DisplayQuestArrow);
        Handler.Add(0xBB, UltimaMessengerR);
        Handler.Add(0xBC, Season);
        Handler.Add(0xBE, AssistVersion);
        Handler.Add(0xBF, ExtendedCommand);
        Handler.Add(0xC0, GraphicEffect);
        Handler.Add(0xC1, DisplayClilocString);
        Handler.Add(0xC2, UnicodePrompt);
        Handler.Add(0xC4, Semivisible);
        Handler.Add(0xC6, InvalidMapEnable);
        Handler.Add(0xC7, GraphicEffect);
        Handler.Add(0xC8, ClientViewRange);
        Handler.Add(0xCA, GetUserServerPingGodClientR);
        Handler.Add(0xCB, GlobalQueCount);
        Handler.Add(0xCC, DisplayClilocString);
        Handler.Add(0xD0, ConfigurationFileR);
        Handler.Add(0xD1, Logout);
        Handler.Add(0xD2, UpdateCharacter);
        Handler.Add(0xD3, UpdateObject);
        Handler.Add(0xD4, OpenBook);
        Handler.Add(0xD6, MegaCliloc);
        Handler.Add(0xD7, GenericAOSCommandsR);
        Handler.Add(0xD8, CustomHouse);
        Handler.Add(0xDB, CharacterTransferLog);
        Handler.Add(0xDC, OPLInfo);
        Handler.Add(0xDD, OpenCompressedGump);
        Handler.Add(0xDE, UpdateMobileStatus);
        Handler.Add(0xDF, BuffDebuff);
        Handler.Add(0xE2, NewCharacterAnimation);
        Handler.Add(0xE3, KREncryptionResponse);
        Handler.Add(0xE5, DisplayWaypoint);
        Handler.Add(0xE6, RemoveWaypoint);
        Handler.Add(0xF0, KrriosClientSpecial);
        Handler.Add(0xF1, FreeshardListR);
        Handler.Add(0xF3, UpdateItemSA);
        Handler.Add(0xF5, DisplayMap);
        Handler.Add(0xF6, BoatMoving);
        Handler.Add(0xF7, PacketList);

        // login
        Handler.Add(0xA8, ServerListReceived);
        Handler.Add(0x8C, ReceiveServerRelay);
        Handler.Add(0x86, UpdateCharacterList);
        Handler.Add(0xA9, ReceiveCharacterList);
        Handler.Add(0x82, ReceiveLoginRejection);
        Handler.Add(0x85, ReceiveLoginRejection);
        Handler.Add(0x53, ReceiveLoginRejection);
        Handler.Add(0xFD, LoginDelay);
    }

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

        Entity? entity = world.Get(p.ReadUInt32BE());
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

        uint serial = p.ReadUInt32BE();

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        string? oldName = entity.Name;
        entity.Name = p.ReadFixedString<ASCIICP1215>(30);
        entity.Hits = p.ReadUInt16BE();
        entity.HitsMax = p.ReadUInt16BE();

        if (entity.HitsRequest == HitsRequestStatus.Pending)
            entity.HitsRequest = HitsRequestStatus.Received;

        if (!SerialHelper.IsMobile(serial))
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

        if (p[0] == 0x16 && Client.Game.UO.Version < Utility.ClientVersion.CV_500A)
            return;

        Mobile? mobile = world.Mobiles.Get(p.ReadUInt32BE());
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

        uint serial = p.ReadUInt32BE();
        ushort count = 0;
        byte graphicInc = 0;
        byte direction = 0;
        ushort hue = 0;
        byte flags = 0;
        byte type = 0;

        if ((serial & 0x80000000) != 0)
        {
            serial &= 0x7FFFFFFF;
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

        UpdateGameObject(world, serial, graphic, graphicInc, count, x, y, z, (Direction)direction, hue, (Flags)flags, count, type, 1);
    }

    // 0x1B
    private static void EnterWorld(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();

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
        uint serial = p.ReadUInt32BE();
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

        uint serial = p.ReadUInt32BE();
        if (world.Player == serial)
            return;

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        bool updateAbilities = false;

        if (entity is Item item)
        {
            uint cont = item.Container & 0x7FFFFFFF;

            if (SerialHelper.IsValid(item.Container))
            {
                Entity? top = world.Get(item.RootContainer);

                if (top is not null && top == world.Player)
                {
                    updateAbilities = item.Layer is Layer.OneHanded or Layer.TwoHanded;

                    Item? tradeBoxItem = world.Player.GetSecureTradeBox();
                    if (tradeBoxItem is not null)
                        UIManager.GetTradingGump(tradeBoxItem)?.RequestUpdateContents();
                }

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

        if (world.CorpseManager.Exists(0, serial))
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
                    UIManager.GetGump<PaperDollGump>(cont)?.RequestUpdateContents();
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

        uint serial = p.ReadUInt32BE();
        ushort graphic = p.ReadUInt16BE();
        byte graphic_inc = p.ReadUInt8();
        ushort hue = p.ReadUInt16BE();
        Flags flags = (Flags)p.ReadUInt8();
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();
        ushort serverID = p.ReadUInt16BE();
        Direction direction = (Direction)p.ReadUInt8();
        sbyte z = p.ReadInt8();

        UpdatePlayer(world, serial, graphic, graphic_inc, hue, flags, x, y, z, serverID, direction);
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
        ushort count = p.ReadUInt16BE();
        uint source = p.ReadUInt32BE();
        ushort sourceX = p.ReadUInt16BE();
        ushort sourceY = p.ReadUInt16BE();
        sbyte sourceZ = p.ReadInt8();
        uint dest = p.ReadUInt32BE();
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
            source = 0;
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
            dest = 0;
        }
        else
        {
            destX = destEntity.X;
            destY = destEntity.Y;
            destZ = destEntity.Z;
        }

        GraphicEffectType effect = !SerialHelper.IsValid(source) || !SerialHelper.IsValid(dest)
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

        uint serial = p.ReadUInt32BE();
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

        uint serial = p.ReadUInt32BE();
        ushort graphic = (ushort)(p.ReadUInt16BE() + p.ReadUInt8());
        ushort amount = Math.Max((ushort)1, p.ReadUInt16BE());
        ushort x = p.ReadUInt16BE();
        ushort y = p.ReadUInt16BE();

        if (Client.Game.UO.Version >= ClientVersion.CV_6017)
            p.Skip(1);

        uint containerSerial = p.ReadUInt32BE();
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
                world.ObjectToRemove = 0;

            if (!SerialHelper.IsValid(itemHold.Serial) || itemHold.Graphic == 0xFFFF)
            {
                Log.Error($"Wrong data: serial = {itemHold.Serial:X8}  -  graphic = {itemHold.Graphic:X4}");
            }
            else
            {
                if (!itemHold.UpdatedInWorld)
                {
                    if (itemHold.Layer == Layer.Invalid && SerialHelper.IsValid(itemHold.Container))
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
                            if (SerialHelper.IsMobile(container.Serial))
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
                                world.RemoveItem(item, true);
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
        uint serial = p.ReadUInt32BE();

        Entity? entity = world.Get(serial);
        if (entity is null)
            return;

        entity.HitsMax = p.ReadUInt16BE();
        entity.Hits = p.ReadUInt16BE();

        if (entity.HitsRequest == HitsRequestStatus.Pending)
            entity.HitsRequest = HitsRequestStatus.Received;

        if (!SerialHelper.IsMobile(serial))
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

        uint serial = p.ReadUInt32BE();
        Item item = world.GetOrCreateItem(serial);

        if (item.Graphic != 0 && item.Layer != Layer.Backpack)
            world.RemoveItemFromContainer(item);

        if (SerialHelper.IsValid(item.Container))
        {
            UIManager.GetGump<ContainerGump>(item.Container)?.RequestUpdateContents();
            UIManager.GetGump<PaperDollGump>(item.Container)?.RequestUpdateContents();
        }

        item.Graphic = (ushort)(p.ReadUInt16BE() + p.ReadInt8());
        item.Layer = (Layer)p.ReadUInt8();
        item.Container = p.ReadUInt32BE();
        item.FixHue(p.ReadUInt16BE());
        item.Amount = 1;

        Entity? entity = world.Get(item.Container);
        entity?.PushToBack(item);

        if (item.Layer is >= Layer.ShopBuyRestock and <= Layer.ShopSell)
        { }
        else if (SerialHelper.IsValid(item.Container) && item.Layer < Layer.Mount)
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

        uint attackers = p.ReadUInt32BE();
        if (attackers != world.Player)
            return;

        uint defenders = p.ReadUInt32BE();

        const int TIME_TURN_TO_LASTTARGET = 2000;

        if (world.TargetManager.LastAttack != defenders
            || world.Player is not { InWarMode: true, Steps.Count: 0 } player
            || player.Walker.LastStepRequestTime + TIME_TURN_TO_LASTTARGET >= Time.Ticks)
        {
            return;
        }

        Mobile? enemy = world.Mobiles.Get(defenders);

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
            uint serial = p.ReadUInt32BE();
            ushort graphic = (ushort)(p.ReadUInt16BE() + p.ReadUInt8());
            ushort amount = Math.Max(p.ReadUInt16BE(), (ushort)1);
            ushort x = p.ReadUInt16BE();
            ushort y = p.ReadUInt16BE();

            if (Client.Game.UO.Version >= ClientVersion.CV_6017)
                p.Skip(1);

            uint containerSerial = p.ReadUInt32BE();
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

        uint serial = p.ReadUInt32BE();
        UIManager.GetGump<ShopGump>(serial)?.Dispose();
    }

    // 0x4E
    private static void PersonalLightLevel(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        if (world.Player != p.ReadUInt32BE())
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

        uint serial = p.ReadUInt32BE();

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

        uint serial = p.ReadUInt32BE();
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
        party.PartyHealTarget = 0;
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
        Mobile? mobile = world.Mobiles.Get(p.ReadUInt32BE());
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
        uint serial = p.ReadUInt32BE();

        if (type == 0)
        {
            uint id1 = p.ReadUInt32BE();
            uint id2 = p.ReadUInt32BE();

            // standard client doesn't allow the trading system if one of the traders is invisible (=not sent by server)
            if (world.Get(id1) is null || world.Get(id2) is null)
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

    // 0x71
    private static void BulletinBoardData(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        switch (p.ReadUInt8())
        {
            case 0: // open
                {
                    uint serial = p.ReadUInt32BE();
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
                    uint boardSerial = p.ReadUInt32BE();
                    BulletinBoardGump? bulletinBoard = UIManager.GetGump<BulletinBoardGump>(boardSerial);
                    if (bulletinBoard == null)
                        return;

                    uint serial = p.ReadUInt32BE();
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
                    uint boardSerial = p.ReadUInt32BE();
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

        Item? container = world.Items.Get(p.ReadUInt32BE());
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

    // 0x7C
    private static void OpenMenu(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        uint serial = p.ReadUInt32BE();
        ushort id = p.ReadUInt16BE();
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

                ref readonly Renderer.SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(graphic);

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
            GrayMenuGump gump = new GrayMenuGump(world, serial, id, name)
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

    // 0x88
    private static void OpenPaperdoll(World world, ref SpanReader p)
    {
        Mobile? mobile = world.Mobiles.Get(p.ReadUInt32BE());
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

        uint serial = p.ReadUInt32BE();

        Entity? corpse = world.Get(serial);
        if (corpse is null)
            return;

        // if it's not a corpse we should skip this [?]
        if (corpse.Graphic != 0x2006)
            return;

        Layer layer = (Layer)p.ReadUInt8();

        while (layer != Layer.Invalid && p.Position < p.Length)
        {
            uint item_serial = p.ReadUInt32BE();

            if (layer - 1 != Layer.Backpack)
            {
                Item item = world.GetOrCreateItem(item_serial);

                world.RemoveItemFromContainer(item);
                item.Container = serial;
                item.Layer = layer - 1;
                corpse.PushToBack(item);
            }

            layer = (Layer)p.ReadUInt8();
        }
    }

    // 0x95
    private static void DyeData(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();
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

        uint serial = p.ReadUInt32BE();
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

        Mobile? vendor = world.Mobiles.Get(p.ReadUInt32BE());
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
            uint serial = p.ReadUInt32BE();
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
        Entity? entity = world.Get(p.ReadUInt32BE());
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
        Mobile? mobile = world.Mobiles.Get(p.ReadUInt32BE());
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
        Mobile? mobile = world.Mobiles.Get(p.ReadUInt32BE());
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

        uint tip = p.ReadUInt32BE();
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

    // 0xAA
    private static void AttackCharacter(World world, ref SpanReader p)
    {
        uint serial = p.ReadUInt32BE();

        GameActions.SendCloseStatus(world, world.TargetManager.LastAttack);
        world.TargetManager.LastAttack = serial;
        GameActions.RequestMobileStatus(world, serial);
    }

    // 0xAB
    private static void TextEntryDialog(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        uint serial = p.ReadUInt32BE();
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

        uint serial = p.ReadUInt32BE();
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

        TextType text_type = TextType.SYSTEM;

        if (type == MessageType.Alliance || type == MessageType.Guild)
        {
            text_type = TextType.GUILD_ALLY;
        }
        else if (type == MessageType.System || serial == 0xFFFF_FFFF || serial == 0
            || name.Equals("system", StringComparison.CurrentCultureIgnoreCase) && entity == null)
        {
            // do nothing
        }
        else if (entity is not null)
        {
            text_type = TextType.OBJECT;

            if (string.IsNullOrEmpty(entity.Name))
                entity.Name = string.IsNullOrEmpty(name) ? text : name;
        }

        world.MessageManager.HandleMessage(entity, text, name, hue, type, ProfileManager.CurrentProfile.ChatFont, text_type, true);
    }

    // 0xAF
    private static void DisplayDeath(World world, ref SpanReader p)
    {
        if (!world.InGame)
            return;

        uint serial = p.ReadUInt32BE();
        uint corpseSerial = p.ReadUInt32BE();
        uint running = p.ReadUInt32BE();

        Mobile? owner = world.Mobiles.Get(serial);
        if (owner is null || serial == world.Player)
            return;

        serial |= 0x80000000;

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

        if (SerialHelper.IsValid(corpseSerial))
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

        uint sender = p.ReadUInt32BE();
        uint gumpId = p.ReadUInt32BE();
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

    // 0xC8
    private static void ClientViewRange(World world, ref SpanReader p)
    {
        world.ClientViewRange = p.ReadUInt8();
    }
}
