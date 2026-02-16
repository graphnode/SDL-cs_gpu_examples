using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class Texture2DArray
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
        var window = SDL.CreateWindow("Texture2DArray", 640, 480, 0);
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
        var vertexShader = Common.LoadShader(device, "TexturedQuad.vert");
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "TexturedQuadArray.frag", 1);
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create fragment shader!");
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

        // Load the images
        var imageData1 = Common.LoadImage(device, "ravioli.bmp", out var imgWidth1, out var imgHeight1);
        if (imageData1 == IntPtr.Zero)
        {
            Console.WriteLine("Could not load first image data!");
            return -1;
        }

        var imageData2 = Common.LoadImage(device, "ravioli_inverted.bmp", out int _, out int _);
        if (imageData2 == IntPtr.Zero)
        {
            Console.WriteLine("Could not load second image data!");
            return -1;
        }

        // Create the GPU resources
        var vertexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        });

        var indexBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = sizeof(ushort) * 6
        });

        var texture = SDL.CreateGPUTexture(device, new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2DArray,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)imgWidth1,
            Height = (uint)imgHeight1,
            LayerCountOrDepth = 2,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
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

        // Set up texture data
        uint imageSizeInBytes = (uint)(imgWidth1 * imgHeight1 * 4);
        var textureTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = imageSizeInBytes * 2
        });

        var textureTransferPtr = SDL.MapGPUTransferBuffer(device, textureTransferBuffer, false);
        unsafe
        {
            var surfacePtr1 = (SDL.Surface*)imageData1;
            var surfacePtr2 = (SDL.Surface*)imageData2;
            Buffer.MemoryCopy((void*)surfacePtr1->Pixels, (void*)textureTransferPtr, imageSizeInBytes, imageSizeInBytes);
            Buffer.MemoryCopy((void*)surfacePtr2->Pixels, (void*)(textureTransferPtr + (nint)imageSizeInBytes), imageSizeInBytes, imageSizeInBytes);
        }
        SDL.UnmapGPUTransferBuffer(device, textureTransferBuffer);

        // Upload the transfer data to the GPU resources
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        // Upload vertex buffer
        SDL.UploadToGPUBuffer(copyPass,
            new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = 0 },
            new SDL.GPUBufferRegion { Buffer = vertexBuffer, Offset = 0, Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4) },
            false);

        // Upload index buffer
        SDL.UploadToGPUBuffer(copyPass,
            new SDL.GPUTransferBufferLocation { TransferBuffer = bufferTransferBuffer, Offset = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4) },
            new SDL.GPUBufferRegion { Buffer = indexBuffer, Offset = 0, Size = sizeof(ushort) * 6 },
            false);

        // Upload texture layer 0
        SDL.UploadToGPUTexture(copyPass,
            new SDL.GPUTextureTransferInfo { TransferBuffer = textureTransferBuffer, Offset = 0 },
            new SDL.GPUTextureRegion { Texture = texture, W = (uint)imgWidth1, H = (uint)imgHeight1, D = 1 },
            false);

        // Upload texture layer 1
        SDL.UploadToGPUTexture(copyPass,
            new SDL.GPUTextureTransferInfo { TransferBuffer = textureTransferBuffer, Offset = imageSizeInBytes },
            new SDL.GPUTextureRegion { Texture = texture, Layer = 1, W = (uint)imgWidth1, H = (uint)imgHeight1, D = 1 },
            false);

        SDL.DestroySurface(imageData1);
        SDL.DestroySurface(imageData2);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);
        SDL.ReleaseGPUTransferBuffer(device, bufferTransferBuffer);
        SDL.ReleaseGPUTransferBuffer(device, textureTransferBuffer);

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
        SDL.ReleaseGPUSampler(device, sampler);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
