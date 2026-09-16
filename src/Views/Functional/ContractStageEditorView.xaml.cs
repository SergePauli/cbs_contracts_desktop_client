using Microsoft.UI.Xaml.Controls;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class ContractStageEditorView : UserControl
{
    public ContractStageEditorView() => InitializeComponent();
    public CalendarInput Start => StartEditor;
    public Dropdown TaskKind => TaskKindEditor;
    public Dropdown Status => StatusEditor;
    public Dropdown DeadlineKind => DeadlineKindEditor;
    public TextBox Duration => DurationEditor;
    public CalendarInput Deadline => DeadlineEditor;
    public TextBox Cost => CostEditor;
    public CalendarInput Funded => FundedEditor;
    public Dropdown PaymentKind => PaymentKindEditor;
    public TextBox PaymentDuration => PaymentDurationEditor;
    public CalendarInput PaymentDeadline => PaymentDeadlineEditor;
    public MultiSelect Tasks => TasksEditor;
    public TextBox RideOut => RideOutEditor;
    public TextBox Completed => CompletedEditor;
    public TextBox Sended => SendedEditor;
    public TextBox Comment => CommentEditor;
    public Button AddStage => AddStageButton;
    public Button DeleteStage => DeleteStageButton;
}
