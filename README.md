# CalculatorDotNet

CalculatorDotNet is a .NET binding of the [Microsoft Calculator](https://github.com/microsoft/calculator)
calculation engine (`CalcManager`).

## Installing

```sh
dotnet add package CalculatorDotNet
```

## Usage

```csharp
using CalculatorDotNet;

using var calculator = new Calculator();

calculator.SendCommands(
    CalculatorCommand.One,
    CalculatorCommand.Add,
    CalculatorCommand.Two,
    CalculatorCommand.Equals);

Console.WriteLine(calculator.PrimaryDisplay); // "3"
```

```csharp
// Scientific: sin(90°)
calculator.Mode = CalculatorMode.Scientific;
calculator.SendCommands(CalculatorCommand.Nine, CalculatorCommand.Zero, CalculatorCommand.Sine);

// Programmer: 255 in hex
calculator.Mode = CalculatorMode.Programmer;
calculator.SendCommands(CalculatorCommand.Two, CalculatorCommand.Five, CalculatorCommand.Five, CalculatorCommand.Equals);
calculator.SetRadix(NumberRadix.Hexadecimal); // PrimaryDisplay == "FF"
```

### Unit conversion

```csharp
using var converter = new UnitConverter(
[
    new UnitCategoryDefinition(1, "Length", SupportsNegative: false,
    [
        new UnitDefinition(10, "Meters", "m", Factor: 1.0, IsConversionTarget: true),
        new UnitDefinition(11, "Kilometers", "km", Factor: 1000.0, IsConversionSource: true),
    ]),
    new UnitCategoryDefinition(2, "Temperature", SupportsNegative: true,
    [
        new UnitDefinition(20, "Celsius", "°C", IsConversionSource: true),
        new UnitDefinition(21, "Fahrenheit", "°F", IsConversionTarget: true),
    ])
    {
        ExplicitConversions =
        [
            new ExplicitConversion(20, 21, Ratio: 1.8, Offset: 32.0),               // F = C * 1.8 + 32
            new ExplicitConversion(21, 20, Ratio: 1.0 / 1.8, Offset: -32.0, OffsetFirst: true),
        ],
    },
]);

converter.SendCommand(ConverterCommand.One); // 1 km ...
Console.WriteLine(converter.ToValue);        // "1000" (meters)

converter.SetCategory(2);
converter.SendCommands(ConverterCommand.One, ConverterCommand.Zero, ConverterCommand.Zero);
Console.WriteLine(converter.ToValue);        // "212" (°F)
```

Results surface through `FromValue`/`ToValue`, the `DisplayChanged` event, and
`SuggestedValues`.

## Building

```sh
git submodule update --init
dotnet build
dotnet test
```

### iOS and Mac Catalyst apps

```sh
./build/apple/build-dylibs.sh
```

```xml
<ItemGroup>
  <ProjectReference Include="path/to/src/CalculatorDotNet/CalculatorDotNet.csproj" />
</ItemGroup>
<Import Project="path/to/build/apple/CalculatorDotNet.Apple.targets" />
```

## License

The Microsoft Calculator sources are MIT licensed; see `external/calculator/LICENSE`.
