using System.Runtime.InteropServices;

namespace CalculatorDotNet.Interop;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct CalcShimCallbacks
{
    public delegate* unmanaged<nint, char*, int, void> SetPrimaryDisplay;
    public delegate* unmanaged<nint, int, void> SetIsInError;
    public delegate* unmanaged<nint, char**, int*, int, void> SetExpressionDisplay;
    public delegate* unmanaged<nint, uint, void> SetParenthesisNumber;
    public delegate* unmanaged<nint, void> OnNoRightParenAdded;
    public delegate* unmanaged<nint, void> MaxDigitsReached;
    public delegate* unmanaged<nint, void> BinaryOperatorReceived;
    public delegate* unmanaged<nint, uint, void> OnHistoryItemAdded;
    public delegate* unmanaged<nint, char**, int, void> SetMemorizedNumbers;
    public delegate* unmanaged<nint, uint, void> MemoryItemChanged;
    public delegate* unmanaged<nint, void> InputChanged;
    public delegate* unmanaged<nint, char*, char*> GetCEngineString;
}

internal static unsafe partial class NativeMethods
{
    private const string LibraryName = "CalcManagerShim";

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_Create")]
    public static partial nint Create(CalcShimCallbacks* callbacks, nint state);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_Destroy")]
    public static partial void Destroy(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_FreeString")]
    public static partial void FreeString(char* str);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_Reset")]
    public static partial void Reset(nint handle, int clearMemory);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetStandardMode")]
    public static partial void SetStandardMode(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetScientificMode")]
    public static partial void SetScientificMode(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetProgrammerMode")]
    public static partial void SetProgrammerMode(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SendCommand")]
    public static partial void SendCommand(nint handle, int command);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizeNumber")]
    public static partial void MemorizeNumber(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizedNumberLoad")]
    public static partial void MemorizedNumberLoad(nint handle, uint index);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizedNumberAdd")]
    public static partial void MemorizedNumberAdd(nint handle, uint index);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizedNumberSubtract")]
    public static partial void MemorizedNumberSubtract(nint handle, uint index);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizedNumberClear")]
    public static partial void MemorizedNumberClear(nint handle, uint index);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MemorizedNumberClearAll")]
    public static partial void MemorizedNumberClearAll(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_IsEngineRecording")]
    public static partial int IsEngineRecording(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_IsInputEmpty")]
    public static partial int IsInputEmpty(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetRadix")]
    public static partial void SetRadix(nint handle, int radixType);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetMemorizedNumbersString")]
    public static partial void SetMemorizedNumbersString(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_GetResultForRadix")]
    public static partial char* GetResultForRadix(nint handle, uint radix, int precision, int groupDigitsPerRadix);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetPrecision")]
    public static partial void SetPrecision(nint handle, int precision);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_UpdateMaxIntDigits")]
    public static partial void UpdateMaxIntDigits(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_DecimalSeparator")]
    public static partial ushort DecimalSeparator(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_GetHistoryItemCount")]
    public static partial int GetHistoryItemCount(nint handle, int mode);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_GetHistoryItemAt")]
    public static partial int GetHistoryItemAt(nint handle, int mode, uint index, char** expression, char** result);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_RemoveHistoryItem")]
    public static partial int RemoveHistoryItem(nint handle, uint index);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_ClearHistory")]
    public static partial void ClearHistory(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_MaxHistorySize")]
    public static partial ulong MaxHistorySize(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_GetCurrentDegreeMode")]
    public static partial int GetCurrentDegreeMode(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "CalcShim_SetInHistoryItemLoadMode")]
    public static partial void SetInHistoryItemLoadMode(nint handle, int isHistoryItemLoadMode);

    public static string? ConsumeString(char* str)
    {
        if (str == null)
        {
            return null;
        }
        try
        {
            return new string(str);
        }
        finally
        {
            FreeString(str);
        }
    }
}
