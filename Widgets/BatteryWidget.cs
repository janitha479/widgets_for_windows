using System;

namespace WidgetTools;

/// <summary>Host-managed battery overlay (see <see cref="MusicWidget"/> for the pattern).</summary>
public sealed class BatteryWidget : IWidget
{
    private BatteryWindow? _win;

    public string Id => "battery";
    public string Name => "Battery";
    public bool IsRunning => _win is not null;

    public event EventHandler? Closed;

    public void Show()
    {
        if (_win is not null) return;
        _win = new BatteryWindow();
        _win.Closed += OnWindowClosed;
        _win.Show();
    }

    public void Hide()
    {
        if (_win is null) return;
        var w = _win;
        _win = null;
        w.Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_win is null) return;
        _win = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
