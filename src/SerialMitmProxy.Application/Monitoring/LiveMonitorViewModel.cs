using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Channels;
using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Application.Monitoring;

public enum MonitorTrafficStage
{
    Inbound = 0,
    Forwarded = 1,
}

public sealed class FrameDisplayItem
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required MonitorTrafficStage Stage { get; init; }

    public required Direction Direction { get; init; }

    public required int Length { get; init; }

    public required byte[] Payload { get; init; }

    public required string Hex { get; init; }

    public required string Ascii { get; init; }

    public required bool IsModified { get; init; }
}

public sealed class LiveMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly List<StatSample> _statsSamples = new();
    private readonly Channel<FrameDisplayItem> _queue = Channel.CreateUnbounded<FrameDisplayItem>();
    private readonly SynchronizationContext? _syncContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private string _searchText = string.Empty;
    private bool _showAtoB = true;
    private bool _showBtoA = true;
    private int _aToBBytesPerSec;
    private int _bToABytesPerSec;
    private int _framesPerSec;
    private int _uiThrottleMs = 50;
    private Func<IReadOnlyList<Rule>> _monitorRulesProvider = static () => Array.Empty<Rule>();

    public LiveMonitorViewModel()
    {
        _syncContext = SynchronizationContext.Current;
        _pump = _syncContext is null
            ? Task.Run(() => PumpAsync(_cts.Token), _cts.Token)
            : Task.CompletedTask;
    }

    public ObservableCollection<FrameDisplayItem> Frames { get; } = new();

    public ObservableCollection<FrameDisplayItem> VisibleFrames { get; } = new();

    public ObservableCollection<FrameDisplayItem> InboundVisibleFrames { get; } = new();

    public ObservableCollection<FrameDisplayItem> ForwardedVisibleFrames { get; } = new();

    public int MaxFrames { get; set; } = 2000;

    public int UiThrottleMs
    {
        get => _uiThrottleMs;
        set => _uiThrottleMs = Math.Clamp(value, 10, 1000);
    }

    public int AToBBytesPerSec
    {
        get => _aToBBytesPerSec;
        private set => SetProperty(ref _aToBBytesPerSec, value);
    }

    public int BToABytesPerSec
    {
        get => _bToABytesPerSec;
        private set => SetProperty(ref _bToABytesPerSec, value);
    }

    public int FramesPerSec
    {
        get => _framesPerSec;
        private set => SetProperty(ref _framesPerSec, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            RefreshVisibleFrames();
        }
    }

    public bool ShowAtoB
    {
        get => _showAtoB;
        set
        {
            if (!SetProperty(ref _showAtoB, value))
            {
                return;
            }

            RefreshVisibleFrames();
        }
    }

    public bool ShowBtoA
    {
        get => _showBtoA;
        set
        {
            if (!SetProperty(ref _showBtoA, value))
            {
                return;
            }

            RefreshVisibleFrames();
        }
    }

    public void Post(Frame frame)
    {
        PostForwarded(frame);
    }

    public void PostInbound(Direction direction, byte[] payload, DateTimeOffset timestampUtc)
    {
        Post(MonitorTrafficStage.Inbound, direction, payload, timestampUtc);
    }

    public void PostForwarded(Frame frame)
    {
        Post(MonitorTrafficStage.Forwarded, frame.Direction, frame.Payload, frame.TimestampUtc, frame.IsModified);
    }

    public void Clear()
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(
                _ =>
                {
                    Frames.Clear();
                    VisibleFrames.Clear();
                    InboundVisibleFrames.Clear();
                    ForwardedVisibleFrames.Clear();
                    _statsSamples.Clear();
                    AToBBytesPerSec = 0;
                    BToABytesPerSec = 0;
                    FramesPerSec = 0;
                },
                null);
            return;
        }

        Frames.Clear();
        VisibleFrames.Clear();
        InboundVisibleFrames.Clear();
        ForwardedVisibleFrames.Clear();
        _statsSamples.Clear();
        AToBBytesPerSec = 0;
        BToABytesPerSec = 0;
        FramesPerSec = 0;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
        try
        {
            _pump.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }

    public void SetMonitorRulesProvider(Func<IReadOnlyList<Rule>> monitorRulesProvider)
    {
        _monitorRulesProvider = monitorRulesProvider ?? throw new ArgumentNullException(nameof(monitorRulesProvider));
        RefreshFilters();
    }

    public IReadOnlyList<Frame> CreateReplayFramesSnapshot()
    {
        return Frames
            .Select(item => Frame.Create(item.Direction, item.Payload, item.TimestampUtc, isSynthetic: true, isModified: item.IsModified))
            .ToArray();
    }

    public void RefreshFilters()
    {
        RefreshVisibleFrames();
    }

    private void Post(
        MonitorTrafficStage stage,
        Direction direction,
        byte[] payload,
        DateTimeOffset timestampUtc,
        bool isModified = false)
    {
        var item = new FrameDisplayItem
        {
            TimestampUtc = timestampUtc,
            Stage = stage,
            Direction = direction,
            Length = payload.Length,
            Payload = payload.ToArray(),
            Hex = BitConverter.ToString(payload).Replace('-', ' '),
            Ascii = ToAscii(payload),
            IsModified = isModified,
        };

        if (_syncContext is not null)
        {
            _syncContext.Post(_ => ApplyItems(new[] { item }), null);
            return;
        }

        _queue.Writer.TryWrite(item);
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        var batch = new List<FrameDisplayItem>();
        var lastStatsRefreshUtc = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            while (_queue.Reader.TryRead(out var item))
            {
                batch.Add(item);
                if (batch.Count >= 256)
                {
                    FlushBatch(batch);
                }
            }

            if (batch.Count > 0)
            {
                FlushBatch(batch);
            }
            else if (DateTime.UtcNow - lastStatsRefreshUtc >= TimeSpan.FromMilliseconds(200))
            {
                RefreshStatsOnly();
                lastStatsRefreshUtc = DateTime.UtcNow;
            }

            await Task.Delay(UiThrottleMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private void FlushBatch(List<FrameDisplayItem> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        void Apply()
        {
            ApplyItems(batch);
        }

        if (_syncContext is not null)
        {
            _syncContext.Post(_ => Apply(), null);
        }
        else
        {
            Apply();
        }

        batch.Clear();
    }

    private void RefreshStatsOnly()
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => RecalculateStats(DateTime.UtcNow), null);
            return;
        }

        RecalculateStats(DateTime.UtcNow);
    }

    private void RecordStats(IEnumerable<FrameDisplayItem> batch)
    {
        var nowUtc = DateTime.UtcNow;
        foreach (var item in batch)
        {
            _statsSamples.Add(new StatSample(nowUtc, item.Stage, item.Direction, item.Length));
        }
    }

    private void ApplyItems(IReadOnlyList<FrameDisplayItem> items)
    {
        foreach (var item in items)
        {
            Frames.Add(item);
        }

        while (Frames.Count > MaxFrames)
        {
            Frames.RemoveAt(0);
        }

        RefreshVisibleFramesInternal();
        RecordStats(items);
        RecalculateStats(DateTime.UtcNow);
    }

    private void RecalculateStats(DateTime nowUtc)
    {
        var cutoff = nowUtc - TimeSpan.FromSeconds(1);
        _statsSamples.RemoveAll(sample => sample.TimestampUtc < cutoff);

        AToBBytesPerSec = _statsSamples
            .Where(sample => sample.Direction == Direction.AtoB)
            .Sum(static sample => sample.Length);

        BToABytesPerSec = _statsSamples
            .Where(sample => sample.Direction == Direction.BtoA)
            .Sum(static sample => sample.Length);

        FramesPerSec = _statsSamples.Count;
    }

    private void RefreshVisibleFrames()
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => RefreshVisibleFramesInternal(), null);
            return;
        }

        RefreshVisibleFramesInternal();
    }

    private void RefreshVisibleFramesInternal()
    {
        var activeMonitorRules = _monitorRulesProvider()
            .Where(rule => rule.Enabled && rule.Scope == RuleScope.Monitor)
            .ToArray();

        var filtered = Frames.Where(item => MatchesFilter(item, activeMonitorRules)).ToArray();

        VisibleFrames.Clear();
        InboundVisibleFrames.Clear();
        ForwardedVisibleFrames.Clear();

        foreach (var item in filtered)
        {
            VisibleFrames.Add(item);
            if (item.Stage == MonitorTrafficStage.Inbound)
            {
                InboundVisibleFrames.Add(item);
            }
            else
            {
                ForwardedVisibleFrames.Add(item);
            }
        }
    }

    private bool MatchesFilter(FrameDisplayItem item, IReadOnlyList<Rule> activeMonitorRules)
    {
        if (item.Direction == Direction.AtoB && !ShowAtoB)
        {
            return false;
        }

        if (item.Direction == Direction.BtoA && !ShowBtoA)
        {
            return false;
        }

        var keyword = SearchText.Trim();
        if (keyword.Length == 0)
        {
            return MatchesMonitorRules(item, activeMonitorRules);
        }

        var matchesSearch = item.Hex.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || item.Ascii.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || item.Stage.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            || item.Direction.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);

        return matchesSearch && MatchesMonitorRules(item, activeMonitorRules);
    }

    private static bool MatchesMonitorRules(FrameDisplayItem item, IReadOnlyList<Rule> activeMonitorRules)
    {
        if (activeMonitorRules.Count == 0)
        {
            return true;
        }

        return activeMonitorRules.Any(rule => rule.Matchers.All(matcher => matcher.IsMatch(item.Direction, item.Payload)));
    }

    private static string ToAscii(byte[] payload)
    {
        var chars = payload.Select(static b => b is >= 32 and <= 126 ? (char)b : '.').ToArray();
        return new string(chars);
    }

    private readonly record struct StatSample(DateTime TimestampUtc, MonitorTrafficStage Stage, Direction Direction, int Length);
}
