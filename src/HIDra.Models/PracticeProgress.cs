using System.Collections.Generic;

namespace HIDra.Models;

/// <summary>
/// A student's practice on the Practice tab, kept day by day as simple counts, so staff
/// can look back over weeks in a review. Saved beside the student's settings.
/// </summary>
public class PracticeProgress
{
    /// <summary>
    /// How small the circles have got, 0 being the biggest. The next session starts here.
    /// </summary>
    public int CircleLevel { get; set; }

    /// <summary>One entry per day with any practice, oldest first</summary>
    public List<PracticeDay> Days { get; set; } = new();
}

/// <summary>
/// One day's practice
/// </summary>
public class PracticeDay
{
    /// <summary>The day, as yyyy-MM-dd</summary>
    public string Date { get; set; } = "";

    public int CircleHits { get; set; }

    /// <summary>Clicks in the circle box that missed the circle</summary>
    public int CircleMisses { get; set; }

    /// <summary>The smallest circle reached that day, as a level (0 is the biggest)</summary>
    public int SmallestCircle { get; set; }

    public int WordsTyped { get; set; }

    /// <summary>Letters typed that did not match the word</summary>
    public int WrongLetters { get; set; }

    /// <summary>Times the keyboard was opened and then closed again</summary>
    public int KeyboardRounds { get; set; }
}
