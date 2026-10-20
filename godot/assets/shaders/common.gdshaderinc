// Shared by every world shader: the fog of war, the strategic (inked map) view and the night.
uniform sampler2D fog_tex : filter_linear, repeat_disable;
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float world_size = 1024.0;
uniform float night = 0.0;
uniform float strategic = 0.0;
// Trial painted look (--style painted): banded cel light, rim, richer colour.
uniform float toon = 0.0;
// The season, blended across its turns (summer is the baseline look): snow lying in winter, turned leaves in autumn,
// fresh greens in spring. Set on every material by MapView from Seasons.Look.
uniform float snow = 0.0;
uniform float autumn = 0.0;
uniform float spring = 0.0;

const vec3 PARCHMENT = vec3(0.89, 0.83, 0.70);
const vec3 INK = vec3(0.30, 0.23, 0.16);

vec3 to_linear(vec3 c) { return c * c * (c * 0.305306 + 0.682171) + c * 0.012522; }
float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

// x: unseen (0 live, ~0.4 described, ~0.7 stale, 1 unknown), y: described by a report.
// The lookup wobbles a few tiles so the edges of what is known wander like a painted map's.
vec2 fog_at(vec2 p) {
	vec2 w = (texture(noise_tex, p * 0.021).rg - 0.5) * 7.0;
	return texture(fog_tex, (p + w) / world_size).rg;
}

// For culling whole objects under the fog in the vertex shader: true when the ground is unknown.
bool hidden_by_fog(vec2 p) { return textureLod(fog_tex, p / world_size, 0.0).r > 0.9; }

float cloud_noise(vec2 p, float t) {
	float a = texture(noise_tex, p * 0.0045 + vec2(t * 0.0021, t * 0.0013)).r;
	float b = texture(noise_tex, p * 0.013 - vec2(t * 0.0034, -t * 0.0018)).g;
	return a * 0.65 + b * 0.35;
}

// Seen ground keeps its colour; ground known only from reports fades to a faded sepia memory;
// unknown ground lies under drifting painted cloud with a pale mist along its edge.
vec3 apply_fog(vec3 col, vec2 p, float t, out float cloud) {
	float unseen = fog_at(p).r;
	float n = cloud_noise(p, t);
	float memory = smoothstep(0.08, 0.4, unseen);
	vec3 faded = mix(vec3(luma(col)) * vec3(1.02, 0.96, 0.86), col, 0.45) * 0.8;
	col = mix(col, faded, memory);
	cloud = smoothstep(0.62, 0.95, unseen + (n - 0.5) * 0.45);
	float swirl = texture(noise_tex, p * 0.03 + vec2(n * 0.4, t * 0.004)).b;
	vec3 sky = mix(vec3(0.12, 0.14, 0.17), vec3(0.40, 0.44, 0.49), smoothstep(0.3, 0.85, n * 0.7 + swirl * 0.3));
	float rim = smoothstep(0.5, 0.78, unseen + (n - 0.5) * 0.3) * (1.0 - smoothstep(0.78, 0.98, unseen + (n - 0.5) * 0.3));
	col = mix(col, sky, cloud);
	return col + vec3(0.55, 0.6, 0.64) * rim * 0.35;
}

const vec3 SNOW = vec3(0.80, 0.85, 0.91);

// How much snow lies on a surface at p that faces up by `up` (world normal y): patchy, thinning on slopes,
// thickening as winter deepens.
float snow_cover(vec2 p, float up) {
	if (snow < 0.002) return 0.0;
	float n = texture(noise_tex, p * 0.035).r * 0.5 + texture(noise_tex, p * 0.16).g * 0.32 + texture(noise_tex, p * 0.7).b * 0.18;
	float amount = snow * smoothstep(0.25, 0.75, up);
	float d = amount * 0.6 - n;
	return max(smoothstep(-0.03, 0.04, d), smoothstep(-0.22, 0.04, d) * 0.4) * smoothstep(0.0, 0.25, snow);
}

// Snow over a colour: blue-white in the hollows of the noise, brightest on the crests.
vec3 lay_snow(vec3 c, vec2 p, float cover) {
	float g = texture(noise_tex, p * 0.5).a;
	return mix(c, SNOW * (0.9 + 0.12 * g) - vec3(0.04, 0.02, 0.0) * (1.0 - g), cover);
}

// How green a colour is (grass, leaves): 0 for bark, stone, thatch and paint.
float greenness(vec3 c) { return clamp((c.g - max(c.r, c.b)) * 10.0, 0.0, 1.0); }

// Green ground and grass toward straw in autumn, fresher in spring, a dull dormant olive under winter.
vec3 season_grass(vec3 c) {
	float g = greenness(c);
	float l = luma(c);
	vec3 straw = vec3(0.70, 0.60, 0.36) * (l / 0.46);
	vec3 fresh = c * vec3(0.93, 1.07, 0.86) + vec3(0.0, 0.02, 0.0);
	vec3 dormant = mix(vec3(0.44, 0.39, 0.30) * (l / 0.42), c, 0.15);
	c = mix(c, straw, autumn * g * 0.55);
	c = mix(c, fresh, spring * g * 0.9);
	return mix(c, dormant, snow * (0.4 + 0.5 * g));
}

// Diffuse for light(): Godot's lambert wrap, or with toon two soft bands with the shadow folded into the dark one.
float cel(float ndl, float atten, float rough) {
	float wrap = max(0.0, (ndl + rough) / ((1.0 + rough) * (1.0 + rough))) * atten;
	float lit = smoothstep(0.0, 0.1, ndl) * smoothstep(0.3, 0.6, atten);
	float band = mix(0.17, 0.44, lit) + 0.08 * clamp(ndl, 0.0, 1.0) * lit;
	return mix(wrap, band, toon);
}

// A warm sunlit rim on silhouettes, Northgard's soft halo.
vec3 rim(vec3 n, vec3 v) {
	return vec3(1.0, 0.9, 0.72) * pow(1.0 - clamp(dot(n, v), 0.0, 1.0), 4.0) * 0.16 * toon * (1.0 - night) * (1.0 - strategic);
}
