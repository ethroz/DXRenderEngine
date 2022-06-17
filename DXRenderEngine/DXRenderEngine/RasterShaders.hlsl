/////////////////////////////////////////
//            Definitions              //
/////////////////////////////////////////

#define NUM_LIGHTS 1
#define PI 3.14159265359f
#define TWO_PI 6.28318530718f
#define DEPTH_BIAS 2.0f
#define NORMAL_BIAS 2.0f
#define THIC 0.00001f
#define ATMOSPHERE_DIR float3(0.0f, 1.0f, 0.0f)
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
	float3 view : POSITION0;
	float3 shadowRays[NUM_LIGHTS] : POSITION1;
};

struct GeometryInputType
{
	float4 position : SV_POSITION;
};

struct PixelInputType
{
	float4 position : SV_POSITION;
	float3 light : POSITION;
	uint index : SV_RenderTargetArrayIndex;
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
	float NormalBias;
	bool Line;
}

cbuffer PerObject : register(b3)
{
	// Object matrices
	matrix worldMat;
	matrix normalMat;
	// Material stuff
	float3 diffuse; // kd; ka = kd * atmosphere
	float rough; // sqrt(alpha)
	float3 spec; // ks
	float shine; // s
	float ior; // IOR
	float3 padding2;
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
	return (GeometryInputType)mul(worldMat, float4(In.position, 1.0f));
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
			Out.light = In[j].position.xyz - Lights[index].position;
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
	//Out.view = EyePos - Out.position.xyz;
	Out.view = Out.position.xyz;
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
	In.normal = normalize(In.normal); // Normalize the input normal

	// all light calculations
	[unroll]
	for (uint i = 0; i < NUM_LIGHTS; ++i)
	{
		// Depth calculations
		float depth = length(In.shadowRays[i]);
		float3 lightDir = -In.shadowRays[i] / depth; // normalize and flip the shadowRay to get the lightDir;
		// bias
		float cosTheta = dot(In.normal, lightDir);
		float theta = acos(cosTheta) / PI * 180.0f;
		//if (Line && abs(theta - NormalBias) < 0.001f) // select normal
		//if (Line && (abs(theta - NormalBias) < 0.001f || abs(abs(In.view.x) - abs(In.view.z)) < 0.001f)) // xz plane x, select normal
		//if (Line && (abs(theta - NormalBias) < THIC || abs(abs(In.view.x) - abs(In.view.z)) < THIC || abs(abs(In.view.x) - abs(In.view.y)) < THIC || abs(abs(In.view.y) - abs(In.view.z)) < THIC)) // 3d x, select normal
		if (Line && (abs(sqrt(square(In.view.x) + square(In.view.y)) - abs(In.view.z)) < THIC || lengthSqr(In.view) < THIC / 100.0f)) // z-cone x with dot
		//if (Line && abs(sqrt(square(In.view.x) + square(In.view.y)) - abs(In.view.z)) < THIC) // x no dot
		{
			Out.color.rgb = RED;
			return Out;
		}

		//float cosTheta = dot(In.normal, lightDir);
		//if (cosTheta < 0.0f) // we don't want bias behind shadows.
		//{
		//	cosTheta = 1.0f;
		//}
		//float bias = tan(acos(cosTheta)) / Lights[i].res;
		//float bias = (DepthBias + NormalBias * tan(acos(cosTheta))) / Lights[i].res;
		//bias = clamp(bias, 0.0f, 0.01f);
		//float shadow = ShadowTextures[i].SampleCmpLevelZero(DepthSampler, In.shadowRays[i], depth / Lights[i].far - bias);
		float shadow = ShadowTextures[i].SampleCmpLevelZero(DepthSampler, In.shadowRays[i], depth / Lights[i].far - DepthBias / Lights[i].res);
		Out.color.rgb = shadow >= 1.0f ? 1.0f : 0.0f;
		return Out;
		//float shadow = ShadowTextures[i].SampleCmpLevelZero(DepthSampler, In.shadowRays[i], depth / Lights[i].far);

		// Color calculations
		float lamb = dot(lightDir, In.normal);
		if (lamb > 0.0f)
		{
			// Cook-Torrance BRDF model
			float3 viewDir = normalize(In.view);
			float alpha = square(rough);
			float3 middle = normalize(lightDir + viewDir);
			float D = NormalDistribution(alpha, In.normal, middle);
			float G = Geometric(viewDir, lightDir, middle, In.normal, alpha);
			float F = Fresnel(ior, viewDir, middle);
			float rs = D * G * F / (4.0f * dot(In.normal, viewDir));
			Out.color.rgb += Lights[i].color * shadow * (lamb * (1.0f - shine) * diffuse + shine * rs * spec);
		}
	}

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
	//for (int i = 0; i < 3; i++)
	//{
	//	for (int j = 0; j < 3; j++)
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
	//for (int i = 0; i < 3; i++)
	//{
	//	for (int j = 0; j < 3; j++)
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
	//for (int i = 0; i < 3; i++)
	//{
	//	for (int j = 0; j < 3; j++)
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
	//for (uint i = 0; i < 2 * quality + 1; i++)
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
	for (int i = 0; i < 2 * quality + 1; i++)
	{
		for (int j = 0; j < 2 * quality + 1; j++)
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

float4 shadowBlurPixelShader(TextureShaderOutput In) : SV_Target
{
	// no blur

	//float4 color = 0.0f;
	//color.rgb = ShaderTexture.Sample(ColorSampler, In.tex).rgb;
	//return color;

	// mipmap

	//float4 color = 0.0f;
	//color.a = ShaderTexture.SampleLevel(ColorSampler, In.tex, 0).a;
	//color.rgb += ShaderTexture.SampleLevel(ColorSampler, In.tex, 1).rgb;
	//color.rgb += ShaderTexture.SampleLevel(ColorSampler, In.tex, 2).rgb * 2.0f;
	//color.rgb += ShaderTexture.SampleLevel(ColorSampler, In.tex, 3).rgb * 3.0f;
	//color.rgb /= 6.0f;
	//return color;

	// gaussian

	//float4 color = 0.0f;
	//color.a = ShaderTexture.Sample(ColorSampler, In.tex).a;
	//float kernel[49] = { 0, 0, 0, 5, 0, 0, 0, 0, 5, 18, 32, 18, 5, 0, 0, 18, 64, 100, 64, 18, 0, 5, 32, 100, 100, 100, 32, 5, 0, 18, 64, 100, 64, 18, 0, 0, 5, 18, 32, 18, 5, 0, 0, 0, 0, 5, 0, 0, 0 };
	//float total = 0.0f;
	//for (int i = 0; i < 7; i++)
	//{
	//	for (int j = 0; j < 7; j++)
	//	{
	//		if (abs(ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 3, i - 3)) / float2(Width, Height)).a - color.a) < 0.1f)
	//		{
	//			color.rgb += kernel[i * 7 + j] * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 3, i - 3)) / float2(Width, Height)).rgb;
	//			total += kernel[i * 7 + j];
	//		}
	//	}
	//}
	//color.rgb /= total;
	//return color;

	// custom gaussian

	float4 color = 0.0f;
	float2 col = ShaderTexture.Sample(ColorSampler, In.tex).ra;
	color.a = col.y;
	int quality = 5;
	int max = 2 * quality * quality + 1;
	float total = 0.0f;
	for (int i = 0; i < 2 * quality + 1; i++)
	{
		for (int j = 0; j < 2 * quality + 1; j++)
		{
			if (abs(ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 3, i - 3)) / float2(Width, Height)).a - color.a) < 0.1f)
			{
				float coefficient = max - (i - quality) * (i - quality) - (j - quality) * (j - quality);
				color.rgb += coefficient * ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - 3, i - 3)) / float2(Width, Height)).rgb;
				total += coefficient;
			}
		}
	}
	color.rgb /= total;
	return color;

	// box

	//int quality = 5;
	//float4 color = 0.0f;
	//color.a = ShaderTexture.Sample(ColorSampler, In.tex).a;
	//float total = 0.0f;
	//for (int i = 0; i < 2 * quality + 1; i++)
	//{
	//	for (int j = 0; j < 2 * quality + 1; j++)
	//	{
	//		if (abs(ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - quality, i - quality)) / float2(Width, Height)).a - color.a) < 0.1f)
	//		{
	//			color.rgb += ShaderTexture.Sample(ColorSampler, (In.position.xy + float2(j - quality, i - quality)) / float2(Width, Height)).rgb;
	//			total += 1.0f;
	//		}
	//	}
	//}
	//color.rgb /= total;
	//return color;
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
