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
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Resources;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System;
using static ClassicUO.Network.NetClient;

namespace ClassicUO.Game;

#nullable enable

internal static class GameActions
{
    public static int LastSpellIndex { get; set; } = 1;
    public static int LastSkillIndex { get; set; } = 1;

    public static void ToggleWarMode(PlayerMobile player)
    {
        RequestWarMode(player, !player.InWarMode);
    }

    public static void RequestWarMode(PlayerMobile player, bool war)
    {
        if (!player.IsDead)
        {
            if (war && ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.EnableMusic)
                Client.Game.Audio.PlayMusic((RandomHelper.GetValue(0, 3) % 3) + 38, true);

            else if (!war)
                Client.Game.Audio.StopWarMusic();
        }

        Socket.SendChangeWarMode(war);
    }

    public static void OpenMacroGump(World world, string name)
    {
        MacroGump? macroGump = UIManager.GetGump<MacroGump>();

        macroGump?.Dispose();
        UIManager.Add(new MacroGump(world, name));
    }

    public static void OpenPaperdoll(World world, Serial serial)
    {
        PaperDollGump? paperDollGump = UIManager.GetGump<PaperDollGump>(serial);

        if (paperDollGump is null)
        {
            DoubleClick(world, serial.ToVirtual());
        }
        else
        {
            if (paperDollGump.IsMinimized)
                paperDollGump.IsMinimized = false;

            paperDollGump.SetInScreen();
            paperDollGump.BringOnTop();
        }
    }

    public static void OpenSettings(World world, int page = 0)
    {
        OptionsGump? opt = UIManager.GetGump<OptionsGump>();

        if (opt is null)
        {
            OptionsGump optionsGump = new(world)
            {
                X = (Client.Game.Window.ClientBounds.Width >> 1) - 300,
                Y = (Client.Game.Window.ClientBounds.Height >> 1) - 250
            };

            UIManager.Add(optionsGump);
            optionsGump.ChangePage(page);
            optionsGump.SetInScreen();
        }
        else
        {
            opt.SetInScreen();
            opt.BringOnTop();
        }
    }

    public static void OpenStatusBar(World world)
    {
        Client.Game.Audio.StopWarMusic();

        if (StatusGumpBase.GetStatusGump() is null)
            UIManager.Add(StatusGumpBase.AddStatusGump(world, 100, 100));
    }

    public static void OpenJournal(World world)
    {
        JournalGump? journalGump = UIManager.GetGump<JournalGump>();

        if (journalGump is null)
        {
            UIManager.Add(new JournalGump(world) { X = 64, Y = 64 });
        }
        else
        {
            journalGump.SetInScreen();
            journalGump.BringOnTop();

            if (journalGump.IsMinimized)
                journalGump.IsMinimized = false;
        }
    }

    public static void OpenSkills(World world)
    {
        StandardSkillsGump? skillsGump = UIManager.GetGump<StandardSkillsGump>();

        if (skillsGump is not null && skillsGump.IsMinimized)
        {
            skillsGump.IsMinimized = false;
        }
        else
        {
            world.SkillsRequested = true;
            Socket.SendSkillsRequest(world.Player.Serial);
        }
    }

    public static void OpenMiniMap(World world)
    {
        MiniMapGump? miniMapGump = UIManager.GetGump<MiniMapGump>();

        if (miniMapGump is null)
        {
            UIManager.Add(new MiniMapGump(world));
        }
        else
        {
            miniMapGump.ToggleSize();
            miniMapGump.SetInScreen();
            miniMapGump.BringOnTop();
        }
    }

    public static void OpenWorldMap(World world)
    {
        WorldMapGump? worldMap = UIManager.GetGump<WorldMapGump>();

        if (worldMap is not { IsDisposed: false })
        {
            worldMap = new WorldMapGump(world);
            UIManager.Add(worldMap);
        }
        else
        {
            worldMap.BringOnTop();
            worldMap.SetInScreen();
        }
    }

