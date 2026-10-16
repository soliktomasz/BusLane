namespace BusLane.Views.Controls;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

/// <summary>
/// Read-only message inspection alongside the message list.
/// </summary>
public partial class MessageInspectorView : UserControl
{
    // A short settle ties the newly selected row to the payload that replaces the previous one.
    private static readonly Animation SelectionMotion = new()
    {
        Duration = TimeSpan.FromMilliseconds(180),
        Easing = new SplineEasing(0.16, 1, 0.3, 1),
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0),
                Setters =
                {
                    new Setter(OpacityProperty, 0.35),
                    new Setter(TranslateTransform.YProperty, 4.0)
                }
            },
            new KeyFrame
            {
                Cue = new Cue(1),
                Setters =
                {
                    new Setter(OpacityProperty, 1.0),
                    new Setter(TranslateTransform.YProperty, 0.0)
                }
            }
        }
    };

    private CancellationTokenSource? _selectionMotion;

    public MessageInspectorView()
    {
        InitializeComponent();
        // The settle animates this transform's Y; RenderTransform itself has no animator.
        InspectedContent.RenderTransform = new TranslateTransform();
        SizeChanged += (_, _) => Classes.Set("compact", Bounds.Height < 460);
    }

    /// <summary>
    /// Plays the selection settle; a newer selection interrupts the running one.
    /// </summary>
    public void PlaySelectionMotion()
    {
        if (!IsEffectivelyVisible)
            return;

        _selectionMotion?.Cancel();
        _selectionMotion = new CancellationTokenSource();
        _ = SelectionMotion.RunAsync(InspectedContent, _selectionMotion.Token);
    }
}
