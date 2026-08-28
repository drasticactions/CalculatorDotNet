using CalculatorDotNet;

namespace CalculatorDotNet.Tests;

public class UnitConverterTests
{
    private const int LengthCategoryId = 1;
    private const int TemperatureCategoryId = 2;
    private const int MeterId = 10;
    private const int KilometerId = 11;
    private const int CentimeterId = 12;
    private const int CelsiusId = 20;
    private const int FahrenheitId = 21;

    private static UnitConverter CreateConverter() => new(
    [
        new UnitCategoryDefinition(LengthCategoryId, "Length", SupportsNegative: false,
        [
            new UnitDefinition(MeterId, "Meters", "m", Factor: 1.0, IsConversionTarget: true),
            new UnitDefinition(KilometerId, "Kilometers", "km", Factor: 1000.0, IsConversionSource: true),
            new UnitDefinition(CentimeterId, "Centimeters", "cm", Factor: 0.01),
        ]),
        new UnitCategoryDefinition(TemperatureCategoryId, "Temperature", SupportsNegative: true,
        [
            new UnitDefinition(CelsiusId, "Celsius", "°C", IsConversionSource: true),
            new UnitDefinition(FahrenheitId, "Fahrenheit", "°F", IsConversionTarget: true),
        ])
        {
            ExplicitConversions =
            [
                new ExplicitConversion(CelsiusId, FahrenheitId, Ratio: 1.8, Offset: 32.0),
                new ExplicitConversion(FahrenheitId, CelsiusId, Ratio: 1.0 / 1.8, Offset: -32.0, OffsetFirst: true),
            ],
        },
    ]);

    [Fact]
    public void DefaultSelection_UsesFlaggedUnits()
    {
        using var converter = CreateConverter();
        Assert.Equal(LengthCategoryId, converter.CurrentCategory.Id);
        Assert.Equal(KilometerId, converter.FromUnit?.Id);
        Assert.Equal(MeterId, converter.ToUnit?.Id);
    }

    [Fact]
    public void FactorBasedConversion_KilometersToMeters()
    {
        using var converter = CreateConverter();
        converter.SendCommand(ConverterCommand.One);
        Assert.Equal("1", converter.FromValue);
        Assert.Equal("1000", converter.ToValue);
    }

    [Fact]
    public void DecimalInput_ConvertsFractions()
    {
        using var converter = CreateConverter();
        converter.SendCommands(ConverterCommand.Two, ConverterCommand.Decimal, ConverterCommand.Five);
        Assert.Equal("2500", converter.ToValue);
    }

    [Fact]
    public void SetUnits_ChangesConversionDirection()
    {
        using var converter = CreateConverter();
        converter.SetUnits(MeterId, CentimeterId);
        converter.SendCommand(ConverterCommand.Three);
        Assert.Equal("300", converter.ToValue);
    }

    [Fact]
    public void ExplicitConversion_CelsiusToFahrenheit()
    {
        using var converter = CreateConverter();
        converter.SetCategory(TemperatureCategoryId);
        Assert.Equal(CelsiusId, converter.FromUnit?.Id);
        Assert.Equal(FahrenheitId, converter.ToUnit?.Id);

        converter.SendCommands(ConverterCommand.One, ConverterCommand.Zero, ConverterCommand.Zero);
        Assert.Equal("212", converter.ToValue);
    }

    [Fact]
    public void ExplicitConversion_FahrenheitToCelsius()
    {
        using var converter = CreateConverter();
        converter.SetCategory(TemperatureCategoryId);
        converter.SetUnits(FahrenheitId, CelsiusId);
        converter.SendCommands(ConverterCommand.Three, ConverterCommand.Two);
        Assert.Equal("0", converter.ToValue);
    }

    [Fact]
    public void NegativeInput_SupportedForTemperature()
    {
        using var converter = CreateConverter();
        converter.SetCategory(TemperatureCategoryId);
        converter.SendCommands(ConverterCommand.Four, ConverterCommand.Zero, ConverterCommand.Negate);
        Assert.Equal("-40", converter.FromValue);
        Assert.Equal("-40", converter.ToValue);
    }

    [Fact]
    public void DisplayChanged_EventFires()
    {
        using var converter = CreateConverter();
        var events = new List<(string From, string To)>();
        converter.DisplayChanged += (_, e) => events.Add((e.FromValue, e.ToValue));
        converter.SendCommand(ConverterCommand.Five);
        Assert.Contains(("5", "5000"), events);
    }

    [Fact]
    public void SuggestedValues_ReportOtherUnits()
    {
        using var converter = CreateConverter();
        converter.SendCommand(ConverterCommand.One);
        Assert.NotEmpty(converter.SuggestedValues);
        Assert.All(converter.SuggestedValues, suggestion => Assert.NotEqual(KilometerId, suggestion.Unit.Id));
    }

    [Fact]
    public void SwitchActive_SwapsUnitsAndValues()
    {
        using var converter = CreateConverter();
        converter.SendCommand(ConverterCommand.One);
        converter.SwitchActive();
        Assert.Equal(MeterId, converter.FromUnit?.Id);
        Assert.Equal(KilometerId, converter.ToUnit?.Id);
        Assert.Equal("1000", converter.FromValue);
    }

    [Fact]
    public void Backspace_RemovesLastDigit()
    {
        using var converter = CreateConverter();
        converter.SendCommands(ConverterCommand.One, ConverterCommand.Two, ConverterCommand.Backspace);
        Assert.Equal("1", converter.FromValue);
        Assert.Equal("1000", converter.ToValue);
    }

    [Fact]
    public void Clear_ResetsValues()
    {
        using var converter = CreateConverter();
        converter.SendCommands(ConverterCommand.Seven, ConverterCommand.Clear);
        Assert.Equal("0", converter.FromValue);
    }

    [Fact]
    public void UserPreferences_RoundTrip()
    {
        using var converter = CreateConverter();
        converter.SetUnits(MeterId, CentimeterId);
        var preferences = converter.SaveUserPreferences();
        Assert.False(string.IsNullOrEmpty(preferences));

        using var restored = CreateConverter();
        restored.RestoreUserPreferences(preferences);
        Assert.Equal(LengthCategoryId, restored.CurrentCategory.Id);
    }

    [Fact]
    public void DuplicateUnitIds_Throw()
    {
        Assert.Throws<ArgumentException>(() => new UnitConverter(
        [
            new UnitCategoryDefinition(1, "A", true, [new UnitDefinition(1, "x", "x"), new UnitDefinition(1, "y", "y")]),
        ]));
    }

    [Fact]
    public void UnknownCategory_Throws()
    {
        using var converter = CreateConverter();
        Assert.Throws<ArgumentException>(() => converter.SetCategory(999));
    }

    [Fact]
    public void EmptyCategories_Throw()
    {
        Assert.Throws<ArgumentException>(() => new UnitConverter([]));
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var converter = CreateConverter();
        converter.Dispose();
        Assert.Throws<ObjectDisposedException>(() => converter.SendCommand(ConverterCommand.One));
    }
}
