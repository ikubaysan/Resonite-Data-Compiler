using Elements.Core;
using FrooxEngine.ProtoFlux;
using FrooxEngine;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal class Program
{
    private const int MAX_PARAMETERS = 1;

    private static async Task Main(string[] args)
    {
        ProtoFluxTypeProcessor processor = new ProtoFluxTypeProcessor();
        processor.ProcessAssemblies();
        await processor.SaveProtoFluxTypeInfo(args.Length > 0 ? args[0] : "../../../data/");
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
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

    private class ProtoFluxTypeProcessor
    {
        private readonly List<ProtoFluxTypeInfo> protoFluxTypeInfoList = new();

        public void ProcessAssemblies()
        {
            IEnumerable<Assembly> assemblies = LoadAssemblies();
            Console.WriteLine($"Loaded {assemblies.Count()} assemblies.");

            List<Type> allTypes = assemblies.SelectMany(GetLoadableTypes).ToList();
            Console.WriteLine($"Loaded {allTypes.Count} types.");

            WorkerInitializer.Initialize(allTypes, true);

            ProcessProtoFluxTypes();
        }

        private IEnumerable<Assembly> LoadAssemblies()
        {
            return Directory
                .GetFiles(Directory.GetCurrentDirectory(), "*.dll")
                .Where(s => !s.StartsWith("System"))
                .Select(s =>
                {
                    try
                    {
                        return Assembly.LoadFrom(s);
                    }
                    catch (BadImageFormatException)
                    {
                        return null;
                    }
                })
                .Where(asm => asm != null)!;
        }

        private void ProcessProtoFluxTypes()
        {
            StringBuilder ProtofluxString = new StringBuilder();
            CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux");

            foreach (CategoryNode<Type> node in ProtofluxPath.Subcategories)
            {
                ProcessNode(node, ProtofluxString, 0);
            }

            HashSet<string> seenOverloads = new HashSet<string>();
            foreach (Type node in ProtofluxPath.Elements)
            {
                AddProtoFluxTypeInfo(node, ProtofluxString, -1, seenOverloads, "ProtoFlux");
            }

            Console.WriteLine($"Loaded {protoFluxTypeInfoList.Count} ProtoFlux types.");
        }

        private void ProcessNode(CategoryNode<Type> node, StringBuilder builder, int depth, string parentCategoryPath = "")
        {
            string currentCategoryPath = $"{parentCategoryPath}{(parentCategoryPath == "" ? "" : "/")}{node.Name}";

            foreach (CategoryNode<Type> subdir in node.Subcategories)
            {
                ProcessNode(subdir, builder, depth + 1, currentCategoryPath);
            }

            HashSet<string> seenOverloads = new HashSet<string>();
            foreach (Type element in node.Elements)
            {
                AddProtoFluxTypeInfo(element, builder, depth, seenOverloads, currentCategoryPath);
            }
        }

        private void AddProtoFluxTypeInfo(Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads, string currentCategoryPath)
        {
            string adjustedCategoryPath = currentCategoryPath.StartsWith("Runtimes/Execution/") ? currentCategoryPath.Substring("Runtimes/Execution/".Length) : currentCategoryPath;
            ProtoFluxTypeInfo protoFluxTypeInfo = CreateProtoFluxTypeInfo(element, adjustedCategoryPath);

            if (protoFluxTypeInfo.ParameterCount > MAX_PARAMETERS) return;
            protoFluxTypeInfoList.Add(protoFluxTypeInfo);
        }

        private ProtoFluxTypeInfo CreateProtoFluxTypeInfo(Type element, string adjustedCategoryPath)
        {
            return new ProtoFluxTypeInfo
            {
                FullName = element.FullName,
                NiceName = element.GetNiceName(),
                NiceCategory = adjustedCategoryPath
            };
        }

        public async Task SaveProtoFluxTypeInfo(string outputFolder)
        {
            EnsureDirectoryExists(outputFolder);

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(protoFluxTypeInfoList, options);

            string filePath = Path.Combine(outputFolder, "ProtoFluxTypes_new.json");
            await File.WriteAllTextAsync(filePath, json);

            Console.WriteLine($"ProtoFlux data saved to {filePath}");
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
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