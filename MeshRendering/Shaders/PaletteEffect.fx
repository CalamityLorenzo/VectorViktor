// A mesh's faces in one draw, whatever colours they are (see MeshRendering/PaletteEffect.cs): each vertex carries the
// slot of its palette its face is coloured from, and the instance's palette comes in as an array of colours. Fogged as
// BasicEffect fogs: linearly, by depth in front of the eye, from FogStart to FogEnd, into FogColor.
//
// After changing this, compile it again (see PaletteEffect.cs): the game loads the compiled copy beside it.

#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

#define MAX_COLOURS 64

float4x4 WorldViewProj;
float4x4 WorldView;
float4 Palette[MAX_COLOURS];
float3 FogColor;
float FogStart;
float FogEnd;
float FogOn;   // 1 fogged, 0 not

struct VertexIn
{
    float4 Position : POSITION0;
    float Slot : TEXCOORD0;
};

struct VertexOut
{
    float4 Position : SV_POSITION;
    float4 Colour : COLOR0;
    float Fog : TEXCOORD0;
};

VertexOut Faces(VertexIn input)
{
    VertexOut output;
    output.Position = mul(input.Position, WorldViewProj);
    output.Colour = Palette[(int)(input.Slot + 0.5)];
    float depth = -mul(input.Position, WorldView).z;
    output.Fog = FogOn * saturate((depth - FogStart) / (FogEnd - FogStart));
    return output;
}

float4 Colour(VertexOut input) : COLOR0
{
    return float4(lerp(input.Colour.rgb, FogColor, input.Fog), input.Colour.a);
}

technique Faces
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL Faces();
        PixelShader = compile PS_SHADERMODEL Colour();
    }
};
