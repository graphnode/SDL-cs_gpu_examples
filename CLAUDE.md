# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SDL-cs_gpu_examples is a C# GPU graphics programming examples project using SDL3-CS bindings. It demonstrates modern GPU programming patterns with SDL3 for .NET 10. Based on [TheSpydog/SDL_gpu_examples](https://github.com/TheSpydog/SDL_gpu_examples).

## Build & Run Commands

```bash
# Build
dotnet build SDL-cs_gpu_examples.sln

# Run all examples
dotnet run --project SDL-cs_gpu_examples

# Run a specific example
dotnet run --project SDL-cs_gpu_examples -- <ExampleName>
```

Available examples: ClearScreen, ClearScreenMultiWindow, BasicTriangle, BasicVertexBuffer, TexturedQuad, TexturedAnimatedQuad, CustomSampling, BlitMirror, GenerateMipmaps, Latency, BasicCompute, ComputeUniforms, ComputeSampler, CopyAndReadback, CopyConsistency, CullMode, BasicStencil, InstancedIndexed, WindowResize, TriangleMSAA, Clear3DSlice, DrawIndirect, Texture2DArray, Cubemap, Blit2DArray, BlitCube, DepthSampler, PullSpriteBatch, ComputeSpriteBatch

## Architecture

### Entry Point (Program.cs)
- Examples registered as `ExampleInfo(string Name, Func<int> Run)` records in `AllExamples` array
- CLI takes optional example name argument; runs all if none specified
- Initializes SDL ShaderCross at startup and cleans up on exit

### Common.cs - Shared Utilities
- **Vertex structs** with `[StructLayout(LayoutKind.Sequential)]` for GPU interop:
  - `PositionVertex`, `PositionColorVertex`, `PositionTextureVertex`, `PositionTextureColorVertex`, `ComputeSpriteInstance`
- **LoadShader()** - Runtime shader compilation using ShaderCross:
  1. Reads HLSL source from `Content/Shaders/`
  2. Compiles HLSL to SPIRV via `ShaderCross.CompileSPIRVFromHLSL()`
  3. Creates GPU shader via `ShaderCross.CompileGraphicsShaderFromSPIRV()` (handles cross-compilation to DXIL/MSL internally)

### Examples Pattern
Each example is a static class with `Main()` method following this flow:
1. SDL init with Video subsystem
2. GPU device creation (with SPIRV | DXIL | MSL format flags)
3. Window creation and claiming for GPU
4. Event loop with command buffer acquire → render pass → submit
5. Explicit resource cleanup

### Shader System
- **Source files**: `Content/Shaders/*.hlsl`
- Runtime compilation via SDL ShaderCross (HLSL → SPIRV → backend format)
- ShaderCross handles cross-compilation to the appropriate backend: D3D12 (DXIL), Vulkan (SPIRV), Metal (MSL)

## Key Conventions

- All GPU handles are `IntPtr`; check for `IntPtr.Zero` after creation
- Use `Marshal.AllocHGlobal/FreeHGlobal` or `SDL.StructureToPointer<T>()` for GPU struct marshaling
- Shader stage detected from filename convention (`.vert.hlsl`, `.frag.hlsl`, `.comp.hlsl`)
- `unsafe` blocks required for GPU pointer operations; cast `(nint)(&struct)` for uniform data
- Use `System.Numerics.Matrix4x4` for matrix math (not custom structs)
- Project supports Native AOT compilation (`PublishAot: true`)

## Known SDL3-CS Binding Workarounds

- **`CompileComputePipelineFromSPIRV`**: C# binding incorrectly takes `GraphicsShaderMetadata` instead of `ComputePipelineMetadata`. Workaround: direct P/Invoke in `Common.cs`
- **`DownloadFromGPUBuffer`**: C# binding incorrectly types second parameter as `GPUTextureRegion` instead of `GPUBufferRegion`. Workaround: direct P/Invoke in `CopyAndReadback.cs`

## Dependencies

- **SDL3-CS**: C# bindings for SDL3
- **SDL3-CS.Native**: Native SDL3 libraries
- **SDL3-CS.Native.Shadercross**: SDL ShaderCross for runtime HLSL compilation and cross-compilation
