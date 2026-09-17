using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.References;

public sealed partial class EmployeeEditDialog : AppEditDialog
{
    private bool _isClosed;

    public EmployeeEditDialog(EmployeeEditViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        IdRow.Visibility = viewModel.State.IsCreateMode ? Visibility.Collapsed : Visibility.Visible;
        ConfigureXamlFooter(SaveButton, CancelButton);
        Closed += (_, _) =>
        {
            _isClosed = true;
            ViewModel.CancelLookups();
        };
        DialogChrome.Apply(this);
    }

    public EmployeeEditViewModel ViewModel { get; }

    public override bool Validate()
    {
        if (ViewModel.CanSubmit) return true;
        ViewModel.ShowErrorInfo("Заполните обязательные поля или внесите изменения.");
        return false;
    }

    private async void Position_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_isClosed || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var input = sender.Text;
        try
        {
            await ViewModel.UpdatePositionOptionsAsync(input);
            if (!_isClosed && sender.Text == input)
                sender.IsSuggestionListOpen = ViewModel.PositionOptions.Count > 0;
        }
        catch (Exception ex)
        {
            DiagnosticsFileLogger.AppendBlock("EmployeeEditDialog.Position_TextChanged",
                $"input={System.Text.Json.JsonSerializer.Serialize(input)}{Environment.NewLine}{ex}");
            if (!_isClosed) ViewModel.ShowErrorInfo(ex.Message);
        }
    }

    private async void Contragent_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_isClosed || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var input = sender.Text;
        try
        {
            await ViewModel.UpdateContragentOptionsAsync(input);
            if (!_isClosed && sender.Text == input)
                sender.IsSuggestionListOpen = ViewModel.ContragentOptions.Count > 0;
        }
        catch (Exception ex)
        {
            DiagnosticsFileLogger.AppendBlock("EmployeeEditDialog.Contragent_TextChanged",
                $"input={System.Text.Json.JsonSerializer.Serialize(input)}{Environment.NewLine}{ex}");
            if (!_isClosed) ViewModel.ShowErrorInfo(ex.Message);
        }
    }

    private void Position_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        => ViewModel.SelectPositionOption(ViewModel.FindPositionOption((string)args.SelectedItem));

    private void Contragent_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        => ViewModel.SelectContragentOption(ViewModel.FindContragentOption((string)args.SelectedItem));

    private void Position_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.CommitPositionInput(sender.Text);
        sender.IsSuggestionListOpen = false;
    }

    private void Contragent_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.CommitContragentInput(sender.Text);
        sender.IsSuggestionListOpen = false;
    }

    private void Position_LostFocus(object sender, RoutedEventArgs args)
        => ViewModel.CommitPositionInput(PositionEditor.Text);

    private void Contragent_LostFocus(object sender, RoutedEventArgs args)
        => ViewModel.CommitContragentInput(ContragentEditor.Text);
}
