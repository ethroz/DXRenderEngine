/////////////////////////////////////////
//            Definitions              //
/////////////////////////////////////////

#define NUM_LIGHTS 1
#define RCP_SQRT3 0.57735026919f
#define QUARTER_PI 0.78539816339f
#define PI 3.14159265359f
#define TWO_PI 6.28318530718f
#define DEPTH_BIAS 0.0f
#define SOFTNESS 1.0f
#define SAMPLE_COUNT 4u
#define EXPOSURE 1.5f
#define ATMOSPHERE_DIR float3(0.0f, 1.0f, 0.0f)
#define RED float3(1.0f, 0.0f, 0.0f)
#define GREEN float3(0.0f, 1.0f, 0.0f)
#define BLUE float3(0.0f, 0.0f, 1.0f)
#define MAGENTA float3(1.0f, 0.0f, 1.0f)
#define CYAN float3(0.0f, 1.0f, 1.0f)

static float3 AXES[6] =
{
	float3(-1.0f, 0.0f, 0.0f),
	float3(1.0f, 0.0f, 0.0f),
	float3(0.0f, -1.0f, 0.0f),
	float3(0.0f, 1.0f, 0.0f),
	float3(0.0f, 0.0f, -1.0f),
	float3(0.0f, 0.0f, 1.0f)
};

static float3 SAMPLES[SAMPLE_COUNT] =
{
	float3(-RCP_SQRT3, -RCP_SQRT3, -RCP_SQRT3),
	float3(-RCP_SQRT3,  RCP_SQRT3,  RCP_SQRT3),
	float3( RCP_SQRT3, -RCP_SQRT3,  RCP_SQRT3),
	float3( RCP_SQRT3,  RCP_SQRT3, -RCP_SQRT3)
};

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
	float3 view : POSITION0;
	float3 shadowRays[NUM_LIGHTS] : POSITION1;
};

struct GeometryInputType
{
	float4 position : SV_POSITION;
	float3 normal : NORMAL;
};

struct PixelInputType
{
	float4 position : SV_POSITION;
	float3 normal : NORMAL;
	uint index : SV_RENDERTARGETARRAYINDEX;
	float3 light : POSITION;
};

struct PixelShaderOutput
{
	float4 color : SV_TARGET;
	float depth : SV_DEPTH;
};

struct TextureShaderInput
{
    float4 position : POSITION;
    float2 tex : TEXCOORD0;
};

struct TextureShaderOutput
{
    float4 position : SV_Position;
    float2 tex : TEXCOORD0;
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
	float3 padding;
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

float Pow5(float value)
{
	return value * value * value * value * value;
}

float rand(float2 p)
{
	float2 k = float2(
		23.14069263277926, // e^pi (Gelfond's constant)
		2.665144142690225 // 2^sqrt(2) (Gelfond–Schneider constant)
		);
	return frac(cos(dot(p, k)) * 12345.6789);
}

float random(float2 p)
{
	return rand(p.xy * (rand(p.xy * ModdedTime) - rand(rand(p.xy * ModdedTime) - ModdedTime)));
}

float lengthSqr(float3 vec)
{
	return vec.x * vec.x + vec.y * vec.y + vec.z * vec.z;
}

float luminance(float3 v)
{
	return 0.2126f * v.r + 0.7152f * v.g + 0.0722f * v.b;
}

float NormalDistribution(float alpha, float3 normal, float3 middle)
{
	// GGX
	float ndotm = dot(normal, middle);
	if (ndotm <= 0.0f)
	{
		return 0.0f;
	}
	float dotSqr = square(ndotm);
	float alphaSqr = square(alpha);
	return alphaSqr / (PI * square(dotSqr * (alphaSqr + (1.0f - dotSqr) / dotSqr)));
}

float Geometric(float3 view, float3 light, float3 middle, float3 normal, float alpha)
{
	// GGX
	float vdotm = dot(view, middle);
	float vdotn = dot(view, normal);
	float ldotm = dot(light, middle);
	float ldotn = dot(light, normal);
	if (vdotm / vdotn <= 0.0f || ldotm / ldotn <= 0.0f)
	{
		return 0.0f;
	}
	else
	{
		float alphaSqr = square(alpha);
		float vDotSqr = square(vdotn);
		float lDotSqr = square(ldotn);
		return 4.0f / (1.0f + sqrt(1.0f + alphaSqr * (1.0f - vDotSqr) / vDotSqr)) / (1.0f + sqrt(1.0f + alphaSqr * (1.0f - lDotSqr) / lDotSqr));
	}
}

float Fresnel(float ior, float3 view, float3 middle)
{
	// Schlick’s Approximation
	float F0 = square(ior - 1.0f) / square(ior + 1.0f);
	return F0 + (1.0f - F0) * Pow5(1.0f - dot(view, middle));
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

// shadow map rasterizer
GeometryInputType shadowVertexShader(VertexShaderInput In)
{
	GeometryInputType Out;
	Out.position = mul(worldMat, float4(In.position, 1.0f));
	Out.normal = mul((float3x3)normalMat, In.normal);
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
			Out.normal = In[j].normal;
			Out.index = i;
			Out.light = Lights[index].position - In[j].position.xyz;
			triStream.Append(Out);
		}
		triStream.RestartStrip();
	}
}

