#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "lightUtil.fxh"

float4x4 world;
float4x4 view;
float4x4 projection;


float2 screenSize;
float radius;

struct PVSI
{
    float4 Position : POSITION;
    float2 TexCoord : TEXCOORD;
};
struct PVSO
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD;
};
struct PSO
{
    float4 color : COLOR0;
    float4 blurH : COLOR1;
    float4 blurV : COLOR2;
};
static const int kernel_r = 6;
static const int kernel_size = 13;
static const float Kernel[kernel_size] =
{
    0.002216, 0.008764, 0.026995, 0.064759, 0.120985, 0.176033, 0.199471, 0.176033, 0.120985, 0.064759, 0.026995, 0.008764, 0.002216,
};

texture colorMap;
sampler colorSampler = sampler_state
{
    Texture = (colorMap);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
};
texture normalMap;
sampler normalMapSampler = sampler_state
{
    Texture = (normalMap);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
};

texture positionMap;
sampler positionMapSampler = sampler_state
{
    Texture = (positionMap);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
};

texture lightMap;
sampler lightMapSampler = sampler_state
{
    Texture = (lightMap);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
};

texture bloomFilter;
sampler bloomFilterSampler = sampler_state
{
    Texture = (bloomFilter);
    AddressU = CLAMP;
    AddressV = CLAMP;
    MagFilter = POINT;
    MinFilter = POINT;
    Mipfilter = POINT;
};
texture blurH;
sampler2D blurHSampler = sampler_state
{
    Texture = (blurH);
    MagFilter = Linear;
    MinFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};
texture blurV;
sampler2D blurVSampler = sampler_state
{
    Texture = (blurV);
    MagFilter = Linear;
    MinFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

PVSO PostVS(PVSI input)
{
    PVSO output;
    output.Position = input.Position;
    output.TexCoord = input.TexCoord;
    return output;
}

PVSO PointLightVS(PVSI input)
{
    PVSO output;
    float4 worldPosition = mul(input.Position, world);
    float4 viewPosition = mul(worldPosition, view);
    float4 screenPos = mul(viewPosition, projection);
    
    output.Position = screenPos;
    output.TexCoord = input.TexCoord;
    return output;
}


float4 AmbientLight(PVSO input)
{
    float4 colorRaw = tex2D(colorSampler, input.TexCoord);
    float3 color = colorRaw.rgb;
    float KD = colorRaw.a;
    
    float4 normalRaw = tex2D(normalMapSampler, input.TexCoord);
    
    if (KD == 0)
    {
        return float4(0, 0, 0, 1);
    }
    
    float KS = normalRaw.a;
    float4 worldRaw = tex2D(positionMapSampler, input.TexCoord);
    float shininess = worldRaw.a * 60;
    
    float3 normal = normalize((normalRaw.rgb * 2.0) - 1);
    float3 worldPos = worldRaw.rgb;
    
    return float4(getPixelAmbient(worldPos, normal, KD, KS, shininess), 1);
}

PSO AmbientAndBlurPS(PVSO input) 
{
    PSO output;
    output.color = AmbientLight(input);
    
    float3 hColor = float3(0, 0, 0);
    float3 vColor = float3(0, 0, 0);
    
    
    for (int i = 0; i < kernel_size; i++)
    {
        float2 scaledTextureCoordinatesH = input.TexCoord + float2((float) (i - kernel_r) / screenSize.x, 0);
        float2 scaledTextureCoordinatesV = input.TexCoord + float2(0, (float) (i - kernel_r) / screenSize.y);
        hColor += tex2D(bloomFilterSampler, scaledTextureCoordinatesH).rgb * Kernel[i];
        vColor += tex2D(bloomFilterSampler, scaledTextureCoordinatesV).rgb * Kernel[i];
    }
    
    output.blurH = float4(hColor, 1);
    output.blurV = float4(vColor, 1);
    
    return output;
}


float sqr(float x)
{
    return x * x;
}
float attenuate_no_cusp(float distance, float radius, float max_intensity, float falloff)
{
    float s = distance / radius;

    if (s >= 1.0)
        return 0.0;

    float s2 = sqr(s);

    return max_intensity * sqr(1 - s2) / (1 + falloff * s2);
}
float4 PointLightPS(PVSO input) : COLOR
{
    float2 sceneCoord = input.Position.xy / screenSize;
    
    float4 colorRaw = tex2D(colorSampler, sceneCoord);
    float3 color = colorRaw.rgb;
    float KD = colorRaw.a;
    
    float4 normalRaw = tex2D(normalMapSampler, sceneCoord);
    
    if (KD == 0)
    {
        return float4(0,0,0, 1);
    }
    
    float KS = normalRaw.a;
    float4 worldRaw = tex2D(positionMapSampler, sceneCoord);
    float shininess = worldRaw.a * 60;
   
    
    float3 normal = normalize((normalRaw.rgb * 2.0) - 1);
    float3 worldPos = worldRaw.rgb;

    //cheap scaling
    //float scaling = 1- smoothstep(0, radius, distance(worldPos, lightPosition));
    
    //advanced scaling
    float scaling = attenuate_no_cusp(distance(worldPos, lightPosition), radius, 3, 6);
    
    //red border for debug
    //if (scaling == 0)
    //    return float4(1, 0, 0, 1);
    
    return float4(getPixelColorNoAmbient(worldPos, normal, KD, KS, shininess) * scaling, 1);
}
PSO PointLightBPS(PVSO input)
{
    float4 prev = PointLightPS(input);
    PSO output = (PSO)0;
    output.color = prev;
    output.blurH = float4(0, 0, 0, 1);
    output.blurV = float4(0, 0, 0, 1);
    return output;
}
float4 processBloom(float3 color, float filter, float3 light, float2 texCoord)
{
    float3 blurHColor = tex2D(blurHSampler, texCoord).rgb;
    float3 blurVColor = tex2D(blurVSampler, texCoord).rgb;
    
    float attenuation = 0.8;
    float lightPower = .7;
    
    if (filter == 0)
        return float4(color * attenuation + blurHColor * lightPower + blurVColor * lightPower, 1);
    else
        return float4(color * attenuation * light + blurHColor * lightPower + blurVColor * lightPower, 1);
}
float4 IntegratePS(PVSO input) : COLOR
{
    float4 color = tex2D(colorSampler, input.TexCoord);
    float4 light = tex2D(lightMapSampler, input.TexCoord);
   
    return processBloom(color.rgb, color.a, light.rgb, input.TexCoord);
}

technique point_light
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PointLightVS();
        PixelShader = compile PS_SHADERMODEL PointLightPS();
    }
}
technique ambient_light
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PostVS();
        PixelShader = compile PS_SHADERMODEL AmbientAndBlurPS();
    }
}
technique integrate
{
    pass P0
    {
        AlphaBlendEnable = FALSE;
        VertexShader = compile VS_SHADERMODEL PostVS();
        PixelShader = compile PS_SHADERMODEL IntegratePS();
    }
}