using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace AutodeskDllInspector;

class Program
{
    static readonly (string Name, string ProcessName)[] SupportedApps =
    [
        ("Revit", "Revit"),
        ("AutoCAD", "acad"),
        ("Civil 3D", "acad"),  // Civil 3D uses acad.exe too
    ];

    static int Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("  Autodesk DLL Inspector");
        Console.WriteLine("  Inspects loaded assemblies in Revit/AutoCAD");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Parse arguments for search filter
        string? searchFilter = null;
        if (args.Length > 0)
        {
            searchFilter = string.Join(" ", args);
            Console.WriteLine($"Filter: \"{searchFilter}\"");
            Console.WriteLine();
        }

        // Find running Autodesk processes
        var revitProcesses = Process.GetProcessesByName("Revit");
        var acadProcesses = Process.GetProcessesByName("acad");

        var hasRevit = revitProcesses.Length > 0;
        var hasAcad = acadProcesses.Length > 0;

        if (!hasRevit && !hasAcad)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: No supported application is running.");
            Console.WriteLine("Please start Revit or AutoCAD and try again.");
            Console.ResetColor();
            WaitForKeyPress();
            return 1;
        }

        Process targetProcess;
        string appName;

        if (hasRevit && hasAcad)
        {
            // Both running - prompt user to choose
            Console.WriteLine("Multiple applications detected:");
            Console.WriteLine("  [1] Revit");
            Console.WriteLine("  [2] AutoCAD / Civil 3D");
            Console.WriteLine();
            Console.Write("Select application (1 or 2): ");

            var key = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            if (key.KeyChar == '1')
            {
                targetProcess = revitProcesses[0];
                appName = "Revit";
            }
            else if (key.KeyChar == '2')
            {
                targetProcess = acadProcesses[0];
                appName = "AutoCAD";
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid selection.");
                Console.ResetColor();
                WaitForKeyPress();
                return 1;
            }
        }
        else if (hasRevit)
        {
            targetProcess = revitProcesses[0];
            appName = "Revit";

            if (revitProcesses.Length > 1)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: Multiple Revit instances found ({revitProcesses.Length}).");
                Console.WriteLine("Attaching to the first one...");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
        else
        {
            targetProcess = acadProcesses[0];
            appName = "AutoCAD";

            if (acadProcesses.Length > 1)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: Multiple AutoCAD instances found ({acadProcesses.Length}).");
                Console.WriteLine("Attaching to the first one...");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Found {appName} (PID: {targetProcess.Id})");
        Console.WriteLine();

        try
        {
            using var dataTarget = DataTarget.AttachToProcess(targetProcess.Id, suspend: false);

            var clrVersion = dataTarget.ClrVersions.FirstOrDefault();
            if (clrVersion == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Could not find CLR in {appName} process.");
                Console.ResetColor();
                WaitForKeyPress();
                return 1;
            }

            Console.WriteLine($"CLR Version: {clrVersion.Version}");
            Console.WriteLine();

            using var runtime = clrVersion.CreateRuntime();

            var assemblies = runtime.AppDomains
                .SelectMany(ad => ad.Modules)
                .Where(m => m.AssemblyName != null)
                .Select(m => new AssemblyInfo
                {
                    Name = m.AssemblyName ?? "(unknown)",
                    Address = m.Address,
                    FileName = m.Name ?? "(unknown)"
                })
                .DistinctBy(a => a.Name)
                .OrderBy(a => a.Name)
                .ToList();

            // Apply filter if specified
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                assemblies = assemblies
                    .Where(a => a.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                a.FileName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            Console.WriteLine($"{"Assembly Name",-60} {"Version",-20} Location");
            Console.WriteLine(new string('-', 140));

            foreach (var asm in assemblies)
            {
                var (name, version) = ParseAssemblyName(asm.Name);
                var location = GetShortPath(asm.FileName);

                // Highlight common conflict candidates
                var color = IsCommonConflictAssembly(name) ? ConsoleColor.Yellow : ConsoleColor.Gray;

                Console.ForegroundColor = color;
                Console.WriteLine($"{name,-60} {version,-20} {location}");
            }

            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Total: {assemblies.Count} assemblies");

            if (string.IsNullOrWhiteSpace(searchFilter))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("TIP: Run with a filter to search, e.g.:");
                Console.WriteLine("  AutodeskDllInspector Newtonsoft");
                Console.WriteLine("  AutodeskDllInspector System.Text.Json");
                Console.ResetColor();
            }

            WaitForKeyPress();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Failed to attach to process.");
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("This may happen if:");
            Console.WriteLine("  - You need to run as Administrator");
            Console.WriteLine("  - The application is running as a different user");
            Console.WriteLine("  - AntiVirus is blocking process inspection");
            Console.ResetColor();
            WaitForKeyPress();
            return 1;
        }
    }

    static (string Name, string Version) ParseAssemblyName(string fullName)
    {
        // Parse "AssemblyName, Version=1.0.0.0, Culture=neutral, PublicKeyToken=..."
        var parts = fullName.Split(',');
        var name = parts[0].Trim();
        var version = "unknown";

        foreach (var part in parts.Skip(1))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Version=", StringComparison.OrdinalIgnoreCase))
            {
                version = trimmed.Substring(8);
                break;
            }
        }

        return (name, version);
    }

    static string GetShortPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "(in-memory)";

        // Shorten common paths for readability
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (path.StartsWith(programData, StringComparison.OrdinalIgnoreCase))
            return "%ProgramData%" + path.Substring(programData.Length);
        if (path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            return "%AppData%" + path.Substring(appData.Length);
        if (path.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
            return "%ProgramFiles%" + path.Substring(programFiles.Length);

        return path;
    }

    static bool IsCommonConflictAssembly(string name)
    {
        // Highlight assemblies that commonly cause conflicts between add-ins
        var conflictProne = new[]
        {
            "Newtonsoft.Json",
            "System.Text.Json",
            "System.Memory",
            "System.Buffers",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Threading.Tasks.Extensions",
            "Microsoft.Bcl.AsyncInterfaces",
            "System.ValueTuple",
            "System.Numerics.Vectors",
            "RestSharp",
            "NLog",
            "log4net",
            "Serilog",
            "Autofac",
            "Dapper",
            "CsvHelper"
        };

        return conflictProne.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    static void WaitForKeyPress()
    {
        Console.WriteLine();
        Console.Write("Press any key to exit...");
        Console.ReadKey(intercept: true);
    }
}

record AssemblyInfo
{
    public required string Name { get; init; }
    public required ulong Address { get; init; }
    public required string FileName { get; init; }
}
