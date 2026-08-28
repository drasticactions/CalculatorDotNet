namespace CalculatorDotNet;

public sealed class HistoryItemAddedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}
