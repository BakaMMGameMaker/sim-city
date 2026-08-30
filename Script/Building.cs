using Godot;
using System;
using System.Collections.Generic;
using MySimCity.Definitions;

namespace MySimCity;

/// <summary>
/// 建筑节点：只负责外观/预览/建造流程，不持有产出组件。
/// 几何参数、建造时间与成本等数据全部来自 IBuildingDefinition
/// （经 ApplyDefinition 注入，本节点持有定义引用而非逐字段复制）；
/// 本节点仅保留场景级视觉资产（材质、地基厚度）与运行时状态（等级/模式）。
/// 建造完成时发送 ConstructionFinished 事件，由外部挂载的
/// ProducingComponent 自行监听并启动产出。
/// </summary>
[GlobalClass]
public partial class Building : Node3D, IProducibleBuilding
{
	public uint Level { get; set; } = 1;

	public event Action ConstructionFinished;

	public IBuildingDefinition Definition { get; private set; }

	[Export]
	public float FoundationThickness = 0.06f;

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

	private MeshInstance3D _foundationMesh;
	private Node3D _bodyRoot;
	private MeshInstance3D _bodyMesh;
	private ShaderMaterial _constructionMaterialInstance;

	private Vector3 _bodyBaseOffset = Vector3.Zero;

	private enum Mode { Preview, Constructing, Idle }
	private Mode _mode = Mode.Idle;

	public override void _Ready()
	{
		AssertExports.AssertExportsNode(this);

		_foundationMesh = GetNode<MeshInstance3D>("Foundation");
		_bodyRoot = GetNode<Node3D>("Body");
		_bodyMesh = GetNode<MeshInstance3D>("Body/Mesh");

		if (Definition != null)
			SetupBuilding();
	}

	public void ApplyDefinition(IBuildingDefinition def)
	{
		ArgumentNullException.ThrowIfNull(def);

		Definition = def;
		if (IsInsideTree())
			SetupBuilding();
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
		var def = Definition;
		if (def.BodyAlign == BodyAlignMode.Center)
		{
			_bodyBaseOffset = Vector3.Zero;
			return;
		}

		float fw = def.FoundationSize.X * GridSize;
		float fd = def.FoundationSize.Y * GridSize;
		float bodyCenterX = -fw * 0.5f + def.BodyOffsetX + def.Width * 0.5f;
		float bodyCenterZ = -fd * 0.5f + def.BodyOffsetZ + def.Depth * 0.5f;
		_bodyBaseOffset = new Vector3(bodyCenterX, 0f, bodyCenterZ);
	}

	private void SetupFoundation()
	{
		var def = Definition;
		float worldW = def.FoundationSize.X * GridSize;
		float worldD = def.FoundationSize.Y * GridSize;

		var box = new BoxMesh { Size = new Vector3(worldW, FoundationThickness, worldD) };
		_foundationMesh.Mesh = box;
		_foundationMesh.MaterialOverride = FoundationMaterial;
		_foundationMesh.Position = new Vector3(0, FoundationThickness * 0.5f, 0);
	}

	private void SetupBody()
	{
		var def = Definition;
		_bodyRoot.Position = _bodyBaseOffset;

		var box = new BoxMesh { Size = new Vector3(def.Width, def.Height, def.Depth) };
		_bodyMesh.Mesh = box;
		_bodyMesh.MaterialOverride = BodyMaterial;
		_bodyMesh.Position = new Vector3(0, def.Height * 0.5f, 0);
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

		_foundationMesh.MaterialOverride = foundationMat;
		_bodyMesh.MaterialOverride = bodyMat;
	}

	public void ExitPreview()
	{
		_foundationMesh.MaterialOverride = FoundationMaterial;
		_bodyMesh.MaterialOverride = BodyMaterial;
	}

	public void StartConstruction()
	{
		_mode = Mode.Constructing;

		_foundationMesh.MaterialOverride = FoundationMaterial;

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
		tween.TweenMethod(Callable.From<float>(UpdateConstruction), 0.0f, 1.0f, Definition.BuildTime);
		tween.Finished += OnConstructionFinished;
	}

	private void UpdateConstruction(float progress)
	{
		SetBuildHeight(progress);
	}

	private void SetBuildHeight(float progress)
	{
		_constructionMaterialInstance.SetShaderParameter("build_height", -Definition.Height * 0.5f + progress * Definition.Height);
	}

	private void OnConstructionFinished()
	{
		_mode = Mode.Idle;
		_bodyMesh.MaterialOverride = BodyMaterial;
		ConstructionFinished?.Invoke();
	}

	public Rect2 GetFootprintRect()
	{
		if (Definition == null) return default;

		float worldW = Definition.FoundationSize.X * GridSize;
		float worldD = Definition.FoundationSize.Y * GridSize;
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
