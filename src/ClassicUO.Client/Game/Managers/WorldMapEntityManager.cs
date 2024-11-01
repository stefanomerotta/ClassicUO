﻿// Copyright (c) 2024, andreakarasho
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

using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using ClassicUO.Network.Encryptions;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

#nullable enable

internal class WMapEntity
{
    public WMapEntity(Serial serial)
    {
        Serial = serial;

        //var mob = World.Mobiles.Get(serial);

        //if (mob != null)
        //    GetName();
    }

    public bool IsGuild;
    public uint LastUpdate;
    public string Name;
    public readonly Serial Serial;
    public int X, Y, HP, Map;
}

internal sealed class WorldMapEntityManager
{
    private bool _ackReceived;
    private uint _lastUpdate, _lastPacketSend, _lastPacketRecv;
    private readonly List<WMapEntity> _toRemove = new List<WMapEntity>();
    private readonly World _world;

    public WorldMapEntityManager(World world) { _world = world; }

    public bool Enabled
    {
        get
        {
            return ((_world.ClientFeatures.Flags & CharacterListFlags.CLF_NEW_MOVEMENT_SYSTEM) == 0 || _ackReceived) &&
                    NetClient.Socket.EncryptionType == EncryptionType.NONE &&
                    ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.WorldMapShowParty && 
                    UIManager.GetGump<WorldMapGump>() != null; // horrible, but works
        }
    }

    public readonly Dictionary<Serial, WMapEntity> Entities = [];

    public void SetACKReceived()
    {
        _ackReceived = true;
    }

    public void SetEnable(bool v)
    {
        if ((_world.ClientFeatures.Flags & CharacterListFlags.CLF_NEW_MOVEMENT_SYSTEM) != 0 && !_ackReceived)
        {
            Log.Warn("Server support new movement system. Can't use the 0xF0 packet to query guild/party position");
            v = false;
        }
        else if (NetClient.Socket.EncryptionType != EncryptionType.NONE && !_ackReceived)
        {
            Log.Warn("Server has encryption. Can't use the 0xF0 packet to query guild/party position");
            v = false;
        }

        if (v)
        {
            RequestServerPartyGuildInfo(true);
        }
    }

    public void AddOrUpdate
    (
        Serial serial,
        int x,
        int y,
        int hp,
        int map,
        bool isguild,
        string name = null,
        bool from_packet = false
    )
    {
        if (from_packet)
        {
            _lastPacketRecv = Time.Ticks + 10000;
        }
        else if (_lastPacketRecv < Time.Ticks)
        {
            return;
        }

        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            Entity ent = _world.Get(serial);

            if (ent != null && !string.IsNullOrEmpty(ent.Name))
            {
                name = ent.Name;
            }
        }

        if (!Entities.TryGetValue(serial, out WMapEntity entity) || entity == null)
        {
            entity = new WMapEntity(serial)
            {
                X = x, Y = y, HP = hp, Map = map,
                LastUpdate = Time.Ticks + 1000,
                IsGuild = isguild,
                Name = name
            };

            Entities[serial] = entity;
        }
        else
        {
            entity.X = x;
            entity.Y = y;
            entity.HP = hp;
            entity.Map = map;
            entity.IsGuild = isguild;
            entity.LastUpdate = Time.Ticks + 1000;

            if (string.IsNullOrEmpty(entity.Name) && !string.IsNullOrEmpty(name))
            {
                entity.Name = name;
            }
        }
    }

    public void Remove(Serial serial)
    {
        if (Entities.ContainsKey(serial))
        {
            Entities.Remove(serial);
        }
    }

    public void RemoveUnupdatedWEntity()
    {
        if (_lastUpdate > Time.Ticks)
        {
            return;
        }

        _lastUpdate = Time.Ticks + 1000;

        long ticks = Time.Ticks - 1000;

        foreach (WMapEntity entity in Entities.Values)
        {
            if (entity.LastUpdate < ticks)
            {
                _toRemove.Add(entity);
            }
        }

        if (_toRemove.Count != 0)
        {
            foreach (WMapEntity entity in _toRemove)
            {
                Entities.Remove(entity.Serial);
            }

            _toRemove.Clear();
        }
    }

    public WMapEntity? GetEntity(Serial serial)
    {
        Entities.TryGetValue(serial, out WMapEntity? entity);
        return entity;
    }

    public void RequestServerPartyGuildInfo(bool force = false)
    {
        if (!force && !Enabled)
        {
            return;
        }

        if (_world.InGame && _lastPacketSend < Time.Ticks)
        {
            //GameActions.Print($"SENDING PACKET! {Time.Ticks}");

            _lastPacketSend = Time.Ticks + 250;

            //if (!force && !_can_send)
            //{
            //    return;
            //}

            NetClient.Socket.SendQueryGuildPosition();

            if (_world.Party != null && _world.Party.Leader != 0)
            {
                foreach (PartyMember e in _world.Party.Members)
                {
                    if (e != null && e.Serial.IsEntity)
                    {
                        Mobile mob = _world.Mobiles.Get(e.Serial);

                        if (mob == null || mob.Distance > _world.ClientViewRange)
                        {
                            NetClient.Socket.SendQueryPartyPosition();

                            break;
                        }
                    }
                }
            }
        }
    }

    public void Clear()
    {
        Entities.Clear();
        _ackReceived = false;
        SetEnable(false);
    }
}