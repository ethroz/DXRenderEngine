/////////////////////////////////////////
//            Declarations             //
/////////////////////////////////////////

struct VertexShaderInput
{
	float4 normal : NORMAL;
	float4 color : COLOR;
	float3 position : POSITION;
};

struct VertexShaderOutput
{
	float4 position : SV_POSITION;
	float4 positionws : POSITION0;
	float4 lightViewPosition : POSITION1;
	float4 color : COLOR;
	float3 normal : NORMAL;
};

struct PixelInputType
{
	float4 position : SV_POSITION;
	float4 depthPosition : POSITION;
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

struct Ray
{
	float3 origin;
	float3 direction;
};

/////////////////////////////////////////
//              Buffers                //
/////////////////////////////////////////

Texture2D ShaderTexture : register(t0);
Texture2D DepthTexture : register(t1);
SamplerState ColorSampler : register(s0);
SamplerState DepthSampler : register(s1);

cbuffer MatrixBuffer : register(b0)
{
	matrix ProjectionMatrix;
	matrix ViewMatrix;
	matrix WorldMatrix;
	matrix NormalMatrix;
	matrix LightProjectionMatrix;
	int LightIndex;
};

cbuffer MainBuffer : register(b1)
{
	float3x3 EyeRot;
	float3 EyePos;
	float Width;
	float3 BGCol;
	float Height;
	float MinBrightness;
	float ModdedTime;
	uint RayDepth;
	uint NumTris;
	uint NumSpheres;
	uint NumLights;
};

cbuffer GeometryBuffer : register(b2)
{
    float3 Vertices[1];
    float4 Normals[1];
    float4 Colors[1];
    float4 Spheres[1];
};

cbuffer LightBuffer : register(b3)
{
	float4 Lights[1];
};

/////////////////////////////////////////
//              Methods                //
/////////////////////////////////////////

float Square(float value)
{
	return value * value;
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

float luminance(float3 v)
{
	return 0.2126f * v.r + 0.7152f * v.g + 0.0722f * v.b;
}

float3 refr(float3 I, float3 N, float ior)
{
	float cosi = dot(I, N);
	float etai = 1.0f;
	float etat = ior;
	float3 n = N;
	if (cosi < 0)
		cosi = -cosi;
	else
	{ 
		float temp = etai;
		etai = etat;
		etat = temp;
		n = -N;
	}
	float eta = etai / etat;
	float k = 1 - eta * eta * (1 - cosi * cosi);
	if (k < 0.0f)
		return reflect(I, n);
	else
		return eta * I + (eta * cosi - sqrt(k)) * n;
}

float TriangleInterpolation(float3 e, float3 f, float3 g)
{
	return length(cross(f, g)) / length(cross(f, e));
}

float TriangleIntersect(Ray ray, float3 v0, float3 v1, float3 v2)
{
	float3 n = normalize(cross(v1 - v0, v2 - v0));
	float numerator = dot(n, v0 - ray.origin);
	float denominator = dot(n, ray.direction);
	if (denominator >= 0.0f) // not facing camera
	{
		return -1.0f;
	}
	float intersection = numerator / denominator;
	if (intersection <= 0.0f) // intersects behind camera
	{
		return -1.0f;
	}

	// test if intersection is inside triangle ////////////////////////////
	float3 pt = ray.origin + ray.direction * intersection;
	float3 edge0 = v1 - v0;
	float3 edge1 = v2 - v1;
	float3 edge2 = v0 - v2;
	float3 C0 = pt - v0;
	float3 C1 = pt - v1;
	float3 C2 = pt - v2;
	if (!(dot(n, cross(C0, edge0)) <= 0 &&
		dot(n, cross(C1, edge1)) <= 0 &&
		dot(n, cross(C2, edge2)) <= 0))
	{
		return -1.0f; // point is outside the triangle
	}
	return intersection;
}

float SphereIntersect(Ray ray, float3 position, float radius, uint intersect)
{
	float3 toSphere = ray.origin - position;
	float discriminant = Square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + Square(radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return -1.0f;
	}
	float intersection = -dot(ray.direction, ray.origin - position);
	if (intersect == 1)
		intersection -= sqrt(discriminant);
	else if (intersect == 2)
		intersection += sqrt(discriminant);
	else
		return -1.0f;
	if (intersection <= 0.0f) // intersects behind camera
	{
		return -1.0f;
	}
	return intersection;
}

float3 RayCastEthan(Ray ray, uint itteration)
{
	float prevSpec = 1.0f;
    float distances[1];
	itteration++;
	for (uint j = 0; j < NumTris; j++)
	{
		distances[j] = TriangleIntersect(ray, Vertices[j * 3], Vertices[j * 3 + 1], Vertices[j * 3 + 2]);
	}
	for (j = 0; j < NumSpheres; j++)
	{
		distances[NumTris + j] = SphereIntersect(ray, Spheres[j * 2].xyz, Spheres[j * 2].w, 1);
	}
	for (j = 0; j < NumLights; j++)
	{
		distances[NumTris + NumSpheres + j] = SphereIntersect(ray, Lights[j * 3].xyz, Lights[j * 3 + 2].x, 1);
	}
	uint index = -1;
	float bestDistance = 1.#INF;
	for (j = 0; j < distances.Length; j++)
	{
		if (distances[j] != -1.0f && distances[j] < bestDistance)
		{
			index = j;
			bestDistance = distances[j];
		}
	}
	if (index != -1)
	{
		if (index >= NumTris + NumSpheres) // if light is closest
		{
			return Lights[(index - NumTris - NumSpheres) * 3 + 1].xyz * Lights[(index - NumTris - NumSpheres) * 3 + 2].y;
		}
		else // if sphere or tri is closest
		{
			ray.origin = ray.origin + ray.direction * bestDistance;
			float4 col;
			float3 normal;
			float spec = 0.0f;
			if (index >= NumTris) // if sphere is closest
			{
				col = Spheres[(index - NumTris) * 2 + 1];
				normal = normalize(ray.origin - Spheres[(index - NumTris) * 2].xyz);
			}
			else // if tri is closest
			{
				col = Colors[index];
				normal = Normals[index].xyz;
			}
			ray.direction = normalize(reflect(ray.direction, normal)); // reflect the ray off of the surface for the next raycast
			float3 color;
			for (uint k = 0; k < NumLights; k++)
			{
				float3 toLight = Lights[k * 3].xyz - ray.origin;
				if (dot(toLight, normal) > 0) // check if the surface is facing the light
				{
					Ray ray2;
					ray2.direction = normalize(toLight);
					ray2.origin = ray.origin;
					uint count = 0;
					for (uint m = 0; m < NumTris; m++)
					{
						float dist = TriangleIntersect(ray2, Vertices[m * 3], Vertices[m * 3 + 1], Vertices[m * 3 + 2]);
						if (dist != -1.0f && dist < length(toLight)) // check for shadows from tris
							count++;
					}
					for (m = 0; m < NumSpheres; m++)
					{
						float dist = SphereIntersect(ray2, Spheres[m * 2].xyz, Spheres[m * 2].w, 1);
						if (dist != -1.0f && dist < length(toLight)) // check for shadows from spheres
							count++;
					}
					if (count == 0) // check if there are no objects between the light and the sphere
					{
						float brightness = Lights[k * 3 + 2].y / 4.0f / 3.14f / dot(toLight, toLight);
						color += Lights[k * 3 + 1].xyz * col.xyz * max(lerp(0, brightness, prevSpec), MinBrightness);
						if (brightness > 1)
							spec += 1.0f;
						else
							spec += max(lerp(0, brightness, prevSpec), MinBrightness);
					}
					else
					{
						color += col.xyz * max(MinBrightness * prevSpec, MinBrightness);
						//spec += prevSpec;
						spec += MinBrightness;
					}
				}
				else
				{
					color += col.xyz * max(MinBrightness * prevSpec, MinBrightness);
					spec += MinBrightness;
				}
			}
			spec /= NumLights;
			prevSpec *= col.w * spec;
			return color;
		}
	}
	else
	{
		return BGCol * prevSpec;
		//color /= itteration;
		//return color;
	}
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

// cpu rasterizer
VertexShaderOutput vertexShaderPassthrough(VertexShaderInput In)
{
	VertexShaderOutput Out;
	Out.position = float4(In.position, 1.0f);
	Out.positionws = float4(In.position, 1.0f);
	Out.normal = In.normal.xyz;
	Out.color = In.color;
	return Out;
}

float3 pixelShaderPassthrough(VertexShaderOutput In) : SV_TARGET
{
	return In.color.xyz + ((random(In.position.xy) - 0.5f) / 255.0f);
}

// shadow rasterizer
PixelInputType shadowVertexShader(float3 position : POSITION)
{
	PixelInputType Out;
	Out.position = mul(LightProjectionMatrix, float4(position, 1.0f));
	Out.depthPosition = Out.position;
	return Out;
}

float4 shadowPixelShader(PixelInputType In) : SV_TARGET
{
	//return In.position.z / In.position.w;
	return In.depthPosition.z / In.depthPosition.w;
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

// gpu rasterizer
VertexShaderOutput vertexShader(VertexShaderInput In)
{
	VertexShaderOutput Out;
	Out.position = mul(WorldMatrix, float4(In.position, 1.0f));
	Out.positionws = Out.position;
	Out.position = mul(ViewMatrix, Out.position);
	Out.positionws.w = Out.position.z;
	Out.position = mul(ProjectionMatrix, Out.position);
	Out.lightViewPosition = mul(LightProjectionMatrix, float4(In.position, 1.0f));
	if (In.normal.w != 1.0f)
		Out.normal = normalize(mul((float3x3)NormalMatrix, In.normal.xyz));
	else
		Out.normal = 0.0f;
	Out.color = In.color;
	return Out;
}

PixelShaderOutput pixelShader(VertexShaderOutput In)
{
	PixelShaderOutput Out;
	Out.depth = In.position.z;
	In.normal = normalize(In.normal);
	float col = ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height)).r;
	if (LightIndex != 0)
		Out.color = float4(col, col, col, In.positionws.w);
	else
		Out.color = float4(0.0f, 0.0f, 0.0f, In.positionws.w);
	float2 projectTexCoord;
	projectTexCoord.x = 0.5f + (In.lightViewPosition.x / In.lightViewPosition.w * 0.5f);
	projectTexCoord.y = 0.5f - (In.lightViewPosition.y / In.lightViewPosition.w * 0.5f);

	// Determine if the projected coordinates are in the 0 to 1 range.  If so then this pixel is in the view of the light.
	if ((saturate(projectTexCoord.x) == projectTexCoord.x) && (saturate(projectTexCoord.y) == projectTexCoord.y))
	{
		float depthValue = DepthTexture.Sample(DepthSampler, projectTexCoord).x;
		float lightDepthValue = In.lightViewPosition.z / In.lightViewPosition.w;
		lightDepthValue = lightDepthValue - Lights[LightIndex * 3 + 2].z;

		if (lightDepthValue < depthValue)
		{
			Out.color.rgb += 1.0f;
		}
	}
	if (LightIndex == NumLights - 1)
		Out.color.rgb /= NumLights;
	return Out;
}

PixelShaderOutput pixelShader2(VertexShaderOutput In)
{
	PixelShaderOutput Out;
	Out.depth = In.position.z;
	Out.color = float4(0.0f, 0.0f, 0.0f, In.positionws.w);
	In.normal = normalize(In.normal);
	float4 col = ShaderTexture.Sample(ColorSampler, In.position.xy / float2(Width, Height));
	if ((col.r == 0.0f && col.g == 0.0f && col.b == 0.0f))
		return Out;
	for (uint i = 0; i < NumLights; i++)
	{
		float3 toLight = Lights[i * 3].xyz - In.positionws.xyz;
		if (dot(In.normal, toLight) > 0)
		{
			float len = length(toLight);
			toLight /= len;
			float brightness = Lights[i * 3 + 2].y / (len * len);
			Out.color.rgb += In.color.xyz * Lights[i * 3 + 1].xyz * brightness * saturate(dot(In.normal, toLight));
			float3 reflectionVector = normalize(reflect(In.positionws.xyz - EyePos, In.normal));
			Out.color.rgb += pow(saturate(dot(reflectionVector, toLight)), pow(2, In.color.w * 12.0f + 1.0f)) * brightness;
		}
	}
	Out.color.rgb *= col.rgb;
	return Out;
}

// postprocessing pixel shader
float3 postProcessPixelShader(TextureShaderOutput In) : SV_Target
{
	float depth = ShaderTexture.SampleLevel(ColorSampler, In.position.xy / float2(Width, Height), 0).w;
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
	return 0.0f;
	
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

// gpu raytracer
float3 rayPixelShader(TextureShaderOutput In) : SV_TARGET
{
	//return random(In.position.xy);
	//return float3(In.position.x / Width, In.position.y / Height, min((Width - In.position.x) / Width, (Height - In.position.y) / Height));

	// cross hair
	/*float2 p = In.position.xy;
	if (p.x > Width / 2 - 5 && p.x < Width / 2 + 5 && p.y > Height / 2 - 1 && p.y < Height / 2 + 1)
		return 1.0f;
	else if (p.y > Height / 2 - 5 && p.y < Height / 2 + 5 && p.x > Width / 2 - 1 && p.x < Width / 2 + 1)
		return 1.0f;*/

	float3 color = 0.0f;
	uint raycont;
    float distances[1];
    Ray rays[1];
	float dropOff[1];
	float pitch = (In.position.y * -2.0f / Height + 1.0f) * (Height / Width) * 0.1f;
	float yaw = (In.position.x * 2.0f / Width - 1.0f) * 0.1f;
	float3 direction = float3(yaw, pitch, 0.1f);
	direction = mul(EyeRot, direction);
	rays[0].origin = direction + EyePos;
	rays[0].direction = normalize(direction);
	//return RayCastEthan(ray, 1);

	dropOff[0] = 1.0f;
	uint count = 1;
	for (uint i = 0; i < RayDepth; i++)
	{
		for (uint j = 0; j < count; j++)
		{
			if (rays[j].direction.x * rays[j].direction.x + rays[j].direction.y * rays[j].direction.y + rays[j].direction.z * rays[j].direction.z == 0.0f)
				break;
			raycont++;
			for (uint k = 0; k < NumTris; k++)
			{
				distances[k] = TriangleIntersect(rays[j], Vertices[k * 3], Vertices[k * 3 + 1], Vertices[k * 3 + 2]);
			}
			for (k = 0; k < NumSpheres; k++)
			{
				distances[NumTris + k] = SphereIntersect(rays[j], Spheres[k * 3].xyz, Spheres[k * 3].w, 1);
			}
			for (k = 0; k < NumLights; k++)
			{
				if (Lights[k * 3 + 2].x != 0)
					distances[NumTris + NumSpheres + k] = SphereIntersect(rays[j], Lights[k * 3].xyz, Lights[k * 3 + 2].x, 1);
				else
					distances[NumTris + NumSpheres + k] = 1.#INF;
			}
			uint index = -1;
			float bestDistance = 1.#INF;
			for (k = 0; k < distances.Length; k++)
			{
				if (distances[k] != -1.0f && distances[k] < bestDistance)
				{
					index = k;
					bestDistance = distances[k];
				}
			}
			if (index != -1)
			{
				if (index >= NumTris + NumSpheres) // if light is closest
				{
					color += Lights[(index - NumTris - NumSpheres) * 3 + 1].xyz * Lights[(index - NumTris - NumSpheres) * 3 + 2].y;
					rays[j].direction = 0.0f;
				}
				else // if sphere or tri is closest
				{
					rays[j].origin = rays[j].origin + rays[j].direction * bestDistance;
					direction = -rays[j].direction;
					float4 col;
					float3 normal;
					if (index >= NumTris) // if sphere is closest, normal is from centre of sphere to intersect
					{
						col = Spheres[(index - NumTris) * 3 + 1];
						normal = normalize(rays[j].origin - Spheres[(index - NumTris) * 3].xyz);
					}
					else // if tri is closest, blend between each vertex normal
					{
						col = Colors[index];
						uint4 indices = uint4(0, 1, 2, 0);
						if (Normals[index * 3].w == 1.0f) // if the triangle is special needs
							indices = uint4(1, 2, 0, 1);
						else if (Normals[index * 3 + 1].w == 1.0f)
							indices = uint4(0, 2, 1, 1);
						else if (Normals[index * 3 + 2].w == 1.0f)
							indices.w = 1;
						Ray a;
						Ray b;
						a.origin = Vertices[index * 3 + indices.x];
						a.direction = Vertices[index * 3 + indices.y] - Vertices[index * 3 + indices.x];
						b.origin = Vertices[index * 3 + indices.z];
						b.direction = rays[j].origin - Vertices[index * 3 + indices.z];
						float c = TriangleInterpolation(a.direction, b.direction, Vertices[index * 3 + indices.z] - Vertices[index * 3 + indices.x]);
						float3 intersect = a.origin + a.direction * c;
						float d = length(b.direction) / length(intersect - Vertices[index * 3 + indices.z]);
						normal = normalize(lerp(Normals[index * 3 + indices.x].xyz, Normals[index * 3 + indices.y].xyz, c));
						if (indices.w == 0)
							normal = normalize(lerp(Normals[index * 3 + indices.z].xyz, normal, d));
					}
					rays[j].direction = normalize(reflect(rays[j].direction, normal));
					color += col.xyz * MinBrightness; // ambient light
					for (k = 0; k < NumLights; k++)
					{
						float3 toLight = Lights[k * 3].xyz - rays[j].origin;
						if (dot(toLight, normal) > 0) // check if the surface is facing the light
						{
							float dist = length(toLight);
							Ray ray2;
							ray2.direction = normalize(toLight);
							ray2.origin = rays[j].origin;
							uint number = 0;
							float mult = 1.0f;
							bool dim = false;
							for (uint m = 0; m < NumTris; m++)
							{
								float d = TriangleIntersect(ray2, Vertices[m * 3], Vertices[m * 3 + 1], Vertices[m * 3 + 2]);
								if (d != -1.0f && d < dist) // check for shadows from tris
									number++;
							}
							for (m = 0; m < NumSpheres; m++)
							{
								float d = SphereIntersect(ray2, Spheres[m * 3].xyz, Spheres[m * 3].w, 1);
								if (d != -1.0f && d < dist)// && Spheres[m * 3 + 2].x == 1.#INF) // check for shadows from spheres
									number++;
								if (d != -1.0f && Spheres[m * 3 + 2].x != 0.0f) // check if intersecting sphere is transparent
								{
									dim = true;
									mult *= rcp(abs(Spheres[m * 3 + 2].x));
								}
							}
							if (number == 0 || dim) // check if there are no objects between the light and the sphere
							{
								float brightness = Lights[k * 3 + 2].y / dot(toLight, toLight);
								color += Lights[k * 3 + 1].xyz * col.xyz * brightness * saturate(dot(ray2.direction, normal)) * mult * dropOff[j]; // diffused light
								//float3 H = normalize(normalize(toLight) + direction);
								//color += pow(saturate(dot(normal, H)), pow(2, col.w * 14 + 2.0f)) * mult * dropOff[j];  												// blinn-phong specular light
								color += pow(saturate(dot(rays[j].direction, normalize(toLight))), pow(2, col.w * 12.0f + 1.0f)) * mult * dropOff[j] * brightness;	// phong specular light
							}
						}
					}
					dropOff[j] *= col.w;
					if (dropOff[j] <= MinBrightness)
						rays[j].direction = 0.0f;
					if (index >= NumTris)
					{
						if (Spheres[(index - NumTris) * 3 + 2].x != 0.0f)
						{
							// refract ray
							if (Spheres[(index - NumTris) * 3 + 2].x == 1.0f)
								raycont--;
							rays[count].origin = rays[j].origin;
							rays[count].direction = -direction;
							rays[count].direction = refr(rays[count].direction, normal, Spheres[(index - NumTris) * 3 + 2].x);
							float intersect = SphereIntersect(rays[count], Spheres[(index - NumTris) * 3].xyz, Spheres[(index - NumTris) * 3].w, 2);
							rays[count].origin = rays[count].origin + rays[count].direction * intersect;
							float3 normal2 = normalize(rays[count].origin - Spheres[(index - NumTris) * 3].xyz);
							rays[count].direction = refr(rays[count].direction, normal2, Spheres[(index - NumTris) * 3 + 2].x);
							dropOff[count] = dropOff[j];
							count++;
						}
					}
				}
			}
			else
			{
				color += BGCol;
				rays[j].direction = 0.0f;
				if (j > 0)
					raycont++;
				/*if (raycont > 1)
					raycont -= (0.2126f * (1.0f - BGCol.r) + 0.7152f * (1.0f - BGCol.g) + 0.0722f * (1.0f - BGCol.b));*/
			}
		}
	}
	color /= raycont;
	color += ((random(In.position.xy) - 0.5f) / 255.0f);
	return color;
}