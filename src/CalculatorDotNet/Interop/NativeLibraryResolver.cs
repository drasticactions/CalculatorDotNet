using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CalculatorDotNet.Interop;

internal static class NativeLibraryResolver
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2255:The ModuleInitializer attribute should not be used in libraries",
        Justification = "Registers this assembly's own DllImportResolver exactly once, before any P/Invoke.")]
    [ModuleInitializer]
    internal static void Initialize() =>
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);

    private static nint Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "CalcManagerShim"
            || !(OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsTvOS()))
        {
            return 0;
        }

        if (NativeLibrary.TryLoad("@rpath/libCalcManagerShim.dylib", out var handle))
        {
            return handle;
        }

        foreach (var candidate in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Frameworks", "libCalcManagerShim.dylib"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Frameworks", "libCalcManagerShim.dylib")),
        })
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
            {
                return handle;
            }
        }

        return 0;
    }
}
