namespace CalculatorDotNet;

public sealed record UnitCategoryDefinition(int Id, string Name, bool SupportsNegative, IReadOnlyList<UnitDefinition> Units)
{
    public IReadOnlyList<ExplicitConversion> ExplicitConversions { get; init; } = [];
}
