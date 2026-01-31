#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 1 "C:/Users/dgome/Projects/SDL-cs_gpu_examples/SDL-cs_gpu_examples/Content/Shaders/Source/SolidColor.frag.hlsl"
struct pixelOutput_0
{
    float4 output_0 [[color(0)]];
};


#line 1
struct pixelInput_0
{
    float4 Color_0 [[user(TEXCOORD)]];
};


#line 1
[[fragment]] pixelOutput_0 main_0(pixelInput_0 _S1 [[stage_in]])
{

#line 1
    pixelOutput_0 _S2 = { _S1.Color_0 };

    return _S2;
}

