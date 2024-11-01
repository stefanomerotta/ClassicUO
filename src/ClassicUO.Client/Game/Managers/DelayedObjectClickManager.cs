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
using ClassicUO.Game.GameObjects;
using ClassicUO.Input;

namespace ClassicUO.Game.Managers;

#nullable enable

internal sealed class DelayedObjectClickManager
{
    private readonly World _world;

    public Serial Serial { get; private set; }
    public bool IsEnabled { get; private set; }
    public uint Timer { get; private set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int LastMouseX { get; set; }
    public int LastMouseY { get; set; }

    public DelayedObjectClickManager(World world)
    {
        _world = world;
    }

    public void Update()
    {
        if (!IsEnabled || Timer > Time.Ticks)
            return;

        Entity? entity = _world.Get(Serial);

        if (entity is not null)
        {
            if (!_world.ClientFeatures.TooltipsEnabled || Serial.IsItem && ((Item)entity).IsLocked && ((Item)entity).ItemData.Weight == 255 && !((Item)entity).ItemData.IsContainer)
                GameActions.SingleClick(_world, Serial);

            if (_world.ClientFeatures.PopupEnabled)
                GameActions.OpenPopupMenu(Serial);
        }

        Clear();
    }

    public void Set(Serial serial, int x, int y, uint timer)
    {
        Serial = serial;
        LastMouseX = Mouse.Position.X;
        LastMouseY = Mouse.Position.Y;
        X = x;
        Y = y;
        Timer = timer;
        IsEnabled = true;
    }

    public void Clear()
    {
        IsEnabled = false;
        Serial = Serial.MinusOne;
        Timer = 0;
    }

    public void Clear(uint serial)
    {
        if (Serial == serial)
        {
            Timer = 0;
            Serial = Serial.Zero;
            IsEnabled = false;
            X = 0;
            Y = 0;
            LastMouseX = 0;
            LastMouseY = 0;
        }
    }
}