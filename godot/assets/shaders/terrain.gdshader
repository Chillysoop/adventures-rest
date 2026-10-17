shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

#include "res://assets/shaders/common.gdshaderinc"

// Territories: a jittered grid of cells, one seed each (rg) with the cell's biome (b); state per
// cell: held by the town (r), corrupted by the enemy (g).
uniform sampler2D cell_tex : filter_nearest, repeat_disable;
uniform sampler2D state_tex : filter_nearest, repeat_disable;
// r: forest cover, g: water depth.
uniform sampler2D data_tex : filter_linear, repeat_disable;
uniform float cells = 26.0;
uniform float cell_size = 40.0;
// Colours here are sRGB; the shader converts once at the end.
uniform vec3 biome_a[6];
uniform vec3 biome_b[6];
uniform vec3 held_color = vec3(0.95, 0.75, 0.25);
uniform vec3 enemy_color = vec3(0.75, 0.18, 0.12);
uniform float border_width = 0.35;
uniform float far = 0.0;
uniform vec4 tracks[24];
uniform int track_count = 0;
uniform vec4 rings[12];
uniform vec4 ring_colors[12];
uniform int ring_count = 0;

varying vec3 wpos;
varying vec3 wnrm;

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = NORMAL;
}

vec2 warp(vec2 p) {
	return p + vec2(sin(p.y * 0.045 + 1.7) + 0.6 * sin(p.x * 0.11 + p.y * 0.03),
		sin(p.x * 0.05 + 0.4) + 0.6 * sin(p.y * 0.09 - p.x * 0.02)) * 4.5;
}

vec4 cell_at(ivec2 c) { return texelFetch(cell_tex, clamp(c, ivec2(0), ivec2(int(cells) - 1)), 0); }
vec2 seed_of(ivec2 c) { return (vec2(c) + cell_at(c).rg) * cell_size; }

vec3 biome_col(int b, vec2 p, float n1, float n2) {
	return mix(biome_a[b], biome_b[b], smoothstep(0.3, 0.7, n1)) * (0.9 + 0.2 * n2);
}

float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-4), 0.0, 1.0);
	return length(p - a - ab * t);
}

