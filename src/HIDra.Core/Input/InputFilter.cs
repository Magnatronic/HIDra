using HIDra.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HIDra.Core.Input;

/// <summary>
/// Steadies the controller's input before anything else sees it, for a student whose
/// hands are not always steady. Everything downstream - the pointer, scrolling, the
/// keyboard highlight, every button - reads the filtered state, so one setting helps
/// all of them alike.
///
///   Smoothing   The sticks are averaged over a short time, so a wobble does not become
///               a wobbling pointer. The average lets go faster than it builds up:
///               releasing the stick to stop on a target must stop the pointer promptly,
///               or smoothing would cost the very precision it is there to give.
///
///   Repeats     A press that begins very soon after the same button was let go is
///               ignored until it is released - a tremor or a bounce, not a decision.
///               Measured from the release rather than the first press, so a long
///               deliberate press followed by a quick bounce is still caught.
///
/// Both are off by default, and cost nothing while off.
/// </summary>
public sealed class InputFilter
{
    private readonly InputSettings _settings;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastSeconds;

    private float _leftX, _leftY, _rightX, _rightY;
    private ControllerState? _previousRaw;

    private readonly Dictionary<string, double> _releasedAt = new();
    private readonly HashSet<string> _ignored = new();

    private static readonly (string Name, Func<ControllerState, bool> Get, Action<ControllerState, bool> Set)[] Buttons =
    {
        ("A", s => s.ButtonA, (s, v) => s.ButtonA = v),
        ("B", s => s.ButtonB, (s, v) => s.ButtonB = v),
        ("X", s => s.ButtonX, (s, v) => s.ButtonX = v),
        ("Y", s => s.ButtonY, (s, v) => s.ButtonY = v),
        ("LB", s => s.LeftBumper, (s, v) => s.LeftBumper = v),
        ("RB", s => s.RightBumper, (s, v) => s.RightBumper = v),
        ("Back", s => s.Back, (s, v) => s.Back = v),
        ("Start", s => s.Start, (s, v) => s.Start = v),
        ("LS", s => s.LeftStickClick, (s, v) => s.LeftStickClick = v),
        ("RS", s => s.RightStickClick, (s, v) => s.RightStickClick = v),
        ("Up", s => s.DpadUp, (s, v) => s.DpadUp = v),
        ("Down", s => s.DpadDown, (s, v) => s.DpadDown = v),
        ("Left", s => s.DpadLeft, (s, v) => s.DpadLeft = v),
        ("Right", s => s.DpadRight, (s, v) => s.DpadRight = v),
    };

    public InputFilter(InputSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ControllerState Apply(ControllerState raw)
    {
        double now = _clock.Elapsed.TotalSeconds;
        float deltaSeconds = (float)Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;

        var filtered = raw.Clone();
        SmoothSticks(filtered, deltaSeconds);
        IgnoreRepeats(raw, filtered, now);

        _previousRaw = raw.Clone();
        return filtered;
    }

    /// <summary>
    /// Forget the past, after a pause or a reconnection, so nothing stale carries over
    /// </summary>
    public void Reset()
    {
        _previousRaw = null;
        _leftX = _leftY = _rightX = _rightY = 0f;
        _releasedAt.Clear();
        _ignored.Clear();
    }

    private void SmoothSticks(ControllerState state, float deltaSeconds)
    {
        float seconds = _settings.StickSmoothingSeconds;

        if (seconds <= 0f)
        {
            (_leftX, _leftY, _rightX, _rightY) = (state.LeftStickX, state.LeftStickY, state.RightStickX, state.RightStickY);
            return;
        }

        (_leftX, _leftY) = Smooth(_leftX, _leftY, state.LeftStickX, state.LeftStickY, seconds, deltaSeconds);
        (_rightX, _rightY) = Smooth(_rightX, _rightY, state.RightStickX, state.RightStickY, seconds, deltaSeconds);

        (state.LeftStickX, state.LeftStickY, state.RightStickX, state.RightStickY) = (_leftX, _leftY, _rightX, _rightY);
    }

    private static (float X, float Y) Smooth(float x, float y, float targetX, float targetY, float seconds, float deltaSeconds)
    {
        // Letting go of the stick settles three times as fast as pushing it builds up
        bool easingOff = targetX * targetX + targetY * targetY < x * x + y * y;
        float timeConstant = easingOff ? seconds / 3f : seconds;
        float share = 1f - MathF.Exp(-deltaSeconds / timeConstant);

        return (x + (targetX - x) * share, y + (targetY - y) * share);
    }

    private void IgnoreRepeats(ControllerState raw, ControllerState filtered, double now)
    {
        float window = _settings.IgnoreRepeatSeconds;

        foreach (var (name, get, set) in Buttons)
        {
            bool down = get(raw);
            bool wasDown = _previousRaw != null && get(_previousRaw);

            if (down && !wasDown && window > 0f
                && _releasedAt.TryGetValue(name, out double releasedAt) && now - releasedAt < window)
            {
                _ignored.Add(name);
            }

            if (!down && wasDown)
            {
                _releasedAt[name] = now;
                _ignored.Remove(name);
            }

            if (_ignored.Contains(name))
            {
                set(filtered, false);
            }
        }
    }
}
