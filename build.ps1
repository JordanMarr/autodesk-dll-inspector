# Build script for AutodeskDllInspector
# Creates a self-contained executable that doesn't require .NET runtime

$ErrorActionPreference = "Stop"

Write-Host "Building AutodeskDllInspector..." -ForegroundColor Cyan

$srcDir = Join-Path $PSScriptRoot "src"
$outDir = Join-Path $PSScriptRoot "publish"

# Clean previous build
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

# Build self-contained single-file executable
dotnet publish $srcDir `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outDir

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output: $outDir\AutodeskDllInspector.exe" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  1. Start Revit"
    Write-Host "  2. Run: AutodeskDllInspector.exe"
    Write-Host "  3. Or with filter: AutodeskDllInspector.exe Newtonsoft"
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
