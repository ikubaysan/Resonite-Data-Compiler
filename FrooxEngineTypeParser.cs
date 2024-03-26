using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

internal class Program
{
    private static async Task Main(string[] args)
    {
        IEnumerable<ProtoFluxTypeInfo> protoFluxTypes = GetProtoFluxTypesWithCategories();
        int protoFluxTypesCount = protoFluxTypes.Count();
        Console.WriteLine($"Loaded {protoFluxTypesCount} ProtoFlux types.");

        // Add support for serialization of complex types such as List<string>
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

        string json = JsonSerializer.Serialize(protoFluxTypes, options);

        string outputFolder = args.Length > 0 ? args[0] : "../../../data/";
        EnsureDirectoryExists(outputFolder);

        string filePath = Path.Combine(outputFolder, "ProtoFluxTypes.json");
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"ProtoFlux data saved to {filePath}");
    }

    private static IEnumerable<ProtoFluxTypeInfo> GetProtoFluxTypesWithCategories()
    {
        IEnumerable<Assembly> assemblies = Directory
            .GetFiles(Directory.GetCurrentDirectory())
            .Where(file => file.EndsWith(".dll"))
            .Select(Assembly.LoadFrom);

        var protoFluxTypes = new List<ProtoFluxTypeInfo>();

        foreach (var assembly in assemblies)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes().Where(type => typeof(ProtoFluxNode).IsAssignableFrom(type) && type.Namespace.StartsWith("FrooxEngine.ProtoFlux."));
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null && typeof(ProtoFluxNode).IsAssignableFrom(t) && t.Namespace.StartsWith("FrooxEngine.ProtoFlux."));
            }

            protoFluxTypes.AddRange(types.Select(type => new ProtoFluxTypeInfo
            {
                FullName = type.FullName,
                NiceName = type.GetNiceName(),
                FullCategory = type.Namespace,
                NiceCategory = type.Namespace?.Replace("FrooxEngine.ProtoFlux.", "")
            }));
        }

        return protoFluxTypes;
    }

    private static void EnsureDirectoryExists(string path)
    {
        string directoryName = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
    }
}

public class ProtoFluxTypeInfo
{
    public string FullName { get; set; }
    public string NiceName { get; set; }
    public string FullCategory { get; set; }
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

