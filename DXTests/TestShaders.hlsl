/////////////////////////////////////////
//            Definitions              //
/////////////////////////////////////////

#define NUM_LIGHTS 1
#define THIC 0.0001f
#define QUARTER_PI 0.78539816339f
#define PI 3.14159265359f
#define TWO_PI 6.28318530718f
#define RED float3(1.0f, 0.0f, 0.0f)
#define GREEN float3(0.0f, 1.0f, 0.0f)
#define BLUE float3(0.0f, 0.0f, 1.0f)
#define MAGENTA float3(1.0f, 0.0f, 1.0f)
#define CYAN float3(0.0f, 1.0f, 1.0f)

/////////////////////////////////////////
//             Structures              //
/////////////////////////////////////////

struct VertexShaderInput
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct VertexShaderOutput
{
	float4 position : SV_POSITION;
	float3 normal : NORMAL;
	float3 world : POSITION0;
	float3 view : POSITION1;
	float3 shadowRays[NUM_LIGHTS] : POSITION2;
};

struct GeometryInputType
{
	float4 position : SV_POSITION;
};

struct PixelInputType
{
	float4 position : SV_POSITION;
	float3 light : POSITION;
	uint index : SV_RENDERTARGETARRAYINDEX;
};

struct PixelShaderOutput
{
	float4 color : SV_TARGET;
	float depth : SV_DEPTH;
};

struct Light
{
	float3 position;
	float radius;
	float3 color;
	float luminosity;
	float res;
	float far;
	float2 padding;
};

struct Material
{
	float3 diffuse; // kd; ka = kd * atmosphere
	float rough; // sqrt(alpha)
	float3 spec; // ks
	float shine; // s
	float ior; // IOR
	float3 padding;
};

/////////////////////////////////////////
//              Buffers                //
/////////////////////////////////////////

Texture2D ShaderTexture : register(t0);
TextureCube ShadowTextures[NUM_LIGHTS] : register(t1);
SamplerState ColorSampler : register(s0);
SamplerComparisonState DepthSampler : register(s1);

cbuffer PerApplication : register(b0)
{
	matrix ProjectionMatrix;
	float3 LowerAtmosphere;
	uint Width;
	float3 UpperAtmosphere;
	uint Height;
};

cbuffer PerFrame : register(b1)
{
	matrix ViewMatrix;
	float3 EyePos;
	float ModdedTime;
	Light Lights[NUM_LIGHTS];
}

cbuffer PerLight : register(b2)
{
	matrix LightMatrices[6];
	uint index;
	float DepthBias;
	bool Line;
	float2 padding;
}

cbuffer PerObject : register(b3)
{
	// Object matrices
	matrix worldMat;
	matrix normalMat;
	// Material stuff
	Material material;
}

/////////////////////////////////////////
//              Methods                //
/////////////////////////////////////////

float square(float value)
{
	return value * value;
}

float lengthSqr(float3 vec)
{
	return vec.x * vec.x + vec.y * vec.y + vec.z * vec.z;
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

// shadow map rasterizer
GeometryInputType shadowVertexShader(VertexShaderInput In)
{
	GeometryInputType Out;
	Out.position = mul(worldMat, float4(In.position, 1.0f));
	return Out;
}

[maxvertexcount(18)]
void shadowGeometryShader(triangle GeometryInputType In[3], inout TriangleStream<PixelInputType> triStream)
{
	[unroll]
	for (uint i = 0; i < 6; ++i)
	{
		[unroll]
		for (uint j = 0; j < 3; ++j)
		{
			PixelInputType Out;
			Out.position = mul(LightMatrices[i], In[j].position);
			Out.light = Lights[index].position - In[j].position.xyz;
			Out.index = i;
			triStream.Append(Out);
		}
		triStream.RestartStrip();
	}
}

float shadowPixelShader(PixelInputType In) : SV_DEPTH
{
	return length(In.light) / Lights[index].far;
}

// object lighting rasterizer
VertexShaderOutput vertexShader(VertexShaderInput In)
{
	VertexShaderOutput Out;
	Out.position = mul(worldMat, float4(In.position, 1.0f));
	Out.view = EyePos - Out.position.xyz;
	Out.world = Out.position.xyz;
	Out.shadowRays[0] = Out.position.xyz - Lights[0].position;
	Out.position = mul(ViewMatrix, Out.position);
	Out.position = mul(ProjectionMatrix, Out.position);
	Out.normal = mul((float3x3)normalMat, In.normal);
	return Out;
}

PixelShaderOutput pixelShader(VertexShaderOutput In)
{
	PixelShaderOutput Out;
	Out.depth = In.position.z; // output depth is screenspace depth
	Out.color = 1.0f;
	In.normal = normalize(In.normal); // Normalize the input normal

	// Depth calculations
	float depth = length(In.shadowRays[0]);
	float3 lightDir = -In.shadowRays[0] / depth; // normalize and flip the shadowRay to get the lightDir;
	// bias
	float cosTheta = dot(In.normal, lightDir);
	float theta = acos(cosTheta) / PI * 180.0f;
	//if (Line && (abs(sqrt(square(In.world.y) + square(In.world.z)) - abs(In.world.x)) < THIC || lengthSqr(In.world) < THIC / 100.0f)) // x-cone x with dot
	if (Line && abs(sqrt(square(In.world.y) + square(In.world.z)) - abs(In.world.x)) < THIC) // x-cone x no dot
	{
		Out.color.rgb = RED;
		return Out;
	}

	float shadow = ShadowTextures[0].SampleCmpLevelZero(DepthSampler, In.shadowRays[0], depth / Lights[0].far - DepthBias / Lights[0].res);
	Out.color.rgb = shadow >= 1.0f ? 1.0f : 0.0f;

	return Out;
}
