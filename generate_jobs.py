#!/usr/bin/env python3
"""
Generate PBS job scripts for property calculations (prop-only).

Assumes optimization outputs (opti_clean.out) already exist for each combination.
Each job runs only the prop step for a single (model, formula) pair.

Usage:
    python3 generate_jobs.py
    python3 generate_jobs.py --eos-dir /scratch/xd2/sg8812/eos
    python3 generate_jobs.py --submit   # also submits all jobs via qsub
"""

import argparse
import os
import subprocess

# ── Configuration ──────────────────────────────────────────────────

MODELS = ["SLB08", "SLB11", "SLB21"]

FORMULAS = [
    "pyrolite/ncfmas",
    "pyrolite/cfmas",
    "pyrolite/fmas",
    "pyrolite/cfms",
    "pyrolite/fms",
    "pyrolite/ms",
    "bulk-oceanic-crust/ncfmas",
    "bulk-oceanic-crust/cfmas",
    "bulk-oceanic-crust/fmas",
    "bulk-oceanic-crust/cfms",
    "bulk-oceanic-crust/fms",
    "bulk-oceanic-crust/ms",
    "depleted-mantle/cfmas",
    "depleted-mantle/fmas",
    "depleted-mantle/cfms",
    "depleted-mantle/fms",
    "depleted-mantle/ms",
]

# Grid parameters
NPROCS = 48
P_START = "0Pa"
P_END = "140e9Pa"
NP = 701
T_START = "0K"
T_END = "4500"
NT = 91

OUTPUT_PROPERTIES = "p,T,rho,V,beta,alpha,kappa&mu,vp&vs,Cp,Cv,gamma"

# PBS defaults
PBS_PROJECT = "xd2"
PBS_QUEUE = "normal"
PBS_NCPUS = 48
PBS_MEM = "192GB"
PBS_WALLTIME = "2:00:00"  # Reduced since prop-only is faster than opti+prop
PBS_STORAGE = "scratch/xd2"


def make_job_name(model, formula):
    """Create a short PBS job name from model and formula."""
    # e.g. SLB21_pyrolite_cfmas
    return f"{model}_{formula.replace('/', '_')}"


def make_script(model, formula, eos_dir, output_base):
    """Generate a PBS job script for property calculation only (assumes opti_clean.out exists)."""
    job_name = make_job_name(model, formula) + "_prop"
    output_dir = f"{output_base}/output-{model}/{formula}"

    script = f"""#!/bin/bash
#PBS -P {PBS_PROJECT}
#PBS -q {PBS_QUEUE}
#PBS -l ncpus={PBS_NCPUS}
#PBS -l mem={PBS_MEM}
#PBS -l walltime={PBS_WALLTIME}
#PBS -l storage={PBS_STORAGE}
#PBS -l wd
#PBS -N {job_name}
#PBS -j oe
#PBS -o {output_base}/jobs/{job_name}.log

set -euo pipefail

module purge
module load openmpi/4.0.7

EOS_DIR="{eos_dir}"
OUTPUT_DIR="{output_dir}"

echo "=== Job: {job_name} ==="
echo "Model:   {model}"
echo "Formula: {formula}"
echo "Started: $(date)"
echo ""

# Check that cleaned optimization output exists
if [ ! -f "$OUTPUT_DIR/opti_clean.out" ]; then
    echo "ERROR: $OUTPUT_DIR/opti_clean.out not found"
    echo "Please run optimization first"
    exit 1
fi

# ── Property Calculation ──────────────────────────────────────────
echo "Running property calculation..."
echo "Input: $OUTPUT_DIR/opti_clean.out"

mpiexec -np {NPROCS} "$EOS_DIR/eos" prop \\
    -db="{model}" \\
    -x="$OUTPUT_DIR/opti_clean.out" \\
    -P="{P_START}" \\
    -toP="{P_END}" \\
    -nP={NP} \\
    -T="{T_START}" \\
    -toT="{T_END}" \\
    -nT={NT} \\
    -o="{OUTPUT_PROPERTIES}" \\
    > "$OUTPUT_DIR/prop.out"

echo ""
echo "Finished: $(date)"
echo "Output:   $OUTPUT_DIR/prop.out"
"""
    return script


def main():
    parser = argparse.ArgumentParser(
        description="Generate PBS job scripts for all EoS model/composition combinations."
    )
    parser.add_argument(
        "--eos-dir",
        default="/scratch/xd2/sg8812/eos",
        help="Directory containing the eos binary (default: /scratch/xd2/sg8812/eos)",
    )
    parser.add_argument(
        "--output-base",
        default="/scratch/xd2/sg8812/eos",
        help="Base directory for output files (default: /scratch/xd2/sg8812/eos)",
    )
    parser.add_argument(
        "--jobs-dir",
        default=None,
        help="Directory to write job scripts (default: <output-base>/jobs)",
    )
    parser.add_argument(
        "--submit",
        action="store_true",
        help="Submit all generated scripts via qsub",
    )
    args = parser.parse_args()

    jobs_dir = args.jobs_dir or os.path.join(args.output_base, "jobs")
    os.makedirs(jobs_dir, exist_ok=True)

    scripts = []
    for model in MODELS:
        for formula in FORMULAS:
            job_name = make_job_name(model, formula)
            script_path = os.path.join(jobs_dir, f"{job_name}.sh")
            content = make_script(model, formula, args.eos_dir, args.output_base)

            with open(script_path, "w") as f:
                f.write(content)
            os.chmod(script_path, 0o755)

            scripts.append(script_path)
            print(f"  {script_path}")

    print(f"\nGenerated {len(scripts)} job scripts in {jobs_dir}/")

    if args.submit:
        print("\nSubmitting jobs...")
        for script_path in scripts:
            result = subprocess.run(
                ["qsub", script_path], capture_output=True, text=True
            )
            name = os.path.basename(script_path)
            if result.returncode == 0:
                job_id = result.stdout.strip()
                print(f"  {name} -> {job_id}")
            else:
                print(f"  {name} -> FAILED: {result.stderr.strip()}")
    else:
        print("\nTo submit all jobs:")
        print(f"  for f in {jobs_dir}/*.sh; do qsub \"$f\"; done")


if __name__ == "__main__":
    main()
