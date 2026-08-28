using System.Runtime.InteropServices;
using CalculatorDotNet.Interop;

namespace CalculatorDotNet;

public sealed unsafe class UnitConverter : IDisposable
{
    private readonly Dictionary<int, UnitDefinition> unitsById = new();
    private readonly Dictionary<int, UnitCategoryDefinition> categoriesById = new();
    private GCHandle gcHandle;
    private nint handle;

    public UnitConverter(IEnumerable<UnitCategoryDefinition> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        this.Categories = categories.ToArray();
        if (this.Categories.Count == 0)
        {
            throw new ArgumentException("At least one category is required.", nameof(categories));
        }

        foreach (var category in this.Categories)
        {
            if (!this.categoriesById.TryAdd(category.Id, category))
            {
                throw new ArgumentException($"Duplicate category id {category.Id}.", nameof(categories));
            }

            foreach (var unit in category.Units)
            {
                if (!this.unitsById.TryAdd(unit.Id, unit))
                {
                    throw new ArgumentException($"Duplicate unit id {unit.Id}; unit ids must be unique across all categories.", nameof(categories));
                }
            }

            foreach (var conversion in category.ExplicitConversions)
            {
                if (!this.unitsById.ContainsKey(conversion.FromUnitId) || !this.unitsById.ContainsKey(conversion.ToUnitId))
                {
                    throw new ArgumentException($"Explicit conversion {conversion.FromUnitId} -> {conversion.ToUnitId} references an unknown unit.", nameof(categories));
                }
            }
        }

        var builder = UnitNativeMethods.CreateBuilder();
        if (builder == 0)
        {
            throw new InvalidOperationException("Failed to create the native unit converter builder.");
        }

        this.gcHandle = GCHandle.Alloc(this);
        try
        {
            foreach (var category in this.Categories)
            {
                UnitNativeMethods.AddCategory(builder, category.Id, category.Name, category.SupportsNegative ? 1 : 0);
                foreach (var unit in category.Units)
                {
                    UnitNativeMethods.AddUnit(
                        builder,
                        category.Id,
                        unit.Id,
                        unit.Name,
                        unit.Abbreviation,
                        unit.Factor,
                        unit.IsConversionSource ? 1 : 0,
                        unit.IsConversionTarget ? 1 : 0,
                        unit.IsWhimsical ? 1 : 0);
                }

                foreach (var conversion in category.ExplicitConversions)
                {
                    UnitNativeMethods.AddExplicitConversion(
                        builder,
                        conversion.FromUnitId,
                        conversion.ToUnitId,
                        conversion.Ratio,
                        conversion.Offset,
                        conversion.OffsetFirst ? 1 : 0);
                }
            }

            var callbacks = new UnitShimCallbacks
            {
                DisplayCallback = &OnDisplayCallback,
                SuggestedValuesCallback = &OnSuggestedValuesCallback,
                MaxDigitsReached = &OnMaxDigitsReached,
            };

            this.handle = UnitNativeMethods.Create(builder, &callbacks, GCHandle.ToIntPtr(this.gcHandle));
            if (this.handle == 0)
            {
                throw new InvalidOperationException("Failed to create the native unit converter.");
            }

            this.SetCategory(this.Categories[0].Id);
        }
        catch
        {
            this.ReleaseResources();
            throw;
        }
    }

    public event EventHandler<ConversionDisplayChangedEventArgs>? DisplayChanged;

    public event EventHandler? SuggestedValuesChanged;

    public event EventHandler? MaxDigitsReached;

    public IReadOnlyList<UnitCategoryDefinition> Categories { get; }

    public UnitCategoryDefinition CurrentCategory { get; private set; } = null!;

    public UnitDefinition? FromUnit { get; private set; }

    public UnitDefinition? ToUnit { get; private set; }

    public string FromValue { get; private set; } = "0";

    public string ToValue { get; private set; } = "0";

    public IReadOnlyList<SuggestedValue> SuggestedValues { get; private set; } = [];

    public bool IsSwitchedActive => UnitNativeMethods.IsSwitchedActive(this.Handle) != 0;

    private nint Handle => this.handle != 0 ? this.handle : throw new ObjectDisposedException(nameof(UnitConverter));

