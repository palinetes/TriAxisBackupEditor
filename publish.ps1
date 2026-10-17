# Gera o executável autônomo para Windows x64 (arquivo único, não exige .NET instalado).
# Uso:  powershell -ExecutionPolicy Bypass -File .\publish.ps1
param(
    [string]$Configuration = "Release",
    [string]$Output = "publish\win-x64"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet restore TriAxisBackupEditor.sln
dotnet test TriAxisBackupEditor.sln -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Testes falharam: publicação cancelada." }

dotnet publish src\TriAxisBackupEditor.App\TriAxisBackupEditor.App.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $Output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

$exe = Join-Path $Output "TriAxisBackupEditor.exe"
Write-Host ""
Write-Host "Pronto: $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"
Write-Host "SHA-256: $((Get-FileHash $exe -Algorithm SHA256).Hash)"
