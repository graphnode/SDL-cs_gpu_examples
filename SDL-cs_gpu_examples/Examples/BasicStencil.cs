using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class BasicStencil
{
    public static int Main()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        var device = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            true,
            null);
        if (device == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU device: {SDL.GetError()}");
            return -1;
        }

        var window = SDL.CreateWindow("BasicStencil", 640, 480, 0);
        if (window == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window: {SDL.GetError()}");
            return -1;
        }

        if (!SDL.ClaimWindowForGPUDevice(device, window))
        {
            Console.WriteLine($"Failed to claim window: {SDL.GetError()}");
            return -1;
        }

        // Load shaders
        var vertexShader = Common.LoadShader(device, "PositionColor.vert");
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "SolidColor.frag");
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load fragment shader!");
            return -1;
        }

        // Choose a depth-stencil format
        SDL.GPUTextureFormat depthStencilFormat;
        if (SDL.GPUTextureSupportsFormat(
            device,
            SDL.GPUTextureFormat.D24UnormS8Uint,
            SDL.GPUTextureType.TextureType2D,
            SDL.GPUTextureUsageFlags.DepthStencilTarget))
        {
            depthStencilFormat = SDL.GPUTextureFormat.D24UnormS8Uint;
        }
        else if (SDL.GPUTextureSupportsFormat(
            device,
            SDL.GPUTextureFormat.D32FloatS8Uint,
            SDL.GPUTextureType.TextureType2D,
            SDL.GPUTextureUsageFlags.DepthStencilTarget))
        {
            depthStencilFormat = SDL.GPUTextureFormat.D32FloatS8Uint;
        }
        else
        {
            Console.WriteLine("Stencil formats not supported!");
            return -1;
        }

        // Set up vertex input state
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
            new() { BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Ubyte4Norm, Location = 1, Offset = sizeof(float) * 3 }
        };
        var attrSize = Marshal.SizeOf<SDL.GPUVertexAttribute>();
        var vertexAttributesPtr = Marshal.AllocHGlobal(attrSize * vertexAttributes.Length);
        for (var i = 0; i < vertexAttributes.Length; i++)
            Marshal.StructureToPtr(vertexAttributes[i], vertexAttributesPtr + i * attrSize, false);

        // Create masker pipeline (writes to stencil buffer)
        var maskerPipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
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
                EnableStencilTest = true,
                FrontStencilState = new SDL.GPUStencilOpState
                {
                    CompareOp = SDL.GPUCompareOp.Never,
                    FailOp = SDL.GPUStencilOp.Replace,
                    PassOp = SDL.GPUStencilOp.Keep,
                    DepthFailOp = SDL.GPUStencilOp.Keep
                },
                BackStencilState = new SDL.GPUStencilOpState
                {
                    CompareOp = SDL.GPUCompareOp.Never,
                    FailOp = SDL.GPUStencilOp.Replace,
                    PassOp = SDL.GPUStencilOp.Keep,
                    DepthFailOp = SDL.GPUStencilOp.Keep
                },
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
                DepthStencilFormat = depthStencilFormat
            }
        };

        var maskerPipeline = SDL.CreateGPUGraphicsPipeline(device, in maskerPipelineCreateInfo);
        if (maskerPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create masker pipeline!");
            return -1;
        }

        // Create maskee pipeline (reads from stencil buffer)
        var maskeePipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
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
                EnableStencilTest = true,
                FrontStencilState = new SDL.GPUStencilOpState
                {
                    CompareOp = SDL.GPUCompareOp.Equal,
                    FailOp = SDL.GPUStencilOp.Keep,
                    PassOp = SDL.GPUStencilOp.Keep,
                    DepthFailOp = SDL.GPUStencilOp.Keep
                },
                BackStencilState = new SDL.GPUStencilOpState
                {
                    CompareOp = SDL.GPUCompareOp.Never,
                    FailOp = SDL.GPUStencilOp.Keep,
                    PassOp = SDL.GPUStencilOp.Keep,
                    DepthFailOp = SDL.GPUStencilOp.Keep
                },
                CompareMask = 0xFF,
                WriteMask = 0
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
                DepthStencilFormat = depthStencilFormat
            }
        };

        var maskeePipeline = SDL.CreateGPUGraphicsPipeline(device, in maskeePipelineCreateInfo);
        if (maskeePipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create maskee pipeline!");
            return -1;
        }

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributesPtr);

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create vertex buffer (6 vertices: 3 for masker triangle, 3 for maskee triangle)
        var vertexSize = (uint)Marshal.SizeOf<PositionColorVertex>();
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexSize * 6
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        // Create depth-stencil texture
        SDL.GetWindowSizeInPixels(window, out int w, out int h);

        var depthStencilTextureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Width = (uint)w,
            Height = (uint)h,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
            Format = depthStencilFormat,
            Usage = SDL.GPUTextureUsageFlags.DepthStencilTarget
        };
        var depthStencilTexture = SDL.CreateGPUTexture(device, in depthStencilTextureCreateInfo);

        // Upload vertex data
        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = vertexSize * 6
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var vertices = (PositionColorVertex*)transferDataPtr;
            // Masker triangle (small yellow)
            vertices[0] = new PositionColorVertex(-0.5f, -0.5f, 0, 255, 255, 0, 255);
            vertices[1] = new PositionColorVertex(0.5f, -0.5f, 0, 255, 255, 0, 255);
            vertices[2] = new PositionColorVertex(0, 0.5f, 0, 255, 255, 0, 255);
            // Maskee triangle (full-size RGB)
            vertices[3] = new PositionColorVertex(-1, -1, 0, 255, 0, 0, 255);
            vertices[4] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[5] = new PositionColorVertex(0, 1, 0, 0, 0, 255, 255);
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var transferLocation = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var bufferRegion = new SDL.GPUBufferRegion { Buffer = vertexBuffer, Offset = 0, Size = vertexSize * 6 };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation, in bufferRegion, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, transferBuffer);

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
                }
            }

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
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var depthStencilTargetInfo = new SDL.GPUDepthStencilTargetInfo
                {
                    Texture = depthStencilTexture,
                    Cycle = 1,
                    ClearDepth = 0,
                    ClearStencil = 0,
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.DontCare,
                    StencilLoadOp = SDL.GPULoadOp.Clear,
                    StencilStoreOp = SDL.GPUStoreOp.DontCare
                };

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var dsPtr = SDL.StructureToPointer<SDL.GPUDepthStencilTargetInfo>(depthStencilTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, dsPtr);
                Marshal.FreeHGlobal(colorTargetPtr);
                Marshal.FreeHGlobal(dsPtr);

                // Bind vertex buffer
                var bufferBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                // Draw masker (small yellow triangle writes stencil value 1)
                SDL.SetGPUStencilReference(renderPass, 1);
                SDL.BindGPUGraphicsPipeline(renderPass, maskerPipeline);
                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);

                // Draw maskee (full RGB triangle only where stencil == 0, but ref is 0)
                SDL.SetGPUStencilReference(renderPass, 0);
                SDL.BindGPUGraphicsPipeline(renderPass, maskeePipeline);
                SDL.DrawGPUPrimitives(renderPass, 3, 1, 3, 0);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, maskeePipeline);
        SDL.ReleaseGPUGraphicsPipeline(device, maskerPipeline);
        SDL.ReleaseGPUTexture(device, depthStencilTexture);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
