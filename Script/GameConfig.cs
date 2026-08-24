using Godot;

/// <summary>
/// 全局唯一配置（Autoload）。GridSize 等共享属性只在这里维护。
/// </summary>
[GlobalClass]
public partial class GameConfig : Node
{
	public static GameConfig Instance { get; private set; }

	[Export]
	public float GridSize { get; set; } = 1.0f;

	public override void _Ready()
	{
		Instance = this;
	}
}
