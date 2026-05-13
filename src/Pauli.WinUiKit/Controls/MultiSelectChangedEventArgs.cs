namespace Pauli.WinUiKit.Controls;

public sealed class MultiSelectChangedEventArgs(IReadOnlyList<object> value) : EventArgs
{
    public IReadOnlyList<object> Value { get; } = value;
}
