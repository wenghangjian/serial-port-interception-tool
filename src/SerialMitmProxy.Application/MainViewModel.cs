using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Application.Intercept;
using SerialMitmProxy.Application.Monitoring;
using SerialMitmProxy.Application.Replay;
using SerialMitmProxy.Application.Rules;
using SerialMitmProxy.Application.Session;

namespace SerialMitmProxy.Application;

public sealed class MainViewModel : ViewModelBase
{
    public MainViewModel(
        SessionManagerViewModel sessionManager,
        LiveMonitorViewModel liveMonitor,
        InterceptQueueViewModel interceptQueue,
        ChecksumToolViewModel checksumTool,
        RuleEditorViewModel ruleEditor,
        ReplayControllerViewModel replayController,
        UiLocalizer localizer)
    {
        SessionManager = sessionManager;
        LiveMonitor = liveMonitor;
        InterceptQueue = interceptQueue;
        ChecksumTool = checksumTool;
        RuleEditor = ruleEditor;
        ReplayController = replayController;
        Localizer = localizer;
    }

    public SessionManagerViewModel SessionManager { get; }

    public LiveMonitorViewModel LiveMonitor { get; }

    public InterceptQueueViewModel InterceptQueue { get; }

    public ChecksumToolViewModel ChecksumTool { get; }

    public RuleEditorViewModel RuleEditor { get; }

    public ReplayControllerViewModel ReplayController { get; }

    public UiLocalizer Localizer { get; }
}
