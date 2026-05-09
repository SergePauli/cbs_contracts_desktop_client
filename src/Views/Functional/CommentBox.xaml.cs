using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed partial class CommentBox : UserControl
    {
        private const int CommersDepartmentId = 2;
        private const int FinDepartmentId = 3;
        private readonly IUserService _userService;

        public static readonly DependencyProperty CommentsProperty =
            DependencyProperty.Register(
                nameof(Comments),
                typeof(IReadOnlyList<ReferenceDataRow>),
                typeof(CommentBox),
                new PropertyMetadata(Array.Empty<ReferenceDataRow>(), OnCommentsChanged));

        public CommentBox()
        {
            _userService = App.Services.GetRequiredService<IUserService>();
            InitializeComponent();
            Render();
        }

        public IReadOnlyList<ReferenceDataRow> Comments
        {
            get => (IReadOnlyList<ReferenceDataRow>)GetValue(CommentsProperty);
            set => SetValue(CommentsProperty, value);
        }

        private static void OnCommentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CommentBox)d).Render();
        }

        private void Render()
        {
            CommentsPanel.Children.Clear();
            var comments = Comments?.Where(static comment => comment is not null && !comment.IsPlaceholder).ToList() ?? [];
            EmptyTextBlock.Visibility = comments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CommentsScrollViewer.Visibility = comments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            foreach (var comment in comments)
            {
                CommentsPanel.Children.Add(BuildCommentRow(comment));
            }
        }

        private FrameworkElement BuildCommentRow(ReferenceDataRow comment)
        {
            var departmentId = TryGetInt(comment.GetValue("profile.department.id"));
            var profileId = TryGetInt(comment.GetValue("profile.id"));
            var isOut = profileId is not null && profileId == _userService.CurrentUser?.Id;
            var isContract = string.Equals(
                comment.GetValue("commentable_type")?.ToString(),
                "Contract",
                StringComparison.OrdinalIgnoreCase);

            var row = new Grid
            {
                HorizontalAlignment = ResolveRowAlignment(departmentId),
                MaxWidth = 760
            };

            var bubble = new Border
            {
                Padding = new Thickness(9, 6, 9, 5),
                CornerRadius = new CornerRadius(7),
                Background = ResolveBubbleBrush(isOut, isContract),
                BorderBrush = ResolveBubbleBorderBrush(isContract),
                BorderThickness = isContract ? new Thickness(1) : new Thickness(0)
            };

            var content = new StackPanel
            {
                Spacing = 3
            };

            content.Children.Add(new TextBlock
            {
                Text = comment.GetValue("content")?.ToString() ?? string.Empty,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            });

            var metaText = BuildMetaText(comment, isOut);
            if (!string.IsNullOrWhiteSpace(metaText))
            {
                content.Children.Add(new TextBlock
                {
                    Text = metaText,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["ShellCaptionTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            bubble.Child = content;
            row.Children.Add(bubble);
            return row;
        }

        private static HorizontalAlignment ResolveRowAlignment(int? departmentId)
        {
            return departmentId switch
            {
                CommersDepartmentId => HorizontalAlignment.Left,
                FinDepartmentId => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Right
            };
        }

        private static Brush ResolveBubbleBrush(bool isOut, bool isContract)
        {
            if (isContract)
            {
                return (Brush)Application.Current.Resources["ShellAccentPanelBackgroundBrush"];
            }

            return isOut
                ? new SolidColorBrush(Microsoft.UI.Colors.Honeydew)
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 236, 241));
        }

        private static Brush ResolveBubbleBorderBrush(bool isContract)
        {
            return isContract
                ? (Brush)Application.Current.Resources["ShellAccentMutedBrush"]
                : (Brush)Application.Current.Resources["ShellPanelBorderBrush"];
        }

        private static string BuildMetaText(ReferenceDataRow comment, bool isOut)
        {
            var when = comment.GetValue("when")?.ToString() ?? string.Empty;
            if (isOut)
            {
                return when;
            }

            var department = comment.GetValue("profile.department.name")?.ToString();
            var person = comment.GetValue("person.name")?.ToString()
                ?? comment.GetValue("profile.user.name")?.ToString();
            var author = string.Join(
                ", ",
                new[] { department, person }.Where(static value => !string.IsNullOrWhiteSpace(value)));

            if (string.IsNullOrWhiteSpace(author))
            {
                return when;
            }

            return string.IsNullOrWhiteSpace(when)
                ? author
                : $"{author} | {when}";
        }

        private static int? TryGetInt(object? value)
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                decimal decimalValue => checked((int)decimalValue),
                string text when int.TryParse(text, out var parsedValue) => parsedValue,
                JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
