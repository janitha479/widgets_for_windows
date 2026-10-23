using System;

namespace WidgetTools;

/// <summary>
/// Wraps the music overlay window as a host-managed widget. Enabling creates a
/// fresh <see cref="MainWindow"/> (so timers / audio capture only run while it's
/// visible); disabling closes it.
/// </summary>
public sealed class MusicWidget : IWidget
{
    private MainWindow? _win;

    public string Id => "music";
    public string Name => "Music";
    public bool IsRunning => _win is not null;

    public event EventHandler? Closed;

    public void Show()
    {
        if (_win is not null) return;
        _win = new MainWindow();
        _win.Closed += OnWindowClosed;
        _win.Show();
    }

    public void Hide()
    {
        if (_win is null) return;
        var w = _win;
        _win = null;            // cleared first so OnWindowClosed treats this as a host-driven hide
        w.Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Only a user-driven close (the overlay's own X) leaves _win set here;
        // Hide() nulls it beforehand, so this fires the public event only when
        // the widget shut *itself* down.
        if (_win is null) return;
        _win = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