    public static void OpenChat(World world)
    {
        if (world.ChatManager.ChatIsEnabled == ChatStatus.Enabled)
        {
            ChatGump? chatGump = UIManager.GetGump<ChatGump>();

            if (chatGump is null)
            {
                UIManager.Add(new ChatGump(world));
            }
            else
            {
                chatGump.SetInScreen();
                chatGump.BringOnTop();
            }
        }
        else if (world.ChatManager.ChatIsEnabled == ChatStatus.EnabledUserRequest)
        {
            ChatGumpChooseName? chatGump = UIManager.GetGump<ChatGumpChooseName>();

            if (chatGump is null)
            {
                UIManager.Add(new ChatGumpChooseName(world));
            }
            else
            {
                chatGump.SetInScreen();
                chatGump.BringOnTop();
            }
        }
    }

    public static bool OpenCorpse(World world, Serial serial)
    {
        if (!serial.IsItem)
            return false;

        Item? item = world.Items.Get(serial);
        if (item is not { IsCorpse: true, IsDestroyed: false })
            return false;

        world.Player.ManualOpenedCorpses.Add(serial);
        DoubleClick(world, serial);

        return true;
    }

    public static bool OpenBackpack(World world)
    {
        Item? backpack = world.Player.FindItemByLayer(Layer.Backpack);
        if (backpack is null)
            return false;

        ContainerGump? backpackGump = UIManager.GetGump<ContainerGump>(backpack.Serial);

        if (backpackGump is null)
        {
            DoubleClick(world, backpack.Serial);
        }
        else
        {
            if (backpackGump.IsMinimized)
                backpackGump.IsMinimized = false;

            backpackGump.SetInScreen();
            backpackGump.BringOnTop();
        }

        return true;
    }

    public static void Attack(World world, Serial serial)
    {
        if (ProfileManager.CurrentProfile.EnabledCriminalActionQuery)
        {
            Mobile? m = world.Mobiles.Get(serial);

            if (m is not null && (world.Player.NotorietyFlag is NotorietyFlag.Innocent or NotorietyFlag.Ally)
                && m.NotorietyFlag == NotorietyFlag.Innocent && m != world.Player)
            {
                QuestionGump messageBox = new QuestionGump(world, ResGeneral.ThisMayFlagYouCriminal, s =>
                {
                    if (s)
                        Socket.SendAttackRequest(serial);
                });

                UIManager.Add(messageBox);

                return;
            }
        }

        world.TargetManager.NewTargetSystemSerial = serial;
        world.TargetManager.LastAttack = serial;
        Socket.SendAttackRequest(serial);
    }

    public static void DoubleClickQueued(Serial serial)
    {
        Client.Game.GetScene<GameScene>()?.DoubleClickDelayed(serial);
    }

    public static void DoubleClick(World world, Serial serial)
    {
        if (serial != world.Player && serial.IsMobile && world.Player.InWarMode)
        {
            RequestMobileStatus(world, serial);
            Attack(world, serial);
        }
        else
        {
            Socket.SendDoubleClick(serial);
        }

        if (serial.IsItem || serial.IsMobile && (world.Mobiles.Get(serial)?.IsHuman ?? false))
            world.LastObject = serial;
        else
            world.LastObject = Serial.Zero;
    }

    public static void SingleClick(World world, Serial serial)
    {
        // add  request context menu
        Socket.SendClickRequest(serial);

        Entity? entity = world.Get(serial);

        if (entity is not null)
            entity.IsClicked = true;
    }

    public static void Say(string message, ushort hue = 0xFFFF, MessageType type = MessageType.Regular, byte font = 3)
    {
        if (hue == 0xFFFF)
            hue = ProfileManager.CurrentProfile.SpeechHue;

        // TODO: identify what means 'older client' that uses ASCIISpeechRquest [0x03]
        //
        // Fix -> #1267
        if (Client.Game.UO.Version >= ClientVersion.CV_200)
            Socket.SendUnicodeSpeechRequest(message, type, font, hue, Settings.GlobalSettings.Language);
        else
            Socket.SendASCIISpeechRequest(message, type, font, hue);
    }

