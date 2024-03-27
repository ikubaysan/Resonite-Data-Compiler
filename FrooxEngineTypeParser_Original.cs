using System.Reflection;
using System.Text;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;

internal class Program
{
    // Define a constant character used as a separator in output strings.
    const char SEPARATOR = '#';

    // Method to safely get all loadable types from an assembly, catching and handling any exceptions.
    static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            // Try to get all types from the assembly.
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // Return only the successfully loaded types if there's a load exception.
            return e.Types.Where(t => t != null)!;
        }
    }

    // Print a formatted string of a type's information, including its hierarchy depth and full name.
    public static void PrintComp(Type element, StringBuilder builder, int depth, HashSet<string> seenOverloads)
    {
        // Check if the element is a type of ProtoFluxNode and format its string representation accordingly.
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
        // Format and append the element's string representation to the StringBuilder.
        _ = builder.AppendLine(
            new string(SEPARATOR, depth + 1)
                + " "
                + element.GetNiceName()
                + SEPARATOR
                + element.FullName
        );
    }

    // Recursively processes each node in the category tree, building a string representation.
    public static void ProcessNode(CategoryNode<Type> node, StringBuilder builder, int depth)
    {
        // Append the name of the current node with separators indicating its depth.
        _ = builder.AppendLine(new string(SEPARATOR, depth) + " " + node.Name);
        // Recursively process subcategories to build their string representation.
        foreach (CategoryNode<Type>? subdir in node.Subcategories)
        {
            ProcessNode(subdir, builder, depth + 1);
        }
        // Initialize a HashSet to track seen overloads; required for proper functioning despite not being directly used.
        HashSet<string> seenOverloads = new();
        // Process and print each element within the current node.
        foreach (Type element in node.Elements)
        {
            PrintComp(element, builder, depth, seenOverloads);
        }
    }

    // The entry point of the program which initializes and processes assemblies and their types.
    private static async Task Main(string[] args)
    {
        // Load assemblies from the current directory excluding system assemblies.
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
                    catch (BadImageFormatException) { } // Catch exceptions for non-C# DLLs.
                    return null;
                }
            )
            .Where((asm) => asm != null)!;

        Console.WriteLine($"Loaded {asms.Count()} assemblies.");

        // Aggregate all types from the loaded assemblies.
        List<Type> allTypes = asms.SelectMany(GetLoadableTypes).ToList();

        Console.WriteLine($"Loaded {allTypes.Count} types.");

        // Initialize the worker with the loaded types.
        WorkerInitializer.Initialize(allTypes, true);

        StringBuilder componentsString = new();

        // Determine the output folder based on the provided arguments.
        string outputFolder = (args.Length < 1) ? "../../../data/" : args[0];

        StringBuilder ProtofluxString = new();
        // Get the ProtoFlux category from the component library.
        CategoryNode<Type> ProtofluxPath = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux");

        // Process each subcategory within the ProtoFlux category.
        foreach (CategoryNode<Type>? node in ProtofluxPath.Subcategories)
        {
            ProcessNode(node, ProtofluxString, 0);
        }

        HashSet<string> seenOverloads = new();
        // Process and print components directly under the ProtoFlux category.
        foreach (Type? node in ProtofluxPath.Elements)
        {
            PrintComp(node, ProtofluxString, -1, seenOverloads);
        }

        // Write the constructed ProtoFlux component list to a file.
        await File.WriteAllTextAsync(
            Path.Combine(outputFolder, "ProtoFluxList.txt"),
            ProtofluxString.ToString()
        );
    }
}
