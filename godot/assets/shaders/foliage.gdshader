shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Instanced trees: vertex colour tinted per instance, a gentle sway, gone in the strategic view.
// Instance alpha below 1 marks an evergreen (WorldView.Instances); the seasons pass it by but for the snow.
varying vec3 wpos;
varying float lift;
varying float up;
varying float seed;

void vertex() {
	lift = VERTEX.y;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	up = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz).y;
	seed = fract(sin(dot(MODEL_MATRIX[3].xz, vec2(12.9898, 78.233))) * 43758.5453);
	float sway = sin(TIME * 1.1 + wpos.x * 0.21 + wpos.z * 0.17) * 0.035 * max(VERTEX.y - 0.25, 0.0);
	VERTEX.x += sway;
	// Lit as if rounded: half the facet normal, half straight up, so crowns and blades read soft.
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.5));
	VERTEX *= 1.0 - strategic;
	// Trees on unknown ground are not drawn at all; the cloud paints over where they would be.
	if (hidden_by_fog(MODEL_MATRIX[3].xz)) VERTEX = vec3(0.0);
}

void fragment() {
	vec3 c = COLOR.rgb * (0.82 + 0.3 * clamp(lift / 1.6, 0.0, 1.0));
	bool evergreen = COLOR.a < 0.75;
	float leaf = greenness(COLOR.rgb);
	float l = luma(c);
	float speck = texture(noise_tex, wpos.xz * 2.7 + vec2(wpos.y * 2.3, seed * 9.0)).r;
	if (evergreen) {
		c = mix(c, c * vec3(0.85, 0.92, 1.0), snow * leaf);
		c = mix(c, c * vec3(0.95, 1.08, 0.9), spring * leaf * 0.6);
	} else {
		// Autumn: each crown its own gold, orange or russet, a few still turning; dappled within the crown.
		vec3 gold = vec3(0.80, 0.60, 0.20), orange = vec3(0.78, 0.40, 0.14), russet = vec3(0.56, 0.22, 0.12);
		vec3 turn = mix(mix(gold, orange, smoothstep(0.25, 0.5, seed)), russet, smoothstep(0.62, 0.85, seed));
		turn = mix(turn, vec3(0.62, 0.66, 0.22), step(0.92, seed) * 0.8);
		turn *= clamp(l / 0.4, 0.5, 1.1) * (0.8 + 0.35 * speck);
		c = mix(c, turn, autumn * leaf);
		// Winter: the broadleaves stand bare, grey-brown twigs.
		c = mix(c, vec3(0.50, 0.42, 0.35) * (0.8 + 0.6 * l), snow * leaf * 0.85);
		// Spring: bright new leaves, and blossom on some crowns.
		c = mix(c, c * vec3(0.96, 1.08, 0.86) + vec3(0.0, 0.02, 0.0), spring * leaf * 0.8);
		float dot_ = texture(noise_tex, wpos.xy * 4.5 + vec2(seed * 13.0)).g * texture(noise_tex, wpos.zy * 4.5 + vec2(0.37, seed * 7.0)).b;
		float bloom = step(0.5, seed) * smoothstep(0.36, 0.4, dot_) * leaf * spring * step(0.35, lift);
		c = mix(c, mix(vec3(0.93, 0.56, 0.70), vec3(0.97, 0.90, 0.88), step(0.9, seed)), bloom);
	}
	c = lay_snow(c, wpos.xz, snow_cover(wpos.xz + vec2(seed * 31.0), up * (evergreen ? 1.6 : 1.1) * (0.75 + 0.5 * speck)));
	// Painted: deeper, more saturated crowns with sunny tips.
	c = mix(c, mix(vec3(luma(c)), c, 1.3) * mix(0.85, 1.12, clamp(lift / 1.6, 0.0, 1.0)), toon * leaf);
	vec3 emit = rim(NORMAL, VIEW) * c * 2.5;
	float cloud;
	c = apply_fog(c, wpos.xz, TIME, cloud);
	EMISSION = to_linear(emit * (1.0 - cloud));
	ALBEDO = to_linear(c);
	ROUGHNESS = 1.0;
}

void light() {
	DIFFUSE_LIGHT += LIGHT_COLOR * cel(dot(NORMAL, LIGHT), ATTENUATION, ROUGHNESS) / PI;
}
