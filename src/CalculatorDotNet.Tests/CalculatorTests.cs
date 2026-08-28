using CalculatorDotNet;

namespace CalculatorDotNet.Tests;

public class CalculatorTests
{
    [Fact]
    public void Addition_UpdatesPrimaryDisplay()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Add, CalculatorCommand.Two, CalculatorCommand.Equals);
        Assert.Equal("3", calculator.PrimaryDisplay);
        Assert.False(calculator.IsInError);
    }

    [Fact]
    public void MultiDigitArithmetic_ComputesCorrectly()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(
            CalculatorCommand.One, CalculatorCommand.Two, CalculatorCommand.Multiply,
            CalculatorCommand.One, CalculatorCommand.Two, CalculatorCommand.Equals);
        Assert.Equal("144", calculator.PrimaryDisplay);
    }

    [Fact]
    public void DivideByZero_SetsErrorState()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Divide, CalculatorCommand.Zero, CalculatorCommand.Equals);
        Assert.True(calculator.IsInError);
        Assert.Equal(new EngineResourceProvider().GetEngineString("99"), calculator.PrimaryDisplay);
    }

    [Fact]
    public void Negate_ShowsNegativeNumber()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.Five, CalculatorCommand.Negate);
        Assert.Equal("-5", calculator.PrimaryDisplay);
    }

    [Fact]
    public void DisplayChanged_EventFires()
    {
        using var calculator = new Calculator();
        var texts = new List<string>();
        calculator.DisplayChanged += (_, e) => texts.Add(e.Text);
        calculator.SendCommands(CalculatorCommand.Four, CalculatorCommand.Two);
        Assert.Contains("42", texts);
    }

    [Fact]
    public void DecimalSeparator_DefaultsToPeriod()
    {
        using var calculator = new Calculator();
        Assert.Equal('.', calculator.DecimalSeparator);
    }

    [Fact]
    public void Reset_RestoresInitialDisplay()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.Seven, CalculatorCommand.Eight);
        calculator.Reset();
        Assert.Equal("0", calculator.PrimaryDisplay);
    }
}

public class ScientificModeTests
{
    [Fact]
    public void SquareRoot_ComputesExactResult()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Scientific;
        calculator.SendCommands(CalculatorCommand.Nine, CalculatorCommand.SquareRoot);
        Assert.Equal("3", calculator.PrimaryDisplay);
    }

    [Fact]
    public void SineOf90Degrees_IsOne()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Scientific;
        Assert.Equal(AngleUnit.Degrees, calculator.AngleUnit);
        calculator.SendCommands(CalculatorCommand.Nine, CalculatorCommand.Zero, CalculatorCommand.Sine);
        Assert.Equal("1", calculator.PrimaryDisplay);
    }

    [Fact]
    public void Factorial_OfFive_Is120()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Scientific;
        calculator.SendCommands(CalculatorCommand.Five, CalculatorCommand.Factorial);
        Assert.Equal("120", calculator.PrimaryDisplay);
    }

    [Fact]
    public void AngleUnit_SwitchesToRadians()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Scientific;
        calculator.SendCommand(CalculatorCommand.Radians);
        Assert.Equal(AngleUnit.Radians, calculator.AngleUnit);
    }
}

public class ProgrammerModeTests
{
    [Fact]
    public void HexRadix_Shows255AsFF()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Programmer;
        calculator.SendCommands(CalculatorCommand.Two, CalculatorCommand.Five, CalculatorCommand.Five, CalculatorCommand.Equals);
        calculator.SetRadix(NumberRadix.Hexadecimal);
        Assert.Equal("FF", calculator.PrimaryDisplay);
    }

    [Fact]
    public void GetResultForRadix_FormatsBinary()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Programmer;
        calculator.SendCommands(CalculatorCommand.Five, CalculatorCommand.Equals);
        Assert.Equal("101", calculator.GetResultForRadix(2, precision: 64));
    }

    [Fact]
    public void HexDigitInput_WorksAfterRadixSwitch()
    {
        using var calculator = new Calculator();
        calculator.Mode = CalculatorMode.Programmer;
        calculator.SetRadix(NumberRadix.Hexadecimal);
        calculator.SendCommands(CalculatorCommand.F, CalculatorCommand.F, CalculatorCommand.Equals);
        calculator.SetRadix(NumberRadix.Decimal);
        Assert.Equal("255", calculator.PrimaryDisplay);
    }
}

