<#
.SYNOPSIS
	Build and test MMA-EoS

.PARAMETER Framework
	Target .NET framework for the build ('net8.0', or 'net462').

.PARAMETER Configuration
	Build configuration ('Release' or 'Debug').

.PARAMETER DotNET
	The .NET Core SDK tool command.

.PARAMETER Make
	A GNU Make compatible make command.

.PARAMETER CC
	The C compiler to use for native code.

.PARAMETER MPICC
	The C compiler to use for native code using MPI.

.PARAMETER Pandoc
	The Pandoc converter to use for the documentation.

.PARAMETER Document
	Whether to generate API documentation.

.PARAMETER Test
	Whether to run tests.

.PARAMETER Version
	Override version number (use with care).

.PARAMETER Runtimes
	Override runtime identifier(s) (use with care).
#>

[CmdletBinding(PositionalBinding = $false)]
param (
	[ValidateSet('net8.0', 'net462')]
	[string]$Framework = 'net8.0',
	[ValidateSet('Release', 'Debug')]
	[string]$Configuration = 'Release',
	[string]$DotNET = 'dotnet',
	[string]$Make = 'make',
	[string]$CC = 'cc',
	[string]$MPICC = 'mpicc',
	[string]$Pandoc = 'pandoc',
	[switch]$Document = $false,
	[switch]$Test = $false,
	[string]$Version,
	[string[]]$Runtimes
)

Push-Location $PSScriptRoot
$ErrorActionPreference = 'Stop'
try {

if (-not $Version) {
	$Now = Get-Date -AsUTC
	$Ref = New-Object DateTime ($Now.Year, 1, 1, 0, 0, 0, $Now.Kind)
	$Version = "2.1.0.$([Math]::Floor(((($Now - $Ref).Days + 1) * 24 + $Now.Hour) * 6 + $Now.Minute / 10))"
}

if (-not $Runtimes) {
	$Runtimes = switch ([Environment]::OSVersion.Platform) {
		'Unix' {
			[Environment]::Is64BitOperatingSystem ? 'linux-x64' : 'linux-x86'
		}
		'MacOSX' {
			'osx-x64', 'osx-arm64'
		}
		default {
			$Runtime = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
			if ($Runtime -like '*-x64') {
				$Runtime, ($Runtime -replace '-x64$', '-x86')
			}
			else {
				$Runtime
			}
		}
	}
}

Write-Host "Building $Configuration MMA-EoS $Version for $Runtimes" -ForegroundColor Blue

$PathVar = switch ([Environment]::OSVersion.Platform) {
	'Unix'   { 'LD_LIBRARY_PATH' }
	'MacOSX' { 'DYLD_LIBRARY_PATH' }
	default  { 'PATH' }
}

$NativePath = New-Object Collections.Specialized.OrderedDictionary
foreach ($Runtime in $Runtimes) {
	$LPSolveBin = Join-Path $PWD LPSolve bin $Configuration $Runtime
	$MPIGlueBin = Join-Path $PWD MPIGlue bin $Configuration $Runtime

	New-Item $LPSolveBin -Type Directory -ErrorAction Ignore >$null
	New-Item $MPIGlueBin -Type Directory -ErrorAction Ignore >$null

	if (-not $NativePath.Contains($LPSolveBin)) {
		$NativePath.Add($LPSolveBin, 'LPSolve')
	}
	if (-not $NativePath.Contains($MPIGlueBin)) {
		$NativePath.Add($MPIGlueBin, 'MPIGlue')
	}
	foreach ($_ in (Get-Content Env:$PathVar -ErrorAction Ignore) -split [IO.Path]::PathSeparator) {
		if ((-not [String]::IsNullOrEmpty($_)) -and (-not $NativePath.Contains($_)) -and (Test-Path $_ -Type Container)) {
			$NativePath.Add($_, "Env:$PathVar")
		}
	}

	Write-Host "Building native code for $Runtime..." -ForegroundColor Blue

	& $Make -C LPSolve CC="$CC" CONF="$Configuration" ARCH="$Runtime"
	& $Make -C MPIGlue CC="$MPICC" CONF="$Configuration" ARCH="$Runtime"
}

Set-Content Env:$PathVar ($NativePath.Keys -join [IO.Path]::PathSeparator)

Write-Host "Building .NET code..." -ForegroundColor Blue

& $DotNET build -c $Configuration -p:"Version=$Version" EoS.sln

if ($Document) {
	Write-Host "Compiling documentation..." -ForegroundColor Blue

	$Documentation = Join-Path doc $Configuration
	$NetStandard = 'netstandard2.0'

	New-Item $Documentation -Type Directory -ErrorAction Ignore >$null

	& $Pandoc --standalone --metadata 'title=MMA-EoS' README.md -o (Join-Path $Documentation README.html)
	& $DotNET (Join-Path EoS.DocumentationTool bin $Configuration $Framework eosdoc.dll) -o $Documentation `
	  (Join-Path EoS.Core bin $Configuration $NetStandard EoS.Core.dll) `
	  (Join-Path EoS.DebyeModel bin $Configuration $NetStandard EoS.DebyeModel.dll) `
	  (Join-Path EoS.PolynomialModel bin $Configuration $NetStandard EoS.PolynomialModel.dll) `
	  (Join-Path EoS.Optimization bin $Configuration $NetStandard EoS.Optimization.dll) `
	  (Join-Path EoS.CommandLine bin $Configuration $NetStandard EoS.CommandLine.dll)
}

if ($Test) {
	Write-Host "Testing .NET libraries..." -ForegroundColor Blue
	& $DotNET test -c $Configuration --no-build EoS.sln
}

foreach ($Runtime in $Runtimes) {
	Write-Host "Publishing MMA-EoS tool for $Runtime..." -ForegroundColor Blue

	$Framework1 = if (($Runtime -like 'win*') -and ($Framework -like 'net?.?')) {
		"$Framework-windows"
	}
	else {
		$Framework
	}

	$LPSolveBin = Join-Path LPSolve bin $Configuration $Runtime *
	$MPIGlueBin = Join-Path MPIGlue bin $Configuration $Runtime *
	$PublishBin = Join-Path EoS.CommandLineTool bin $Configuration $Framework1 $Runtime publish

	& $DotNET publish -c $Configuration -f $Framework1 -r $Runtime `
	  --self-contained true -p:"Version=$Version" `
	  (Join-Path EoS.CommandLineTool EoS.CommandLineTool.fsproj) -o $PublishBin

	Copy-Item $LPSolveBin $PublishBin
	Copy-Item $MPIGlueBin $PublishBin
}

Write-Host "done" -ForegroundColor Blue

}
finally {
	Pop-Location
}

<# vim:set sts=4 ts=4 sw=4 noet ai: #>
