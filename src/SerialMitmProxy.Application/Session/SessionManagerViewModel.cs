using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Application.Monitoring;
using SerialMitmProxy.Core.Proxy;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Application.Session;

public sealed class SessionManagerViewModel : ViewModelBase
{
    private enum SessionStatusKind
    {
        Idle = 0,
        Running = 1,
        Stopped = 2,
        StartFailed = 3,
        RunningRulesUpdated = 4,
        StopFailed = 5,
        SessionError = 6,
    }

    private readonly Func<ProxySession> _sessionFactory;
    private readonly LiveMonitorViewModel _liveMonitor;
    private Func<string, string> _translate;
    private ProxySession? _session;
    private bool _isRunning;
    private string _status = string.Empty;
    private SessionStatusKind _statusKind = SessionStatusKind.Idle;
    private string _statusDetail = string.Empty;
    private int _statusRulesCount;

    public SessionManagerViewModel(
        Func<ProxySession> sessionFactory,
        LiveMonitorViewModel liveMonitor,
        Func<string, string>? translate = null)
    {
        _sessionFactory = sessionFactory;
        _liveMonitor = liveMonitor;
        _translate = translate ?? (static key => key);
        SetStatus(SessionStatusKind.Idle);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return true;
        }

        ProxySession? candidateSession = null;
        try
        {
            candidateSession = _sessionFactory();
            candidateSession.RawBytesObserved += (_, args) => _liveMonitor.PostInbound(args.Direction, args.Payload, args.TimestampUtc);
            candidateSession.FrameForwarded += (_, args) => _liveMonitor.PostForwarded(args.Frame);
            candidateSession.Error += (_, args) => SetStatus(SessionStatusKind.SessionError, detail: args.Exception.Message);

            await candidateSession.StartAsync(cancellationToken);

            _session = candidateSession;
            IsRunning = true;
            SetStatus(SessionStatusKind.Running);
            return true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            SetStatus(SessionStatusKind.StartFailed, detail: ex.Message);
            if (candidateSession is not null)
            {
                try
                {
                    await candidateSession.DisposeAsync();
                }
                catch
                {
                }
            }

            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning || _session is null)
        {
            return;
        }

        var session = _session;
        _session = null;
        IsRunning = false;

        try
        {
            await session.StopAsync();
            await session.DisposeAsync();
            SetStatus(SessionStatusKind.Stopped);
        }
        catch (Exception ex)
        {
            SetStatus(SessionStatusKind.StopFailed, detail: ex.Message);
        }
    }

    public bool ApplyRules(IEnumerable<Rule> rules)
    {
        if (_session is null)
        {
            return false;
        }

        _session.ReplaceRules(rules);
        SetStatus(SessionStatusKind.RunningRulesUpdated, rulesCount: rules.Count());
        return true;
    }

    public void SetTranslator(Func<string, string> translate)
    {
        _translate = translate;
        UpdateStatusText();
    }

    private void SetStatus(SessionStatusKind kind, string? detail = null, int rulesCount = 0)
    {
        _statusKind = kind;
        _statusDetail = detail ?? string.Empty;
        _statusRulesCount = rulesCount;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        Status = _statusKind switch
        {
            SessionStatusKind.Idle => _translate("StatusIdle"),
            SessionStatusKind.Running => _translate("StatusRunning"),
            SessionStatusKind.Stopped => _translate("StatusStopped"),
            SessionStatusKind.StartFailed => string.Format(_translate("StatusStartFailed"), _statusDetail),
            SessionStatusKind.RunningRulesUpdated => string.Format(_translate("StatusRunningRulesUpdated"), _statusRulesCount),
            SessionStatusKind.StopFailed => string.Format(_translate("StatusStopFailed"), _statusDetail),
            SessionStatusKind.SessionError => string.Format(_translate("StatusSessionError"), _statusDetail),
            _ => _translate("StatusIdle"),
        };
    }
}
