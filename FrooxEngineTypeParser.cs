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
        List<Type> protoFluxTypes = GetProtoFluxTypes();
        Console.WriteLine($"Loaded {protoFluxTypes.Count} ProtoFlux types.");

        List<ProtoFluxTypeInfo> typeInfos = protoFluxTypes.Select(type => new ProtoFluxTypeInfo
        {
            FullName = type.FullName,
            NiceName = type.GetNiceName()
        }).ToList();

        string json = JsonSerializer.Serialize(typeInfos, new JsonSerializerOptions { WriteIndented = true });

        string outputFolder = args.Length > 0 ? args[0] : "../../../data/";
        EnsureDirectoryExists(outputFolder);

        string filePath = Path.Combine(outputFolder, "ProtoFluxTypes.json");
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"ProtoFlux data saved to {filePath}");
    }

    private static List<Type> GetProtoFluxTypes()
    {
        IEnumerable<Assembly> assemblies = Directory
            .GetFiles(Directory.GetCurrentDirectory())
            .Where(file => file.EndsWith(".dll"))
            .Select(Assembly.LoadFrom)
            .ToList();

        List<Type> allTypes = new List<Type>();

        foreach (var assembly in assemblies)
        {
            try
            {
                allTypes.AddRange(assembly.GetTypes().Where(type => typeof(ProtoFluxNode).IsAssignableFrom(type)));
            }
            catch (ReflectionTypeLoadException e)
            {
                var loadableTypes = e.Types.Where(t => t != null && typeof(ProtoFluxNode).IsAssignableFrom(t));
                allTypes.AddRange(loadableTypes);
            }
        }

        return allTypes.ToList();
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
}
