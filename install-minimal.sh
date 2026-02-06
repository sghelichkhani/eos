#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────
# install-minimal.sh — Fully native, self-contained build
#
# Downloads and builds ALL dependencies locally. No system
# packages needed beyond a C compiler and basic build tools.
#
# Prerequisites: cc (clang/gcc), make, curl, tar
#
# Usage:
#   ./install-minimal.sh                        # install to ~/.local
#   ./install-minimal.sh --prefix=/opt/eos      # custom location
#   ./install-minimal.sh --no-mpi               # skip MPI support
#   ./install-minimal.sh --jobs=8               # parallel make jobs
#   ./install-minimal.sh --clean                # clean build artifacts first
# ──────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/_build"

# ── Dependency versions ─────────────────────────────────────────
DOTNET_VERSION="8.0"
UDUNITS_VERSION="2.2.28"
OPENMPI_VERSION="5.0.5"

# ── Defaults ────────────────────────────────────────────────────
PREFIX="$HOME/.local"
BUILD_MPI=true
JOBS="$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 4)"
CLEAN=false

# ── Parse arguments ─────────────────────────────────────────────
for arg in "$@"; do
    case "$arg" in
        --prefix=*) PREFIX="${arg#--prefix=}" ;;
        --no-mpi)   BUILD_MPI=false ;;
        --jobs=*)   JOBS="${arg#--jobs=}" ;;
        --clean)    CLEAN=true ;;
        --help|-h)
            echo "Usage: $0 [--prefix=DIR] [--no-mpi] [--jobs=N] [--clean]"
            echo ""
            echo "Builds all dependencies natively from source:"
            echo "  .NET SDK $DOTNET_VERSION, UDUNITS2 $UDUNITS_VERSION, OpenMPI $OPENMPI_VERSION"
            echo "  lp_solve 5.5 (bundled), mpiglue (bundled)"
            echo ""
            echo "Only requires: cc, make, curl, tar"
            exit 0
            ;;
        *) echo "Unknown option: $arg" >&2; exit 1 ;;
    esac
done

# ── Check basic build tools ─────────────────────────────────────
echo "==> Checking build tools..."
for cmd in cc make curl tar; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "Error: '$cmd' not found. Install Xcode CLT (macOS) or build-essential (Linux)." >&2
        exit 1
    fi
done

# ── Clean if requested ──────────────────────────────────────────
if [[ "$CLEAN" == true ]]; then
    echo "==> Cleaning build artifacts..."
    rm -rf "$BUILD_DIR"
    rm -rf "$SCRIPT_DIR/LPSolve/bin"
    rm -rf "$SCRIPT_DIR/MPIGlue/bin"
fi

# ── Detect platform ─────────────────────────────────────────────
case "$(uname -s)-$(uname -m)" in
    Darwin-arm64)  RID="osx-arm64";   LIB_EXT="dylib"; PLATFORM="osx" ;;
    Darwin-x86_64) RID="osx-x64";     LIB_EXT="dylib"; PLATFORM="osx" ;;
    Linux-x86_64)  RID="linux-x64";   LIB_EXT="so";    PLATFORM="linux" ;;
    Linux-aarch64) RID="linux-arm64";  LIB_EXT="so";    PLATFORM="linux" ;;
    *) echo "Unsupported: $(uname -s)-$(uname -m)" >&2; exit 1 ;;
esac
echo "==> Platform: $RID (using $JOBS parallel jobs)"

mkdir -p "$BUILD_DIR"

# ══════════════════════════════════════════════════════════════════
# PHASE 1: Install .NET SDK locally
# ══════════════════════════════════════════════════════════════════
DOTNET_ROOT="$BUILD_DIR/dotnet"

if [[ -x "$DOTNET_ROOT/dotnet" ]]; then
    echo "==> .NET SDK already installed, skipping..."
else
    echo "==> Installing .NET SDK $DOTNET_VERSION..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "$BUILD_DIR/dotnet-install.sh"
    chmod +x "$BUILD_DIR/dotnet-install.sh"
    "$BUILD_DIR/dotnet-install.sh" \
        --channel "$DOTNET_VERSION" \
        --install-dir "$DOTNET_ROOT" \
        --no-path
fi

export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
echo "    .NET $(dotnet --version)"

# ══════════════════════════════════════════════════════════════════
# PHASE 2: Build UDUNITS2 from source
# ══════════════════════════════════════════════════════════════════
UDUNITS_PREFIX="$BUILD_DIR/udunits2"

if [[ -f "$UDUNITS_PREFIX/lib/libudunits2.$LIB_EXT" ]]; then
    echo "==> UDUNITS2 already built, skipping..."
