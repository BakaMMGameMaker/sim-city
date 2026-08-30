using System;
using System.IO;
using System.Text;

namespace MySimCity.BuildTools;

/// <summary>
/// 独立 CLI 入口：与构建目标（ValidateDefinitions.targets）配合使用，也可手动执行：
///   dotnet run --project Tools/DefinitionValidator -- --root .
/// --errors-file 模式（构建时使用）：错误写入 UTF-8 文件（file|line|message 每行一条），
///   控制台只输出摘要——错误由 ValidateDefinitions.targets 里的上报任务逐条
///   Log.LogError，保证中文与行号在 IDE 错误列表中完好显示；
/// 不带 --errors-file 时：错误直接打印到控制台（file(line,col): error MSC_DEF: message）。
/// 有错误时退出码为 1。
/// </summary>
public static class Program
{
	public static int Main(string[] args)
	{
		string? root = null;
		string? errorsFile = null;
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "--root" && i + 1 < args.Length)
				root = args[++i];
			else if (args[i] == "--errors-file" && i + 1 < args.Length)
				errorsFile = args[++i];
		}

		if (root == null)
		{
			Console.Error.WriteLine("用法：DefinitionValidator --root <项目目录> [--errors-file <路径>]");
			return 2;
		}

		var result = ValidationRunner.ValidateProject(root);

		if (errorsFile != null)
		{
			WriteErrorsFile(errorsFile, result);
		}
		else
		{
			foreach (var error in result.Errors)
				Console.WriteLine($"{error.File}({error.Line},0): error MSC_DEF: {error.Message}");
		}

		// 摘要行保持 ASCII，避免经 Exec 控制台捕获时因代码页产生乱码；
		// 详细中文错误经 --errors-file 由 MSBuild Log.LogError 直接呈现。
		Console.WriteLine($"Definition validation: {result.FilesScanned} .tres/.tscn scanned, {result.DefinitionsChecked} definitions checked, {result.Errors.Count} error(s).");
		return result.Errors.Count == 0 ? 0 : 1;
	}

	private static void WriteErrorsFile(string path, ValidationRunner.Result result)
	{
		var sb = new StringBuilder();
		foreach (var error in result.Errors)
			sb.Append(error.File).Append('|').Append(error.Line).Append('|').Append(error.Message).Append('\n');
		File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
	}
}
