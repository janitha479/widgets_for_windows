using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WidgetTools;

/// <summary>
/// The app shell: a single tray icon whose menu lists every widget with a
/// checkbox, remembers the choices, and owns app shutdown. Add a new widget by
/// constructing it in <see cref="BuildWidgets"/> — nothing else needs to change.
/// </summary>
public sealed class WidgetHost
{
    private readonly WidgetConfig _config;
    private readonly List<IWidget> _widgets;
    private readonly NotifyIcon _tray;

    public WidgetHost()
    {
        _config = WidgetConfig.Load();
        _widgets = BuildWidgets();

        var menu = new ContextMenuStrip();
        foreach (var w in _widgets)
        {
            var item = new ToolStripMenuItem(w.Name) { CheckOnClick = true };
            item.Checked = _config.IsEnabled(w.Id);          // set before subscribing so init doesn't fire Toggle
            item.CheckedChanged += (_, _) => Toggle(w, item.Checked);

            // If the widget closes itself, reflect that in the menu (and config).
            w.Closed += (_, _) => { if (item.Checked) item.Checked = false; };

            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => Shutdown();
        menu.Items.Add(exit);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Widgets",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Show whatever was enabled last time.
        foreach (var w in _widgets)
            if (_config.IsEnabled(w.Id))
                w.Show();
    }

    /// <summary>Register the available widgets here.</summary>
    private static List<IWidget> BuildWidgets() => new()
    {
        new MusicWidget(),
        new SystemMonitorWidget(),
        new BatteryWidget(),
    };

    private void Toggle(IWidget w, bool on)
    {
        if (on) w.Show(); else w.Hide();
        _config.Set(w.Id, on);
        _config.Save();
    }

    private void Shutdown()
    {
        foreach (var w in _widgets) w.Hide();
        _tray.Visible = false;
        _tray.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
