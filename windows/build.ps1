param(
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$assets = Join-Path $root '..\assets'
$out = Join-Path $root '..\build\windows'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bc = Join-Path $root 'vendor\BouncyCastle.Crypto.dll'
$icon = Join-Path $assets 'linkdrop.ico'
$manifest = Join-Path $root 'app.manifest'

if (-not (Test-Path $csc)) { throw "Cannot find the .NET Framework C# compiler." }
if (-not (Test-Path $bc)) { throw "Missing BouncyCastle.Crypto.dll." }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$files = @(Get-ChildItem $src -Filter '*.cs' | ForEach-Object { $_.FullName })
$argsList = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/langversion:5',
    '/codepage:65001',
    '/optimize+',
    '/debug-',
    ('/win32icon:' + $icon),
    ('/win32manifest:' + $manifest),
    ('/out:' + (Join-Path $out 'LinkDrop.exe')),
    ('/r:' + $bc),
    '/r:System.Web.Extensions.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Drawing.dll',
    '/r:System.Security.dll'
)
$argsList += $files
& $csc @argsList
if ($LASTEXITCODE -ne 0) { throw "Windows build failed (csc exit code $LASTEXITCODE)." }
Copy-Item $bc (Join-Path $out 'BouncyCastle.Crypto.dll') -Force
Copy-Item (Join-Path $root 'vendor\NOTICE.txt') (Join-Path $out 'THIRD-PARTY-NOTICES.txt') -Force
Copy-Item (Join-Path $root 'app.config') (Join-Path $out 'LinkDrop.exe.config') -Force
Write-Output ("Built: " + (Join-Path $out 'LinkDrop.exe'))
