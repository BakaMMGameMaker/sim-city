using System;

namespace MySimCity;

/// <summary>一次性定时器句柄：超时时触发 Timeout（Action 事件）。</summary>
public interface ITimerHandle
{
	event Action Timeout;
}
