using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WidgetTools;

/// <summary>
/// Battery overlay: a charge ring with center percent + state, plus voltage /
/// power / current / ETA readouts. Collapses to a ring badge (like the music
/// disk) and expands to the full card on hover. Data from <see cref="BatteryReader"/>.
/// </summary>
public partial class BatteryWindow : Window
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x4C, 0xD4, 0xB0));
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xFF, 0xB4, 0x54));
    private static readonly SolidColorBrush Red   = new(Color.FromRgb(0xFF, 0x6B, 0x6B));
    private static readonly SolidColorBrush Text  = new(Color.FromRgb(0xF2, 0xF3, 0xF5));

    private readonly BatteryReader _reader = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _expanded;

    public BatteryWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => { _timer.Stop(); _collapseTimer.Stop(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 300;

        WireInteractions();

        _timer.Tick += (_, _) => Sample();
        _timer.Start();
        Sample();
    }

    // ---- expand / collapse ----
    private void WireInteractions()
    {
        Badge.MouseEnter += (_, _) => Expand();
        Card.MouseEnter += (_, _) => _collapseTimer.Stop();
        Card.MouseLeave += (_, _) => _collapseTimer.Start();
        _collapseTimer.Tick += (_, _) => { _collapseTimer.Stop(); Collapse(); };

        Card.MouseLeftButtonDown += Drag;
        Badge.MouseLeftButtonDown += Drag;

        CloseBtn.MouseLeftButtonDown += (_, ev) => ev.Handled = true;
        CloseBtn.MouseLeftButtonUp += (_, _) => Close();
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Expand()
    {
        if (_expanded) return;
        _expanded = true;
        _collapseTimer.Stop();
        Card.IsHitTestVisible = true;
        Badge.IsHitTestVisible = false;
        Ui.Animate(CardScale, ScaleTransform.ScaleXProperty, 1, 180);
        Ui.Animate(CardScale, ScaleTransform.ScaleYProperty, 1, 180);
        Ui.Animate(Card, OpacityProperty, 1, 180);
        Ui.Animate(Badge, OpacityProperty, 0, 140);
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        Badge.IsHitTestVisible = true;
        Ui.Animate(CardScale, ScaleTransform.ScaleXProperty, 0.3, 160);
        Ui.Animate(CardScale, ScaleTransform.ScaleYProperty, 0.3, 160);
        Ui.Animate(Card, OpacityProperty, 0, 160, () => Card.IsHitTestVisible = false);
        Ui.Animate(Badge, OpacityProperty, 1, 200);
    }

    // ---- data ----
    private void Sample()
    {
        var b = _reader.Read();
        if (b is null)
        {
            PctText.Text = "--";
            StateText.Text = "no battery";
            BadgeText.Text = "--";
            Ui.SetArc(BatArc, 52, 52, 42, 0);
            Ui.SetArc(BadgeArc, 36, 36, 26, 0);
            VoltText.Text = PowerText.Text = CurrentText.Text = EtaText.Text = "";
            return;
        }

        var color = b.Charging ? Green : (b.Percent is <= 20 ? Red : Amber);
        double pct = b.Percent ?? 0;

        BatArc.Stroke = BadgeArc.Stroke = color;
        Ui.SetArc(BatArc, 52, 52, 42, pct);
        Ui.SetArc(BadgeArc, 36, 36, 26, pct);

        string pctStr = b.Percent is int p ? $"{p}%" : "--%";
        PctText.Text = pctStr;
        PctText.Foreground = b.Charging ? color : Text;
        BadgeText.Text = b.Percent is int bp ? $"{bp}%" : "--%";
        StateText.Text = (b.Charging ? "⚡ " : "") + b.State;

        string sign = b.Charging ? "+" : (b.Discharging ? "−" : "");
        VoltText.Text    = b.VoltageMv > 0 ? $"{"Voltage",-8}{b.VoltageMv:N0} mV" : $"{"Voltage",-8}-";
        PowerText.Text   = b.PowerMw   > 0 ? $"{"Power",-8}{sign}{b.PowerMw:N0} mW" : $"{"Power",-8}0 mW";
        CurrentText.Text = b.CurrentMa > 0 ? $"{"Current",-8}{sign}{b.CurrentMa:N0} mA" : $"{"Current",-8}-";
        EtaText.Text     = FormatEta(b);
    }

    private static string FormatEta(BatteryInfo b)
    {
        string? eta = b.EtaMin is int m and > 0
            ? (m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m} min")
            : null;

        if (eta is not null && b.Charging) return $"{"Full in",-8}~{eta}";
        if (eta is not null && b.Discharging) return $"{"Left",-8}~{eta}";
        if (b.PowerMw == 0 && b.Percent is not null) return $"{"Status",-8}plugged in";
        return "";
    }
}
