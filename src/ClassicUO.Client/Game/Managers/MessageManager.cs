﻿#region license

// Copyright (c) 2024, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility;
using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers;

#nullable enable

internal enum AffixType : byte
{
    Append = 0x00,
    Prepend = 0x01,
    System = 0x02,
    None = 0xFF
}

internal sealed class MessageManager
{
    private readonly World _world;

    public event EventHandler<MessageEventArgs>? MessageReceived;
    public event EventHandler<MessageEventArgs>? LocalizedMessageReceived;

    public PromptData PromptData { get; set; }

    public MessageManager(World world)
    {
        _world = world;
    }

    public void HandleMessage
    (
        Entity? parent,
        string text,
        string name,
        ushort hue,
        MessageType type,
        byte font,
        TextType textType,
        bool unicode = false
    )
    {
        if (string.IsNullOrEmpty(text))
            return;

        Profile? currentProfile = ProfileManager.CurrentProfile;

        if (currentProfile is { OverrideAllFonts: true })
        {
            font = currentProfile.ChatFont;
            unicode = currentProfile.OverrideAllFontsIsUnicode;
        }

        switch (type)
        {
            case MessageType.Command:
            case MessageType.Encoded:
            case MessageType.System:
            case MessageType.Party:
                break;

            case MessageType.Guild:
                if (currentProfile.IgnoreGuildMessages) return;
                break;

            case MessageType.Alliance:
                if (currentProfile.IgnoreAllianceMessages) return;
                break;

            case MessageType.Spell:
                {
                    //server hue color per default
                    if (!SpellDefinition.WordToTargettype.TryGetValue(text, out SpellDefinition? spell))
                        goto case MessageType.Label;

                    if (currentProfile is { EnabledSpellFormat: true } && !string.IsNullOrWhiteSpace(currentProfile.SpellDisplayFormat))
                    {
                        ValueStringBuilder sb = new(currentProfile.SpellDisplayFormat.AsSpan());
                        {
                            sb.Replace("{power}".AsSpan(), spell.PowerWords.AsSpan());
                            sb.Replace("{spell}".AsSpan(), spell.Name.AsSpan());

                            text = sb.ToString().Trim();
                        }
                        sb.Dispose();
                    }

                    //server hue color per default if not enabled
                    if (currentProfile is { EnabledSpellHue: true })
                    {
                        hue = spell.TargetType switch
                        {
                            TargetType.Beneficial => currentProfile.BeneficHue,
                            TargetType.Harmful => currentProfile.HarmfulHue,
                            _ => currentProfile.NeutralHue,
                        };
                    }

                    goto case MessageType.Label;
                }

            default:
            case MessageType.Focus:
            case MessageType.Whisper:
            case MessageType.Yell:
            case MessageType.Regular:
            case MessageType.Label:
            case MessageType.Limit3Spell:

                if (parent is null)
                    break;

                // If person who send that message is in ignores list - but filter out Spell Text
                if (_world.IgnoreManager.IgnoredCharsList.Contains(parent.Name) && type != MessageType.Spell)
                    break;

                TextObject msg = CreateMessage(text, hue, font, unicode, type, textType);
                msg.Owner = parent;

                if (parent is Item { OnGround: false } item)
                {
                    msg.X = _world.DelayedObjectClickManager.X;
                    msg.Y = _world.DelayedObjectClickManager.Y;
                    msg.IsTextGump = true;
                    bool found = false;

                    for (LinkedListNode<Gump>? gump = UIManager.Gumps.Last; gump != null; gump = gump.Previous)
                    {
                        Control g = gump.Value;

                        if (!g.IsDisposed)
                        {
                            switch (g)
                            {
                                case PaperDollGump paperDoll when g.LocalSerial == item.Container:
                                    paperDoll.AddText(msg);
                                    found = true;

                                    break;

                                case ContainerGump container when g.LocalSerial == item.Container:
                                    container.AddText(msg);
                                    found = true;

                                    break;

                                case TradingGump trade when trade.ID1 == item.Container || trade.ID2 == item.Container:
                                    trade.AddText(msg);
                                    found = true;

                                    break;
                            }
                        }

                        if (found)
                            break;
                    }
                }

                parent.AddMessage(msg);

                break;
        }

        MessageReceived.Raise(new MessageEventArgs(parent, text, name, hue, type, font, textType, unicode), parent);
    }

    public void OnLocalizedMessage(Entity entity, MessageEventArgs args)
    {
        LocalizedMessageReceived.Raise(args, entity);
    }

    public TextObject CreateMessage
    (
        string msg,
        ushort hue,
        byte font,
        bool isunicode,
        MessageType type,
        TextType textType
    )
    {
        if (ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.OverrideAllFonts)
        {
            font = ProfileManager.CurrentProfile.ChatFont;
            isunicode = ProfileManager.CurrentProfile.OverrideAllFontsIsUnicode;
        }

        int width = isunicode ? Client.Game.UO.FileManager.Fonts.GetWidthUnicode(font, msg) : Client.Game.UO.FileManager.Fonts.GetWidthASCII(font, msg);

        if (width > 200)
        {
            width = isunicode ?
                Client.Game.UO.FileManager.Fonts.GetWidthExUnicode
                (
                    font,
                    msg,
                    200,
                    TEXT_ALIGN_TYPE.TS_LEFT,
                    (ushort)FontStyle.BlackBorder
                ) :
                Client.Game.UO.FileManager.Fonts.GetWidthExASCII
                (
                    font,
                    msg,
                    200,
                    TEXT_ALIGN_TYPE.TS_LEFT,
                    (ushort)FontStyle.BlackBorder
                );
        }
        else
        {
            width = 0;
        }


        ushort fixedColor = (ushort)(hue & 0x3FFF);

        if (fixedColor != 0)
        {
            if (fixedColor >= 0x0BB8)
            {
                fixedColor = 1;
            }

            fixedColor |= (ushort)(hue & 0xC000);
        }
        else
        {
            fixedColor = (ushort)(hue & 0x8000);
        }


        TextObject textObject = TextObject.Create(_world);
        textObject.Alpha = 0xFF;
        textObject.Type = type;
        textObject.Hue = fixedColor;

        if (!isunicode && textType == TextType.OBJECT)
        {
            fixedColor = 0x7FFF;
        }

        textObject.RenderedText = RenderedText.Create
        (
            msg,
            fixedColor,
            font,
            isunicode,
            FontStyle.BlackBorder,
            TEXT_ALIGN_TYPE.TS_LEFT,
            width,
            30,
            false,
            false,
            textType == TextType.OBJECT
        );

        textObject.Time = CalculateTimeToLive(textObject.RenderedText);
        textObject.RenderedText.Hue = textObject.Hue;

        return textObject;
    }

    private static long CalculateTimeToLive(RenderedText rtext)
    {
        Profile currentProfile = ProfileManager.CurrentProfile;

        if (currentProfile == null)
        {
            return 0;
        }

        long timeToLive;

        if (currentProfile.ScaleSpeechDelay)
        {
            int delay = currentProfile.SpeechDelay;

            if (delay < 10)
            {
                delay = 10;
            }

            timeToLive = (long)(4000 * rtext.LinesCount * delay / 100.0f);
        }
        else
        {
            long delay = (5497558140000 * currentProfile.SpeechDelay) >> 32 >> 5;

            timeToLive = (delay >> 31) + delay;
        }

        timeToLive += Time.Ticks;

        return timeToLive;
    }
}