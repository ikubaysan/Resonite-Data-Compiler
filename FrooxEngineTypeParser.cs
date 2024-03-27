using Elements.Core;
using FrooxEngine.ProtoFlux;
using FrooxEngine;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal class Program
{
    const int MAX_PARAMETERS = 1;
    const char SEPARATOR = '#';

    static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null)!;
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        string directoryName = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
    }

    // Pass the current category path as an additional argument.
    public static void PrintComp(List<ProtoFluxTypeInfo> protoFluxTypeInfoList, Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads, string currentCategoryPath)
    {
        // Adjust currentCategoryPath if it starts with "Runtimes/Execution/" - remove this prefix.
        string adjustedCategoryPath = currentCategoryPath.StartsWith("Runtimes/Execution/") ? currentCategoryPath.Substring("Runtimes/Execution/".Length) : currentCategoryPath;
        ProtoFluxTypeInfo protoFluxTypeInfo;

        if (typeof(ProtoFluxNode).IsAssignableFrom(element))
        {
            Type toPrint = element;
            _ = builder.AppendLine(
                new string(SEPARATOR, depth + 1)
                    + " "
                    + toPrint.GetNiceName()
                    + SEPARATOR
                    + toPrint.FullName
                    + SEPARATOR // Append the category path.
                    + adjustedCategoryPath // Append the current category path here.
            );

            protoFluxTypeInfo = new ProtoFluxTypeInfo
            {
                FullName = toPrint.FullName,
                NiceName = toPrint.GetNiceName(),
                NiceCategory = adjustedCategoryPath
            };

            if (protoFluxTypeInfo.ParameterCount > MAX_PARAMETERS) return;
            protoFluxTypeInfoList.Add(protoFluxTypeInfo);

            return;
        }
        _ = builder.AppendLine(
            new string(SEPARATOR, depth + 1)
                + " "
                + element.GetNiceName()
                + SEPARATOR
                + element.FullName
                + SEPARATOR // Append the category path.
                + adjustedCategoryPath // Append the current category path here.
        );

        protoFluxTypeInfo = new ProtoFluxTypeInfo
        {
            FullName = element.FullName,
            NiceName = element.GetNiceName(),
            NiceCategory = adjustedCategoryPath
        };

        if (protoFluxTypeInfo.ParameterCount > MAX_PARAMETERS) return;
        protoFluxTypeInfoList.Add(protoFluxTypeInfo);

    }

    public static void ProcessNode(List<ProtoFluxTypeInfo> protoFluxTypeInfoList, CategoryNode<Type> node, StringBuilder builder, int depth, string parentCategoryPath = "")
    {
        string currentCategoryPath = parentCategoryPath + (parentCategoryPath == "" ? "" : "/") + node.Name; // Build the current category path.
        _ = builder.AppendLine(new string(SEPARATOR, depth) + " " + node.Name + SEPARATOR + currentCategoryPath); // Append the category path to the category line as well.

        foreach (CategoryNode<Type>? subdir in node.Subcategories)
        {
            ProcessNode(protoFluxTypeInfoList, subdir, builder, depth + 1, currentCategoryPath);
        }

        HashSet<string> seenOverloads = new();
        foreach (Type element in node.Elements)
        
            PrintComp(protoFluxTypeInfoList, element, builder, depth, seenOverloads, currentCategoryPath);
    }

    private static async Task Main(string[] args)
    {
        List<ProtoFluxTypeInfo> protoFluxTypeInfoList = new List<ProtoFluxTypeInfo>();

        IEnumerable<Assembly> asms = Directory
            .GetFiles(Directory.GetCurrentDirectory())
            .Where((s) => s.EndsWith(".dll") && !s.StartsWith("System"))
            .Select(
                (s) =>
                {
                    try
                    {
                        return Assembly.LoadFrom(s);
                    }
                    catch (BadImageFormatException) { }
                    return null;
                }
            )
            .Where((asm) => asm != null)!;

        Console.WriteLine($"Loaded {asms.Count()} assemblies.");

        List<Type> allTypes = asms.SelectMany(GetLoadableTypes).ToList();

        Console.WriteLine($"Loaded {allTypes.Count} types.");

        WorkerInitializer.Initialize(allTypes, true);

        StringBuilder ProtofluxString = new();
        CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux");

        foreach (CategoryNode<Type>? node in ProtofluxPath.Subcategories)
        {
            ProcessNode(protoFluxTypeInfoList, node, ProtofluxString, 0);
        }

        HashSet<string> seenOverloads = new();
        foreach (Type? node in ProtofluxPath.Elements)
        {
            PrintComp(protoFluxTypeInfoList, node, ProtofluxString, -1, seenOverloads, "ProtoFlux");
        }

        int protoFluxTypesCount = protoFluxTypeInfoList.Count();
        Console.WriteLine($"Loaded {protoFluxTypesCount} ProtoFlux types.");

        // Add support for serialization of complex types such as List<string>
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

        string json = JsonSerializer.Serialize(protoFluxTypeInfoList, options);

        string outputFolder = args.Length > 0 ? args[0] : "../../../data/";
        EnsureDirectoryExists(outputFolder);

        string filePath = Path.Combine(outputFolder, "ProtoFluxTypes_new.json");
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"ProtoFlux data saved to {filePath}");
    }
}



public class ProtoFluxTypeInfo
{
    public string FullName { get; set; }
    public string NiceName { get; set; }
    public string NiceCategory { get; set; }
    public int ParameterCount => GetParameterCount();
    public List<string> WordsOfNiceName => GetWordsOfNiceName();

    private int GetParameterCount()
    {
        // Extract the number after the ` character if present
        var match = System.Text.RegularExpressions.Regex.Match(FullName, @"`\d+");
        if (match.Success)
        {
            return int.Parse(match.Value.Substring(1)); // skip the ` character and parse the number
        }
        return 0; // If no ` character is found, return 0
    }

    private List<string> GetWordsOfNiceName()
    {
        var cleanName = NiceName.Split('<')[0]; // Remove generic type indicators

        // Define special cases with unique placeholders
        var specialCases = new Dictionary<string, string>
    {
        { "NaN", "%%NaN%%" },
        { "OwO", "%%OwO%%" }
    };

        // Temporarily replace special cases in the cleanName with placeholders
        foreach (var kvp in specialCases)
        {
            cleanName = cleanName.Replace(kvp.Key, kvp.Value);
        }

        // Adjust the pattern to explicitly separate 'bool' and similar keywords followed by numbers, and ensure they're treated as distinct words
        var pattern = @"
        (%%[^%]+%%)             # Matches placeholders for special cases
        |(\bbool\b)(?=_|\d)     # Matches 'bool' as a whole word only when followed by an underscore or digit
        |([A-Z][a-z]+)          # Matches words starting with uppercase letter followed by lowercase letters
        |([A-Z]+(?![a-z]))      # Matches uppercase acronyms or sequences of uppercase letters
        |(\d+)                  # Matches sequences of digits
        |(_+)                   # Matches underscores (to be removed)
        |([A-Z]?[a-z]+)         # Matches any lowercase words that may not start with an uppercase
        |([A-Z])                # Matches single uppercase letters
    ";

        var words = System.Text.RegularExpressions.Regex.Matches(cleanName, pattern, System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Value.Replace("_", "")) // Remove underscores
                    .ToList();

        // Replace placeholders in the list with their original special cases
        for (int i = 0; i < words.Count; i++)
        {
            foreach (var kvp in specialCases)
            {
                words[i] = words[i].Replace(kvp.Value, kvp.Key);
            }
        }

        return words.Where(word => !string.IsNullOrEmpty(word)).ToList();
    }
}