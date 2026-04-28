using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace CbsContractsDesktopClient.Views.References
{
    public static class DialogLookupEditors
    {
        public static AutoSuggestBox BuildAutoSuggestBox(
            string itemsSourcePath,
            Func<string> getInitialText,
            Func<string, Task> updateOptionsAsync,
            Func<string?, bool> trySelectSuggestion,
            Action<string?> commitInput,
            Func<string> getCommittedText,
            double minWidth = 280,
            double maxSuggestionListHeight = 180,
            double? maxWidth = null)
        {
            var autoSuggestBox = new AutoSuggestBox
            {
                MinWidth = minWidth,
                MaxWidth = maxWidth ?? minWidth,
                Width = maxWidth ?? minWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxSuggestionListHeight = maxSuggestionListHeight,
                UpdateTextOnSelect = false,
                ItemTemplate = BuildSuggestionTemplate()
            };
            autoSuggestBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding
            {
                Path = new PropertyPath(itemsSourcePath)
            });

            autoSuggestBox.Loaded += (_, _) =>
            {
                autoSuggestBox.Text = getInitialText();
            };

            autoSuggestBox.TextChanged += async (_, args) =>
            {
                if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    return;
                }

                await updateOptionsAsync(autoSuggestBox.Text);
                autoSuggestBox.IsSuggestionListOpen = autoSuggestBox.Items.Count > 0;
            };

            autoSuggestBox.SuggestionChosen += (_, args) =>
            {
                if (trySelectSuggestion(args.SelectedItem as string))
                {
                    autoSuggestBox.Text = getCommittedText();
                }
            };

            autoSuggestBox.QuerySubmitted += (_, args) =>
            {
                var chosenLabel = args.ChosenSuggestion as string;
                if (!trySelectSuggestion(chosenLabel))
                {
                    commitInput(autoSuggestBox.Text);
                }

                autoSuggestBox.Text = getCommittedText();
                autoSuggestBox.IsSuggestionListOpen = false;
            };

            autoSuggestBox.LostFocus += (_, _) =>
            {
                commitInput(autoSuggestBox.Text);
                autoSuggestBox.Text = getCommittedText();
                autoSuggestBox.IsSuggestionListOpen = false;
            };

            return autoSuggestBox;
        }

        private static DataTemplate BuildSuggestionTemplate()
        {
            return (DataTemplate)XamlReader.Load(
                """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                  <TextBlock Text="{Binding}" Margin="0" Padding="0" Height="24" TextTrimming="CharacterEllipsis" />
                </DataTemplate>
                """);
        }
    }
}
