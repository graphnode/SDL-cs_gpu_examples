using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class CopyConsistency
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
        var window = SDL.CreateWindow("CopyConsistency", 640, 480, 0);
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

        // Create the pipeline with alpha blending
        SDL.GetGPUSwapchainTextureFormat(device, window);
        var blendState = new SDL.GPUColorTargetBlendState
        {
            EnableBlend = true,
            AlphaBlendOp = SDL.GPUBlendOp.Add,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcColorBlendFactor = SDL.GPUBlendFactor.SrcAlpha,
            SrcAlphaBlendFactor = SDL.GPUBlendFactor.SrcAlpha,
            DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha
        };
        var pipeline = Common.CreatePositionTexturePipeline(device, window, vertexShader, fragmentShader, blendState);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create textures (16x16 as in original)
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = 16,
            Height = 16,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        };
        var leftTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);
        var rightTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);
        var activeTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Load image data
        var leftSurface = Common.LoadImage(device, "ravioli.bmp", out int leftW, out int leftH);
        if (leftSurface == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        var rightSurface = Common.LoadImage(device, "ravioli_inverted.bmp", out int rightW, out int rightH);
        if (rightSurface == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        // Create sampler
        var samplerCreateInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        var sampler = SDL.CreateGPUSampler(device, in samplerCreateInfo);

        // Create buffers
        uint vertexSize = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4);
        uint indexSize = sizeof(ushort) * 6;

        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexSize
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);
        var leftVertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);
        var rightVertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = indexSize
        };
        var indexBuffer = SDL.CreateGPUBuffer(device, in indexBufferCreateInfo);

        // Set up vertex + index buffer data
        uint bufferUploadSize = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 8) + indexSize;
        var bufferTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = bufferUploadSize
        };
        var bufferTransferBuffer = SDL.CreateGPUTransferBuffer(device, in bufferTransferCreateInfo);

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, bufferTransferBuffer, false);
        unsafe
        {
            var vertices = (PositionTextureVertex*)transferDataPtr;
            // Left quad
            vertices[0] = new PositionTextureVertex(-1.0f,  1.0f, 0, 0, 0);
            vertices[1] = new PositionTextureVertex( 0.0f,  1.0f, 0, 1, 0);
            vertices[2] = new PositionTextureVertex( 0.0f, -1.0f, 0, 1, 1);
            vertices[3] = new PositionTextureVertex(-1.0f, -1.0f, 0, 0, 1);

            // Right quad
            vertices[4] = new PositionTextureVertex( 0.0f,  1.0f, 0, 0, 0);
            vertices[5] = new PositionTextureVertex( 1.0f,  1.0f, 0, 1, 0);
            vertices[6] = new PositionTextureVertex( 1.0f, -1.0f, 0, 1, 1);
            vertices[7] = new PositionTextureVertex( 0.0f, -1.0f, 0, 0, 1);

            // Index data
            var indexData = (ushort*)&vertices[8];
            indexData[0] = 0;
            indexData[1] = 1;
            indexData[2] = 2;
            indexData[3] = 0;
            indexData[4] = 2;
            indexData[5] = 3;
        }
        SDL.UnmapGPUTransferBuffer(device, bufferTransferBuffer);

        // Set up texture data
        uint textureDataSize = (uint)(leftW * leftH * 4);
        var textureTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = textureDataSize * 2
        };
        var textureTransferBuffer = SDL.CreateGPUTransferBuffer(device, in textureTransferCreateInfo);

        var texTransferPtr = SDL.MapGPUTransferBuffer(device, textureTransferBuffer, false);
        unsafe
        {
            var leftSurfPtr = (SDL.Surface*)leftSurface;
            var rightSurfPtr = (SDL.Surface*)rightSurface;
            Buffer.MemoryCopy((void*)leftSurfPtr->Pixels, (void*)texTransferPtr, textureDataSize, textureDataSize);
            Buffer.MemoryCopy((void*)rightSurfPtr->Pixels, (void*)(texTransferPtr + (int)textureDataSize), textureDataSize, textureDataSize);
        }
        SDL.UnmapGPUTransferBuffer(device, textureTransferBuffer);

        // Upload everything
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        // Upload left vertex buffer
        var transferLoc = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = bufferTransferBuffer,
            Offset = 0
        };
        var bufRegion = new SDL.GPUBufferRegion
        {
            Buffer = leftVertexBuffer,
            Offset = 0,
            Size = vertexSize
        };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc, in bufRegion, false);

        // Upload right vertex buffer
        transferLoc = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = bufferTransferBuffer,
            Offset = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 4)
        };
        bufRegion = new SDL.GPUBufferRegion
        {
            Buffer = rightVertexBuffer,
            Offset = 0,
            Size = vertexSize
        };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc, in bufRegion, false);

        // Upload index buffer
        transferLoc = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = bufferTransferBuffer,
            Offset = (uint)(Marshal.SizeOf<PositionTextureVertex>() * 8)
        };
        bufRegion = new SDL.GPUBufferRegion
        {
            Buffer = indexBuffer,
            Offset = 0,
            Size = indexSize
        };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc, in bufRegion, false);

        // Upload left texture
        var texTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = textureTransferBuffer,
            Offset = 0
        };
        var texRegion = new SDL.GPUTextureRegion
        {
            Texture = leftTexture,
            W = (uint)leftW,
            H = (uint)leftH,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in texTransferInfo, in texRegion, false);

        // Upload right texture
        texTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = textureTransferBuffer,
            Offset = (uint)(leftW * leftW * 4) // matches C original: leftImageData->w * leftImageData->w * 4
        };
        texRegion = new SDL.GPUTextureRegion
        {
            Texture = rightTexture,
            W = (uint)rightW,
            H = (uint)rightH,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in texTransferInfo, in texRegion, false);

        SDL.DestroySurface(leftSurface);
        SDL.DestroySurface(rightSurface);
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
                    ClearColor = new SDL.FColor { R = 0, G = 0, B = 0, A = 1 },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                // Copy left-side resources
                copyPass = SDL.BeginGPUCopyPass(commandBuffer);
                var srcBufLoc = new SDL.GPUBufferLocation { Buffer = leftVertexBuffer };
                var dstBufLoc = new SDL.GPUBufferLocation { Buffer = vertexBuffer };
                SDL.CopyGPUBufferToBuffer(copyPass, in srcBufLoc, in dstBufLoc, vertexSize, false);

                var srcTexLoc = new SDL.GPUTextureLocation { Texture = leftTexture };
                var dstTexLoc = new SDL.GPUTextureLocation { Texture = activeTexture };
                SDL.CopyGPUTextureToTexture(copyPass, in srcTexLoc, in dstTexLoc, 16, 16, 1, false);
                SDL.EndGPUCopyPass(copyPass);

                // Draw left side
                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipeline);

                var vertBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var vertBindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(vertBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, vertBindingPtr, 1);
                Marshal.FreeHGlobal(vertBindingPtr);

                var idxBinding = new SDL.GPUBufferBinding { Buffer = indexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in idxBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                var samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = activeTexture, Sampler = sampler };
                var samplerBindingPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(samplerBinding);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindingPtr, 1);
                Marshal.FreeHGlobal(samplerBindingPtr);

                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);
                SDL.EndGPURenderPass(renderPass);

                // Copy right-side resources
                copyPass = SDL.BeginGPUCopyPass(commandBuffer);
                srcBufLoc = new SDL.GPUBufferLocation { Buffer = rightVertexBuffer };
                dstBufLoc = new SDL.GPUBufferLocation { Buffer = vertexBuffer };
                SDL.CopyGPUBufferToBuffer(copyPass, in srcBufLoc, in dstBufLoc, vertexSize, false);

                srcTexLoc = new SDL.GPUTextureLocation { Texture = rightTexture };
                dstTexLoc = new SDL.GPUTextureLocation { Texture = activeTexture };
                SDL.CopyGPUTextureToTexture(copyPass, in srcTexLoc, in dstTexLoc, 16, 16, 1, false);
                SDL.EndGPUCopyPass(copyPass);

                // Draw right side (load existing contents)
                colorTargetInfo.LoadOp = SDL.GPULoadOp.Load;
                colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipeline);

                vertBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                vertBindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(vertBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, vertBindingPtr, 1);
                Marshal.FreeHGlobal(vertBindingPtr);

                idxBinding = new SDL.GPUBufferBinding { Buffer = indexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in idxBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = activeTexture, Sampler = sampler };
                samplerBindingPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(samplerBinding);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindingPtr, 1);
                Marshal.FreeHGlobal(samplerBindingPtr);

                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseGPUBuffer(device, leftVertexBuffer);
        SDL.ReleaseGPUBuffer(device, rightVertexBuffer);
        SDL.ReleaseGPUBuffer(device, indexBuffer);
        SDL.ReleaseGPUTexture(device, activeTexture);
        SDL.ReleaseGPUTexture(device, leftTexture);
        SDL.ReleaseGPUTexture(device, rightTexture);
        SDL.ReleaseGPUSampler(device, sampler);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
