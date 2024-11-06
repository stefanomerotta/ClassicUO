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

using ClassicUO.Core;
using ClassicUO.Extensions;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network.Packets;
using ClassicUO.Utility;
using ClassicUO.Utility.Collections.Comparers;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace ClassicUO.Modules.Gumps;

#nullable enable

internal static partial class GumpCreator
{
    private static readonly FrozenDictionary<byte[], BuildControlDelegate>.AlternateLookup<ReadOnlySpan<byte>> activators;

    public static Gump? CreateGump(World world, Serial sender, Serial gumpId, int x, int y, ReadOnlySpan<byte> layout, string[] lines)
    {
        Gump? gump = null;
        bool mustBeAdded = true;

        if (UIManager.GetGumpCachePosition(gumpId, out Point pos))
        {
            x = pos.X;
            y = pos.Y;

            for (LinkedListNode<Gump>? last = UIManager.Gumps.Last; last is not null; last = last.Previous)
            {
                Control g = last.Value;

                if (g.IsDisposed || g.LocalSerial != sender || g.ServerSerial != gumpId)
                    continue;

                g.Clear();
                gump = g as Gump;
                mustBeAdded = false;

                break;
            }
        }
        else
        {
            UIManager.SavePosition(gumpId, new Point(x, y));
        }

        gump ??= new Gump(world, sender, gumpId)
        {
            X = x,
            Y = y,
            CanMove = true,
            CanCloseWithRightClick = true,
            CanCloseWithEsc = true,
            InvalidateContents = false,
            IsFromServer = true
        };

        GumpData data = new(world, gump, lines);

        foreach (ReadOnlySpan<byte> token in new GumpLayoutEnumerator(layout))
        {
            GumpArgumentsReader reader = new(token);
            ReadOnlySpan<byte> type = reader.ReadSpan();

            if (!activators.TryGetValue(type, out BuildControlDelegate? activator))
                continue;

            activator.Invoke(ref data, ref reader);
        }

        if (mustBeAdded)
            UIManager.Add(gump);

        gump.Update();
        gump.SetInScreen();

        return gump;
    }

    private static void ApplyTrans(Gump gump, int current_page, int x, int y, int width, int height)
    {
        int x2 = x + width;
        int y2 = y + height;
        ReadOnlySpan<Control> children = gump.Children;

        for (int i = 0; i < children.Length; i++)
        {
            Control child = children[i];
            bool canDraw = child.Page == 0 || current_page == child.Page;

            bool overlap =
                (x < child.X + child.Width)
                && (child.X < x2)
                && (y < child.Y + child.Height)
                && (child.Y < y2);

            if (canDraw && child.IsVisible && overlap)
                child.Alpha = 0.5f;
        }
    }

