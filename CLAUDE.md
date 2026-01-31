# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SDL-cs_gpu_examples is a C# GPU graphics programming examples project using SDL3-CS bindings. It demonstrates modern GPU programming patterns with SDL3 for .NET 10.

## Build & Run Commands

```bash
# Build
dotnet build SDL-cs_gpu_examples.sln

# Run all examples
dotnet run --project SDL-cs_gpu_examples

# Run a specific example
dotnet run --project SDL-cs_gpu_examples -- <ExampleName>
```

Available examples: ClearScreen, ClearScreenMultiWindow, BasicTriangle, BasicVertexBuffer

## Architecture

### Entry Point (Program.cs)
- Examples registered as `ExampleInfo(string Name, Func<int> Run)` records in `AllExamples` array
- CLI takes optional example name argument; runs all if none specified
- Initializes Slang shader compiler at startup and cleans up on exit

### Common.cs - Shared Utilities
- **Vertex structs** with `[StructLayout(LayoutKind.Sequential)]` for GPU interop:
  - `PositionVertex`, `PositionColorVertex`, `PositionTextureVertex`
- **LoadShader()** - Smart shader loading with precompilation support:
  1. First tries to load precompiled shader from `Content/Shaders/Compiled/{SPIRV|DXIL|MSL}/`
  2. Falls back to runtime Slang compilation if precompiled not found
  3. In DEBUG builds: auto-recompiles if source is newer than compiled, saves result

### SlangCompiler.cs - Runtime Shader Compilation
- Uses Slang.Sdk + Microsoft.Direct3D.DXC for runtime shader compilation
- `Init()` / `Quit()` for lifecycle management
- `Compile()` - Compiles shader source to DXIL or SPIR-V using slangc
- `GetPreferredFormat()` - Returns DXIL on Windows (D3D12), SPIR-V elsewhere (Vulkan)

### Examples Pattern
Each example is a static class with `Main()` method following this flow:
1. SDL init with Video subsystem
2. GPU device creation (with SPIR-V format flag for Vulkan backend)
3. Window creation and claiming for GPU
4. Event loop with command buffer acquire → render pass → submit
5. Explicit resource cleanup

### Shader System
- **Source files**: `Content/Shaders/Source/*.hlsl` (or `.slang`)
- **Precompiled shaders**: `Content/Shaders/Compiled/{SPIRV|DXIL|MSL}/`
- Shaders can be precompiled for faster startup, with runtime Slang compilation as fallback
- In DEBUG builds, source modifications trigger automatic recompilation
- Slang is highly HLSL-compatible - existing HLSL shaders work with minimal/no changes
- GPU backend: D3D12 (DXIL) on Windows, Vulkan (SPIR-V) on Linux, Metal (MSL) on macOS

## Key Conventions

- All GPU handles are `IntPtr`; check for `IntPtr.Zero` after creation
- Use `Marshal.AllocHGlobal/FreeHGlobal` for GPU struct marshaling
- Shader stage detected from filename convention (`.vert.hlsl`, `.frag.hlsl`)
- `unsafe` blocks required for GPU pointer operations
- Project supports Native AOT compilation (`PublishAot: true`)

## Dependencies

- **SDL3-CS**: C# bindings for SDL3
- **SDL3-CS.Native**: Native SDL3 libraries
- **Slang.Sdk**: Runtime shader compiler (alpha, Windows-only currently)
- **Microsoft.Direct3D.DXC**: DirectX Shader Compiler for DXIL output (D3D12)
