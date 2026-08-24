using Godot;
using System;

[GlobalClass]
public partial class Building : Node3D
{
	public enum BodyAlignMode
	{
		/// <summary>建筑体中心与地基中心对齐</summary>
		Center,
		/// <summary>建筑体角落相对地基角落偏移（OffsetX / OffsetZ）</summary>
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
	public int FoundationWidth = 4;

	[Export]
	public int FoundationDepth = 4;

	[Export]
	public float FoundationThickness = 0.06f;

	/// <summary>建筑体与地基的对齐方式</summary>
	[Export]
	public BodyAlignMode BodyAlign = BodyAlignMode.Center;

	/// <summary>仅 Offset 模式生效：建筑体 min-corner 相对地基 min-corner 的 X 偏移</summary>
	[Export]
	public float BodyOffsetX = 0f;

	/// <summary>仅 Offset 模式生效：建筑体 min-corner 相对地基 min-corner 的 Z 偏移</summary>
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

	// ---- 建造成本 & 产出 ----
	[Export]
	public int WoodCost = 0;

	[Export]
	public bool IsProducer = false;

	[Export]
	public int Level = 1;

	/// <summary>产出等级配置表（仅 IsProducer 时使用）</summary>
	[Export]
	public ProductionLevelConfig[] ProductionTable;

	private MeshInstance3D _bodyInstance;
	private MeshInstance3D _foundationInstance;
	private EdgeComponent _edgeComponent;

	/// <summary>建筑体相对 Building 原点的 XZ 偏移（已根据 AlignMode 计算）</summary>
	private Vector3 _bodyBaseOffset = Vector3.Zero;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	private Timer _productionTimer;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_foundationInstance = GetNodeOrNull<MeshInstance3D>("Foundation");
		if (_foundationInstance == null)
		{
			_foundationInstance = new MeshInstance3D { Name = "Foundation" };
			AddChild(_foundationInstance);
		}

		_bodyInstance = GetNode<MeshInstance3D>("Body");
		_edgeComponent = GetNode<EdgeComponent>("EdgeComponent");

		SetupBuilding();
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

		// Offset 模式：建筑体 min-corner = 地基 min-corner + (BodyOffsetX, BodyOffsetZ)
		float fw = FoundationWidth * GridSize;
		float fd = FoundationDepth * GridSize;
		float bodyCenterX = -fw * 0.5f + BodyOffsetX + Width * 0.5f;
		float bodyCenterZ = -fd * 0.5f + BodyOffsetZ + Depth * 0.5f;
		_bodyBaseOffset = new Vector3(bodyCenterX, 0f, bodyCenterZ);
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
		_bodyInstance.Position = new Vector3(_bodyBaseOffset.X, Height * 0.5f, _bodyBaseOffset.Z);
	}

	private void SetupEdgeComponent()
	{
		_edgeComponent.Setup(new EdgeComponent.EdgeSetupConfig
		{
			Width = Width,
			Depth = Depth,
			Height = Height,
			Thickness = EdgeThickness,
			OffsetX = _bodyBaseOffset.X,
			OffsetZ = _bodyBaseOffset.Z,
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
		_bodyInstance.Position = new Vector3(_bodyBaseOffset.X, Height * 0.5f, _bodyBaseOffset.Z);
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
		_bodyInstance.Position = new Vector3(_bodyBaseOffset.X, 0, _bodyBaseOffset.Z);
		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(0.0f, 0.0f));
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

		_bodyInstance.Scale = new Vector3(1, scaleY, 1);
		_bodyInstance.Position = new Vector3(_bodyBaseOffset.X, top / 2f, _bodyBaseOffset.Z);

		_edgeComponent.Update(new EdgeComponent.BuildingConstructionState(scaleY, top));
	}

	private void OnConstructionFinished()
	{
		_mode = Mode.Idle;
		if (IsProducer)
			StartProduction();
	}

	private void StartProduction()
	{
		var config = GetCurrentProductionConfig();
		if (config == null) return;

		if (_productionTimer == null)
		{
			_productionTimer = new Timer
			{
				Name = "ProductionTimer",
				OneShot = false,
				Autostart = false
			};
			AddChild(_productionTimer);
			_productionTimer.Timeout += OnProductionTick;
		}

		_productionTimer.WaitTime = config.IntervalSeconds;
		_productionTimer.Start();
	}

	private ProductionLevelConfig GetCurrentProductionConfig()
	{
		if (ProductionTable == null || ProductionTable.Length == 0)
			return null;

		ProductionLevelConfig best = null;
		foreach (var c in ProductionTable)
		{
			if (c == null) continue;
			if (c.Level <= Level && (best == null || c.Level > best.Level))
				best = c;
		}
		return best;
	}

	private void OnProductionTick()
	{
		var config = GetCurrentProductionConfig();
		if (config == null) return;

		if (config.MaterialId == "wood" && Inventory.Instance != null)
			Inventory.Instance.AddWood(config.Amount);
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

	/// <summary>
	/// 根据类型应用预设数值（住宅 / 伐木场）。
	/// 可在外部再覆盖个别字段。
	/// </summary>
	public void ApplyPreset(MySimCity.BuildingType type)
	{
		switch (type)
		{
			case MySimCity.BuildingType.Residential:
				// 普通住宅：中等体积，需要原木，无产出
				Width = 2.8f;
				Depth = 2.8f;
				Height = 5.5f;
				BuildTime = 4.5f;
				FoundationWidth = 3;
				FoundationDepth = 3;
				BodyAlign = BodyAlignMode.Center;
				BodyOffsetX = 0f;
				BodyOffsetZ = 0f;
				WoodCost = 12;
				IsProducer = false;
				Level = 1;
				ProductionTable = null;
				break;

			case MySimCity.BuildingType.LumberMill:
				// 伐木场：稍大，低成本起步，产出原木
				Width = 3.6f;
				Depth = 3.2f;
				Height = 4.2f;
				BuildTime = 7.0f;
				FoundationWidth = 4;
				FoundationDepth = 4;
				BodyAlign = BodyAlignMode.Offset;
				// 让建筑体稍微偏向地基一角，便于视觉区分
				BodyOffsetX = 0.2f;
				BodyOffsetZ = 0.15f;
				WoodCost = 5;
				IsProducer = true;
				Level = 1;
				ProductionTable = new[]
				{
					new ProductionLevelConfig { Level = 1, IntervalSeconds = 12.0f, Amount = 2, MaterialId = "wood" },
					new ProductionLevelConfig { Level = 2, IntervalSeconds = 10.0f, Amount = 3, MaterialId = "wood" },
					new ProductionLevelConfig { Level = 3, IntervalSeconds = 8.0f, Amount = 5, MaterialId = "wood" },
				};
				break;
		}

		// 重新应用几何（_Ready 已跑过后调用时需要）
		if (IsInsideTree())
		{
			SetupBuilding();
		}
	}
}
