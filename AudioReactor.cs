using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace WidgetTools;

/// <summary>
/// Captures the system's audio output (WASAPI loopback) and turns it into a set
/// of frequency-band levels (0..1) for the ring visualizer. Capture runs on
/// NAudio's own thread; the UI just reads <see cref="Levels"/> each frame.
/// </summary>
public sealed class AudioReactor : IDisposable
{
    public int Bands { get; }
    public float[] Levels { get; }   // 0..1 per band, read by the UI

    private const int FftLength = 2048;          // power of two
    private readonly int _fftExp = (int)Math.Log2(FftLength);
    private readonly float[] _window = new float[FftLength];
    private readonly float[] _ring = new float[FftLength];
    private int _pos;
    private float _peak = 1e-4f;
    private readonly float _decay;

    private WasapiLoopbackCapture? _capture;

    public AudioReactor(int bands = 48, float decay = 0.82f)
    {
        Bands = bands;
        Levels = new float[bands];
        _decay = decay;
        for (int i = 0; i < FftLength; i++)      // Hann window
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftLength - 1))));
    }

    public void Start()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();   // default render device
            _capture.DataAvailable += OnData;
            _capture.StartRecording();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[audio] disabled: {ex.Message}");
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var fmt = _capture!.WaveFormat;              // loopback is 32-bit IEEE float
        int ch = fmt.Channels;
        int step = 4 * ch;                           // bytes per frame
        for (int off = 0; off + step <= e.BytesRecorded; off += step)
        {
            float sum = 0f;
            for (int c = 0; c < ch; c++)
                sum += BitConverter.ToSingle(e.Buffer, off + c * 4);
            _ring[_pos++] = sum / ch;
            if (_pos >= FftLength) { Analyze(); _pos = 0; }
        }
    }

    private void Analyze()
    {
        var buf = new Complex[FftLength];
        for (int i = 0; i < FftLength; i++)
        {
            buf[i].X = _ring[i] * _window[i];
            buf[i].Y = 0f;
        }
        FastFourierTransform.FFT(true, _fftExp, buf);

        int half = FftLength / 2;
        float sr = _capture!.WaveFormat.SampleRate;
        var vals = new float[Bands];

        // log-spaced bands from 40 Hz to 16 kHz
        for (int b = 0; b < Bands; b++)
        {
            double f0 = 40 * Math.Pow(16000.0 / 40, (double)b / Bands);
            double f1 = 40 * Math.Pow(16000.0 / 40, (double)(b + 1) / Bands);
            int i0 = Math.Clamp((int)(f0 / sr * FftLength), 0, half - 1);
            int i1 = Math.Clamp((int)(f1 / sr * FftLength), i0 + 1, half);

            float mag = 0f;
            for (int i = i0; i < i1; i++)
                mag += (float)Math.Sqrt(buf[i].X * buf[i].X + buf[i].Y * buf[i].Y);
            vals[b] = (float)Math.Log(1 + mag / (i1 - i0));
        }

        float max = 0f;
        foreach (var v in vals) max = Math.Max(max, v);
        _peak = Math.Max(_peak * 0.995f, max);       // slow-decaying auto-gain

        for (int b = 0; b < Bands; b++)
        {
            float norm = _peak > 1e-4f ? Math.Clamp(vals[b] / _peak, 0f, 1f) : 0f;
            Levels[b] = Math.Max(norm, Levels[b] * _decay);  // fast attack, slow fall
        }
    }

    public void Dispose()
    {
        try { _capture?.StopRecording(); } catch { }
        _capture?.Dispose();
    }
}
