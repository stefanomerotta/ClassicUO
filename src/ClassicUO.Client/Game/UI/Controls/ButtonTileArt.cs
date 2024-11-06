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
using System.Collections.Generic;

namespace ClassicUO.Game.UI.Controls;

#nullable enable

internal class ButtonTileArt : Button
{
    private readonly ushort _hue;
    private readonly bool _isPartial;
    private readonly ushort _graphic;
    private readonly int _tileX;
    private readonly int _tileY;

    public ButtonTileArt(int id, ushort normal, ushort pressed, ushort graphic, ushort hue, int tileX, int tileY)
        : base(id, normal, pressed)
    {
        _graphic = graphic;
        _hue = hue;
        _tileX = tileX;
        _tileY = tileY;
        ContainsByBounds = true;

        ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(_graphic);

        if (artInfo.Texture is null)
        {
            Dispose();
            return;
        }

        _isPartial = Client.Game.UO.FileManager.TileData.StaticData[_graphic].IsPartialHue;
    }

    public ButtonTileArt(List<string> gparams) : base(gparams)
    {
        X = int.Parse(gparams[1]);
        Y = int.Parse(gparams[2]);
        _graphic = UInt16Converter.Parse(gparams[8]);
        _hue = UInt16Converter.Parse(gparams[9]);
        _tileX = int.Parse(gparams[10]);
        _tileY = int.Parse(gparams[11]);
        ContainsByBounds = true;
        IsFromServer = true;

        ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(_graphic);

        if (artInfo.Texture is null)
        {
            Dispose();
            return;
        }

        _isPartial = Client.Game.UO.FileManager.TileData.StaticData[_graphic].IsPartialHue;
    }

    public override bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        base.Draw(batcher, x, y);

        Vector3 hueVector = ShaderHueTranslator.GetHueVector(_hue, _isPartial, 1f);

        ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(_graphic);

        if (artInfo.Texture is null)
            return false;

        batcher.Draw(artInfo.Texture, new Vector2(x + _tileX, y + _tileY), artInfo.UV, hueVector);
        return true;
    }
}
