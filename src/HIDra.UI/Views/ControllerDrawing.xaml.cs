using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HIDra.Models;

namespace HIDra.UI.Views;

/// <summary>
/// The controller drawing, with a label per control. The main window fills in the
/// labels; this lights the controls up as they are used and, on the Controller tab,
/// lets a label (or the control itself) be clicked to choose its job.
/// </summary>
public partial class ControllerDrawing : UserControl
{
    private readonly List<(ControllerControls Controls, Shape Shape, Brush Stroke, double Thickness)> _liveShapes = new();
    private readonly List<(ControllerControls Controls, Border Card)> _liveCards = new();

    private bool _editable;
    private string? _selected;

    /// <summary>
    /// A label that can be given a job was clicked, with its Tag: a button name from
    /// <see cref="ButtonJobCatalogue"/>, or LeftTrigger, DPad or StickPress
    /// </summary>
    public event EventHandler<string>? PartChosen;

    public ControllerDrawing()
    {
        InitializeComponent();

        void Part(ControllerControls controls, Shape shape, Border card)
        {
            _liveShapes.Add((controls, shape, shape.Stroke, shape.StrokeThickness));
            _liveCards.Add((controls, card));
        }

        Part(ControllerControls.LeftTrigger, CtlLT, CardLT);
        Part(ControllerControls.RightTrigger, CtlRT, CardRT);
        Part(ControllerControls.LeftBumper, CtlLB, CardLB);
        Part(ControllerControls.RightBumper, CtlRB, CardRB);
        Part(ControllerControls.LeftStick, CtlLeftStick, CardLeftStick);
        Part(ControllerControls.RightStick, CtlRightStick, CardRightStick);
        Part(ControllerControls.DPad, CtlDPad, CardDPad);
        Part(ControllerControls.A, CtlA, CardA);
        Part(ControllerControls.B, CtlB, CardB);
        Part(ControllerControls.X, CtlX, CardX);
        Part(ControllerControls.Y, CtlY, CardY);
        Part(ControllerControls.Back, CtlBack, CardBack);
        Part(ControllerControls.Start, CtlStart, CardStart);

        // Pressing a stick in lights the stick as well as its label
        _liveShapes.Add((ControllerControls.LeftStickPress, CtlLeftStick, CtlLeftStick.Stroke, CtlLeftStick.StrokeThickness));
        _liveShapes.Add((ControllerControls.RightStickPress, CtlRightStick, CtlRightStick.Stroke, CtlRightStick.StrokeThickness));
        _liveCards.Add((ControllerControls.LeftStickPress | ControllerControls.RightStickPress, CardStickPress));
    }

    /// <summary>
    /// Light each control, and on the Guide its label, while it is in use
    /// </summary>
    public void ShowActive(ControllerControls active)
    {
        var accent = (Brush)FindResource("AccentBrush");
        var cardBorder = (Brush)FindResource("CardBorderBrush");

        // A shape can be lit by more than one control - a stick by moving it or by
        // pressing it in - so work out each one's state before painting
        var litShapes = new HashSet<Shape>();
        foreach (var part in _liveShapes)
        {
            if ((active & part.Controls) != 0)
            {
                litShapes.Add(part.Shape);
            }
        }

        foreach (var part in _liveShapes)
        {
            bool lit = litShapes.Contains(part.Shape);
            part.Shape.Stroke = lit ? accent : part.Stroke;
            part.Shape.StrokeThickness = lit ? 4 : part.Thickness;
        }

        // While editing, a label's border shows where the pointer is instead
        if (_editable)
        {
            return;
        }

        foreach (var (controls, card) in _liveCards)
        {
            card.BorderBrush = (active & controls) != 0 ? accent : cardBorder;
        }

        // Holding Back and Start together brings the HIDra window back, so it lights
        // the label that says so
        var chord = ControllerControls.Back | ControllerControls.Start;
        CardRecovery.BorderBrush = (active & chord) == chord ? accent : cardBorder;
    }

    /// <summary>
    /// Make the labels that can be given a job clickable, with an orange border under
    /// the pointer like every other button on the main screen. Clicking the control on
    /// the drawing does the same as clicking its label.
    /// </summary>
    public void EnableEditing()
    {
        _editable = true;

        var shapes = new Dictionary<Border, Shape[]>
        {
            [CardLT] = new Shape[] { CtlLT },
            [CardLB] = new Shape[] { CtlLB },
            [CardRB] = new Shape[] { CtlRB },
            [CardDPad] = new Shape[] { CtlDPad },
            [CardA] = new Shape[] { CtlA },
            [CardB] = new Shape[] { CtlB },
            [CardX] = new Shape[] { CtlX },
            [CardY] = new Shape[] { CtlY },
            [CardBack] = new Shape[] { CtlBack },
            [CardStart] = new Shape[] { CtlStart },
            [CardStickPress] = new Shape[] { CtlLeftStick, CtlRightStick },
        };

        var accent = (Brush)FindResource("AccentBrush");
        var cardBorder = (Brush)FindResource("CardBorderBrush");

        foreach (var (card, controls) in shapes)
        {
            string tag = (string)card.Tag;
            card.Cursor = Cursors.Hand;
            card.MouseEnter += (_, _) => card.BorderBrush = accent;
            card.MouseLeave += (_, _) => card.BorderBrush = cardBorder;
            card.MouseLeftButtonUp += (_, _) => PartChosen?.Invoke(this, tag);

            foreach (var shape in controls)
            {
                shape.Cursor = Cursors.Hand;
                shape.MouseLeftButtonUp += (_, _) => PartChosen?.Invoke(this, tag);
            }
        }

        // The rest cannot be changed, and are dimmed to say so
        foreach (var fixedCard in new[] { CardRT, CardLeftStick, CardRightStick, CardRecovery })
        {
            fixedCard.Opacity = 0.55;
        }
    }

    /// <summary>
    /// Fill the label whose job is being chosen orange, as any chosen thing is
    /// </summary>
    public void Select(string? tag)
    {
        _selected = tag;
        var on = (Brush)FindResource("AccentOnBrush");

        foreach (var (_, card) in _liveCards)
        {
            card.ClearValue(Border.BackgroundProperty);
            if (card.Tag is string cardTag && cardTag == _selected)
            {
                card.Background = on;
            }
        }
    }
}
