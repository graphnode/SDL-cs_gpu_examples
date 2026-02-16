using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class BasicVertexBuffer
{
    public static int Main()
    {
        // Initialize SDL
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        // Create GPU device - request multiple shader formats for compatibility
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
        var window = SDL.CreateWindow("BasicVertexBuffer", 640, 480, 0);
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

        // Create pipeline
        var pipeline = CreatePipeline(device, window, vertexShader, fragmentShader);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        // Create vertex buffer
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 3)
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);
        if (vertexBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create vertex buffer: {SDL.GetError()}");
            return -1;
        }

        // Create transfer buffer to upload vertex data
        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 3)
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);
        if (transferBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create transfer buffer: {SDL.GetError()}");
            return -1;
        }

        // Map transfer buffer and write vertex data
        var transferDataPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var vertices = (PositionColorVertex*)transferDataPtr;
            vertices[0] = new PositionColorVertex(-1, -1, 0, 255, 0, 0, 255);   // Red
            vertices[1] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);    // Green
            vertices[2] = new PositionColorVertex(0, 1, 0, 0, 0, 255, 255);     // Blue
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        // Upload transfer data to vertex buffer
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var transferLocation = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = transferBuffer,
            Offset = 0
        };
        var bufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = vertexBuffer,
            Offset = 0,
            Size = (uint)(Marshal.SizeOf<PositionColorVertex>() * 3)
        };
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

                // Bind vertex buffer
                var bufferBinding = new SDL.GPUBufferBinding
                {
                    Buffer = vertexBuffer,
                    Offset = 0
                };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }

    private static IntPtr CreatePipeline(IntPtr device, IntPtr window, IntPtr vertexShader, IntPtr fragmentShader)
    {
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);

        // Color target description
        var colorTargetDesc = new SDL.GPUColorTargetDescription
        {
            Format = swapchainFormat
        };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        // Vertex buffer description
        var vertexBufferDesc = new SDL.GPUVertexBufferDescription
        {
            Slot = 0,
            InputRate = SDL.GPUVertexInputRate.Vertex,
            InstanceStepRate = 0,
            Pitch = (uint)Marshal.SizeOf<PositionColorVertex>()
        };
        var vertexBufferDescPtr = SDL.StructureToPointer<SDL.GPUVertexBufferDescription>(vertexBufferDesc);

        // Vertex attributes
        var vertexAttributes = new SDL.GPUVertexAttribute[]
        {
            new()
            {
                BufferSlot = 0,
                Format = SDL.GPUVertexElementFormat.Float3,
                Location = 0,
                Offset = 0
            },
            new()
            {
                BufferSlot = 0,
                Format = SDL.GPUVertexElementFormat.Ubyte4Norm,
                Location = 1,
                Offset = sizeof(float) * 3
            }
        };
        var attrSize = Marshal.SizeOf<SDL.GPUVertexAttribute>();
        var vertexAttributesPtr = Marshal.AllocHGlobal(attrSize * vertexAttributes.Length);
        for (var i = 0; i < vertexAttributes.Length; i++)
        {
            Marshal.StructureToPtr(vertexAttributes[i], vertexAttributesPtr + (i * attrSize), false);
        }

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