    public static void Print(World world, string message, ushort hue = 946, MessageType type = MessageType.Regular,
        byte font = 3, bool unicode = true)
    {
        Print(world, null, message, hue, type, font, unicode);
    }

    public static void Print(World world, Entity entity, string message, ushort hue = 946,
        MessageType type = MessageType.Regular, byte font = 3, bool unicode = true)
    {
        world.MessageManager.HandleMessage(entity, message, entity is not null ? entity.Name : "System",
            hue, type, font, entity is null ? TextType.SYSTEM : TextType.OBJECT, unicode);
    }

    public static void SayParty(string message)
    {
        Socket.SendPartyMessage(message, Serial.Zero);
    }

    public static void SayParty(string message, Serial serial)
    {
        Socket.SendPartyMessage(message, serial);
    }

    public static void RequestPartyAccept(Serial serial)
    {
        Socket.SendPartyAccept(serial);

        UIManager.GetGump<PartyInviteGump>()?.Dispose();
    }

    public static void RequestPartyRemoveMemberByTarget()
    {
        Socket.SendPartyRemoveRequest(Serial.Zero);
    }

    public static void RequestPartyRemoveMember(Serial serial)
    {
        Socket.SendPartyRemoveRequest(serial);
    }

    public static void RequestPartyQuit(PlayerMobile player)
    {
        Socket.SendPartyRemoveRequest(player.Serial);
    }

    public static void RequestPartyInviteByTarget()
    {
        Socket.SendPartyInviteRequest();
    }

    public static void RequestPartyLootState(bool isLootable)
    {
        Socket.SendPartyChangeLootTypeRequest(isLootable);
    }

    public static bool PickUp(World world, Serial serial, int x, int y, int amount = -1, Point? offset = null, bool is_gump = false)
    {
        if (world.Player.IsDead || Client.Game.UO.GameCursor.ItemHold.Enabled)
            return false;

        Item? item = world.Items.Get(serial);

        if (item is not { IsDestroyed: false, IsMulti: false } || item.OnGround && (item.IsLocked || item.Distance > Constants.DRAG_ITEMS_DISTANCE))
            return false;

        if (amount <= -1 && item.Amount > 1 && item.ItemData.IsStackable)
        {
            if (ProfileManager.CurrentProfile.HoldShiftToSplitStack == Keyboard.Shift)
            {
                SplitMenuGump? gump = UIManager.GetGump<SplitMenuGump>(item.Serial);
                if (gump is not null)
                    return false;

                gump = new SplitMenuGump(world, item, new Point(x, y))
                {
                    X = Mouse.Position.X - 80,
                    Y = Mouse.Position.Y - 40
                };

                UIManager.Add(gump);
                UIManager.AttemptDragControl(gump, true);

                return true;
            }
        }

        if (amount <= 0)
            amount = item.Amount;

        Client.Game.UO.GameCursor.ItemHold.Clear();
        Client.Game.UO.GameCursor.ItemHold.Set(item, (ushort)amount, offset);
        Client.Game.UO.GameCursor.ItemHold.IsGumpTexture = is_gump;
        Socket.SendPickUpRequest(item.Serial, (ushort)amount);

        if (item.OnGround)
            item.RemoveFromTile();

        item.TextContainer?.Clear();

        world.ObjectToRemove = item.Serial;

        return true;
    }

    public static void DropItem(Serial serial, int x, int y, int z, Serial container)
    {
        if (Client.Game.UO.GameCursor.ItemHold is { Enabled: true, IsFixedPosition: true } hold
            && (hold.Serial != container || hold.ItemData.IsStackable))
        {
            Socket.SendDropRequest(serial, x, y, z, container);

            hold.Enabled = false;
            hold.Dropped = true;
        }
    }

    public static void Equip(World world)
    {
        Equip(world, Serial.MinusOne);
    }

