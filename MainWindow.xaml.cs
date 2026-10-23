using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Storage.Streams;

namespace WidgetTools;

public partial class MainWindow : Window
{
    // ring geometry (within the 104x104 RingCanvas)
    private const int Bands = 48;
    private const double Cx = 52, Cy = 52, RIn = 34, BarMax = 14;

    private readonly MediaController _media = new();
    private readonly AudioReactor _reactor = new(Bands);
    private readonly Line[] _bars = new Line[Bands];
    private readonly SolidColorBrush[] _barBrushes = new SolidColorBrush[Bands];

    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private bool _expanded;
    private string? _lastTitle;
    private double _position, _duration, _posAt;
    private bool _isPlaying;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // The host creates/destroys this window as the widget is toggled, so
        // release everything the window started — otherwise timers and the
        // render hook keep running on the dispatcher after it's gone.
        CompositionTarget.Rendering -= OnRender;
        _pollTimer.Stop();
        _progressTimer.Stop();
        _collapseTimer.Stop();
        _reactor.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // park top-right
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 40;

        BuildRing();
        WireInteractions();

        await _media.InitAsync();
        _reactor.Start();

        _pollTimer.Tick += async (_, _) => await PollAsync();
        _progressTimer.Tick += (_, _) => UpdateBar();
        _pollTimer.Start();
        _progressTimer.Start();
        CompositionTarget.Rendering += OnRender;

