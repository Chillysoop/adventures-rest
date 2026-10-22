// The creatures' rig (see Beasts.cs, BeastKit.cs): like the heroes' (rig.gdshaderinc), rigid parts turned in the vertex
// shader, but every joint is the mesh's own, carried per vertex (UV.y and UV2 hold the pivot's y and x, z), so one shader
// moves a spider's eight legs, a dragon's wings, a serpent's coils and a kraken's arms. A creature built from the heroes'
// kit (people, golems) uses the heroes' rig as it is.
//
// INSTANCE_CUSTOM.b = gait + 16 * strike + 256 * size (tenths); INSTANCE_CUSTOM.a = act (0 idle, 1 move, 2 attack) + phase.
// Parts: 10/11 the two sets of legs, 12/13 right and left arm or claw, 14 head and neck, 15 jaw, 16 tail, 17/18 right and
// left wing, 19 tentacle, 20 frond, 21 a mote of a swarm.
//
// Gaits: 0 people, 1 walk, 2 lumber, 3 trot, 4 prowl, 5 skitter, 6 slither, 7 fly, 8 hover, 9 ooze, 10 swim, 11 root, 12 hop, 13 swarm.
// Strikes: 0 weapon, 1 shoot, 2 cast, 3 bite, 4 claw, 5 slam, 6 sting, 7 gore, 8 lash, 9 spit, 10 engulf, 11 talons, 12 touch, 13 trample.

#include "res://assets/shaders/rig.gdshaderinc"

