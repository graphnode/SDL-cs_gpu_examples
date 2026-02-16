using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class DrawIndirect
{
    // Match the SDL_GPUIndexedIndirectDrawCommand struct layout
    [StructLayout(LayoutKind.Sequential)]
    private struct IndexedIndirectDrawCommand
    {
        public uint NumIndices;
        public uint NumInstances;
        public uint FirstIndex;
        public int VertexOffset;
        public uint FirstInstance;
    }

    // Match the SDL_GPUIndirectDrawCommand struct layout
    [StructLayout(LayoutKind.Sequential)]
    private struct IndirectDrawCommand
    {
        public uint NumVertices;
        public uint NumInstances;
        public uint FirstVertex;
        public uint FirstInstance;
    }

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

        var window = SDL.CreateWindow("DrawIndirect", 640, 480, 0);
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

        // Create pipeline
        var pipeline = Common.CreatePositionColorPipeline(device, window, vertexShader, fragmentShader);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        // Create buffers
        var vertexSize = (uint)Marshal.SizeOf<PositionColorVertex>();
        uint vertexBufferSize = vertexSize * 10;

        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Vertex,
            Size = vertexBufferSize
        };
        var vertexBuffer = SDL.CreateGPUBuffer(device, in vertexBufferCreateInfo);

        uint indexBufferSize = sizeof(ushort) * 6;
        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Index,
            Size = indexBufferSize
        };
        var indexBuffer = SDL.CreateGPUBuffer(device, in indexBufferCreateInfo);

        uint drawBufferSize = (uint)(Marshal.SizeOf<IndexedIndirectDrawCommand>() + Marshal.SizeOf<IndirectDrawCommand>() * 2);
        var drawBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.Indirect,
            Size = drawBufferSize
        };
        var drawBuffer = SDL.CreateGPUBuffer(device, in drawBufferCreateInfo);

        // Create transfer buffer for all data
        var transferBufferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = vertexBufferSize + indexBufferSize + drawBufferSize
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferBufferCreateInfo);

        var transferDataPtr = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        unsafe
        {
            var vertices = (PositionColorVertex*)transferDataPtr;

            // Quad vertices (indexed draw)
            vertices[0] = new PositionColorVertex(-1, -1, 0, 255, 0, 0, 255);
            vertices[1] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[2] = new PositionColorVertex(1, 1, 0, 0, 0, 255, 255);
            vertices[3] = new PositionColorVertex(-1, 1, 0, 255, 255, 255, 255);

            // Triangle 1 vertices (non-indexed indirect draw)
            vertices[4] = new PositionColorVertex(1, -1, 0, 0, 255, 0, 255);
            vertices[5] = new PositionColorVertex(0, -1, 0, 0, 0, 255, 255);
            vertices[6] = new PositionColorVertex(0.5f, 1, 0, 255, 0, 0, 255);

            // Triangle 2 vertices (non-indexed indirect draw)
            vertices[7] = new PositionColorVertex(-1, -1, 0, 0, 255, 0, 255);
            vertices[8] = new PositionColorVertex(0, -1, 0, 0, 0, 255, 255);
            vertices[9] = new PositionColorVertex(-0.5f, 1, 0, 255, 0, 0, 255);

            // Index data
            var indexData = (ushort*)((byte*)transferDataPtr + vertexBufferSize);
            indexData[0] = 0;
            indexData[1] = 1;
            indexData[2] = 2;
            indexData[3] = 0;
            indexData[4] = 2;
            indexData[5] = 3;

            // Indexed indirect draw command
            var indexedDrawCmd = (IndexedIndirectDrawCommand*)((byte*)transferDataPtr + vertexBufferSize + indexBufferSize);
            indexedDrawCmd[0] = new IndexedIndirectDrawCommand
            {
                NumIndices = 6,
                NumInstances = 1,
                FirstIndex = 0,
                VertexOffset = 0,
                FirstInstance = 0
            };

            // Non-indexed indirect draw commands
            var drawCmds = (IndirectDrawCommand*)((byte*)indexedDrawCmd + Marshal.SizeOf<IndexedIndirectDrawCommand>());
            drawCmds[0] = new IndirectDrawCommand
            {
                NumVertices = 3,
                NumInstances = 1,
                FirstVertex = 4,
                FirstInstance = 0
            };
            drawCmds[1] = new IndirectDrawCommand
            {
                NumVertices = 3,
                NumInstances = 1,
                FirstVertex = 7,
                FirstInstance = 0
            };
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        // Upload to GPU
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        var transferLoc1 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var bufRegion1 = new SDL.GPUBufferRegion { Buffer = vertexBuffer, Offset = 0, Size = vertexBufferSize };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc1, in bufRegion1, false);

        var transferLoc2 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = vertexBufferSize };
        var bufRegion2 = new SDL.GPUBufferRegion { Buffer = indexBuffer, Offset = 0, Size = indexBufferSize };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc2, in bufRegion2, false);

        var transferLoc3 = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = vertexBufferSize + indexBufferSize };
        var bufRegion3 = new SDL.GPUBufferRegion { Buffer = drawBuffer, Offset = 0, Size = drawBufferSize };
        SDL.UploadToGPUBuffer(copyPass, in transferLoc3, in bufRegion3, false);

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

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipeline);

                // Bind vertex buffer
                var bufferBinding = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
                var bindingPtr = SDL.StructureToPointer<SDL.GPUBufferBinding>(bufferBinding);
                SDL.BindGPUVertexBuffers(renderPass, 0, bindingPtr, 1);
                Marshal.FreeHGlobal(bindingPtr);

                // Bind index buffer
                var indexBinding = new SDL.GPUBufferBinding { Buffer = indexBuffer, Offset = 0 };
                SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize16Bit);

                // Indexed indirect draw (the quad)
                SDL.DrawGPUIndexedPrimitivesIndirect(renderPass, drawBuffer, 0, 1);

                // Non-indexed indirect draw (two triangles)
                SDL.DrawGPUPrimitivesIndirect(
                    renderPass,
                    drawBuffer,
                    (uint)Marshal.SizeOf<IndexedIndirectDrawCommand>(),
                    2);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseGPUBuffer(device, vertexBuffer);
        SDL.ReleaseGPUBuffer(device, indexBuffer);
        SDL.ReleaseGPUBuffer(device, drawBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
