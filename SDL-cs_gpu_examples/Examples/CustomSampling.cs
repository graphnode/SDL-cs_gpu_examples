using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class CustomSampling
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
        var window = SDL.CreateWindow("CustomSampling", 640, 480, 0);
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
        // CustomSampling.frag uses 0 samplers, 1 uniform buffer, 0 storage buffers, 1 storage texture
        var vertexShader = Common.LoadShader(device, "TexturedQuad.vert");
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "CustomSampling.frag", 0, 1, 0, 1);
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load fragment shader!");
            return -1;
        }

        // Load the image
        var surface = Common.LoadImage(device, "ravioli.bmp", out var imgWidth, out var imgHeight);
        if (surface == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        // Create the pipeline
        var pipeline = Common.CreatePositionTexturePipeline(device, window, vertexShader, fragmentShader);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create vertex buffer
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        // Create index buffer
        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 6
        };
        var indexBuffer = SDL.CreateGPUBuffer(device, in indexBufferCreateInfo);

        // Create texture with GraphicsStorageRead usage for storage texture access in shader
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)imgWidth,
            Height = (uint)imgHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.GraphicsStorageRead
        };
        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Set up buffer data
        var bufferTransferSize = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4 + sizeof(ushort) * 6);
        var bufferTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = bufferTransferSize
        };
        var bufferTransferBuffer = SDL.CreateGPUTransferBuffer(device, in bufferTransferCreateInfo);

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

        // Upload texture from surface
        Common.UploadTextureFromSurface(device, texture, surface, imgWidth, imgHeight);
        SDL.DestroySurface(surface);

        // Upload buffer data
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var vertTransferLoc = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = bufferTransferBuffer,
            Offset = 0
        };
        var vertBufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = vertexBuffer,
            Offset = 0,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        };
        SDL.UploadToGPUBuffer(copyPass, in vertTransferLoc, in vertBufferRegion, false);

        var idxTransferLoc = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = bufferTransferBuffer,
            Offset = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        };
        var idxBufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = indexBuffer,
            Offset = 0,
            Size = sizeof(ushort) * 6
        };
        SDL.UploadToGPUBuffer(copyPass, in idxTransferLoc, in idxBufferRegion, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, bufferTransferBuffer);

        Console.WriteLine("Press Left/Right to switch sampler modes");

        var samplerMode = 0;

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
                            samplerMode = samplerMode == 0 ? 1 : 0;
                            Console.WriteLine($"Setting sampler mode to: {samplerMode}");
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

                // Bind storage texture
                SDL.BindGPUFragmentStorageTextures(renderPass, 0, [texture], 1);

                // Push sampler mode uniform
                var modePtr = SDL.StructureToPointer<int>(samplerMode);
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, modePtr, sizeof(int));
                Marshal.FreeHGlobal(modePtr);

                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseGPUBuffer(device, indexBuffer);
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