float shadowPixelShader(PixelInputType In) : SV_DEPTH
{
	float depth = length(In.light);
	In.light /= depth;
	depth /= Lights[index].far;
	float cosTheta = dot(normalize(In.normal), In.light);
	if (cosTheta < 0.0f)
		return 1.0f;
	float bias = (2.82f * depth * square(dot(-AXES[In.index], In.light)) * tan(acos(cosTheta)) + DEPTH_BIAS) / Lights[index].res;
	return depth + bias;
}

// object lighting rasterizer
VertexShaderOutput vertexShader(VertexShaderInput In)
{
	VertexShaderOutput Out;
	Out.position = mul(worldMat, float4(In.position, 1.0f));
	Out.view = EyePos - Out.position.xyz;
	[unroll]
	for (uint i = 0; i < NUM_LIGHTS; ++i)
	{
		Out.shadowRays[i] = Out.position.xyz - Lights[i].position;
	}
	Out.position = mul(ViewMatrix, Out.position);
	Out.position = mul(ProjectionMatrix, Out.position);
	Out.normal = mul((float3x3)normalMat, In.normal);
	return Out;
}

PixelShaderOutput pixelShader(VertexShaderOutput In)
{
	PixelShaderOutput Out;
	Out.depth = In.position.z; // output depth is screenspace depth
	Out.color = float4(0.0f, 0.0f, 0.0f, In.position.w); // post process uses alpha for real depth
	//Out.color = float4(1.0f, 1.0f, 1.0f, In.position.w); // post process uses alpha for real depth
	In.normal = normalize(In.normal); // Normalize the input normal

	// All light calculations
	[unroll]
	for (uint i = 0; i < NUM_LIGHTS; ++i)
	{
		// Depth calculations
		float depth = length(In.shadowRays[i]) / Lights[i].far;
		float shadow = ShadowTextures[i].SampleCmpLevelZero(DepthSampler, In.shadowRays[i], depth);

		// Color calculations
		float3 lightDir = -normalize(In.shadowRays[i]); // normalize and flip the shadowRay to get the lightDir
		float lamb = dot(lightDir, In.normal);
		if (lamb > 0.0f)
		{
			// Cook-Torrance BRDF model
			float3 viewDir = normalize(In.view);
			float alpha = square(material.rough);
			float3 middle = normalize(lightDir + viewDir);
			float D = NormalDistribution(alpha, In.normal, middle);
			float G = Geometric(viewDir, lightDir, middle, In.normal, alpha);
			float F = Fresnel(material.ior, viewDir, middle);
			float rs = D * G * F / (4.0f * dot(In.normal, viewDir));
			Out.color.rgb += Lights[i].color * shadow * (lamb * (1.0f - material.shine) * material.diffuse + material.shine * rs * material.spec);
		}
	}

	// hdr thyme
	Out.color.rgb = 1.0f - exp(-EXPOSURE * Out.color.rgb);

	// temporal denoiser
	Out.color.rgb += ((random(In.position.xy) - 0.5f) / 255.0f);
	return Out;
}

