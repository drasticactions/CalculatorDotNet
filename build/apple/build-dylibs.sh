#!/bin/bash

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_DIR="${1:-$REPO_ROOT/artifacts/apple}"
BUILD_ROOT="$REPO_ROOT/src/CalculatorDotNet/obj/native/apple"

IOS_DEPLOYMENT_TARGET=15.0
MACCATALYST_DEPLOYMENT_TARGET=15.0
MACOS_DEPLOYMENT_TARGET=12.0

if [ ! -f "$REPO_ROOT/external/calculator/src/CalcManager/UnitConverter.cpp" ]; then
    echo "error: external/calculator is empty. Run: git submodule update --init" >&2
    exit 1
fi

if ! xcrun --sdk iphoneos --show-sdk-path >/dev/null 2>&1; then
    echo "error: the iOS SDK is unavailable. Install Xcode and run xcode-select -s." >&2
    exit 1
fi

# build_slice <name> <cmake-args...>
build_slice() {
    local name="$1"; shift
    local dir="$BUILD_ROOT/$name"
    local log="$BUILD_ROOT/$name.log"

    echo "==> Building $name"
    mkdir -p "$BUILD_ROOT"
    if ! { cmake -S "$REPO_ROOT/src/native" -B "$dir" \
            -DCMAKE_BUILD_TYPE=Release \
            "$@" && cmake --build "$dir" --parallel; } >"$log" 2>&1; then
        echo "error: $name slice failed to build" >&2
        cat "$log" >&2
        exit 1
    fi

    mkdir -p "$OUTPUT_DIR/$name"
    cp "$dir/libCalcManagerShim.dylib" "$OUTPUT_DIR/$name/"
    codesign --force --sign - "$OUTPUT_DIR/$name/libCalcManagerShim.dylib"
}

build_slice ios \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_SYSROOT=iphoneos \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="$IOS_DEPLOYMENT_TARGET"

build_slice iossimulator \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_SYSROOT=iphonesimulator \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="$IOS_DEPLOYMENT_TARGET"

build_slice macos \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="$MACOS_DEPLOYMENT_TARGET"

CATALYST_FLAGS="-target arm64-apple-ios${MACCATALYST_DEPLOYMENT_TARGET}-macabi -isysroot $(xcrun --sdk macosx --show-sdk-path)"
build_slice maccatalyst \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
    -DCMAKE_CXX_FLAGS="$CATALYST_FLAGS" \
    -DCMAKE_SHARED_LINKER_FLAGS="$CATALYST_FLAGS"

echo "==> $OUTPUT_DIR"
for slice in ios iossimulator macos maccatalyst; do
    echo "    $slice: $(lipo -archs "$OUTPUT_DIR/$slice/libCalcManagerShim.dylib")"
done
