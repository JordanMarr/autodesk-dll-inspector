@echo off
echo Building AutodeskDllInspector...
echo.

dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo.
    echo Output: publish\AutodeskDllInspector.exe
    echo.
    echo Usage:
    echo   1. Start Revit
    echo   2. Run: AutodeskDllInspector.exe
    echo   3. Or with filter: AutodeskDllInspector.exe Newtonsoft
) else (
    echo Build failed!
    exit /b 1
)
