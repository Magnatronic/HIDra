using System;
using WindowsInput;
using WindowsInput.Native;

namespace HIDra.Core.Simulation;

/// <summary>
/// Simulates mouse movements and clicks
/// </summary>
public class MouseSimulator : IDisposable
{
    private readonly InputSimulator _simulator;
    private bool _isLeftButtonHeld;
    private bool _isRightButtonHeld;
    private bool _isMiddleButtonHeld;

    public MouseSimulator()
    {
        _simulator = new InputSimulator();
    }

    /// <summary>
    /// Move mouse cursor by relative amount
    /// </summary>
    public void MoveMouse(float deltaX, float deltaY)
    {
        if (Math.Abs(deltaX) < 0.1f && Math.Abs(deltaY) < 0.1f)
        {
            return; // Too small to matter
        }

        _simulator.Mouse.MoveMouseBy((int)Math.Round(deltaX), (int)Math.Round(deltaY));
    }

    /// <summary>
    /// Perform left mouse click
    /// </summary>
    public void LeftClick()
    {
        _simulator.Mouse.LeftButtonClick();
    }

    /// <summary>
    /// Perform right mouse click
    /// </summary>
    public void RightClick()
    {
        _simulator.Mouse.RightButtonClick();
    }

    /// <summary>
    /// Perform middle mouse click
    /// </summary>
    public void MiddleClick()
    {
        _simulator.Mouse.MiddleButtonClick();
    }

    /// <summary>
    /// Perform double click
    /// </summary>
    public void DoubleClick()
    {
        _simulator.Mouse.LeftButtonDoubleClick();
    }

    /// <summary>
    /// Press and hold left mouse button
    /// </summary>
    public void LeftButtonDown()
    {
        if (!_isLeftButtonHeld)
        {
            _simulator.Mouse.LeftButtonDown();
            _isLeftButtonHeld = true;
        }
    }

    /// <summary>
    /// Release left mouse button
    /// </summary>
    public void LeftButtonUp()
    {
        if (_isLeftButtonHeld)
        {
            _simulator.Mouse.LeftButtonUp();
            _isLeftButtonHeld = false;
        }
    }

    /// <summary>
    /// Press and hold right mouse button
    /// </summary>
    public void RightButtonDown()
    {
        if (!_isRightButtonHeld)
        {
            _simulator.Mouse.RightButtonDown();
            _isRightButtonHeld = true;
        }
    }

    /// <summary>
    /// Release right mouse button
    /// </summary>
    public void RightButtonUp()
    {
        if (_isRightButtonHeld)
        {
            _simulator.Mouse.RightButtonUp();
            _isRightButtonHeld = false;
        }
    }

    /// <summary>
    /// Scroll vertically
    /// </summary>
    public void ScrollVertical(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        _simulator.Mouse.VerticalScroll(amount);
    }

    /// <summary>
    /// Scroll horizontally
    /// </summary>
    public void ScrollHorizontal(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        _simulator.Mouse.HorizontalScroll(amount);
    }

    /// <summary>
    /// Release all held buttons
    /// </summary>
    public void ReleaseAll()
    {
        if (_isLeftButtonHeld)
        {
            _simulator.Mouse.LeftButtonUp();
            _isLeftButtonHeld = false;
        }

        if (_isRightButtonHeld)
        {
            _simulator.Mouse.RightButtonUp();
            _isRightButtonHeld = false;
        }

        if (_isMiddleButtonHeld)
        {
            _simulator.Mouse.MiddleButtonUp();
            _isMiddleButtonHeld = false;
        }
    }

    public void Dispose()
    {
        ReleaseAll();
    }
}
