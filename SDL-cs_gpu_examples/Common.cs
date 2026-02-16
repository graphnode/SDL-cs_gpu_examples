using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

// Vertex formats matching the C examples
[StructLayout(LayoutKind.Sequential)]
public struct PositionVertex(float x, float y, float z)
{
    public float X = x, Y = y, Z = z;
}

[StructLayout(LayoutKind.Sequential)]
public struct PositionColorVertex(float x, float y, float z, byte r, byte g, byte b, byte a)
{
    public float X = x, Y = y, Z = z;
    public byte R = r, G = g, B = b, A = a;
}

[StructLayout(LayoutKind.Sequential)]
public struct PositionTextureVertex(float x, float y, float z, float u, float v)
{
    public float X = x, Y = y, Z = z;
    public float U = u, V = v;
}

[StructLayout(LayoutKind.Sequential)]
public struct PositionTextureColorVertex
{
    public float X, Y, Z, W;
    public float U, V, PaddingA, PaddingB;
    public float R, G, B, A;
}

[StructLayout(LayoutKind.Sequential)]
public struct ComputeSpriteInstance
{
    public float X, Y, Z;
    public float Rotation;
    public float W, H, PaddingA, PaddingB;
    public float TexU, TexV, TexW, TexH;
    public float R, G, B, A;
}

public static class Common
{
    public static string BasePath
    {
        get
        {
            string basePath = SDL.GetBasePath() ?? AppContext.BaseDirectory;
#if DEBUG
            var currentDir = new DirectoryInfo(basePath);
            while (currentDir != null)
            {
                if (currentDir.GetFiles("*.csproj").Length > 0)
                {
                    field = currentDir.FullName;
                    return field;
                }
                currentDir = currentDir.Parent;
            }
#endif
            field = basePath;
            return field;
        }
    }

