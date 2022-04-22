/////////////////////////////////////////
//            Declarations             //
/////////////////////////////////////////

#define NUM_TRIS 1
#define NUM_SPHERES 1
#define NUM_LIGHTS 1
#define NUM_RAYS 1
#define RED float3(1.0f, 0.0f, 0.0f)

struct Ray
{
	float3 origin;
	float residual;
	float3 direction;
	float3 tint;
};

struct Triangle
{
	float3 vertices[3];
	float3 normals[3];
	float3 color;
	float reflectivity;
};

struct Sphere
{
	float3 position;
	float radius;
	float3 color;
	float ior;
	float reflectivity;
	float3 padding;
};

struct Light
{
	float3 position;
	float radius;
	float3 color;
	float padding;
};

/////////////////////////////////////////
//              Buffers                //
/////////////////////////////////////////

cbuffer PerApplication : register(b0)
{
	float3 BGCol;
	float MinBrightness;
	uint Width;
	uint Height;
	float2 padding;
};

cbuffer Geometry : register(b1)
{
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
//              Methods                //
/////////////////////////////////////////

float square(float value)
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

float TriangleInterpolation(float3 e, float3 f, float3 g)
{
	return length(cross(f, g)) / length(cross(f, e));
}

float TriangleIntersect(Ray ray, Triangle tri)
{
	float3 n = normalize(cross(tri.vertices[1] - tri.vertices[0], tri.vertices[2] - tri.vertices[0]));
	float numerator = dot(n, tri.vertices[0] - ray.origin);
	float denominator = dot(n, ray.direction);
	if (denominator >= 0.0f) // not facing camera
	{
		return 1.#INF;
	}
	float intersection = numerator / denominator;
	if (intersection <= 0.0f) // intersects behind camera
	{
		return 1.#INF;
	}

	// test if intersection is inside triangle ////////////////////////////
	float3 pt = ray.origin + ray.direction * intersection;
	float3 edge0 = tri.vertices[1] - tri.vertices[0];
	float3 edge1 = tri.vertices[2] - tri.vertices[1];
	float3 edge2 = tri.vertices[0] - tri.vertices[2];
	float3 C0 = pt - tri.vertices[0];
	float3 C1 = pt - tri.vertices[1];
	float3 C2 = pt - tri.vertices[2];
	if (dot(n, cross(C0, edge0)) <= 0 &&
		dot(n, cross(C1, edge1)) <= 0 &&
		dot(n, cross(C2, edge2)) <= 0)
	{
		return intersection;
	}
	return 1.#INF; // point is outside the triangle
}

float SphereIntersect(Ray ray, Sphere sph)
{
	float3 toSphere = ray.origin - sph.position;
	float discriminant = square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + square(sph.radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return 1.#INF;
	}
	float intersection = -dot(ray.direction, ray.origin - sph.position) - sqrt(discriminant);
	if (intersection <= 0.0f) // intersects behind ray
	{
		return 1.#INF;
	}
	return intersection;
}

float SphereOutersect(Ray ray, Sphere sph)
{
	float3 toSphere = ray.origin - sph.position;
	float discriminant = square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + square(sph.radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return 1.#INF;
	}
	float intersection = -dot(ray.direction, ray.origin - sph.position) + sqrt(discriminant);
	if (intersection <= 0.0f) // intersects behind camera
	{
		return 1.#INF;
	}
	return intersection;
}

float LightIntersect(Ray ray, Light lig)
{
	float3 toSphere = ray.origin - lig.position;
	float discriminant = square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + square(lig.radius);
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

float2 closest(Ray ray)
{
	int index = -1;
	float bestDistance = 1.#INF;
	for (uint j = 0; j < NUM_TRIS; j++)
	{
		float dist = TriangleIntersect(ray, Triangles[j]);
		if (dist < bestDistance)
		{
			index = j;
			bestDistance = dist;
		}
	}
	for (j = 0; j < NUM_SPHERES; j++)
	{
		float dist = SphereIntersect(ray, Spheres[j]);
		if (dist < bestDistance)
		{
			index = NUM_TRIS + j;
			bestDistance = dist;
		}
	}
	for (j = 0; j < NUM_LIGHTS; j++)
	{
		float dist = LightIntersect(ray, Lights[j]);
		if (dist < bestDistance)
		{
			index = NUM_TRIS + NUM_SPHERES + j;
			bestDistance = dist;
		}
	}

	return float2(bestDistance, index);
}

float2 closestIgnoreLights(Ray ray)
{
	float bestDistance = 1.#INF;
	int index = -1;
	for (uint j = 0; j < NUM_TRIS; j++)
	{
		float dist = TriangleIntersect(ray, Triangles[j]);
		if (dist < bestDistance)
		{
			bestDistance = dist;
			index = j;
		}
	}
	for (j = 0; j < NUM_SPHERES; j++)
	{
		float dist = SphereIntersect(ray, Spheres[j]);
		if (dist < bestDistance)
		{
			bestDistance = dist;
			index = j + NUM_TRIS;
		}
	}

	return float2(bestDistance, index);
}

float4 closestWithTint(Ray ray)
{
	float bestDistance = 1.#INF;
	float3 color = 1.0f;
	for (uint j = 0; j < NUM_TRIS; j++)
	{
		float dist = TriangleIntersect(ray, Triangles[j]);
		if (dist < bestDistance)
			return 1.#INF;
	}
	for (j = 0; j < NUM_SPHERES; j++)
	{
		float dist = SphereIntersect(ray, Spheres[j]);
		if (dist < bestDistance)
		{
			if (Spheres[j].ior == 0.0f)
				return 1.#INF;
			bestDistance = dist;
			color *= Spheres[j].color;
		}
	}

	return float4(color, bestDistance);
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

float4 vertexShader(float3 position : POSITION) : SV_Position
{
	return float4(position, 1.0f);
}

// gpu raytracer
float3 pixelShader(float4 position : SV_Position) : SV_TARGET
{
	//return Spheres[1].ior;
	//return random(position.xy);
	//return float3(position.x / Width, position.y / Height, min((Width - position.x) / Width, (Height - position.y) / Height));

	float3 color = 0.0f;
    Ray rays[NUM_RAYS];
	float pitch = (position.y * -2.0f / Height + 1.0f) * (Height / (float)Width) * 0.1f;
	float yaw = (position.x * 2.0f / Width - 1.0f) * 0.1f;
	float3 direction = float3(yaw, pitch, 0.1f);
	direction = mul((float3x3)EyeRot, direction);
	rays[0].origin = direction + EyePos;
	rays[0].direction = normalize(direction);
	rays[0].residual = 1.0f;
	rays[0].tint = 1.0f;

	for (uint i = 0; i < 3; i++)
	{
		if (rays[i].residual == 0.0f) // if ray isnt set
		{
			rays[2 * i + 1].residual = 0.0f;
			rays[2 * i + 2].residual = 0.0f;
			continue;
		}

		// get the closest object
		float2 data = closest(rays[i]);
		int cIndex = data.y;

		if (cIndex == -1) // no collision
		{
			color += BGCol * rays[i].residual * rays[i].tint;
			rays[2 * i + 1].residual = 0.0f;
			rays[2 * i + 2].residual = 0.0f;
			continue;
		}

		// if light is closest
		if (cIndex >= NUM_TRIS + NUM_SPHERES)
		{
			if (i == 0)
				color += Lights[cIndex - NUM_TRIS - NUM_SPHERES].color * rays[i].residual * rays[i].tint;
			rays[2 * i + 1].residual = 0.0f;
			rays[2 * i + 2].residual = 0.0f;
			continue;
		}

		// update next ray
		rays[2 * i + 1].residual = rays[i].residual;
		rays[2 * i + 1].tint = rays[i].tint;
		rays[2 * i + 2].residual = rays[i].residual;
		rays[2 * i + 2].tint = rays[i].tint;

		// if sphere or tri is closest
		rays[2 * i + 1].origin = rays[i].origin + rays[i].direction * data.x; // ray hit
		float3 normal;
		float3 col;
		float3 tint = 0.0f;
		float refl;
		if (cIndex >= NUM_TRIS) // if sphere is closest, normal is from centre of sphere to intersect
		{
			int temp = cIndex - NUM_TRIS;
			if (Spheres[temp].ior == 0.0f)
				col = Spheres[temp].color;
			else
			{
				col = 0.0f;
				tint = Spheres[temp].color;
			}
			rays[2 * i + 1].residual *= Spheres[temp].reflectivity;
			rays[2 * i + 2].residual *= (1.0f - Spheres[temp].reflectivity);
			refl = Spheres[temp].reflectivity;
			normal = normalize(rays[2 * i + 1].origin - Spheres[temp].position);
		}
		else // if tri is closest, blend between each vertex normal
		{
			col = Triangles[cIndex].color;
			rays[2 * i + 1].residual *= Triangles[cIndex].reflectivity;
			rays[2 * i + 2].residual *= 0.0f;
			refl = Triangles[cIndex].reflectivity;

			// different normal possibilities
			if (lengthSqr(Triangles[cIndex].normals[0]) == 0.0f)
			{
				float3 ad = Triangles[cIndex].vertices[2] - Triangles[cIndex].vertices[1];
				float3 bd = rays[2 * i + 1].origin - Triangles[cIndex].vertices[0];
				float c = TriangleInterpolation(ad, bd, Triangles[cIndex].vertices[0] - Triangles[cIndex].vertices[1]);
				normal = normalize(lerp(Triangles[cIndex].normals[1], Triangles[cIndex].normals[2], c));
			}
			else if (lengthSqr(Triangles[cIndex].normals[1]) == 0.0f)
			{
				float3 ad = Triangles[cIndex].vertices[2] - Triangles[cIndex].vertices[0];
				float3 bd = rays[2 * i + 1].origin - Triangles[cIndex].vertices[1];
				float c = TriangleInterpolation(ad, bd, Triangles[cIndex].vertices[1] - Triangles[cIndex].vertices[0]);
				normal = normalize(lerp(Triangles[cIndex].normals[0], Triangles[cIndex].normals[2], c));
			}
			else if (lengthSqr(Triangles[cIndex].normals[2]) == 0.0f)
			{
				float3 ad = Triangles[cIndex].vertices[1] - Triangles[cIndex].vertices[0];
				float3 bd = rays[2 * i + 1].origin - Triangles[cIndex].vertices[2];
				float c = TriangleInterpolation(ad, bd, Triangles[cIndex].vertices[2] - Triangles[cIndex].vertices[0]);
				normal = normalize(lerp(Triangles[cIndex].normals[0], Triangles[cIndex].normals[1], c));
			}
			else // typical normals
			{
				float3 ao = Triangles[cIndex].vertices[0];
				float3 ad = Triangles[cIndex].vertices[1] - Triangles[cIndex].vertices[0];
				float3 bd = rays[2 * i + 1].origin - Triangles[cIndex].vertices[2];
				float c = TriangleInterpolation(ad, bd, Triangles[cIndex].vertices[2] - Triangles[cIndex].vertices[0]);
				float3 intersect = ao + ad * c;
				float d = sqrt(lengthSqr(bd) / lengthSqr(intersect - Triangles[cIndex].vertices[2]));
				normal = normalize(lerp(Triangles[cIndex].normals[0], Triangles[cIndex].normals[1], c));
				normal = normalize(lerp(Triangles[cIndex].normals[2], normal, d));
			}
		}

		// reflection ray
		rays[2 * i + 1].direction = normalize(reflect(rays[i].direction, normal));

		// direct lighting / specular
		for (uint j = 0; j < NUM_LIGHTS; j++) 
		{
			float3 toLight = Lights[j].position - rays[2 * i + 1].origin;
			if (dot(toLight, normal) > 0) // check if the surface is facing the light
			{
				float lightDist = length(toLight);
				Ray ray2;
				ray2.direction = toLight / lightDist;
				ray2.origin = rays[2 * i + 1].origin;

				// check for shadows
				data = closestIgnoreLights(ray2);
				int sIndex = data.y;
				
				if (sIndex < 0) // no shadows
				{
					float diffuse = dot(ray2.direction, normal);
					float specular = pow(saturate(dot(ray2.direction, rays[2 * i + 1].direction)), 1.0f / refl - 1.0f);
					color += rays[i].residual * Lights[j].color * (col * diffuse + specular) * 0.5f * rays[i].tint;
				}
				else if (sIndex >= NUM_TRIS && Spheres[sIndex - NUM_TRIS].ior != 0.0f) // transparent object
				{
					sIndex -= NUM_TRIS;
					// refract through front 
					ray2.origin = ray2.origin + ray2.direction * data.x;
					float3 refrNormal = normalize(ray2.origin - Spheres[sIndex].position);
					float3 refracted = refr(ray2.direction, refrNormal, Spheres[sIndex].ior);
					float strength = Spheres[sIndex].ior * pow(dot(refracted, ray2.direction), Spheres[sIndex].ior * 100.0f); // strength calculation

					//refract through back
					ray2.direction = refracted;
					float intersect = SphereOutersect(ray2, Spheres[sIndex]);
					ray2.origin = ray2.origin + ray2.direction * intersect;
					refrNormal = normalize(ray2.origin - Spheres[sIndex].position);
					ray2.direction = refr(ray2.direction, refrNormal, Spheres[sIndex].ior);

					// check collision with light
					float4 data2 = closestWithTint(ray2);

					if (data2.w < 1.#INF)
					{
						return RED;
						color += rays[i].residual * (1.0f - Spheres[sIndex].reflectivity) * strength * 
							Spheres[sIndex].color * Lights[j].color * rays[i].tint * data2.rgb;
					}
				}
				//else // opaque object
				//{
				//	////////// soft shadows ////////////
				//}
			}
		}

		// refraction ray
		if (cIndex >= NUM_TRIS && i <= NUM_RAYS / 2 && Spheres[cIndex - NUM_TRIS].ior != 0.0f)
		{
			// setup
			cIndex -= NUM_TRIS;
			int next = 2 * i + 2;
			rays[next].origin = rays[next - 1].origin;
			rays[next].direction = rays[i].direction;

			// refract front
			rays[next].direction = refr(rays[next].direction, normal, Spheres[cIndex].ior);
			float intersect = SphereOutersect(rays[next], Spheres[cIndex]);

			//refract back
			rays[next].origin = rays[next].origin + rays[next].direction * intersect;
			normal = normalize(rays[next].origin - Spheres[cIndex].position);
			rays[next].direction = refr(rays[next].direction, normal, Spheres[cIndex].ior);
			
			// update tint
			rays[next].tint *= tint;
		}

		// remove invisible rays
		if (rays[2 * i + 1].residual < 0.01f)
			rays[2 * i + 1].residual = 0.0f;
		if (rays[2 * i + 2].residual < 0.01f)
			rays[2 * i + 2].residual = 0.0f;
	}
	
	color += ((random(position.xy) - 0.5f) / 255.0f);
	return color;
}