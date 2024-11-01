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
using ClassicUO.Game.Managers;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Keyboard = ClassicUO.Input.Keyboard;

namespace ClassicUO.Game.UI.Controls;

#nullable enable

internal abstract class Control
{
    internal static int _StepsDone = 1;
    internal static int _StepChanger = 1;

    private bool _acceptKeyboardInput;
    private bool _acceptMouseInput;
    private bool _handlesKeyboardFocus;
    private Rectangle _bounds;
    private Point _offset;
    private Control? _parent;
    protected List<Control> _children;

    public virtual ClickPriority Priority { get; set; } = ClickPriority.Default;
    public Serial ServerSerial { get; set; }
    public Serial LocalSerial { get; set; }
    public bool IsFromServer { get; set; }
    public int Page { get; set; }
    public bool IsDisposed { get; private set; }
    public bool IsVisible { get; set; } = true;
    public bool IsEnabled { get; set; }
    public bool CanMove { get; set; }
    public bool CanCloseWithRightClick { get; set; } = true;
    public bool CanCloseWithEsc { get; set; }
    public bool IsEditable { get; set; }
    public bool IsFocused { get; set; }
    public float Alpha { get; set; } = 1.0f;
    public ReadOnlySpan<Control> Children => CollectionsMarshal.AsSpan(_children);
    public object? Tag { get; set; }
    public object? Tooltip { get; private set; }
    public ContextMenuControl? ContextMenu { get; set; }
    public UILayer LayerOrder { get; set; } = UILayer.Default;
    public bool IsModal { get; set; }
    public bool ModalClickOutsideAreaClosesThisControl { get; set; }
    public int ActivePage { get; set { field = value; OnPageChanged(); } }
    public bool WantUpdateSize { get; set; } = true;
    public bool AllowedToDraw { get; set; }
    public int TooltipMaxLength { get; private set; }
    public bool HasTooltip => Tooltip is not null;
    public bool HasKeyboardFocus => UIManager.KeyboardFocusControl == this;
    public bool MouseIsOver => UIManager.MouseOverControl == this;
    public ref Rectangle Bounds => ref _bounds;
    public Point Offset => _offset;
    public ref int X => ref _bounds.X;
    public ref int Y => ref _bounds.Y;
    public ref int Width => ref _bounds.Width;
    public ref int Height => ref _bounds.Height;
    public int ParentX => Parent is not null ? Parent.X + Parent.ParentX : 0;
    public int ParentY => Parent is not null ? Parent.Y + Parent.ParentY : 0;
    public int ScreenCoordinateX => ParentX + X;
    public int ScreenCoordinateY => ParentY + Y;
    public Point Location
    {
        get => _bounds.Location;
        set
        {
            X = value.X;
            Y = value.Y;
            _bounds.Location = value;
        }
    }

    public virtual bool AcceptKeyboardInput
    {
        get => IsEnabled && !IsDisposed && IsVisible && _acceptKeyboardInput;
        set => _acceptKeyboardInput = value;
    }

    public virtual bool AcceptMouseInput
    {
        get => IsEnabled && !IsDisposed && _acceptMouseInput && IsVisible;
        set => _acceptMouseInput = value;
    }

    public Control? Parent
    {
        get => _parent;
        internal set
        {
            _parent?._children.Remove(this);
            value?._children.Add(this);

            _parent = value;
        }
    }

    public Control? RootParent
    {
        get
        {
            if (Parent is null)
                return null;

            Control p = Parent;

            while (p.Parent is not null)
            {
                p = p.Parent;
            }

            return p;
        }
    }

    public virtual bool HandlesKeyboardFocus
    {
        get
        {
            if (!IsEnabled || IsDisposed || !IsVisible)
                return false;

            if (_handlesKeyboardFocus)
                return true;

            foreach (Control c in _children)
            {
                if (c.HandlesKeyboardFocus)
                    return true;
            }

            return false;
        }
        set => _handlesKeyboardFocus = value;
    }

