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
    /// <summary>
    /// Where we believe the cursor is, kept as a fraction of a pixel.
    ///
    /// Rounding each frame's movement to whole pixels and discarding the remainder made
    /// slow movement visibly step: a frame asking for 0.4 pixels moved nothing, the next
    /// asking for 0.6 moved a whole one. That is worst in precision mode, which is
    /// exactly where smoothness matters most. Carrying the fraction between frames
    /// means small movements accumulate instead of being thrown away.
    /// </summary>
    private double _positionX;
    private double _positionY;
    private bool _hasPosition;

    /// <summary>
    /// How far the real cursor may differ from where we put it before we assume
    /// something else moved it - a real mouse, or an application recentring it.
    /// Normalising to the 0-65535 absolute grid can land a pixel off, so this needs a
    /// little slack.
    /// </summary>
    private const int ResyncThresholdPixels = 3;

    /// <summary>
    /// Move the cursor by a number of pixels, which may be fractional.
    /// </summary>
    public void MoveMouse(float deltaX, float deltaY)
    {
        if (Math.Abs(deltaX) < 0.0001f && Math.Abs(deltaY) < 0.0001f)
        {
            // Genuinely stationary. Drop our tracked position so that if the user moves
            // the cursor by other means while the stick is centred, we pick it up from
            // wherever it actually ended rather than snapping it back.
            _hasPosition = false;
            return;
        }

        if (!NativeInput.GetCursorPos(out var actual))
        {
            return;
        }

        // Re-anchor whenever the cursor is not where we last put it.
        if (!_hasPosition ||
            Math.Abs(actual.X - (int)Math.Round(_positionX)) > ResyncThresholdPixels ||
            Math.Abs(actual.Y - (int)Math.Round(_positionY)) > ResyncThresholdPixels)
        {
            _positionX = actual.X;
            _positionY = actual.Y;
            _hasPosition = true;
        }

        _positionX += deltaX;
        _positionY += deltaY;

        var (left, top, width, height) = NativeInput.GetVirtualScreen();
        _positionX = Math.Clamp(_positionX, left, left + width - 1);
        _positionY = Math.Clamp(_positionY, top, top + height - 1);

        NativeInput.MoveMouseTo((int)Math.Round(_positionX), (int)Math.Round(_positionY));
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
