shader_type canvas_item;

uniform bool guard_active = false;
uniform bool parry_active = false;
uniform bool hurt_active = false;

uniform vec3 guard_color: source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 parry_color: source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 hurt_color: source_color = vec3(1.0, 1.0, 1.0);

void fragment() {
	vec4 previous_color = texture(TEXTURE, UV);
	vec4 color;
	
	if(guard_active) {
		color = vec4(guard_color.r, guard_color.g, guard_color.b, previous_color.a);
	} else if(parry_active) {
		color = vec4(parry_color.r, parry_color.g, parry_color.b, previous_color.a);
	} else if(hurt_active) {
		color = vec4(hurt_color.r, hurt_color.g, hurt_color.b, previous_color.a);
	}
	
	vec4 new_color = previous_color;
	if (guard_active || parry_active || hurt_active) {
		new_color = color;
	}
	COLOR = new_color;
}