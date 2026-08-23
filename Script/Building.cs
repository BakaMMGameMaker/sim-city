using Godot;
using System;

[GlobalClass]
public partial class Building : Node3D
{
	[Export]
	public float Width = 3.2f;

	[Export]
	public float Depth = 3.2f;

	[Export]
	public float Height = 12.0f;

	[Export]
	public float BuildTime = 6.0f;

	[Export]
	public int FoundationWidth = 4;

	[Export]
	public int FoundationHeight = 4;

	[Export]
	public float CellSize = 1.0f;

	[Export]
	public StandardMaterial3D BodyMaterial;

	[Export]
	public StandardMaterial3D EdgeMaterial;

	[Export]
	public float EdgeThickness = 0.07f;

	[Export]
	public StandardMaterial3D FoundationMaterial;

	[Export]
	public StandardMaterial3D PreviewValidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewValidEdgeMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidEdgeMaterial;

	[Export]
	public StandardMaterial3D PreviewValidFoundationMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidFoundationMaterial;

	private MeshInstance3D _bodyInstance;
	private MeshInstance3D _foundationInstance;
	private EdgeComponent _edgeComponent;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	private const float FoundationThickness = 0.06f;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_bodyInstance = GetNode<MeshInstance3D>("Body");
		_edgeComponent = GetNode<EdgeComponent>("EdgeComponent");

		SetupBuilding();
	}

	private void SetupBuilding()
	{
		SetupFoundation();
		SetupBody();
		SetupEdgeComponent();
	}

	/// <summary>
	/// 地面层：覆盖整个 Footprint 区域，体现实际占据空间。
	/// Building 原点为占地矩形的最小角（min corner）。
	/// </summary>
	private void SetupFoundation()
	{
		float worldW = FoundationWidth * CellSize;
		float worldD = FoundationHeight * CellSize;

		_foundationInstance = new MeshInstance3D { Name = "Foundation" };
		var box = new BoxMesh { Size = new Vector3(worldW, FoundationThickness, worldD) };
		_foundationInstance.Mesh = box;
		_foundationInstance.MaterialOverride = FoundationMaterial;
		// 中心落在 footprint 中心，底面贴地
		_foundationInstance.Position = new Vector3(worldW * 0.5f, FoundationThickness * 0.5f, worldD * 0.5f);
		AddChild(_foundationInstance);
	}

	/// <summary>
	/// 建筑体顶着 Footprint 的最小角（原点）：体块占据 [0,Width] x [0,Depth]。
	/// </summary>
	private void SetupBody()
	{
		var box = new BoxMesh { Size = new Vector3(Width, Height, Depth) };
		_bodyInstance.Mesh = box;
		_bodyInstance.MaterialOverride = BodyMaterial;
		_bodyInstance.Position = new Vector3(Width * 0.5f, Height * 0.5f, Depth * 0.5f);
	}

	private void SetupEdgeComponent()
	{
		_edgeComponent.Setup(new EdgeComponent.EdgeSetupConfig
		{
			Width = Width,
			Depth = Depth,
			Height = Height,
			Thickness = EdgeThickness,
			Material = EdgeMaterial,
			VerticalUpdater = (node, state) =>
			{
				node.Scale = new Vector3(node.Scale.X, state.ScaleY, node.Scale.Z);
				node.Position = new Vector3(node.Position.X, state.Top / 2f, node.Position.Z);
			},
			TopUpdater = (node, state) =>
			{
				node.Position = new Vector3(node.Position.X, state.Top, node.Position.Z);
			},
			BottomUpdater = (_, _) => { },
		});
	}

	public void EnterPreview()
	{
		_mode = Mode.Preview;
		_bodyInstance.Scale = Vector3.One;
		_bodyInstance.Position = new Vector3(Width * 0.5f, Height * 0.5f, Depth * 0.5f);
		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(1.0f, Height));
	}

	public void SetValidPreview()
	{
		if (_mode != Mode.Preview) return;

		_bodyInstance.MaterialOverride = PreviewValidBodyMaterial;
		_edgeComponent.SetMaterial(PreviewValidEdgeMaterial);
		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = PreviewValidFoundationMaterial;
	}

	public void SetInvalidPreview()
	{
		if (_mode != Mode.Preview) return;

		_bodyInstance.MaterialOverride = PreviewInvalidBodyMaterial;
		_edgeComponent.SetMaterial(PreviewInvalidEdgeMaterial);
		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = PreviewInvalidFoundationMaterial;
	}

	public void ExitPreview()
	{
		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);
		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = FoundationMaterial;
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);
		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = FoundationMaterial;

		InitBuilding();
		StartConstructionTween();
	}

	private void InitBuilding()
	{
		_bodyInstance.Scale = new Vector3(1, 0, 1);
		_bodyInstance.Position = new Vector3(Width * 0.5f, 0, Depth * 0.5f);
		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(0.0f, 0.0f));
	}

	private void StartConstructionTween()
	{
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenMethod(Callable.From<float>(UpdateConstruction), 0.0f, 1.0f, BuildTime);
	}

	private void UpdateConstruction(float progress)
	{
		float scaleY = progress;
		float top = progress * Height;

		_bodyInstance.Scale = new Vector3(1, scaleY, 1);
		_bodyInstance.Position = new Vector3(Width * 0.5f, top / 2f, Depth * 0.5f);

		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(scaleY, top));
	}

	/// <summary>
	/// 占地矩形（世界坐标），原点为最小角。
	/// </summary>
	public Rect2 GetFootprintRect()
	{
		float worldW = FoundationWidth * CellSize;
		float worldD = FoundationHeight * CellSize;
		return new Rect2(GlobalPosition.X, GlobalPosition.Z, worldW, worldD);
	}

	public Vector2I GetOriginCell()
	{
		return new Vector2I(
			Mathf.RoundToInt(GlobalPosition.X / CellSize),
			Mathf.RoundToInt(GlobalPosition.Z / CellSize)
		);
	}
}
