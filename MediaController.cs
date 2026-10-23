using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WidgetTools;

/// <summary>
/// Thin wrapper around the Windows System Media Transport Controls (SMTC) —
/// the same session your keyboard's media keys drive. No Spotify login / API.
/// </summary>
public sealed class MediaController
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    private GlobalSystemMediaTransportControlsSession? Session(string prefer = "spotify")
    {
        if (_manager is null) return null;
        foreach (var s in _manager.GetSessions())
        {
            var id = (s.SourceAppUserModelId ?? string.Empty).ToLowerInvariant();
            if (id.Contains(prefer)) return s;
        }
        return _manager.GetCurrentSession();
    }

    public Task PlayPauseAsync() => Safe(s => s.TryTogglePlayPauseAsync());
    public Task NextAsync()      => Safe(s => s.TrySkipNextAsync());
    public Task PreviousAsync()  => Safe(s => s.TrySkipPreviousAsync());

    public Task SeekAsync(double seconds) =>
        Safe(s => s.TryChangePlaybackPositionAsync((long)(seconds * 10_000_000)));

    private async Task Safe(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> op)
    {
        var s = Session();
        if (s is null) return;
        try { await op(s); } catch { /* transient; ignore */ }
    }

    public async Task<NowPlaying?> GetNowPlayingAsync()
    {
        var s = Session();
        if (s is null) return null;

        var np = new NowPlaying();
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            np.Title = p.Title;
            np.Artist = p.Artist;
            np.Thumbnail = p.Thumbnail;
        }
        catch { /* ignore */ }

        try
        {
            np.IsPlaying = s.GetPlaybackInfo().PlaybackStatus
                == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch { /* ignore */ }

        try
        {
            var t = s.GetTimelineProperties();
            np.Position = (t.Position - t.StartTime).TotalSeconds;
            np.Duration = (t.EndTime - t.StartTime).TotalSeconds;
        }
        catch { /* ignore */ }

        return np;
    }
}

public sealed class NowPlaying
{
    public string? Title;
    public string? Artist;
    public bool IsPlaying;
    public double Position;
    public double Duration;
    public IRandomAccessStreamReference? Thumbnail;
}
