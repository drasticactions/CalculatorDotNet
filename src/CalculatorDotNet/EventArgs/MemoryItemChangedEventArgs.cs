namespace CalculatorDotNet;

public sealed class MemoryItemChangedEventArgs(int index) : EventArgs
{
    public int Index { get; } = index;
}
