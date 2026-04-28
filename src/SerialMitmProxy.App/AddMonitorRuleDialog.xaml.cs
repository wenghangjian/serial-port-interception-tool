using System.Windows;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.App;

public enum MonitorRuleDialogKind
{
    Hex = 0,
    Ascii = 1,
}

public partial class AddMonitorRuleDialog : Window
{
    public AddMonitorRuleDialog(MonitorRuleDialogKind kind, Func<string, string> translate)
    {
        InitializeComponent();
        Kind = kind;
        _translate = translate;
        ConfigureTexts();
        DirectionComboBox.ItemsSource = new[]
        {
            new DirectionOption(null, _translate("DirectionAll")),
            new DirectionOption(Direction.AtoB, _translate("DirAtoB")),
            new DirectionOption(Direction.BtoA, _translate("DirBtoA")),
        };
        DirectionComboBox.SelectedIndex = 0;
    }

    private readonly Func<string, string> _translate;

    public MonitorRuleDialogKind Kind { get; }

    public string RuleName => RuleNameTextBox.Text.Trim();

    public Direction? SelectedDirection => DirectionComboBox.SelectedValue switch
    {
        Direction direction => direction,
        _ => null,
    };

    public string Pattern => PatternTextBox.Text.Trim();

    private void ConfigureTexts()
    {
        Title = Kind == MonitorRuleDialogKind.Hex
            ? _translate("AddHexMonitorRule")
            : _translate("AddAsciiMonitorRule");

        DescriptionTextBlock.Text = Kind == MonitorRuleDialogKind.Hex
            ? _translate("AddHexMonitorRuleDescription")
            : _translate("AddAsciiMonitorRuleDescription");

        RuleNameLabel.Text = _translate("RuleName");
        DirectionLabel.Text = _translate("ColDirection");
        PatternLabel.Text = Kind == MonitorRuleDialogKind.Hex
            ? _translate("HexPattern")
            : _translate("AsciiRegex");

        HintTextBlock.Text = Kind == MonitorRuleDialogKind.Hex
            ? _translate("HexMonitorRuleHint")
            : _translate("AsciiMonitorRuleHint");

        SaveButton.Content = _translate("AddRule");
        CancelButton.Content = _translate("Cancel");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (Pattern.Length == 0)
        {
            MessageBox.Show(
                _translate("MonitorRulePatternRequired"),
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record DirectionOption(Direction? Value, string Label);
}