    public void SetCategory(int categoryId)
    {
        if (!this.categoriesById.TryGetValue(categoryId, out var category))
        {
            throw new ArgumentException($"Unknown category id {categoryId}.", nameof(categoryId));
        }

        if (UnitNativeMethods.SetCurrentCategory(this.Handle, categoryId, out var fromUnitId, out var toUnitId) == 0)
        {
            throw new InvalidOperationException($"The native converter rejected category {categoryId}.");
        }

        this.CurrentCategory = category;
        this.FromUnit = this.unitsById.GetValueOrDefault(fromUnitId);
        this.ToUnit = this.unitsById.GetValueOrDefault(toUnitId);
    }

    public void SetUnits(int fromUnitId, int toUnitId)
    {
        if (!this.unitsById.TryGetValue(fromUnitId, out var fromUnit))
        {
            throw new ArgumentException($"Unknown unit id {fromUnitId}.", nameof(fromUnitId));
        }

        if (!this.unitsById.TryGetValue(toUnitId, out var toUnit))
        {
            throw new ArgumentException($"Unknown unit id {toUnitId}.", nameof(toUnitId));
        }

        if (UnitNativeMethods.SetCurrentUnitTypes(this.Handle, fromUnitId, toUnitId) == 0)
        {
            throw new InvalidOperationException($"The native converter rejected units {fromUnitId} -> {toUnitId}.");
        }

        this.FromUnit = fromUnit;
        this.ToUnit = toUnit;
    }

    public void SendCommand(ConverterCommand command) => UnitNativeMethods.SendCommand(this.Handle, (int)command);

    public void SendCommands(params ReadOnlySpan<ConverterCommand> commands)
    {
        foreach (var command in commands)
        {
            this.SendCommand(command);
        }
    }

    public void SwitchActive()
    {
        var newInput = this.ToValue;
        UnitNativeMethods.SwitchActive(this.Handle, newInput);
        (this.FromUnit, this.ToUnit) = (this.ToUnit, this.FromUnit);
        (this.FromValue, this.ToValue) = (this.ToValue, this.FromValue);
    }

    public void Calculate() => UnitNativeMethods.Calculate(this.Handle);

    public string SaveUserPreferences()
        => NativeMethods.ConsumeString(UnitNativeMethods.SaveUserPreferences(this.Handle)) ?? string.Empty;

    public void RestoreUserPreferences(string preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        UnitNativeMethods.RestoreUserPreferences(this.Handle, preferences);
        this.CurrentCategory = this.categoriesById.GetValueOrDefault(UnitNativeMethods.GetCurrentCategory(this.Handle)) ?? this.CurrentCategory;
    }

    public void Dispose()
    {
        this.ReleaseResources();
        GC.SuppressFinalize(this);
    }

    [UnmanagedCallersOnly]
    private static void OnDisplayCallback(nint state, char* from, char* to)
    {
        var self = FromState(state);
        self.FromValue = from is null ? string.Empty : new string(from);
        self.ToValue = to is null ? string.Empty : new string(to);
        self.DisplayChanged?.Invoke(self, new ConversionDisplayChangedEventArgs(self.FromValue, self.ToValue));
    }

    [UnmanagedCallersOnly]
    private static void OnSuggestedValuesCallback(nint state, char** values, int* unitIds, int count)
    {
        var self = FromState(state);
        var suggested = new List<SuggestedValue>(count);
        for (var i = 0; i < count; i++)
        {
            if (self.unitsById.TryGetValue(unitIds[i], out var unit))
            {
                suggested.Add(new SuggestedValue(values[i] is null ? string.Empty : new string(values[i]), unit));
            }
        }

        self.SuggestedValues = suggested;
        self.SuggestedValuesChanged?.Invoke(self, EventArgs.Empty);
    }

    [UnmanagedCallersOnly]
    private static void OnMaxDigitsReached(nint state)
    {
        var self = FromState(state);
        self.MaxDigitsReached?.Invoke(self, EventArgs.Empty);
    }

    private static UnitConverter FromState(nint state) => (UnitConverter)GCHandle.FromIntPtr(state).Target!;

    private void ReleaseResources()
    {
        if (this.handle != 0)
        {
            UnitNativeMethods.Destroy(this.handle);
            this.handle = 0;
        }

        if (this.gcHandle.IsAllocated)
        {
            this.gcHandle.Free();
        }
    }
}
