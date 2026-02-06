# MMA-EoS Codebase Overview

A computational framework for mineralogical thermodynamics, implementing equations of state for crystalline solids.

**Language**: F# (.NET 8.0)
**Version**: 2.1.0
**License**: GNU GPL v3+

## Directory Structure

```
eos/
├── Libraries/
│   ├── EoS.Core/              # Foundation: types, math, chemistry, parsing
│   ├── EoS.CommandLine/       # CLI argument parsing & MPI support
│   ├── EoS.DebyeModel/        # Birch-Murnaghan-Mie-Debye-Grüneisen EoS
│   ├── EoS.PolynomialModel/   # Polynomial & Holland-Powell EoS
│   └── EoS.Optimization/      # Gibbs energy minimization (LP solver)
├── Tools/
│   ├── EoS.CommandLineTool/   # CLI tools (prop, opti, adib, etc.)
│   ├── EoS.DocumentationTool/ # Doc generator
│   └── EoS.Slackbot/          # Slack integration
├── Tests/                     # NUnit tests for each module
├── LPSolve/                   # Linear programming C library wrapper
├── MPIGlue/                   # MPI parallelization C wrapper
└── *.xml                      # Mineral databases (SLB11, SLB21, HHP13, etc.)
```

## Core Utilities (EoS.Core)

### Math.fs
- `debye3`: Debye function D₃(x) computation
- `bisectionRoot`: Root finding via bisection method
- Numerical differentiation with configurable step sizes

### Chemistry.fs & Element.fs
- `Element`: 88 chemical elements with atomic masses
- `Formula`: Parse chemical formulas (e.g., "Mg2SiO4", "(Fe0.9Mg0.1)2SiO4")
- `Composite`: Formula composition with coefficients

### Fractions.fs
- Molar ↔ mass ↔ atomic fraction conversions

### Parsing.fs
- Generic parsing utilities (success/failure types, whitespace handling)
- Regex patterns in `RegularExpressionConstants.fs`

### Units.fs
- UDUNITS2 C library integration via P/Invoke
- Runtime unit parsing and conversion

### Xml.fs
- `IXSerializable`: Interface for XML persistence
- `XFormatter`: Context for unit system and database loading
- Database lookup and object registration

## Key Interfaces (EoS.Phases)

```fsharp
type IThermoElastic =
  abstract Volume : p:float<Pa> * T:float<K> * x:float[] -> float<m^3/mol>
  abstract Moduli : ... -> κ:float<Pa> * µ:float<Pa>
  abstract Velocities : ... -> vp:float<m/s> * vs:float<m/s>
  abstract Energy, Entropy, IsobaricHeatCapacity : ...

type IPhase =
  inherit IThermoElastic
  abstract Mass : x:float[] -> float<kg/mol>
  abstract Formula : x:float[] -> Chemistry.Formula
```

## Command Line Utilities (EoS.CommandLine)

### CommandLine.fs
- `IFlag`, `BoolFlag`, `ValueFlag<'T>`: Flexible argument parsing
- `ValueRange<'T>`: Support for parameter sweeps

### MPI.fs
- MPI parallelization via P/Invoke to `mpiglue` C library
- Communicator creation, send/receive, barrier synchronization

## CLI Tools (EoS.CommandLineTool)

| Command | Purpose |
|---------|---------|
| `form`  | Convert formulas between molar/mass/atomic fractions |
| `pidx`  | List phases in database with indices |
| `prop`  | Compute properties: V, ρ, β, α, κ, µ, vp, vs, G, S, Cp |
| `opti`  | Gibbs energy minimization for phase assemblages |
| `adib`  | Compute adiabatic phase diagrams |
| `bmpv`  | Interactive visualization of phase diagrams |
| `fitf`  | Parameter fitting utilities |

**Usage**: `eos <tool> [options] [arguments]`

## Equation of State Models

### DebyeModel (EoS.DebyeModel)
Birch-Murnaghan-Mie-Debye-Grüneisen model. Parameters: V₀, K₀, K₀', θ₀, γ₀, G₀, etc.

### PolynomialModel (EoS.PolynomialModel)
- `PolynomialSolid.fs`: Polynomial phase implementation
- `HollandPowellSolid.fs`: Holland-Powell model with polynomial heat capacity

## Optimization (EoS.Optimization)

- `Optimization.fs`: Gibbs energy minimization algorithm
- `LPSolve.fs`: P/Invoke wrapper for lp_solve 5.5
- MPI parallelization for phase diagram generation

## Databases (XML)

- `SLB11.xml`, `SLB21.xml`: Stixrude & Lithgow-Bertelloni (mantle minerals)
- `HHP13.xml`: Holland-Powell 2013 (crustal minerals)
- `PSN07.xml`: Pelton, Scheil, Navarre 2007

## Build & Run

```bash
# Full build + install (to ~/.local by default)
./install.sh

# Custom install location
./install.sh --prefix=/opt/eos

# Build + test + install
./install.sh --test

# Clean rebuild
./install.sh --clean

# Execute (after adding $PREFIX/bin to PATH)
eos prop -db=SLB11 fo -P=10e9Pa -T=1000K -o=V
```

### Manual build (without install.sh)

```bash
dotnet build EoS.sln
dotnet test
dotnet publish -c Release
```

## External Dependencies

- **UDUNITS2**: Unit conversion (C library)
- **lp_solve 5.5**: Linear programming (C library)
- **MPI**: Parallel computing (via mpiglue wrapper)
- **Murphy.ByteString**: NuGet package for binary handling
