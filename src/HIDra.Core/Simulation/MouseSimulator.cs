using System;

namespace HIDra.Core.Simulation;

/// <summary>
/// Simulates mouse movements and clicks
/// </summary>
public class MouseSimulator : IDisposable
{
    private bool _isLeftButtonHeld;
    private bool _isRightButtonHeld;
    private bool _isMiddleButtonHeld;

    /// <summary>
    /// Move mouse cursor by relative amount
    /// </summary>
    public void MoveMouse(float deltaX, float deltaY)
    {
        if (Math.Abs(deltaX) < 0.1f && Math.Abs(deltaY) < 0.1f)
        {
            return; // Too small to matter
        }

        NativeInput.MoveMouseBy((int)Math.Round(deltaX), (int)Math.Round(deltaY));
    }

    /// <summary>
    /// Perform left mouse click
    /// </summary>
    public void LeftClick()
    {
        NativeInput.LeftButtonClick();
    }

    /// <summary>
    /// Perform right mouse click
    /// </summary>
    public void RightClick()
    {
        NativeInput.RightButtonClick();
    }

    /// <summary>
    /// Perform middle mouse click
    /// </summary>
    public void MiddleClick()
    {
        NativeInput.MiddleButtonClick();
    }

    /// <summary>
    /// Perform double click
    /// </summary>
    public void DoubleClick()
    {
        NativeInput.LeftButtonDoubleClick();
    }

    /// <summary>
    /// Press and hold left mouse button
    /// </summary>
    public void LeftButtonDown()
    {
        if (!_isLeftButtonHeld)
        {
            NativeInput.LeftButtonDown();
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
            NativeInput.LeftButtonUp();
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
            NativeInput.RightButtonDown();
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
            NativeInput.RightButtonUp();
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

        NativeInput.VerticalScroll(amount);
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

        NativeInput.HorizontalScroll(amount);
    }

    /// <summary>
    /// Release all held buttons
    /// </summary>
    public void ReleaseAll()
    {
        if (_isLeftButtonHeld)
        {
            NativeInput.LeftButtonUp();
            _isLeftButtonHeld = false;
        }

        if (_isRightButtonHeld)
        {
            NativeInput.RightButtonUp();
            _isRightButtonHeld = false;
        }

        if (_isMiddleButtonHeld)
        {
            NativeInput.MiddleButtonUp();
            _isMiddleButtonHeld = false;
        }
    }

    public void Dispose()
    {
        ReleaseAll();
    }
}