public class MemoryTests
{
    [Fact]
    public void MemorizeAndRecall_RoundTrips()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.Four, CalculatorCommand.Two);
        calculator.MemorizeNumber();
        Assert.Contains("42", calculator.MemorizedNumbers);

        calculator.SendCommand(CalculatorCommand.Clear);
        calculator.RecallMemorizedNumber(0);
        Assert.Equal("42", calculator.PrimaryDisplay);
    }

    [Fact]
    public void AddToMemory_Accumulates()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Zero);
        calculator.MemorizeNumber();
        calculator.SendCommands(CalculatorCommand.Clear, CalculatorCommand.Five);
        calculator.AddToMemory(0);
        calculator.RecallMemorizedNumber(0);
        Assert.Equal("15", calculator.PrimaryDisplay);
    }

    [Fact]
    public void ClearMemory_EmptiesSlots()
    {
        using var calculator = new Calculator();
        calculator.SendCommand(CalculatorCommand.Seven);
        calculator.MemorizeNumber();
        calculator.ClearMemory();
        calculator.RefreshMemorizedNumbers();
        Assert.Empty(calculator.MemorizedNumbers);
    }
}

public class HistoryTests
{
    [Fact]
    public void CompletedCalculation_AppearsInHistory()
    {
        using var calculator = new Calculator();
        var addedIndexes = new List<int>();
        calculator.HistoryItemAdded += (_, e) => addedIndexes.Add(e.Index);

        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Add, CalculatorCommand.Two, CalculatorCommand.Equals);

        var history = calculator.GetHistory(HistoryMode.Standard);
        var item = Assert.Single(history);
        Assert.Equal("3", item.Result);
        Assert.Contains("1", item.Expression);
        Assert.Contains("2", item.Expression);
        Assert.Single(addedIndexes);
    }

    [Fact]
    public void ClearHistory_RemovesItems()
    {
        using var calculator = new Calculator();
        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Add, CalculatorCommand.One, CalculatorCommand.Equals);
        calculator.ClearHistory();
        Assert.Empty(calculator.GetHistory(HistoryMode.Standard));
    }

    [Fact]
    public void MaxHistorySize_IsPositive()
    {
        using var calculator = new Calculator();
        Assert.True(calculator.MaxHistorySize > 0);
    }
}

public class ExpressionTests
{
    [Fact]
    public void PendingExpression_ExposesTokens()
    {
        using var calculator = new Calculator();
        var expressionChanged = false;
        calculator.ExpressionChanged += (_, _) => expressionChanged = true;

        calculator.SendCommands(CalculatorCommand.One, CalculatorCommand.Add);

        Assert.True(expressionChanged);
        Assert.NotEmpty(calculator.Expression);
        Assert.Contains(calculator.Expression, token => token.Text.Contains('1'));
    }
}

public class LifetimeTests
{
    [Fact]
    public void MultipleInstances_AreIndependent()
    {
        using var first = new Calculator();
        using var second = new Calculator();
        first.SendCommands(CalculatorCommand.One, CalculatorCommand.Add, CalculatorCommand.One, CalculatorCommand.Equals);
        Assert.Equal("2", first.PrimaryDisplay);
        Assert.Equal("0", second.PrimaryDisplay);
    }

    [Fact]
    public void UseAfterDispose_Throws()
    {
        var calculator = new Calculator();
        calculator.Dispose();
        Assert.Throws<ObjectDisposedException>(() => calculator.SendCommand(CalculatorCommand.One));
    }

    [Fact]
    public void DoubleDispose_IsSafe()
    {
        var calculator = new Calculator();
        calculator.Dispose();
        calculator.Dispose();
    }
}
