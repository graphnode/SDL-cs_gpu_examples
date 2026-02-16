using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class BasicTriangle
{
    private static readonly SDL.GPUViewport SmallViewport = new()
    {
        X = 160, Y = 120, W = 320, H = 240, MinDepth = 0.1f, MaxDepth = 1.0f
    };

    private static readonly SDL.Rect ScissorRect = new()
    {
        X = 320, Y = 240, W = 320, H = 240
    };

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
        var window = SDL.CreateWindow("BasicTriangle", 640, 480, 0);
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
        var vertexShader = Common.LoadShader(device, "RawTriangle.vert");
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

        // Create pipelines
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);

        var colorTargetDesc = new SDL.GPUColorTargetDescription
        {
            Format = swapchainFormat
        };

        var fillPipeline = CreatePipeline(device, vertexShader, fragmentShader, colorTargetDesc, SDL.GPUFillMode.Fill);
        if (fillPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create fill pipeline!");
            return -1;
        }

        var linePipeline = CreatePipeline(device, vertexShader, fragmentShader, colorTargetDesc, SDL.GPUFillMode.Line);
        if (linePipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create line pipeline!");
            return -1;
        }

        // Print instructions
        Console.WriteLine("Press Left to toggle wireframe mode");
        Console.WriteLine("Press Down to toggle small viewport");
        Console.WriteLine("Press Right to toggle scissor rect");

        // State
        var useWireframeMode = false;
        var useSmallViewport = false;
        var useScissorRect = false;

        // Main loop
        var running = true;
        while (running)
        {
            // Process events
            while (SDL.PollEvent(out var evt))
            {
                switch ((SDL.EventType)evt.Type)
                {
                    case SDL.EventType.Quit:
                        running = false;
                        break;
                    case SDL.EventType.WindowCloseRequested:
                        running = false;
                        break;
                    case SDL.EventType.KeyDown:
                        var scancode = evt.Key.Scancode;
                        if (scancode == SDL.Scancode.Left)
                        {
                            useWireframeMode = !useWireframeMode;
                            Console.WriteLine($"Wireframe mode: {useWireframeMode}");
                        }
                        else if (scancode == SDL.Scancode.Down)
                        {
                            useSmallViewport = !useSmallViewport;
                            Console.WriteLine($"Small viewport: {useSmallViewport}");
                        }
                        else if (scancode == SDL.Scancode.Right)
                        {
                            useScissorRect = !useScissorRect;
                            Console.WriteLine($"Scissor rect: {useScissorRect}");
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

                SDL.BindGPUGraphicsPipeline(renderPass, useWireframeMode ? linePipeline : fillPipeline);

                if (useSmallViewport)
                {
                    var viewport = SmallViewport;
                    SDL.SetGPUViewport(renderPass, in viewport);
                }

                if (useScissorRect)
                {
                    var scissor = ScissorRect;
                    SDL.SetGPUScissor(renderPass, in scissor);
                }

                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, fillPipeline);
        SDL.ReleaseGPUGraphicsPipeline(device, linePipeline);
        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }

    private static IntPtr CreatePipeline(IntPtr device, IntPtr vertexShader, IntPtr fragmentShader, SDL.GPUColorTargetDescription colorTargetDesc, SDL.GPUFillMode fillMode)
    {
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            RasterizerState = new SDL.GPURasterizerState
            {
                FillMode = fillMode
            },
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1
            }
        };

        var pipeline = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);
        Marshal.FreeHGlobal(colorTargetDescPtr);
        return pipeline;
    }
}
