shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Soft see-through sheets (Scenery.cs). mode 0: mist banks lying low over water and in hollows, drifting noise that thins
// at their edges, as thick as `mist` (MapView sets it by hour, season and the sea). mode 1: falling water, streaks running
// down the sheet (UV.y 0 at the lip, 1 at the foot) with foam at both ends. Vertex colour tints; its alpha scales the sheet.

uniform float mode = 0.0;
uniform float mist = 0.0;

varying vec3 wpos;
varying vec4 tint;

void vertex() {
	tint = COLOR;
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (hidden_by_fog(wpos.xz) || strategic > 0.5) VERTEX = vec3(0.0);
}

void fragment() {
	vec3 col = to_linear(tint.rgb);
	if (mode < 0.5) {
		vec2 p = UV * 2.0 - 1.0;
		float edge = 1.0 - smoothstep(0.45, 1.0, length(p));
		float n = texture(noise_tex, wpos.xz * 0.011 + TIME * vec2(0.0035, 0.0018)).r;
		float m = texture(noise_tex, wpos.xz * 0.023 - TIME * vec2(0.002, 0.004)).g;
		float a = edge * smoothstep(0.3, 0.7, n * 0.65 + m * 0.45) * mist * tint.a;
		ALBEDO = col * mix(1.0, 0.45, night);
		ALPHA = clamp(a * 0.9, 0.0, 0.72);
	} else {
		float streak = texture(noise_tex, vec2(UV.x * 0.35 + wpos.x * 0.01, UV.y * 0.25 - TIME * 0.55)).r;
		float foam = smoothstep(0.75, 1.0, UV.y) + smoothstep(0.12, 0.0, UV.y) * 0.6;
		float side = 1.0 - smoothstep(0.35, 0.5, abs(UV.x - 0.5));
		vec3 water = mix(col, vec3(0.78, 0.86, 0.9), clamp(smoothstep(0.45, 0.8, streak) * 0.8 + foam * 0.6, 0.0, 1.0));
		ALBEDO = water * 0.62 * mix(1.0, 0.4, night);
		ALPHA = side * tint.a * (0.7 + 0.3 * streak);
	}
}