    public static void Equip(World world, Serial container)
    {
        if (Client.Game.UO.GameCursor.ItemHold is not { Enabled: true, IsFixedPosition: false, IsWearable: true } itemHold)
            return;

        if (!container.IsEntity)
            container = world.Player.Serial;

        Socket.SendEquipRequest(itemHold.Serial, (Layer)itemHold.ItemData.Layer, container);

        itemHold.Enabled = false;
        itemHold.Dropped = true;
    }

    public static void ReplyGump(Serial local, Serial server, int button, ReadOnlySpan<uint> switches = default,
        ReadOnlySpan<(ushort, string)> entries = default)
    {
        Socket.SendGumpResponse(local, server, button, switches, entries);
    }

    public static void RequestHelp()
    {
        Socket.SendHelpRequest();
    }

    public static void RequestQuestMenu(World world)
    {
        Socket.SendQuestMenuRequest(world);
    }

    public static void RequestProfile(Serial serial)
    {
        Socket.SendProfileRequest(serial);
    }

    public static void ChangeSkillLockStatus(ushort skillindex, byte lockstate)
    {
        Socket.SendSkillStatusChangeRequest(skillindex, lockstate);
    }

    public static void RequestMobileStatus(World world, Serial serial, bool force = false)
    {
        if (world.InGame)
        {
            Entity ent = world.Get(serial);

            if (ent != null)
            {
                if (force)
                {
                    if (ent.HitsRequest >= HitsRequestStatus.Pending)
                    {
                        SendCloseStatus(world, serial);
                    }
                }

                if (ent.HitsRequest < HitsRequestStatus.Received)
                {
                    ent.HitsRequest = HitsRequestStatus.Pending;
                    force = true;
                }
            }

            if (force && serial.IsEntity)
            {
                //ent = ent ?? World.Player;
                //ent.AddMessage(MessageType.Regular, $"PACKET SENT: 0x{serial:X8}", 3, 0x34, true, TextType.OBJECT);
                Socket.SendStatusRequest(serial);
            }
        }
    }

    public static void SendCloseStatus(World world, Serial serial, bool force = false)
    {
        if (Client.Game.UO.Version >= ClientVersion.CV_200 && world.InGame)
        {
            Entity ent = world.Get(serial);

            if (ent != null && ent.HitsRequest >= HitsRequestStatus.Pending)
            {
                ent.HitsRequest = HitsRequestStatus.None;
                force = true;
            }

            if (force && serial.IsEntity)
            {
                //ent = ent ?? World.Player;
                //ent.AddMessage(MessageType.Regular, $"PACKET REMOVED SENT: 0x{serial:X8}", 3, 0x34 + 10, true, TextType.OBJECT);
                Socket.SendCloseStatusBarGump(serial);
            }
        }
    }

    public static void CastSpellFromBook(int index, Serial bookSerial)
    {
        if (index >= 0)
        {
            LastSpellIndex = index;
            Socket.SendCastSpellFromBook(index, bookSerial);
        }
    }

    public static void CastSpell(int index)
    {
        if (index >= 0)
        {
            LastSpellIndex = index;
            Socket.SendCastSpell(index);
        }
    }

    public static void OpenGuildGump(World world)
    {
        Socket.SendGuildMenuRequest(world);
    }

    public static void ChangeStatLock(byte stat, Lock state)
    {
        Socket.SendStatLockStateRequest(stat, state);
    }

    public static void Rename(Serial serial, string name)
    {
        Socket.SendRenameRequest(serial, name);
    }

    public static void UseSkill(int index)
    {
        if (index >= 0)
        {
            LastSkillIndex = index;
            Socket.SendUseSkill(index);
        }
    }

    public static void OpenPopupMenu(Serial serial, bool shift = false)
    {
        shift = shift || Keyboard.Shift;

        if (ProfileManager.CurrentProfile.HoldShiftForContext && !shift)
        {
            return;
        }

        Socket.SendRequestPopupMenu(serial);
    }

    public static void ResponsePopupMenu(Serial serial, ushort index)
    {
        Socket.SendPopupMenuSelection(serial, index);
    }

