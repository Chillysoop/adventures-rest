// Shared by every world shader: the fog of war, the strategic (inked map) view and the night.
uniform sampler2D fog_tex : filter_linear, repeat_disable;
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float world_size = 2048.0;
uniform float night = 0.0;
uniform float strategic = 0.0;
// The painted look (MapView.Painted): banded cel light, rim, richer colour.
uniform float toon = 0.0;
// The season, blended across its turns (summer is the baseline look): snow lying in winter, turned leaves in autumn,
// fresh greens in spring. Set on every material by MapView from Seasons.Look.
uniform float snow = 0.0;
uniform float autumn = 0.0;
uniform float spring = 0.0;
// The weather (MapView.UpdateSky): the wind's strength (0 still, 1 a gale) and how wet the ground is.
uniform float gale_wind = 0.2;
uniform float wet_ground = 0.0;

const vec3 PARCHMENT = vec3(0.89, 0.83, 0.70);
const vec3 INK = vec3(0.30, 0.23, 0.16);

vec3 to_linear(vec3 c) { return c * c * (c * 0.305306 + 0.682171) + c * 0.012522; }
float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

// x: unseen (0 live, ~0.4 described, ~0.7 stale, 1 unknown), y: described by a report.
// The lookup wobbles a few tiles so the edges of what is known wander like a painted map's.
vec2 fog_at(vec2 p) {
	vec2 w = (texture(noise_tex, p * 0.021).rg - 0.5) * 7.0;
	return texture(fog_tex, (p + w) / world_size).rg;
}

// For culling whole objects under the fog in the vertex shader: true when the ground is unknown.
bool hidden_by_fog(vec2 p) { return textureLod(fog_tex, p / world_size, 0.0).r > 0.9; }

float cloud_noise(vec2 p, float t) {
	float a = texture(noise_tex, p * 0.0045 + vec2(t * 0.0021, t * 0.0013)).r;
	float b = texture(noise_tex, p * 0.013 - vec2(t * 0.0034, -t * 0.0018)).g;
	return a * 0.65 + b * 0.35;
}

// Seen ground keeps its colour; ground known only from reports fades to a faded sepia memory;
// unknown ground lies under drifting painted cloud with a pale mist along its edge.
vec3 apply_fog(vec3 col, vec2 p, float t, out float cloud) {
	float unseen = fog_at(p).r;
	float n = cloud_noise(p, t);
	float memory = smoothstep(0.08, 0.4, unseen);
	vec3 faded = mix(vec3(luma(col)) * vec3(1.02, 0.96, 0.86), col, 0.45) * 0.8;
	col = mix(col, faded, memory);
	cloud = smoothstep(0.62, 0.95, unseen + (n - 0.5) * 0.45);
	float swirl = texture(noise_tex, p * 0.03 + vec2(n * 0.4, t * 0.004)).b;
	vec3 sky = mix(vec3(0.12, 0.14, 0.17), vec3(0.40, 0.44, 0.49), smoothstep(0.3, 0.85, n * 0.7 + swirl * 0.3));
	float rim = smoothstep(0.5, 0.78, unseen + (n - 0.5) * 0.3) * (1.0 - smoothstep(0.78, 0.98, unseen + (n - 0.5) * 0.3));
	col = mix(col, sky, cloud);
	return col + vec3(0.55, 0.6, 0.64) * rim * 0.35;
}

const vec3 SNOW = vec3(0.80, 0.85, 0.91);

// How much snow lies on a surface at p that faces up by `up` (world normal y): patchy, thinning on slopes,
// thickening as winter deepens.
float snow_cover(vec2 p, float up) {
	if (snow < 0.002) return 0.0;
	float n = texture(noise_tex, p * 0.035).r * 0.5 + texture(noise_tex, p * 0.16).g * 0.32 + texture(noise_tex, p * 0.7).b * 0.18;
	float amount = snow * smoothstep(0.25, 0.75, up);
	float d = amount * 0.6 - n;
	return max(smoothstep(-0.03, 0.04, d), smoothstep(-0.22, 0.04, d) * 0.4) * smoothstep(0.0, 0.25, snow);
}

// Snow over a colour: blue-white in the hollows of the noise, brightest on the crests.
vec3 lay_snow(vec3 c, vec2 p, float cover) {
	float g = texture(noise_tex, p * 0.5).a;
	return mix(c, SNOW * (0.9 + 0.12 * g) - vec3(0.04, 0.02, 0.0) * (1.0 - g), cover);
}

// How green a colour is (grass, leaves): 0 for bark, stone, thatch and paint.
float greenness(vec3 c) { return clamp((c.g - max(c.r, c.b)) * 10.0, 0.0, 1.0); }

