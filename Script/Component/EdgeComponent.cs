using Godot;
using System;
using System.Collections.Generic;

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

	public void Setup(StandardMaterial3D edgeMaterial, float edgeThickness)
	{
		Material = edgeMaterial;
		Thickness = edgeThickness;
		_enabled = true;
		ClearEdges();
	}

	public void SetMaterial(StandardMaterial3D newMaterial)
	{
		Material = newMaterial;
		foreach (var edge in _edges)
		{
			if (IsInstanceValid(edge.Instance))
			{
				edge.Instance.MaterialOverride = Material;
			}
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

	public void RegisterEdge(Vector3 edgeSize, Vector3 basePosition, Action<MeshInstance3D, BuildingConstructionState> updater)
	{
		System.Diagnostics.Debug.Assert(_enabled, "组件未启用但尝试注册边");

		var mi = MakeEdge(edgeSize);
		mi.Position = basePosition;
		_edges.Add(new EdgeData(mi, updater));
	}

	public void Update(BuildingConstructionState state)
	{
		if (!_enabled) return;

		foreach (var edge in _edges)
		{
			edge.Updater(edge.Instance, state);
		}
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
