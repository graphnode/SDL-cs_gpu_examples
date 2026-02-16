using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class BasicCompute
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
        var window = SDL.CreateWindow("BasicCompute", 640, 480, 0);
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

        // Load shaders for the draw pipeline
        var vertexShader = Common.LoadShader(device, "TexturedQuad.vert", samplerCount: 0);
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "TexturedQuad.frag", samplerCount: 1);
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load fragment shader!");
            return -1;
        }

        // Create the compute pipeline to fill the texture
        var computeMetadata = new ShaderCross.ComputePipelineMetadata
        {
            NumSamplers = 0,
            NumReadOnlyStorageTextures = 0,
            NumReadOnlyStorageBuffers = 0,
            NumReadWriteStorageTextures = 1,
            NumUniformBuffers = 0,
            ThreadCountX = 8,
            ThreadCountY = 8,
            ThreadCountZ = 1,
        };
        var fillTexturePipeline = Common.CreateComputePipelineFromShader(device, "FillTexture.comp", computeMetadata);
        if (fillTexturePipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create compute pipeline!");
            return -1;
        }

        // Create the draw pipeline
        var drawPipeline = Common.CreatePositionTexturePipeline(device, window, vertexShader, fragmentShader);
        if (drawPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create draw pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Get window size
        SDL.GetWindowSizeInPixels(window, out int w, out int h);

        // Create texture for compute output
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)w,
            Height = (uint)h,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.ComputeStorageWrite | SDL.GPUTextureUsageFlags.Sampler
        };
        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Create sampler
        var samplerCreateInfo = new SDL.GPUSamplerCreateInfo
        {
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat
        };
        var sampler = SDL.CreateGPUSampler(device, in samplerCreateInfo);

        // Create vertex buffer for fullscreen quad
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 6)
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        // Upload vertex data
        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 6)
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var vertices = (PositionTextureVertex*)transferDataPtr;
            vertices[0] = new PositionTextureVertex(-1, -1, 0, 0, 0);
            vertices[1] = new PositionTextureVertex( 1, -1, 0, 1, 0);
            vertices[2] = new PositionTextureVertex( 1,  1, 0, 1, 1);
            vertices[3] = new PositionTextureVertex(-1, -1, 0, 0, 0);
            vertices[4] = new PositionTextureVertex( 1,  1, 0, 1, 1);
            vertices[5] = new PositionTextureVertex(-1,  1, 0, 0, 1);
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        var cmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(cmdBuf);

        var transferLocation = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = transferBuffer,
            Offset = 0
        };
        var bufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = vertexBuffer,
            Offset = 0,
            Size = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 6)
        };
        SDL.UploadToGPUBuffer(copyPass, in transferLocation, in bufferRegion, false);

        SDL.EndGPUCopyPass(copyPass);

        // Run compute pass to fill the texture
        SDL.GPUStorageTextureReadWriteBinding[] storageTexBindings =
        [
            new() { Texture = texture }
        ];
        var computePass = SDL.BeginGPUComputePass(cmdBuf, storageTexBindings, 1, [], 0);

        SDL.BindGPUComputePipeline(computePass, fillTexturePipeline);
        SDL.DispatchGPUCompute(computePass, (uint)(w / 8), (uint)(h / 8), 1);
        SDL.EndGPUComputePass(computePass);

        SDL.SubmitGPUCommandBuffer(cmdBuf);

        SDL.ReleaseGPUComputePipeline(device, fillTexturePipeline);
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
                    ClearColor = new SDL.FColor { R = 0, G = 0, B = 0, A = 1 },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, drawPipeline);

                var bufferBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                var samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture, Sampler = sampler };
                var samplerBindingPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(samplerBinding);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindingPtr, 1);
                Marshal.FreeHGlobal(samplerBindingPtr);

                SDL.DrawGPUPrimitives(renderPass, 6, 1, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, drawPipeline);
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseGPUSampler(device, sampler);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
