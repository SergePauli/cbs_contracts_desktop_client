using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class IsecurityToolEditDialog : AppEditDialog
    {
        private readonly ReferenceEditViewModel _viewModel;
        private readonly ReferenceEditFieldViewModel _nameField;
        private readonly ReferenceEditFieldViewModel _unitField;
        private readonly ReferenceEditFieldViewModel _defaultCostField;
        private readonly ReferenceEditFieldViewModel _kindField;
        private readonly TextBox _nameEditor;
        private readonly Dropdown _unitEditor = new();
        private readonly TextBox _defaultCostEditor;
        private readonly Dropdown _kindEditor = new();

        public IsecurityToolEditDialog(
            ReferenceEditViewModel viewModel,
            IReadOnlyList<string> unitOptions)
        {
            _viewModel = viewModel;
            _nameField = GetField("name");
            _unitField = GetField("unit");
            _defaultCostField = GetField("default_cost");
            _kindField = GetField("kind");

            _nameEditor = new TextBox { Text = _nameField.TextValue };
            _defaultCostEditor = BuildMoneyInputTextBox(_defaultCostField.TextValue);
            ConfigureUnitEditor(unitOptions);
            ConfigureKindEditor();

            Title = viewModel.DialogTitle;
            Content = BuildEditContent(BuildContent());
            DialogChrome.Apply(this);
        }

        public override bool Validate()
        {
            _unitEditor.CommitText();
            _nameField.TextValue = _nameEditor.Text;
            _unitField.TextValue = _unitEditor.Text;
            _defaultCostField.TextValue = _defaultCostEditor.Text;
            _kindField.SelectedEnumOption = _kindEditor.SelectedItem as ReferenceEnumOption;

            if (_viewModel.CanSubmit)
            {
                return true;
            }

            ShowErrorInfo("Заполните обязательные поля корректными значениями или внесите изменения.");
            return false;
        }

        private FrameworkElement BuildContent()
        {
            var grid = new Grid
            {
                Padding = new Thickness(8, 0, 8, 8),
                RowSpacing = 8,
                ColumnSpacing = 8,
                MinWidth = 560
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var nameControl = (FrameworkElement)BuildLabeledControl("Наименование *", _nameEditor, spacing: 3);
            Grid.SetColumnSpan(nameControl, 3);
            grid.Children.Add(nameControl);

            var unitControl = (FrameworkElement)BuildLabeledControl("Единица измерения", _unitEditor, spacing: 3);
            Grid.SetRow(unitControl, 1);
            grid.Children.Add(unitControl);

            var costControl = (FrameworkElement)BuildLabeledControl("Стоимость по умолчанию", _defaultCostEditor, spacing: 3);
            Grid.SetRow(costControl, 1);
            Grid.SetColumn(costControl, 1);
            grid.Children.Add(costControl);

            var kindControl = (FrameworkElement)BuildLabeledControl("Тип товара *", _kindEditor, spacing: 3);
            Grid.SetRow(kindControl, 1);
            Grid.SetColumn(kindControl, 2);
            grid.Children.Add(kindControl);

            return grid;
        }

        private void ConfigureUnitEditor(IReadOnlyList<string> unitOptions)
        {
            _unitEditor.AllowCustomOptions = true;
            _unitEditor.IsClearButtonEnabled = true;
            foreach (var unit in unitOptions)
            {
                _unitEditor.Items.Add(unit);
            }

            _unitEditor.Text = _unitField.TextValue;
        }

        private void ConfigureKindEditor()
        {
            _kindEditor.DisplayMemberPath = nameof(ReferenceEnumOption.Label);
            _kindEditor.TextMemberPath = nameof(ReferenceEnumOption.Label);
            _kindEditor.MatchMemberPath = nameof(ReferenceEnumOption.Label);
            _kindEditor.IsClearButtonEnabled = false;
            _kindEditor.ItemsSource = _kindField.EnumOptions;
            _kindEditor.SelectedItem = _kindField.SelectedEnumOption;
        }

        private ReferenceEditFieldViewModel GetField(string fieldKey)
        {
            return _viewModel.Fields.Single(field =>
                string.Equals(field.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
        }
    }
}