    internal event EventHandler<MouseEventArgs>? MouseDown, MouseUp, MouseOver, MouseEnter, MouseExit, DragBegin, DragEnd;
    internal event EventHandler<MouseWheelEventArgs>? MouseWheel;
    internal event EventHandler<MouseDoubleClickEventArgs>? MouseDoubleClick;
    internal event EventHandler? FocusEnter, FocusLost;
    internal event EventHandler<KeyboardEventArgs>? KeyDown, KeyUp;

    protected Control(Control? parent = null)
    {
        Parent = parent;
        _children = [];
        AllowedToDraw = true;
        AcceptMouseInput = true;
        Page = 0;
        IsDisposed = false;
        IsEnabled = true;
    }

    public void UpdateOffset(int x, int y)
    {
        if (_offset.X == x && _offset.Y == y)
            return;

        _offset.X = x;
        _offset.Y = y;

        foreach (Control c in _children)
        {
            c.UpdateOffset(x, y);
        }
    }

    public virtual bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        if (IsDisposed)
            return false;

        foreach (Control c in _children)
        {
            if ((c.Page == 0 || c.Page == ActivePage) && c.IsVisible)
                c.Draw(batcher, c.X + x, c.Y + y);
        }

        DrawDebug(batcher, x, y);

        return true;
    }

    public virtual void Update()
    {
        if (IsDisposed)
            return;

        if (_children.Count == 0)
            return;

        int w = 0, h = 0;

        for (int i = 0; i < _children.Count; i++)
        {
            Control c = _children[i];

            if (c.IsDisposed)
            {
                OnChildRemoved();
                _children.RemoveAt(i--);

                continue;
            }

            c.Update();

            if (WantUpdateSize)
            {
                if ((c.Page == 0 || c.Page == ActivePage) && c.IsVisible)
                {
                    if (w < c.Bounds.Right)
                        w = c.Bounds.Right;

                    if (h < c.Bounds.Bottom)
                        h = c.Bounds.Bottom;
                }
            }
        }

        if (WantUpdateSize && IsVisible)
        {
            if (w != Width)
                Width = w;

            if (h != Height)
                Height = h;

            WantUpdateSize = false;
        }
    }

    public virtual void OnPageChanged()
    {
        //Update size as pages may vary in size.
        if (ServerSerial != Serial.Zero)
            WantUpdateSize = true;
    }

    private void DrawDebug(UltimaBatcher2D batcher, int x, int y)
    {
        if (!IsVisible || !CUOEnviroment.Debug)
            return;

        Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);
        batcher.DrawRectangle(SolidColorTextureCache.GetTexture(Color.Green), x, y, Width, Height, hueVector);
    }

    public void BringOnTop()
    {
        UIManager.MakeTopMostGump(this);
    }

    public void SetTooltip(string? text, int maxWidth = 0)
    {
        ClearTooltip();

        if (string.IsNullOrEmpty(text))
            return;

        Tooltip = text;
        TooltipMaxLength = maxWidth;
    }

    public void SetTooltip(Serial entity)
    {
        ClearTooltip();
        Tooltip = entity;
    }

    public void ClearTooltip()
    {
        Tooltip = null;
    }

    public void SetKeyboardFocus()
    {
        if (AcceptKeyboardInput && !HasKeyboardFocus)
            UIManager.KeyboardFocusControl = this;
    }

    public void HitTest(int x, int y, ref Control res)
    {
        if (!IsVisible || !IsEnabled || IsDisposed)
            return;

        int parentX = ParentX;
        int parentY = ParentY;

        if (!Bounds.Contains(x - parentX - _offset.X, y - parentY - _offset.Y))
            return;

        if (!Contains(x - X - parentX, y - Y - parentY))
            return;

        if (AcceptMouseInput)
        {
            if (res is null || res.Priority >= Priority)
            {
                res = this;
                OnHitTestSuccess(x, y, ref res);
            }
        }

        for (int i = 0; i < _children.Count; i++)
        {
            Control c = _children[i];

            if (c.Page == 0 || c.Page == ActivePage)
                c.HitTest(x, y, ref res);
        }
    }

    public void HitTest(Point position, ref Control res)
    {
        HitTest(position.X, position.Y, ref res);
    }

    public virtual void OnHitTestSuccess(int x, int y, ref Control res)
    {
    }

    public Control? GetFirstControlAcceptKeyboardInput()
    {
        if (_acceptKeyboardInput)
            return this;

        if (_children is not { Count: > 0 })
            return null;

        foreach (Control c in _children)
        {
            Control? a = c.GetFirstControlAcceptKeyboardInput();
            if (a is not null)
                return a;
        }

        return null;
    }

    public virtual void Add(Control c, int page = 0)
    {
        c.Page = page;
        c.Parent = this;
        OnChildAdded();
    }

    public void Insert(int index, Control c)
    {
        c.Page = 0;
        c._parent?._children.Remove(c);
        c._parent = this;

        _children.Insert(index, c);
        OnChildAdded();
    }

    public virtual void Remove(Control c)
    {
        if (c is null)
            return;

        c.Parent = null;
        _children.Remove(c);
        OnChildRemoved();
    }

    public IEnumerable<T> GetDescendantsOfType<T>()
    {
        return _children.SelectMany(c => c._children).OfType<T>();
    }

    public virtual void Clear()
    {
        foreach (Control c in _children)
        {
            c.Dispose();
        }
    }

    public void ClearChildren()
    {
        _children.Clear();
    }

    public T[] GetControls<T>() where T : Control
    {
        return _children.OfType<T>().Where(s => !s.IsDisposed).ToArray();
    }

    public IEnumerable<T> FindControls<T>() where T : Control
    {
        return _children.OfType<T>().Where(s => !s.IsDisposed);
    }

    public void InvokeMouseDown(Point position, MouseButtonType button)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnMouseDown(x, y, button);
        MouseDown.Raise(new MouseEventArgs(x, y, button, ButtonState.Pressed), this);
    }

    public void InvokeMouseUp(Point position, MouseButtonType button)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnMouseUp(x, y, button);
        MouseUp.Raise(new MouseEventArgs(x, y, button), this);
    }

    public void InvokeMouseCloseGumpWithRClick()
    {
        if (CanCloseWithRightClick)
            CloseWithRightClick();
    }

    public void InvokeMouseOver(Point position)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnMouseOver(x, y);
        MouseOver.Raise(new MouseEventArgs(x, y), this);
    }

    public void InvokeMouseEnter(Point position)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnMouseEnter(x, y);
        MouseEnter.Raise(new MouseEventArgs(x, y), this);
    }

    public void InvokeMouseExit(Point position)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnMouseExit(x, y);
        MouseExit.Raise(new MouseEventArgs(x, y), this);
    }

    public bool InvokeMouseDoubleClick(Point position, MouseButtonType button)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        bool result = OnMouseDoubleClick(x, y, button);

        MouseDoubleClickEventArgs arg = new(x, y, button);
        MouseDoubleClick.Raise(arg, this);
        result |= arg.Result;

        return result;
    }

    public void InvokeTextInput(string c)
    {
        OnTextInput(c);
    }

    public void InvokeKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
    {
        OnKeyDown(key, mod);
        KeyboardEventArgs arg = new(key, mod, KeyboardEventType.Down);
        KeyDown?.Raise(arg);
    }

    public void InvokeKeyUp(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
    {
        OnKeyUp(key, mod);
        KeyboardEventArgs arg = new(key, mod, KeyboardEventType.Up);
        KeyUp?.Raise(arg);
    }

    public void InvokeMouseWheel(MouseEventType delta)
    {
        OnMouseWheel(delta);
        MouseWheel.Raise(new MouseWheelEventArgs(delta), this);
    }

    public void InvokeDragBegin(Point position)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnDragBegin(x, y);
        DragBegin.Raise(new MouseEventArgs(x, y, MouseButtonType.Left, ButtonState.Pressed), this);
    }

    public void InvokeDragEnd(Point position)
    {
        int x = position.X - X - ParentX;
        int y = position.Y - Y - ParentY;
        OnDragEnd(x, y);
        DragEnd.Raise(new MouseEventArgs(x, y, MouseButtonType.Left), this);
    }

    public void InvokeMove(int x, int y)
    {
        x = x - X - ParentX;
        y = y - Y - ParentY;
        OnMove(x, y);
    }

    protected virtual void OnMouseDown(int x, int y, MouseButtonType button)
    {
        Parent?.OnMouseDown(X + x, Y + y, button);
    }

    protected virtual void OnMouseUp(int x, int y, MouseButtonType button)
    {
        Parent?.OnMouseUp(X + x, Y + y, button);

        if (button == MouseButtonType.Right && !IsDisposed && !CanCloseWithRightClick && !Keyboard.Alt && !Keyboard.Shift && !Keyboard.Ctrl)
            ContextMenu?.Show();
    }

    protected virtual void OnMouseWheel(MouseEventType delta)
    {
        Parent?.OnMouseWheel(delta);
    }

    protected virtual void OnMouseOver(int x, int y)
    {
        Parent?.OnMouseOver(X + x, Y + y);
    }

    protected virtual void OnMouseEnter(int x, int y)
    { }

    protected virtual void OnMouseExit(int x, int y)
    { }

    protected virtual bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
    {
        return Parent?.OnMouseDoubleClick(X + x, Y + y, button) ?? false;
    }

    protected virtual void OnDragBegin(int x, int y)
    { }

    protected virtual void OnDragEnd(int x, int y)
    { }

    protected virtual void OnTextInput(string c)
    { }

    protected virtual void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
    {
        Parent?.OnKeyDown(key, mod);
    }

    protected virtual void OnKeyUp(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
    {
        Parent?.OnKeyUp(key, mod);
    }

    public virtual bool Contains(int x, int y)
    {
        return !IsDisposed;
    }

    protected virtual void OnMove(int x, int y)
    { }

    internal virtual void OnFocusEnter()
    {
        if (IsFocused)
            return;

        IsFocused = true;
        FocusEnter.Raise(this);
    }

    internal virtual void OnFocusLost()
    {
        if (!IsFocused)
            return;

        IsFocused = false;
        FocusLost.Raise(this);
    }

    protected virtual void OnChildAdded()
    { }

    protected virtual void OnChildRemoved()
    { }

    protected virtual void CloseWithRightClick()
    {
        if (!CanCloseWithRightClick)
            return;

        Control? parent = Parent;

        while (parent is not null)
        {
            if (!parent.CanCloseWithRightClick)
                return;

            parent = parent.Parent;
        }

        if (Parent is null)
            Dispose();
        else
            Parent.CloseWithRightClick();
    }

    public void KeyboardTabToNextFocus(Control c)
    {
        int startIndex = _children.IndexOf(c);

        for (int i = startIndex + 1; i < _children.Count; i++)
        {
            if (!_children[i].AcceptKeyboardInput)
                continue;

            _children[i].SetKeyboardFocus();
            return;
        }

        for (int i = 0; i < startIndex; i++)
        {
            if (!_children[i].AcceptKeyboardInput)
                continue;

            _children[i].SetKeyboardFocus();
            return;
        }
    }

    public virtual void OnButtonClick(int buttonID)
    {
        Parent?.OnButtonClick(buttonID);
    }

    public virtual void OnKeyboardReturn(int textID, string text)
    {
        Parent?.OnKeyboardReturn(textID, text);
    }

    public virtual void ChangePage(int pageIndex)
    {
        Parent?.ChangePage(pageIndex);
    }

    public virtual void Dispose()
    {
        if (IsDisposed)
            return;

        if (_children is not null)
        {
            foreach (Control c in _children)
            {
                c.Dispose();
            }

            _children.Clear();
        }

        IsDisposed = true;
    }
}