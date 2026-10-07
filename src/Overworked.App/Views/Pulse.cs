using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Overworked.App.Views;

/// <summary>
/// A panel whose contents slowly fade in and out: a lane's line and its worktree dots. All pulses share one clock,
/// so the whole lane breathes together.
/// </summary>
public sealed class Pulse : Panel
{
    const double PeriodSeconds = 4.8, Dimmest = 0.3;

    static readonly List<Pulse> Live = [];
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    // ponytail: ~12 fps instead of a 60 fps animation. A slow fade looks the same, and 60 fps redraws
    // cost a third of a CPU core for an app that sits open all day.
    static readonly DispatcherTimer Timer = new(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) => Tick());

    protected override Type StyleKeyOverride => typeof(Panel);

    static double Current => Dimmest + (1 - Dimmest) * (1 + Math.Cos(Clock.Elapsed.TotalSeconds * 2 * Math.PI / PeriodSeconds)) / 2;

    static void Tick()
    {
        var opacity = Current;
        foreach (var pulse in Live) pulse.Opacity = opacity;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Live.Add(this);
        Opacity = Current;
        Timer.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Live.Remove(this);
        if (Live.Count == 0) Timer.Stop();
    }
}
