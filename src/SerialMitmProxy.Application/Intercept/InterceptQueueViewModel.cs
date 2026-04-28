using System.Collections.ObjectModel;
using System.Globalization;
using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Core.Intercept;

namespace SerialMitmProxy.Application.Intercept;

public sealed class InterceptQueueItemViewModel : ViewModelBase
{
    private string _editedHex;

    public InterceptQueueItemViewModel(InterceptRequest request)
    {
        Request = request;
        _editedHex = BitConverter.ToString(request.Frame.Payload).Replace('-', ' ');
    }

    public InterceptRequest Request { get; }

    public string EditedHex
    {
        get => _editedHex;
        set => SetProperty(ref _editedHex, value);
    }

    public bool Complete(InterceptDecision decision)
    {
        return Request.Resolve(decision);
    }

    public byte[] ParseEditedPayload()
    {
        var tokens = EditedHex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Select(token => byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
    }
}

public sealed class InterceptQueueViewModel : ViewModelBase
{
    private readonly InterceptManager _manager;
    private readonly SynchronizationContext? _syncContext;
    private Func<string, string> _translate;
    private bool _isIntercepting;

    public InterceptQueueViewModel(InterceptManager manager, Func<string, string>? translate = null)
    {
        _manager = manager;
        _syncContext = SynchronizationContext.Current;
        _translate = translate ?? (static key => key);
        Pending.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(PendingCount));
        _manager.InterceptionEnabledChanged += HandleInterceptionEnabledChanged;
        UpdateState(_manager.IsEnabled);
    }

    public ObservableCollection<InterceptQueueItemViewModel> Pending { get; } = new();

    public bool IsIntercepting
    {
        get => _isIntercepting;
        private set => SetProperty(ref _isIntercepting, value);
    }

    public int PendingCount => Pending.Count;

    public string ModeTitle => IsIntercepting
        ? _translate("InterceptModeEnabled")
        : _translate("InterceptModeDisabled");

    public string ModeDescription => IsIntercepting
        ? _translate("InterceptModeEnabledHint")
        : _translate("InterceptModeDisabledHint");

    public string ToggleButtonText => IsIntercepting
        ? _translate("StopIntercept")
        : _translate("StartIntercept");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _manager.ReadPendingAsync(cancellationToken).ConfigureAwait(false))
        {
            if (request.IsCompleted)
            {
                continue;
            }

            var item = new InterceptQueueItemViewModel(request);
            if (_syncContext is not null)
            {
                _syncContext.Post(_ => Pending.Add(item), null);
            }
            else
            {
                Pending.Add(item);
            }
        }
    }

    public void ToggleInterception()
    {
        _manager.SetEnabled(!IsIntercepting);
    }

    public void SetTranslator(Func<string, string> translate)
    {
        _translate = translate;
        RaiseModePropertiesChanged();
    }

    public void Forward(InterceptQueueItemViewModel item)
    {
        var payload = item.ParseEditedPayload();
        var originalPayload = item.Request.Frame.Payload;
        var decision = payload.AsSpan().SequenceEqual(originalPayload)
            ? new InterceptDecision { Command = InterceptCommand.Forward }
            : new InterceptDecision
            {
                Command = InterceptCommand.EditAndForward,
                EditedPayload = payload,
            };

        if (item.Complete(decision))
        {
            Pending.Remove(item);
        }
    }

    public void Drop(InterceptQueueItemViewModel item)
    {
        if (item.Complete(new InterceptDecision { Command = InterceptCommand.Drop }))
        {
            Pending.Remove(item);
        }
    }

    public void EditAndForward(InterceptQueueItemViewModel item)
    {
        var payload = item.ParseEditedPayload();
        if (item.Complete(new InterceptDecision
            {
                Command = InterceptCommand.EditAndForward,
                EditedPayload = payload,
            }))
        {
            Pending.Remove(item);
        }
    }

    public void Repeat(InterceptQueueItemViewModel item, int repeatCount)
    {
        if (item.Complete(new InterceptDecision
            {
                Command = InterceptCommand.Repeat,
                RepeatCount = repeatCount,
            }))
        {
            Pending.Remove(item);
        }
    }

    public void Inject(InterceptQueueItemViewModel item, byte[] injectPayload)
    {
        if (item.Complete(new InterceptDecision
            {
                Command = InterceptCommand.Inject,
                InjectPayload = injectPayload,
            }))
        {
            Pending.Remove(item);
        }
    }

    private void HandleInterceptionEnabledChanged(bool isEnabled)
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => UpdateState(isEnabled), null);
            return;
        }

        UpdateState(isEnabled);
    }

    private void UpdateState(bool isEnabled)
    {
        IsIntercepting = isEnabled;
        if (!isEnabled)
        {
            Pending.Clear();
        }

        RaiseModePropertiesChanged();
    }

    private void RaiseModePropertiesChanged()
    {
        RaisePropertyChanged(nameof(ModeTitle));
        RaisePropertyChanged(nameof(ModeDescription));
        RaisePropertyChanged(nameof(ToggleButtonText));
    }
}
