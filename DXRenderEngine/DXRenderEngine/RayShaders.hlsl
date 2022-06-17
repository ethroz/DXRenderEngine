/////////////////////////////////////////
//            Definitions              //
/////////////////////////////////////////

#define NUM_MATERIALS 1
#define NUM_OBJECTS 1
#define NUM_TRIS 1
#define NUM_SPHERES 1
#define NUM_LIGHTS 1
#define NUM_RAYS 1
#define BOX false
#define PI 3.14159265359f
#define TWO_PI 6.28318530718f
#define MIN_POWER 0.005f
#define NORMAL_OFFSET 0.0001f
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
	float2 position : POSITION;
	float2 normal : NORMAL;
};

struct VertexShaderOutput
{
	float4 position : SV_POSITION;
	float3 direction : NORMAL;
};

struct Ray
{
	float3 origin;
	float residual;
	float3 direction;
	float3 tint;
};

struct Material
{
	float3 diffuse; // kd; ka = kd * atmosphere
	float rough; // sqrt(alpha)
	float3 spec; // ks
	float shine; // s
	float ior; // ior
	float3 padding;
};

struct Gameobject
{
	float3 position;
	float radius;
	uint startIndex;
	uint endIndex;
	float2 padding;
};

struct Triangle
{
	float3 vertices[3];
	float3 normals[3];
};

struct Sphere
{
	float3 position;
	float radius;
};

struct Light
{
	float3 position;
	float radius;
	float3 color;
	float luminosity;
};

struct RayHit
{
	float distance;
	float2 bary;
	int object;
	int tri;
	bool outside;
};

struct RayHitBlocked
{
	int object;
	bool blocked;
};

/////////////////////////////////////////
//              Buffers                //
/////////////////////////////////////////

cbuffer PerApplication : register(b0)
{
	float3 LowerAtmosphere;
	uint Width;
	float3 UpperAtmosphere;
	uint Height;
};

cbuffer Geometry : register(b1)
{
	Material Materials[NUM_MATERIALS];
	Gameobject Gameobjects[NUM_OBJECTS];
	Triangle Triangles[NUM_TRIS];
	Sphere Spheres[NUM_SPHERES];
	Light Lights[NUM_LIGHTS];
};

cbuffer PerFrame : register(b2)
{
	matrix EyeRot;
	float3 EyePos;
	float ModdedTime;
};

/////////////////////////////////////////
//             Functions               //
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

