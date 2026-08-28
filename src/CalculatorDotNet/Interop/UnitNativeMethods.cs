using System.Runtime.InteropServices;

namespace CalculatorDotNet.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnitShimCallbacks
{
    public delegate* unmanaged<nint, char*, char*, void> DisplayCallback;
    public delegate* unmanaged<nint, char**, int*, int, void> SuggestedValuesCallback;
    public delegate* unmanaged<nint, void> MaxDigitsReached;
}

internal static unsafe partial class UnitNativeMethods
{
    private const string LibraryName = "CalcManagerShim";

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_CreateBuilder")]
    public static partial nint CreateBuilder();

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_DestroyBuilder")]
    public static partial void DestroyBuilder(nint builder);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_AddCategory", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void AddCategory(nint builder, int id, string name, int supportsNegative);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_AddUnit", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void AddUnit(
        nint builder,
        int categoryId,
        int unitId,
        string name,
        string abbreviation,
        double factor,
        int isConversionSource,
        int isConversionTarget,
        int isWhimsical);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_AddExplicitConversion")]
    public static partial void AddExplicitConversion(nint builder, int fromUnitId, int toUnitId, double ratio, double offset, int offsetFirst);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_Create")]
    public static partial nint Create(nint builder, UnitShimCallbacks* callbacks, nint state);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_Destroy")]
    public static partial void Destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_SetCurrentCategory")]
    public static partial int SetCurrentCategory(nint handle, int categoryId, out int fromUnitId, out int toUnitId);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_GetCurrentCategory")]
    public static partial int GetCurrentCategory(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_SetCurrentUnitTypes")]
    public static partial int SetCurrentUnitTypes(nint handle, int fromUnitId, int toUnitId);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_SwitchActive", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void SwitchActive(nint handle, string newValue);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_IsSwitchedActive")]
    public static partial int IsSwitchedActive(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_SendCommand")]
    public static partial void SendCommand(nint handle, int command);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_Calculate")]
    public static partial void Calculate(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_SaveUserPreferences")]
    public static partial char* SaveUserPreferences(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_RestoreUserPreferences", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void RestoreUserPreferences(nint handle, string preferences);

    [LibraryImport(LibraryName, EntryPoint = "UnitShim_ResetCategoriesAndRatios")]
    public static partial void ResetCategoriesAndRatios(nint handle);
}
