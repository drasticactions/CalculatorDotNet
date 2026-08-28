using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace CalculatorDotNet;

public class EngineResourceProvider : IEngineResourceProvider
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> EnglishStrings = new(LoadEmbeddedStrings);

    private readonly CultureInfo? culture;

    public EngineResourceProvider()
        : this(null)
    {
    }

    public EngineResourceProvider(CultureInfo? culture)
    {
        this.culture = culture;
    }

    public virtual string GetEngineString(string id)
    {
        switch (id)
        {
            case "sDecimal":
                return this.culture?.NumberFormat.NumberDecimalSeparator ?? ".";
            case "sThousand":
                return this.culture?.NumberFormat.NumberGroupSeparator ?? ",";
            case "sGrouping":
                return this.culture is null ? "3;0" : ToWindowsGroupingString(this.culture.NumberFormat.NumberGroupSizes);
        }

        return EnglishStrings.Value.TryGetValue(id, out var value) ? value : string.Empty;
    }

    private static string ToWindowsGroupingString(int[] groupSizes)
    {
        if (groupSizes.Length == 0)
        {
            return "0";
        }

        var grouping = string.Join(";", groupSizes);
        return groupSizes[^1] == 0 ? grouping : grouping + ";0";
    }

    private static IReadOnlyDictionary<string, string> LoadEmbeddedStrings()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("CalculatorDotNet.CEngineStrings.en-US.resw")
            ?? throw new InvalidOperationException("Embedded engine string resource is missing.");

        var document = XDocument.Load(stream);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in document.Root!.Elements("data"))
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            if (name is not null && value is not null)
            {
                strings[name] = value;
            }
        }

        return strings;
    }
}
