shader_type canvas_item;

const int MAX_REVEAL_AREAS = 32;

uniform vec4 fog_color : source_color = vec4(0.025, 0.03, 0.04, 0.97);
uniform vec2 map_size = vec2(404.0, 745.0);
uniform float edge_variation_pixels : hint_range(0.0, 16.0, 0.5) = 4.0;
uniform int reveal_count = 0;
uniform vec2 reveal_centers[32];
uniform float reveal_radii[32];
uniform float reveal_feathers[32];

void fragment()
{
	vec2 pixel_position = UV * map_size;
	float revealed = 0.0;

	for (int index = 0; index < MAX_REVEAL_AREAS; index++)
	{
		if (index >= reveal_count)
			break;

		vec2 center_pixels = reveal_centers[index] * map_size;
		float radius = reveal_radii[index];
		float feather = max(1.0, reveal_feathers[index]);
		float edge_noise =
			(sin(pixel_position.x * 0.071 + float(index))
			+ sin(pixel_position.y * 0.113 - float(index)))
			* edge_variation_pixels * 0.5;
		float distance_from_center =
			distance(pixel_position, center_pixels) + edge_noise;
		float opening = 1.0 - smoothstep(
			max(0.0, radius - feather),
			radius,
			distance_from_center);

		revealed = max(revealed, opening);
	}

	COLOR = vec4(fog_color.rgb, fog_color.a * (1.0 - revealed));
}
