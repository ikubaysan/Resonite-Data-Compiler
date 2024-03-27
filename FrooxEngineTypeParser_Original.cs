using System.Reflection;
using System.Text;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

internal class Program
{

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


    public static void PrintComp(Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads)
    {
        if (typeof(ProtoFluxNode).IsAssignableFrom(element))
        {
            Type toPrint = element;
            _ = builder.AppendLine(
                new string(SEPARATOR, depth + 1)
                    + " "
                    + toPrint.GetNiceName()
                    + SEPARATOR
                    + toPrint.FullName
            );
            return;
        }
        _ = builder.AppendLine(
            new string(SEPARATOR, depth + 1)
                + " "
                + element.GetNiceName()
                + SEPARATOR
                + element.FullName
        );
    }

    public static void ProcessNode(CategoryNode<Type> node, StringBuilder builder, int depth)
    {
        _ = builder.AppendLine(new string(SEPARATOR, depth) + " " + node.Name);
        foreach (CategoryNode<Type>? subdir in node.Subcategories)
        {
            ProcessNode(subdir, builder, depth + 1);
        }
        // the line below is only useful in logix mode
        // but commenting it out will cause the PrintComp line to error since it requires that variable
        HashSet<string> seenOverloads = new();
        foreach (Type element in node.Elements)
        {
            PrintComp(element, builder, depth, seenOverloads);
        }
    }


    private static async Task Main(string[] args)
    {
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
                    // For non C# dlls in the managed folder
                    catch (BadImageFormatException) { }
                    return null;
                }
            )
            .Where((asm) => asm != null)!;

        Console.WriteLine($"Loaded {asms.Count()} assemblies.");

        List<Type> allTypes = asms.SelectMany(GetLoadableTypes).ToList();

        Console.WriteLine($"Loaded {allTypes.Count} types.");

        WorkerInitializer.Initialize(allTypes, true);

        StringBuilder componentsString = new();

        string outputFolder = (args.Length < 1) ? "../../../data/" : args[0];


        StringBuilder ProtofluxString = new();
        CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory(
            "ProtoFlux"
        );


        foreach (CategoryNode<Type>? node in ProtofluxPath.Subcategories)
        {
            ProcessNode(node, ProtofluxString, 0);
        }

        HashSet<string> seenOverloads = new();
        foreach (Type? node in ProtofluxPath.Elements)
        {
            PrintComp(node, ProtofluxString, -1, seenOverloads);
        }

        // Writes our ProtoFlux list to a file.
        await File.WriteAllTextAsync(
            Path.Combine(outputFolder, "ProtoFluxList.txt"),
            ProtofluxString.ToString()
        );
    }
}