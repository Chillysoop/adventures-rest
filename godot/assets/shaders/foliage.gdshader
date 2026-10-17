shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Instanced trees: vertex colour tinted per instance, a gentle sway, gone in the strategic view.
varying vec3 wpos;
varying float lift;

void vertex() {
	lift = VERTEX.y;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
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
	float cloud;
	c = apply_fog(c, wpos.xz, TIME, cloud);
	ALBEDO = to_linear(c);
	ROUGHNESS = 1.0;
}
