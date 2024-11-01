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

internal sealed partial class IncomingPackets
{
    public static IncomingPackets Handler { get; } = new();

    public static unsafe void Configure(ClientVersion version)
    {
        Prepopulate();

        if (version < ClientVersion.CV_7010400)
            Handler.SetNotSupported(0xFD);

        if (version < ClientVersion.CV_7090)
        {
            Handler.AdjustLength(0x24, 7);
            Handler.AdjustLength(0x99, 26);
            Handler.AdjustLength(0xBA, 6);
            Handler.AdjustLength(0xF3, 24);
        }

        if (version < ClientVersion.CV_60142)
            Handler.Add(0xB9, 3, &EnableLockedFeatures);

        if (version < ClientVersion.CV_6060)
            Handler.SetNotSupported(0xF1);

        if (version < ClientVersion.CV_6017)
            Handler.AdjustLength(0x25, 20);

        if (version < ClientVersion.CV_6013)
        {
            Handler.Add(0xE3, 77, &NoOp);
            Handler.SetNotSupported(0xE6);
        }

        if (version < ClientVersion.CV_500A)
            Handler.AdjustLength(0x16, 1);
    }

    public unsafe void Add(byte id, short length, delegate*<World, ref SpanReader, void> handler)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        _handlers[id] = new(id, length, handler);
    }

    public unsafe void Add(byte id, delegate*<World, ref SpanReader, void> handler)
    {
        _handlers[id] = new(id, -1, handler);
    }

    public unsafe void AdjustLength(byte packetId, short length = 0)
    {
        if (length == 0)
            length = -1;

        PacketHandlerData old = _handlers[packetId];
        _handlers[packetId] = new(packetId, length, old.Handler);
    }

    public unsafe void SetNotSupported(byte packetId)
    {
        _handlers[packetId] = new(packetId, -1, &NotSupported);
    }

    private static void NoOp(World world, ref SpanReader p)
    { }

    private static void NotSupported(World world, ref SpanReader p)
    {
        throw new Exception();
    }

    private static unsafe void Prepopulate()
    {
        Handler.Add(0x03, &ClientTalk);
        Handler.Add(0x0B, 7, &Damage);
        Handler.Add(0x11, &CharacterStatus);
        Handler.Add(0x15, 9, &FollowR);
        Handler.Add(0x16, &NewHealthbarUpdate);
        Handler.Add(0x17, &NewHealthbarUpdate);
        Handler.Add(0x1A, &UpdateItem);
        Handler.Add(0x1B, 37, &EnterWorld);
        Handler.Add(0x1C, &Talk);
        Handler.Add(0x1D, 5, &DeleteObject);
        Handler.Add(0x20, 19, &UpdatePlayer);
        Handler.Add(0x21, 8, &DenyWalk);
        Handler.Add(0x22, 3, &ConfirmWalk);
        Handler.Add(0x23, 26, &DragAnimation);
        Handler.Add(0x24, 9, &OpenContainer);
        Handler.Add(0x25, 21, &UpdateContainedItem);
        Handler.Add(0x27, 2, &DenyMoveItem);
        Handler.Add(0x28, 5, &EndDraggingItem);
        Handler.Add(0x29, 1, &DropItemAccepted);
        Handler.Add(0x2C, 2, &DeathScreen);
        Handler.Add(0x2D, 17, &MobileAttributes);
        Handler.Add(0x2E, 15, &EquipItem);
        Handler.Add(0x2F, 10, &Swing);
        Handler.Add(0x32, 2, &NoOp);
        Handler.Add(0x38, 7, &Pathfinding);
        Handler.Add(0x3A, &UpdateSkills);
        Handler.Add(0x3B, &CloseVendorInterface);
        Handler.Add(0x3C, &UpdateContainedItems);
        Handler.Add(0x4E, 6, &PersonalLightLevel);
        Handler.Add(0x4F, 2, &LightLevel);
        Handler.Add(0x54, 12, &PlaySoundEffect);
        Handler.Add(0x55, 1, &LoginComplete);
        Handler.Add(0x56, 11, &MapData);
        Handler.Add(0x5B, 4, &NoOp);
        Handler.Add(0x65, 4, &SetWeather);
        Handler.Add(0x66, &BookData);
        Handler.Add(0x6C, 19, &TargetCursor);
        Handler.Add(0x6D, 3, &PlayMusic);
        Handler.Add(0x6E, 14, &CharacterAnimation);
        Handler.Add(0x6F, &SecureTrading);
        Handler.Add(0x70, 28, &GraphicEffect70);
        Handler.Add(0x71, &BulletinBoardData);
        Handler.Add(0x72, 5, &Warmode);
        Handler.Add(0x73, 2, &Ping);
        Handler.Add(0x74, &BuyList);
        Handler.Add(0x76, 16, &NoOp);
        Handler.Add(0x77, 17, &UpdateCharacter);
        Handler.Add(0x78, &UpdateObject);
        Handler.Add(0x7C, &OpenMenu);
        Handler.Add(0x88, 66, &OpenPaperdoll);
        Handler.Add(0x89, &CorpseEquipment);
        Handler.Add(0x90, 19, &DisplayMap90);
        Handler.Add(0x93, 99, &OpenBook93);
        Handler.Add(0x95, 9, &DyeData);
        Handler.Add(0x97, 2, &MovePlayer);
        Handler.Add(0x98, &UpdateName);
        Handler.Add(0x99, 30, &MultiPlacement);
        Handler.Add(0x9A, &ASCIIPrompt);
        Handler.Add(0x9E, &SellList);
        Handler.Add(0xA1, 9, &UpdateHitpoints);
        Handler.Add(0xA2, 9, &UpdateMana);
        Handler.Add(0xA3, 9, &UpdateStamina);
        Handler.Add(0xA5, &OpenUrl);
        Handler.Add(0xA6, &TipWindow);
        Handler.Add(0xAA, 5, &AttackCharacter);
        Handler.Add(0xAB, &TextEntryDialog);
        Handler.Add(0xAE, &UnicodeTalk);
        Handler.Add(0xAF, 13, &DisplayDeath);
        Handler.Add(0xB0, &OpenGump);
        Handler.Add(0xB2, &ChatMessage);
        Handler.Add(0xB7, &NoOp);
        Handler.Add(0xB8, &CharacterProfile);
        Handler.Add(0xB9, 5, &EnableLockedFeatures);
        Handler.Add(0xBA, 10, &DisplayQuestArrow);
        Handler.Add(0xBB, 9, &NoOp);
        Handler.Add(0xBC, 3, &Season);
        Handler.Add(0xBD, &SendClientVersion);
        Handler.Add(0xBE, &NoOp);
        Handler.Add(0xBF, &ExtendedCommand);
        Handler.Add(0xC0, 36, &GraphicEffectC0);
        Handler.Add(0xC1, &DisplayClilocString);
        Handler.Add(0xC2, &UnicodePrompt);
        Handler.Add(0xC4, 6, &NoOp);
        Handler.Add(0xC6, 1, &NoOp);
        Handler.Add(0xC7, 49, &GraphicEffectC7);
        Handler.Add(0xC8, 2, &ClientViewRange);
        Handler.Add(0xCA, 6, &NoOp);
        Handler.Add(0xCB, 7, &NoOp);
        Handler.Add(0xCC, &DisplayClilocString);
        Handler.Add(0xD0, &NoOp);
        Handler.Add(0xD1, 2, &Logout);
        Handler.Add(0xD2, 25, &UpdateCharacter);
        Handler.Add(0xD3, &UpdateObject);
        Handler.Add(0xD4, &OpenBookD4);
        Handler.Add(0xD6, &MegaCliloc);
        Handler.Add(0xD7, &NoOp);
        Handler.Add(0xD8, &CustomHouse);
        Handler.Add(0xDB, &NoOp);
        Handler.Add(0xDC, 9, &OPLInfo);
        Handler.Add(0xDD, &OpenCompressedGump);
        Handler.Add(0xDE, &UpdateMobileStatus);
        Handler.Add(0xDF, &BuffDebuff);
        Handler.Add(0xE2, 10, &NewCharacterAnimation);
        Handler.Add(0xE3, &NotSupported);
        Handler.Add(0xE5, &DisplayWaypoint);
        Handler.Add(0xE6, 5, &NoOp);
        Handler.Add(0xF0, &KrriosClientSpecial);
        Handler.Add(0xF1, 9, &NoOp);
        Handler.Add(0xF3, 26, &UpdateItemSA);
        Handler.Add(0xF5, 21, &DisplayMapF5);
        Handler.Add(0xF6, &BoatMoving);
        Handler.Add(0xF7, &PacketList);

        // login          
        Handler.Add(0x53, 2, &ReceiveLoginRejection);
        Handler.Add(0x82, 2, &ReceiveLoginRejection);
        Handler.Add(0x85, 2, &ReceiveLoginRejection);
        Handler.Add(0x86, &UpdateCharacterList);
        Handler.Add(0x8C, 11, &ReceiveServerRelay);
        Handler.Add(0xA8, &ServerListReceived);
        Handler.Add(0xA9, &ReceiveCharacterList);
        Handler.Add(0xFD, 2, &LoginDelay);
    }
}
