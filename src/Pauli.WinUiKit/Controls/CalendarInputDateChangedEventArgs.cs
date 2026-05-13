namespace Pauli.WinUiKit.Controls;

public sealed class CalendarInputDateChangedEventArgs(DateTimeOffset? oldDate, DateTimeOffset? newDate) : EventArgs
{
    public DateTimeOffset? OldDate { get; } = oldDate;

    public DateTimeOffset? NewDate { get; } = newDate;
}
