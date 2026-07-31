using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class StageOrderEditDialog : AppEditDialog
    {
        private readonly StageOrderEditViewModel _vm;
        public StageOrderEditDialog(StageOrderEditViewModel vm)
        {
            _vm = vm; Title = vm.State.IsCreateMode ? "Новая позиция заказа" : "Редактирование позиции";
            Resources["ContentDialogMinWidth"] = 760d;
            Resources["ContentDialogMaxWidth"] = 820d;
            Content = BuildEditContent(BuildContent()); DialogChrome.Apply(this);
        }
        public override bool Validate()
        {
            try { _ = StageOrderEditPayloadBuilder.Build(_vm); return true; }
            catch (Exception ex) { ShowErrorInfo(ex.Message); return false; }
        }
        private FrameworkElement BuildContent()
        {
            var isControlFieldsOnly = _vm.State.AccessMode == StageOrderEditAccessMode.ControlFieldsOnly;
            var g = new Grid { Padding = new Thickness(8, 0, 8, 8), RowSpacing = 7, ColumnSpacing = 8 };
            for (var i = 0; i < 3; i++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            var stageEditor = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(StageOrderEditViewModel.StageSuggestions),
                () => _vm.StageInput,
                _vm.UpdateStageSuggestionsAsync,
                _vm.TrySelectStage,
                _vm.CommitStage,
                () => _vm.StageInput,
                minWidth: 290,
                maxWidth: 360,
                maxSuggestionListHeight: 240,
                bindingSource: _vm);
            stageEditor.IsEnabled = !isControlFieldsOnly;
            if (!_vm.State.IsStageFixed)
            {
                Add(g, Labeled("Этап", stageEditor), 0, 0);
            }
            var toolEditor = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(StageOrderEditViewModel.ToolSuggestions),
                () => _vm.ToolInput,
                _vm.UpdateToolSuggestionsAsync,
                _vm.TrySelectTool,
                _vm.CommitTool,
                () => _vm.ToolInput,
                minWidth: 290,
                maxWidth: 360,
                maxSuggestionListHeight: 240,
                bindingSource: _vm);
            toolEditor.IsEnabled = !isControlFieldsOnly;
            var toolControl = Labeled("Товар *", toolEditor);
            if (_vm.State.IsStageFixed)
            {
                Grid.SetColumnSpan(toolControl, 2);
                Add(g, toolControl, 0, 0);
            }
            else
            {
                Add(g, toolControl, 0, 1);
            }
            Add(g, Labeled("Важность", Dropdown(_vm.SeverityOptions, _vm.SelectedSeverity, x => _vm.SelectedSeverity = x as ReferenceEnumOption, canClear: true)), 0, 2);
            var values = new Grid { ColumnSpacing = 8 };
            values.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            values.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            values.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            values.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            var priceEditor = Text(nameof(StageOrderEditViewModel.PriceCost));
            var amountEditor = Text(nameof(StageOrderEditViewModel.Amount));
            var costEditor = Text(nameof(StageOrderEditViewModel.Cost));
            priceEditor.IsEnabled = !isControlFieldsOnly;
            amountEditor.IsEnabled = !isControlFieldsOnly;
            costEditor.IsEnabled = !isControlFieldsOnly;
            Add(values, Labeled("Цена", priceEditor), 0, 0);
            Add(values, Labeled("Количество", amountEditor), 0, 1);
            Add(values, Labeled("Сумма", costEditor), 0, 2);
            Add(values, Labeled("Приоритет", Text(nameof(StageOrderEditViewModel.Priority))), 0, 3);
            Grid.SetColumnSpan(values, 3); Add(g, values, 1, 0);
            var description = Text(nameof(StageOrderEditViewModel.Description)); description.AcceptsReturn = true; description.MinHeight = 58;
            var d = Labeled("Описание", description); Grid.SetColumnSpan(d, 3); Add(g, d, 2, 0);
            return g;
        }
        private Dropdown Dropdown(IEnumerable<object> items, object? selected, Action<object?> changed, bool canClear = false)
        {
            var d = new Dropdown { DisplayMemberPath = "Label", TextMemberPath = "Label", MatchMemberPath = "Label", ItemsSource = items, SelectedItem = selected, IsClearButtonEnabled = canClear };
            d.SelectionChanged += (_, _) => changed(d.SelectedItem); return d;
        }
        private TextBox Text(string path) { var t = new TextBox(); t.SetBinding(TextBox.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = _vm, Path = new PropertyPath(path), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay, UpdateSourceTrigger = Microsoft.UI.Xaml.Data.UpdateSourceTrigger.PropertyChanged }); return t; }
        private static FrameworkElement Labeled(string label, UIElement control) => (FrameworkElement)BuildLabeledControl(label, control, 3);
        private static void Add(Grid g, UIElement e, int r, int c) { Grid.SetRow((FrameworkElement)e, r); Grid.SetColumn((FrameworkElement)e, c); g.Children.Add(e); }
    }
}
