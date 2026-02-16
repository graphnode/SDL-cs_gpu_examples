using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

[StructLayout(LayoutKind.Sequential)]
public struct FragMultiplyUniform
{
    public float R, G, B, A;

    public FragMultiplyUniform(float r, float g, float b, float a)
    {
        R = r; G = g; B = b; A = a;
    }
}

public static class TexturedAnimatedQuad
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
        var window = SDL.CreateWindow("TexturedAnimatedQuad", 640, 480, 0);
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
        var vertexShader = Common.LoadShader(device, "TexturedQuadWithMatrix.vert", 0, 1);
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "TexturedQuadWithMultiplyColor.frag", 1, 1);
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

        // Create the pipeline with alpha blending
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

        // Create texture
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)imgWidth,
            Height = (uint)imgHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        };
        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Create sampler
        var samplerInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge
        };
        var sampler = SDL.CreateGPUSampler(device, in samplerInfo);

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
            vertices[0] = new PositionTextureVertex(-0.5f, -0.5f, 0, 0, 0);
            vertices[1] = new PositionTextureVertex(0.5f, -0.5f, 0, 1, 0);
            vertices[2] = new PositionTextureVertex(0.5f, 0.5f, 0, 1, 1);
            vertices[3] = new PositionTextureVertex(-0.5f, 0.5f, 0, 0, 1);

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

        float t = 0;
        var lastTicks = SDL.GetTicks();

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

            // Update time
            var currentTicks = SDL.GetTicks();
            var deltaTime = (currentTicks - lastTicks) / 1000.0f;
            lastTicks = currentTicks;
            t += deltaTime;

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

                var texSamplerBinding = new SDL.GPUTextureSamplerBinding
                {
                    Texture = texture,
                    Sampler = sampler
                };
                var samplerBindPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(texSamplerBinding);
                SDL.BindGPUFragmentSamplers(renderPass, 0, samplerBindPtr, 1);
                Marshal.FreeHGlobal(samplerBindPtr);

                // Top-left
                var matrixUniform = Matrix4x4.Multiply(
                    Matrix4x4.CreateRotationZ(t),
                    Matrix4x4.CreateTranslation(-0.5f, -0.5f, 0)
                );
                var matrixPtr = SDL.StructureToPointer<Matrix4x4>(matrixUniform);
                SDL.PushGPUVertexUniformData(commandBuffer, 0, matrixPtr, (uint)Marshal.SizeOf<Matrix4x4>());
                Marshal.FreeHGlobal(matrixPtr);

                var fragUniform = new FragMultiplyUniform(1.0f, 0.5f + MathF.Sin(t) * 0.5f, 1.0f, 1.0f);
                var fragPtr = SDL.StructureToPointer<FragMultiplyUniform>(fragUniform);
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, fragPtr, (uint)Marshal.SizeOf<FragMultiplyUniform>());
                Marshal.FreeHGlobal(fragPtr);
                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);

                // Top-right
                matrixUniform = Matrix4x4.Multiply(
                    Matrix4x4.CreateRotationZ((2.0f * MathF.PI) - t),
                    Matrix4x4.CreateTranslation(0.5f, -0.5f, 0)
                );
                matrixPtr = SDL.StructureToPointer<Matrix4x4>(matrixUniform);
                SDL.PushGPUVertexUniformData(commandBuffer, 0, matrixPtr, (uint)Marshal.SizeOf<Matrix4x4>());
                Marshal.FreeHGlobal(matrixPtr);

                fragUniform = new FragMultiplyUniform(1.0f, 0.5f + MathF.Cos(t) * 0.5f, 1.0f, 1.0f);
                fragPtr = SDL.StructureToPointer<FragMultiplyUniform>(fragUniform);
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, fragPtr, (uint)Marshal.SizeOf<FragMultiplyUniform>());
                Marshal.FreeHGlobal(fragPtr);
                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);

                // Bottom-left
                matrixUniform = Matrix4x4.Multiply(
                    Matrix4x4.CreateRotationZ(t),
                    Matrix4x4.CreateTranslation(-0.5f, 0.5f, 0)
                );
                matrixPtr = SDL.StructureToPointer<Matrix4x4>(matrixUniform);
                SDL.PushGPUVertexUniformData(commandBuffer, 0, matrixPtr, (uint)Marshal.SizeOf<Matrix4x4>());
                Marshal.FreeHGlobal(matrixPtr);

                fragUniform = new FragMultiplyUniform(1.0f, 0.5f + MathF.Sin(t) * 0.2f, 1.0f, 1.0f);
                fragPtr = SDL.StructureToPointer<FragMultiplyUniform>(fragUniform);
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, fragPtr, (uint)Marshal.SizeOf<FragMultiplyUniform>());
                Marshal.FreeHGlobal(fragPtr);
                SDL.DrawGPUIndexedPrimitives(renderPass, 6, 1, 0, 0, 0);

                // Bottom-right
                matrixUniform = Matrix4x4.Multiply(
                    Matrix4x4.CreateRotationZ(t),
                    Matrix4x4.CreateTranslation(0.5f, 0.5f, 0)
                );
                matrixPtr = SDL.StructureToPointer<Matrix4x4>(matrixUniform);
                SDL.PushGPUVertexUniformData(commandBuffer, 0, matrixPtr, (uint)Marshal.SizeOf<Matrix4x4>());
                Marshal.FreeHGlobal(matrixPtr);

                fragUniform = new FragMultiplyUniform(1.0f, 0.5f + MathF.Cos(t) * 1.0f, 1.0f, 1.0f);
                fragPtr = SDL.StructureToPointer<FragMultiplyUniform>(fragUniform);
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, fragPtr, (uint)Marshal.SizeOf<FragMultiplyUniform>());
                Marshal.FreeHGlobal(fragPtr);
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
