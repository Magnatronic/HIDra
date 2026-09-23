using System;
using System.Collections.Generic;

namespace HIDra.Models;

/// <summary>
/// A student's practice tests, every run kept, so staff can look back over weeks in a
/// review. Each test is the same every time at a given size or level, which is what
/// makes one run comparable with the last. Saved beside the student's settings.
/// </summary>
public class PracticeProgress
{
    /// <summary>
    /// How small the pointer test's circles have got, 0 being the biggest. The next run
    /// starts here.
    /// </summary>
    public int CircleLevel { get; set; }

    /// <summary>The typing test's level: 0 words, 1 phrases, 2 sentences</summary>
    public int TypingLevel { get; set; }

    public List<PointerRun> PointerRuns { get; set; } = new();

    public List<TypingRun> TypingRuns { get; set; } = new();
}

/// <summary>
/// One run of the pointer test: a set of circles to click, some needing a scroll to reach
/// </summary>
public class PointerRun
{
    public DateTime When { get; set; }

    /// <summary>The circle size, as a level (0 is the biggest)</summary>
    public int Size { get; set; }

    public int Targets { get; set; }

    /// <summary>From the first circle appearing to the last one hit</summary>
    public double Seconds { get; set; }

    /// <summary>Clicks that missed the circle</summary>
    public int Misses { get; set; }

    /// <summary>Circles that needed a scroll to reach</summary>
    public int Scrolled { get; set; }

    /// <summary>Times a circle was scrolled into view and then out again, past it</summary>
    public int Overshoots { get; set; }

    /// <summary>
    /// Pointing speed in bits a second, from the circles that needed no scroll (Fitts's
    /// law, as in ISO 9241-9). It allows for distance and size, so it stays comparable
    /// when the circles get smaller. 0 if there were none to measure.
    /// </summary>
    public double Throughput { get; set; }
}

/// <summary>
/// One run of the typing test
/// </summary>
public class TypingRun
{
    public DateTime When { get; set; }

    /// <summary>0 words, 1 phrases, 2 sentences</summary>
    public int Level { get; set; }

    /// <summary>Letters (and spaces) in the items typed, not counting skipped ones</summary>
    public int Characters { get; set; }

    /// <summary>Time spent typing, from the first letter of each item to its last</summary>
    public double Seconds { get; set; }

    /// <summary>Letters typed that did not match</summary>
    public int WrongLetters { get; set; }

    /// <summary>Times something typed was taken away again</summary>
    public int Corrections { get; set; }

    /// <summary>Times the keyboard was moved out of the way with LT</summary>
    public int KeyboardMoves { get; set; }

    public int Skipped { get; set; }

    public double LettersPerMinute => Seconds > 0 ? Characters / Seconds * 60 : 0;
}
