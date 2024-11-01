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

using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using ClassicUO.Utility;
using System;
using System.Runtime.CompilerServices;
using static ClassicUO.Network.NetClient;

namespace ClassicUO.Game.GameObjects;

#nullable enable

internal enum HitsRequestStatus
{
    None,
    Pending,
    Received
}

internal abstract class Entity : GameObject, IEquatable<Entity>
{
    private static readonly RenderedText?[] _hitsPercText = new RenderedText[101];

    public byte AnimIndex;
    public bool ExecuteAnimation = true;
    internal long LastAnimationChangeTime;
    public Flags Flags;
    public ushort Hits;
    public ushort HitsMax;
    public byte HitsPercentage;
    public bool IsClicked;
    public uint LastStepTime;
    public string? Name;
    public Serial Serial;
    public HitsRequestStatus HitsRequest;

    public bool IsHidden => (Flags & Flags.Hidden) != 0;
    public bool Exists => World.Contains(Serial);
    public RenderedText? HitsTexture => _hitsPercText[HitsPercentage % _hitsPercText.Length];

    public Direction Direction
    {
        get;
        set
        {
            if ((field) == value)
                return;

            field = value;
            OnDirectionChanged();
        }
    }

    protected Entity(World world, Serial serial)
        : base(world)
    {
        Serial = serial;
    }

    public bool Equals(Entity? e)
    {
        return e is not null && Serial == e.Serial;
    }

    public void FixHue(ushort hue)
    {
        ushort fixedColor = (ushort)(hue & 0x3FFF);

        if (fixedColor != 0)
        {
            if (fixedColor >= 0x0BB8)
                fixedColor = 1;

            fixedColor |= (ushort)(hue & 0xC000);
        }
        else
        {
            fixedColor = (ushort)(hue & 0x8000);
        }

        Hue = fixedColor;
    }

    public void UpdateHits(byte perc)
    {
        if (perc != HitsPercentage)
        {
            HitsPercentage = perc;

            ref RenderedText? rtext = ref _hitsPercText[perc % _hitsPercText.Length];
            if (rtext is { IsDestroyed: false })
                return;

            ushort color = perc switch
            {
                < 30 => 0x0021,
                < 50 => 0x0030,
                < 80 => 0x0058,
                _ => 0x0044
            };

            rtext = RenderedText.Create($"[{perc}%]", color, 3, false);
        }
    }

    public virtual void CheckGraphicChange(byte animIndex = 0)
    { }

    public override void Update()
    {
        base.Update();

        if (ObjectHandlesStatus == ObjectHandlesStatus.OPEN)
        {
            ObjectHandlesStatus = ObjectHandlesStatus.DISPLAYING;

            // TODO: Some servers may not want to receive this (causing original client to not send it),
            //but all servers tested (latest POL, old POL, ServUO, Outlands) do.
            if (Serial.IsMobile)
                Socket.SendNameRequest(Serial);

            UIManager.Add(new NameOverheadGump(World, Serial));
        }


        if (HitsMax > 0)
        {
            int perc = MathHelper.PercetangeOf(Hits, HitsMax);
            perc = Math.Clamp(perc, 0, 100);

            UpdateHits((byte)perc);
        }
    }

    public override void Destroy()
    {
        base.Destroy();

        GameActions.SendCloseStatus(World, Serial, HitsRequest >= HitsRequestStatus.Pending);

        AnimIndex = 0;
        LastAnimationChangeTime = 0;
    }

    public Item? FindItem(ushort graphic, ushort hue = 0xFFFF)
    {
        Item? item = null;

        if (hue == 0xFFFF)
        {
            int minColor = 0xFFFF;

            for (LinkedObject? i = Items; i != null; i = i.Next)
            {
                Item it = (Item)i;

                if (it.Graphic == graphic && it.Hue < minColor)
                {
                    item = it;
                    minColor = it.Hue;
                }

                if (it.Container.IsEntity)
                {
                    Item? found = it.FindItem(graphic, hue);

                    if (found is not null && found.Hue < minColor)
                    {
                        item = found;
                        minColor = found.Hue;
                    }
                }
            }
        }
        else
        {
            for (LinkedObject? i = Items; i != null; i = i.Next)
            {
                Item it = (Item)i;

                if (it.Graphic == graphic && it.Hue == hue)
                    item = it;

                if (it.Container.IsEntity)
                {
                    Item? found = it.FindItem(graphic, hue);
                    if (found is not null)
                        item = found;
                }
            }
        }

        return item;
    }

    public Item? GetItemByGraphic(ushort graphic, bool deepsearch = false)
    {
        for (LinkedObject? i = Items; i != null; i = i.Next)
        {
            Item item = (Item)i;

            if (item.Graphic == graphic)
                return item;

            if (!deepsearch || item.IsEmpty)
                continue;
            
            for (LinkedObject? ic = Items; ic != null; ic = ic.Next)
            {
                Item childItem = (Item)ic;

                Item? res = childItem.GetItemByGraphic(graphic, deepsearch);
                if (res is not null)
                    return res;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Item? FindItemByLayer(Layer layer)
    {
        for (LinkedObject? i = Items; i != null; i = i.Next)
        {
            Item it = (Item)i;

            if (!it.IsDestroyed && it.Layer == layer)
                return it;
        }

        return null;
    }

    public static implicit operator Serial(Entity entity)
    {
        return entity.Serial;
    }

    public static bool operator ==(Entity? e, Entity? s)
    {
        return Equals(e, s);
    }

    public static bool operator !=(Entity? e, Entity? s)
    {
        return !Equals(e, s);
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity ent && Equals(ent);
    }

    public override int GetHashCode()
    {
        return (int)Serial.Value;
    }

    public abstract void ProcessAnimation(bool evalutate = false);

    public abstract ushort GetGraphicForAnimation();
}