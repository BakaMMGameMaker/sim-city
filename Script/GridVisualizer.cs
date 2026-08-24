using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class GridVisualizer : Node3D
{
	[Export]
	public int Extent = 20;

	[Export]
	public Color LineColor = new(0.35f, 0.55f, 0.75f, 0.35f);

	[Export]
	public float LineY = 0.03f;

	private float GridSize => GameConfig.Instance != null ? GameConfig.Instance.GridSize : 1.0f;

	public override void _Ready()
	{
		BuildGridMesh();
	}

	private void BuildGridMesh()
	{
		var vertices = new List<Vector3>();
		float half = Extent * GridSize;

		for (int i = -Extent; i <= Extent; i++)
		{
			float v = i * GridSize;
			// 沿 X 的线（固定 Z）
			vertices.Add(new Vector3(-half, LineY, v));
			vertices.Add(new Vector3(half, LineY, v));
			// 沿 Z 的线（固定 X）
			vertices.Add(new Vector3(v, LineY, -half));
			vertices.Add(new Vector3(v, LineY, half));
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();

		var arrMesh = new ArrayMesh();
		arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		var mat = new StandardMaterial3D
		{
			AlbedoColor = LineColor,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

		var mi = new MeshInstance3D
		{
			Name = "GridLines",
			Mesh = arrMesh,
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(mi);
	}
}
