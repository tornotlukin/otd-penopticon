<#
Builds the OpenTabletDriver reference assemblies this plugin compiles against.

Penopticon switches the driver's live settings through IDriverDaemon, which lives
in OpenTabletDriver.Desktop. That assembly is not published to NuGet and the official
release ships it inside single-file executables, so it is built here from the matching
source tag. The plugin references it with Private=false: nothing from lib/ ships in the
release; at runtime the plugin binds to the copy already loaded by the daemon.

Usage:  pwsh tools/build-otd-reference.ps1 [-Tag v0.6.7]
#>
param([string]$Tag = 'v0.6.7')

$ErrorActionPreference = 'Stop'
$root   = Split-Path -Parent $PSScriptRoot
$src    = Join-Path $root ".otd-src\$Tag"
$libDir = Join-Path $root "lib\$($Tag.TrimStart('v'))"

if (-not (Test-Path $src)) {
    git clone --depth 1 --branch $Tag https://github.com/OpenTabletDriver/OpenTabletDriver.git $src
    if ($LASTEXITCODE -ne 0) { throw "git clone failed for $Tag" }
}

dotnet build (Join-Path $src 'OpenTabletDriver.Desktop\OpenTabletDriver.Desktop.csproj') -c Release -nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "OpenTabletDriver.Desktop build failed" }

$out = Get-ChildItem (Join-Path $src 'OpenTabletDriver.Desktop\bin\Release') -Directory | Select-Object -First 1
New-Item -ItemType Directory -Path $libDir -Force | Out-Null
foreach ($name in 'OpenTabletDriver.Desktop.dll', 'OpenTabletDriver.dll', 'OpenTabletDriver.Configurations.dll') {
    Copy-Item (Join-Path $out.FullName $name) $libDir -Force
}

Get-ChildItem $libDir | ForEach-Object {
    "{0}  {1}" -f $_.Name, [Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version
}