float beast_hash(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

void beast_rig(inout vec3 v, inout vec3 n, float part, vec3 pivot, float anim, float code, float flash, float t) {
	float gait = mod(code + 0.5, 16.0);
	gait = floor(gait);
	float strike = floor(mod(floor(code / 16.0 + 0.001), 16.0));
	float size = max(floor(code / 256.0 + 0.001) / 10.0, 0.2);
	if (gait < 0.5 && part < 9.5) {
		// People and golems: the heroes' rig, with the act already in the heroes' numbering. Their wings, tails and
		// jaws (parts 10 and up) carry on below: the heroes' walk is 1 and their blows 2 and up, as here.
		rig(v, n, part, anim, flash);
		return;
	}
	float kind = floor(anim + 0.0005);
	float u = clamp(anim - kind, 0.0, 1.0);
	float ph = u * 6.2831853;
	bool moving = kind > 0.5 && kind < 1.5;
	bool attacking = kind > 1.5;
	float wind = attacking ? smoothstep(0.0, 0.42, u) : 0.0;
	float hit = attacking ? smoothstep(0.42, 0.52, u) * (1.0 - smoothstep(0.6, 1.0, u)) : 0.0;
	float back = wind * (1.0 - hit);
	float speed = moving ? 1.0 : 0.25;
	float dist = length(v - pivot);
	float seed = beast_hash(floor(pivot * 50.0));

	// The parts --------------------------------------------------------------------------------------------------
	if (part > 9.5 && part < 11.5) {
		// Legs: alternate sets, a wave down a long body for the many-legged, tucked up in flight.
		float off = part > 10.5 ? 3.14159 : 0.0;
		float amp = 0.55;
		if (gait > 4.5 && gait < 5.5) amp = 0.35;
		else if (gait > 2.5 && gait < 4.5) amp = 0.5;
		else if (gait > 1.5 && gait < 2.5) amp = 0.4;
		float a = moving ? amp * sin(ph + off + pivot.z * 14.0 / size) : 0.03 * sin(t + off);
		if (gait > 11.5 && gait < 12.5) a = moving ? -0.9 * max(sin(ph), 0.0) : 0.0;
		if (gait > 6.5 && gait < 7.5) a = -0.9 + 0.1 * sin(t * 3.0);
		if (strike > 12.5 && attacking) a += (part > 10.5 ? 0.0 : 1.1) * back - 0.4 * hit;
		mat3 r = rot_x(-a);
		if (gait > 4.5 && gait < 5.5) r = rot_y(0.35 * a * sign(v.x - 0.0)) * rot_x(-0.4 * a);
		rig_turn(v, n, pivot, r);
	} else if (part > 11.5 && part < 13.5) {
		// Arms and claws: swing as it goes; raise and rake, or both high and slam down.
		float side = part > 12.5 ? 1.0 : -1.0;
		float a = moving ? 0.45 * sin(ph + (side > 0.0 ? 3.14159 : 0.0)) : 0.05 * sin(t + side);
		float spread = 0.0;
		if (attacking) {
			if (strike > 4.5 && strike < 5.5 || strike > 9.5 && strike < 10.5) a = mix(0.0, 2.6, back) - 1.2 * hit;
			else if (strike > 1.5 && strike < 2.5) a = 2.2 * wind;
			else if (side < 0.0 || strike > 3.5 && strike < 4.5) { a = mix(0.0, 2.0, back) - 0.6 * hit; spread = 0.6 * hit; }
		}
		rig_turn(v, n, pivot, rot_y(-side * spread) * rot_x(-a));
	} else if (part > 13.5 && part < 14.5) {
		// Head and neck: a look round, a bob in stride; drawn back, then the lunge.
		float nod = 0.05 * sin(t * 0.7 + seed * 6.0) + (moving ? 0.08 * sin(ph * 2.0) : 0.0);
		float turn = moving ? 0.0 : 0.25 * sin(t * 0.37 + seed * 9.0);
		if (attacking) {
			if (strike > 2.5 && strike < 3.5 || strike > 6.5 && strike < 7.5) nod = -0.45 * back + 0.55 * hit;
			else if (strike > 8.5 && strike < 9.5) nod = -0.7 * back + 0.3 * hit;
			else nod = 0.2 * hit;
		}
		rig_turn(v, n, pivot, rot_y(turn) * rot_x(nod));
		if (attacking && (strike > 2.5 && strike < 3.5 || strike > 6.5 && strike < 7.5)) v.z += 0.06 * size * hit;
	} else if (part > 14.5 && part < 15.5) {
		// The jaw: open wide on the wind-up, snapped shut on the bite.
		float a = attacking ? 0.7 * back : 0.08 + 0.06 * sin(t * 1.3 + seed * 5.0);
		rig_turn(v, n, pivot, rot_x(a));
	} else if (part > 15.5 && part < 16.5) {
		// The tail: a lash that travels to the tip; a sting arcs over the back and drives forward.
		float a = (moving ? 0.35 : 0.15) * sin(t * (moving ? 6.0 : 1.5) - dist * 6.0 / size) * clamp(dist * 3.0 / size, 0.2, 1.5);
		float up = 0.0;
		if (attacking && strike > 5.5 && strike < 6.5) up = -1.6 * back + 0.9 * hit;
		else if (attacking && strike > 7.5 && strike < 8.5) a += 1.2 * hit - 0.5 * back;
		rig_turn(v, n, pivot, rot_x(up) * rot_y(a));
	} else if (part > 16.5 && part < 18.5) {
		// Wings: beating in flight, folded on the ground, mantled wide to strike.
		float side = part > 17.5 ? 1.0 : -1.0;
		bool flying = gait > 6.5 && gait < 8.5;
		float a = -0.55 + 0.05 * sin(t);
		if (flying || moving) a = 0.75 * sin(t * (flying ? 7.0 : 9.0) + seed) + 0.15;
		if (attacking) a = 0.9 * back - 0.4 * hit + 0.2;
		rig_turn(v, n, pivot, rot_z(side * a));
	} else if (part > 18.5 && part < 19.5) {
		// Tentacles: curling waves along their length; flung out to lash.
		float w = sin(t * 2.0 + seed * 6.28 - dist * 9.0 / size);
		float a = 0.25 * w * clamp(dist * 4.0 / size, 0.0, 1.5);
		float b = 0.2 * cos(t * 1.7 + seed * 4.0 - dist * 7.0 / size) * clamp(dist * 4.0 / size, 0.0, 1.5);
		if (attacking) a += -1.0 * back + 1.4 * hit;
		rig_turn(v, n, pivot, rot_x(a) * rot_z(b));
	} else if (part > 19.5 && part < 20.5) {
		// Fronds, branches, petals: sway, and whip on the strike.
		float a = 0.08 * sin(t * 1.3 + seed * 6.0) * (1.0 + dist / size);
		if (attacking) a += -0.6 * back + 0.9 * hit;
		rig_turn(v, n, pivot, rot_x(a) * rot_z(0.05 * sin(t * 0.9 + seed * 3.0)));
	} else if (part > 20.5 && part < 21.5) {
		// A mote of a swarm: darting about its own spot.
		vec3 j = vec3(sin(t * 3.1 + seed * 17.0), 0.6 * sin(t * 4.3 + seed * 11.0), cos(t * 2.7 + seed * 13.0)) * 0.04 * size;
		v += j * (moving ? 1.5 : 1.0);
	}

	// The whole body ---------------------------------------------------------------------------------------------
	if (gait > 5.5 && gait < 6.5 || gait > 9.5 && gait < 10.5) {
		// Slithering and swimming: a wave down the body.
		float k = 10.0 / size;
		float amp = 0.04 * size * (moving ? 1.0 : 0.35);
		v.x += amp * sin(v.z * k - t * (moving ? 7.0 : 2.0)) * clamp(1.0 - v.z / (0.6 * size), 0.2, 1.4);
	}
	if (gait > 8.5 && gait < 9.5) {
		// Oozing: a slow squash and swell, heaving forward as it goes.
		float s = sin(t * (moving ? 5.0 : 1.8) + seed);
		v.y *= 1.0 + 0.08 * s + (attacking ? 0.25 * back - 0.2 * hit : 0.0);
		v.xz *= 1.0 - 0.04 * s + (attacking ? 0.2 * hit : 0.0);
	}
	if (gait > 7.5 && gait < 8.5) v.y += 0.025 * size * sin(t * 1.6 + seed * 3.0);
	if (gait > 6.5 && gait < 7.5) v.y += 0.02 * size * sin(t * 7.0);
	if (gait > 10.5 && gait < 11.5) v.x += 0.03 * sin(t * 1.1 + seed * 4.0) * v.y;
	if (gait > 11.5 && gait < 12.5 && moving) v.y += 0.09 * size * max(sin(ph), 0.0);
	if (gait > 0.5 && gait < 5.5 && moving) v.y += 0.012 * size * abs(cos(ph));
	if (gait > 1.5 && gait < 2.5) {
		// Lumbering: a heavy roll from side to side.
		rig_turn(v, n, vec3(0.0), rot_z(0.06 * (moving ? sin(ph) : 0.3 * sin(t * 0.8))));
	}
	// The lunge of the whole body into a bite, a gore or a trample; the rock back when struck.
	if (attacking && (strike > 2.5 && strike < 3.5 || strike > 6.5 && strike < 7.5 || strike > 12.5)) v.z += 0.05 * size * (hit - 0.4 * back);
	if (attacking && strike > 10.5 && strike < 11.5) v.y -= 0.1 * size * hit;
	v.z -= 0.04 * size * flash;
}
