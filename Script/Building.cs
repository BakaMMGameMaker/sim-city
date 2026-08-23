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
	public Vector2 FootprintSize = new Vector2(4.0f, 4.0f);

	[Export]
	public StandardMaterial3D BodyMaterial;

	[Export]
	public StandardMaterial3D EdgeMaterial;

	[Export]
	public float EdgeThickness = 0.07f;

	[Export]
	public StandardMaterial3D PreviewValidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewValidEdgeMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidEdgeMaterial;

	private MeshInstance3D _bodyInstance;
	private EdgeComponent _edgeComponent;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_bodyInstance = GetNode<MeshInstance3D>("Body");
		_edgeComponent = GetNode<EdgeComponent>("EdgeComponent");

		SetupBuilding();
	}

	private void SetupBuilding()
	{
		SetupBody();
		SetupEdgeComponent();
	}

	private void SetupBody()
	{
		var box = new BoxMesh { Size = new Vector3(Width, Height, Depth) };
		_bodyInstance.Mesh = box;
		_bodyInstance.MaterialOverride = BodyMaterial;
	}

	private void SetupEdgeComponent()
	{
		_edgeComponent.Setup(EdgeMaterial, EdgeThickness);
		RegisterEdges();
	}

	private void RegisterEdges()
	{
		float halfX = Width * 0.5f;
		float halfZ = Depth * 0.5f;

		RegisterVerticalEdges(halfX, halfZ, EdgeThickness);
		RegisterTopEdges(halfX, halfZ, EdgeThickness);
		RegisterBottomEdges(halfX, halfZ, EdgeThickness);
	}

	private void RegisterVerticalEdges(float halfX, float halfZ, float t)
	{
		Action<MeshInstance3D, EdgeComponent.BuildingConstructionState> updater = (node, state) =>
		{
			node.Scale = new Vector3(node.Scale.X, state.ScaleY, node.Scale.Z);
			node.Position = new Vector3(node.Position.X, state.Top / 2f, node.Position.Z);
		};

		foreach (float x in new[] { -halfX, halfX })
		{
			foreach (float z in new[] { -halfZ, halfZ })
			{
				_edgeComponent.RegisterEdge(new Vector3(t, Height, t), new Vector3(x, 0, z), updater);
			}
		}
	}

	private void RegisterTopEdges(float halfX, float halfZ, float t)
	{
		Action<MeshInstance3D, EdgeComponent.BuildingConstructionState> updater = (node, state) =>
		{
			node.Position = new Vector3(node.Position.X, state.Top, node.Position.Z);
		};

		foreach (float z in new[] { -halfZ, halfZ })
		{
			_edgeComponent.RegisterEdge(new Vector3(Width, t, t), new Vector3(0, 0, z), updater);
		}
		foreach (float x in new[] { -halfX, halfX })
		{
			_edgeComponent.RegisterEdge(new Vector3(t, t, Depth), new Vector3(x, 0, 0), updater);
		}
	}

	private void RegisterBottomEdges(float halfX, float halfZ, float t)
	{
		Action<MeshInstance3D, EdgeComponent.BuildingConstructionState> updater = (_, _) => { };

		foreach (float z in new[] { -halfZ, halfZ })
		{
			_edgeComponent.RegisterEdge(new Vector3(Width, t, t), new Vector3(0, 0, z), updater);
		}
		foreach (float x in new[] { -halfX, halfX })
		{
			_edgeComponent.RegisterEdge(new Vector3(t, t, Depth), new Vector3(x, 0, 0), updater);
		}
	}

	public void EnterPreview()
	{
		_mode = Mode.Preview;
		_bodyInstance.Scale = Vector3.One;
		_bodyInstance.Position = new Vector3(_bodyInstance.Position.X, Height / 2f, _bodyInstance.Position.Z);
		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(1.0f, Height));
	}

	public void SetValidPreview()
	{
		if (_mode != Mode.Preview) return;

		_bodyInstance.MaterialOverride = PreviewValidBodyMaterial;
		_edgeComponent.SetMaterial(PreviewValidEdgeMaterial);
	}

	public void SetInvalidPreview()
	{
		if (_mode != Mode.Preview) return;

		_bodyInstance.MaterialOverride = PreviewInvalidBodyMaterial;
		_edgeComponent.SetMaterial(PreviewInvalidEdgeMaterial);
	}

	public void ExitPreview()
	{
		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		_bodyInstance.MaterialOverride = BodyMaterial;
		_edgeComponent.SetMaterial(EdgeMaterial);

		InitBuilding();
		StartConstructionTween();
	}

	private void InitBuilding()
	{
		InitBodyInstance();
		InitEdgeComponent();
	}

	private void InitBodyInstance()
	{
		_bodyInstance.Scale = new Vector3(1, 0, 1);
		_bodyInstance.Position = new Vector3(_bodyInstance.Position.X, 0, _bodyInstance.Position.Z);
	}

	private void InitEdgeComponent()
	{
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
		_bodyInstance.Position = new Vector3(_bodyInstance.Position.X, top / 2f, _bodyInstance.Position.Z);

		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(scaleY, top));
	}

	public Rect2 GetFootprintRect()
	{
		var half = FootprintSize * 0.5f;
		return new Rect2(
			GlobalPosition.X - half.X,
			GlobalPosition.Z - half.Y,
			FootprintSize.X,
			FootprintSize.Y
		);
	}
}
