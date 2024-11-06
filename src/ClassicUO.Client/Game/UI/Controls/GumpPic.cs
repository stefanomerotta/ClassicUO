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

using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ClassicUO.Game.UI.Controls;

#nullable enable

internal class GumpPic : GumpPicBase
{
    public GumpPic(int x, int y, ushort graphic, ushort hue)
    {
        X = x;
        Y = y;
        Graphic = graphic;
        Hue = hue;
        IsFromServer = true;
    }

    public GumpPic(List<string> parts)
        : this(int.Parse(parts[1]), int.Parse(parts[2]), UInt16Converter.Parse(parts[3]), (ushort)(parts.Count > 4
                ? TransformHue((ushort)(UInt16Converter.Parse(parts[4].AsSpan(parts[4].IndexOf('=') + 1)) + 1))
              : 0))
    { }

    public bool IsPartialHue { get; set; }
    public bool ContainsByBounds { get; set; }

    public override bool Contains(int x, int y)
    {
        return ContainsByBounds || base.Contains(x, y);
    }

    private static ushort TransformHue(ushort hue)
    {
        if (hue <= 2)
            hue = 0;

        return hue;
    }

    public override bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        if (IsDisposed)
            return false;

        Vector3 hueVector = ShaderHueTranslator.GetHueVector(Hue, IsPartialHue, Alpha, true);

        ref readonly SpriteInfo gumpInfo = ref Client.Game.UO.Gumps.GetGump(Graphic);

        if (gumpInfo.Texture is not null)
            batcher.Draw(gumpInfo.Texture, new Rectangle(x, y, Width, Height), gumpInfo.UV, hueVector);

        return base.Draw(batcher, x, y);
    }
}