    static GumpCreator()
    {
        activators = new Dictionary<byte[], BuildControlDelegate>()
        {
            ["button"u8.ToArray()] = CreateButton,
            ["buttontileart"u8.ToArray()] = CreateButtonTileArt,
            ["checkertrans"u8.ToArray()] = CreateCheckedTrans,
            ["croppedtext"u8.ToArray()] = CreateCroppedText,
            ["gumppic"u8.ToArray()] = CreateGumpPic,
            ["gumppictiled"u8.ToArray()] = CreateGumpPicTiled,
            ["htmlgump"u8.ToArray()] = CreateHtmlGump,
            ["xmfhtmlgump"u8.ToArray()] = CreateXmfHtmlGump,
            ["xmfhtmlgumpcolor"u8.ToArray()] = CreateXmfHtmlGumpColor,
            ["xmfhtmltok"u8.ToArray()] = CreateXmfHtmlTok,
            ["page"u8.ToArray()] = SetPage,
            ["resizepic"u8.ToArray()] = CreateResizePic,
            ["textentrylimited"u8.ToArray()] = CreateTextEntryLimited,
            ["textentry"u8.ToArray()] = CreateTextEntry,
            ["tilepichue"u8.ToArray()] = CreateTilePic,
            ["tilepic"u8.ToArray()] = CreateTilePic,
            ["noclose"u8.ToArray()] = SetNoClose,
            ["nodispose"u8.ToArray()] = SetNoDispose,
            ["nomove"u8.ToArray()] = SetNoMove,
            ["group"u8.ToArray()] = SetGroup,
            ["endgroup"u8.ToArray()] = SetGroup,
            ["radio"u8.ToArray()] = CreateRadio,
            ["checkbox"u8.ToArray()] = CreateCheckbox,
            ["tooltip"u8.ToArray()] = CreateTooltip,
            ["itemproperty"u8.ToArray()] = CreateItemProperty,
            ["noresize"u8.ToArray()] = SetNoResize,
            ["mastergump"u8.ToArray()] = SetMastergump,
            ["picinpic"u8.ToArray()] = CreatePicInPic,
            ["gumppichued"u8.ToArray()] = CreateGumpPicHued,
            ["gumppicphued"u8.ToArray()] = CreateGumpPicHued,
            ["togglelimitgumpscale"u8.ToArray()] = NoOp,
        }
        .ToFrozenDictionary(ByteArrayEqualityComparer.Instance)
        .GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    private static void CreateButton(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort normal = reader.ReadInteger<ushort>();
        ushort pressed = reader.ReadInteger<ushort>();
        ButtonAction action = reader.ReadEnum<ButtonAction>();
        int toPage = reader.ReadInteger<int>();
        int buttonId = reader.ReadInteger<int>();

        Button control = new(buttonId, normal, pressed)
        {
            X = x,
            Y = y,
            ButtonAction = action,
            ToPage = toPage,
            WantUpdateSize = false,
            ContainsByBounds = true,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateButtonTileArt(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort normal = reader.ReadInteger<ushort>();
        ushort pressed = reader.ReadInteger<ushort>();
        ButtonAction action = reader.ReadEnum<ButtonAction>();
        int toPage = reader.ReadInteger<int>();
        int buttonId = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();
        ushort hue = reader.ReadInteger<ushort>();
        int tileX = reader.ReadInteger<ushort>();
        int tileY = reader.ReadInteger<ushort>();

        ButtonTileArt control = new(buttonId, normal, pressed, graphic, hue, tileX, tileY)
        {
            X = x,
            Y = y,
            ButtonAction = action,
            ToPage = toPage,
            WantUpdateSize = false,
            ContainsByBounds = true,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateCheckedTrans(ref GumpData data, ref GumpArgumentsReader reader)
    {
        CheckerTrans control = new()
        {
            X = reader.ReadInteger<int>(),
            Y = reader.ReadInteger<int>(),
            Width = reader.ReadInteger<int>(),
            Height = reader.ReadInteger<int>(),
            AcceptMouseInput = false,
            IsFromServer = true
        };

        data.Add(control);
        ApplyTrans(data.Gump, data.Page, control.X, control.Y, control.Width, control.Height);
    }

    private static void CreateCroppedText(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        ushort hue = reader.ReadInteger<ushort>();
        int stringIndex = reader.ReadInteger<int>();

        CroppedText control = new
        (
            text: stringIndex >= 0 && stringIndex < data.Lines.Length ? data.Lines[stringIndex] : "",
            hue: (ushort)(hue + 1),
            maxWidth: width
        )
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateGumpPic(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();
        bool isVirtuePic = false;

        if (reader.TryReadHueAttribute(out ushort hue))
        {
            hue++;
            isVirtuePic = reader.ReadVirtueClassAttribute();
        }

        GumpPic control = new(x, y, graphic, hue)
        {
            IsFromServer = true
        };

        if (!isVirtuePic)
        {
            data.Add(control);
            return;
        }

        control.ContainsByBounds = true;

        string lvl = hue switch
        {
            2403 => "",
            1154 or 1547 or 2213 or 235 or 18 or 2210 or 1348 => "Seeker of ",
            2404 or 1552 or 2216 or 2302 or 2118 or 618 or 2212 or 1352 => "Follower of ",
            43 or 53 or 1153 or 33 or 318 or 67 or 98 => "Knight of ",
            2406 => graphic == 0x6F ? "Seeker of " : "Knight of ",
            _ => "",
        };

        string? s = Client.Game.UO.FileManager.Clilocs.GetString(1051000 + graphic switch
        {
            0x69 => 2,
            0x6A => 7,
            0x6B => 5,
            0x6D => 6,
            0x6E => 1,
            0x6F => 3,
            0x70 => 4,
            _ => 0
        });

        if (string.IsNullOrEmpty(s))
            s = "Unknown virtue";

        control.SetTooltip(lvl + s, 100);

        data.Add(control);
    }

    private static void CreateGumpPicTiled(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();

        GumpPicTiled control = new(x, y, width, height, graphic)
        {
            CanMove = true,
            AcceptMouseInput = true,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateHtmlGump(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        int textIndex = reader.ReadInteger<int>();
        bool hasBackground = reader.ReadBool();
        bool hasScrollbar = reader.ReadBool();

        string text = textIndex >= 0 && textIndex <= data.Lines.Length ? data.Lines[textIndex] : "";

        HtmlControl control = new(x, y, width, height, hasBackground, hasScrollbar, false, text, ishtml: true)
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateXmfHtmlGump(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        int cliloc = reader.ReadCliloc();
        bool hasBackground = reader.ReadBool();
        int rawHasScrollbar = reader.ReadInteger<byte>();

        HtmlControl control = new(x, y, width, height, hasBackground, rawHasScrollbar != 0,
            hasBackground && rawHasScrollbar == 2, Client.Game.UO.FileManager.Clilocs.GetString(cliloc), ishtml: true)
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateXmfHtmlGumpColor(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        int cliloc = reader.ReadCliloc();
        bool hasBackground = reader.ReadBool();
        int rawHasScrollbar = reader.ReadInteger<byte>();
        int hue = reader.ReadInteger<int>();

        HtmlControl control = new(x, y, width, height, hasBackground, rawHasScrollbar == 0,
            hasBackground && rawHasScrollbar == 2, Client.Game.UO.FileManager.Clilocs.GetString(cliloc), hue, true)
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateXmfHtmlTok(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        bool hasBackground = reader.ReadBool();
        int rawHasScrollbar = reader.ReadInteger<byte>();
        int hue = reader.ReadInteger<int>();
        int cliloc = reader.ReadCliloc();
        ReadOnlySpan<byte> tags = reader.ReadArguments();

        if (hue == 0x7FFF)
            hue = 0x00FFFFFF;

        string? args;

        if (tags.IsEmpty)
            args = Client.Game.UO.FileManager.Clilocs.GetString(cliloc);
        else
            args = Client.Game.UO.FileManager.Clilocs.Translate(cliloc, Encoding.ASCII.GetString(tags));

        HtmlControl control = new(x, y, width, height, hasBackground, rawHasScrollbar == 0,
            hasBackground && rawHasScrollbar == 2, args, hue, true)
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void SetPage(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Page = reader.ReadInteger<int>();
    }

    private static void CreateResizePic(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();

        ResizePic control = new(graphic)
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsFromServer = true,
        };

        data.Add(control);
    }

    private static void CreateTextEntry(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        ushort hue = reader.ReadInteger<ushort>();
        Serial serial = reader.ReadSerial();
        int textIndex = reader.ReadInteger<int>();

        StbTextBox control = new
        (
            font: 1,
            maxCharCount: -1,
            maxWidth: width,
            style: FontStyle.BlackBorder | FontStyle.CropTexture,
            hue: (ushort)(hue + 1))
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            LocalSerial = serial,
            IsFromServer = true,
            Multiline = false,
        };

        if (textIndex >= 0 && textIndex < data.Lines.Length)
            control.SetText(data.Lines[textIndex]);

        if (!data.TextFocused)
        {
            control.SetKeyboardFocus();
            data.TextFocused = true;
        }

        data.Add(control);
    }

    private static void CreateTextEntryLimited(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        int width = reader.ReadInteger<int>();
        int height = reader.ReadInteger<int>();
        ushort hue = reader.ReadInteger<ushort>();
        Serial serial = reader.ReadSerial();
        int textIndex = reader.ReadInteger<int>();
        int maxCharCount = reader.ReadInteger<int>();

        StbTextBox control = new
        (
            font: 1,
            maxCharCount: maxCharCount,
            maxWidth: width,
            style: FontStyle.BlackBorder | FontStyle.CropTexture,
            hue: (ushort)(hue + 1))
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            LocalSerial = serial,
            IsFromServer = true,
            Multiline = false,
        };

        if (textIndex >= 0 && textIndex <= data.Lines.Length)
            control.SetText(data.Lines[textIndex]);

        if (!data.TextFocused)
        {
            control.SetKeyboardFocus();
            data.TextFocused = true;
        }

        data.Add(control);
    }

    private static void CreateTilePic(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();
        ushort hue = reader.ReadOptionalInteger<ushort>();

        StaticPic control = new(graphic, hue)
        {
            X = x,
            Y = y,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void SetNoClose(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Gump.CanCloseWithRightClick = false;
    }

    private static void SetNoDispose(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Gump.CanCloseWithEsc = false;
    }

    private static void SetNoMove(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Gump.CanMove = false;
    }

    private static void SetGroup(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Group++;
    }

    private static void CreateRadio(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort inactive = reader.ReadInteger<ushort>();
        ushort active = reader.ReadInteger<ushort>();
        bool @checked = reader.ReadBool();
        Serial serial = reader.ReadSerial();

        RadioButton control = new(data.Group, inactive, active)
        {
            X = x,
            Y = y,
            IsChecked = @checked,
            LocalSerial = serial,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateCheckbox(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort inactive = reader.ReadInteger<ushort>();
        ushort active = reader.ReadInteger<ushort>();
        bool @checked = reader.ReadBool();
        Serial serial = reader.ReadSerial();

        Checkbox control = new(inactive, active)
        {
            X = x,
            Y = y,
            IsChecked = @checked,
            LocalSerial = serial,
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateTooltip(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int cliloc = reader.ReadCliloc();
        ReadOnlySpan<byte> tags = reader.ReadArguments();

        string? text;

        if (tags.IsEmpty)
            text = Client.Game.UO.FileManager.Clilocs.GetString(cliloc);
        else
            text = Client.Game.UO.FileManager.Clilocs.Translate(cliloc, Encoding.ASCII.GetString(tags));

        ReadOnlySpan<Control> children = data.Gump.Children;
        Control? last = !children.IsEmpty ? children[^1] : null;

        if (last is not null)
        {
            if (last.HasTooltip)
            {
                if (last.Tooltip is string s)
                {
                    s += '\n' + text;
                    last.SetTooltip(s);
                }
            }
            else
            {
                last.SetTooltip(text);
            }

            last.Priority = ClickPriority.High;
            last.AcceptMouseInput = true;
        }
    }

    private static void CreateItemProperty(ref GumpData data, ref GumpArgumentsReader reader)
    {
        if (!data.World.ClientFeatures.TooltipsEnabled || data.Gump.Children.IsEmpty)
            return;

        Serial serial = reader.ReadSerial();

        data.Gump.Children[^1].SetTooltip(serial);

        if (serial.IsEntity && (!data.World.OPL.TryGetRevision(serial, out uint rev) || rev == 0))
            OutgoingPackets.AddMegaClilocRequest(serial);
    }

    private static void SetNoResize(ref GumpData data, ref GumpArgumentsReader reader)
    { }

    private static void SetMastergump(ref GumpData data, ref GumpArgumentsReader reader)
    {
        data.Gump.MasterGumpSerial = new(reader.ReadOptionalInteger<uint>());
    }

    private static void CreatePicInPic(ref GumpData data, ref GumpArgumentsReader reader)
    {
        GumpPicInPic control = new
        (
            x: reader.ReadInteger<int>(),
            y: reader.ReadInteger<int>(),
            graphic: reader.ReadInteger<ushort>(),
            sx: reader.ReadInteger<ushort>(),
            sy: reader.ReadInteger<ushort>(),
            width: reader.ReadInteger<ushort>(),
            height: reader.ReadInteger<ushort>()
        )
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void CreateGumpPicHued(ref GumpData data, ref GumpArgumentsReader reader)
    {
        int x = reader.ReadInteger<int>();
        int y = reader.ReadInteger<int>();
        ushort graphic = reader.ReadInteger<ushort>();

        if (reader.TryReadHueAttribute(out ushort hue))
            hue++;

        GumpPic control = new(x, y, graphic, hue)
        {
            IsFromServer = true
        };

        data.Add(control);
    }

    private static void NoOp(ref GumpData data, ref GumpArgumentsReader reader)
    { }


    private delegate void BuildControlDelegate(ref GumpData data, ref GumpArgumentsReader reader);

    private ref struct GumpData
    {
        public readonly string[] Lines;
        public readonly Gump Gump;
        public readonly World World;
        public int Page;
        public int Group;
        public bool TextFocused;

        public GumpData(World world, Gump gump, string[] lines)
        {
            World = world;
            Gump = gump;
            Lines = lines;
        }

        public readonly void Add(Control control)
        {
            Gump.Add(control, Page);
        }
    }
}
