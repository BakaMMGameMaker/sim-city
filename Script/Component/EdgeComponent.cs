using Godot;
using System;
using System.Collections.Generic;

namespace MySimCity;

/// <summary>
/// 建筑本体边缘高亮组件。
/// 只关心本体的长宽高与本地坐标系，不感知地基或 BodyOffset。
/// BodyOffset 由 Building 把整个 Body 节点平移来处理。
/// </summary>
[GlobalClass]
public partial class EdgeComponent : Node3D
{
	[Export]
	public StandardMaterial3D Material;

	[Export]
	public float Thickness = 0.07f;

	public class BuildingConstructionState
	{
		public float ScaleY;
		public float Top;

		public BuildingConstructionState(float scaleY = 0.0f, float top = 0.0f)
		{
			ScaleY = scaleY;
			Top = top;
		}
	}

	/// <summary>
	/// 只描述本体本身的尺寸与更新回调。
	/// 本体本地坐标系始终以中心为原点：XZ 范围 [-Width/2, Width/2] × [-Depth/2, Depth/2]。
	/// </summary>
	public class EdgeSetupConfig
	{
		public float Width;
		public float Depth;
		public float Height;
		public float Thickness = 0.07f;
		public StandardMaterial3D Material;
		public Action<MeshInstance3D, BuildingConstructionState> VerticalUpdater;
		public Action<MeshInstance3D, BuildingConstructionState> TopUpdater;
		public Action<MeshInstance3D, BuildingConstructionState> BottomUpdater;
	}

	private class EdgeData
	{
		public MeshInstance3D Instance { get; }
		public Action<MeshInstance3D, BuildingConstructionState> Updater { get; }

		public EdgeData(MeshInstance3D instance, Action<MeshInstance3D, BuildingConstructionState> updater)
		{
			Instance = instance;
			Updater = updater;
		}
	}

	private bool _enabled = true;
	private readonly List<EdgeData> _edges = new();

	public void Setup(EdgeSetupConfig config)
	{
		Material = config.Material;
		Thickness = config.Thickness;
		_enabled = true;
		ClearEdges();
		RegisterAllEdges(config);
	}

	public void SetMaterial(StandardMaterial3D newMaterial)
	{
		Material = newMaterial;
		foreach (var edge in _edges)
		{
			if (IsInstanceValid(edge.Instance))
				edge.Instance.MaterialOverride = Material;
		}
	}

	public void Enable()
	{
		_enabled = true;
		foreach (var edge in _edges)
		{
			if (IsInstanceValid(edge.Instance))
				edge.Instance.Visible = true;
		}
	}

	public void Disable()
	{
		_enabled = false;
		foreach (var edge in _edges)
		{
			if (IsInstanceValid(edge.Instance))
				edge.Instance.Visible = false;
		}
	}

	public void Update(BuildingConstructionState state)
	{
		if (!_enabled) return;

		foreach (var edge in _edges)
			edge.Updater(edge.Instance, state);
	}

	private void RegisterAllEdges(EdgeSetupConfig config)
	{
		float halfX = config.Width * 0.5f;
		float halfZ = config.Depth * 0.5f;
		float h = config.Height;
		float t = config.Thickness;

		var vertical = config.VerticalUpdater ?? ((_, _) => { });
		var top = config.TopUpdater ?? ((_, _) => { });
		var bottom = config.BottomUpdater ?? ((_, _) => { });

		// 竖直棱：体块四个角
		foreach (float x in new[] { -halfX, halfX })
		{
			foreach (float z in new[] { -halfZ, halfZ })
				RegisterEdge(new Vector3(t, h, t), new Vector3(x, 0, z), vertical);
		}

		// 顶边
		foreach (float z in new[] { -halfZ, halfZ })
			RegisterEdge(new Vector3(config.Width, t, t), new Vector3(0, 0, z), top);
		foreach (float x in new[] { -halfX, halfX })
			RegisterEdge(new Vector3(t, t, config.Depth), new Vector3(x, 0, 0), top);

		// 底边
		foreach (float z in new[] { -halfZ, halfZ })
			RegisterEdge(new Vector3(config.Width, t, t), new Vector3(0, 0, z), bottom);
		foreach (float x in new[] { -halfX, halfX })
			RegisterEdge(new Vector3(t, t, config.Depth), new Vector3(x, 0, 0), bottom);
	}

	private void RegisterEdge(Vector3 edgeSize, Vector3 basePosition, Action<MeshInstance3D, BuildingConstructionState> updater)
	{
		System.Diagnostics.Debug.Assert(_enabled, "组件未启用但尝试注册边");

		var mi = MakeEdge(edgeSize);
		mi.Position = basePosition;
		_edges.Add(new EdgeData(mi, updater));
	}

	private MeshInstance3D MakeEdge(Vector3 size)
	{
		var mi = new MeshInstance3D();
		var box = new BoxMesh { Size = size };
		mi.Mesh = box;
		mi.MaterialOverride = Material;
		AddChild(mi);
		return mi;
	}

	private void ClearEdges()
	{
		foreach (var edgeData in _edges)
		{
			var mi = edgeData.Instance;
			if (IsInstanceValid(mi))
				mi.QueueFree();
		}
		_edges.Clear();
	}
}
