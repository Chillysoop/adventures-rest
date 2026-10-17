// Shared by every world shader: the fog of war, the strategic (inked map) view and the night.
uniform sampler2D fog_tex : filter_linear, repeat_disable;
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float world_size = 1024.0;
uniform float night = 0.0;
uniform float strategic = 0.0;

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
