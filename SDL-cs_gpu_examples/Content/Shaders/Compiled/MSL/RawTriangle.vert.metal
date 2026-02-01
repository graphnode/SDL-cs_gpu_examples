#include <metal_stdlib>
#include <metal_math>
#include <metal_texture>
using namespace metal;

#line 6 "C:/Users/dgome/Projects/SDL-cs_gpu_examples/SDL-cs_gpu_examples/Content/Shaders/Source/RawTriangle.vert.hlsl"
struct main_Result_0
{
    float4 Color_0 [[user(TEXCOORD)]];
    float4 Position_0 [[position]];
};


#line 6
struct Output_0
{
    float4 Color_1;
    float4 Position_1;
};




[[vertex]] main_Result_0 main_0(uint VertexIndex_0 [[vertex_id]])
{

#line 14
    thread Output_0 output_0;

#line 14
    float2 pos_0;

    if(VertexIndex_0 == 0U)
    {
        float2 _S1 = float2(-1.0) ;
        (&output_0)->Color_1 = float4(1.0, 0.0, 0.0, 1.0);

#line 19
        pos_0 = _S1;

#line 16
    }
    else
    {


        if(VertexIndex_0 == 1U)
        {
            float2 _S2 = float2(1.0, -1.0);
            (&output_0)->Color_1 = float4(0.0, 1.0, 0.0, 1.0);

#line 24
            pos_0 = _S2;

#line 21
        }
        else
        {


            if(VertexIndex_0 == 2U)
            {
                float2 _S3 = float2(0.0, 1.0);
                (&output_0)->Color_1 = float4(0.0, 0.0, 1.0, 1.0);

#line 29
                pos_0 = _S3;

#line 26
            }

#line 21
        }

#line 16
    }

#line 31
    (&output_0)->Position_1 = float4(pos_0, 0.0, 1.0);
    Output_0 _S4 = output_0;

#line 32
    thread main_Result_0 _S5;

#line 32
    (&_S5)->Color_0 = _S4.Color_1;

#line 32
    (&_S5)->Position_0 = _S4.Position_1;

#line 32
    return _S5;
}