// postprocessing pixel shader
float3 postProcessPixelShader(TextureShaderOutput In) : SV_Target
{
	// depth test

	/*float depth = ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).w;
	if (depth < 1000.0f)
	{
		if (depth < 0.3f)
			return 0.0f;
		else if (depth < 0.5f)
			return 0.25f;
		else if (depth < 0.7f)
			return 0.5f;
		else if (depth < 0.9f)
			return 0.75f;
		else
			return 1.0f;
	}
	return 0.0f;*/
	
	// no post process

	//float3 colour = ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).rgb;
	//colour += ((random(In.position.xy) - 0.5f) / 255.0f);
	//return colour;

	// fxaa fail

	//if (In.position.x == 0.5f || In.position.x == Width - 0.5f || In.position.y == 0.5f || In.position.y == Height - 0.5f)
	//	return 0.0f;
	//float3 horizontal = 0.0f;
	//float3 vertical = 0.0f;
	//float kernel1[9] = { 1, 0, -1, 2, 0, -2, 1, 0, -1 };
	//float kernel2[9] = { 1, 2, 1, 0, 0, 0, -1, -2, -1 };
	//float kernel3[9] = { 0.2, 0, 0.2, 0, 0.2, 0, 0.2, 0, 0.2 };
	//for (int i = 0; i < 3; ++i)
	//{
	//	for (int j = 0; j < 3; ++j)
	//	{
	//		horizontal += kernel1[i * 3 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 1, i - 1)) / float2(Width, Height)).rgb;
	//		vertical += kernel2[i * 3 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 1, i - 1)) / float2(Width, Height)).rgb;
	//	}
	//}
	//float mag = sqrt(length(horizontal) + length(vertical));
	//float threshold = 100.0f / 255.0f;
	//float value = max(threshold, mag);
	//if (value == threshold)
	//{
	//	if (LightIndex == 0)
	//		return ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height)).rgb;
	//	else
	//		return 0.0f;
	//}
	//if (LightIndex == 1)
	//	return value;
	//float3 color = 0.0f;
	//for (int i = 0; i < 3; ++i)
	//{
	//	for (int j = 0; j < 3; ++j)
	//	{
	//		color += kernel3[i * 3 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 1, i - 1)) / float2(Width, Height)).rgb;
	//	}
	//}
	//return color;

	// fxaa attempt 2

	//float3 col = 0.0f;
	//float3 color = ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).rgb;
	//float centre = luminance(ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).rgb);
	//float north = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float east = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, 0.0f)) / float2(Width, Height), 0).rgb);
	//float south = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float west = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, 0.0f)) / float2(Width, Height), 0).rgb);
	//float highest = max(max(max(max(centre, north), east), south), west);
	//float lowest = min(min(min(min(centre, north), east), south), west);
	//float contrast = highest - lowest;
	//float threshold = 0.0312f; // 0.0833 - upper limit (default, the start of visible unfiltered edges)    0.0625 - high quality (faster)    0.0312 - visible limit (slower)
	//float relativethreshold = 0.063f; // 0.333 - too little (faster)    0.250 - low quality    0.166 - default    0.125 - high quality    0.063 - overkill (slower)
	//threshold = max(threshold, relativethreshold * contrast);
	//if (contrast < threshold)
	//{
	//	if (LightIndex == 0)
	//		return color;
	//	else
	//		return 0.0f;
	//}
	//float northwest = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float northeast = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float southeast = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float southwest = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float filter = 2.0f * (north + east + south + west) + (northwest + northeast + southeast + southwest);
	//filter /= 12.0f;
	//filter = abs(filter - centre);
	//filter = saturate(filter / contrast);
	////if (LightIndex == 1)
	////	return filter;
	//float blendfactor = smoothstep(0.0f, 1.0f, filter);
	//blendfactor *= blendfactor;
	//float horizontal = abs(north + south - 2.0f * centre) * 2.0f + abs(northeast + southeast - 2.0f * east) + abs(northwest + southwest - 2.0f * west);
	//float vertical = abs(east + west - 2.0f * centre) * 2.0f + abs(northeast + northwest - 2.0f * north) + abs(southeast + southwest - 2.0f * south);
	//float pixelstep = 1.0f;
	//float pluminance;
	//float nluminance;
	//if (horizontal >= vertical)
	//{
	//	pluminance = north;
	//	nluminance = south;
	//}
	//else
	//{
	//	pluminance = east;
	//	nluminance = west;
	//}
	//float pgradient = abs(pluminance - centre);
	//float ngradient = abs(nluminance - centre);
	//if (pgradient < ngradient)
	//	pixelstep = -1.0f;
	//if (LightIndex == 1)
	//{
	//	if (pixelstep < 0.0f)
	//		return float3(1.0f, 0.0f, 0.0f);
	//	else
	//		return float3(0.0f, 1.0f, 0.0f);
	//}
	//if (horizontal >= vertical)
	//{
	//	col = ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(pixelstep * blendfactor, 0.0f)) / float2(Width, Height), 0).rgb;
	//}
	//else
	//{
	//	col = ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, pixelstep * blendfactor)) / float2(Width, Height), 0).rgb;
	//}
	//return col;

	// fxaa attempt 3

	//if (In.position.x == 0.5f || In.position.x == Width - 0.5f || In.position.y == 0.5f || In.position.y == Height - 0.5f)
	//	return 0.0f;
	//float3 horz = 0.0f;
	//float3 vert = 0.0f;
	//float kernel1[9] = { 1, 0, -1, 2, 0, -2, 1, 0, -1 };
	//float kernel2[9] = { 1, 2, 1, 0, 0, 0, -1, -2, -1 };
	//for (int i = 0; i < 3; ++i)
	//{
	//	for (int j = 0; j < 3; ++j)
	//	{
	//		horz += kernel1[i * 3 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 1, i - 1)) / float2(Width, Height)).rgb;
	//		vert += kernel2[i * 3 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 1, i - 1)) / float2(Width, Height)).rgb;
	//	}
	//}
	//float mag = sqrt(length(horz) + length(vert));
	//float threshold = 100.0f / 255.0f;
	//float value = max(threshold, mag);
	//if (value == threshold)
	//{
	//	if (LightIndex == 0)
	//		return ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height)).rgb;
	//	else
	//		return 0.0f;
	//}
	////if (LightIndex == 1)
	////	return value;
	//float3 col = 0.0f;
	//float centre = luminance(ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).rgb);
	//float north = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float east = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, 0.0f)) / float2(Width, Height), 0).rgb);
	//float south = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float west = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, 0.0f)) / float2(Width, Height), 0).rgb);
	//float highest = max(max(max(max(centre, north), east), south), west);
	//float lowest = min(min(min(min(centre, north), east), south), west);
	//float contrast = highest - lowest;
	//float northwest = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float northeast = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, 1.0f)) / float2(Width, Height), 0).rgb);
	//float southeast = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(1.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float southwest = luminance(ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(-1.0f, -1.0f)) / float2(Width, Height), 0).rgb);
	//float filter = 2.0f * (north + east + south + west) + (northwest + northeast + southeast + southwest);
	//filter /= 12.0f;
	//filter = abs(filter - centre);
	//filter = saturate(filter / contrast);
	////if (LightIndex == 1)
	////	return filter;
	////float blendfactor = filter;
	//float blendfactor = smoothstep(0.0f, 1.0f, filter);
	//blendfactor *= blendfactor;
	//float horizontal = abs(north + south - 2.0f * centre) * 2.0f + abs(northeast + southeast - 2.0f * east) + abs(northwest + southwest - 2.0f * west);
	//float vertical = abs(east + west - 2.0f * centre) * 2.0f + abs(northeast + northwest - 2.0f * north) + abs(southeast + southwest - 2.0f * south);
	//float pixelstep = 1.0f;
	//float pluminance;
	//float nluminance;
	//if (horizontal >= vertical)
	//{
	//	pluminance = north;
	//	nluminance = south;
	//}
	//else
	//{
	//	pluminance = east;
	//	nluminance = west;
	//}
	//float pgradient = abs(pluminance - centre);
	//float ngradient = abs(nluminance - centre);
	//if (pgradient < ngradient)
	//	pixelstep = -1.0f;
	//if (LightIndex == 1)
	//{
	//	if (pixelstep < 0.0f)
	//		return float3(1.0f, 0.0f, 0.0f);
	//	else
	//		return float3(0.0f, 1.0f, 0.0f);
	//}
	//if (horizontal >= vertical)
	//{
	//	float3 temp1 = ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).rgb;
	//	float3 temp2 = ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(pixelstep, 0.0f)) / float2(Width, Height), 0).rgb;
	//	col = lerp(temp1, temp2, blendfactor);
	//}
	//else
	//{
	//	col = ShaderTexture.SampleLevel(ColorSampler, (In.position.xy + float2(0.0f, pixelstep * blendfactor)) / float2(Width, Height), 0).rgb;
	//}
	//return col;

	// chromatic aberration

	//uint quality = 10;
	//float exponent = 2.0f;
	//float intensity = 2.0f;
	//float2 offset = (In.position.xy - float2(Width / 2, Height / 2)) / float2(Width, Height);
	//float len = length(offset);
	//float amount = pow(len, exponent) * intensity * 0.5f;
	//offset = float2(amount * offset.x / len, amount * offset.y / len);
	//if (quality == 0)
	//	return ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height)).rgb;
	//float3 color;
	//float3 totals;
	//for (uint i = 0; i < 2 * quality + 1; ++i)
	//{
	//	float2 pos;
	//	if (i == 0)
	//		pos = In.position.xy / float2(Width, Height);
	//	else
	//		pos = In.position.xy / float2(Width, Height) - offset * i / (quality * 2.0f);
	//	float3 coefficient;
	//	if (i < quality)
	//		coefficient = float3((quality - float(i)) / quality, float(i) / quality, 0.0f);
	//	else if (i < 2 * quality)
	//		coefficient = float3(0.0f, (quality - float(i - quality)) / quality, float(i - quality) / quality);
	//	else
	//		coefficient = float3(0.0f, 0.0f, 1.0f);
	//	totals += coefficient;
	//	color += ShaderTexture.Sample(ColorSampler, pos).rgb * coefficient;
	//}
	//color /= totals;
	//return color;

	// depth of field

	float plane = 0.0f;
	float span = 3.0f;
	float focus = clamp((ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height)).a - plane) / span, -1, 1);
	//return focus;
	float3 color = 0.0f;
	int total = 0;
	float quality = abs(focus) * 3.0f;
	for (int i = 0; i < 2 * quality + 1; ++i)
	{
		for (int j = 0; j < 2 * quality + 1; ++j)
		{
			float2 offset = float2(j - quality, i - quality);
			if (length(offset) <= quality)
			{
				color += ShaderTexture.Sample(ColorSampler, (In.position.xy + offset) / float2(Width, Height)).rgb;
				total += 1;
			}
		}
	}
	color /= total;
	return color;
}

// texture shader
TextureShaderOutput planePassthrough(TextureShaderInput In)
{
	TextureShaderOutput Out;
	Out.position = In.position;
	Out.tex = In.tex;
    return Out;
}

float3 textureShader(TextureShaderOutput In) : SV_TARGET
{
	float3 color = ShaderTexture.Sample(ColorSampler, In.tex).xyz;
	color += ((random(In.position.xy) - 0.5f) / 255.0f);
	return color;
}
