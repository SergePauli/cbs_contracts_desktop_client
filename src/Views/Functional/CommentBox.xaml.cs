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
            var items = (Comments ?? [])
                .Where(static comment => comment is not null && !comment.IsPlaceholder)
                .Select(BuildCommentItem)
                .ToList();
            CommentsListView.ItemsSource = items;
            EmptyTextBlock.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CommentsListView.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private CommentBoxItem BuildCommentItem(TableDataRow comment)
        {
            var departmentId = TryGetInt(comment.GetValue("profile.department.id"));
            var profileId = TryGetInt(comment.GetValue("profile.id"));
            var isOut = profileId is not null && profileId == _userService.CurrentUser?.Id;
            var isContract = string.Equals(
                comment.GetValue("commentable_type")?.ToString(),
                "Contract",
                StringComparison.OrdinalIgnoreCase);
            var meta = BuildMeta(comment, isOut);
            return new CommentBoxItem(
                comment.GetValue("content")?.ToString() ?? string.Empty,
                meta.Author,
                meta.When,
                ResolveRowAlignment(departmentId),
                ResolveBubbleBrush(isOut, isContract),
                ResolveBubbleBorderBrush(isContract),
                isContract ? new Thickness(1) : new Thickness(0));
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
            return new CommentMeta(author, when);
        }

        private sealed record CommentMeta(string Author, string When);
    }

    public sealed record CommentBoxItem(
        string Text,
        string Author,
        string When,
        HorizontalAlignment RowAlignment,
        Brush Background,
        Brush BorderBrush,
        Thickness BorderThickness)
    {
        public Visibility MetaVisibility =>
            string.IsNullOrWhiteSpace(Author) && string.IsNullOrWhiteSpace(When)
                ? Visibility.Collapsed
                : Visibility.Visible;

        public Visibility AuthorVisibility =>
            string.IsNullOrWhiteSpace(Author) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility SeparatorVisibility =>
            !string.IsNullOrWhiteSpace(Author) && !string.IsNullOrWhiteSpace(When)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility WhenVisibility =>
            string.IsNullOrWhiteSpace(When) ? Visibility.Collapsed : Visibility.Visible;
    }
}
