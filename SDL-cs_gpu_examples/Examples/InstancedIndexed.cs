using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class InstancedIndexed
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

        var window = SDL.CreateWindow("InstancedIndexed", 640, 480, 0);
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
        var vertexShader = Common.LoadShader(device, "PositionColorInstanced.vert");
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
        var pipeline = Common.CreatePositionColorPipeline(device, window, vertexShader, fragmentShader);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create vertex buffer (9 vertices: 3 sets of triangle colors)
        var vertexSize = (uint)Marshal.SizeOf<PositionColorVertex>();
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexSize * 9
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        // Create index buffer (6 indices)
        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 6
        };
        var indexBuffer = SDL.CreateGPUBuffer(device, in indexBufferCreateInfo);

        // Create transfer buffer for both
        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = vertexSize * 9 + sizeof(ushort) * 6
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var vertices = (PositionColorVertex*)transferDataPtr;
            // First triangle colors (RGB)
            vertices[0] = new PositionColorVertex(-1, -1, 0, 255, 0, 0, 255);
            vertices[1] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[2] = new PositionColorVertex(0, 1, 0, 0, 0, 255, 255);
            // Second triangle colors (orange, green, cyan)
            vertices[3] = new PositionColorVertex(-1, -1, 0, 255, 165, 0, 255);
            vertices[4] = new PositionColorVertex(1, -1, 0, 0, 128, 0, 255);
            vertices[5] = new PositionColorVertex(0, 1, 0, 0, 255, 255, 255);
            // Third triangle colors (white)
            vertices[6] = new PositionColorVertex(-1, -1, 0, 255, 255, 255, 255);
            vertices[7] = new PositionColorVertex(1, -1, 0, 255, 255, 255, 255);
            vertices[8] = new PositionColorVertex(0, 1, 0, 255, 255, 255, 255);

            // Index data follows vertex data
            var indexData = (ushort*)((byte*)transferDataPtr + vertexSize * 9);
            for (ushort j = 0; j < 6; j++)
                indexData[j] = j;
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        // Upload to GPU
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var transferLocation1 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var bufferRegion1 = new SDL.GPUBufferRegion { Buffer = vertexBuffer, Offset = 0, Size = vertexSize * 9 };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation1, in bufferRegion1, false);

        var transferLocation2 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = vertexSize * 9 };
        var bufferRegion2 = new SDL.GPUBufferRegion { Buffer = indexBuffer, Offset = 0, Size = sizeof(ushort) * 6 };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation2, in bufferRegion2, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, transferBuffer);

        Console.WriteLine("Press Left to toggle vertex offset");
        Console.WriteLine("Press Right to toggle index offset");
        Console.WriteLine("Press Up to toggle index buffer usage");

        bool useVertexOffset = false;
        bool useIndexOffset = false;
        bool useIndexBuffer = true;

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
                            useVertexOffset = !useVertexOffset;
                            Console.WriteLine($"Using vertex offset: {useVertexOffset}");
                        }
                        else if (scancode == SDL.Scancode.Right)
                        {
                            useIndexOffset = !useIndexOffset;
                            Console.WriteLine($"Using index offset: {useIndexOffset}");
                        }
                        else if (scancode == SDL.Scancode.Up)
                        {
                            useIndexBuffer = !useIndexBuffer;
                            Console.WriteLine($"Using index buffer: {useIndexBuffer}");
                        }
                        break;
                }
            }

            uint vertexOffset = useVertexOffset ? 3u : 0u;
            uint indexOffset = useIndexOffset ? 3u : 0u;

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

                var bufferBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                if (useIndexBuffer)
                {
                    var indexBinding = new SDL.GPUBufferBinding { Buffer = indexBuffer, Offset = 0 };
                    SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                    SDL.DrawGPUIndexedPrimitives(renderPass, 3, 16, indexOffset, (int)vertexOffset, 0);
                }
                else
                {
                    SDL.DrawGPUPrimitives(renderPass, 3, 16, vertexOffset, 0);
                }

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseGPUBuffer(device, indexBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
