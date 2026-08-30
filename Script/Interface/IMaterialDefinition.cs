namespace MySimCity;

/// <summary>材料定义的运行时只读视图。</summary>
public interface IMaterialDefinition
{
	string Id { get; }
	string DisplayName { get; }
}
