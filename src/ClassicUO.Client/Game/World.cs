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

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Map;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.IO.Audio;
using ClassicUO.Utility.Logging;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using MathHelper = ClassicUO.Utility.MathHelper;

namespace ClassicUO.Game;

#nullable enable

internal sealed class World
{
    private readonly EffectManager _effectManager;
    private readonly List<Serial> _toRemove = [];
    private uint _timeToDelete;
    public Point RangeSize;
    public HouseCustomizationManager? CustomHouseManager;
    public Serial LastObject;
    public Serial ObjectToRemove;
    public ActiveSpellIconsManager ActiveSpellIcons = new();

    public WorldMapEntityManager WMapManager { get; }
    public ObjectPropertiesListManager OPL { get; } = new();
    public CorpseManager CorpseManager { get; }
    public PartyManager Party { get; }
    public HouseManager HouseManager { get; }
    public MessageManager MessageManager { get; }
    public ContainerManager ContainerManager { get; }
    public IgnoreManager IgnoreManager { get; }
    public SkillsGroupManager SkillsGroupManager { get; }
    public ChatManager ChatManager { get; }
    public AuraManager AuraManager { get; }
    public UoAssist UoAssist { get; }
    public TargetManager TargetManager { get; }
    public DelayedObjectClickManager DelayedObjectClickManager { get; }
    public BoatMovingManager BoatMovingManager { get; }
    public NameOverHeadManager NameOverHeadManager { get; }
    public MacroManager Macros { get; }
    public CommandManager CommandManager { get; }
    public Weather Weather { get; }
    public InfoBarManager InfoBars { get; }
    public Dictionary<Serial, Item> Items { get; } = [];
    public Dictionary<Serial, Mobile> Mobiles { get; } = [];
    public JournalManager Journal { get; } = new();
    public WorldTextManager WorldTextManager { get; }
    public IsometricLight Light { get; } = new(0);
    public LockedFeatures ClientLockedFeatures { get; } = new LockedFeatures();
    public ClientFeatures ClientFeatures { get; } = new ClientFeatures();

    public PlayerMobile? Player { get; private set; }
    public Map.Map? Map { get; private set; }
    public byte ClientViewRange { get; set; } = Constants.MAX_VIEW_RANGE;
    public bool SkillsRequested { get; set; }
    public Season Season { get; private set; } = Season.Summer;
    public Season OldSeason { get; set; } = Season.Summer;
    public int OldMusicIndex { get; set; }
    public string ServerName { get; set; } = "_";
    public bool InGame => Player is not null && Map is not null;

    public int MapIndex
    {
        get => Map?.Index ?? -1;
        set
        {
            if (MapIndex != value)
            {
                InternalMapChangeClear(true);

                if (value < 0 && Map != null)
                {
                    Map.Destroy();
                    Map = null;

                    return;
                }

                if (Map != null)
                {
                    if (MapIndex >= 0)
                    {
                        Map.Destroy();
                    }

                    ushort x = Player.X;
                    ushort y = Player.Y;
                    sbyte z = Player.Z;

                    Map = null;

                    if (value >= MapLoader.MAPS_COUNT)
                    {
                        value = 0;
                    }

                    Client.Game.UO.FileManager.Maps.LoadMap(value, ClientFeatures.Flags.HasFlag(CharacterListFlags.CLF_UNLOCK_FELUCCA_AREAS));
                    Map = new Map.Map(this, value);

                    Player.SetInWorldTile(x, y, z);
                    Player.ClearSteps();
                }
                else
                {
                    Client.Game.UO.FileManager.Maps.LoadMap(value, ClientFeatures.Flags.HasFlag(CharacterListFlags.CLF_UNLOCK_FELUCCA_AREAS));
                    Map = new Map.Map(this, value);
                }

                // force cursor update when switching map
                if (Client.Game.UO.GameCursor != null)
                {
                    Client.Game.UO.GameCursor.Graphic = 0xFFFF;
                }

                UoAssist.SignalMapChanged(value);
            }
        }
    }

