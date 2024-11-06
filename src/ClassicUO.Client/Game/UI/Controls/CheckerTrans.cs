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

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ClassicUO.Game.UI.Controls;

#nullable enable

internal sealed class CheckerTrans : Control
{
    public CheckerTrans(List<string> parts)
    {
        X = int.Parse(parts[1]);
        Y = int.Parse(parts[2]);
        Width = int.Parse(parts[3]);
        Height = int.Parse(parts[4]);
        AcceptMouseInput = false;
        IsFromServer = true;
    }

    public CheckerTrans()
    { }

    public override bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, 0.5f);
        batcher.Draw(SolidColorTextureCache.GetTexture(Color.Black), new Rectangle(x, y, Width, Height), hueVector);

        return true;
    }
}