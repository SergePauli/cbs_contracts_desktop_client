using System.Diagnostics;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class RevisionEditDialog : ContentDialog
    {
        private readonly TextBox _descriptionBox = new();
        private readonly CheckBox _isSignedBox = new();
        private readonly CheckBox _isPresentBox = new();
        private readonly CheckBox _usedBox = new();
        private readonly TextBox _docLinkBox = new();
        private readonly TextBox _scanLinkBox = new();
        private readonly TextBox _protocolLinkBox = new();
        private readonly TextBox _zipLinkBox = new();
        private readonly TextBlock _errorText = new();
        private readonly string? _listKey;

        public RevisionEditDialog(ReferenceDataRow sourceRow)
        {
            ArgumentNullException.ThrowIfNull(sourceRow);

            Id = TryGetLong(sourceRow.GetValue("id"))
                ?? throw new InvalidOperationException("У выбранной ревизии отсутствует ID.");
            _listKey = sourceRow.GetValue("list_key")?.ToString();

            var contractName = sourceRow.GetValue("contract.name")?.ToString() ?? string.Empty;
            var revisionNumber = FormatRevisionNumber(sourceRow.GetValue("priority"));

            PrimaryButtonText = "Сохранить";
            CloseButtonText = "Отмена";
            DefaultButton = ContentDialogButton.Primary;
            Resources["ContentDialogMinWidth"] = 720d;
            Resources["ContentDialogMaxWidth"] = 920d;
            Content = BuildContent(sourceRow);
            DialogChrome.Apply(this, BuildTitleText(contractName, revisionNumber));
        }

        public long Id { get; }

        public IReadOnlyDictionary<string, object?> BuildPayload()
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = Id,
                ["description"] = _descriptionBox.Text ?? string.Empty,
                ["is_signed"] = _isSignedBox.IsChecked == true,
                ["is_present"] = _isPresentBox.IsChecked == true,
                ["used"] = _usedBox.IsChecked == true,
                ["doc_link"] = NormalizeOptionalText(_docLinkBox.Text),
                ["scan_link"] = NormalizeOptionalText(_scanLinkBox.Text),
                ["protocol_link"] = NormalizeOptionalText(_protocolLinkBox.Text),
                ["zip_link"] = NormalizeOptionalText(_zipLinkBox.Text)
            };

            if (!string.IsNullOrWhiteSpace(_listKey))
            {
                payload["list_key"] = _listKey;
            }

            return payload;
        }

        public void ShowErrorInfo(string message)
        {
            _errorText.Text = message;
            _errorText.Visibility = Visibility.Visible;
        }

        private static string BuildTitleText(string contractName, string revisionNumber)
        {
            return string.IsNullOrWhiteSpace(contractName)
                ? $"Редактирование ревизии {revisionNumber}"
                : $"Редактирование ревизии {revisionNumber} контракта {contractName}";
        }

        private UIElement BuildContent(ReferenceDataRow sourceRow)
        {
            var root = new Grid
            {
                MinWidth = 680,
                MaxWidth = 860
            };

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 620,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var stack = new StackPanel
            {
                Spacing = 14
            };
            scrollViewer.Content = stack;

            var flagsGrid = new Grid
            {
                ColumnSpacing = 18
            };
            flagsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            flagsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            flagsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _isSignedBox.Content = "Подписан";
            _isSignedBox.IsChecked = TryGetBool(sourceRow.GetValue("is_signed")) == true;
            _isPresentBox.Content = "В наличии";
            _isPresentBox.IsChecked = TryGetBool(sourceRow.GetValue("is_present")) == true;
            _usedBox.Content = "Используется";
            _usedBox.IsChecked = TryGetBool(sourceRow.GetValue("used")) != false;

            flagsGrid.Children.Add(_isSignedBox);
            Grid.SetColumn(_isPresentBox, 1);
            flagsGrid.Children.Add(_isPresentBox);
            Grid.SetColumn(_usedBox, 2);
            flagsGrid.Children.Add(_usedBox);
            stack.Children.Add(flagsGrid);

            _descriptionBox.Text = sourceRow.GetValue("description")?.ToString() ?? string.Empty;
            _descriptionBox.AcceptsReturn = true;
            _descriptionBox.TextWrapping = TextWrapping.Wrap;
            _descriptionBox.MinHeight = 92;
            _descriptionBox.MaxHeight = 160;
            stack.Children.Add(BuildLabeledControl("Комментарий", _descriptionBox));

            _docLinkBox.Text = sourceRow.GetValue("doc_link")?.ToString() ?? string.Empty;
            _scanLinkBox.Text = sourceRow.GetValue("scan_link")?.ToString() ?? string.Empty;
            _protocolLinkBox.Text = sourceRow.GetValue("protocol_link")?.ToString() ?? string.Empty;
            _zipLinkBox.Text = sourceRow.GetValue("zip_link")?.ToString() ?? string.Empty;

            stack.Children.Add(BuildFileLinkEditor("Файл договора", _docLinkBox));
            stack.Children.Add(BuildFileLinkEditor("Скан", _scanLinkBox));
            stack.Children.Add(BuildFileLinkEditor("Протокол", _protocolLinkBox));
            stack.Children.Add(BuildFileLinkEditor("Архив", _zipLinkBox));

            _errorText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            _errorText.TextWrapping = TextWrapping.Wrap;
            _errorText.Visibility = Visibility.Collapsed;
            stack.Children.Add(_errorText);

            root.Children.Add(scrollViewer);
            return root;
        }

        private static UIElement BuildLabeledControl(string label, UIElement control)
        {
            var stack = new StackPanel
            {
                Spacing = 6
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            stack.Children.Add(control);
            return stack;
        }

        private UIElement BuildFileLinkEditor(string label, TextBox editor)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            editor.PlaceholderText = "Путь к файлу";

            var pickButton = new Button
            {
                Content = "Выбрать"
            };
            pickButton.Click += async (_, _) => await PickFilePathAsync(editor);

            var openButton = new Button
            {
                Content = "Открыть"
            };
            openButton.Click += (_, _) => OpenFilePath(editor.Text);

            Grid.SetColumn(editor, 0);
            grid.Children.Add(editor);
            Grid.SetColumn(pickButton, 1);
            grid.Children.Add(pickButton);
            Grid.SetColumn(openButton, 2);
            grid.Children.Add(openButton);

            return BuildLabeledControl(label, grid);
        }

        private async Task PickFilePathAsync(TextBox target)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                picker.FileTypeFilter.Add("*");

                if (App.CurrentWindow is not null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file is not null)
                {
                    target.Text = file.Path;
                }
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось выбрать файл: {ex.Message}");
            }
        }

        private void OpenFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowErrorInfo("Путь к файлу не заполнен.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath.Trim(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось открыть файл: {ex.Message}");
            }
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string FormatRevisionNumber(object? value)
        {
            return TryGetLong(value) is long priority
                ? priority == 0 ? "договор" : priority.ToString()
                : value?.ToString() ?? string.Empty;
        }

        private static bool? TryGetBool(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                string text when long.TryParse(text, out var numericValue) => numericValue != 0,
                long int64Value => int64Value != 0,
                int int32Value => int32Value != 0,
                _ => null
            };
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