        await PollAsync();
    }

    // ---- ring ----
    private void BuildRing()
    {
        for (int i = 0; i < Bands; i++)
        {
            var brush = new SolidColorBrush(Color.FromRgb(29, 185, 84));
            var line = new Line
            {
                Stroke = brush,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
            };
            _barBrushes[i] = brush;
            _bars[i] = line;
            RingCanvas.Children.Add(line);
        }
    }

    private void OnRender(object? sender, EventArgs e)
    {
        if (DiskGroup.Opacity <= 0.01) return;   // hidden while expanded
        var levels = _reactor.Levels;
        for (int i = 0; i < Bands; i++)
        {
            double lvl = i < levels.Length ? levels[i] : 0.0;
            double ang = 2 * Math.PI * i / Bands - Math.PI / 2;
            double ca = Math.Cos(ang), sa = Math.Sin(ang);
            double r1 = RIn + 2 + lvl * BarMax;
            var ln = _bars[i];
            ln.X1 = Cx + ca * RIn; ln.Y1 = Cy + sa * RIn;
            ln.X2 = Cx + ca * r1; ln.Y2 = Cy + sa * r1;
            _barBrushes[i].Color = BarColor(lvl);
        }
    }

    private static Color BarColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte r = (byte)(29 + (255 - 29) * t);
        byte g = (byte)(185 + (255 - 185) * t);
        byte b = (byte)(84 + (255 - 84) * t);
        return Color.FromRgb(r, g, b);
    }

    // ---- interactions ----
    private void WireInteractions()
    {
        DiskGroup.MouseEnter += (_, _) => Expand();
        Card.MouseEnter += (_, _) => _collapseTimer.Stop();
        Card.MouseLeave += (_, _) => _collapseTimer.Start();
        _collapseTimer.Tick += (_, _) => { _collapseTimer.Stop(); Collapse(); };

        // drag surfaces: the card (events bubble up from its children) and the
        // disk. Interactive controls below mark their mouse-down handled so a
        // click on them doesn't start a window drag.
        Card.MouseLeftButtonDown += Drag;
        DiskGroup.MouseLeftButtonDown += Drag;
        DiskGroup.MouseRightButtonUp += (_, _) => Close();

        // transport
        Hoverable(PlayBtn); Hoverable(PrevBtn); Hoverable(NextBtn);
        NoDrag(PlayBtn, PrevBtn, NextBtn, CloseBtn, BarHit);
        PlayBtn.MouseLeftButtonUp += async (_, _) => await _media.PlayPauseAsync();
        PrevBtn.MouseLeftButtonUp += async (_, _) => await _media.PreviousAsync();
        NextBtn.MouseLeftButtonUp += async (_, _) => await _media.NextAsync();
        CloseBtn.MouseLeftButtonUp += (_, _) => Close();

        BarHit.MouseLeftButtonUp += OnSeek;
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    // Stop a press on these controls from reaching the card's drag handler,
    // while still letting their own click (MouseLeftButtonUp) fire.
    private static void NoDrag(params UIElement[] elements)
    {
        foreach (var el in elements)
            el.MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    private static readonly SolidColorBrush Accent = new(Color.FromRgb(29, 185, 84));

    private void Hoverable(TextBlock t)
    {
        var normal = t.Foreground;
        t.MouseEnter += (_, _) => t.Foreground = Accent;
        t.MouseLeave += (_, _) => t.Foreground = normal;
    }

    private async void OnSeek(object sender, MouseButtonEventArgs e)
    {
        if (_duration <= 0) return;
        double frac = Math.Clamp(e.GetPosition(BarHit).X / BarHit.ActualWidth, 0, 1);
        _position = frac * _duration;
        _posAt = _clock.Elapsed.TotalSeconds;
        UpdateBar();
        await _media.SeekAsync(_position);
    }

    // ---- expand / collapse ----
    private void Expand()
    {
        if (_expanded) return;
        _expanded = true;
        _collapseTimer.Stop();
        Card.IsHitTestVisible = true;
        DiskGroup.IsHitTestVisible = false;
        Animate(CardScale, ScaleTransform.ScaleXProperty, 1, 180);
        Animate(CardScale, ScaleTransform.ScaleYProperty, 1, 180);
        Animate(Card, OpacityProperty, 1, 180);
        Animate(DiskGroup, OpacityProperty, 0, 140);
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        DiskGroup.IsHitTestVisible = true;
        Animate(CardScale, ScaleTransform.ScaleXProperty, 0.25, 160);
        Animate(CardScale, ScaleTransform.ScaleYProperty, 0.25, 160);
        Animate(Card, OpacityProperty, 0, 160, () => Card.IsHitTestVisible = false);
        Animate(DiskGroup, OpacityProperty, 1, 200);
    }

    private static void Animate(IAnimatable target, DependencyProperty prop, double to, int ms, Action? done = null)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (done != null) anim.Completed += (_, _) => done();
        target.BeginAnimation(prop, anim);
    }

    // ---- media polling ----
    private async Task PollAsync()
    {
        var np = await _media.GetNowPlayingAsync();
        if (np is null || np.Title is null)
        {
            TitleText.Text = "Nothing playing";
            ArtistText.Text = "";
            _duration = 0;
            _isPlaying = false;
            PlayBtn.Text = "⏵︎";
            UpdateBar();
            return;
        }

        if (np.Title != _lastTitle)
        {
            _lastTitle = np.Title;
            await LoadArtAsync(np.Thumbnail);
        }

        TitleText.Text = np.Title;
        ArtistText.Text = np.Artist ?? "";
        _isPlaying = np.IsPlaying;
        _duration = np.Duration;
        _position = np.Position;
        _posAt = _clock.Elapsed.TotalSeconds;
        // Show the pause glyph while playing, the play glyph while paused — never both.
        // The U+FE0E variation selector forces plain text style (no emoji keycap box).
        PlayBtn.Text = _isPlaying ? "⏸︎" : "⏵︎";
        UpdateBar();
    }

    private async Task LoadArtAsync(IRandomAccessStreamReference? thumbRef)
    {
        var bmp = await ToBitmapAsync(thumbRef);
        DiskBrush.ImageSource = bmp;
        Thumb.Source = bmp;
        BgImage.Source = bmp;
    }

    private static async Task<BitmapImage?> ToBitmapAsync(IRandomAccessStreamReference? thumbRef)
    {
        if (thumbRef is null) return null;
        try
        {
            using var ras = await thumbRef.OpenReadAsync();
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ras.AsStreamForRead();
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ---- progress bar ----
    private double Elapsed()
    {
        double e = _position;
        if (_isPlaying && _duration > 0) e += _clock.Elapsed.TotalSeconds - _posAt;
        return _duration > 0 ? Math.Min(e, _duration) : e;
    }

    private void UpdateBar()
    {
        double trackWidth = ((FrameworkElement)BarFill.Parent).ActualWidth;
        if (_duration > 0 && trackWidth > 0)
        {
            double frac = Elapsed() / _duration;
            BarFill.Width = frac * trackWidth;
            TimeCur.Text = Fmt(Elapsed());
            TimeTot.Text = Fmt(_duration);
        }
        else
        {
            BarFill.Width = 0;
            TimeCur.Text = "";
            TimeTot.Text = "";
        }
    }

    private static string Fmt(double seconds)
    {
        int s = (int)Math.Max(0, seconds);
        return $"{s / 60}:{s % 60:00}";
    }
}
