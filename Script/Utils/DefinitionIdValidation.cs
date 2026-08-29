using System.Text.RegularExpressions;

namespace MySimCity;

/// <summary>
/// 定义类资源（建筑 / 材料）Id 的统一校验规则：
/// 小写字母开头，仅允许小写字母、数字、下划线；与 .tres 文件名一致。
/// </summary>
public static class DefinitionIdValidation
{
	public const string Pattern = "^[a-z][a-z0-9_]*$";
	public const string PatternHint = "小写字母开头，仅小写字母/数字/下划线";

	private static readonly Regex _regex = new(Pattern, RegexOptions.Compiled);

	public static bool IsValid(string id)
	{
		return !string.IsNullOrWhiteSpace(id) && _regex.IsMatch(id);
	}

	public static string ErrorMessage => $"Id 需匹配 ^{Pattern}$（{PatternHint}）";
}
