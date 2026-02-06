<#
.SYNOPSIS
	Package MMA-EoS

.PARAMETER Path
	Output path.

.PARAMETER Framework
	Target .NET framework for the build ('net8.0', or 'net462').

.PARAMETER Configuration
	Build configuration ('Release' or 'Debug').

.PARAMETER Version
	Override version number (use with care).

.PARAMETER Runtime
	Override runtime identifier (use with care).

.PARAMETER Parallel
	Use multithreaded compression, if possible.
#>

[CmdletBinding(PositionalBinding = $false)]
param (
	[Parameter(Position = 0, Mandatory = $false)]
	[string]$Path,
	[ValidateSet('net8.0', 'net462')]
	[string]$Framework = 'net8.0',
	[ValidateSet('Release', 'Debug')]
	[string]$Configuration = 'Release',
	[string]$Version,
	[string]$Runtime,
	[int]$Parallel = 1
)

$ErrorActionPreference = 'Stop'
try {

if (-not $Runtime) {
	$Runtime = switch ([Environment]::OSVersion.Platform) {
		'Unix' {
			[Environment]::Is64BitOperatingSystem ? 'linux-x64' : 'linux-x86'
		}
		'MacOSX' {
			'osx-x64'
		}
		default {
			[Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
		}
	}
}

if ($Runtime -like 'win*') {
	$EoSExe = 'eos.exe'
	if ($Framework -like 'net?.?') {
		$Framework1 = "$Framework-windows"
	}
}
else {
	$EoSExe = 'eos'
	$Framework1 = $Framework
}

$PublishDoc = Join-Path $PSScriptRoot doc $Configuration | Get-Item -ErrorAction Ignore
$PublishBin = Join-Path $PSScriptRoot EoS.CommandLineTool bin $Configuration $Framework1 $Runtime publish

if (-not $Version) {
	$Version = (Join-Path $PublishBin eos.dll | Get-Item).VersionInfo.FileVersion
}

if (-not $Path) {
	$Path = Join-Path bin "EoS-$Version.$Runtime.tar.zst"
}

Write-Host "Packaging $Configuration MMA-EoS $Version for $Runtime into $Path" -ForegroundColor Blue

$Archive = switch -Wildcard (Split-Path $Path -Leaf) {
	'*.tar.zst' { '.tar.zst' }
	'*.tar.bz2' { '.tar.bz2' }
	'*.tar.xz'  { '.tar.xz' }
	'*.tar.gz'  { '.tar.gz' }
	'*.tar.Z'   { '.tar.Z' }
	'*.tar'     { '.tar' }
	default     { $null }
}

$Stage = if ($Archive) {
	New-Item ([IO.Path]::ChangeExtension($Path, '.d')) -Type Directory
}
else {
	New-Item $Path -Type Directory -ErrorAction Ignore >$null
	Get-Item $Path
}

$Root = New-Item (Join-Path $Stage "EoS-$Version") -Type Directory

Copy-Item (Join-Path $PSScriptRoot LICENSE.txt) $Root
Copy-Item (Join-Path $PSScriptRoot ICON.svg) $Root
Copy-Item (Join-Path $PSScriptRoot ICON.ico) $Root
if ($PublishDoc) {
	Copy-Item $PublishDoc (Join-Path $Root doc) -Recurse
}
else {
	Copy-Item (Join-Path $PSScriptRoot README.md) $Root
}

Copy-Item $PublishBin (Join-Path $Root bin) -Recurse
New-Item (Join-Path $Root $EoSExe) -Type SymbolicLink -Target (Join-Path bin $EoSExe) -ErrorAction Continue

if ($Archive) {
	switch ($Archive) {
		'.tar.zst' { sh -c "tar -C '$Stage' -c '$(Split-Path $Root -Leaf)' | zstd -T$Parallel -o '$Path'" }
		'.tar.bz2' { tar -C $Stage -cjf $Path (Split-Path $Root -Leaf) }
		'.tar.xz'  { tar -C $Stage -cJf $Path (Split-Path $Root -Leaf) }
		'.tar.gz'  { tar -C $Stage -czf $Path (Split-Path $Root -Leaf) }
		'.tar.Z'   { tar -C $Stage -cZf $Path (Split-Path $Root -Leaf) }
		'.tar'     { tar -C $Stage -cf $Path (Split-Path $Root -Leaf) }
	}
	Remove-Item $Stage -Recurse
}

Write-Host "done" -ForegroundColor Blue

}
finally {
	Pop-Location
}

<# vim:set sts=4 ts=4 sw=4 noet ai: #>
