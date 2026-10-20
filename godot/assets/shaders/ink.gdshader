shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Trial ink outline (--style painted): the mesh drawn again a hair larger, back faces only, in dark ink.
// Its width grows with the camera distance so lines stay about the same thickness on screen.
uniform float width = 0.0016;
uniform vec3 ink = vec3(0.12, 0.08, 0.07);
uniform float sway = 0.0;

varying vec3 wpos;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (sway > 0.5) VERTEX.x += sin(TIME * 1.1 + wpos.x * 0.21 + wpos.z * 0.17) * 0.035 * max(VERTEX.y - 0.25, 0.0);
	vec3 cam = INV_VIEW_MATRIX[3].xyz;
	float d = length(cam - wpos);
	vec3 n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 grow = n * width * d;
	VERTEX += (inverse(mat3(MODEL_MATRIX)) * grow);
	VERTEX *= 1.0 - strategic;
	if (hidden_by_fog(wpos.xz)) VERTEX = vec3(0.0);
}

void fragment() {
	float cloud;
	vec3 c = apply_fog(ink, wpos.xz, TIME, cloud);
	ALBEDO = to_linear(mix(c, ink, 0.0));
	if (cloud > 0.5) discard;
}
