shader_type canvas_item;

// The map's colour grade: a little extra saturation, warm highlights and cool shadows, a soft vignette.
uniform float saturation = 0.9;
uniform float vignette = 0.28;
uniform float strategic = 0.0;

void fragment() {
	vec3 c = texture(TEXTURE, UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, mix(saturation, 1.0, strategic));
	c *= mix(vec3(0.95, 0.98, 1.05), vec3(1.04, 1.0, 0.94), smoothstep(0.1, 0.7, l));
	vec2 d = UV - 0.5;
	c *= 1.0 - vignette * dot(d, d) * 2.4;
	COLOR = vec4(c, 1.0);
}
