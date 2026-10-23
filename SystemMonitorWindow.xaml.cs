using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WidgetTools;

/// <summary>
/// CPU / RAM overlay shown as two filled progress rings. Collapses to a single
/// CPU badge (like the music disk) and expands to both rings on hover. CPU load
/// comes from GetSystemTimes deltas, RAM from GlobalMemoryStatusEx.
/// </summary>
public partial class SystemMonitorWindow : Window
{
    private static readonly SolidColorBrush Calm = new(Color.FromRgb(29, 185, 84));
    private static readonly SolidColorBrush Warn = new(Color.FromRgb(255, 170, 40));
    private static readonly SolidColorBrush Hot  = new(Color.FromRgb(240, 70, 70));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _havePrev;
    private bool _expanded;

    public SystemMonitorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => { _timer.Stop(); _collapseTimer.Stop(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // park top-right, below the music disk
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 170;

        WireInteractions();

        _timer.Tick += (_, _) => Sample();
        _timer.Start();
        Sample();
    }

    // ---- expand / collapse (mirrors the music overlay) ----
    private void WireInteractions()
    {
        Badge.MouseEnter += (_, _) => Expand();
        Card.MouseEnter += (_, _) => _collapseTimer.Stop();
        Card.MouseLeave += (_, _) => _collapseTimer.Start();
        _collapseTimer.Tick += (_, _) => { _collapseTimer.Stop(); Collapse(); };

        Card.MouseLeftButtonDown += Drag;
        Badge.MouseLeftButtonDown += Drag;

        CloseBtn.MouseLeftButtonDown += (_, ev) => ev.Handled = true; // don't drag
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

    // ---- sampling ----
    private void Sample()
    {
        double cpu = ReadCpu();
        if (cpu >= 0)
        {
            SetRing(CpuArc, 48, 48, 34, CpuText, cpu);
            SetRing(BadgeArc, 36, 36, 26, BadgeText, cpu);
        }

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0;
            double totalGb = mem.ullTotalPhys / 1073741824.0;
            Card.ToolTip = $"RAM {usedGb:0.0} / {totalGb:0.0} GB";
            SetRing(RamArc, 48, 48, 34, RamText, mem.dwMemoryLoad);
        }
    }

    /// <summary>Returns CPU load 0..100, or -1 until the first delta is available.</summary>
    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return -1;
        ulong idle = ToU64(idleFt), kernel = ToU64(kernelFt), user = ToU64(userFt);

        double pct = -1;
        if (_havePrev)
        {
            ulong total = (kernel - _prevKernel) + (user - _prevUser); // kernel includes idle
            ulong busy = total - (idle - _prevIdle);
            pct = total > 0 ? 100.0 * busy / total : 0;
        }
        _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
        _havePrev = true;
        return pct;
    }

    private static void SetRing(Path arc, double cx, double cy, double r, TextBlock label, double pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        Ui.SetArc(arc, cx, cy, r, pct);
        arc.Stroke = pct >= 90 ? Hot : pct >= 70 ? Warn : Calm;
        label.Text = $"{pct:0}%";
    }

    private static ulong ToU64(System.Runtime.InteropServices.ComTypes.FILETIME ft) =>
        ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    // ---- Win32 ----
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
