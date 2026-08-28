namespace CalculatorDotNet;

public sealed record UnitDefinition(
    int Id,
    string Name,
    string Abbreviation,
    double Factor = 1.0,
    bool IsConversionSource = false,
    bool IsConversionTarget = false,
    bool IsWhimsical = false);
