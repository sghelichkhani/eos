# MMA-EoS

[![Build and Test](https://github.com/sghelichkhani/eos/actions/workflows/build.yml/badge.svg)](https://github.com/sghelichkhani/eos/actions/workflows/build.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

> **Note:** This repository is a fork of the original [MMA-EoS project](https://chust.org/repos/eos/) by Thomas Chust et al. The goal of this fork is to gradually optimize and modernize the codebase while maintaining compatibility with the original functionality.

*MMA-EoS* is a computational framework for mineralogical
thermodynamics. You can use it to calculate compositional,
thermodynamic and mechanical properties of polycrystalline aggregates
for use in geosciences.

*MMA-EoS* implements equations of state for solids, comes with sets of
ready-to-use model parameters and can either be controlled through
command line programs or linked to your own applications as a library.

## Installation

### Quick Start (macOS/Linux)

The easiest way to build and install from source:

```bash
# Clone the repository
git clone https://github.com/sghelichkhani/eos.git
cd eos

# Build and install (requires .NET 8.0 SDK, C compiler, MPI)
./install.sh

# Or with custom options:
./install.sh --prefix=/opt/eos    # Custom install location
./install.sh --test               # Run tests before installing
./install.sh --clean              # Clean rebuild
```

After installation, add `~/.local/bin` (or your custom prefix) to your PATH.

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/) or later
- C compiler (gcc, clang)
- MPI libraries (`openmpi` or similar) for parallel execution
- [UDUNITS2](http://www.unidata.ucar.edu/software/udunits/) for unit conversion

### Building from Source

Most of the code for *MMA-EoS* is written in [F#](http://fsharp.org/)
with some performance critical parts and system interfaces in C. This
means that the code will run on many platforms that have a .NET
environment, but some machine specific parts need to be present for
certain functionality.

For manual builds:

```bash
# Build native libraries
make -C LPSolve CC=cc CONF=Release ARCH=linux-x64
make -C MPIGlue CC=mpicc CONF=Release ARCH=linux-x64

# Build .NET solution
dotnet build EoS.sln -c Release

# Run tests
dotnet test EoS.sln -c Release
```

### Docker

You can run *MMA-EoS* using [Docker](https://www.docker.com/). Images are
provided in the repository [chust/eos](https://hub.docker.com/r/chust/eos):

```bash
docker run --rm chust/eos                      # Single process
docker run --env=NPROC=N --rm chust/eos        # Parallel execution
```

### Binary Distribution

If you unpacked a [binary distribution](https://chust.org/repos/eos/uvlist)
archive of *MMA-EoS*, you should be able to run the launcher program
`eos` directly.

## Command Line Use ##

The launcher program allows you to invoke a number of command line
tools for common tasks. Simply running

```.bash
eos
```

will list the available subcommand. Running a subcommand with the
`-help` flag should list its command line options, for example:

```.bash
eos prop -help
```

The tools all have similar options. Perhaps the most important one
`-db=...` which selects a file with model parameters to use in the
calculations. You can write, for example, `-db=SLB11` to select the
file `SLB11.xml` from the *MMA-EoS* library directory or
`-db=path/to/some.xml` to specify an explicit path. Additional
non-option arguments often allow to narrow the selection of phases
from the database.

To see what's in a phase collection, open the XML database file in a
text editor or try something like this:

```.bash
eos pidx -db=SLB11
```

The `pidx` tool also accepts the option `-r` to recursively list
members of collections; and it can offset the indices it reports
(`-o=N`), which can be handy if you want to determine column numbers
in a file that contains composition vectors along with other data
fields.

The `prop` tool can compute state variables for phases and collections
of phases. For example, you could output the volume (`-o=V`) of
forsterite (`fo`) with model parameters from Stixrude and
Lithgow-Bertelloni 2011 (`-db=SBL11`) at a pressure of 10 GPa
(`-P=10e9Pa`) and a temperature of 1000 K (`-T=1000K`) with this
command:

```.bash
eos prop -db=SLB11 fo -P=10e9Pa -T=1000K -o=V
```

If pressures and temperatures are specified in other units than pascal
and kelvin, they are passed to unit conversion functions that require
the [UDUNITS](http://www.unidata.ucar.edu/software/udunits/) library
to be installed.

Instead of passing pressure and temperature options to the program,
you can also convey that information through standard input. If you
want to obtain properties for a solid solution or collection of
phases, *MMA-EoS* also needs a composition vector. For example one
could obtain the density of olivine with 90% forsterite and 10%
fayalite at 1 bar and 300 K with this command:

```.bash
echo '1e5 300 0.9 0.1' | eos prop -db=SLB11 ol -o=rho
```

The `-o=...` option supports a variety of output variables:

| Variable   | Unit    | Description                                     |
|------------|---------|-------------------------------------------------|
| `p`        | Pa      | Pressure                                        |
| `T`        | K       | Temperature                                     |
| `x`        | 1       | Composition vector                              |
| `atoms`    | 1       | Number of atoms per formula unit                |
| `V`        | m^3/mol | Molar volume                                    |
| `rho`      | kg/m^3  | Density                                         |
| `beta`     | 1/Pa    | Isothermal compressibility                      |
| `alpha`    | 1/K     | Thermal expansivity                             |
| `kappa&mu` | Pa      | Adiabatic compression modulus and shear modulus |
| `vp&vs`    | m/s     | P- and S-wave velocities                        |
| `G`        | J/mol   | Molar Gibbs energy                              |
| `S`        | J/mol/K | Molar entropy                                   |
| `Cp`       | J/mol/K | Isobaric molar heat capacity                    |
| `Cv`       | J/mol/K | Isochoric molar heat capacity                   |
| `gamma`    | 1       | Grüneisen parameter                             |

Finally, you can tell the tool to loop over a range of pressures
and/or temperatures using a combination of the `-P=...`, `-toP=...`,
`-nP=...`, `-T=...`, `-toT=...` and `-nT=...` options. For example,
the following command would display a graphical representation of
forsterite volume as a function of pressure using
[GMT](http://gmt.soest.hawaii.edu/) plotting tools and
[ImageMagick](http://www.imagemagick.org/):

```.bash
eos prop -db=SLB11 fo -P=0 -toP=25e9Pa -nP=251 -T=300K -o=p,V \
| psxy -JX15c -R0/25e9/3.5e-5/4.5e-5 -B'5e9:p/Pa:/0.5e-5:m@+3@+/mol:' \
| display -
```

The `opti` and `bmpv` tools can be used to produce phase
diagrams. `opti` takes a phase collection, pressure, temperature and
bulk composition as input and computes a stable phase
assemblage. `bmpv` is an interactive graphical viewer for such
data. The following command, for example, produces a coarsely resolved
diagram of stable phases with the bulk composition Mg2SiO4:

```.bash
cfg=(-db=SLB11 -P=0 -toP=25e9Pa -nP=26 -T=300K -toT=2300 -nT=21)
mpiexec -n $(nproc) eos opti ${cfg[@]} -bulk=Mg2SiO4 -o=p,T,phases \
| eos bmpv ${cfg[@]}
```

Both the `prop` and `opti` tools can run computations in parallel when
invoked in an MPI environment, provided the support library for MPI is
available for the host platform. Note that the output of the tools
will not necessarily be in the same sequence as their input in this
case; it is advisable to echo the input parameters through suitable
settings with the `-o=...` option.

As with the `prop` tool, the output of the `opti` tool can be
controlled using different arguments to the `-o=...` command line
option:

| Variable     | Unit    | Description                                         |
|--------------|---------|-----------------------------------------------------|
| `p`          | Pa      | Pressure                                            |
| `T`          | K       | Temperature                                         |
| `bulk`       | formula | Chemical bulk composition formula                   |
| `xbulk`      | 1       | Bulk composition interval position mapped to [0, 1] |
| `x`          | 1       | Composition vector of stable phase assemblage       |
| `xtree`      | string  | Textual representation of stable phase assemblage   |
| `phases`     | bitmap  | Bit field with bits set for each phase present      |
| `endmembers` | bitmap  | Bit field with bits set for each endmember present  |

When output from `opti` is generated with the `-o=p,T,x` option (which
is the default), the result can not only be piped into `prop`, but
also loaded by that tool as a composition grid:

```.bash
mpiexec -n $(nproc) eos opti -db=SLB11 \
  -P=0 -toP=25e9Pa -nP=26 -T=1000K -toT=2000 -nT=11 \
  -bulk='(Mg0.9Fe0.1)2SiO4' -o=p,T,x >grid.txt
mpiexec -n $(nproc) eos prop -db=SLB11 -x=grid.txt \
  -P=10e9Pa -T=300K -toT=1300K -nT=11 -o=p,T,rho
```

`prop` maps every requested pressure, temperature point to the nearest
grid point to determine the associated composition vector.

Note that *MMA-EoS* prints some progress messages on standard error
while data and comments describing the data go to standard output. If
you redirect output, make sure not to mix the two streams (for example
use `nohup` with two redirections like this `>output.txt 2>error.log`
to prevent random interleaving of output).

*MMA-EoS* deals with compositions as chemical formulas. The `form`
tool can convert between the molar format and mass or atomic
fractions, for example the bulk composition Mg2SiO4 can be converted
to a list of mass fractions like this:

```.bash
eos form -to=mass -flat -table Mg2SiO4
```

Of course the other direction works as well:

```.bash
eos form -from=mass 0.454874 O 0.345504 Mg Si
```

The fraction of the last chemical component passed on the command line
can be omitted and is assumed to be one minus the sum of the other
fractions.

As an alternative to specifying the composition manually, *MMA-EoS*
also comes with a small database of abbreviations for common bulk
compositions; for example `-bulk=pyrolite/fms` is equivalent to
`-bulk=(MgO)1.3424(FeO)0.1662(SiO2)`.

## Library Use ##

*MMA-EoS* is highly modular. The command line tools that come with it
by default are nothing but thin wrapper scripts for a set of F#
assemblies:

* `EoS.Core.dll` contains the infrastructure concerning chemical
  formulas, physical units and thermoelastic phases.
* `EoS.CommandLine.dll` contains code for parsing command line
  arguments and interfacing with MPI.
* `EoS.Optimization.dll` provides Gibbs energy minimization
  facilities; it depends on the
  [LPSolve](http://lpsolve.sourceforge.net/5.5/) library to handle
  linear optimization problems efficiently.
* `EoS.DebyeModel.dll` implements an equation of state for solids
  based on the Birch-Murnaghan elastic energy approximation and the
  Mie-Debye-Grüneisen model of lattice vibrations.
* `EoS.PolynomialModel.dll` implements equations of state of solids
  based on the Birch-Murnaghan elastic energy approximation and
  polynomial representations of heat capacity, thermal expansivity and
  elastic parameters.

Depending on what you want to do, you will have to link against one or
more of these libraries. XML documentation comes with the
assemblies. Building *MMA-EoS* from source will also render a simple
[HTML representation](index.xml) of the documentation comments in
the source. A good starting point to explore the API is the `EoS.Phases`
namespace.

The following example will output the volume of a phase as specified
by command line options:

```.fsx
#r "EoS.Core"
#r "EoS.CommandLine"

open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.Phases
open EoS.CommandLine

let db = Flag.DatabaseOpt "db" "Model parameter database"
let p = Flag.PressureOpt "P" 1.0e5<Pa> "Pressure"
let T = Flag.TemperatureOpt "T" 300.0<K> "Temperature"

let main (args : string[]) =
  let pname = if args.Length > 0 then args[0] else "fo"
  let phase = db.Value.TryGetObject<IPhase>(pname).Value
  let p, T = p.Value, T.Value
  printfn "V(p = %g Pa, T = %g K) = %g m^3/mol" <|
  p / 1.0<Pa> <| T / 1.0<K> <|
  phase.Volume(p, T) / 1.0<m^3/mol>
  0

Flag.WithCommandLineArgs main
```

## Background Information ##

Detailed information about the physical models and implementation
strategy behind *MMA-EoS* as well as a couple of application examples can
be found in this article:

> T. C. Chust, G. Steinle‐Neumann, D. Dolejs, B. S. Schuberth, and H. P. Bunge. MMA‐EoS: A computational framework for mineralogical thermodynamics. *Journal of Geophysical Research: Solid Earth*, 122:9881-9920, 2017. [doi:10.1002/2017JB014501](https://doi.org/10.1002/2017JB014501)

## Community Support ##

A user support forum for *MMA-EoS* is available in the form of a
[Slack](https://slack.com/) workspace. You are welcome to use the
[open invitation](https://join.slack.com/t/mma-eos/shared_invite/zt-37yqcvrm-aUZ3qM~WrxUuXpuv3QCYjQ)
and join the discussion!
