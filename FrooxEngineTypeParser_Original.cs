using Elements.Core;
using FrooxEngine.ProtoFlux;
using FrooxEngine;
using System.Reflection;
using System.Text;

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

    // Pass the current category path as an additional argument.
    public static void PrintComp(Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads, string currentCategoryPath)
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
                    + SEPARATOR // Append the category path.
                    + currentCategoryPath // Append the current category path here.
            );
            return;
        }
        _ = builder.AppendLine(
            new string(SEPARATOR, depth + 1)
                + " "
                + element.GetNiceName()
                + SEPARATOR
                + element.FullName
                + SEPARATOR // Append the category path.
                + currentCategoryPath // Append the current category path here.
        );
    }

    public static void ProcessNode(CategoryNode<Type> node, StringBuilder builder, int depth, string parentCategoryPath = "")
    {
        string currentCategoryPath = parentCategoryPath + (parentCategoryPath == "" ? "" : "/") + node.Name; // Build the current category path.
        _ = builder.AppendLine(new string(SEPARATOR, depth) + " " + node.Name + SEPARATOR + currentCategoryPath); // Append the category path to the category line as well.

        foreach (CategoryNode<Type>? subdir in node.Subcategories)
        {
            ProcessNode(subdir, builder, depth + 1, currentCategoryPath);
        }

        HashSet<string> seenOverloads = new();
        foreach (Type element in node.Elements)
        {
            PrintComp(element, builder, depth, seenOverloads, currentCategoryPath);
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
                    catch (BadImageFormatException) { }
                    return null;
                }
            )
            .Where((asm) => asm != null)!;

        Console.WriteLine($"Loaded {asms.Count()} assemblies.");

        List<Type> allTypes = asms.SelectMany(GetLoadableTypes).ToList();

        Console.WriteLine($"Loaded {allTypes.Count} types.");

        WorkerInitializer.Initialize(allTypes, true);

        string outputFolder = (args.Length < 1) ? "../../../data/" : args[0];

        StringBuilder ProtofluxString = new();
        CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux");

        foreach (CategoryNode<Type>? node in ProtofluxPath.Subcategories)
        {
            ProcessNode(node, ProtofluxString, 0);
        }

        HashSet<string> seenOverloads = new();
        foreach (Type? node in ProtofluxPath.Elements)
        {
            PrintComp(node, ProtofluxString, -1, seenOverloads, "ProtoFlux");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputFolder, "ProtoFluxList.txt"),
            ProtofluxString.ToString()
        );
    }
}
