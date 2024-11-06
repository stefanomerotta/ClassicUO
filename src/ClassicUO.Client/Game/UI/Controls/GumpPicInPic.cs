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

using System.Collections.Generic;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls;

internal class GumpPicInPic : GumpPicBase
{
    private readonly Rectangle _picInPicBounds;

    public GumpPicInPic(
        int x,
        int y,
        ushort graphic,
        ushort sx,
        ushort sy,
        ushort width,
        ushort height
    )
    {
        X = x;
        Y = y;
        Graphic = graphic;
        Width = width;
        Height = height;
        _picInPicBounds = new Rectangle(sx, sy, Width, Height);
        IsFromServer = true;
    }

    public GumpPicInPic(List<string> parts)
        : this(
            int.Parse(parts[1]),
            int.Parse(parts[2]),
            UInt16Converter.Parse(parts[3]),
            UInt16Converter.Parse(parts[4]),
            UInt16Converter.Parse(parts[5]),
            UInt16Converter.Parse(parts[6]),
            UInt16Converter.Parse(parts[7])
        )
    { }

    public override bool Contains(int x, int y)
    {
        return true;
    }

    public override bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        if (IsDisposed)
        {
            return false;
        }

        Vector3 hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha, true);

        ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(Graphic);

        var sourceBounds = new Rectangle(gumpInfo.UV.X + _picInPicBounds.X, gumpInfo.UV.Y + _picInPicBounds.Y, _picInPicBounds.Width, _picInPicBounds.Height);

        if (gumpInfo.Texture != null)
        {
            batcher.Draw(
                gumpInfo.Texture,
                new Rectangle(x, y, Width, Height),
                sourceBounds,
                hueVector
            );
        }

        return base.Draw(batcher, x, y);
    }
}
