namespace MySimCity;

/// <summary>定时器工厂：按秒创建一次性定时器。</summary>
public interface ITimerFactory
{
	ITimerHandle CreateTimer(double seconds);
}