else
    echo "==> Building UDUNITS2 $UDUNITS_VERSION..."
    UDUNITS_SRC="$BUILD_DIR/udunits-$UDUNITS_VERSION"
    UDUNITS_TAR="$BUILD_DIR/udunits-$UDUNITS_VERSION.tar.gz"

    if [[ ! -f "$UDUNITS_TAR" ]]; then
        curl -sSL "https://downloads.unidata.ucar.edu/udunits/$UDUNITS_VERSION/udunits-$UDUNITS_VERSION.tar.gz" \
            -o "$UDUNITS_TAR"
    fi

    tar xzf "$UDUNITS_TAR" -C "$BUILD_DIR"
    cd "$UDUNITS_SRC"
    ./configure \
        --prefix="$UDUNITS_PREFIX" \
        --disable-dependency-tracking
    make -j"$JOBS"
    make install
    cd "$SCRIPT_DIR"
    echo "    UDUNITS2 installed to $UDUNITS_PREFIX"
fi

# ══════════════════════════════════════════════════════════════════
# PHASE 3: Build OpenMPI from source (optional)
# ══════════════════════════════════════════════════════════════════
MPI_PREFIX="$BUILD_DIR/openmpi"

if [[ "$BUILD_MPI" == true ]]; then
    if [[ -x "$MPI_PREFIX/bin/mpicc" ]]; then
        echo "==> OpenMPI already built, skipping..."
    else
        echo "==> Building OpenMPI $OPENMPI_VERSION (this takes a few minutes)..."
        OPENMPI_SRC="$BUILD_DIR/openmpi-$OPENMPI_VERSION"
        OPENMPI_TAR="$BUILD_DIR/openmpi-$OPENMPI_VERSION.tar.gz"
        OPENMPI_MAJOR="${OPENMPI_VERSION%%.*}"
        OPENMPI_MINOR="v${OPENMPI_VERSION%.*}"

        if [[ ! -f "$OPENMPI_TAR" ]]; then
            curl -sSL "https://download.open-mpi.org/release/open-mpi/$OPENMPI_MINOR/openmpi-$OPENMPI_VERSION.tar.gz" \
                -o "$OPENMPI_TAR"
        fi

        tar xzf "$OPENMPI_TAR" -C "$BUILD_DIR"
        cd "$OPENMPI_SRC"
        ./configure \
            --prefix="$MPI_PREFIX" \
            --disable-mpi-fortran \
            --enable-shared \
            --disable-static
        make -j"$JOBS"
        make install
        cd "$SCRIPT_DIR"
        echo "    OpenMPI installed to $MPI_PREFIX"
    fi
    export PATH="$MPI_PREFIX/bin:$PATH"
    MPICC="$MPI_PREFIX/bin/mpicc"
else
    echo "==> Skipping MPI (--no-mpi)"
    MPICC=""
fi

# ══════════════════════════════════════════════════════════════════
# PHASE 4: Build native libraries from repo sources
# ══════════════════════════════════════════════════════════════════

# 4a: lp_solve (bundled source)
echo "==> Building liblpsolve55..."
make -C "$SCRIPT_DIR/LPSolve" CC=cc CONF=Release ARCH="$RID"

# 4b: mpiglue (bundled source)
if [[ "$BUILD_MPI" == true ]]; then
    echo "==> Building libmpiglue..."
    make -C "$SCRIPT_DIR/MPIGlue" CC="$MPICC" CONF=Release ARCH="$RID"
fi

# ══════════════════════════════════════════════════════════════════
# PHASE 5: Publish .NET project (self-contained)
# ══════════════════════════════════════════════════════════════════
echo "==> Publishing eos (self-contained, $RID)..."
dotnet publish -c Release -f net8.0 -r "$RID" \
    --self-contained true \
    "$SCRIPT_DIR/EoS.CommandLineTool/EoS.CommandLineTool.fsproj"

PUBLISH_DIR="$SCRIPT_DIR/EoS.CommandLineTool/bin/Release/net8.0/$RID/publish"

if [[ ! -d "$PUBLISH_DIR" ]]; then
    echo "Error: publish directory not found at $PUBLISH_DIR" >&2
    exit 1
fi

# ══════════════════════════════════════════════════════════════════
# PHASE 6: Assemble — copy native libs into publish directory
# ══════════════════════════════════════════════════════════════════
echo "==> Assembling native libraries..."

# lp_solve
cp "$SCRIPT_DIR/LPSolve/bin/Release/$RID/liblpsolve55.$LIB_EXT" "$PUBLISH_DIR/"

