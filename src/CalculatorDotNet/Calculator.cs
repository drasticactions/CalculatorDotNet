using System.Runtime.InteropServices;
using CalculatorDotNet.Interop;

namespace CalculatorDotNet;

public sealed unsafe class Calculator : IDisposable
{
    private static readonly object EngineLock = new();

    private readonly IEngineResourceProvider resources;
    private readonly Dictionary<string, nint> resourceStringCache = new(StringComparer.Ordinal);
    private GCHandle gcHandle;
    private nint handle;
    private CalculatorMode mode = CalculatorMode.Standard;

    public Calculator()
        : this(null)
    {
    }

    public Calculator(IEngineResourceProvider? resourceProvider)
    {
        this.resources = resourceProvider ?? new EngineResourceProvider();
        this.gcHandle = GCHandle.Alloc(this);
        try
        {
            var callbacks = new CalcShimCallbacks
            {
                SetPrimaryDisplay = &OnSetPrimaryDisplay,
                SetIsInError = &OnSetIsInError,
                SetExpressionDisplay = &OnSetExpressionDisplay,
                SetParenthesisNumber = &OnSetParenthesisNumber,
                OnNoRightParenAdded = &OnNoRightParenAdded,
                MaxDigitsReached = &OnMaxDigitsReached,
                BinaryOperatorReceived = &OnBinaryOperatorReceived,
                OnHistoryItemAdded = &OnHistoryItemAdded,
                SetMemorizedNumbers = &OnSetMemorizedNumbers,
                MemoryItemChanged = &OnMemoryItemChanged,
                InputChanged = &OnInputChanged,
                GetCEngineString = &OnGetCEngineString,
            };
            lock (EngineLock)
            {
                this.handle = NativeMethods.Create(&callbacks, GCHandle.ToIntPtr(this.gcHandle));
                if (this.handle == 0)
                {
                    throw new InvalidOperationException("Failed to create the native calculator manager.");
                }

                NativeMethods.SetStandardMode(this.handle);
            }
        }
        catch
        {
            this.ReleaseResources();
            throw;
        }
    }

    public event EventHandler<DisplayChangedEventArgs>? DisplayChanged;

    public event EventHandler? IsInErrorChanged;

    public event EventHandler? ExpressionChanged;

    public event EventHandler? MemoryChanged;

    public event EventHandler<HistoryItemAddedEventArgs>? HistoryItemAdded;

    public event EventHandler? ParenthesisCountChanged;

    public event EventHandler? NoRightParenthesisAdded;

    public event EventHandler? MaxDigitsReached;

    public event EventHandler? BinaryOperatorReceived;

    public event EventHandler<MemoryItemChangedEventArgs>? MemoryItemChanged;

    public event EventHandler? InputChanged;

    public string PrimaryDisplay { get; private set; } = "0";

    public bool IsInError { get; private set; }

    public IReadOnlyList<ExpressionToken> Expression { get; private set; } = [];

    public IReadOnlyList<string> MemorizedNumbers { get; private set; } = [];

    public int ParenthesisCount { get; private set; }

    public bool IsEngineRecording { get { lock (EngineLock) { return NativeMethods.IsEngineRecording(this.Handle) != 0; } } }

    public bool IsInputEmpty { get { lock (EngineLock) { return NativeMethods.IsInputEmpty(this.Handle) != 0; } } }

    public char DecimalSeparator { get { lock (EngineLock) { return (char)NativeMethods.DecimalSeparator(this.Handle); } } }

    public AngleUnit AngleUnit
    {
        get
        {
            lock (EngineLock)
            {
                return (CalculatorCommand)NativeMethods.GetCurrentDegreeMode(this.Handle) switch
                {
                    CalculatorCommand.Radians => AngleUnit.Radians,
                    CalculatorCommand.Gradians => AngleUnit.Gradians,
                    _ => AngleUnit.Degrees,
                };
            }
        }
    }

