using Godot;
using System;

namespace MySimCity;

[GlobalClass]
public partial class Building : Node3D
{
	public enum BodyAlignMode
	{
		Center,
		Offset
	}

	[Export]
	public float Width = 3.2f;

	[Export]
	public float Depth = 3.2f;

	[Export]
	public float Height = 12.0f;

	[Export]
	public float BuildTime = 6.0f;

	[Export]
	public Vector2I FoundationSize = new(4, 4);

	[Export]
	public float FoundationThickness = 0.06f;

	[Export]
	public BodyAlignMode BodyAlign = BodyAlignMode.Center;

	[Export]
	public float BodyOffsetX = 0f;

	[Export]
	public float BodyOffsetZ = 0f;

	[Export]
	public StandardMaterial3D BodyMaterial;

	[Export]
	public ShaderMaterial ConstructionMaterial;

	[Export]
	public StandardMaterial3D FoundationMaterial;

	[Export]
	public StandardMaterial3D PreviewValidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidBodyMaterial;

	[Export]
	public StandardMaterial3D PreviewValidFoundationMaterial;

	[Export]
	public StandardMaterial3D PreviewInvalidFoundationMaterial;

	[Export]
	public MaterialAmount[] Costs = [];

	private MeshInstance3D _foundationInstance;
	private Node3D _bodyRoot;
	private MeshInstance3D _bodyMesh;
	private ShaderMaterial _constructionMaterialInstance;
	private ProductionComponent _productionComponent;

	// _Ready 之前调用 Initialize/ApplyDefinition 时暂存，_Ready 后再注入组件
	private IInventory _pendingInventory;
	private ProductionLevelConfig[] _pendingProductionTable = [];
	private int _pendingLevel = 1;

	private Vector3 _bodyBaseOffset = Vector3.Zero;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_foundationInstance = GetNode<MeshInstance3D>("Foundation");
		_bodyRoot = GetNode<Node3D>("Body");
		_bodyMesh = GetNode<MeshInstance3D>("Body/Mesh");
		_productionComponent = GetNode<ProductionComponent>("ProductionComponent");

		if (_pendingInventory != null)
			_productionComponent.Initialize(_pendingInventory);
		_productionComponent.Configure(_pendingProductionTable, _pendingLevel);

		SetupBuilding();
	}

	public void Initialize(IInventory inventory)
	{
		ArgumentNullException.ThrowIfNull(inventory);

		_pendingInventory = inventory;
		if (_productionComponent != null)
			_productionComponent.Initialize(inventory);
	}

	private float GridSize => GameConfig.Instance != null ? GameConfig.Instance.GridSize : 1.0f;

	private void SetupBuilding()
	{
		ComputeBodyOffset();
		SetupFoundation();
		SetupBody();
	}

	private void ComputeBodyOffset()
	{
		if (BodyAlign == BodyAlignMode.Center)
		{
			_bodyBaseOffset = Vector3.Zero;
			return;
		}

		float fw = FoundationSize.X * GridSize;
		float fd = FoundationSize.Y * GridSize;
		float bodyCenterX = -fw * 0.5f + BodyOffsetX + Width * 0.5f;
		float bodyCenterZ = -fd * 0.5f + BodyOffsetZ + Depth * 0.5f;
		_bodyBaseOffset = new Vector3(bodyCenterX, 0f, bodyCenterZ);
	}

	private void SetupFoundation()
	{
		float worldW = FoundationSize.X * GridSize;
		float worldD = FoundationSize.Y * GridSize;

		var box = new BoxMesh { Size = new Vector3(worldW, FoundationThickness, worldD) };
		_foundationInstance.Mesh = box;
		_foundationInstance.MaterialOverride = FoundationMaterial;
		_foundationInstance.Position = new Vector3(0, FoundationThickness * 0.5f, 0);
	}

	private void SetupBody()
	{
		_bodyRoot.Position = _bodyBaseOffset;

		var box = new BoxMesh { Size = new Vector3(Width, Height, Depth) };
		_bodyMesh.Mesh = box;
		_bodyMesh.MaterialOverride = BodyMaterial;
		_bodyMesh.Position = new Vector3(0, Height * 0.5f, 0);
	}


	public void EnterPreview()
	{
		_mode = Mode.Preview;
	}

	public void SetPreviewValid(bool valid)
	{
		if (_mode != Mode.Preview) return;

		var foundationMat = valid ? PreviewValidFoundationMaterial : PreviewInvalidFoundationMaterial;
		var bodyMat = valid ? PreviewValidBodyMaterial : PreviewInvalidBodyMaterial;

		_foundationInstance.MaterialOverride = foundationMat;
		_bodyMesh.MaterialOverride = bodyMat;
	}

	public void ExitPreview()
	{
		_foundationInstance.MaterialOverride = FoundationMaterial;
		_bodyMesh.MaterialOverride = BodyMaterial;
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		_foundationInstance.MaterialOverride = FoundationMaterial;

		InitConstruction();
		StartConstructionTween();
	}

	private void InitConstruction()
	{
		_constructionMaterialInstance = (ShaderMaterial)ConstructionMaterial.Duplicate();
		_bodyMesh.MaterialOverride = _constructionMaterialInstance;
		SetBuildHeight(0.0f);
	}

	private void StartConstructionTween()
	{
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenMethod(Callable.From<float>(UpdateConstruction), 0.0f, 1.0f, BuildTime);
		tween.Finished += OnConstructionFinished;
	}

	private void UpdateConstruction(float progress)
	{
		SetBuildHeight(progress);
	}

	private void SetBuildHeight(float progress)
	{
		_constructionMaterialInstance.SetShaderParameter("build_height", -Height * 0.5f + progress * Height);
	}

	private void OnConstructionFinished()
	{
		_mode = Mode.Idle;
		_bodyMesh.MaterialOverride = BodyMaterial;
		_productionComponent.StartProduction();
	}

	public Rect2 GetFootprintRect()
	{
		float worldW = FoundationSize.X * GridSize;
		float worldD = FoundationSize.Y * GridSize;
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

	public void ApplyDefinition(BuildingDefinition def)
	{
		ArgumentNullException.ThrowIfNull(def);

		Width = def.Width;
		Depth = def.Depth;
		Height = def.Height;
		BuildTime = def.BuildTime;
		FoundationSize = def.FoundationSize;
		BodyAlign = def.BodyAlign;
		BodyOffsetX = def.BodyOffsetX;
		BodyOffsetZ = def.BodyOffsetZ;
		Costs = def.Costs ?? [];

		_pendingProductionTable = def.ProductionTable ?? [];
		_pendingLevel = 1;
		if (_productionComponent != null)
			_productionComponent.Configure(_pendingProductionTable, _pendingLevel);

		if (IsInsideTree())
		{
			SetupBuilding();
		}
	}
}
