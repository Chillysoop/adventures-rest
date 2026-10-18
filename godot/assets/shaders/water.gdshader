shader_type spatial;
render_mode diffuse_lambert_wrap;

#include "res://assets/shaders/common.gdshaderinc"

// Opaque painted water: colour from the baked depth, foam along the shore, drifting ripples.
uniform sampler2D data_tex : filter_linear, repeat_disable;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = wpos.xz;
	float t = TIME;
	float depth = texture(data_tex, p / world_size).g;
	vec4 n1 = texture(noise_tex, p * 0.05 + vec2(t * 0.012, t * 0.007));
	vec4 n2 = texture(noise_tex, p * 0.11 - vec2(t * 0.009, -t * 0.013));
	vec3 col = mix(vec3(0.36, 0.58, 0.62), vec3(0.16, 0.34, 0.46), smoothstep(0.05, 0.45, depth));
	col += vec3(0.08, 0.1, 0.1) * smoothstep(0.55, 0.8, n1.r * 0.6 + n2.g * 0.4);
	float foam = (1.0 - smoothstep(0.0, 0.035, depth)) * smoothstep(0.45, 0.7, n2.r);
	col = mix(col, vec3(0.82, 0.88, 0.86), foam * 0.45);
	// Winter: the water runs dark and cold, and the shallows skin over with ice.
	col = mix(col, col * vec3(0.72, 0.8, 0.88), snow);
	float ice = snow * (1.0 - smoothstep(0.03, 0.12, depth + (n1.b - 0.5) * 0.08));
	col = mix(col, vec3(0.78, 0.86, 0.90) * (0.92 + 0.12 * n2.r), ice * 0.9);
	float cloud;
	col = apply_fog(col, p, t, cloud);
	// On the campaign map water is an ink wash, and unknown water is blank paper like the land under it.
	vec3 ink = mix(vec3(0.48, 0.60, 0.64), PARCHMENT * 0.82, smoothstep(0.75, 0.95, fog_at(p).r));
	col = mix(col, ink, strategic);
	ALBEDO = to_linear(col) * (1.0 - strategic * 0.8);
	NORMAL_MAP = normalize(vec3(n1.g - 0.5, n2.b - 0.5, 2.5)) * 0.5 + 0.5;
	ROUGHNESS = mix(mix(0.35, 0.6, ice), 1.0, max(cloud, strategic));
	SPECULAR = 0.3 * (1.0 - cloud);
	EMISSION = to_linear(col) * strategic * 0.95;
}
