#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────
# integration-test.sh — Low-resolution opti+prop pipeline test
#
# Runs a small Gibbs energy minimisation (opti) followed by
# property calculation (prop) for pyrolite/ms (Mg-Si system)
# using SLB21.  Serial only — no MPI required.
#
# Usage:
#   ./tests/integration-test.sh /path/to/eos
# ──────────────────────────────────────────────────────────────

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <path-to-eos-binary>" >&2
    exit 1
fi

EOS_BIN="$1"

if [[ ! -x "$EOS_BIN" ]]; then
    echo "Error: '$EOS_BIN' is not executable or does not exist" >&2
    exit 1
fi

TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TMPDIR"' EXIT

OPTI_OUT="$TMPDIR/opti.txt"
PROP_OUT="$TMPDIR/prop.txt"

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ── Step 1: Run opti ──────────────────────────────────────────
echo "==> Running opti (SLB21, pyrolite/ms, 5x5 grid)..."

"$EOS_BIN" opti -db=SLB21 \
    -P=0Pa -toP=140e9Pa -nP=5 \
    -T=300K -toT=3000K -nT=5 \
    -bulk='pyrolite/ms' \
    -o=p,T,x \
    2>"$TMPDIR/opti.err" \
    | sed 's/\x1b\[[0-9;]*m//g' \
    > "$OPTI_OUT"

OPTI_EXIT=${PIPESTATUS[0]}

if [[ $OPTI_EXIT -eq 0 ]]; then
    pass "opti exited with code 0"
else
    fail "opti exited with code $OPTI_EXIT"
fi

OPTI_LINES=$(wc -l < "$OPTI_OUT" | tr -d ' ')
echo "     opti output: $OPTI_LINES lines"

if [[ $OPTI_LINES -gt 0 ]]; then
    pass "opti produced output ($OPTI_LINES lines)"
else
    fail "opti produced no output"
fi

# ── Step 2: Run prop with opti grid ──────────────────────────
echo "==> Running prop (SLB21, reading opti grid, rho+vp&vs)..."

"$EOS_BIN" prop -db=SLB21 \
    -x="$OPTI_OUT" \
    -o=p,T,rho,vp\&vs \
    2>"$TMPDIR/prop.err" \
    > "$PROP_OUT"

PROP_EXIT=$?

if [[ $PROP_EXIT -eq 0 ]]; then
    pass "prop exited with code 0"
else
    fail "prop exited with code $PROP_EXIT"
fi

PROP_LINES=$(wc -l < "$PROP_OUT" | tr -d ' ')
echo "     prop output: $PROP_LINES lines"

if [[ $PROP_LINES -gt 0 ]]; then
    pass "prop produced output ($PROP_LINES lines)"
else
    fail "prop produced no output"
fi

# Check that prop output has the expected columns (p, T, rho, vp, vs = 5 columns)
FIRST_DATA=$(grep -v '^#' "$PROP_OUT" | head -1)
if [[ -n "$FIRST_DATA" ]]; then
    NCOLS=$(echo "$FIRST_DATA" | awk '{print NF}')
    if [[ $NCOLS -ge 5 ]]; then
        pass "prop output has $NCOLS columns (expected >= 5)"
    else
        fail "prop output has $NCOLS columns (expected >= 5)"
    fi
else
    fail "prop output has no data lines (only comments)"
fi

# ── Summary ───────────────────────────────────────────────────
echo ""
echo "============================================"
echo " Integration test: $PASS passed, $FAIL failed"
echo "============================================"

if [[ $FAIL -gt 0 ]]; then
    echo ""
    echo "--- opti stderr ---"
    cat "$TMPDIR/opti.err" 2>/dev/null || true
    echo "--- prop stderr ---"
    cat "$TMPDIR/prop.err" 2>/dev/null || true
    exit 1
fi

exit 0
