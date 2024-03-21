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

        string json = JsonSerializer.Serialize(protoFluxTypes, new JsonSerializerOptions { WriteIndented = true });

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
}
