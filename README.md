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

## Shader Compilation

Shaders are compiled at runtime using [Slang](https://shader-slang.org/). HLSL source files in `Content/Shaders/Source/` are compiled to DXIL (D3D12) on Windows or SPIR-V (Vulkan) on Linux.
