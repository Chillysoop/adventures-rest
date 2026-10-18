shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// The painted town plan: one texel per plan cell, colour premultiplied by alpha, so the bilinear
// filter blends neighbouring cells and a threshold on alpha draws smooth, round zone edges.
uniform sampler2D zones_tex : filter_linear, repeat_disable;
uniform float strength = 0.1;

void fragment() {
	vec4 z = texture(zones_tex, UV);
	float edge = smoothstep(0.42, 0.58, z.a);
	vec3 c = z.rgb / max(z.a, 0.001);
	// A brighter rim where the paint ends, so a zone reads as an area with a border.
	float rim = smoothstep(0.42, 0.5, z.a) * (1.0 - smoothstep(0.55, 0.7, z.a));
	ALBEDO = to_linear(mix(c, vec3(1.0), rim * 0.35) * (1.0 - 0.55 * night));
	ALPHA = (edge * strength + rim * strength * 1.4) * (1.0 - strategic);
}
