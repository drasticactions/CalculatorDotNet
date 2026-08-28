namespace CalculatorDotNet;

public sealed class ConversionDisplayChangedEventArgs(string fromValue, string toValue) : EventArgs
{
    public string FromValue { get; } = fromValue;

    public string ToValue { get; } = toValue;
}
