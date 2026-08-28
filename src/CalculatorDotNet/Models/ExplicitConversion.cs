namespace CalculatorDotNet;

public sealed record ExplicitConversion(int FromUnitId, int ToUnitId, double Ratio, double Offset = 0.0, bool OffsetFirst = false);