    public static IntPtr LoadShader(
        IntPtr device,
        string shaderFilename,
        uint samplerCount = 0,
        uint uniformBufferCount = 0,
        uint storageBufferCount = 0,
        uint storageTextureCount = 0)
    {
        SDL.GPUShaderStage stage;
        if (shaderFilename.Contains(".vert"))
            stage = SDL.GPUShaderStage.Vertex;
        else if (shaderFilename.Contains(".frag"))
            stage = SDL.GPUShaderStage.Fragment;
        else
        {
            Console.WriteLine("Invalid shader stage!");
            return IntPtr.Zero;
        }

        string sourcePath = Path.Combine(BasePath, "Content", "Shaders", $"{shaderFilename}.hlsl");
        if (!File.Exists(sourcePath))
        {
            Console.WriteLine($"Shader source not found: {sourcePath}");
            return IntPtr.Zero;
        }

        var hlslSource = File.ReadAllText(sourcePath);

        var hlslInfo = new ShaderCross.HLSLInfo
        {
            ManagedSource = hlslSource,
            ManagedEntrypoint = "main",
            ShaderStage = (ShaderCross.ShaderStage)stage,
            Props = 0
        };

        var spirvBuffer = ShaderCross.CompileSPIRVFromHLSL(ref hlslInfo, out var spirvSize);
        if (spirvBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to compile HLSL to SPIRV: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        var spirvInfo = new ShaderCross.SPIRVInfo
        {
            ByteCode = spirvBuffer,
            ByteCodeSize = spirvSize,
            ShaderStage = (ShaderCross.ShaderStage)stage,
            ManagedEntrypoint = "main"
        };

        var resourceInfo = new ShaderCross.GraphicsShaderResourceInfo
        {
            NumSamplers = samplerCount,
            NumStorageTextures = storageTextureCount,
            NumStorageBuffers = storageBufferCount,
            NumUniformBuffers = uniformBufferCount
        };

        var shader = ShaderCross.CompileGraphicsShaderFromSPIRV(device, ref spirvInfo, ref resourceInfo, 0);
        SDL.Free(spirvBuffer);

        if (shader == IntPtr.Zero)
            Console.WriteLine($"Failed to create GPU shader: {SDL.GetError()}");

        return shader;
    }

    // P/Invoke for compute pipeline - works around C# binding issue
    [DllImport("SDL3_shadercross", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_ShaderCross_CompileComputePipelineFromSPIRV")]
    private static extern IntPtr CompileComputePipelineFromSPIRV(
        IntPtr device,
        in ShaderCross.SPIRVInfo info,
        in ShaderCross.ComputePipelineMetadata metadata,
        uint props);

    public static IntPtr CreateComputePipelineFromShader(
        IntPtr device,
        string shaderFilename,
        ShaderCross.ComputePipelineMetadata metadata)
    {
        string sourcePath = Path.Combine(BasePath, "Content", "Shaders", $"{shaderFilename}.hlsl");
        if (!File.Exists(sourcePath))
        {
            Console.WriteLine($"Compute shader source not found: {sourcePath}");
            return IntPtr.Zero;
        }

        var hlslSource = File.ReadAllText(sourcePath);

        var hlslInfo = new ShaderCross.HLSLInfo
        {
            ManagedSource = hlslSource,
            ManagedEntrypoint = "main",
            ShaderStage = (ShaderCross.ShaderStage)2, // Compute
            Props = 0
        };

        var spirvBuffer = ShaderCross.CompileSPIRVFromHLSL(ref hlslInfo, out var spirvSize);
        if (spirvBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to compile compute HLSL to SPIRV: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        var spirvInfo = new ShaderCross.SPIRVInfo
        {
            ByteCode = spirvBuffer,
            ByteCodeSize = spirvSize,
            ShaderStage = (ShaderCross.ShaderStage)2, // Compute
            ManagedEntrypoint = "main"
        };

        var pipeline = CompileComputePipelineFromSPIRV(device, spirvInfo, metadata, 0);
        SDL.Free(spirvBuffer);

        if (pipeline == IntPtr.Zero)
            Console.WriteLine($"Failed to create compute pipeline: {SDL.GetError()}");

        return pipeline;
    }

    public static IntPtr LoadImage(IntPtr device, string imageFilename, out int width, out int height)
    {
        string fullPath = Path.Combine(BasePath, "Content", "Images", imageFilename);

        var surface = SDL.LoadBMP(fullPath);
        if (surface == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to load BMP: {SDL.GetError()}");
            width = height = 0;
            return IntPtr.Zero;
        }

        unsafe
        {
            var surfacePtr = (SDL.Surface*)surface;
            width = surfacePtr->Width;
            height = surfacePtr->Height;

            // Convert to ABGR8888 if needed
            if (surfacePtr->Format != SDL.PixelFormat.ABGR8888)
            {
                var converted = SDL.ConvertSurface(surface, SDL.PixelFormat.ABGR8888);
                SDL.DestroySurface(surface);
                if (converted == IntPtr.Zero)
                {
                    Console.WriteLine($"Failed to convert surface: {SDL.GetError()}");
                    width = height = 0;
                    return IntPtr.Zero;
                }
                surface = converted;
                surfacePtr = (SDL.Surface*)surface;
            }
        }

        return surface;
    }

    public static IntPtr CreateGPUTextureFromImage(IntPtr device, string imageFilename, out int width, out int height)
    {
        var surface = LoadImage(device, imageFilename, out width, out height);
        if (surface == IntPtr.Zero)
            return IntPtr.Zero;

        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        };

        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);
        if (texture == IntPtr.Zero)
        {
            SDL.DestroySurface(surface);
            return IntPtr.Zero;
        }

        UploadTextureFromSurface(device, texture, surface, width, height);
        SDL.DestroySurface(surface);

        return texture;
    }

    public static void UploadTextureFromSurface(IntPtr device, IntPtr texture, IntPtr surface, int width, int height)
    {
        uint dataSize = (uint)(width * height * 4);

        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = dataSize
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);

        var transferPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var surfacePtr = (SDL.Surface*)surface;
            Buffer.MemoryCopy((void*)surfacePtr->Pixels, (void*)transferPtr, dataSize, dataSize);
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var textureTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = transferBuffer,
            Offset = 0
        };
        var textureRegion = new SDL.GPUTextureRegion
        {
            Texture = texture,
            W = (uint)width,
            H = (uint)height,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in textureTransferInfo, in textureRegion, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, transferBuffer);
    }

