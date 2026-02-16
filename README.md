# SDL-cs GPU Examples

C# ports of [TheSpydog's SDL_gpu_examples](https://github.com/TheSpydog/SDL_gpu_examples) using [SDL3-CS](https://github.com/edwardgushchin/SDL3-CS) bindings.

## Examples

### Basics
- **ClearScreen** - Basic window with color clear
- **ClearScreenMultiWindow** - Multiple windows
- **BasicTriangle** - Triangle rendering with vertex/fragment shaders
- **BasicVertexBuffer** - Vertex buffer with color attributes

### Texturing
- **TexturedQuad** - Textured quad with 6 sampler modes (point/linear/anisotropic, clamp/wrap)
- **TexturedAnimatedQuad** - Animated rotating textured quads with color multiply
- **CustomSampling** - Custom fragment shader sampling via storage textures
- **Texture2DArray** - 2D array texture rendering

### Blitting
- **BlitMirror** - Texture blitting with horizontal/vertical flip modes
- **GenerateMipmaps** - Mipmap generation and mip level blitting
- **Blit2DArray** - Blitting 2D array textures with scaling
- **BlitCube** - Blitting cubemap textures

### 3D Rendering
- **Cubemap** - Cubemap texture sampling with 3D projection
- **DepthSampler** - Depth testing and depth texture sampling for post-process outline
- **BasicStencil** - Stencil buffer masking

### Compute
- **BasicCompute** - Basic compute shader that fills a texture
- **ComputeUniforms** - Compute shader with uniform parameters
- **ComputeSampler** - Compute shader with texture sampling
- **ComputeSpriteBatch** - Compute-driven sprite batch rendering
- **PullSpriteBatch** - Vertex shader pulling from storage buffers (8192 sprites)

### Advanced
- **CullMode** - GPU face culling modes (none/front/back, CW/CCW)
- **InstancedIndexed** - Instanced and indexed drawing
- **DrawIndirect** - GPU indirect draw commands
- **TriangleMSAA** - Multi-sample anti-aliasing with configurable sample counts
- **WindowResize** - Window resizing with GPU rendering
- **Clear3DSlice** - Clearing individual slices of a 3D texture
- **CopyAndReadback** - GPU buffer copy and CPU readback
- **CopyConsistency** - GPU copy operation consistency testing
- **Latency** - Input-to-output latency measurement

## Building & Running

```bash
dotnet build
dotnet run --project SDL-cs_gpu_examples                   # Run all examples sequentially
dotnet run --project SDL-cs_gpu_examples -- BasicTriangle  # Run a specific example
```

## Shader System

HLSL shader sources live in `Content/Shaders/`. At runtime, [SDL ShaderCross](https://wiki.libsdl.org/SDL3_shadercross/FrontPage) compiles HLSL to SPIRV, then cross-compiles to the appropriate backend format (DXIL for D3D12, MSL for Metal, or SPIRV for Vulkan).
