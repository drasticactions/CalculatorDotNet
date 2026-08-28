namespace CalculatorDotNet;

public sealed class DisplayChangedEventArgs(string text, bool isError) : EventArgs
{
    public string Text { get; } = text;

    public bool IsError { get; } = isError;
}