// Green ground and grass toward straw in autumn, fresher in spring, a dull dormant olive under winter.
vec3 season_grass(vec3 c) {
	float g = greenness(c);
	float l = luma(c);
	vec3 straw = vec3(0.70, 0.60, 0.36) * (l / 0.46);
	vec3 fresh = c * vec3(0.93, 1.07, 0.86) + vec3(0.0, 0.02, 0.0);
	vec3 dormant = mix(vec3(0.44, 0.39, 0.30) * (l / 0.42), c, 0.15);
	c = mix(c, straw, autumn * g * 0.55);
	c = mix(c, fresh, spring * g * 0.9);
	return mix(c, dormant, snow * (0.4 + 0.5 * g));
}

// Diffuse for light(): Godot's lambert wrap, or with toon two soft bands with the shadow folded into the dark one.
float cel(float ndl, float atten, float rough) {
	float wrap = max(0.0, (ndl + rough) / ((1.0 + rough) * (1.0 + rough))) * atten;
	float lit = smoothstep(0.0, 0.1, ndl) * smoothstep(0.3, 0.6, atten);
	float band = mix(0.17, 0.44, lit) + 0.08 * clamp(ndl, 0.0, 1.0) * lit;
	return mix(wrap, band, toon);
}

// A warm sunlit rim on silhouettes, Northgard's soft halo.
vec3 rim(vec3 n, vec3 v) {
	return vec3(1.0, 0.9, 0.72) * pow(1.0 - clamp(dot(n, v), 0.0, 1.0), 4.0) * 0.16 * toon * (1.0 - night) * (1.0 - strategic);
}

// ---------- Wind ----------
// One breeze across the whole map in rolling gusts, so a gust is seen sweeping over fields, banners and woods in turn.
const vec2 WIND = vec2(0.8, 0.6);

// 0 in a lull, 1 at a gust's front.
// Harder and quicker as the weather's wind rises (a gale keeps the woods tossing).
float gust(vec2 p, float t) {
	float hard = 0.6 + gale_wind * 2.0;
	return smoothstep(-0.6, 1.1, sin(dot(p, WIND) * 0.07 - t * 1.1 * (0.8 + gale_wind)) + 0.4 * sin(dot(p, vec2(-0.6, 0.8)) * 0.045 + t * 0.37)) * hard;
}

// The breeze in a mesh's own space (its rotation undone), so offsets scale with the model.
vec3 wind_local(mat4 m) {
	vec3 d = transpose(mat3(m)) * vec3(WIND.x, 0.0, WIND.y);
	return d / max(length(d), 1e-5);
}

// Trees: the old gentle lean, stronger as a gust passes.
float tree_sway(vec3 v, vec3 wp, float t) {
	return sin(t * 1.1 + wp.x * 0.21 + wp.z * 0.17) * 0.035 * max(v.y - 0.25, 0.0) * (0.55 + 0.9 * gust(wp.xz, t));
}

// Moves a vertex by the wind, from MeshKit's wind data (MeshKit.WindAt): cloth (uv.x > 0, how free it hangs; flutter axis in
// uv2, half length for a wall banner) ripples, stalks (uv.y > 0, how far up the stalk) bend with the gusts, and smoke puffs (uv.x < 0, its phase; centre in
// uv.y and uv2) rise from a chimney, swell, drift downwind and shrink away on a loop.
vec3 blow(vec3 v, vec2 uv, vec2 uv2, mat4 m, float t) {
	if (uv.x == 0.0 && uv.y == 0.0) return v;
	vec3 wp = (m * vec4(v, 1.0)).xyz;
	float g = gust(wp.xz, t);
	float ph = dot(m[3].xz, vec2(1.7, 1.3));
	if (uv.x > 0.0) {
		float w = uv.x;
		float f = sin(t * (3.0 + 1.6 * g) - w * 10.0 + ph) * (0.16 + 0.2 * g) + sin(t * 5.7 - w * 17.0 + ph * 1.3) * 0.05;
		// A half-length axis: a banner on a wall, which only lifts away from it.
		if (length(uv2) < 0.75) f = abs(f);
		return v + vec3(uv2.x, 0.0, uv2.y) / length(uv2) * f * w;
	}
	vec3 wl = wind_local(m);
	if (uv.x == 0.0) {
		float s = uv.y;
		return v + wl * s * (0.04 + 0.26 * g + 0.05 * sin(t * 2.4 + ph + wp.x * 0.9));
	}
	vec3 c = vec3(uv2.x, uv.y, uv2.y);
	float r = length(v - c);
	float k = fract(t * 0.14 + (-uv.x - 1.0) + fract(ph * 0.131));
	float size = smoothstep(0.0, 0.12, k) * (1.0 - smoothstep(0.6, 1.0, k)) * (0.8 + 2.4 * k);
	float rise = k * r * 16.0;
	float drift = k * k * r * (8.0 + 10.0 * g);
	return c + (v - c) * size + vec3(0.0, rise, 0.0) + wl * drift + vec3(wl.z, 0.0, -wl.x) * sin(t * 0.9 + ph + k * 6.0) * r * 1.2 * k;
}
