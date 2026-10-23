using System;

namespace WidgetTools;

/// <summary>
/// Host-managed CPU / RAM overlay. Same lifecycle pattern as <see cref="MusicWidget"/>:
/// a fresh window per enable so its sampling timer only runs while it's on screen.
/// </summary>
public sealed class SystemMonitorWidget : IWidget
{
    private SystemMonitorWindow? _win;

    public string Id => "sysmon";
    public string Name => "System monitor";
    public bool IsRunning => _win is not null;

    public event EventHandler? Closed;

    public void Show()
    {
        if (_win is not null) return;
        _win = new SystemMonitorWindow();
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
        if (_win is null) return;   // host-driven hide already handled it
        _win = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