    public int MaxHistorySize { get { lock (EngineLock) { return (int)NativeMethods.MaxHistorySize(this.Handle); } } }

    public CalculatorMode Mode
    {
        get => this.mode;
        set
        {
            lock (EngineLock)
            {
                switch (value)
                {
                    case CalculatorMode.Standard:
                        NativeMethods.SetStandardMode(this.Handle);
                        break;
                    case CalculatorMode.Scientific:
                        NativeMethods.SetScientificMode(this.Handle);
                        break;
                    case CalculatorMode.Programmer:
                        NativeMethods.SetProgrammerMode(this.Handle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value));
                }

                this.mode = value;
            }
        }
    }

    private nint Handle => this.handle != 0 ? this.handle : throw new ObjectDisposedException(nameof(Calculator));

    public void SendCommand(CalculatorCommand command)
    {
        lock (EngineLock)
        {
            NativeMethods.SendCommand(this.Handle, (int)command);
        }
    }

    public void SendCommands(params ReadOnlySpan<CalculatorCommand> commands)
    {
        foreach (var command in commands)
        {
            this.SendCommand(command);
        }
    }

    public void Reset(bool clearMemory = true)
    {
        lock (EngineLock)
        {
            NativeMethods.Reset(this.Handle, clearMemory ? 1 : 0);
            this.mode = CalculatorMode.Standard;
        }
    }

    public void MemorizeNumber()
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizeNumber(this.Handle);
        }
    }

    public void RecallMemorizedNumber(int index)
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizedNumberLoad(this.Handle, checked((uint)index));
        }
    }

    public void AddToMemory(int index)
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizedNumberAdd(this.Handle, checked((uint)index));
        }
    }

    public void SubtractFromMemory(int index)
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizedNumberSubtract(this.Handle, checked((uint)index));
        }
    }

    public void ClearMemorizedNumber(int index)
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizedNumberClear(this.Handle, checked((uint)index));
        }
    }

    public void ClearMemory()
    {
        lock (EngineLock)
        {
            NativeMethods.MemorizedNumberClearAll(this.Handle);
        }
    }

    public void RefreshMemorizedNumbers()
    {
        lock (EngineLock)
        {
            NativeMethods.SetMemorizedNumbersString(this.Handle);
        }
    }

    public void SetRadix(NumberRadix radix)
    {
        lock (EngineLock)
        {
            NativeMethods.SetRadix(this.Handle, (int)radix);
        }
    }

    public string GetResultForRadix(int radix, int precision, bool groupDigitsPerRadix = false)
    {
        lock (EngineLock)
        {
            return NativeMethods.ConsumeString(NativeMethods.GetResultForRadix(this.Handle, checked((uint)radix), precision, groupDigitsPerRadix ? 1 : 0)) ?? string.Empty;
        }
    }

    public void SetPrecision(int precision)
    {
        lock (EngineLock)
        {
            NativeMethods.SetPrecision(this.Handle, precision);
        }
    }

    public void UpdateMaxIntDigits()
    {
        lock (EngineLock)
        {
            NativeMethods.UpdateMaxIntDigits(this.Handle);
        }
    }

    public IReadOnlyList<HistoryItem> GetHistory(HistoryMode historyMode)
    {
        lock (EngineLock)
        {
            return this.GetHistoryCore(historyMode);
        }
    }

    private IReadOnlyList<HistoryItem> GetHistoryCore(HistoryMode historyMode)
    {
        var count = NativeMethods.GetHistoryItemCount(this.Handle, (int)historyMode);
        var items = new List<HistoryItem>(count);
        for (uint i = 0; i < count; i++)
        {
            char* expression;
            char* result;
            if (NativeMethods.GetHistoryItemAt(this.Handle, (int)historyMode, i, &expression, &result) != 0)
            {
                items.Add(new HistoryItem(
                    NativeMethods.ConsumeString(expression) ?? string.Empty,
                    NativeMethods.ConsumeString(result) ?? string.Empty));
            }
        }

        return items;
    }

    public bool RemoveHistoryItem(int index)
    {
        lock (EngineLock)
        {
            return NativeMethods.RemoveHistoryItem(this.Handle, checked((uint)index)) != 0;
        }
    }

    public void ClearHistory()
    {
        lock (EngineLock)
        {
            NativeMethods.ClearHistory(this.Handle);
        }
    }

    public void SetInHistoryItemLoadMode(bool isLoading)
    {
        lock (EngineLock)
        {
            NativeMethods.SetInHistoryItemLoadMode(this.Handle, isLoading ? 1 : 0);
        }
    }

    public void Dispose()
    {
        this.ReleaseResources();
        GC.SuppressFinalize(this);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnSetPrimaryDisplay(nint state, char* text, int isError)
    {
        var self = FromState(state);
        self.PrimaryDisplay = text is null ? string.Empty : new string(text);
        self.DisplayChanged?.Invoke(self, new DisplayChangedEventArgs(self.PrimaryDisplay, isError != 0));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnSetIsInError(nint state, int isInError)
    {
        var self = FromState(state);
        self.IsInError = isInError != 0;
        self.IsInErrorChanged?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnSetExpressionDisplay(nint state, char** tokenStrings, int* tokenIds, int count)
    {
        var self = FromState(state);
        var tokens = new ExpressionToken[count];
        for (var i = 0; i < count; i++)
        {
            tokens[i] = new ExpressionToken(tokenStrings[i] is null ? string.Empty : new string(tokenStrings[i]), tokenIds[i]);
        }

        self.Expression = tokens;
        self.ExpressionChanged?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnSetParenthesisNumber(nint state, uint count)
    {
        var self = FromState(state);
        self.ParenthesisCount = (int)count;
        self.ParenthesisCountChanged?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnNoRightParenAdded(nint state)
    {
        var self = FromState(state);
        self.NoRightParenthesisAdded?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnMaxDigitsReached(nint state)
    {
        var self = FromState(state);
        self.MaxDigitsReached?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnBinaryOperatorReceived(nint state)
    {
        var self = FromState(state);
        self.BinaryOperatorReceived?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnHistoryItemAdded(nint state, uint index)
    {
        var self = FromState(state);
        self.HistoryItemAdded?.Invoke(self, new HistoryItemAddedEventArgs((int)index));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnSetMemorizedNumbers(nint state, char** numbers, int count)
    {
        var self = FromState(state);
        var memorized = new string[count];
        for (var i = 0; i < count; i++)
        {
            memorized[i] = numbers[i] is null ? string.Empty : new string(numbers[i]);
        }

        self.MemorizedNumbers = memorized;
        self.MemoryChanged?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnMemoryItemChanged(nint state, uint index)
    {
        var self = FromState(state);
        self.MemoryItemChanged?.Invoke(self, new MemoryItemChangedEventArgs((int)index));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnInputChanged(nint state)
    {
        var self = FromState(state);
        self.InputChanged?.Invoke(self, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static char* OnGetCEngineString(nint state, char* id)
    {
        var self = FromState(state);
        var key = id is null ? string.Empty : new string(id);
        if (!self.resourceStringCache.TryGetValue(key, out var cached))
        {
            var value = self.resources.GetEngineString(key) ?? string.Empty;
            cached = Marshal.StringToHGlobalUni(value);
            self.resourceStringCache[key] = cached;
        }

        return (char*)cached;
    }

    private static Calculator FromState(nint state) => (Calculator)GCHandle.FromIntPtr(state).Target!;

    private void ReleaseResources()
    {
        if (this.handle != 0)
        {
            lock (EngineLock)
            {
                NativeMethods.Destroy(this.handle);
            }

            this.handle = 0;
        }

        foreach (var allocation in this.resourceStringCache.Values)
        {
            Marshal.FreeHGlobal(allocation);
        }

        this.resourceStringCache.Clear();

        if (this.gcHandle.IsAllocated)
        {
            this.gcHandle.Free();
        }
    }
}
