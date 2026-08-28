// Ratpack's conv.cpp selects a Calc_UInt32x32To64 implementation from a hard-coded list of
// target architectures and #errors on anything unrecognized. That list predates WebAssembly,
// so wasm builds fail even though the operation itself is entirely portable: both branches
// expand to the same widening 32x32->64 multiply, and wasm32 has the 32-bit pointers and
// 64-bit arithmetic the check is really about.
//
// Force-included ahead of the upstream sources on wasm only. Defining _M_CEE_PURE is what
// satisfies the #if. It is not literally true (it means "pure MSIL" to MSVC), but it is the
// one macro in that list which describes no CPU, so it cannot be mistaken for a claim about
// the target the way _M_AMD64 or __ARM_ARCH would be, and nothing in the toolchain's own
// headers tests it. conv.cpp's inner guard is #ifndef, so the definition below is what the
// build actually uses; the macro only steers the #error away.
//
// The alternative is patching external/calculator, which the repo deliberately never does.

#pragma once

#ifdef __wasm__

#ifndef _M_CEE_PURE
#define _M_CEE_PURE 1
#endif

#ifndef Calc_UInt32x32To64
#define Calc_UInt32x32To64(a, b) ((uint64_t)((uint32_t)(a)) * (uint64_t)((uint32_t)(b)))
#endif

#endif
