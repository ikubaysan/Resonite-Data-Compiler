using Elements.Core;
using FrooxEngine.ProtoFlux;
using FrooxEngine;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal class Program
{

    private static async Task Main(string[] args)
    {
        ProtoFluxTypeProcessor processor = new ProtoFluxTypeProcessor();
        processor.ProcessAssemblies();
        // Save processed ProtoFlux type information to a JSON file.
        // The output folder path is taken from command line arguments or defaults to "../../../data/".
        await processor.SaveProtoFluxTypeInfo(args.Length > 0 ? args[0] : "../../../data/");
    }
}

public class ProtoFluxTypeProcessor
{
    private const int MAX_PARAMETERS = 1; // Max number of parameters to consider a type for processing.
    private readonly List<ProtoFluxTypeInfo> protoFluxTypeInfoList = new(); // List to hold processed type information.

    // Retrieves loadable types from an assembly, handling exceptions gracefully.
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            // Attempt to get all types from the assembly.
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // Return only the types that were successfully loaded, excluding nulls.
            return e.Types.Where(t => t != null)!;
        }
    }

    // Main method for processing assemblies to find ProtoFlux types.
    public void ProcessAssemblies()
    {
        // Load non-system assemblies from the current directory.
        IEnumerable<Assembly> assemblies = LoadAssemblies();
        Console.WriteLine($"Loaded {assemblies.Count()} assemblies.");

        // Extract all loadable types from the loaded assemblies.
        List<Type> allTypes = assemblies.SelectMany(GetLoadableTypes).ToList();
        Console.WriteLine($"Loaded {allTypes.Count} types.");

        // Perform initial setup or processing with the identified types, if required.
        WorkerInitializer.Initialize(allTypes, true);

        // Process identified types to extract ProtoFlux type information.
        ProcessProtoFluxTypes();
    }

    // Loads assemblies from the current directory, excluding system assemblies.
    private IEnumerable<Assembly> LoadAssemblies()
    {
        return Directory
            .GetFiles(Directory.GetCurrentDirectory(), "*.dll")
            .Where(s => !s.StartsWith("System"))
            .Select(s =>
            {
                try
                {
                    // Attempt to load the assembly from file.
                    return Assembly.LoadFrom(s);
                }
                catch (BadImageFormatException)
                {
                    // Ignore files that are not valid assemblies.
                    return null;
                }
            })
            .Where(asm => asm != null)!; // Exclude null values from the result.
    }

    // Processes identified types to collect ProtoFlux type information.
    private void ProcessProtoFluxTypes()
    {
        // StringBuilder to accumulate some form of string output, if necessary.
        StringBuilder ProtofluxString = new StringBuilder();

        // Retrieve the top-level ProtoFlux category node to start processing.
        CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux");

        // Process each subcategory node recursively.
        foreach (CategoryNode<Type> node in ProtofluxPath.Subcategories)
        {
            ProcessNode(node, ProtofluxString, 0);
        }

        // HashSet to track seen overloads and avoid duplicates, if necessary.
        HashSet<string> seenOverloads = new HashSet<string>();

        // Process each type in the top-level ProtoFlux category.
        foreach (Type node in ProtofluxPath.Elements)
        {
            AddProtoFluxTypeInfo(node, ProtofluxString, -1, seenOverloads, "ProtoFlux");
        }

        Console.WriteLine($"Loaded {protoFluxTypeInfoList.Count} ProtoFlux types.");
    }

    // Recursively processes nodes representing categories of ProtoFlux types.
    private void ProcessNode(CategoryNode<Type> node, StringBuilder builder, int depth, string parentCategoryPath = "")
    {
        // Construct the current category path for nested categories.
        string currentCategoryPath = $"{parentCategoryPath}{(parentCategoryPath == "" ? "" : "/")}{node.Name}";

        // Recursively process subcategories.
        foreach (CategoryNode<Type> subdir in node.Subcategories)
        {
            ProcessNode(subdir, builder, depth + 1, currentCategoryPath);
        }

        // Process each type in the current category node.
        HashSet<string> seenOverloads = new HashSet<string>();
        foreach (Type element in node.Elements)
        {
            AddProtoFluxTypeInfo(element, builder, depth, seenOverloads, currentCategoryPath);
        }
    }

    // Adds information about a specific type to the list if it matches criteria for ProtoFlux types.
    private void AddProtoFluxTypeInfo(Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads, string currentCategoryPath)
    {
        // Adjust the category path if it starts with a specific prefix, indicating it's part of the runtime execution path.
        string adjustedCategoryPath = currentCategoryPath.StartsWith("Runtimes/Execution/Nodes/") ? currentCategoryPath.Substring("Runtimes/Execution/".Length) : currentCategoryPath;

        // Create a new ProtoFluxTypeInfo object for the element with adjusted category path.
        ProtoFluxTypeInfo protoFluxTypeInfo = CreateProtoFluxTypeInfo(element, adjustedCategoryPath);

        // If the type exceeds the maximum parameter count, it's skipped.
        if (protoFluxTypeInfo.ParameterCount > MAX_PARAMETERS) return;

        // Add the type info object to the list for later serialization.
        protoFluxTypeInfoList.Add(protoFluxTypeInfo);
    }

    // Creates a ProtoFluxTypeInfo object from a Type, including its fully qualified name, a "nice" name, and its category.
    private ProtoFluxTypeInfo CreateProtoFluxTypeInfo(Type element, string adjustedCategoryPath)
    {
        return new ProtoFluxTypeInfo
        {
            FullName = element.FullName,
            NiceName = element.GetNiceName(), // Assuming GetNiceName is an extension method or implemented elsewhere.
            NiceCategory = adjustedCategoryPath
        };
    }


    // Serializes the collected ProtoFlux type information into a JSON file and writes it to the specified output folder.
    public async Task SaveProtoFluxTypeInfo(string outputFolder)
    {
        // Ensure the output directory exists, creating it if necessary.
        EnsureDirectoryExists(outputFolder);

        // Serialization options for pretty printing.
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

        // Serialize the list of ProtoFluxTypeInfo objects to JSON.
        string json = JsonSerializer.Serialize(protoFluxTypeInfoList, options);

        // Determine the full path for the output JSON file.
        string filePath = Path.Combine(outputFolder, "ProtoFluxTypes.json");

        // Write the serialized JSON to the file, asynchronously.
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"ProtoFlux data saved to {filePath}");
    }

    // Ensures that a specified directory exists, creating it if it does not.
    private void EnsureDirectoryExists(string path)
    {
        // Path.GetDirectoryName might return null for root paths or paths that are not fully qualified.
        string? directoryName = Path.GetDirectoryName(path);
        if (directoryName != null && !Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
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