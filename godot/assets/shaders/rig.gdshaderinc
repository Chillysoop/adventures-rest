// The heroes' rig: rigid parts (UV.x) swung about fixed joints by an animation the client picks per
// instance (INSTANCE_CUSTOM.a = kind + phase of the cycle, see Heroes.Act). No skeleton, no bones
// texture: a few rotations in the vertex shader, so a hundred heroes cost what a hundred statues do.
// Parts: 0 torso, 1 right arm, 2 left arm, 3 right leg, 4 left leg, 5 head, 6 cape, 7 right-hand item, 8 left-hand item.

const vec3 RIG_HIP = vec3(0.0, 0.17, 0.0);
const vec3 RIG_LEG_R = vec3(-0.045, 0.17, 0.0);
const vec3 RIG_LEG_L = vec3(0.045, 0.17, 0.0);
const vec3 RIG_ARM_R = vec3(-0.118, 0.335, 0.0);
const vec3 RIG_ARM_L = vec3(0.118, 0.335, 0.0);
const vec3 RIG_HAND_R = vec3(-0.128, 0.19, 0.02);
const vec3 RIG_HAND_L = vec3(0.128, 0.19, 0.02);
const vec3 RIG_NECK = vec3(0.0, 0.37, 0.0);
const vec3 RIG_CAPE = vec3(0.0, 0.355, -0.07);

mat3 rot_x(float a) { float c = cos(a), s = sin(a); return mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, c, s), vec3(0.0, -s, c)); }
mat3 rot_y(float a) { float c = cos(a), s = sin(a); return mat3(vec3(c, 0.0, -s), vec3(0.0, 1.0, 0.0), vec3(s, 0.0, c)); }
mat3 rot_z(float a) { float c = cos(a), s = sin(a); return mat3(vec3(c, s, 0.0), vec3(-s, c, 0.0), vec3(0.0, 0.0, 1.0)); }

// Raises a limb hanging down toward the front (+Z) by a radians.
mat3 raise(float a) { return rot_x(-a); }

void rig_turn(inout vec3 v, inout vec3 n, vec3 pivot, mat3 r) {
	v = pivot + r * (v - pivot);
	n = r * n;
}

