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

using ClassicUO.Game;
using ClassicUO.IO.Buffers;
using ClassicUO.Utility;
using System;

namespace ClassicUO.Network.Packets;

internal static partial class IncomingPackets
{
    static unsafe IncomingPackets()
    {
        Add(0x03, &ClientTalk);
        Add(0x0B, &Damage, 7);
        Add(0x11, &CharacterStatus);
        Add(0x15, &FollowR, 9);
        Add(0x16, &NewHealthbarUpdate);
        Add(0x17, &NewHealthbarUpdate);
        Add(0x1A, &UpdateItem);
        Add(0x1B, &EnterWorld, 37);
        Add(0x1C, &Talk);
        Add(0x1D, &DeleteObject, 5);
        Add(0x20, &UpdatePlayer, 19);
        Add(0x21, &DenyWalk, 8);
        Add(0x22, &ConfirmWalk, 3);
        Add(0x23, &DragAnimation, 26);
        Add(0x24, &OpenContainer, 9);
        Add(0x25, &UpdateContainedItem, 21);
        Add(0x27, &DenyMoveItem, 2);
        Add(0x28, &EndDraggingItem, 5);
        Add(0x29, &DropItemAccepted, 1);
        Add(0x2C, &DeathScreen, 2);
        Add(0x2D, &MobileAttributes, 17);
        Add(0x2E, &EquipItem, 15);
        Add(0x2F, &Swing, 10);
        SetNoOp(0x32, 2);
        Add(0x38, &Pathfinding, 7);
        Add(0x3A, &UpdateSkills);
        Add(0x3B, &CloseVendorInterface);
        Add(0x3C, &UpdateContainedItems);
        Add(0x4E, &PersonalLightLevel, 6);
        Add(0x4F, &LightLevel, 2);
        Add(0x54, &PlaySoundEffect, 12);
        Add(0x55, &LoginComplete, 1);
        Add(0x56, &MapData, 11);
        SetNoOp(0x5B, 4);
        Add(0x65, &SetWeather, 4);
        Add(0x66, &BookData);
        Add(0x6C, &TargetCursor, 19);
        Add(0x6D, &PlayMusic, 3);
        Add(0x6E, &CharacterAnimation, 14);
        Add(0x6F, &SecureTrading);
        Add(0x70, &GraphicEffect70, 28);
        Add(0x71, &BulletinBoardData);
        Add(0x72, &Warmode, 5);
        Add(0x73, &Ping, 2);
        Add(0x74, &BuyList);
        SetNoOp(0x76, 16);
        Add(0x77, &UpdateCharacter, 17);
        Add(0x78, &UpdateObject);
        Add(0x7C, &OpenMenu);
        Add(0x88, &OpenPaperdoll, 66);
        Add(0x89, &CorpseEquipment);
        Add(0x90, &DisplayMap90, 19);
        Add(0x93, &OpenBook93, 99);
        Add(0x95, &DyeData, 9);
        Add(0x97, &MovePlayer, 2);
        Add(0x98, &UpdateName);
        Add(0x99, &MultiPlacement, 30);
        Add(0x9A, &ASCIIPrompt);
        Add(0x9E, &SellList);
        Add(0xA1, &UpdateHitpoints, 9);
        Add(0xA2, &UpdateMana, 9);
        Add(0xA3, &UpdateStamina, 9);
        Add(0xA5, &OpenUrl);
        Add(0xA6, &TipWindow);
        Add(0xAA, &AttackCharacter, 5);
        Add(0xAB, &TextEntryDialog);
        Add(0xAE, &UnicodeTalk);
        Add(0xAF, &DisplayDeath, 13);
        Add(0xB0, &OpenGump);
        Add(0xB2, &ChatMessage);
        SetNoOp(0xB7);
        Add(0xB8, &CharacterProfile);
        Add(0xB9, &EnableLockedFeatures, 5);
        Add(0xBA, &DisplayQuestArrow, 10);
        SetNoOp(0xBB, 9);
        Add(0xBC, &Season, 3);
        Add(0xBD, &SendClientVersion);
        SetNoOp(0xBE);
        Add(0xBF, &ExtendedCommand);
        Add(0xC0, &GraphicEffectC0, 36);
        Add(0xC1, &DisplayClilocString);
        Add(0xC2, &UnicodePrompt);
        SetNoOp(0xC4, 6);
        SetNoOp(0xC6, 1);
        Add(0xC7, &GraphicEffectC7, 49);
        Add(0xC8, &ClientViewRange, 2);
        SetNoOp(0xCA, 6);
        SetNoOp(0xCB, 7);
        Add(0xCC, &DisplayClilocString);
        SetNoOp(0xD0);
        Add(0xD1, &Logout, 2);
        Add(0xD2, &UpdateCharacter, 25);
        Add(0xD3, &UpdateObject);
        Add(0xD4, &OpenBookD4);
        Add(0xD6, &MegaCliloc);
        SetNoOp(0xD7);
        Add(0xD8, &CustomHouse);
        SetNoOp(0xDB);
        Add(0xDC, &OPLInfo, 9);
        Add(0xDD, &OpenCompressedGump);
        Add(0xDE, &UpdateMobileStatus);
        Add(0xDF, &BuffDebuff);
        Add(0xE2, &NewCharacterAnimation, 10);
        Add(0xE3, &NotSupported);
        Add(0xE5, &DisplayWaypoint);
        SetNoOp(0xE6, 5);
        Add(0xF0, &KrriosClientSpecial);
        SetNoOp(0xF1, 9);
        Add(0xF3, &UpdateItemSA, 26);
        Add(0xF5, &DisplayMapF5, 21);
        Add(0xF6, &BoatMoving);
        Add(0xF7, &PacketList);

        // login          
        Add(0x53, &ReceiveLoginRejection, 2);
        Add(0x82, &ReceiveLoginRejection, 2);
        Add(0x85, &ReceiveLoginRejection, 2);
        Add(0x86, &UpdateCharacterList);
        Add(0x8C, &ReceiveServerRelay, 11);
        Add(0xA8, &ServerListReceived);
        Add(0xA9, &ReceiveCharacterList);
        Add(0xFD, &LoginDelay, 2);

        // extended - 0xBF
        AddExtended(0x01, &FastWalkPrevention);
        AddExtended(0x02, &FastWalkStack);
        AddExtended(0x04, &CloseGenericGump);
        AddExtended(0x06, &PartyCommands);
        AddExtended(0x08, &SetMap);
        AddExtended(0x0C, &CloseStatusbar);
        AddExtended(0x10, &DisplayEquipInfo);
        AddExtended(0x14, &DisplayPopupOrContextMenu);
        AddExtended(0x16, &CloseUserInterfaceWindows);
        AddExtended(0x18, &EnableMapPatches);
        AddExtended(0x19, &ExtendedStats);
        AddExtended(0x1B, &NewSpellbookContent);
        AddExtended(0x1D, &HouseRevisionState);
        AddExtended(0x20, &CustomHousing);
        AddExtended(0x21, &AbilityIcon);
        AddExtended(0x22, &DamageBF);
        AddExtended(0x25, &ChangeAbility);
        AddExtended(0x26, &MountSpeed);
        AddExtended(0x2A, &ChangeRace);
        AddExtended(0x2B, &UnknownBF);
    }

