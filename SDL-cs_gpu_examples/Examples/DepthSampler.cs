using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class DepthSampler
{
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
        var window = SDL.CreateWindow("DepthSampler", 640, 480, 0);
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

        // Create the Scene shaders
        var sceneVertexShader = Common.LoadShader(device, "PositionColorTransform.vert", 0, 1);
        if (sceneVertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create 'PositionColorTransform' vertex shader!");
            return -1;
        }

        var sceneFragmentShader = Common.LoadShader(device, "SolidColorDepth.frag", 0, 1);
        if (sceneFragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create 'SolidColorDepth' fragment shader!");
            return -1;
        }

        // Create the Effect shaders
        var effectVertexShader = Common.LoadShader(device, "TexturedQuad.vert");
        if (effectVertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create 'TexturedQuad' vertex shader!");
            return -1;
        }

        var effectFragmentShader = Common.LoadShader(device, "DepthOutline.frag", 2, 1);
        if (effectFragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create 'DepthOutline' fragment shader!");
            return -1;
        }

        // Create the Scene pipeline (PositionColor with depth, renders to R8G8B8A8Unorm offscreen)
        var scenePipeline = CreateScenePipeline(device, sceneVertexShader, sceneFragmentShader);
        if (scenePipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create Scene pipeline!");
            return -1;
        }

        // Create the Effect pipeline (PositionTexture with blending)
        var effectBlendState = new SDL.GPUColorTargetBlendState
        {
            EnableBlend = true,
            SrcColorBlendFactor = SDL.GPUBlendFactor.One,
            DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendFactor = SDL.GPUBlendFactor.One,
            DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = SDL.GPUBlendOp.Add
        };
        var effectPipeline = Common.CreatePositionTexturePipeline(
            device, window, effectVertexShader, effectFragmentShader,
            blendState: effectBlendState);

        if (effectPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create Outline Effect pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, effectVertexShader);
        SDL.ReleaseGPUShader(device, effectFragmentShader);
        SDL.ReleaseGPUShader(device, sceneVertexShader);
        SDL.ReleaseGPUShader(device, sceneFragmentShader);

        // Create the Scene Textures (quarter-resolution for pixelated look)
        SDL.GetWindowSizeInPixels(window, out var w, out var h);
        int sceneWidth = w / 4;
        int sceneHeight = h / 4;

        var sceneColorTexture = SDL.CreateGPUTexture(device, new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Width = (uint)sceneWidth,
            Height = (uint)sceneHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.ColorTarget
        });

        var sceneDepthTexture = SDL.CreateGPUTexture(device, new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Width = (uint)sceneWidth,
            Height = (uint)sceneHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
            Format = SDL.GPUTextureFormat.D16Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.DepthStencilTarget
        });

        // Create Outline Effect Sampler
        var effectSampler = SDL.CreateGPUSampler(device, new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeW = SDL.GPUSamplerAddressMode.Repeat
        });

        // Create & Upload Scene Vertex and Index Buffers
        var sceneVertexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 24)
        });

        var sceneIndexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 36
        });

        {
            var bufferTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
            {
                Usage = SDL.GPUTransferBufferUsage.Upload,
                Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 24 + sizeof(ushort) * 36)
            });

            var transferDataPtr = SDL.MapGPUTransferBuffer(device, bufferTransferBuffer, false);
            unsafe
            {
                var vertices = (PositionColorVertex*)transferDataPtr;
                vertices[0] = new PositionColorVertex(-10, -10, -10, 255, 0, 0, 255);
                vertices[1] = new PositionColorVertex(10, -10, -10, 255, 0, 0, 255);
                vertices[2] = new PositionColorVertex(10, 10, -10, 255, 0, 0, 255);
                vertices[3] = new PositionColorVertex(-10, 10, -10, 255, 0, 0, 255);

                vertices[4] = new PositionColorVertex(-10, -10, 10, 255, 255, 0, 255);
                vertices[5] = new PositionColorVertex(10, -10, 10, 255, 255, 0, 255);
                vertices[6] = new PositionColorVertex(10, 10, 10, 255, 255, 0, 255);
                vertices[7] = new PositionColorVertex(-10, 10, 10, 255, 255, 0, 255);

                vertices[8] = new PositionColorVertex(-10, -10, -10, 255, 0, 255, 255);
                vertices[9] = new PositionColorVertex(-10, 10, -10, 255, 0, 255, 255);
                vertices[10] = new PositionColorVertex(-10, 10, 10, 255, 0, 255, 255);
                vertices[11] = new PositionColorVertex(-10, -10, 10, 255, 0, 255, 255);

                vertices[12] = new PositionColorVertex(10, -10, -10, 0, 255, 0, 255);
                vertices[13] = new PositionColorVertex(10, 10, -10, 0, 255, 0, 255);
                vertices[14] = new PositionColorVertex(10, 10, 10, 0, 255, 0, 255);
                vertices[15] = new PositionColorVertex(10, -10, 10, 0, 255, 0, 255);

                vertices[16] = new PositionColorVertex(-10, -10, -10, 0, 255, 255, 255);
                vertices[17] = new PositionColorVertex(-10, -10, 10, 0, 255, 255, 255);
                vertices[18] = new PositionColorVertex(10, -10, 10, 0, 255, 255, 255);
                vertices[19] = new PositionColorVertex(10, -10, -10, 0, 255, 255, 255);

                vertices[20] = new PositionColorVertex(-10, 10, -10, 0, 0, 255, 255);
                vertices[21] = new PositionColorVertex(-10, 10, 10, 0, 0, 255, 255);
                vertices[22] = new PositionColorVertex(10, 10, 10, 0, 0, 255, 255);
                vertices[23] = new PositionColorVertex(10, 10, -10, 0, 0, 255, 255);

                var indexData = (ushort*)&vertices[24];
                ushort[] indices =
                [
                    0, 1, 2, 0, 2, 3,
                    4, 5, 6, 4, 6, 7,
                    8, 9, 10, 8, 10, 11,
                    12, 13, 14, 12, 14, 15,
                    16, 17, 18, 16, 18, 19,
                    20, 21, 22, 20, 22, 23
                ];
                for (int i = 0; i < indices.Length; i++)
                    indexData[i] = indices[i];
            }
            SDL.UnmapGPUTransferBuffer(device, bufferTransferBuffer);

            var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
            var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

            SDL.UploadToGPUBuffer(copyPass,
                new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = 0 },
                new SDL.GPUBufferRegion { Buffer = sceneVertexBuffer, Offset = 0, Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 24) },
                false);

            SDL.UploadToGPUBuffer(copyPass,
                new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = (uint)(Marshal.SizeOf<PositionColorVertex>() * 24) },
                new SDL.GPUBufferRegion { Buffer = sceneIndexBuffer, Offset = 0, Size = sizeof(ushort) * 36 },
                false);

            SDL.EndGPUCopyPass(copyPass);
            SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
            SDL.ReleaseGPUTransferBuffer(device, bufferTransferBuffer);
        }

        // Create & Upload Outline Effect Vertex and Index buffers
        var effectVertexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        });

        var effectIndexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 6
        });

        {
            var bufferTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
            {
                Usage = SDL.GPUTransferBufferUsage.Upload,
                Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4 + sizeof(ushort) * 6)
            });

            var transferDataPtr = SDL.MapGPUTransferBuffer(device, bufferTransferBuffer, false);
            unsafe
            {
                var vertices = (PositionTextureVertex*)transferDataPtr;
                vertices[0] = new PositionTextureVertex(-1, 1, 0, 0, 0);
                vertices[1] = new PositionTextureVertex(1, 1, 0, 1, 0);
                vertices[2] = new PositionTextureVertex(1, -1, 0, 1, 1);
                vertices[3] = new PositionTextureVertex(-1, -1, 0, 0, 1);

                var indexData = (ushort*)&vertices[4];
                indexData[0] = 0;
                indexData[1] = 1;
                indexData[2] = 2;
                indexData[3] = 0;
                indexData[4] = 2;
                indexData[5] = 3;
            }
            SDL.UnmapGPUTransferBuffer(device, bufferTransferBuffer);

            var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
            var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

            SDL.UploadToGPUBuffer(copyPass,
                new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = 0 },
                new SDL.GPUBufferRegion { Buffer = effectVertexBuffer, Offset = 0, Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4) },
                false);

            SDL.UploadToGPUBuffer(copyPass,
                new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4) },
                new SDL.GPUBufferRegion { Buffer = effectIndexBuffer, Offset = 0, Size = sizeof(ushort) * 6 },
                false);

            SDL.EndGPUCopyPass(copyPass);
            SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
            SDL.ReleaseGPUTransferBuffer(device, bufferTransferBuffer);
        }

        float time = 0;
        var lastTicks = SDL.GetTicks();

        // Main loop
        var running = true;
        while (running)
        {
            var currentTicks = SDL.GetTicks();
            float deltaTime = (currentTicks - lastTicks) / 1000.0f;
            lastTicks = currentTicks;
            time += deltaTime;

            while (SDL.PollEvent(out var evt))
            {
                switch ((SDL.EventType)evt.Type)
                {
                    case SDL.EventType.Quit:
                    case SDL.EventType.WindowCloseRequested:
                        running = false;
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
                // Render the 3D Scene (Color and Depth pass)
                float nearPlane = 20.0f;
                float farPlane = 60.0f;

                var proj = Matrix4x4.CreatePerspectiveFieldOfView(
                    75.0f * MathF.PI / 180.0f,
                    sceneWidth / (float)sceneHeight,
                    nearPlane,
                    farPlane);
                var view = Matrix4x4.CreateLookAt(
                    new Vector3(MathF.Cos(time) * 30, 30, MathF.Sin(time) * 30),
                    new Vector3(0, 0, 0),
                    new Vector3(0, 1, 0));

                var viewproj = Matrix4x4.Multiply(view, proj);

                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = sceneColorTexture,
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 0.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var depthStencilTargetInfo = new SDL.GPUDepthStencilTargetInfo
                {
                    Texture = sceneDepthTexture,
                    Cycle = 1,
                    ClearDepth = 1,
                    ClearStencil = 0,
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store,
                    StencilLoadOp = SDL.GPULoadOp.Clear,
                    StencilStoreOp = SDL.GPUStoreOp.Store
                };

                unsafe
                {
                    SDL.PushGPUVertexUniformData(commandBuffer, 0, (nint)(&viewproj), (uint)sizeof(Matrix4x4));
                    float[] depthParams = [nearPlane, farPlane];
                    fixed (float* depthParamsPtr = depthParams)
                    {
                        SDL.PushGPUFragmentUniformData(commandBuffer, 0, (nint)depthParamsPtr, 8);
                    }
                }

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var depthTargetPtr = SDL.StructureToPointer<SDL.GPUDepthStencilTargetInfo>(depthStencilTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, depthTargetPtr);
                Marshal.FreeHGlobal(colorTargetPtr);
                Marshal.FreeHGlobal(depthTargetPtr);

                var bufferBinding = new SDL.GPUBufferBinding { Buffer = sceneVertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                var indexBinding = new SDL.GPUBufferBinding { Buffer = sceneIndexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                SDL.BindGPUGraphicsPipeline(renderPass, scenePipeline);
                SDL.DrawGPUIndexedPrimitives(renderPass, 36, 1, 0, 0, 0);
                SDL.EndGPURenderPass(renderPass);

                // Render the Outline Effect that samples from the Color/Depth textures
                var swapchainTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0.2f, G = 0.5f, B = 0.4f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var swapTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(swapchainTargetInfo);
                renderPass = SDL.BeginGPURenderPass(commandBuffer, swapTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(swapTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, effectPipeline);

                bufferBinding = new SDL.GPUBufferBinding { Buffer = effectVertexBuffer, Offset = 0 };
                bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                indexBinding = new SDL.GPUBufferBinding { Buffer = effectIndexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                // Bind both color and depth texture samplers
                var samplerBindings = new SDL.GPUTextureSamplerBinding[]
                {
                    new() { Texture = sceneColorTexture, Sampler = effectSampler },
                    new() { Texture = sceneDepthTexture, Sampler = effectSampler }
                };
                var samplerBindSize = Marshal.SizeOf<SDL.GPUTextureSamplerBinding>();
                var samplerBindingsPtr = Marshal.AllocHGlobal(samplerBindSize * 2);
                for (int i = 0; i < 2; i++)
                    Marshal.StructureToPtr(samplerBindings[i], samplerBindingsPtr + i * samplerBindSize, false);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindingsPtr, 2);
                Marshal.FreeHGlobal(samplerBindingsPtr);

                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, scenePipeline);
        SDL.ReleaseGPUTexture(device, sceneColorTexture);
        SDL.ReleaseGPUTexture(device, sceneDepthTexture);
        SDL.ReleaseGPUBuffer(device, sceneVertexBuffer);
        SDL.ReleaseGPUBuffer(device, sceneIndexBuffer);

        SDL.ReleaseGPUGraphicsPipeline(device, effectPipeline);
        SDL.ReleaseGPUBuffer(device, effectVertexBuffer);
        SDL.ReleaseGPUBuffer(device, effectIndexBuffer);
        SDL.ReleaseGPUSampler(device, effectSampler);

        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }

    private static IntPtr CreateScenePipeline(IntPtr device, IntPtr vertexShader, IntPtr fragmentShader)
    {
        // Scene pipeline renders to R8G8B8A8Unorm offscreen texture (not swapchain format)
        var colorTargetDesc = new SDL.GPUColorTargetDescription
        {
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm
        };
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
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Ubyte4Norm, Location = 1, Offset = sizeof(float) * 3 }
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
            RasterizerState = new SDL.GPURasterizerState
            {
                CullMode = SDL.GPUCullMode.None,
                FillMode = SDL.GPUFillMode.Fill,
                FrontFace = SDL.GPUFrontFace.CounterClockwise
            },
            DepthStencilState = new SDL.GPUDepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                EnableStencilTest = false,
                CompareOp = SDL.GPUCompareOp.Less,
                WriteMask = 0xFF
            },
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
                HasDepthStencilTarget = true,
                DepthStencilFormat = SDL.GPUTextureFormat.D16Unorm
            }
        };

        var pipelineResult = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributesPtr);

        return pipelineResult;
    }
}
