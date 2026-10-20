shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Combat effects (Fx.cs): camera-facing quads, each drawn as a shape from INSTANCE_CUSTOM.r:
// 0 a soft orb, 1 a four-pointed spark, 2 a ring, 3 a streak along its local x. Instance colour
// alpha fades it out. All additive, so they bloom in the painted look's glow.

varying vec4 tint;
varying float shape;

void vertex() {
	tint = COLOR;
	shape = INSTANCE_CUSTOM.r;
	vec3 centre = MODEL_MATRIX[3].xyz;
	float size = length(MODEL_MATRIX[0].xyz);
	float stretch = length(MODEL_MATRIX[1].xyz) / max(size, 0.0001);
	vec3 right = INV_VIEW_MATRIX[0].xyz, up = INV_VIEW_MATRIX[1].xyz;
	vec3 along = right;
	if (shape > 2.5) {
		// A streak lies along its direction of travel (the instance's x axis), turned to face the camera.
		along = normalize(MODEL_MATRIX[0].xyz);
		vec3 toCam = normalize(INV_VIEW_MATRIX[3].xyz - centre);
		up = normalize(cross(along, toCam));
		stretch = 1.0;
	}
	vec3 w = centre + along * VERTEX.x * size * (shape > 2.5 ? 1.0 : 1.0) + up * VERTEX.y * size * (shape > 2.5 ? INSTANCE_CUSTOM.g : stretch);
	if (hidden_by_fog(w.xz)) w = vec3(0.0);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w * (1.0 - strategic), 1.0);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a;
	if (shape < 0.5) a = pow(max(0.0, 1.0 - r), 2.0);
	else if (shape < 1.5) a = max(0.0, 1.0 - r) * (pow(max(0.0, 1.0 - abs(p.x) * 6.0), 2.0) + pow(max(0.0, 1.0 - abs(p.y) * 6.0), 2.0) + 0.6 * pow(max(0.0, 1.0 - r), 3.0));
	else if (shape < 2.5) a = smoothstep(0.62, 0.8, r) * (1.0 - smoothstep(0.8, 1.0, r)) * 2.0;
	else a = pow(max(0.0, 1.0 - abs(p.y)), 2.0) * smoothstep(1.0, 0.2, abs(p.x));
	ALBEDO = to_linear(tint.rgb) * a * tint.a * 2.2;
}
