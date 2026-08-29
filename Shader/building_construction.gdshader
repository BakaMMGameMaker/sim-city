shader_type spatial;
// 未建造部分不渲染：高于 build_height 的顶点塌缩到当前高度。
// build_height 为 body 局部空间 Y，范围 [-Height/2, Height/2]，由 C# 换算后写入。
uniform float build_height = 0.0;
// 以下 PBR 参数必须与 Material/neon_blue.tres（body 材质）保持一致，
// 保证建造完成切回标准材质时无视觉跳变。
uniform vec4 albedo_color : source_color = vec4(0.5, 0.62, 0.78, 1.0);
uniform float metallic = 0.15;
uniform float roughness = 0.55;
uniform vec3 emission_color : source_color = vec3(0.12, 0.2, 0.32);
uniform float emission_strength = 0.25;

void vertex() {
	VERTEX.y = min(VERTEX.y, build_height);
}

void fragment() {
	ALBEDO = albedo_color.rgb;
	METALLIC = metallic;
	ROUGHNESS = roughness;
	EMISSION = emission_color * emission_strength;
}
