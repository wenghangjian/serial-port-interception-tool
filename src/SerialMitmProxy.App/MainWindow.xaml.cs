using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SerialMitmProxy.Application;
using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Application.Intercept;
using SerialMitmProxy.Application.Monitoring;
using SerialMitmProxy.Application.Replay;
using SerialMitmProxy.Application.Rules;
using SerialMitmProxy.Application.Session;
using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Capture;
using SerialMitmProxy.Core.Decoders;
using SerialMitmProxy.Core.Intercept;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Proxy;
using SerialMitmProxy.Core.Rules;
using SerialMitmProxy.Infrastructure.Memory;
using SerialMitmProxy.Infrastructure.Plugins;
using SerialMitmProxy.Infrastructure.Serial;

namespace SerialMitmProxy.App;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly InterceptManager _interceptManager = new();
    private readonly MainViewModel _viewModel;
    private AppConfig _config;
    private readonly UiLocalizer _localizer;

    public ConfigEditorViewModel ConfigEditor { get; }

    public MainWindow()
    {
        InitializeComponent();
        _config = AppConfigLoader.Load(Environment.CurrentDirectory);
        _localizer = new UiLocalizer();
        ConfigEditor = new ConfigEditorViewModel();
        ConfigEditor.LoadFrom(_config);

        var liveMonitor = new LiveMonitorViewModel();
        liveMonitor.MaxFrames = _config.Monitor.MaxFrames;
        var ruleEditor = new RuleEditorViewModel();
        liveMonitor.SetMonitorRulesProvider(() => ruleEditor.Rules.ToArray());
        var replayController = new ReplayControllerViewModel
        {
            CaptureFolder = Path.Combine(Environment.CurrentDirectory, _config.Capture.Folder),
            SpeedFactor = 1.0,
        };

        var sessionManager = new SessionManagerViewModel(
            () => CreateSession(ruleEditor, _interceptManager, _config),
            liveMonitor,
            _localizer.Translate);

        var interceptQueue = new InterceptQueueViewModel(_interceptManager, _localizer.Translate);
        var checksumTool = new ChecksumToolViewModel(_localizer.Translate);

        _viewModel = new MainViewModel(sessionManager, liveMonitor, interceptQueue, checksumTool, ruleEditor, replayController, _localizer);
        DataContext = _viewModel;
        ApplyConfigToViewModels(_config);
        ApplyLocalizedUiTexts();
        ApplyLiveMonitorColumnVisibility();

        _ = interceptQueue.StartAsync(_lifetimeCts.Token);
    }

    private void ApplyConfigToViewModels(AppConfig config)
    {
        _viewModel.LiveMonitor.MaxFrames = config.Monitor.MaxFrames;
        _viewModel.LiveMonitor.UiThrottleMs = config.Monitor.UiThrottleMs;
        _viewModel.ReplayController.CaptureFolder = Path.Combine(Environment.CurrentDirectory, config.Capture.Folder);
    }

    private static ProxySession CreateSession(
        RuleEditorViewModel ruleEditor,
        InterceptManager interceptManager,
        AppConfig config)
    {
        IProxyEndpoint endpointA;
        IProxyEndpoint endpointB;
        if (config.Session.UseInMemory)
        {
            endpointA = new InMemoryProxyEndpoint("EndpointA");
            endpointB = new InMemoryProxyEndpoint("EndpointB");
        }
        else
        {
            endpointA = new SerialPortEndpoint("EndpointA", config.Session.EndpointA.ToOptions());
            endpointB = new SerialPortEndpoint("EndpointB", config.Session.EndpointB.ToOptions());
        }

        var decoderA = CreateDecoder(config.Session.Decoders);
        var decoderB = CreateDecoder(config.Session.Decoders);
        var ruleEngine = new RuleEngine(ruleEditor.Rules);

        CaptureWriter? captureWriter = null;
        if (config.Capture.Enabled)
        {
            var folder = Path.Combine(
                Environment.CurrentDirectory,
                config.Capture.Folder,
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            captureWriter = CaptureWriter.Create(folder);
        }

        var pluginFolder = Path.Combine(Environment.CurrentDirectory, config.Plugins.Folder);
        var plugins = PluginLoader.Load(pluginFolder);

        return new ProxySession(
            endpointA,
            endpointB,
            decoderA,
            decoderB,
            ruleEngine,
            interceptManager,
            captureWriter,
            plugins);
    }

    private static IFrameDecoder CreateDecoder(DecoderConfig config)
    {
        return config.Mode.ToUpperInvariant() switch
        {
            "DELIMITER" => new DelimiterDecoder(ParseHex(config.DelimiterHex)),
            "FIXEDLENGTH" => new FixedLengthDecoder(config.FixedLength),
            _ => new TimeSliceDecoder(TimeSpan.FromMilliseconds(config.TimeSliceMs)),
        };
    }

    private static byte[] ParseHex(string hex)
    {
        var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Select(token => Convert.ToByte(token, 16)).ToArray();
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var started = await _viewModel.SessionManager.StartAsync(_lifetimeCts.Token);
            if (!started)
            {
                MessageBox.Show(
                    _viewModel.SessionManager.Status,
                    T("StartSessionFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("UnexpectedError"), ex.Message),
                T("StartSessionFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void StopSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.SessionManager.StopAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("StopSessionFailedMessage"), ex.Message),
                T("StopSessionFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void SaveConfigAndReload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = Path.Combine(Environment.CurrentDirectory, "serialmitmproxy.json");
            ConfigEditor.SaveToFile(configPath);

            _config = AppConfigLoader.Load(Environment.CurrentDirectory);
            ConfigEditor.LoadFrom(_config);
            ApplyConfigToViewModels(_config);

            await _viewModel.SessionManager.StopAsync();
            _viewModel.LiveMonitor.Clear();

            var started = await _viewModel.SessionManager.StartAsync(_lifetimeCts.Token);
            if (started)
            {
                MessageBox.Show(
                    T("ConfigSavedReloaded"),
                    T("ConfigSaved"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                string.Format(T("ConfigSavedStartFailed"), _viewModel.SessionManager.Status),
                T("ConfigSaved"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("ConfigSaveFailedMessage"), ex.Message),
                T("ConfigSaveFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AddInterceptRuleAtoB_Click(object sender, RoutedEventArgs e)
    {
        AddInterceptRule(Direction.AtoB);
    }

    private void AddInterceptRuleBtoA_Click(object sender, RoutedEventArgs e)
    {
        AddInterceptRule(Direction.BtoA);
    }

    private void AddInterceptRule(Direction direction)
    {
        _viewModel.RuleEditor.AddDefaultInterceptRule(direction);
        var applied = _viewModel.SessionManager.ApplyRules(_viewModel.RuleEditor.Rules);
        var directionText = direction == Direction.AtoB ? T("DirAtoB") : T("DirBtoA");
        if (applied)
        {
            MessageBox.Show(
                string.Format(T("RuleAddedRunning"), directionText),
                T("RuleAdded"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            string.Format(T("RuleAddedNotRunning"), directionText),
            T("RuleAdded"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        var selectedRule = GetSelectedRule();
        if (!_viewModel.RuleEditor.RemoveRule(selectedRule))
        {
            return;
        }

        _viewModel.SessionManager.ApplyRules(_viewModel.RuleEditor.Rules);
        _viewModel.LiveMonitor.RefreshFilters();
    }

    private void AddHexMonitorRule_Click(object sender, RoutedEventArgs e)
    {
        AddMonitorRule(MonitorRuleDialogKind.Hex);
    }

    private void AddAsciiMonitorRule_Click(object sender, RoutedEventArgs e)
    {
        AddMonitorRule(MonitorRuleDialogKind.Ascii);
    }

    private void RuleEditorGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not DataGridRow)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
            return;
        }

        grid.SelectedItem = null;
    }

    private void RuleEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.LiveMonitor.RefreshFilters();
    }

    private void ForwardIntercept_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedInterceptItem() is InterceptQueueItemViewModel item)
        {
            try
            {
                CommitInterceptGridEdits();
                _viewModel.InterceptQueue.Forward(item);
            }
            catch (FormatException ex)
            {
                MessageBox.Show(
                    string.Format(T("EditPayloadInvalidMessage"), ex.Message),
                    T("EditPayloadInvalid"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (OverflowException ex)
            {
                MessageBox.Show(
                    string.Format(T("EditPayloadInvalidMessage"), ex.Message),
                    T("EditPayloadInvalid"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void ToggleInterceptMode_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.InterceptQueue.ToggleInterception();
    }

    private void InterceptGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.ChecksumTool.SelectedItem = GetSelectedInterceptItem();
    }

    private void DropIntercept_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedInterceptItem() is InterceptQueueItemViewModel item)
        {
            _viewModel.InterceptQueue.Drop(item);
        }
    }

    private void RepeatIntercept_Click(object sender, RoutedEventArgs e)
    {
        if (GetSelectedInterceptItem() is InterceptQueueItemViewModel item)
        {
            _viewModel.InterceptQueue.Repeat(item, 2);
        }
    }

    private async void LoadReplay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var count = await _viewModel.ReplayController.LoadAsync(_lifetimeCts.Token);
            MessageBox.Show(
                string.Format(T("ReplayLoadedMessage"), count),
                T("ReplayLoaded"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("ReplayLoadFailedMessage"), ex.Message),
                T("ReplayLoadFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BrowseReplayFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = T("SelectReplayFolder"),
            InitialDirectory = Directory.Exists(_viewModel.ReplayController.CaptureFolder)
                ? _viewModel.ReplayController.CaptureFolder
                : Environment.CurrentDirectory,
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ReplayController.CaptureFolder = dialog.FolderName;
        }
    }

    private async void SaveCurrentCaptureForReplay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var frames = _viewModel.LiveMonitor.CreateReplayFramesSnapshot();
            if (frames.Count == 0)
            {
                MessageBox.Show(
                    T("NoCapturedFramesToSave"),
                    T("SaveCurrentCaptureForReplay"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var folder = _viewModel.ReplayController.CaptureFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                MessageBox.Show(
                    T("ReplayFolderRequired"),
                    T("SaveCurrentCaptureForReplay"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Directory.CreateDirectory(folder);
            var count = await _viewModel.ReplayController.SaveCaptureAsync(frames, _lifetimeCts.Token);
            MessageBox.Show(
                string.Format(T("ReplayCaptureSavedMessage"), count, folder),
                T("ReplayCaptureSaved"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("ReplayCaptureSaveFailedMessage"), ex.Message),
                T("ReplayCaptureSaveFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void ReplayAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ReplayController.ReplayAsync(
                frame =>
                {
                    _viewModel.LiveMonitor.Post(frame);
                    return Task.CompletedTask;
                },
                _lifetimeCts.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("ReplayFailedMessage"), ex.Message),
                T("ReplayFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ReplayStep_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel.ReplayController.TryStep(out var frame) && frame is not null)
            {
                _viewModel.LiveMonitor.Post(frame);
                return;
            }

            MessageBox.Show(
                T("ReplayStepNoMore"),
                T("ReplayStep"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(T("ReplayStepFailed"), ex.Message),
                T("ReplayFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void LiveMonitorClear_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LiveMonitor.Clear();
    }

    private void LiveMonitorColumnToggle_Changed(object sender, RoutedEventArgs e)
    {
        ApplyLiveMonitorColumnVisibility();
    }

    private void ApplyLiveMonitorColumnVisibility()
    {
        if (InboundHexPayloadColumn is null
            || InboundAsciiPayloadColumn is null
            || ForwardedHexPayloadColumn is null
            || ForwardedAsciiPayloadColumn is null)
        {
            return;
        }

        if (ShowHexColumnRadioButton.IsChecked != true && ShowAsciiColumnRadioButton.IsChecked != true)
        {
            ShowHexColumnRadioButton.IsChecked = true;
            return;
        }

        var showHex = ShowHexColumnRadioButton.IsChecked == true;
        var hexVisibility = showHex ? Visibility.Visible : Visibility.Collapsed;
        var asciiVisibility = showHex ? Visibility.Collapsed : Visibility.Visible;

        InboundHexPayloadColumn.Visibility = hexVisibility;
        ForwardedHexPayloadColumn.Visibility = hexVisibility;
        InboundAsciiPayloadColumn.Visibility = asciiVisibility;
        ForwardedAsciiPayloadColumn.Visibility = asciiVisibility;
    }

    private void LiveMonitorCopyHex_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMonitorItems(sender);
        if (selected.Count == 0)
        {
            MessageBox.Show(T("NoFrameSelected"), T("CopyHex"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, selected.Select(static x => x.Hex)));
    }

    private void LiveMonitorCopyAscii_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMonitorItems(sender);
        if (selected.Count == 0)
        {
            MessageBox.Show(T("NoFrameSelected"), T("CopyAscii"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, selected.Select(static x => x.Ascii)));
    }

    private void LiveMonitorExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedMonitorItems(sender);
        if (selected.Count == 0)
        {
            MessageBox.Show(T("NoFrameSelected"), T("ExportFrames"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = T("ExportDialogFilter"),
            FileName = $"serial_frames_{DateTime.Now:yyyyMMdd_HHmmss}",
            AddExtension = true,
            DefaultExt = "csv",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (string.Equals(Path.GetExtension(dialog.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            var text = string.Join(
                Environment.NewLine,
                selected.Select(static item =>
                    $"{item.TimestampUtc:HH:mm:ss.fff} | {item.Stage} | {item.Direction} | Modified={item.IsModified} | Len={item.Length} | HEX={item.Hex} | ASCII={item.Ascii}"));
            File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
        }
        else
        {
            var builder = new StringBuilder();
            builder.AppendLine("Timestamp,Source,Direction,Modified,Length,Hex,Ascii");
            foreach (var item in selected)
            {
                builder.AppendLine(
                    string.Join(
                        ",",
                        EscapeCsv(item.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                        EscapeCsv(item.Stage.ToString()),
                        EscapeCsv(item.Direction.ToString()),
                        EscapeCsv(item.IsModified.ToString()),
                        item.Length.ToString(CultureInfo.InvariantCulture),
                        EscapeCsv(item.Hex),
                        EscapeCsv(item.Ascii)));
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private void LanguageSelection_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SessionManager.SetTranslator(_localizer.Translate);
        _viewModel.InterceptQueue.SetTranslator(_localizer.Translate);
        _viewModel.ChecksumTool.SetTranslator(_localizer.Translate);
        ApplyLocalizedUiTexts();
    }

    private void AppendChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CommitInterceptGridEdits();
            _viewModel.ChecksumTool.AppendChecksum();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(
                ex.Message,
                T("ChecksumToolTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OverwriteTailChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CommitInterceptGridEdits();
            _viewModel.ChecksumTool.OverwriteTailChecksum();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(
                ex.Message,
                T("ChecksumToolTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private string T(string key)
    {
        return _localizer.Translate(key);
    }

    private void ApplyLocalizedUiTexts()
    {
        InboundMonitorCopyHexMenuItem.Header = T("ContextCopyHex");
        InboundMonitorCopyAsciiMenuItem.Header = T("ContextCopyAscii");
        InboundMonitorExportMenuItem.Header = T("ContextExportSelected");
        ForwardedMonitorCopyHexMenuItem.Header = T("ContextCopyHex");
        ForwardedMonitorCopyAsciiMenuItem.Header = T("ContextCopyAscii");
        ForwardedMonitorExportMenuItem.Header = T("ContextExportSelected");
        RuleAddHexMonitorMenuItem.Header = T("AddHexMonitorRule");
        RuleAddAsciiMonitorMenuItem.Header = T("AddAsciiMonitorRule");
        RuleDeleteMenuItem.Header = T("ContextDeleteRule");

        InboundMonitorTitleText.Text = T("MonitorViewInbound");
        ForwardedMonitorTitleText.Text = T("MonitorViewForwarded");
        ForwardedMonitorHintText.Text = T("ForwardedModifiedHint");

        InboundTimestampColumn.Header = T("ColTimestamp");
        InboundDirectionColumn.Header = T("ColDirection");
        InboundLengthColumn.Header = T("ColLength");
        InboundHexPayloadColumn.Header = T("ColHexPayload");
        InboundAsciiPayloadColumn.Header = T("ColAscii");

        ForwardedTimestampColumn.Header = T("ColTimestamp");
        ForwardedStatusColumn.Header = T("ColStatus");
        ForwardedDirectionColumn.Header = T("ColDirection");
        ForwardedLengthColumn.Header = T("ColLength");
        ForwardedHexPayloadColumn.Header = T("ColHexPayload");
        ForwardedAsciiPayloadColumn.Header = T("ColAscii");

        InterceptDirectionColumn.Header = T("ColDirection");
        InterceptLengthColumn.Header = T("ColLength");
        InterceptEditableHexColumn.Header = T("ColEditableHex");

        RuleNameColumn.Header = T("ColRuleName");
        RuleEnabledColumn.Header = T("ColEnabled");
        RuleScopeColumn.Header = T("ColScope");
        RuleMatchersColumn.Header = T("ColMatchers");
        RuleActionsColumn.Header = T("ColActions");
    }

    private InterceptQueueItemViewModel? GetSelectedInterceptItem()
    {
        return InterceptGrid.SelectedItem as InterceptQueueItemViewModel;
    }

    private List<FrameDisplayItem> GetSelectedMonitorItems(object? sender = null)
    {
        var grid = GetMonitorGridFromSender(sender) ?? GetActiveMonitorGrid();
        if (grid is null)
        {
            return new List<FrameDisplayItem>();
        }

        var items = grid.SelectedItems.OfType<FrameDisplayItem>().ToList();

        if (items.Count == 0 && grid.SelectedItem is FrameDisplayItem single)
        {
            items.Add(single);
        }

        return items;
    }

    private DataGrid? GetMonitorGridFromSender(object? sender)
    {
        if (sender is MenuItem menuItem
            && ItemsControl.ItemsControlFromItemContainer(menuItem) is ContextMenu menu
            && menu.PlacementTarget is DataGrid grid)
        {
            return grid;
        }

        return null;
    }

    private DataGrid? GetActiveMonitorGrid()
    {
        if (InboundMonitorGrid.IsKeyboardFocusWithin || InboundMonitorGrid.SelectedItems.Count > 0)
        {
            return InboundMonitorGrid;
        }

        if (ForwardedMonitorGrid.IsKeyboardFocusWithin || ForwardedMonitorGrid.SelectedItems.Count > 0)
        {
            return ForwardedMonitorGrid;
        }

        return null;
    }

    private Rule? GetSelectedRule()
    {
        return RuleEditorGrid.SelectedItem as Rule;
    }

    private void CommitInterceptGridEdits()
    {
        InterceptGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        InterceptGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void AddMonitorRule(MonitorRuleDialogKind kind)
    {
        var dialog = new AddMonitorRuleDialog(kind, T)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var pattern = dialog.Pattern;
            var direction = dialog.SelectedDirection;
            var generatedName = BuildMonitorRuleName(kind, direction, pattern);
            var ruleName = string.IsNullOrWhiteSpace(dialog.RuleName)
                ? generatedName
                : dialog.RuleName;

            if (kind == MonitorRuleDialogKind.Hex)
            {
                _viewModel.RuleEditor.AddMonitorHexRule(ruleName, direction, pattern);
            }
            else
            {
                _viewModel.RuleEditor.AddMonitorAsciiRule(ruleName, direction, pattern);
            }

            _viewModel.LiveMonitor.RefreshFilters();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            MessageBox.Show(
                string.Format(T("MonitorRuleInvalidMessage"), ex.Message),
                T("MonitorRuleInvalid"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private string BuildMonitorRuleName(MonitorRuleDialogKind kind, Direction? direction, string pattern)
    {
        var prefix = kind == MonitorRuleDialogKind.Hex
            ? T("RuleKindHexFilter")
            : T("RuleKindAsciiMatch");
        var directionText = direction switch
        {
            Direction.AtoB => T("DirAtoB"),
            Direction.BtoA => T("DirBtoA"),
            _ => T("DirectionAll"),
        };

        return $"{prefix}-{directionText}-{pattern}";
    }

    protected override async void OnClosed(EventArgs e)
    {
        _lifetimeCts.Cancel();
        await _viewModel.SessionManager.StopAsync();
        _viewModel.LiveMonitor.Dispose();
        _lifetimeCts.Dispose();
        base.OnClosed(e);
    }
}
