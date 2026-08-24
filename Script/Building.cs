using Godot;
using System;

namespace MySimCity;

/// <summary>
/// 建筑实体。
/// - Foundation 只负责地基尺寸与网格占用
/// - Body 节点承载本体 Mesh + EdgeComponent，BodyOffset 只影响 Body 节点位置
/// - Production 通过挂载的 ProductionComponent 实现，Building 自身不硬编码产出逻辑
/// </summary>
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

	/// <summary>地基占用格子（X = 宽，Y = 深）</summary>
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

	/// <summary>建造成本（可配置多材料）</summary>
	[Export]
	public MaterialAmount[] Costs = Array.Empty<MaterialAmount>();

	private MeshInstance3D _foundationInstance;
	private Node3D _bodyRoot;
	private MeshInstance3D _bodyMesh;
	private EdgeComponent _edgeComponent;
	private ProductionComponent _productionComponent;

	private Vector3 _bodyBaseOffset = Vector3.Zero;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	private IInventory _inventory;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_foundationInstance = GetNodeOrNull<MeshInstance3D>("Foundation");
		_bodyRoot = GetNodeOrNull<Node3D>("Body");
		_bodyMesh = GetNodeOrNull<MeshInstance3D>("Body/Mesh") ?? GetNodeOrNull<MeshInstance3D>("Body");
		_edgeComponent = GetNodeOrNull<EdgeComponent>("Body/EdgeComponent") ?? GetNodeOrNull<EdgeComponent>("EdgeComponent");
		_productionComponent = GetNodeOrNull<ProductionComponent>("ProductionComponent");

		SetupBuilding();
	}

	/// <summary>
	/// 依赖注入入口。必须在产出相关逻辑前调用。
	/// </summary>
	public void Initialize(IInventory inventory)
	{
		_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
		_productionComponent?.Initialize(_inventory);
	}

	private float GridSize => GameConfig.Instance != null ? GameConfig.Instance.GridSize : 1.0f;

	private void SetupBuilding()
	{
		ComputeBodyOffset();
		SetupFoundation();
		SetupBody();
		SetupEdgeComponent();
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
		if (_foundationInstance == null) return;

		float worldW = FoundationSize.X * GridSize;
		float worldD = FoundationSize.Y * GridSize;

		var box = new BoxMesh { Size = new Vector3(worldW, FoundationThickness, worldD) };
		_foundationInstance.Mesh = box;
		_foundationInstance.MaterialOverride = FoundationMaterial;
		_foundationInstance.Position = new Vector3(0, FoundationThickness * 0.5f, 0);
	}

	private void SetupBody()
	{
		if (_bodyRoot != null)
			_bodyRoot.Position = _bodyBaseOffset;

		if (_bodyMesh == null) return;

		var box = new BoxMesh { Size = new Vector3(Width, Height, Depth) };
		_bodyMesh.Mesh = box;
		_bodyMesh.MaterialOverride = BodyMaterial;
		// Mesh 在 Body 本地坐标系中心
		_bodyMesh.Position = new Vector3(0, Height * 0.5f, 0);
	}

	private void SetupEdgeComponent()
	{
		if (_edgeComponent == null) return;

		// EdgeComponent 只拿到本体信息，不再接收 Offset
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
		if (_bodyMesh != null)
		{
			_bodyMesh.Scale = Vector3.One;
			_bodyMesh.Position = new Vector3(0, Height * 0.5f, 0);
		}
		_edgeComponent?.Update(new EdgeComponent.BuildingConstructionState(1.0f, Height));
	}

	public void SetPreviewValid(bool valid)
	{
		if (_mode != Mode.Preview) return;

		var foundationMat = valid ? PreviewValidFoundationMaterial : PreviewInvalidFoundationMaterial;
		var bodyMat = valid ? PreviewValidBodyMaterial : PreviewInvalidBodyMaterial;
		var edgeMat = valid ? PreviewValidEdgeMaterial : PreviewInvalidEdgeMaterial;

		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = foundationMat;
		if (_bodyMesh != null)
			_bodyMesh.MaterialOverride = bodyMat;
		_edgeComponent?.SetMaterial(edgeMat);
	}

	public void ExitPreview()
	{
		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = FoundationMaterial;
		if (_bodyMesh != null)
			_bodyMesh.MaterialOverride = BodyMaterial;
		_edgeComponent?.SetMaterial(EdgeMaterial);
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		if (_foundationInstance != null)
			_foundationInstance.MaterialOverride = FoundationMaterial;
		if (_bodyMesh != null)
			_bodyMesh.MaterialOverride = BodyMaterial;
		_edgeComponent?.SetMaterial(EdgeMaterial);

		InitConstruction();
		StartConstructionTween();
	}

	private void InitConstruction()
	{
		if (_bodyMesh != null)
		{
			_bodyMesh.Scale = new Vector3(1, 0, 1);
			_bodyMesh.Position = new Vector3(0, 0, 0);
		}
		_edgeComponent?.Update(new EdgeComponent.BuildingConstructionState(0.0f, 0.0f));
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
		float scaleY = progress;
		float top = progress * Height;

		if (_bodyMesh != null)
		{
			_bodyMesh.Scale = new Vector3(1, scaleY, 1);
			_bodyMesh.Position = new Vector3(0, top / 2f, 0);
		}

		_edgeComponent?.Update(new EdgeComponent.BuildingConstructionState(scaleY, top));
	}

	private void OnConstructionFinished()
	{
		_mode = Mode.Idle;
		_productionComponent?.StartProduction();
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

	/// <summary>
	/// 用外部可配置的 BuildingDefinition 覆盖当前参数。
	/// 生产表会同步到挂载的 ProductionComponent。
	/// </summary>
	public void ApplyDefinition(BuildingDefinition def)
	{
		if (def == null) throw new ArgumentNullException(nameof(def));

		Width = def.Width;
		Depth = def.Depth;
		Height = def.Height;
		BuildTime = def.BuildTime;
		FoundationSize = def.FoundationSize;
		BodyAlign = def.BodyAlign;
		BodyOffsetX = def.BodyOffsetX;
		BodyOffsetZ = def.BodyOffsetZ;
		Costs = def.Costs ?? Array.Empty<MaterialAmount>();

		if (_productionComponent != null)
		{
			_productionComponent.Configure(def.ProductionTable, level: 1);
		}

		if (IsInsideTree())
		{
			SetupBuilding();
		}
	}
}
