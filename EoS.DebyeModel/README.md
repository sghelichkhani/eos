# SLB24 — Stixrude & Lithgow-Bertelloni 2024

## Reference

Stixrude, L. and C. Lithgow-Bertelloni,
*Thermodynamics of mantle minerals — III. The role of iron*,
Geophysical Journal International, **237**(3), 1699–1733, 2024.
[doi:10.1093/gji/ggae155](https://doi.org/10.1093/gji/ggae155)

## Source Data

The parameter files come from the HeFESTo thermodynamic code repository:

- **Upstream:** <https://github.com/stixrude/HeFESTo_Parameters_010123>
- **Fork:** <https://github.com/sghelichkhani/HeFESTo_Parameters_010123>

The repository contains one file per endmember mineral (43 lines each:
formula, name, and the full Birch-Murnaghan-Mie-Debye-Grüneisen parameter
set including Landau transition parameters) and one file per solution phase
in the `phase/` subdirectory (endmember list + interaction matrix).

## How SLB24.xml is Generated

The file `generate_xml.py` in the HeFESTo parameter repository converts
the raw parameter files into the XML format consumed by the EoS library.
To regenerate:

```bash
cd /path/to/HeFESTo_Parameters_010123
python3 generate_xml.py
cp SLB24.xml /path/to/eos/EoS.DebyeModel/SLB24.xml
```

The script:

1. Reads every endmember parameter file and parses all 43 parameters
   (reference energy, volume, bulk modulus, Debye temperature, Grüneisen
   parameter, shear modulus, and Landau critical temperature/entropy/volume).
2. Reads the `phase/` directory to discover which endmembers belong to which
   solution phase and extracts the symmetric interaction matrix (W values in
   kJ/mol).
3. Converts HeFESTo's site-mixed formula notation
   (e.g. `(Na_2Mg_1)Si_1Si_1Si_3O_12`) into the parenthesised format
   expected by EoS (e.g. `(Na2Mg)(Si)(Si)(Si)3(O)12`).
4. Wraps any endmember with T_crit > 0 in a `LandauModification` element,
   nesting the `DebyeSolid` inside it (with a `/nolandau` sub-id).
5. Emits the full XML with unit conversions: kJ→J for energies, cm³→m³ for
   volumes, GPa→Pa for moduli.

## Scientific Background: SLB24 vs SLB21

### Motivation

SLB21 (*Thermal expansivity, heat capacity and bulk modulus of the mantle*,
GJI 228, 1119–1149, 2022) operated in the six-component NCFMAS system
(Na₂O–CaO–FeO–MgO–Al₂O₃–SiO₂) and treated iron exclusively as
high-spin Fe²⁺. This is a significant simplification: iron is unique among
the major mantle-forming elements because it occurs in multiple valence
states (Fe⁰, Fe²⁺, Fe³⁺) and undergoes pressure-induced spin transitions
(high-spin ↔ low-spin) that alter its volume, entropy, and elastic
properties.

SLB24 addresses these limitations head-on. Its title — *The role of iron* —
reflects a comprehensive treatment of iron's multi-state behaviour
throughout the mantle.

### Expanded Chemical System

| | SLB21 | SLB24 |
|---|---|---|
| **System** | NCFMAS | CNFMASO + Cr |
| **Solution phases** | 15 | 15 |
| **Standalone phases** | 9 | 13 |
| **Endmember species** | 53 | 74 |
| **Landau-modified** | 17 | 34 |

Two new components are added:

- **Oxygen (O)** as an independent component, enabling computation of
  oxygen fugacity (fO₂) through the equilibrium 3 FeO ⇌ Fe₂O₃ + Fe⁰.
- **Chromium (Cr)**, entering garnet (knorringite), spinel (picrochromite),
  akimotoite (eskolaite), bridgmanite, post-perovskite, and Ca-ferrite as
  Cr₂O₃ endmembers.

### Multi-Valence Iron

SLB24 introduces endmembers for all three iron valence states:

- **Fe⁰ (metallic iron)** — three polymorphs are now included:
  α-iron (bcc, ferromagnetic with Curie temperature 1043 K),
  ε-iron (hcp), and γ-iron (fcc). These are essential for computing
  oxygen fugacity.
- **Fe²⁺ (ferrous)** — remains the dominant form in silicates, but is now
  treated with both high-spin and low-spin configurations in
  ferropericlase (`wu` vs `wuls`) and bridgmanite (`fepv`).
- **Fe³⁺ (ferric)** — new endmembers in bridgmanite (`hepv`, `hlpv`,
  `fapv`), post-perovskite (`hppv`, `lppv`), spinel (`smag`),
  ferropericlase (`mag`), Ca-ferrite (`hmag`), akimotoite (`hem`),
  garnet (`andr`), and clinopyroxene (`acm`).

### High-Spin to Low-Spin Transition

Under compression, the crystal-field splitting in Fe²⁺ (and Fe³⁺)
eventually exceeds the electron pairing energy, causing a transition from
the high-spin state (4 unpaired d-electrons for Fe²⁺) to the low-spin
state (0 unpaired electrons). SLB24 models this by including separate
high-spin and low-spin endmembers as components of a regular solution:

- **Ferropericlase:** `wu` (HS wüstite) + `wuls` (LS wüstite)
- **Bridgmanite:** `hepv` (HS Fe₂O₃-perovskite) + `hlpv` (LS Fe₂O₃-perovskite)

The Gibbs energy minimiser then determines the equilibrium HS/LS
proportion as a function of P and T, reproducing the broad spin crossover
observed experimentally across >100 GPa.

### Landau Modifications for Magnetic Transitions

Nearly every iron-bearing endmember carries a Landau modification to
account for magnetic ordering transitions. The Landau model adds excess
contributions to the Gibbs energy, entropy, and volume through an order
parameter Q:

```
Q² = 1 − T/T_C(P)     where  T_C(P) = T_C0 + P · V_D / S_D
```

The maximum excess entropy S_D encodes the magnetic entropy
R·ln(2S+1), where S is the spin quantum number. Representative values:

| Endmember | T_C0 (K) | S_D (J/mol/K) | Physical origin |
|-----------|----------|---------------|-----------------|
| `fea` (α-iron) | 1043 | 9.46 | Curie temperature |
| `hem` (hematite) | 950 | 29.79 | Néel temperature |
| `mag`/`smag`/`hmag` | 845.5 | 43.18 | Curie temperature of magnetite |
| `fa` (fayalite) | 65 | 26.76 | Néel temperature |
| `fepv`, `fppv`, etc. | 5 | varies | Low-T magnetic ordering |

Even when T_C0 is far below mantle temperatures (e.g. 5 K), the Landau
term still affects the reference Gibbs energy at 300 K and thereby shifts
the relative stability of iron-bearing vs iron-free phases. Neglecting
this systematically biases predicted iron partitioning between coexisting
minerals.

For phases where T > T_C at all conditions of interest, the
`transparent-fallback` flag (set to `True` in SLB24) causes the Landau
wrapper to fall back to the underlying DebyeSolid, avoiding numerical
issues.

### New and Expanded Solution Phases

**Bridgmanite (pv):** 3 → 7 endmembers — adds HS and LS Fe₂O₃,
FeAlO₃, and Cr₂O₃ components. This is the most abundant mineral in the
lower mantle, and iron's behaviour in it is critical for interpreting
seismic observations.

**Post-perovskite (ppv):** 3 → 5 endmembers — adds HS Fe₂O₃ and Cr₂O₃
components, plus a standalone LS Fe₂O₃ post-perovskite (`lppv`).

**Ferropericlase (mw):** 3 → 5 endmembers — adds LS wüstite and
magnetite, enabling modelling of the spin crossover.

**Garnet (gt):** 4 → 7 endmembers — adds Na-majorite, andradite (Fe³⁺),
and knorringite (Cr).

**Spinel (sps):** 2 → 4 endmembers — adds magnetite and picrochromite.

**Ca-ferrite (cf):** 3 → 5 endmembers — adds high-pressure magnetite and
Cr Ca-ferrite.

**Akimotoite (il):** 3 → 5 endmembers — adds hematite and eskolaite.

**HP-clinopyroxene (c2c)** and **NAL phase (nal)** are new solution phases
not present in SLB21.

### New Standalone Phases

α-iron (`fea`), ε-iron (`fee`), γ-iron (`feg`),
wollastonite (`wo`), pseudo-wollastonite (`pwo`),
α-PbO₂ SiO₂ / seifertite (`apbo`),
and LS Fe₂O₃ post-perovskite (`lppv`).
