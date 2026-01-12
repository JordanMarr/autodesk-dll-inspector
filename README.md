# Autodesk DLL Inspector

A standalone diagnostic tool that inspects loaded .NET assemblies in a running Revit or AutoCAD process. Useful for troubleshooting DLL version conflicts between add-ins.

## Supported Applications

- Revit (2025+)
- AutoCAD (2025+)
- Civil 3D (2025+)

## Requirements

- Windows x64
- .NET 8 SDK (for building only - the output .exe is self-contained)

## Building

Run either:

```powershell
.\build.ps1
```

or:

```cmd
build.cmd
```

This creates a self-contained `publish\AutodeskDllInspector.exe` (~70MB) that requires no runtime installation.

## Usage

1. **Start Revit or AutoCAD** (and let add-ins load)
2. **Run the tool:**

```cmd
AutodeskDllInspector.exe
```

If both applications are running, you'll be prompted to choose which one to inspect.

This lists all loaded assemblies with their versions and file locations.

### Search/Filter

Search for specific assemblies:

```cmd
AutodeskDllInspector.exe Newtonsoft
AutodeskDllInspector.exe System.Text.Json
AutodeskDllInspector.exe MyCompany
```

## Output Example

```
===========================================
  Autodesk DLL Inspector
  Inspects loaded assemblies in Revit/AutoCAD
===========================================

Multiple applications detected:
  [1] Revit
  [2] AutoCAD / Civil 3D

Select application (1 or 2): 1

Found Revit (PID: 12345)

CLR Version: 8.0.0

Assembly Name                                                Version              Location
--------------------------------------------------------------------------------------------------------------------------------------------
Autodesk.Revit.DB.Architecture                               25.0.0.0             %ProgramFiles%\Autodesk\Revit 2025\...
Newtonsoft.Json                                              13.0.0.0             %ProgramData%\Autodesk\Revit\Addins\2025\SomeAddin\...
...

Total: 245 assemblies

Press any key to exit...
```

## Common Conflict Assemblies

The tool highlights (in yellow) assemblies that commonly cause version conflicts:

- Newtonsoft.Json
- System.Text.Json
- System.Memory
- RestSharp
- NLog / log4net / Serilog
- And others...

## Troubleshooting

**"Access Denied" or fails to attach:**
- Run as Administrator
- Check if antivirus is blocking process inspection

**"No supported application is running":**
- Make sure Revit or AutoCAD is fully started (past the splash screen)

## How It Works

Uses [Microsoft.Diagnostics.Runtime (ClrMD)](https://github.com/microsoft/clrmd) to attach to the process in read-only mode and enumerate loaded assemblies. This is non-invasive and doesn't affect the application's operation.