    // Helper to create a standard PositionColor pipeline
    public static IntPtr CreatePositionColorPipeline(
        IntPtr device,
        IntPtr window,
        IntPtr vertexShader,
        IntPtr fragmentShader,
        SDL.GPURasterizerState rasterizerState = default,
        SDL.GPUDepthStencilState depthStencilState = default,
        SDL.GPUTextureFormat depthStencilFormat = 0,
        bool hasDepthStencilTarget = false,
        SDL.GPUPrimitiveType primitiveType = SDL.GPUPrimitiveType.TriangleList)
    {
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription { Format = swapchainFormat };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var vertexBufferDesc = new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
            Pitch = (uint)Marshal.SizeOf<PositionColorVertex>()
        };
        var vertexBufferDescPtr = SDL.StructureToPointer<SDL.GPUVertexBufferDescription>(vertexBufferDesc);

        var vertexAttributes = new SDL.GPUVertexAttribute[]
        {
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float3, Location = 0, Offset = 0 },
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Ubyte4Norm, Location = 1, Offset = (uint)(sizeof(float) * 3) }
        };
        var attrSize = Marshal.SizeOf<SDL.GPUVertexAttribute>();
        var vertexAttributesPtr = Marshal.AllocHGlobal(attrSize * vertexAttributes.Length);
        for (var i = 0; i < vertexAttributes.Length; i++)
            Marshal.StructureToPtr(vertexAttributes[i], vertexAttributesPtr + i * attrSize, false);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = primitiveType,
            RasterizerState = rasterizerState,
            DepthStencilState = depthStencilState,
            VertexInputState = new SDL.GPUVertexInputState
            {
                VertexBufferDescriptions = vertexBufferDescPtr,
                NumVertexBuffers = 1,
                VertexAttributes = vertexAttributesPtr,
                NumVertexAttributes = 2
            },
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1,
                HasDepthStencilTarget = hasDepthStencilTarget,
                DepthStencilFormat = depthStencilFormat
            }
        };

        var pipeline = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributesPtr);

        return pipeline;
    }

    // Helper to create a standard PositionTexture pipeline
    public static IntPtr CreatePositionTexturePipeline(
        IntPtr device,
        IntPtr window,
        IntPtr vertexShader,
        IntPtr fragmentShader,
        SDL.GPUColorTargetBlendState blendState = default)
    {
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription
        {
            Format = swapchainFormat,
            BlendState = blendState
        };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var vertexBufferDesc = new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
            Pitch = (uint)Marshal.SizeOf<PositionTextureVertex>()
        };
        var vertexBufferDescPtr = SDL.StructureToPointer<SDL.GPUVertexBufferDescription>(vertexBufferDesc);

        var vertexAttributes = new SDL.GPUVertexAttribute[]
        {
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float3, Location = 0, Offset = 0 },
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float2, Location = 1, Offset = (uint)(sizeof(float) * 3) }
        };
        var attrSize = Marshal.SizeOf<SDL.GPUVertexAttribute>();
        var vertexAttributesPtr = Marshal.AllocHGlobal(attrSize * vertexAttributes.Length);
        for (var i = 0; i < vertexAttributes.Length; i++)
            Marshal.StructureToPtr(vertexAttributes[i], vertexAttributesPtr + i * attrSize, false);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            VertexInputState = new SDL.GPUVertexInputState
            {
                VertexBufferDescriptions = vertexBufferDescPtr,
                NumVertexBuffers = 1,
                VertexAttributes = vertexAttributesPtr,
                NumVertexAttributes = 2
            },
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1
            }
        };

        var pipeline = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributesPtr);

        return pipeline;
    }
}
