shader_type spatial;
render_mode diffuse_lambert_wrap;

#include "res://assets/shaders/common.gdshaderinc"
#include "res://assets/shaders/beastrig.gdshaderinc"

// Creatures (see Beasts.cs): every kind its own mesh with its colours baked in, in the heroes' chunky style. The instance
// carries the host's colour (packed 8-bit rgb, for the cloth and war paint marked Trim), a chief's glow and a hit flash,
// the gait, strike and size, and the act (beastrig.gdshaderinc).

varying vec3 wpos;
varying vec3 lpos;
varying vec3 skin;
varying vec3 trim;
varying vec4 wglow;
varying vec4 aglow;
varying float sparkle;
varying float flash;

void vertex() {
	float packed = INSTANCE_CUSTOM.r;
	trim = vec3(floor(packed / 65536.0), mod(floor(packed / 256.0), 256.0), mod(packed, 256.0)) / 255.0;
	skin = vec3(1.0);
	wglow = vec4(0.0);
	aglow = vec4(0.0);
	sparkle = floor(INSTANCE_CUSTOM.g + 0.001);
	flash = INSTANCE_CUSTOM.g - sparkle;
	lpos = VERTEX;
	beast_rig(VERTEX, NORMAL, UV.x, vec3(UV2.x, UV.y, UV2.y), INSTANCE_CUSTOM.a, INSTANCE_CUSTOM.b, flash, TIME);
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX *= 1.0 - strategic;
	if (hidden_by_fog(wpos.xz)) VERTEX = vec3(0.0);
}

float hash3(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }

void fragment() {
	vec3 c = COLOR.rgb;
	float a = COLOR.a;
	vec3 emit = vec3(0.0);
	float rough = 0.9;
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	if (abs(a - 0.45) < 0.04) c = skin;
	else if (abs(a - 0.15) < 0.04) {
		c = trim * c;
		// An enchanted cloak shimmers in its colour.
		if (aglow.a > 0.5) emit += aglow.rgb * (0.25 + 0.2 * sin(TIME * 3.0 + lpos.y * 30.0));
	}
	else if (abs(a - 0.7) < 0.04 || abs(a - 0.92) < 0.04) {
		// Polished metal: a bright facet that catches the sun, and now and then a glint that twinkles.
		rough = 0.3;
		float facet = hash3(floor(wn * 6.0) + floor(lpos * 40.0));
		float tw = step(0.985 - sparkle * 0.012, hash3(floor(wn * 6.0) + floor(TIME * 5.0 + facet * 7.0)));
		emit += c * tw * (1.6 + sparkle * 0.8) * (1.0 - night * 0.5);
		emit += c * (0.12 + sparkle * 0.06) * (0.6 + 0.4 * max(wn.y, 0.0));
		// Enchanted armour glows along its plates in its colour.
		if (aglow.a > 0.5 && a < 0.8) emit += aglow.rgb * (0.35 + 0.2 * sin(TIME * 2.0 + wpos.x));
		if (a > 0.8) {
			if (sparkle > 0.5) emit += c * 1.6 * (0.85 + 0.25 * sin(TIME * 3.0));
			if (wglow.a > 0.5) {
				float fl = wglow.a > 1.5 && wglow.a < 2.5 ? 0.75 + 0.35 * sin(TIME * 23.0 + lpos.y * 60.0) : 0.85 + 0.25 * sin(TIME * 3.0);
				c = mix(c, wglow.rgb, 0.7);
				emit += wglow.rgb * 2.2 * fl;
			}
		}
	} else if (abs(a - 0.3) < 0.04) {
		emit = c * (1.2 + 0.25 * sin(TIME * 7.0 + wpos.x));
	} else if (abs(a - 0.8) < 0.04) {
		// A focus, an orb, a holy symbol: lit from inside.
		emit = c * (1.1 + 0.4 * sin(TIME * 2.5 + wpos.z));
	}
	// A little light of their own, so a hero never sinks into the night's shadows.
	emit += c * (0.05 + 0.22 * night) * (1.0 - strategic);
	float grain = texture(noise_tex, wpos.xz * 2.3 + vec2(wpos.y * 1.9)).r;
	c = mix(c, mix(vec3(luma(c)), c, 1.25) * (0.94 + 0.12 * grain), toon);
	// A faint warm rim, less than a hero's, so a creature reads against the ground.
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	emit += fres * vec3(1.0, 0.9, 0.75) * (0.25 + 0.2 * night) * (1.0 - strategic);
	c = mix(c, vec3(1.0, 0.85, 0.8), flash * 0.45);
	emit += flash * vec3(0.55, 0.32, 0.25);
	float cloud;
	c = apply_fog(c, wpos.xz, TIME, cloud);
	emit *= 1.0 - cloud;
	ALBEDO = to_linear(c);
	EMISSION = to_linear(emit);
	ROUGHNESS = rough;
}

void light() {
	DIFFUSE_LIGHT += LIGHT_COLOR * cel(dot(NORMAL, LIGHT), ATTENUATION, 0.9) / PI;
	// Metal (low roughness) throws back a hard highlight, cel-banded like the rest.
	if (ROUGHNESS < 0.5) {
		float s = pow(max(dot(reflect(-LIGHT, NORMAL), VIEW), 0.0), 18.0);
		SPECULAR_LIGHT += LIGHT_COLOR * smoothstep(0.35, 0.45, s) * ATTENUATION * 1.4;
	}
}
