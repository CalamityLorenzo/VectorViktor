// A mesh's faces in one draw, whatever colours they are (see MeshRendering/PaletteEffect.cs): each vertex carries the
// slot of its palette its face is coloured from, and the instance's palette comes in as an array of colours. Fogged as
// BasicEffect fogs: linearly, by depth in front of the eye, from FogStart to FogEnd, into FogColor.
//
// Graded (see IPaletteGrade), the palette comes in three times over, each changed its own way, and each pixel takes its
// colour from one of them by how far it is across the ground from a centre: inside a place round it, the place's (or,
// inside a front spreading out from the centre, the front's), faded into the rest over the place's edge; beyond, the
// world's. A bright rim marks the front.
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
float4x4 World;
float4 Palette[MAX_COLOURS];        // the world's (all there is, ungraded)
float4 PlacePalette[MAX_COLOURS];   // inside the place
float4 FrontPalette[MAX_COLOURS];   // behind the front
float4 Zones;    // xyz the centre; w the place's radius, across the ground
float4 Front;    // x the front's radius; y the place's edge, how wide; z the rim, how bright; w 1 graded, 0 not
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
    float4 Place : COLOR1;
    float4 Behind : TEXCOORD1;
    float2 Ground : TEXCOORD2;   // where it is, across the ground (x, z)
    float Fog : TEXCOORD0;
};

VertexOut Faces(VertexIn input)
{
    VertexOut output;
    output.Position = mul(input.Position, WorldViewProj);
    int slot = (int)(input.Slot + 0.5);
    output.Colour = Palette[slot];
    output.Place = PlacePalette[slot];
    output.Behind = FrontPalette[slot];
    output.Ground = mul(input.Position, World).xz;
    float depth = -mul(input.Position, WorldView).z;
    output.Fog = FogOn * saturate((depth - FogStart) / (FogEnd - FogStart));
    return output;
}

float4 Colour(VertexOut input) : COLOR0
{
    float3 colour = input.Colour.rgb;
    if (Front.w > 0.5)
    {
        float off = distance(input.Ground, Zones.xz);
        float3 place = off < Front.x ? input.Behind.rgb : input.Place.rgb;
        colour = lerp(colour, place, saturate((Zones.w - off) / Front.y));
        colour = lerp(colour, float3(1, 1, 1), Front.z * saturate(1 - abs(off - Front.x) / 0.5));
    }
    return float4(lerp(colour, FogColor, input.Fog), input.Colour.a);
}

technique Faces
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL Faces();
        PixelShader = compile PS_SHADERMODEL Colour();
    }
};
