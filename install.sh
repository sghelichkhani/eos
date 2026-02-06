#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────
# install.sh — Build and install eos
#
# Usage:
#   ./install.sh                        # build + install to ~/.local
#   ./install.sh --prefix=/opt/eos      # custom install location
#   ./install.sh --test                  # run dotnet test before installing
#   ./install.sh --clean                 # clean all build artifacts first
# ──────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# ── Step 1: Argument parsing ─────────────────────────────────

PREFIX="$HOME/.local"
RUN_TESTS=false
CLEAN=false

for arg in "$@"; do
    case "$arg" in
        --prefix=*)
            PREFIX="${arg#--prefix=}"
            ;;
        --test)
            RUN_TESTS=true
            ;;
        --clean)
            CLEAN=true
            ;;
        --help|-h)
            echo "Usage: $0 [--prefix=DIR] [--test] [--clean]"
            echo ""
            echo "Options:"
            echo "  --prefix=DIR  Install to DIR (default: ~/.local)"
            echo "  --test        Run tests before installing"
            echo "  --clean       Clean all build artifacts first"
            exit 0
            ;;
        *)
            echo "Error: unknown argument '$arg'" >&2
            echo "Run '$0 --help' for usage." >&2
            exit 1
            ;;
    esac
done

# ── Step 2: Prerequisite checks ──────────────────────────────

check_command() {
    if ! command -v "$1" &>/dev/null; then
        echo "Error: '$1' is not installed or not on PATH." >&2
        exit 1
    fi
}

check_command dotnet
check_command make
check_command cc
check_command mpicc

# Verify .NET SDK 8.0+
DOTNET_VERSION="$(dotnet --version)"
DOTNET_MAJOR="${DOTNET_VERSION%%.*}"
if [ "$DOTNET_MAJOR" -lt 8 ] 2>/dev/null; then
    echo "Error: .NET SDK 8.0+ is required (found $DOTNET_VERSION)." >&2
    exit 1
fi

echo "Prerequisites OK (dotnet $DOTNET_VERSION, make, cc, mpicc)"

# ── Step 3: Detect architecture ──────────────────────────────

case "$(uname -m)" in
    arm64)  RID="osx-arm64" ;;
    x86_64) RID="osx-x64"   ;;
    *)
        echo "Error: unsupported architecture '$(uname -m)'." >&2
        exit 1
        ;;
esac

echo "Target: $RID"

# ── Step 4: Clean (if --clean) ───────────────────────────────

if [ "$CLEAN" = true ]; then
    echo "Cleaning build artifacts..."
    rm -rf "$SCRIPT_DIR/LPSolve/bin"
    rm -rf "$SCRIPT_DIR/MPIGlue/bin"
    dotnet clean -c Release "$SCRIPT_DIR/EoS.sln" || true
fi

# ── Step 5: Build native libraries ───────────────────────────

echo "Building liblpsolve55..."
make -C "$SCRIPT_DIR/LPSolve" CC=cc CONF=Release ARCH="$RID"

echo "Building libmpiglue..."
make -C "$SCRIPT_DIR/MPIGlue" CC=mpicc CONF=Release ARCH="$RID"

# ── Step 6: Publish the .NET project ─────────────────────────

echo "Publishing eos (self-contained, $RID)..."
dotnet publish -c Release -f net8.0 -r "$RID" \
    --self-contained true \
    "$SCRIPT_DIR/EoS.CommandLineTool/EoS.CommandLineTool.fsproj"

PUBLISH_DIR="$SCRIPT_DIR/EoS.CommandLineTool/bin/Release/net8.0/$RID/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: publish directory not found at $PUBLISH_DIR" >&2
    exit 1
fi

# ── Step 7: Copy native libraries into publish directory ─────

echo "Copying native libraries into publish directory..."
cp "$SCRIPT_DIR/LPSolve/bin/Release/$RID/liblpsolve55.dylib" "$PUBLISH_DIR/"
cp "$SCRIPT_DIR/MPIGlue/bin/Release/$RID/libmpiglue.dylib"   "$PUBLISH_DIR/"

# ── Step 8: Run tests (if --test) ────────────────────────────

if [ "$RUN_TESTS" = true ]; then
    echo "Running tests..."
    dotnet test -c Release --no-build "$SCRIPT_DIR/EoS.sln"
fi

# ── Step 9: Install ──────────────────────────────────────────

echo "Installing to $PREFIX..."

mkdir -p "$PREFIX/lib"
mkdir -p "$PREFIX/bin"

rm -rf "$PREFIX/lib/eos"
cp -R "$PUBLISH_DIR" "$PREFIX/lib/eos"

cat > "$PREFIX/bin/eos" << 'WRAPPER'
#!/bin/bash
exec "$(dirname "$0")/../lib/eos/eos" "$@"
WRAPPER
chmod +x "$PREFIX/bin/eos"

# ── Step 10: Summary ─────────────────────────────────────────

VERSION="2.1.0"
echo ""
echo "eos $VERSION installed successfully."
echo "  Binary:  $PREFIX/lib/eos/eos"
echo "  Wrapper: $PREFIX/bin/eos"

case ":$PATH:" in
    *":$PREFIX/bin:"*)
        ;;
    *)
        echo ""
        echo "Add $PREFIX/bin to your PATH:"
        echo "  export PATH=\"$PREFIX/bin:\$PATH\""
        ;;
esac