    public static void MessageOverhead(World world, string message, Serial entity)
    {
        Print(world, world.Get(entity), message);
    }

    public static void MessageOverhead(World world, string message, ushort hue, Serial entity)
    {
        Print(world, world.Get(entity), message, hue);
    }

    public static void AcceptTrade(Serial serial, bool accepted)
    {
        Socket.SendTradeResponse(serial, 2, accepted);
    }

    public static void CancelTrade(Serial serial)
    {
        Socket.SendTradeResponse(serial, 1, false);
    }

    public static void AllNames(World world)
    {
        foreach (Mobile mobile in world.Mobiles.Values)
        {
            if (mobile != world.Player)
            {
                Socket.SendClickRequest(mobile.Serial);
            }
        }

        foreach (Item item in world.Items.Values)
        {
            if (item.IsCorpse)
            {
                Socket.SendClickRequest(item.Serial);
            }
        }
    }

    public static void OpenDoor()
    {
        Socket.SendOpenDoor();
    }

    public static void EmoteAction(string action)
    {
        Socket.SendEmoteAction(action);
    }

    public static void OpenAbilitiesBook(World world)
    {
        if (UIManager.GetGump<CombatBookGump>() == null)
        {
            UIManager.Add(new CombatBookGump(world, 100, 100));
        }
    }

    private static void SendAbility(World world, byte idx, bool primary)
    {
        if ((world.ClientLockedFeatures.Flags & LockedFeatureFlags.AOS) == 0)
        {
            if (primary)
                Socket.SendStunRequest();
            else
                Socket.SendDisarmRequest();
        }
        else
        {
            Socket.SendUseCombatAbility(world, idx);
        }
    }

    public static void UsePrimaryAbility(World world)
    {
        ref var ability = ref world.Player.Abilities[0];

        if (((byte)ability & 0x80) == 0)
        {
            for (int i = 0; i < 2; i++)
            {
                world.Player.Abilities[i] &= (Ability)0x7F;
            }

            SendAbility(world, (byte)ability, true);
        }
        else
        {
            SendAbility(world, 0, true);
        }

        ability ^= (Ability)0x80;
    }

    public static void UseSecondaryAbility(World world)
    {
        ref Ability ability = ref world.Player.Abilities[1];

        if (((byte)ability & 0x80) == 0)
        {
            for (int i = 0; i < 2; i++)
            {
                world.Player.Abilities[i] &= (Ability)0x7F;
            }

            SendAbility(world, (byte)ability, false);
        }
        else
        {
            SendAbility(world, 0, true);
        }

        ability ^= (Ability)0x80;
    }

    // ===================================================
    [Obsolete("temporary workaround to not break assistants")]
    public static void UsePrimaryAbility() => UsePrimaryAbility(ClassicUO.Client.Game.UO.World);

    [Obsolete("temporary workaround to not break assistants")]
    public static void UseSecondaryAbility() => UseSecondaryAbility(ClassicUO.Client.Game.UO.World);
    // ===================================================

    public static void QuestArrow(bool rightClick)
    {
        Socket.SendClickQuestArrow(rightClick);
    }

    public static void GrabItem(World world, Serial serial, ushort amount)
    {
        GrabItem(world, serial, amount, Serial.Zero);
    }

    public static void GrabItem(World world, Serial serial, ushort amount, Serial bag)
    {
        Item? backpack = world.Player.FindItemByLayer(Layer.Backpack);
        if (backpack is null)
            return;

        if (!bag.IsValid)
            bag = ProfileManager.CurrentProfile.GrabBagSerial.IsValid ? backpack.Serial : ProfileManager.CurrentProfile.GrabBagSerial;

        if (!world.Items.Contains(bag))
        {
            Print(world, ResGeneral.GrabBagNotFound);
            ProfileManager.CurrentProfile.GrabBagSerial = Serial.Zero;
            bag = backpack.Serial;
        }

        PickUp(world, serial, 0, 0, amount);
        DropItem(serial, 0xFFFF, 0xFFFF, 0, bag);
    }
}
