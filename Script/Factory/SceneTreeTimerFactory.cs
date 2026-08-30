using Godot;
using System;

namespace MySimCity;

/// <summary>
/// 基于 SceneTree 的定时器工厂：把 tree.CreateTimer 包成 ITimerHandle。
/// 这是项目中唯一把场景树适配为 ITimerFactory 的边界，ProducingComponent
/// 等消费方只依赖接口。
/// </summary>
public sealed class SceneTreeTimerFactory : ITimerFactory
{
	private readonly SceneTree _tree;

	public SceneTreeTimerFactory(SceneTree tree)
	{
		_tree = tree ?? throw new ArgumentNullException(nameof(tree));
	}

	public ITimerHandle CreateTimer(double seconds)
	{
		return new Adapter(_tree.CreateTimer(seconds));
	}

	private sealed class Adapter : ITimerHandle
	{
		public event Action Timeout;

		public Adapter(SceneTreeTimer timer)
		{
			if (timer == null) throw new ArgumentNullException(nameof(timer));
			timer.Timeout += () => Timeout?.Invoke();
		}
	}
}
