using System;
using System.Collections.Generic;

namespace MySimCity.BuildTools;

/// <summary>一条校验错误：文件 + 行号 + 消息，可直接映射为 MSBuild 编译错误。</summary>
public sealed class DefinitionError
{
	public readonly string File;
	public readonly int Line;
	public readonly string Message;

	public DefinitionError(string file, int line, string message)
	{
		File = file;
		Line = line;
		Message = message;
	}

	public override string ToString() => $"{File}({Line}): {Message}";
}
