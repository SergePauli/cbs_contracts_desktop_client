using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;
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
                typeof(IReadOnlyList<TableDataRow>),
                typeof(CommentBox),
                new PropertyMetadata(Array.Empty<TableDataRow>(), OnCommentsChanged));

        public CommentBox()
        {
            _userService = App.Services.GetRequiredService<IUserService>();
            InitializeComponent();
            Render();
        }

        public IReadOnlyList<TableDataRow> Comments
        {
            get => (IReadOnlyList<TableDataRow>)GetValue(CommentsProperty);
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

        private FrameworkElement BuildCommentRow(TableDataRow comment)
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
                Padding = new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(7),
                Background = ResolveBubbleBrush(isOut, isContract),
                BorderBrush = ResolveBubbleBorderBrush(isContract),
                BorderThickness = isContract ? new Thickness(1) : new Thickness(0)
            };

            var content = new StackPanel
            {
                Spacing = 0
            };

            content.Children.Add(new TextBlock
            {
                Text = comment.GetValue("content")?.ToString() ?? string.Empty,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(0),
                IsTextSelectionEnabled = true
            });

            var meta = BuildMeta(comment, isOut);
            if (!string.IsNullOrWhiteSpace(meta.Author) || !string.IsNullOrWhiteSpace(meta.When))
            {
                content.Children.Add(BuildMetaPanel(meta));
            }

            bubble.Child = content;
            row.Children.Add(bubble);
            return row;
        }

        private static FrameworkElement BuildMetaPanel(CommentMeta meta)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4
            };

            if (!string.IsNullOrWhiteSpace(meta.Author))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = meta.Author,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (!string.IsNullOrWhiteSpace(meta.Author) && !string.IsNullOrWhiteSpace(meta.When))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "|",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["ShellCaptionTextBrush"]
                });
            }

            if (!string.IsNullOrWhiteSpace(meta.When))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = meta.When,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["ShellCaptionTextBrush"],
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            return panel;
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

        private static CommentMeta BuildMeta(TableDataRow comment, bool isOut)
        {
            var when = comment.GetValue("when")?.ToString() ?? string.Empty;
            if (isOut)
            {
                return new CommentMeta(string.Empty, when);
            }

            var department = comment.GetValue("profile.department.name")?.ToString();
            var person = comment.GetValue("person.name")?.ToString()
                ?? comment.GetValue("profile.user.name")?.ToString();
            var author = string.Join(
                ", ",
                new[] { department, person }.Where(static value => !string.IsNullOrWhiteSpace(value)));

            if (string.IsNullOrWhiteSpace(author))
            {
                return new CommentMeta(string.Empty, when);
            }

            return new CommentMeta(author, when);
        }

        private sealed record CommentMeta(string Author, string When);
    }
}
