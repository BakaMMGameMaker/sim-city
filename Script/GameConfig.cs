using Godot;

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
