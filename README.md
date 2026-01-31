# SDL-cs GPU Examples

C# ports of [TheSpydog's SDL_gpu_examples](https://github.com/TheSpydog/SDL_gpu_examples) using [SDL3-CS](https://github.com/edwardgushchin/SDL3-CS) bindings.

## Requirements

- .NET 10
- Windows (Slang.Sdk currently only supports Windows)

## Examples

- **ClearScreen** - Basic window with color clear
- **ClearScreenMultiWindow** - Multiple windows
- **BasicTriangle** - Triangle rendering with vertex/fragment shaders
- **BasicVertexBuffer** - Vertex buffer with color attributes

## Building & Running

```bash
dotnet build
dotnet run --project SDL-cs_gpu_examples -- BasicTriangle
```

## Shader System

### Precompiled Shaders (Default)
Precompiled shaders are included in `Content/Shaders/Compiled/` for faster startup:
- `SPIRV/` - Vulkan (Linux)
- `DXIL/` - Direct3D 12 (Windows)
- `MSL/` - Metal (macOS)

### Runtime Compilation (Fallback)
If precompiled shaders are not found, [Slang](https://shader-slang.org/) compiles HLSL sources from `Content/Shaders/Source/` at runtime.

### Debug Auto-Recompile
In DEBUG builds, modified source files are automatically recompiled and saved back to the Compiled directory.
