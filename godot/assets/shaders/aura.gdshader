shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;

#include "res://assets/shaders/common.gdshaderinc"
#include "res://assets/shaders/rig.gdshaderinc"

// An enchanted weapon's aura: its mesh again, swollen and glowing in the enchantment's colour, moving
// with the hero's rig. Palette alpha picks the style: 1 steady, 2 fire (tongues licking up), 3 lightning
// (strobing), 4 holy (a slow bright breath), 5 frost (a cold shimmer).
uniform vec4 palette[32];

varying vec3 wpos;
varying vec4 glow;
varying float tongue;

void vertex() {
	float b = INSTANCE_CUSTOM.b + 0.5;
	glow = palette[int(mod(b, 32.0))];
	vec3 rest = VERTEX;
	vec3 n0 = NORMAL;
	float fire = glow.a > 1.5 && glow.a < 2.5 ? 1.0 : 0.0;
	VERTEX += n0 * (0.012 + 0.006 * sin(TIME * 4.0 + rest.y * 40.0));
	rig(VERTEX, NORMAL, UV.x, INSTANCE_CUSTOM.a, fract(INSTANCE_CUSTOM.g));
	tongue = sin(TIME * 13.0 + rest.y * 70.0 + rest.x * 50.0) * 0.5 + 0.5;
	// Fire licks upward in world space, whichever way the blade points.
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wpos.y += fire * tongue * 0.05;
	VERTEX = (inverse(MODEL_MATRIX) * vec4(wpos, 1.0)).xyz;
	VERTEX *= 1.0 - strategic;
	if (hidden_by_fog(wpos.xz) || glow.a < 0.5) VERTEX = vec3(0.0);
}

void fragment() {
	float s = glow.a;
	float k = 0.55;
	if (s > 1.5 && s < 2.5) k = 0.45 + 0.5 * tongue;
	else if (s > 2.5 && s < 3.5) k = step(0.55, fract(sin(floor(TIME * 18.0) * 91.7) * 4375.5)) * 0.9 + 0.15;
	else if (s > 3.5 && s < 4.5) k = 0.5 + 0.3 * sin(TIME * 1.7);
	else if (s > 4.5) k = 0.35 + 0.25 * sin(TIME * 6.0 + wpos.y * 30.0);
	float fres = 1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
	ALBEDO = to_linear(glow.rgb) * k * (0.5 + 1.2 * fres) * (1.0 - strategic);
}