# mpiglue
if [[ "$BUILD_MPI" == true ]]; then
    cp "$SCRIPT_DIR/MPIGlue/bin/Release/$RID/libmpiglue.$LIB_EXT" "$PUBLISH_DIR/"

    # Also copy MPI runtime libraries so it's fully self-contained
    if [[ "$PLATFORM" == "osx" ]]; then
        cp "$MPI_PREFIX"/lib/libmpi*.dylib "$PUBLISH_DIR/" 2>/dev/null || true
        cp "$MPI_PREFIX"/lib/libopen-*.dylib "$PUBLISH_DIR/" 2>/dev/null || true
    else
        cp "$MPI_PREFIX"/lib/libmpi*.so* "$PUBLISH_DIR/" 2>/dev/null || true
        cp "$MPI_PREFIX"/lib/libopen-*.so* "$PUBLISH_DIR/" 2>/dev/null || true
    fi
fi

# udunits2 library + XML database
cp "$UDUNITS_PREFIX/lib/libudunits2.$LIB_EXT"* "$PUBLISH_DIR/" 2>/dev/null || true
cp -R "$UDUNITS_PREFIX/share/udunits" "$PUBLISH_DIR/udunits-db" 2>/dev/null || true

# ══════════════════════════════════════════════════════════════════
# PHASE 7: Install to prefix
# ══════════════════════════════════════════════════════════════════
echo "==> Installing to $PREFIX..."

mkdir -p "$PREFIX/lib/eos"
mkdir -p "$PREFIX/bin"

rm -rf "$PREFIX/lib/eos"
cp -R "$PUBLISH_DIR" "$PREFIX/lib/eos"

# Create wrapper script that sets library paths
if [[ "$PLATFORM" == "osx" ]]; then
    LIBPATH_VAR="DYLD_LIBRARY_PATH"
else
    LIBPATH_VAR="LD_LIBRARY_PATH"
fi

cat > "$PREFIX/bin/eos" << WRAPPER
#!/bin/bash
EOS_LIB="\$(cd "\$(dirname "\$0")/../lib/eos" && pwd)"
export ${LIBPATH_VAR}="\${EOS_LIB}\${${LIBPATH_VAR}:+:\$${LIBPATH_VAR}}"
export UDUNITS2_XML_PATH="\${EOS_LIB}/udunits-db/udunits2.xml"
exec "\${EOS_LIB}/eos" "\$@"
WRAPPER
chmod +x "$PREFIX/bin/eos"

# MPI wrapper (if built)
if [[ "$BUILD_MPI" == true ]]; then
    cat > "$PREFIX/bin/eos-mpi" << WRAPPER
#!/bin/bash
EOS_LIB="\$(cd "\$(dirname "\$0")/../lib/eos" && pwd)"
export ${LIBPATH_VAR}="\${EOS_LIB}\${${LIBPATH_VAR}:+:\$${LIBPATH_VAR}}"
export UDUNITS2_XML_PATH="\${EOS_LIB}/udunits-db/udunits2.xml"
exec mpirun "\${EOS_LIB}/eos" "\$@"
WRAPPER
    chmod +x "$PREFIX/bin/eos-mpi"
fi

# ══════════════════════════════════════════════════════════════════
# Summary
# ══════════════════════════════════════════════════════════════════
INSTALL_SIZE="$(du -sh "$PREFIX/lib/eos" | cut -f1)"

echo ""
echo "============================================"
echo " eos installed successfully"
echo "============================================"
echo ""
echo "  Install size:  $INSTALL_SIZE"
echo "  Binary:        $PREFIX/bin/eos"
if [[ "$BUILD_MPI" == true ]]; then
    echo "  MPI launcher:  $PREFIX/bin/eos-mpi"
fi
echo ""
echo "  Components built from source:"
echo "    .NET Runtime   $(dotnet --version)"
echo "    UDUNITS2       $UDUNITS_VERSION"
echo "    lp_solve       5.5.2.0 (bundled)"
if [[ "$BUILD_MPI" == true ]]; then
    echo "    OpenMPI        $OPENMPI_VERSION"
    echo "    mpiglue        (bundled)"
fi
echo ""
echo "  Build cache: $BUILD_DIR"
echo "  (safe to delete after install, speeds up rebuilds if kept)"
echo ""

if [[ ":$PATH:" != *":$PREFIX/bin:"* ]]; then
    echo "Add to PATH:"
    echo "  export PATH=\"$PREFIX/bin:\$PATH\""
    echo ""
fi