    public static unsafe void Configure(ClientVersion version)
    {
        if (version < ClientVersion.CV_7010400)
            SetNotSupported(0xFD);

        if (version < ClientVersion.CV_7090)
        {
            AdjustLength(0x24, 7);
            AdjustLength(0x99, 26);
            AdjustLength(0xBA, 6);
            AdjustLength(0xF3, 24);
        }

        if (version < ClientVersion.CV_60142)
            Add(0xB9, &EnableLockedFeatures, 3);

        if (version < ClientVersion.CV_6060)
            SetNotSupported(0xF1);

        if (version < ClientVersion.CV_6017)
            AdjustLength(0x25, 20);

        if (version < ClientVersion.CV_6013)
        {
            SetNoOp(0xE3, 77);
            SetNotSupported(0xE6);
        }

        if (version < ClientVersion.CV_500A)
            AdjustLength(0x16, 1);
    }

    public static unsafe void Add(byte id, delegate*<World, ref SpanReader, void> handler, byte length = 0)
    {
        _handlers[id] = new(length, handler);
    }

    public static unsafe void AdjustLength(byte packetId, byte length = 0)
    {
        PacketHandlerData old = _handlers[packetId];
        _handlers[packetId] = new(length, old.Handler);
    }

    public static unsafe void SetNotSupported(byte packetId)
    {
        _handlers[packetId] = new(0, &NotSupported);
    }

    public static unsafe void SetNoOp(byte packetId, byte length = 0)
    {
        _handlers[packetId] = new(length, &NoOp);
    }

    public static unsafe void AddExtended(byte extId, delegate*<World, ref SpanReader, void> handler)
    {
        _extendedHandlers[extId] = new(handler);
    }

    private static void NotSupported(World world, ref SpanReader p)
    {
        throw new Exception($"Packet {p[0]} is not supported with selected client version");
    }

    private static void NoOp(World world, ref SpanReader p)
    { }
}
