using System;

namespace WidgetTools;

/// <summary>
/// A single overlay item (music, battery, system monitor, …) that the host can
/// turn on and off independently. Each widget owns its own window(s); the host
/// only decides which ones are running and remembers that choice.
/// </summary>
public interface IWidget
{
    /// <summary>Stable key used in the config file (e.g. "music"). Never shown to the user.</summary>
    string Id { get; }

    /// <summary>Label shown in the tray menu (e.g. "Music").</summary>
    string Name { get; }

    /// <summary>True while the widget's overlay is on screen.</summary>
    bool IsRunning { get; }

    /// <summary>Create and show the overlay. No-op if already running.</summary>
    void Show();

    /// <summary>Tear the overlay down and release its resources. No-op if not running.</summary>
    void Hide();

    /// <summary>Raised when the widget stops itself (e.g. the user clicked its own close button).</summary>
    event EventHandler? Closed;
}
