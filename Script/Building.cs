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
	public int FoundationDepth = 4;

	[Export]
	public float FoundationThickness = 0.06f;

	[Export]
	public float GridSize = 1.0f;

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

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_foundationInstance = GetNode<MeshInstance3D>("Foundation");
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

	private void SetupFoundation()
	{
		float worldW = FoundationWidth * GridSize;
		float worldD = FoundationDepth * GridSize;

		var box = new BoxMesh { Size = new Vector3(worldW, FoundationThickness, worldD) };
		_foundationInstance.Mesh = box;
		_foundationInstance.MaterialOverride = FoundationMaterial;
		_foundationInstance.Position = new Vector3(0, FoundationThickness * 0.5f, 0);
	}

	private void SetupBody()
	{
		var box = new BoxMesh { Size = new Vector3(Width, Height, Depth) };
		_bodyInstance.Mesh = box;
		_bodyInstance.MaterialOverride = BodyMaterial;
		_bodyInstance.Position = new Vector3(0, Height * 0.5f, 0);
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
		_bodyInstance.Position = new Vector3(0, Height * 0.5f, 0);
		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(1.0f, Height));
	}

	public void SetPreviewValid(bool valid)
	{
		if (_mode != Mode.Preview) return;

		var foundationMat = valid ? PreviewValidFoundationMaterial : PreviewInvalidFoundationMaterial;
		var bodyMat = valid ? PreviewValidBodyMaterial : PreviewInvalidBodyMaterial;
		var edgeMat = valid ? PreviewValidEdgeMaterial : PreviewInvalidEdgeMaterial;

		_foundationInstance.MaterialOverride = foundationMat;
		_bodyInstance.MaterialOverride = bodyMat;
		_edgeComponent.SetMaterial(edgeMat);
	}

	public void ExitPreview()
	{
		_foundationInstance.MaterialOverride = FoundationMaterial;
		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		_foundationInstance.MaterialOverride = FoundationMaterial;
		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);

		InitConstruction();
		StartConstructionTween();
	}

	private void InitConstruction()
	{
		_bodyInstance.Scale = new Vector3(1, 0, 1);
		_bodyInstance.Position = new Vector3(0, 0, 0);
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
		_bodyInstance.Position = new Vector3(0, top / 2f, 0);

		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(scaleY, top));
	}

	public Rect2 GetFootprintRect()
	{
		float worldW = FoundationWidth * GridSize;
		float worldD = FoundationDepth * GridSize;
		float halfW = worldW * 0.5f;
		float halfD = worldD * 0.5f;
		return new Rect2(GlobalPosition.X - halfW, GlobalPosition.Z - halfD, worldW, worldD);
	}

	public Vector2I GetOriginCell()
	{
		return new Vector2I(
			Mathf.RoundToInt(GlobalPosition.X / GridSize),
			Mathf.RoundToInt(GlobalPosition.Z / GridSize)
		);
	}
}
