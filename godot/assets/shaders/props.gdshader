shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Buildings, landmarks and people: vertex colours, with vertex alpha as a flag (see MeshKit.Flag).
uniform float hide_in_strategic = 0.0;

varying vec3 wpos;
varying vec3 skin;
varying float up;

void vertex() {
	VERTEX = blow(VERTEX, UV, UV2, MODEL_MATRIX, TIME);
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	skin = INSTANCE_CUSTOM.rgb;
	up = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz).y;
	if (hide_in_strategic > 0.5) VERTEX *= 1.0 - strategic;
	if (hidden_by_fog(wpos.xz)) VERTEX = vec3(0.0);
}

void fragment() {
	vec3 c = COLOR.rgb;
	float a = COLOR.a;
	vec3 emit = vec3(0.0);
	// Winter snow on roofs, ledges and the ground about the town, but not on faces, lamps or fires.
	if (a > 0.95) c = lay_snow(c, wpos.xz, snow_cover(wpos.xz, up * 1.7) * (1.0 - strategic));
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
	// Painted: a little brush grain and richer colour on walls and roofs.
	float grain = texture(noise_tex, wpos.xz * 2.3 + vec2(wpos.y * 1.9)).r;
	c = mix(c, mix(vec3(luma(c)), c, 1.18) * (0.92 + 0.16 * grain), toon);
	emit += rim(NORMAL, VIEW) * c * 2.0;
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

void light() {
	DIFFUSE_LIGHT += LIGHT_COLOR * cel(dot(NORMAL, LIGHT), ATTENUATION, ROUGHNESS) / PI;
}
