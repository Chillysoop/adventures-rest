shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Buildings, landmarks and people: vertex colours, with vertex alpha as a flag (see MeshKit.Flag).
uniform float hide_in_strategic = 0.0;

varying vec3 wpos;
varying vec3 skin;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	skin = INSTANCE_CUSTOM.rgb;
	if (hide_in_strategic > 0.5) VERTEX *= 1.0 - strategic;
	if (hidden_by_fog(wpos.xz)) VERTEX = vec3(0.0);
}

void fragment() {
	vec3 c = COLOR.rgb;
	float a = COLOR.a;
	vec3 emit = vec3(0.0);
	if (abs(a - 0.45) < 0.05) c = skin;
	if (abs(a - 0.6) < 0.05) {
		vec3 lamp = vec3(1.0, 0.72, 0.38);
		c = mix(c, lamp, night);
		emit = lamp * night * 1.8;
	} else if (abs(a - 0.3) < 0.05) {
		emit = c * (1.2 + 0.25 * sin(TIME * 7.0 + wpos.x));
	} else if (abs(a - 0.8) < 0.05) {
		emit = c * (0.9 + 0.35 * sin(TIME * 2.0));
	}
	float cloud;
	c = apply_fog(c, wpos.xz, TIME, cloud);
	emit *= 1.0 - cloud;
	if (strategic > 0.001) {
		vec3 ink = mix(INK, PARCHMENT * 0.85, smoothstep(0.2, 0.8, luma(c)));
		c = mix(c, ink, strategic);
		emit = mix(emit, ink * 0.6, strategic);
	}
	ALBEDO = to_linear(c);
	EMISSION = to_linear(emit);
	ROUGHNESS = 0.9;
}