    public World()
    {
        WMapManager = new WorldMapEntityManager(this);
        CorpseManager = new CorpseManager(this);
        Party = new PartyManager(this);
        HouseManager = new HouseManager(this);
        WorldTextManager = new WorldTextManager(this);
        _effectManager = new EffectManager(this);
        MessageManager = new MessageManager(this);
        ContainerManager = new ContainerManager(this);
        IgnoreManager = new IgnoreManager(this);
        SkillsGroupManager = new SkillsGroupManager(this);
        ChatManager = new ChatManager(this);
        AuraManager = new AuraManager(this);
        UoAssist = new UoAssist(this);
        TargetManager = new TargetManager(this);
        DelayedObjectClickManager = new DelayedObjectClickManager(this);
        BoatMovingManager = new BoatMovingManager(this);
        NameOverHeadManager = new NameOverHeadManager(this);
        Macros = new MacroManager(this);
        CommandManager = new CommandManager(this);
        Weather = new Weather(this);
        InfoBars = new InfoBarManager(this);
    }

    public void CreatePlayer(Serial serial)
    {
        if (ProfileManager.CurrentProfile is null)
        {
            string lastChar = LastCharacterManager.GetLastCharacter(LoginScene.Account, ServerName);
            ProfileManager.Load(ServerName, LoginScene.Account, lastChar);
        }

        if (Player is not null)
            Clear();

        Player = new PlayerMobile(this, serial);
        Mobiles.Add(Player);

        Log.Trace($"Player [0x{serial:X8}] created");
    }