float3 refr(float3 incident, float3 normal, float ior)
{
	float cosi = dot(incident, normal);
	float etai = 1.0f;
	float etat = ior;
	if (cosi < 0)
		cosi = -cosi;
	else
	{ 
		float temp = etai;
		etai = etat;
		etat = temp;
		normal = -normal;
	}
	float eta = etai / etat;
	float k = 1 - eta * eta * (1 - cosi * cosi);
	if (k < 0.0f)
		return reflect(incident, normal);
	else
		return eta * incident + (eta * cosi - sqrt(k)) * normal;
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

float Fresnel2(float3 incident, float3 normal, float ior)
{
	float cosi = dot(incident, normal);
	float etai = 1.0f;
	float etat = ior;
	if (cosi > 0.0f)
	{ 
		float temp = etai;
		etai = etat;
		etat = temp;
	}
	else
	{
		cosi = -cosi;
	}

	float sint = etai / etat * sqrt(max(0.0f, 1.0f - cosi * cosi));
	if (sint >= 1.0f) 
	{
		return 1.0f;
	}
	else
	{
		float cost = sqrt(max(0.0f, 1.0f - sint * sint));
		float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
		float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
		return (Rs * Rs + Rp * Rp) / 2.0f;
	}
}

float3 TriangleNormal(Triangle tri, float2 bary)
{
	// normal blending does not depend on the order of normals here
	float z = 1.0f - bary.x - bary.y;
	return normalize(tri.normals[0] * z + tri.normals[1] * bary.x  + tri.normals[2] * bary.y);
}

// intersects.x is ray intersection distance, intersects.yz are barycentric coordinates
int TriangleBothsects(Ray ray, Triangle tri, out float3 intersects)
{
	float3 edge1 = tri.vertices[1] - tri.vertices[0];
	float3 edge2 = tri.vertices[2] - tri.vertices[0];
	float3 normal = cross(edge1, edge2);
	float det = -dot(ray.direction, normal);
	float invdet = 1.0f / det;
	float3 toTri = ray.origin - tri.vertices[0];
	float3 DAO = cross(toTri, ray.direction);
	intersects.x = dot(toTri, normal) * invdet;
	intersects.y = dot(edge2, DAO) * invdet;
	intersects.z = -dot(edge1, DAO) * invdet;
	if ((intersects.x >= 0.0f && intersects.y >= 0.0f
		&& intersects.z >= 0.0f && (intersects.y + intersects.z) <= 1.0f))
	{
		if (det >= 1e-6f) // in front
		{
			return 1;
		}
		else if (det <= -1e-6f) // behind
		{
			return -1;
		}
	}
	return 0; // no intersect
}

bool TriangleIntersect(Ray ray, Triangle tri, out float distance)
{
	float3 edge1 = tri.vertices[1] - tri.vertices[0];
	float3 edge2 = tri.vertices[2] - tri.vertices[0];
	float3 normal = cross(edge1, edge2);
	float det = -dot(ray.direction, normal);
	float invdet = 1.0f / det;
	float3 toTri = ray.origin - tri.vertices[0];
	float3 DAO = cross(toTri, ray.direction);
	distance = dot(toTri, normal) * invdet;
	float u = dot(edge2, DAO) * invdet;
	float v = -dot(edge1, DAO) * invdet;
	return (det >= 1e-6f && distance >= 0.0f && u >= 0.0f && v >= 0.0f && (u + v) <= 1.0f);
}

// first intersect is always smaller
float2 ObjectIntersects(Ray ray, Gameobject obj)
{
	float3 toObject = ray.origin - obj.position;
	float discriminant = square(dot(ray.direction, toObject)) - dot(toObject, toObject) + square(obj.radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return 1.#INF;
	}
	float out1 = -dot(ray.direction, ray.origin - obj.position);
	float out2 = sqrt(discriminant);
	return float2(out1 - out2, out1 + out2);
}

// first intersect is always smaller
float2 SphereIntersects(Ray ray, Sphere sph)
{
	float3 toSphere = ray.origin - sph.position;
	float discriminant = square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + square(sph.radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return 1.#INF;
	}
	float out1 = -dot(ray.direction, ray.origin - sph.position);
	float out2 = sqrt(discriminant);
	return float2(out1 - out2, out1 + out2);
}

float LightIntersect(Ray ray, Light lig)
{
	float3 toLight = ray.origin - lig.position;
	float discriminant = square(dot(ray.direction, toLight)) - lengthSqr(toLight) + square(lig.radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return 1.#INF;
	}
	float intersection = -dot(ray.direction, ray.origin - lig.position) - sqrt(discriminant);
	if (intersection <= 0.0f) // intersects behind ray
	{
		return 1.#INF;
	}
	return intersection;
}

RayHit Closest(Ray ray)
{
	RayHit hit;
	hit.object = -1;
	hit.tri = -1;
	hit.distance = 1.#INF;
	hit.bary = 1.#INF;
	hit.outside = true;
	for (uint i = 0; i < NUM_SPHERES; i++)
	{
		float2 intersects = SphereIntersects(ray, Spheres[i]);
		if (intersects.x < hit.distance && intersects.y > 0.0f)
		{
			hit.object = NUM_OBJECTS + i;
			if (intersects.x > 0.0f)
			{
				hit.distance = intersects.x;
				hit.outside = true;
			}
			else
			{
				hit.distance = intersects.y;
				hit.outside = false;
			}
		}
	}
	for (i = 0; i < NUM_LIGHTS; i++)
	{
		float dist = LightIntersect(ray, Lights[i]);
		if (dist < hit.distance)
		{
			hit.object = NUM_OBJECTS + NUM_SPHERES + i;
			hit.distance = dist;
		}
	}
	for (i = 0; i < NUM_OBJECTS; i++)
	{
		float2 intersects = ObjectIntersects(ray, Gameobjects[i]);
		if (intersects.x < hit.distance && intersects.y > 0.0f)
		{
			if (Materials[i].ior == 0.0f)
			{
				for (uint j = Gameobjects[i].startIndex; j < Gameobjects[i].endIndex; j++)
				{
					float3 distances;
					if (TriangleBothsects(ray, Triangles[j], distances) > 0 && distances.x < hit.distance)
					{
						hit.object = i;
						hit.tri = j;
						hit.distance = distances.x;
						hit.bary = distances.yz;
						hit.outside = true;
					}
				}
			}
			else
			{
				for (uint j = Gameobjects[i].startIndex; j < Gameobjects[i].endIndex; j++)
				{
					float3 distances;
					int temp = TriangleBothsects(ray, Triangles[j], distances);
					if (distances.x < hit.distance && temp != 0)
					{
						hit.object = i;
						hit.tri = j;
						hit.distance = distances.x;
						hit.bary = distances.yz;
						hit.outside = temp == 1;
					}
				}
			}
		}
	}

	return hit;
}

RayHitBlocked Occluder(Ray ray, float lightDist)
{
	RayHitBlocked hit;
	hit.blocked = true;
	int temp = -1;
	float best = lightDist;
	hit.object = -1;
	for (uint i = 0; i < NUM_SPHERES; i++)
	{
		float2 intersects = SphereIntersects(ray, Spheres[i]);
		if (intersects.x < lightDist && intersects.x > 0.0f)
		{
			if (Materials[NUM_OBJECTS + i].ior == 0.0f)
				return hit;
			if (intersects.x < best)
			{
				temp = NUM_OBJECTS + i;
				best = intersects.x;
			}
		}
	}
	for (i = 0; i < NUM_OBJECTS; i++)
	{
		float2 intersects = ObjectIntersects(ray, Gameobjects[i]);
		if (intersects.x < lightDist && intersects.y > 0.0f)
		{
			if (Materials[i].ior == 0.0f)
			{
				for (uint j = Gameobjects[i].startIndex; j < Gameobjects[i].endIndex; j++)
				{
					float dist;
					if (TriangleIntersect(ray, Triangles[j], dist) && dist < lightDist)
						return hit;
				}
			}
			else
			{
				for (uint j = Gameobjects[i].startIndex; j < Gameobjects[i].endIndex; j++)
				{
					float dist;
					if (TriangleIntersect(ray, Triangles[j], dist) && dist < best)
					{
						temp = i;
						best = dist;
					}
				}
			}
		}
	}

	hit.object = temp;
	hit.blocked = false;
	return hit;
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

VertexShaderOutput vertexShader(VertexShaderInput In)
{
	VertexShaderOutput Out;
	Out.position = float4(In.position, 0.0f, 1.0f);
	Out.direction = mul((float3x3)EyeRot, float3(In.normal, 0.1f));
	return Out;
}

// gpu raytracer
float3 pixelShader(VertexShaderOutput In) : SV_TARGET
{
	//return random(In.position.xy);
	//return float3(In.position.x / Width, In.position.y / Height, min((Width - In.position.x) / Width, (Height - In.position.y) / Height));

    Ray rays[NUM_RAYS];
	float3 color = 0.0f;
	rays[0].origin = In.direction + EyePos;
	rays[0].direction = normalize(In.direction);
	rays[0].residual = 1.0f;
	rays[0].tint = 1.0f;
	
	// unrolled to avoid artifacts
	[unroll]
	for (uint i = 0; i < NUM_RAYS / 2; i++)
	{
		uint iRefl = (2 * i) + 1;
		uint iRefr = iRefl + 1;

		// remove invisible rays
		if (rays[i].residual < MIN_POWER)
		{
			rays[iRefl].residual = 0.0f;
			rays[iRefr].residual = 0.0f;
			continue;
		}

		// get the closest object
		RayHit hit = Closest(rays[i]);

		// no collision
		if (hit.object == -1)
		{
			float amb = 0.5f + (0.5f * dot(rays[i].direction, ATMOSPHERE_DIR));
			float3 bg = amb * UpperAtmosphere + (1.0f - amb) * LowerAtmosphere;
			color += bg * rays[i].residual * rays[i].tint;
			rays[iRefl].residual = 0.0f;
			rays[iRefr].residual = 0.0f;
			continue;
		}

		// if light is closest
		if (hit.object >= NUM_OBJECTS + NUM_SPHERES)
		{
			color += Lights[hit.object - NUM_OBJECTS - NUM_SPHERES].color * rays[i].residual * rays[i].tint;
			rays[iRefl].residual = 0.0f;
			rays[iRefr].residual = 0.0f;
			continue;
		}

		// update next rays
		rays[iRefl].residual = rays[i].residual;
		rays[iRefl].tint = rays[i].tint;
		rays[iRefr].residual = rays[i].residual;
		rays[iRefr].tint = rays[i].tint;

		rays[iRefl].origin = rays[i].origin + rays[i].direction * hit.distance; // ray hit

		// normal
		float3 normal;
		if (hit.object >= NUM_OBJECTS)
		{
			normal = normalize(rays[iRefl].origin - Spheres[hit.object - NUM_OBJECTS].position);
		}
		else 
		{
			normal = TriangleNormal(Triangles[hit.tri], hit.bary);
		}

		// refraction specifics
		float3 rd;
		if (Materials[hit.object].ior == 0.0f)
		{
			// atmospheric lighting
			float amb = 0.5f + (0.5f * dot(normal, ATMOSPHERE_DIR));
			float3 bg = amb * UpperAtmosphere + (1.0f - amb) * LowerAtmosphere;
			rd = Materials[hit.object].diffuse;
			color += rd * bg; 

			// no refraction
			rays[iRefl].residual *= Materials[hit.object].shine;
			rays[iRefr].residual = 0.0f;
		}
		else
		{
			rd = 0.0f;

			// ignore placeholder rays that will not be cast
			if (i < NUM_RAYS / 4)
			{
				// refration ray
				rays[iRefr].origin = rays[iRefl].origin + normal * NORMAL_OFFSET * (hit.outside ? -1 : 1);
				rays[iRefr].direction = refr(rays[i].direction, normal, Materials[hit.object].ior);

				// refration resisuals
				float reflectance = Fresnel2(rays[i].direction, normal, Materials[hit.object].ior);
				rays[iRefl].residual *= reflectance;
				rays[iRefr].residual *= (1.0f - reflectance);
				rays[iRefr].tint *= Materials[hit.object].diffuse;
			}
		}

		// reflection ray
		if (dot(rays[i].direction, normal) < 0.0f)
		{
			rays[iRefl].direction = normalize(reflect(rays[i].direction, normal));
		}
		else
		{
			// reduces artifacts for low poly objects
			rays[iRefl].direction = rays[i].direction;
		}
		rays[iRefl].origin += normal * NORMAL_OFFSET;

		// do not perform direct lighting on the inside of transparent objects
		if (!hit.outside)
		{
			continue;
		}

		// direct lighting / specular
		for (uint j = 0; j < NUM_LIGHTS; j++)
		{
			float3 toLight = Lights[j].position - rays[iRefl].origin;

			// check if the surface is facing the light
			if (dot(toLight, normal) > 0)
			{
				float dist = length(toLight);

				Ray ray2;
				ray2.direction = toLight / dist;
				ray2.origin = rays[iRefl].origin;

				RayHitBlocked hit2 = Occluder(ray2, dist);

				if (!hit2.blocked)
				{
					float3 lc = (rays[i].residual * Lights[j].color * rays[i].tint);

					// lit surface
					if (hit2.object == -1)
					{
						// cook-torrance brdf model
						float lamb = dot(ray2.direction, normal);
						float3 v = -rays[i].direction;
						float s = Materials[hit.object].shine;
						float d = 1.0f - s;
						float alpha = square(Materials[hit.object].rough);
						float3 middle = normalize(ray2.direction + v);
						float D = NormalDistribution(alpha, normal, middle);
						float G = Geometric(v, ray2.direction, middle, normal, alpha);
						float F = Fresnel(Materials[hit.object].ior, v, middle);
						float rs = D * G * F / (4.0f * dot(normal, v)); // removed lamb from denominator
						color += lc * (lamb * d * rd + s * rs * Materials[hit.object].spec);
					}
					// transparent occluder and opaque receiver
					else if (Materials[hit.object].ior == 0.0f)
					{
						float3 objectLight;
						float3 objectEdge;
						if (hit2.object >= NUM_OBJECTS)
						{
							int index = hit2.object - NUM_OBJECTS;
							objectLight = normalize(Lights[j].position - Spheres[index].position);
							objectEdge = Spheres[index].position - objectLight * Spheres[index].radius;
						}
						else
						{
							objectLight = normalize(Lights[j].position - Gameobjects[hit2.object].position);
							objectEdge = Gameobjects[hit2.object].position - objectLight * Gameobjects[hit2.object].radius;
						}

						// angle and diffuse calculation
						float3 toEdge = normalize(objectEdge - ray2.origin);
						float angle = saturate(dot(toEdge, objectLight));
						float diffuse = dot(toEdge, normal);

						color += 0.92f * angle * diffuse * Materials[hit2.object].diffuse * lc;
					}
					// otherwise, shadows
				}
				// otherwise, shadows
			}
		}
	}
	
	color += ((random(In.position.xy) - 0.5f) / 255.0f);
	return color;
}