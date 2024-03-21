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
        // Remove generic type indicators
        var cleanName = NiceName.Split('<')[0];

        // Split by capital letters and underscores, then filter out any empty strings
        var words = System.Text.RegularExpressions.Regex.Matches(cleanName, @"([A-Z][a-z0-9]+)|([A-Z]+(?![a-z]))|(_+)|(\d+)")
                            .Cast<System.Text.RegularExpressions.Match>()
                            .Select(m => m.Value.Replace("_", ""))
                            .Where(word => !string.IsNullOrEmpty(word)) // Exclude empty strings
                            .ToList();

        // Handle special cases, such as GUID being kept as a single word
        // Additional special cases can be added as needed
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].All(char.IsDigit))
            {
                continue; // Skip pure numeric parts
            }
            if (i > 0 && words[i].All(char.IsUpper) && words[i - 1].All(char.IsLetter))
            {
                words[i - 1] += words[i]; // Merge acronyms with the preceding word
                words.RemoveAt(i);
                i--;
            }
        }
        return words;
    }

}

