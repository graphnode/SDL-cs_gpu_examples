#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 1 "C:/Users/dgome/Projects/SDL-cs_gpu_examples/SDL-cs_gpu_examples/Content/Shaders/Source/PositionColor.vert.hlsl"
struct main_Result_0
{
    float4 Color_0 [[user(TEXCOORD)]];
    float4 Position_0 [[position]];
};


#line 1
struct vertexInput_0
{
    float3 Position_1 [[attribute(0)]];
    float4 Color_1 [[attribute(1)]];
};

struct Output_0
{
    float4 Color_2;
    float4 Position_2;
};


#line 7
[[vertex]] main_Result_0 main_0(vertexInput_0 _S1 [[stage_in]])
{

#line 15
    thread Output_0 output_0;
    (&output_0)->Color_2 = _S1.Color_1;
    (&output_0)->Position_2 = float4(_S1.Position_1, 1.0);

#line 17
    thread main_Result_0 _S2;

#line 17
    (&_S2)->Color_0 = output_0.Color_2;

#line 17
    (&_S2)->Position_0 = output_0.Position_2;

#line 17
    return _S2;
}