// flash (0..1) is a hit landing: the hero rocks back.
void rig(inout vec3 v, inout vec3 n, float part, float anim, float flash) {
	if (part < 0.5 && anim < 0.001 && flash < 0.001) return;
	float kind = floor(anim + 0.0005);
	float u = clamp(anim - kind, 0.0, 1.0);
	float ph = u * 6.2831853;
	float arm_r = 0.12, arm_l = 0.12, out_r = 0.08, out_l = 0.08, wrist_r = 0.0, wrist_l = 0.0;
	float leg_r = 0.0, leg_l = 0.0, twist = 0.0, lean = 0.0, nod = 0.0, cape = 0.12, lift = 0.0;
	if (kind < 0.5) {
		// Idle: breathing, weight shifting.
		arm_r += 0.05 * sin(ph);
		arm_l += 0.05 * sin(ph + 0.6);
		nod = 0.04 * sin(ph + 1.0);
		cape += 0.04 * sin(ph * 2.0);
		lift = 0.003 * sin(ph);
	} else if (kind < 1.5) {
		// Walk: a heroic stride.
		float s = sin(ph);
		leg_r = 0.6 * s;
		leg_l = -0.6 * s;
		arm_r = 0.18 - 0.45 * s;
		arm_l = 0.18 + 0.45 * s;
		lean = 0.08;
		cape = 0.3 + 0.12 * abs(s);
		lift = 0.012 * abs(cos(ph));
	} else if (kind < 3.5) {
		// A swing: wind up overhead, chop down hard, recover. Two-handers bring the left arm along.
		float wind = smoothstep(0.0, 0.42, u), strike = smoothstep(0.42, 0.56, u), rec = smoothstep(0.62, 1.0, u);
		float a = mix(mix(0.25, 2.75, wind), 0.35, strike);
		arm_r = mix(a, 0.25, rec);
		wrist_r = mix(mix(0.0, 0.5, wind), -0.4, strike) * (1.0 - rec);
		twist = mix(mix(0.0, 0.45, wind), -0.4, strike) * (1.0 - rec);
		lean = (mix(-0.12 * wind, 0.3, strike)) * (1.0 - rec);
		leg_r = 0.35 * strike * (1.0 - rec);
		leg_l = -0.25 * strike * (1.0 - rec);
		out_r = 0.08 + 0.15 * wind * (1.0 - strike);
		if (kind > 2.5) {
			arm_l = arm_r * 0.92;
			out_l = -0.25;
		} else arm_l = 0.4 + 0.5 * wind * (1.0 - strike);
		cape = 0.2 + 0.4 * strike * (1.0 - rec);
	} else if (kind < 4.5) {
		// Cast: arms up, the staff held high, a pulse at the top.
		float up = smoothstep(0.0, 0.35, u) * (1.0 - smoothstep(0.78, 1.0, u));
		float push = smoothstep(0.5, 0.58, u) * (1.0 - smoothstep(0.58, 0.8, u));
		arm_r = 0.2 + 1.9 * up + 0.4 * push;
		wrist_r = -(1.6 * up + 0.3 * push);
		arm_l = 0.2 + 1.3 * up + 0.5 * push;
		out_r = 0.08 + 0.4 * up;
		out_l = 0.08 + 0.5 * up;
		lean = -0.12 * up + 0.12 * push;
		nod = -0.2 * up;
		cape = 0.15 + 0.35 * up;
		lift = 0.02 * up;
	} else if (kind < 5.5) {
		// Shoot: bow arm out, draw, loose.
		float draw = smoothstep(0.05, 0.6, u), loose = smoothstep(0.62, 0.68, u);
		arm_l = 1.5;
		wrist_l = -1.5;
		out_l = 0.15;
		arm_r = mix(1.35, 1.1, draw * (1.0 - loose));
		out_r = -0.55 * draw * (1.0 - loose) - 0.1;
		twist = -0.5;
		nod = 0.0;
		lean = 0.05;
	} else if (kind < 6.5) {
		// Thrust: a lunge with the point.
		float go = smoothstep(0.35, 0.5, u) * (1.0 - smoothstep(0.62, 1.0, u));
		float back = smoothstep(0.0, 0.35, u) * (1.0 - go);
		arm_r = 0.4 + 1.1 * go - 0.2 * back;
		wrist_r = -0.9 * go;
		arm_l = 0.6 + 0.6 * back;
		out_l = 0.3;
		twist = 0.3 * back - 0.35 * go;
		lean = 0.3 * go - 0.08 * back;
		leg_r = 0.55 * go;
		leg_l = -0.3 * go;
	} else {
		// Cheer: weapon thrust skyward.
		float b = 0.5 + 0.5 * sin(ph * 2.0);
		arm_r = 2.9 + 0.15 * b;
		wrist_r = -0.8;
		arm_l = 0.4 + 0.2 * b;
		nod = -0.25;
		lift = 0.03 * b;
	}
	lean -= 0.35 * flash;
	if (part > 0.5 && part < 1.5 || part > 6.5 && part < 7.5) {
		if (part > 6.5) rig_turn(v, n, RIG_HAND_R, raise(wrist_r));
		rig_turn(v, n, RIG_ARM_R, rot_z(-out_r) * raise(arm_r));
	} else if (part > 1.5 && part < 2.5 || part > 7.5) {
		if (part > 7.5) rig_turn(v, n, RIG_HAND_L, raise(wrist_l));
		rig_turn(v, n, RIG_ARM_L, rot_z(out_l) * raise(arm_l));
	} else if (part > 2.5 && part < 3.5) rig_turn(v, n, RIG_LEG_R, raise(leg_r));
	else if (part > 3.5 && part < 4.5) rig_turn(v, n, RIG_LEG_L, raise(leg_l));
	else if (part > 4.5 && part < 5.5) rig_turn(v, n, RIG_NECK, rot_x(nod));
	else if (part > 5.5 && part < 6.5) rig_turn(v, n, RIG_CAPE, rot_x(cape));
	// Everything above the hips twists and leans with the blow.
	if (part < 2.5 || part > 4.5) rig_turn(v, n, RIG_HIP, rot_y(twist) * rot_x(lean));
	v.y += lift;
}
