using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class Cubemap
{
    private static readonly SDL.FColor[] ClearColors =
    [
        new() { R = 1.0f, G = 0.0f, B = 0.0f, A = 1.0f },
        new() { R = 0.0f, G = 1.0f, B = 0.0f, A = 1.0f },
        new() { R = 0.0f, G = 0.0f, B = 1.0f, A = 1.0f },
        new() { R = 1.0f, G = 1.0f, B = 0.0f, A = 1.0f },
        new() { R = 1.0f, G = 0.0f, B = 1.0f, A = 1.0f },
        new() { R = 0.0f, G = 1.0f, B = 1.0f, A = 1.0f }
    ];

    public static int Main()
    {
        // Initialize SDL
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        // Create GPU device
        var device = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            true,
            null);
        if (device == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU device: {SDL.GetError()}");
            return -1;
        }

        // Create window
        var window = SDL.CreateWindow("Cubemap", 640, 480, 0);
        if (window == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window: {SDL.GetError()}");
            return -1;
        }

        // Claim window for GPU device
        if (!SDL.ClaimWindowForGPUDevice(device, window))
        {
            Console.WriteLine($"Failed to claim window: {SDL.GetError()}");
            return -1;
        }

        // Create the shaders
        var vertexShader = Common.LoadShader(device, "Skybox.vert", 0, 1);
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "Skybox.frag", 1);
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create fragment shader!");
            return -1;
        }

        // Create the pipeline
        var pipeline = CreateSkyboxPipeline(device, window, vertexShader, fragmentShader);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create the GPU resources
        var vertexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionVertex>() * 24)
        });

        var indexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 36
        });

        var texture = SDL.CreateGPUTexture(device, new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureTypeCube,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = 64,
            Height = 64,
            LayerCountOrDepth = 6,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler
        });

        var sampler = SDL.CreateGPUSampler(device, new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge
        });

        // Set up buffer data
        var bufferTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)(Marshal.SizeOf<PositionVertex>() * 24 + sizeof(ushort) * 36)
        });

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, bufferTransferBuffer, false);
        unsafe
        {
            var vertices = (PositionVertex*)transferDataPtr;
            vertices[0] = new PositionVertex(-10, -10, -10);
            vertices[1] = new PositionVertex(10, -10, -10);
            vertices[2] = new PositionVertex(10, 10, -10);
            vertices[3] = new PositionVertex(-10, 10, -10);

            vertices[4] = new PositionVertex(-10, -10, 10);
            vertices[5] = new PositionVertex(10, -10, 10);
            vertices[6] = new PositionVertex(10, 10, 10);
            vertices[7] = new PositionVertex(-10, 10, 10);

            vertices[8] = new PositionVertex(-10, -10, -10);
            vertices[9] = new PositionVertex(-10, 10, -10);
            vertices[10] = new PositionVertex(-10, 10, 10);
            vertices[11] = new PositionVertex(-10, -10, 10);

            vertices[12] = new PositionVertex(10, -10, -10);
            vertices[13] = new PositionVertex(10, 10, -10);
            vertices[14] = new PositionVertex(10, 10, 10);
            vertices[15] = new PositionVertex(10, -10, 10);

            vertices[16] = new PositionVertex(-10, -10, -10);
            vertices[17] = new PositionVertex(-10, -10, 10);
            vertices[18] = new PositionVertex(10, -10, 10);
            vertices[19] = new PositionVertex(10, -10, -10);

            vertices[20] = new PositionVertex(-10, 10, -10);
            vertices[21] = new PositionVertex(-10, 10, 10);
            vertices[22] = new PositionVertex(10, 10, 10);
            vertices[23] = new PositionVertex(10, 10, -10);

            var indexData = (ushort*)&vertices[24];
            ushort[] indices =
            [
                0, 1, 2, 0, 2, 3,
                6, 5, 4, 7, 6, 4,
                8, 9, 10, 8, 10, 11,
                14, 13, 12, 15, 14, 12,
                16, 17, 18, 16, 18, 19,
                22, 21, 20, 23, 22, 20
            ];
            for (int i = 0; i < indices.Length; i++)
                indexData[i] = indices[i];
        }
        SDL.UnmapGPUTransferBuffer(device, bufferTransferBuffer);

        // Upload the transfer data to the GPU buffers
        var cmdbuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(cmdbuf);

        SDL.UploadToGPUBuffer(copyPass,
            new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = 0 },
            new SDL.GPUBufferRegion { Buffer = vertexBuffer, Offset = 0, Size = (uint)(Marshal.SizeOf<PositionVertex>() * 24) },
            false);

        SDL.UploadToGPUBuffer(copyPass,
            new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = (uint)(Marshal.SizeOf<PositionVertex>() * 24) },
            new SDL.GPUBufferRegion { Buffer = indexBuffer, Offset = 0, Size = sizeof(ushort) * 36 },
            false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.ReleaseGPUTransferBuffer(device, bufferTransferBuffer);

        // Clear the faces of the cube texture
        for (int i = 0; i < 6; i++)
        {
            var colorTargetInfo = new SDL.GPUColorTargetInfo
            {
                Texture = texture,
                LayerOrDepthPlane = (uint)i,
                ClearColor = ClearColors[i],
                LoadOp = SDL.GPULoadOp.Clear,
                StoreOp = SDL.GPUStoreOp.Store
            };

            var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
            var renderPass = SDL.BeginGPURenderPass(cmdbuf, colorTargetPtr, 1, IntPtr.Zero);
            Marshal.FreeHGlobal(colorTargetPtr);
            SDL.EndGPURenderPass(renderPass);
        }

        SDL.SubmitGPUCommandBuffer(cmdbuf);

        // Print the instructions
        Console.WriteLine("Press Left/Right to view the opposite direction!");

        var camPosZ = 4.0f;

        // Main loop
        var running = true;
        while (running)
        {
            while (SDL.PollEvent(out var evt))
            {
                switch ((SDL.EventType)evt.Type)
                {
                    case SDL.EventType.Quit:
                    case SDL.EventType.WindowCloseRequested:
                        running = false;
                        break;
                    case SDL.EventType.KeyDown:
                        var scancode = evt.Key.Scancode;
                        if (scancode == SDL.Scancode.Left || scancode == SDL.Scancode.Right)
                        {
                            camPosZ *= -1;
                        }
                        break;
                }
            }

            // Draw
            var commandBuffer = SDL.AcquireGPUCommandBuffer(device);
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                var proj = Matrix4x4.CreatePerspectiveFieldOfView(
                    75.0f * MathF.PI / 180.0f,
                    640.0f / 480.0f,
                    0.01f,
                    100.0f);
                var view = Matrix4x4.CreateLookAt(
                    new Vector3(0, 0, camPosZ),
                    new Vector3(0, 0, 0),
                    new Vector3(0, 1, 0));

                var viewproj = Matrix4x4.Multiply(view, proj);

                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipeline);

                var bufferBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                var indexBinding = new SDL.GPUBufferBinding { Buffer = indexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                var texSamplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture, Sampler = sampler };
                var samplerBindPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(texSamplerBinding);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindPtr, 1);
                Marshal.FreeHGlobal(samplerBindPtr);

                unsafe
                {
                    SDL.PushGPUVertexUniformData(commandBuffer, 0, (nint)(&viewproj), (uint)sizeof(Matrix4x4));
                }

                SDL.DrawGPUIndexedPrimitives(renderPass, 36, 1, 0, 0, 0);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseGPUBuffer(device, indexBuffer);
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseGPUSampler(device, sampler);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }

    private static IntPtr CreateSkyboxPipeline(IntPtr device, IntPtr window, IntPtr vertexShader, IntPtr fragmentShader)
    {
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription { Format = swapchainFormat };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var vertexBufferDesc = new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
            Pitch = (uint)Marshal.SizeOf<PositionVertex>()
        };
        var vertexBufferDescPtr = SDL.StructureToPointer<SDL.GPUVertexBufferDescription>(vertexBufferDesc);

        var vertexAttribute = new SDL.GPUVertexAttribute
        {
            BufferSlot = 0,
            Format = SDL.GPUVertexElementFormat.Float3,
            Location = 0,
            Offset = 0
        };
        var vertexAttributePtr = SDL.StructureToPointer<SDL.GPUVertexAttribute>(vertexAttribute);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            VertexInputState = new SDL.GPUVertexInputState
            {
                VertexBufferDescriptions = vertexBufferDescPtr,
                NumVertexBuffers = 1,
                VertexAttributes = vertexAttributePtr,
                NumVertexAttributes = 1
            },
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1
            }
        };

        var pipelineResult = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributePtr);

        return pipelineResult;
    }
}