void fragment() {
	vec2 p = wpos.xz;
	float t = TIME;
	vec4 nz = texture(noise_tex, p * 0.012);
	vec4 nz2 = texture(noise_tex, p * 0.07);
	vec4 nz3 = texture(noise_tex, p * 0.35);

	// Which territory, and how far to its nearest border (Voronoi edge distance in warped space).
	vec2 q = warp(p);
	ivec2 c0 = ivec2(floor(q / cell_size));
	float best = 1e9;
	ivec2 bc = c0;
	vec2 bs = vec2(0.0);
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			ivec2 cc = c0 + ivec2(x, y);
			vec2 s = seed_of(cc);
			float d = dot(q - s, q - s);
			if (d < best) { best = d; bc = cc; bs = s; }
		}
	float edge = 1e9;
	ivec2 nc = bc;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			ivec2 cc = bc + ivec2(x, y);
			if (cc == bc) continue;
			vec2 s = seed_of(cc);
			float e = dot(0.5 * (bs + s) - q, normalize(s - bs));
			if (e < edge) { edge = e; nc = cc; }
		}
	vec4 me = cell_at(bc);
	vec4 nb = cell_at(nc);
	vec4 st = texelFetch(state_tex, bc, 0);
	vec4 nst = texelFetch(state_tex, nc, 0);

	// Ground: each territory's biome, blended softly into its neighbour's at the border.
	vec3 col = biome_col(int(me.b * 8.0 + 0.5), p, nz.r, nz2.g);
	vec3 ncol = biome_col(int(nb.b * 8.0 + 0.5), p, nz.r, nz2.g);
	col = mix(mix(col, ncol, 0.5), col, smoothstep(0.0, 5.0, edge + (nz2.b - 0.5) * 3.0));
	// Painterly grain: short strokes of light and shade at two scales.
	vec4 g1 = texture(noise_tex, vec2(p.x * 1.1, p.y * 0.55));
	vec4 g2 = texture(noise_tex, vec2(p.x * 3.1 + p.y * 0.4, p.y * 1.7));
	col *= 0.88 + 0.16 * nz3.r + 0.16 * (g1.g - 0.5) + 0.12 * (g2.r - 0.5);

	float h = wpos.y;
	float slope = 1.0 - normalize(wnrm).y;
	vec3 rock = mix(vec3(0.50, 0.47, 0.42), vec3(0.62, 0.58, 0.52), nz2.r) * (0.9 + 0.15 * nz3.g);
	col = mix(col, rock, smoothstep(0.22, 0.42, slope + (nz2.g - 0.5) * 0.15));
	col = mix(col, vec3(0.93, 0.94, 0.96), smoothstep(27.0, 33.0, h + (nz2.b - 0.5) * 6.0) * smoothstep(0.5, 0.3, slope));
	col = mix(col, vec3(0.80, 0.73, 0.55), smoothstep(0.9, 0.25, h) * (1.0 - smoothstep(-0.3, -0.9, h)));

	vec4 data = texture(data_tex, p / world_size);
	float forest = data.r;
	col = mix(col, vec3(0.24, 0.33, 0.19), forest * 0.55);
	// From far away the trees are gone and the forest is painted onto the ground as canopy.
	float canopy = forest * far * smoothstep(0.35, 0.65, nz2.r * 0.6 + nz3.b * 0.4 + forest * 0.3);
	col = mix(col, mix(vec3(0.17, 0.27, 0.15), vec3(0.27, 0.40, 0.21), nz3.g), clamp(canopy * 1.4, 0.0, 0.9));

	// Tracks the parties have walked, from their reports.
	for (int i = 0; i < track_count; i++) {
		float d = seg_dist(p, tracks[i].xy, tracks[i].zw) + (nz2.r - 0.5) * 0.8;
		col = mix(col, vec3(0.62, 0.50, 0.34) * (0.9 + 0.2 * nz3.r), (1.0 - smoothstep(0.45, 1.0, d)) * 0.85);
	}

	// Corruption: the enemy's land drains of colour and takes a sickly tint.
	float corr = st.g * smoothstep(-2.0, 6.0, edge) + nst.g * (1.0 - smoothstep(-2.0, 6.0, edge)) * 0.5;
	col = mix(col, mix(vec3(luma(col)), vec3(0.42, 0.33, 0.38), 0.45) * 0.85, corr * 0.85);

	// Borders: held land gets the town's colour along its edge and a wash inside; the wild has faint seams.
	float bw = border_width;
	float line = 1.0 - smoothstep(bw * 0.5, bw, edge);
	bool held = st.r > 0.5, nheld = nst.r > 0.5;
	vec3 emit = vec3(0.0);
	if (held != nheld) {
		col = mix(col, held_color, line * 0.9);
		if (held) col = mix(col, held_color, (1.0 - smoothstep(0.0, 6.0 + bw * 4.0, edge)) * 0.22);
		emit += held_color * line * 0.25;
	} else if (st.g > 0.5 != nst.g > 0.5) {
		col = mix(col, enemy_color, line * 0.85);
	} else {
		col *= 1.0 - line * 0.22 * (held ? 0.2 : 1.0);
	}

	// Live sight: rings around what the town is watching right now (scrying, mirrors, scouts, the Spire).
	for (int i = 0; i < ring_count; i++) {
		float d = abs(length(p - rings[i].xy) - rings[i].z);
		float g = 1.0 - smoothstep(0.0, rings[i].w, d);
		emit += ring_colors[i].rgb * g * (0.8 + 0.2 * sin(t * 2.0 + float(i)));
		col = mix(col, ring_colors[i].rgb, g * 0.5);
	}

	float cloud;
	col = apply_fog(col, p, t, cloud);
	emit *= 1.0 - cloud;

	// The strategic view: the same land as an inked campaign map.
	if (strategic > 0.001) {
		vec3 paper = PARCHMENT * (0.93 + 0.08 * nz2.r) * mix(vec3(1.0), biome_col(int(me.b * 8.0 + 0.5), p, nz.r, 0.5) * 1.6, 0.12);
		float contour = 1.0 - smoothstep(0.0, 0.08, abs(fract(h / 4.0) - 0.5) - 0.42);
		paper = mix(paper, INK, contour * 0.25 * step(0.6, h));
		float hatch = step(0.62, fract((p.x + p.y) * 0.7)) * smoothstep(0.25, 0.55, forest);
		paper = mix(paper, vec3(0.38, 0.45, 0.30), hatch * 0.6);
		paper = mix(paper, vec3(0.62, 0.74, 0.78), smoothstep(0.1, -0.4, h));
		float sl = 1.0 - smoothstep(bw * 0.4, bw * 1.2, edge);
		paper = mix(paper, INK, sl * 0.55);
		if (held != nheld) paper = mix(paper, vec3(0.70, 0.18, 0.12), sl);
		if (held) paper = mix(paper, vec3(0.85, 0.55, 0.35), 0.12);
		float unseen = fog_at(p).r;
		paper = mix(paper, PARCHMENT * 0.82, smoothstep(0.75, 0.95, unseen));
		col = mix(col, paper, strategic);
		emit = mix(emit, paper * 0.95, strategic);
		col *= 1.0 - strategic * 0.8;
	}

	ALBEDO = to_linear(col);
	EMISSION = to_linear(emit);
	ROUGHNESS = 1.0;
}
