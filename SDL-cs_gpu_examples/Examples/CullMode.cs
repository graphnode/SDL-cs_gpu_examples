using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class CullMode
{
    private static readonly string[] ModeNames =
    [
        "CCW_CullNone",
        "CCW_CullFront",
        "CCW_CullBack",
        "CW_CullNone",
        "CW_CullFront",
        "CW_CullBack"
    ];

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

        var window = SDL.CreateWindow("CullMode", 640, 480, 0);
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

        // Create 6 pipelines (3 cull modes x 2 front faces)
        var pipelines = new IntPtr[6];

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

        for (int i = 0; i < 6; i++)
        {
            var cullMode = (SDL.GPUCullMode)(i % 3);
            var frontFace = (i > 2) ? SDL.GPUFrontFace.Clockwise : SDL.GPUFrontFace.CounterClockwise;

            var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
                RasterizerState = new SDL.GPURasterizerState
                {
                    CullMode = cullMode,
                    FrontFace = frontFace
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
                    NumColorTargets = 1
                }
            };

            pipelines[i] = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);
            if (pipelines[i] == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create pipeline!");
                return -1;
            }
        }

        Marshal.FreeHGlobal(colorTargetDescPtr);
        Marshal.FreeHGlobal(vertexBufferDescPtr);
        Marshal.FreeHGlobal(vertexAttributesPtr);

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create vertex buffers - CW and CCW winding orders
        var vertexSize = (uint)Marshal.SizeOf<PositionColorVertex>();
        var vertexBufferCWInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexSize * 3
        };
        var vertexBufferCW = SDL.CreateGPUBuffer(device, in vertexBufferCWInfo);

        var vertexBufferCCWInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexSize * 3
        };
        var vertexBufferCCW = SDL.CreateGPUBuffer(device, in vertexBufferCCWInfo);

        // Create transfer buffer for both vertex buffers
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
            // CW vertices
            vertices[0] = new PositionColorVertex(-1, -1, 0, 255, 0, 0, 255);
            vertices[1] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[2] = new PositionColorVertex(0, 1, 0, 0, 0, 255, 255);
            // CCW vertices
            vertices[3] = new PositionColorVertex(0, 1, 0, 255, 0, 0, 255);
            vertices[4] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[5] = new PositionColorVertex(-1, -1, 0, 0, 0, 255, 255);
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        // Upload to GPU
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var transferLocation1 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var bufferRegion1 = new SDL.GPUBufferRegion { Buffer = vertexBufferCW, Offset = 0, Size = vertexSize * 3 };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation1, in bufferRegion1, false);

        var transferLocation2 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = vertexSize * 3 };
        var bufferRegion2 = new SDL.GPUBufferRegion { Buffer = vertexBufferCCW, Offset = 0, Size = vertexSize * 3 };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation2, in bufferRegion2, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, transferBuffer);

        Console.WriteLine("Press Left/Right to switch between modes");
        Console.WriteLine($"Current Mode: {ModeNames[0]}");

        int currentMode = 0;

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
                        if (scancode == SDL.Scancode.Left)
                        {
                            currentMode -= 1;
                            if (currentMode < 0)
                                currentMode = pipelines.Length - 1;
                            Console.WriteLine($"Current Mode: {ModeNames[currentMode]}");
                        }
                        else if (scancode == SDL.Scancode.Right)
                        {
                            currentMode = (currentMode + 1) % pipelines.Length;
                            Console.WriteLine($"Current Mode: {ModeNames[currentMode]}");
                        }
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

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipelines[currentMode]);

                // Left half - CW winding
                var viewportLeft = new SDL.GPUViewport { X = 0, Y = 0, W = 320, H = 480 };
                SDL.SetGPUViewport(renderPass, in viewportLeft);

                var bindingCW = new SDL.GPUBufferBinding { Buffer = vertexBufferCW, Offset = 0 };
                var bindingCWPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bindingCW);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingCWPtr, 1);
                Marshal.FreeHGlobal(bindingCWPtr);

                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);

                // Right half - CCW winding
                var viewportRight = new SDL.GPUViewport { X = 320, Y = 0, W = 320, H = 480 };
                SDL.SetGPUViewport(renderPass, in viewportRight);

                var bindingCCW = new SDL.GPUBufferBinding { Buffer = vertexBufferCCW, Offset = 0 };
                var bindingCCWPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bindingCCW);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingCCWPtr, 1);
                Marshal.FreeHGlobal(bindingCCWPtr);

                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        for (int i = 0; i < pipelines.Length; i++)
            SDL.ReleaseGPUGraphicsPipeline(device, pipelines[i]);

        SDL.ReleaseGPUBuffer(device, vertexBufferCW);
        SDL.ReleaseGPUBuffer(device, vertexBufferCCW);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
