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
        var cleanName = NiceName.Split('<')[0]; // Initial clean-up to remove generic type indicators.

        // Define special cases directly with unique placeholders to ensure they are easily identifiable and uncommon enough to not accidentally match actual content.
        var specialCases = new Dictionary<string, string>
    {
        { "NaN", "%%NaN%%" },
        { "OwO", "%%OwO%%" }
    };

        // Replace special cases in the cleanName with their placeholders.
        foreach (var specialCase in specialCases)
        {
            cleanName = cleanName.Replace(specialCase.Key, specialCase.Value);
        }

        // Enhanced Regex pattern to accurately split the cleanName into words, considering placeholders as separate tokens.
        var pattern = @"
        (%%[^%]+%%)            # Matches placeholders for special cases
        |([A-Z][a-z]+)         # Matches words starting with an uppercase letter followed by lowercase letters
        |([A-Z]+(?![a-z]))     # Matches sequences of uppercase letters (acronyms)
        |(\d+)                 # Matches sequences of digits
        |(_+)                  # Matches underscores (to be removed)
    ";

        var words = System.Text.RegularExpressions.Regex.Matches(cleanName, pattern, System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace)
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Value.Replace("_", "")) // Removing underscores from matches.
                    .ToList();

        // Process each word to replace any placeholders with their original values (the special cases).
        for (int i = 0; i < words.Count; i++)
        {
            foreach (var specialCase in specialCases)
            {
                // Replace the placeholder with the original special case value.
                words[i] = words[i].Replace(specialCase.Value, specialCase.Key);
            }
        }

        // Filter out any empty entries that may have been introduced during processing.
        return words.Where(word => !string.IsNullOrEmpty(word)).ToList();
    }







}