    public void ChangeSeason(Season season, int music)
    {
        Season = season;

        foreach (Chunk chunk in Map.GetUsedChunks())
        {
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    for (GameObject obj = chunk?.GetHeadObject(x, y); obj != null; obj = obj.TNext)
                    {
                        obj.UpdateGraphicBySeason();
                    }
                }
            }
        }

        //TODO(deccer): refactor this out into _audioPlayer.PlayMusic(...)
        UOMusic currentMusic = Client.Game.Audio.GetCurrentMusic();
        if (currentMusic == null || currentMusic.Index == Client.Game.Audio.LoginMusicIndex)
        {
            Client.Game.Audio.PlayMusic(music, false);
        }
    }

    public void Update()
    {
        if (Player is null)
            return;

        if (ObjectToRemove.IsEntity)
        {
            Item? rem = Items.Get(ObjectToRemove);
            ObjectToRemove = Serial.Zero;

            if (rem is not null)
            {
                Entity? container = Get(rem.Container);

                RemoveItem(rem.Serial, true);

                if (rem.Layer == Layer.OneHanded || rem.Layer == Layer.TwoHanded)
                    Player.UpdateAbilities();

                if (container is not null)
                {
                    if (container.Serial.IsMobile)
                    {
                        UIManager.GetGump<PaperDollGump>(container.Serial)?.RequestUpdateContents();
                    }
                    else if (container.Serial.IsItem)
                    {
                        UIManager.GetGump<ContainerGump>(container.Serial)?.RequestUpdateContents();

                        if (container.Graphic == 0x2006)
                            UIManager.GetGump<GridLootGump>(container.Serial)?.RequestUpdateContents();
                    }
                }
            }
        }

        bool do_delete = _timeToDelete < Time.Ticks;

        if (do_delete)
            _timeToDelete = Time.Ticks + 50;

        foreach (Mobile mob in Mobiles.Values)
        {
            mob.Update();

            if (do_delete && mob.Distance > ClientViewRange /*CheckToRemove(mob, ClientViewRange)*/)
                RemoveMobile(mob.Serial);

            if (mob.IsDestroyed)
            {
                _toRemove.Add(mob.Serial);
            }
            else
            {
                if (mob.NotorietyFlag == NotorietyFlag.Ally)
                {
                    WMapManager.AddOrUpdate
                    (
                        mob.Serial,
                        mob.X,
                        mob.Y,
                        MathHelper.PercetangeOf(mob.Hits, mob.HitsMax),
                        MapIndex,
                        true,
                        mob.Name
                    );
                }
                else if (Party.Leader != 0 && Party.Contains(mob.Serial))
                {
                    WMapManager.AddOrUpdate
                    (
                        mob.Serial,
                        mob.X,
                        mob.Y,
                        MathHelper.PercetangeOf(mob.Hits, mob.HitsMax),
                        MapIndex,
                        false,
                        mob.Name
                    );
                }
            }
        }

        if (_toRemove.Count != 0)
        {
            for (int i = 0; i < _toRemove.Count; i++)
            {
                Mobiles.Remove(_toRemove[i]);
            }

            _toRemove.Clear();
        }

        foreach (Item item in Items.Values)
        {
            item.Update();

            if (do_delete && item.OnGround && item.Distance > ClientViewRange /*CheckToRemove(item, ClientViewRange)*/)
            {
                if (item.IsMulti)
                {
                    if (HouseManager.TryToRemove(item.Serial, ClientViewRange))
                    {
                        RemoveItem(item.Serial);
                    }
                }
                else
                {
                    RemoveItem(item.Serial);
                }
            }

            if (item.IsDestroyed)
            {
                _toRemove.Add(item.Serial);
            }
        }

        if (_toRemove.Count != 0)
        {
            for (int i = 0; i < _toRemove.Count; i++)
            {
                Items.Remove(_toRemove[i]);
            }

            _toRemove.Clear();
        }

        _effectManager.Update();
        WorldTextManager.Update();
        WMapManager.RemoveUnupdatedWEntity();
    }

    public bool Contains(Serial serial)
    {
        if (serial.IsItem)
            return Items.Contains(serial);

        return serial.IsMobile && Mobiles.Contains(serial);
    }

    public Entity? Get(Serial serial)
    {
        Entity? ent;

        if (serial.IsMobile)
        {
            ent = Mobiles.Get(serial);
            ent ??= Items.Get(serial);
        }
        else
        {
            ent = Items.Get(serial);
            ent ??= Mobiles.Get(serial);
        }

        if (ent is { IsDestroyed: true })
            ent = null;

        return ent;
    }

    public Item GetOrCreateItem(Serial serial)
    {
        Item? item = Items.Get(serial);

        if (item != null && item.IsDestroyed)
        {
            Items.Remove(serial);
            item = null;
        }

        if (item == null /*|| item.IsDestroyed*/)
        {
            item = Item.Create(this, serial);
            Items.Add(item);
        }

        return item;
    }

    public Mobile GetOrCreateMobile(Serial serial)
    {
        Mobile mob = Mobiles.Get(serial);

        if (mob != null && mob.IsDestroyed)
        {
            Mobiles.Remove(serial);
            mob = null;
        }

        if (mob == null /*|| mob.IsDestroyed*/)
        {
            mob = Mobile.Create(this, serial);
            Mobiles.Add(mob);
        }

        return mob;
    }

    public void RemoveItemFromContainer(Serial serial)
    {
        Item it = Items.Get(serial);

        if (it != null)
        {
            RemoveItemFromContainer(it);
        }
    }

    public void RemoveItemFromContainer(Item obj)
    {
        Serial containerSerial = obj.Container;

        // if entity is running the "dying" animation we have to reset container too.
        // SerialHelper.IsValid(containerSerial) is not ideal in this case
        if (containerSerial != 0xFFFF_FFFF)
        {
            if (containerSerial.IsMobile)
            {
                UIManager.GetGump<PaperDollGump>(containerSerial)?.RequestUpdateContents();
            }
            else if (containerSerial.IsItem)
            {
                UIManager.GetGump<ContainerGump>(containerSerial)?.RequestUpdateContents();
            }

            Entity container = Get(containerSerial);

            if (container != null)
            {
                container.Remove(obj);
            }

            obj.Container = Serial.MinusOne;
        }

        obj.Next = null;
        obj.Previous = null;
        obj.RemoveFromTile();
    }

    public bool RemoveItem(Serial serial, bool forceRemove = false)
    {
        Item? item = Items.Get(serial);
        if (item is not { IsDestroyed: false })
            return false;

        LinkedObject? first = item.Items;
        RemoveItemFromContainer(item);

        while (first is not null)
        {
            LinkedObject? next = first.Next;

            RemoveItem(first as Item, forceRemove);

            first = next;
        }

        OPL.Remove(serial);
        item.Destroy();

        if (forceRemove)
            Items.Remove(serial);

        return true;
    }

    public bool RemoveMobile(Serial serial, bool forceRemove = false)
    {
        Mobile? mobile = Mobiles.Get(serial);
        if (mobile is not { IsDestroyed: false })
            return false;

        LinkedObject? first = mobile.Items;

        while (first is not null)
        {
            LinkedObject? next = first.Next;

            RemoveItem(first as Item, forceRemove);

            first = next;
        }

        OPL.Remove(serial);
        mobile.Destroy();

        if (forceRemove)
            Mobiles.Remove(serial);

        return true;
    }

    public void SpawnEffect
    (
        GraphicEffectType type,
        Serial source,
        Serial target,
        ushort graphic,
        ushort hue,
        ushort srcX,
        ushort srcY,
        sbyte srcZ,
        ushort targetX,
        ushort targetY,
        sbyte targetZ,
        byte speed,
        int duration,
        bool fixedDir,
        bool doesExplode,
        bool hasparticles,
        GraphicEffectBlendMode blendmode
    )
    {
        _effectManager.CreateEffect
        (
            type,
            source,
            target,
            graphic,
            hue,
            srcX,
            srcY,
            srcZ,
            targetX,
            targetY,
            targetZ,
            speed,
            duration,
            fixedDir,
            doesExplode,
            hasparticles,
            blendmode
        );
    }

    public Serial FindNearest(ScanTypeObject scanType)
    {
        int distance = int.MaxValue;
        Serial serial = Serial.Zero;

        if (scanType == ScanTypeObject.Objects)
        {
            foreach (Item item in Items.Values)
            {
                if (item.IsMulti || item.IsDestroyed || !item.OnGround)
                {
                    continue;
                }

                if (item.Distance < distance)
                {
                    distance = item.Distance;
                    serial = item.Serial;
                }
            }
        }
        else
        {
            foreach (Mobile mobile in Mobiles.Values)
            {
                if (mobile.IsDestroyed || mobile == Player)
                {
                    continue;
                }

                switch (scanType)
                {
                    case ScanTypeObject.Party:
                        if (!Party.Contains(mobile.Serial))
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Followers:
                        if (!(mobile.IsRenamable && mobile.NotorietyFlag != NotorietyFlag.Invulnerable && mobile.NotorietyFlag != NotorietyFlag.Enemy))
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Hostile:
                        if (mobile.NotorietyFlag == NotorietyFlag.Ally || mobile.NotorietyFlag == NotorietyFlag.Innocent || mobile.NotorietyFlag == NotorietyFlag.Invulnerable)
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Objects:
                        /* This was handled separately above */
                        continue;
                }

                if (mobile.Distance < distance)
                {
                    distance = mobile.Distance;
                    serial = mobile.Serial;
                }
            }
        }

        return serial;
    }

    public Serial FindNext(ScanTypeObject scanType, Serial lastSerial, bool reverse)
    {
        bool found = false;

        if (scanType == ScanTypeObject.Objects)
        {
            var items = reverse ? Items.Values.Reverse() : Items.Values;
            foreach (Item item in items)
            {
                if (item.IsMulti || item.IsDestroyed || !item.OnGround)
                {
                    continue;
                }

                if (lastSerial == 0)
                {
                    return item.Serial;
                }
                else if (item.Serial == lastSerial)
                {
                    found = true;
                }
                else if (found)
                {
                    return item.Serial;
                }
            }
        }
        else
        {
            IEnumerable<Mobile> mobiles = reverse ? Mobiles.Values.Reverse() : Mobiles.Values;
            foreach (Mobile mobile in mobiles)
            {
                if (mobile.IsDestroyed || mobile == Player)
                {
                    continue;
                }

                switch (scanType)
                {
                    case ScanTypeObject.Party:
                        if (!Party.Contains(mobile.Serial))
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Followers:
                        if (!(mobile.IsRenamable && mobile.NotorietyFlag != NotorietyFlag.Invulnerable && mobile.NotorietyFlag != NotorietyFlag.Enemy))
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Hostile:
                        if (mobile.NotorietyFlag == NotorietyFlag.Ally || mobile.NotorietyFlag == NotorietyFlag.Innocent || mobile.NotorietyFlag == NotorietyFlag.Invulnerable)
                        {
                            continue;
                        }
                        break;
                    case ScanTypeObject.Objects:
                        /* This was handled separately above */
                        continue;
                }

                if (lastSerial == 0)
                {
                    return mobile.Serial;
                }
                else if (mobile.Serial == lastSerial)
                {
                    found = true;
                }
                else if (found)
                {
                    return mobile.Serial;
                }
            }
        }

        if (lastSerial != 0)
        {
            /* If we get here, it means we didn't find anything but we started with a serial number. That means
             * if we restart the search from the beginning it may find something again. */
            return FindNext(scanType, Serial.Zero, reverse);
        }

        return Serial.Zero;
    }


    public void Clear()
    {
        foreach (Mobile mobile in Mobiles.Values)
        {
            RemoveMobile(mobile.Serial);
        }

        foreach (Item item in Items.Values)
        {
            RemoveItem(item.Serial);
        }

        if (Player is not null)
            UIManager.GetGump<BaseHealthBarGump>(Player.Serial)?.Dispose();
        else
            UIManager.GetGump<BaseHealthBarGump>()?.Dispose();

        ObjectToRemove = Serial.Zero;
        LastObject = Serial.Zero;
        Items.Clear();
        Mobiles.Clear();
        Player?.Destroy();
        Player = null;
        Map?.Destroy();
        Map = null;
        Light.Overall = Light.RealOverall = 0;
        Light.Personal = Light.RealPersonal = 0;
        ClientLockedFeatures.SetFlags(0);
        Party?.Clear();
        TargetManager.LastAttack = Serial.Zero;
        MessageManager.PromptData = default;
        _effectManager.Clear();
        _toRemove.Clear();
        CorpseManager.Clear();
        OPL.Clear();
        WMapManager.Clear();
        HouseManager?.Clear();

        Season = Season.Summer;
        OldSeason = Season.Summer;

        Journal.Clear();
        WorldTextManager.Clear();
        ActiveSpellIcons.Clear();

        SkillsRequested = false;
    }

    private void InternalMapChangeClear(bool noplayer)
    {
        if (!noplayer)
        {
            Map.Destroy();
            Map = null;
            Player.Destroy();
            Player = null;
        }

        foreach (Item item in Items.Values)
        {
            if (noplayer && Player != null && !Player.IsDestroyed)
            {
                if (item.RootContainer == Player)
                {
                    continue;
                }
            }

            if (item.OnGround && item.IsMulti)
            {
                HouseManager.Remove(item.Serial);
            }

            _toRemove.Add(item.Serial);
        }

        foreach (Serial serial in _toRemove)
        {
            RemoveItem(serial, true);
        }

        _toRemove.Clear();

        foreach (Mobile mob in Mobiles.Values)
        {
            if (noplayer && Player != null && !Player.IsDestroyed)
            {
                if (mob == Player)
                {
                    continue;
                }
            }

            _toRemove.Add(mob.Serial);
        }

        foreach (Serial serial in _toRemove)
        {
            RemoveMobile(serial, true);
        }

        _toRemove.Clear();
    }
}