shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

#include "res://assets/shaders/common.gdshaderinc"
#include "res://assets/shaders/beastrig.gdshaderinc"

// Ink outline: the mesh drawn again a hair larger, back faces only, in dark ink.
// Its width grows with the camera distance so lines stay about the same thickness on screen.
uniform float width = 0.0016;
uniform vec3 ink = vec3(0.12, 0.08, 0.07);
uniform float sway = 0.0;
uniform float far_cut = 160.0;
// Heroes: follow their rig (rig.gdshaderinc) so the line moves with the arm.
uniform float rigged = 0.0;
// Creatures: follow theirs (beastrig.gdshaderinc).
uniform float beast = 0.0;

varying vec3 wpos;
// Above 0 where the face looks toward the camera: a mesh with flipped winding, which gets no ink.
varying float facing;

void vertex() {
	// The same wind as the mesh it outlines (blow(), tree_sway() in common.gdshaderinc).
	if (beast > 0.5) beast_rig(VERTEX, NORMAL, UV.x, vec3(UV2.x, UV.y, UV2.y), INSTANCE_CUSTOM.a, INSTANCE_CUSTOM.b, fract(INSTANCE_CUSTOM.g), TIME);
	else if (rigged > 0.5) rig(VERTEX, NORMAL, UV.x, INSTANCE_CUSTOM.a, fract(INSTANCE_CUSTOM.g));
	else if (sway > 0.5 && UV == vec2(0.0)) VERTEX.x += tree_sway(VERTEX, (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz, TIME);
	else VERTEX = blow(VERTEX, UV, UV2, MODEL_MATRIX, TIME);
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 cam = INV_VIEW_MATRIX[3].xyz;
	float d = length(cam - wpos);
	vec3 n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	facing = dot(n, normalize(cam - wpos));
	vec3 grow = n * width * d;
	VERTEX += (inverse(mat3(MODEL_MATRIX)) * grow);
	VERTEX *= 1.0 - strategic;
	// Far out the lines would be sub-pixel: draw nothing.
	if (hidden_by_fog(wpos.xz) || d > far_cut) VERTEX = vec3(0.0);
}

void fragment() {
	if (facing > 0.05) discard;
	float cloud;
	vec3 c = apply_fog(ink, wpos.xz, TIME, cloud);
	ALBEDO = to_linear(c);
	if (cloud > 0.5) discard;
}
