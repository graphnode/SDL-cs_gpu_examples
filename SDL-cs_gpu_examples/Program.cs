using SDL3;

namespace SDL_cs_gpu_examples;

public static class Program
{
    private record struct ExampleInfo(string Name, Func<int> Run);

    private static readonly ExampleInfo[] AllExamples =
    [
        new("ClearScreen", ClearScreen.Main),
        new("ClearScreenMultiWindow", ClearScreenMultiWindow.Main),
        new("BasicTriangle", BasicTriangle.Main),
        new("BasicVertexBuffer", BasicVertexBuffer.Main),
        new("TexturedQuad", TexturedQuad.Main),
        new("TexturedAnimatedQuad", TexturedAnimatedQuad.Main),
        new("CustomSampling", CustomSampling.Main),
        new("BlitMirror", BlitMirror.Main),
        new("GenerateMipmaps", GenerateMipmaps.Main),
        new("Latency", Latency.Main),
        new("BasicCompute", BasicCompute.Main),
        new("ComputeUniforms", ComputeUniforms.Main),
        new("ComputeSampler", ComputeSampler.Main),
        new("CopyAndReadback", CopyAndReadback.Main),
        new("CopyConsistency", CopyConsistency.Main),
        new("CullMode", CullMode.Main),
        new("BasicStencil", BasicStencil.Main),
        new("InstancedIndexed", InstancedIndexed.Main),
        new("WindowResize", WindowResize.Main),
        new("TriangleMSAA", TriangleMSAA.Main),
        new("Clear3DSlice", Clear3DSlice.Main),
        new("DrawIndirect", DrawIndirect.Main),
        new("Texture2DArray", Texture2DArray.Main),
        new("Cubemap", Cubemap.Main),
        new("Blit2DArray", Blit2DArray.Main),
        new("BlitCube", BlitCube.Main),
        new("DepthSampler", DepthSampler.Main),
        new("PullSpriteBatch", PullSpriteBatch.Main),
        new("ComputeSpriteBatch", ComputeSpriteBatch.Main),
    ];

    private static ExampleInfo GetExample(string name)
    {
        var example = Array.Find(AllExamples, x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        return example;
    }

    public static int Main(string[] args)
    {
        // Initialize ShaderCross for runtime HLSL compilation
        if (!ShaderCross.Init())
        {
            Console.WriteLine($"Failed to initialize ShaderCross: {SDL.GetError()}");
            return 1;
        }

        try
        {
            if (args.Length > 0)
            {
                var example = GetExample(args[0]);
                if (example == default)
                    return 1;
                example.Run.Invoke();
            }
            else
            {
                foreach (var example in AllExamples)
                {
                    example.Run.Invoke();
                }
            }

            return 0;
        }
        finally
        {
            ShaderCross.Quit();
        }
    }
}
