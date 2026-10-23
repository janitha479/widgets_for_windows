using System.Windows;

namespace WidgetTools;

public partial class App : Application
{
    private WidgetHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // There's no main window — the tray icon keeps the app alive, so don't
        // quit when a widget's window closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _host = new WidgetHost();
    }
}
