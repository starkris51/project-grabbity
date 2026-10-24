#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
float4 TopColor;
float4 MiddleColor;
float4 BottomColor;
float2 ScreenSize;

sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
	float2 ScreenPosition : VPOS;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 texel = tex2D(SpriteTextureSampler, input.TextureCoordinates) * input.Color;

    float t = saturate(input.ScreenPosition.y / ScreenSize.y);

    float4 gradientColor;
    if (t < 0.5)
        gradientColor = lerp(TopColor, MiddleColor, t * 2.0);
    else
        gradientColor = lerp(MiddleColor, BottomColor, (t - 0.5) * 2.0);

    return float4(texel.rgb * gradientColor.rgb, texel.a);
}
technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};