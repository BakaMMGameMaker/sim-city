using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MySimCity.BuildTools;

/// <summary>
/// Godot .tres（format=3 文本资源）的轻量解析器，只覆盖本项目定义资源所用语法：
/// 段头（gd_resource / ext_resource / sub_resource / resource）、
/// 属性（字符串 / 整数 / 浮点 / 布尔 / null / Vector2i / SubResource 数组 /
/// ExtResource 与 SubResource 引用）。
/// 已知属性严格解析（失败即报错并带行号）；未知属性与段只记录不解析，
/// 对未来 Godot 版本新增的键保持鲁棒。
/// </summary>
public sealed class TresDocument
{
	public readonly string FilePath;
	public readonly List<ExtResource> ExtResources = [];
	public readonly List<ResourceSection> SubResources = [];
	public readonly Dictionary<string, ResourceSection> SubById = new(StringComparer.Ordinal);
	public ResourceSection? Root;

	public TresDocument(string filePath)
	{
		FilePath = filePath;
	}

	/// <summary>由 ext_resource 的 id 解析其磁盘路径（res:// 形式），找不到返回 null。</summary>
	public string? ResolveExtPath(string extRef)
	{
		foreach (var ext in ExtResources)
		{
			if (string.Equals(ext.Id, extRef, StringComparison.Ordinal))
				return ext.Path;
		}
		return null;
	}
}

public sealed class ExtResource
{
	public string Id = "";
	public string Path = "";
	public int Line;
}

public sealed class Property
{
	public readonly string Key;
	public readonly string RawValue;
	public readonly int Line;

	public Property(string key, string rawValue, int line)
	{
		Key = key;
		RawValue = rawValue;
		Line = line;
	}
}

public sealed class ResourceSection
{
	/// <summary>sub_resource 的 id；[resource] 根段为空字符串。</summary>
	public string Id = "";
	public int HeaderLine;
	/// <summary>script = ExtResource("x") 中的 ext 引用 id；未声明为空。</summary>
	public string ScriptExtRef = "";
	public int ScriptLine;
	public readonly List<Property> Properties = [];

	public bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Property? property)
	{
		foreach (var p in Properties)
		{
			if (string.Equals(p.Key, key, StringComparison.Ordinal))
			{
				property = p;
				return true;
			}
		}
		property = null;
		return false;
	}
}

public enum ValueKind
{
	None,
	Null,
	String,
	Int,
	Float,
	Bool,
	Vector2I,
	SubRef,
	ExtRef,
	SubRefArray
}

public readonly struct ParsedValue
{
	public readonly ValueKind Kind;
	public readonly string? StringValue;
	public readonly long IntValue;
	public readonly double FloatValue;
	public readonly bool BoolValue;
	public readonly int X;
	public readonly int Y;
	public readonly IReadOnlyList<string>? RefIds;

	private ParsedValue(ValueKind kind, string? stringValue = null, long intValue = 0,
		double floatValue = 0, bool boolValue = false, int x = 0, int y = 0,
		IReadOnlyList<string>? refIds = null)
	{
		Kind = kind;
		StringValue = stringValue;
		IntValue = intValue;
		FloatValue = floatValue;
		BoolValue = boolValue;
		X = x;
		Y = y;
		RefIds = refIds;
	}

	public static ParsedValue Null => new(ValueKind.Null);
	public static ParsedValue OfString(string? value) => new(ValueKind.String, stringValue: value);
	public static ParsedValue OfInt(long value) => new(ValueKind.Int, intValue: value);
	public static ParsedValue OfFloat(double value) => new(ValueKind.Float, floatValue: value);
	public static ParsedValue OfBool(bool value) => new(ValueKind.Bool, boolValue: value);
	public static ParsedValue OfVector2I(int x, int y) => new(ValueKind.Vector2I, x: x, y: y);
	public static ParsedValue OfRef(ValueKind kind, string? id) => new(kind, stringValue: id);
	public static ParsedValue OfRefArray(IReadOnlyList<string> ids) => new(ValueKind.SubRefArray, refIds: ids);
}

public static class TresParser
{
	public static TresDocument Parse(string filePath, List<DefinitionError> errors)
	{
		var doc = new TresDocument(filePath);
		ResourceSection? current = null;
		ExtResource? currentExt = null;

		string[] lines;
		try
		{
			lines = File.ReadAllLines(filePath);
		}
		catch (Exception ex)
		{
			errors.Add(new DefinitionError(filePath, 0, $"无法读取文件：{ex.Message}"));
			return doc;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			int lineNo = i + 1;
			var line = lines[i].Trim();
			if (line.Length == 0) continue;

			if (line[0] == '[')
			{
				current = null;
				currentExt = null;

				if (!line.EndsWith("]", StringComparison.Ordinal))
				{
					errors.Add(new DefinitionError(filePath, lineNo, $"段头格式错误：{line}"));
					continue;
				}

				var inner = line.Substring(1, line.Length - 2).Trim();
				var keyword = inner;
				var attrPart = "";
				int space = IndexOfWhiteSpace(inner);
				if (space >= 0)
				{
					keyword = inner.Substring(0, space);
					attrPart = inner.Substring(space + 1).Trim();
				}

				switch (keyword)
				{
					case "gd_resource":
						// 头部声明（type / script_class / load_steps / uid 等），校验不关心内容
						break;

					case "ext_resource":
					{
						var attrs = ParseHeaderAttributes(attrPart, filePath, lineNo, errors);
						attrs.TryGetValue("id", out var id);
						attrs.TryGetValue("path", out var path);
						if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path))
						{
							errors.Add(new DefinitionError(filePath, lineNo, $"ext_resource 缺少 id 或 path：{line}"));
						}
						else
						{
							currentExt = new ExtResource { Id = id, Path = path, Line = lineNo };
							doc.ExtResources.Add(currentExt);
						}
						break;
					}

					case "sub_resource":
					{
						var attrs = ParseHeaderAttributes(attrPart, filePath, lineNo, errors);
						attrs.TryGetValue("id", out var id);
						if (string.IsNullOrEmpty(id))
						{
							errors.Add(new DefinitionError(filePath, lineNo, $"sub_resource 缺少 id：{line}"));
						}
						else if (doc.SubById.ContainsKey(id))
						{
							errors.Add(new DefinitionError(filePath, lineNo, $"sub_resource id 重复：{id}"));
						}
						else
						{
							current = new ResourceSection { Id = id, HeaderLine = lineNo };
							doc.SubResources.Add(current);
							doc.SubById[id] = current;
						}
						break;
					}

					case "resource":
						if (doc.Root != null)
						{
							errors.Add(new DefinitionError(filePath, lineNo, "存在多个 [resource] 段"));
						}
						else
						{
							current = new ResourceSection { HeaderLine = lineNo };
							doc.Root = current;
						}
						break;

					default:
						errors.Add(new DefinitionError(filePath, lineNo, $"无法识别的段：{keyword}"));
						break;
				}
				continue;
			}

			int eq = line.IndexOf('=');
			if (eq < 0)
			{
				errors.Add(new DefinitionError(filePath, lineNo, $"无法识别的行：{line}"));
				continue;
			}

			if (current == null)
			{
				errors.Add(new DefinitionError(filePath, lineNo, $"属性出现在段外：{line}"));
				continue;
			}

			var key = line.Substring(0, eq).Trim();
			var raw = line.Substring(eq + 1).Trim();
			if (key.Length == 0)
			{
				errors.Add(new DefinitionError(filePath, lineNo, $"属性名缺失：{line}"));
				continue;
			}

			if (string.Equals(key, "script", StringComparison.Ordinal))
			{
				var value = ParseValue(raw, filePath, lineNo, key, errors);
				if (value.Kind == ValueKind.ExtRef)
				{
					current.ScriptExtRef = value.StringValue ?? "";
					current.ScriptLine = lineNo;
				}
				else
				{
					errors.Add(new DefinitionError(filePath, lineNo, $"script 必须是 ExtResource(...) 引用：{raw}"));
				}
			}
			else
			{
				current.Properties.Add(new Property(key, raw, lineNo));
			}
		}

		if (doc.Root == null)
			errors.Add(new DefinitionError(filePath, 1, "缺少 [resource] 段"));

		return doc;
	}

	/// <summary>解析属性原始文本；语法不合法时写入 errors 并返回 None。</summary>
	public static ParsedValue ParseValue(string raw, string file, int line, string propertyKey,
		List<DefinitionError> errors)
	{
		raw = raw.Trim();
		if (raw.Length == 0) return default;

		if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
			return ParsedValue.Null;

		if (raw[0] == '"')
		{
			int end = FindQuotedEnd(raw, 0);
			if (end < 0)
			{
				errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的字符串缺少结束引号"));
				return default;
			}
			if (raw.Substring(end + 1).Trim().Length != 0)
			{
				errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的值多余内容：{raw}"));
				return default;
			}
			return ParsedValue.OfString(Unescape(raw.Substring(1, end - 1)));
		}

		if (raw.StartsWith("[", StringComparison.Ordinal))
			return ParseArray(raw, file, line, propertyKey, errors);

		if (raw.StartsWith("Array", StringComparison.Ordinal))
		{
			var rest = raw.Substring(5).TrimStart();
			if (rest.StartsWith("[", StringComparison.Ordinal))
			{
				// Array[...] —— 跳过可选类型标注，找到后面的数组部分
				int close = rest.IndexOf(']');
				if (close < 0)
				{
					errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的 Array 标注格式错误：{raw}"));
					return default;
				}
				rest = rest.Substring(close + 1).TrimStart();
			}
			if (rest.StartsWith("(", StringComparison.Ordinal))
			{
				int close = FindMatching(rest, 0, '(', ')');
				if (close < 0)
				{
					errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的 Array 缺少右括号：{raw}"));
					return default;
				}
				return ParseArrayContent(rest.Substring(1, close - 1), file, line, propertyKey, errors);
			}
			if (rest.StartsWith("[", StringComparison.Ordinal))
				return ParseArray(rest, file, line, propertyKey, errors);

			errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的 Array 语法无法解析：{raw}"));
			return default;
		}

		if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
			return ParsedValue.OfBool(true);
		if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
			return ParsedValue.OfBool(false);

		int paren = raw.IndexOf('(');
		if (paren > 0)
		{
			var typeName = raw.Substring(0, paren).Trim();
			int close = FindMatching(raw, paren, '(', ')');
			if (close != raw.Length - 1)
			{
				errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的构造调用格式错误：{raw}"));
				return default;
			}
			var args = raw.Substring(paren + 1, close - paren - 1);

			if (string.Equals(typeName, "ExtResource", StringComparison.Ordinal)
				|| string.Equals(typeName, "SubResource", StringComparison.Ordinal))
			{
				var arg = args.Trim();
				if (arg.Length >= 2 && arg[0] == '"' && FindQuotedEnd(arg, 0) == arg.Length - 1)
				{
					var kind = string.Equals(typeName, "ExtResource", StringComparison.Ordinal)
						? ValueKind.ExtRef
						: ValueKind.SubRef;
					return ParsedValue.OfRef(kind, Unescape(arg.Substring(1, arg.Length - 2)));
				}
				errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的引用参数格式错误：{raw}"));
				return default;
			}

			if (string.Equals(typeName, "Vector2i", StringComparison.Ordinal))
			{
				var parts = SplitTopLevel(args, ',');
				if (parts.Count == 2
					&& int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
					&& int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
				{
					return ParsedValue.OfVector2I(x, y);
				}
			}

			errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的构造值无法解析：{raw}"));
			return default;
		}

		if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
			return ParsedValue.OfInt(longValue);
		if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
			return ParsedValue.OfFloat(doubleValue);

		errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的值无法解析：{raw}"));
		return default;
	}

	private static ParsedValue ParseArray(string raw, string file, int line, string propertyKey,
		List<DefinitionError> errors)
	{
		int close = FindMatching(raw, 0, '[', ']');
		if (close != raw.Length - 1)
		{
			errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的数组缺少结束方括号：{raw}"));
			return default;
		}
		return ParseArrayContent(raw.Substring(1, close - 1), file, line, propertyKey, errors);
	}

	private static ParsedValue ParseArrayContent(string inner, string file, int line, string propertyKey,
		List<DefinitionError> errors)
	{
		inner = inner.Trim();
		if (inner.Length == 0)
			return ParsedValue.OfRefArray(Array.Empty<string>());

		var ids = new List<string>();
		foreach (var element in SplitTopLevel(inner, ','))
		{
			var trimmed = element.Trim();
			var id = TryParseRef(trimmed, "SubResource");
			if (id != null)
			{
				ids.Add(id);
			}
			else
			{
				errors.Add(new DefinitionError(file, line, $"属性 {propertyKey} 的数组元素必须是 SubResource(...)：{trimmed}"));
				return default;
			}
		}
		return ParsedValue.OfRefArray(ids);
	}

	/// <summary>解析 SubResource("id") / ExtResource("id") 形式的引用；失败返回 null。</summary>
	private static string? TryParseRef(string raw, string prefix)
	{
		if (!raw.StartsWith(prefix, StringComparison.Ordinal)) return null;
		var rest = raw.Substring(prefix.Length).TrimStart();
		if (rest.Length < 3 || rest[0] != '(') return null;
		int close = FindMatching(rest, 0, '(', ')');
		if (close != rest.Length - 1) return null;
		var arg = rest.Substring(1, close - 1).Trim();
		if (arg.Length < 2 || arg[0] != '"') return null;
		int end = FindQuotedEnd(arg, 0);
		if (end != arg.Length - 1) return null;
		return Unescape(arg.Substring(1, end - 1));
	}

	private static Dictionary<string, string> ParseHeaderAttributes(string inner, string file, int line,
		List<DefinitionError> errors)
	{
		var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
		int i = 0;
		while (true)
		{
			while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;
			if (i >= inner.Length) break;

			int keyStart = i;
			while (i < inner.Length && inner[i] != '=' && !char.IsWhiteSpace(inner[i])) i++;
			var key = inner.Substring(keyStart, i - keyStart);
			while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;
			if (i >= inner.Length || inner[i] != '=')
			{
				errors.Add(new DefinitionError(file, line, $"段头属性缺少 =：{inner}"));
				break;
			}
			i++;
			while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;

			string value;
			if (i < inner.Length && inner[i] == '"')
			{
				int end = FindQuotedEnd(inner, i);
				if (end < 0)
				{
					errors.Add(new DefinitionError(file, line, $"段头字符串缺少结束引号：{inner}"));
					break;
				}
				value = Unescape(inner.Substring(i + 1, end - i - 1));
				i = end + 1;
			}
			else
			{
				int vStart = i;
				while (i < inner.Length && !char.IsWhiteSpace(inner[i])) i++;
				value = inner.Substring(vStart, i - vStart);
			}
			attrs[key] = value;
		}
		return attrs;
	}

	/// <summary>从 start 处（应为引号）找到未转义结束引号的下标；失败返回 -1。</summary>
	private static int FindQuotedEnd(string text, int start)
	{
		for (int i = start + 1; i < text.Length; i++)
		{
			if (text[i] == '\\') { i++; continue; }
			if (text[i] == '"') return i;
		}
		return -1;
	}

	/// <summary>从 openPos 处的 open 括号找到匹配的 close 括号（忽略字符串内括号）。</summary>
	private static int FindMatching(string text, int openPos, char open, char close)
	{
		int depth = 0;
		for (int i = openPos; i < text.Length; i++)
		{
			if (text[i] == '"')
			{
				int end = FindQuotedEnd(text, i);
				if (end < 0) return -1;
				i = end;
				continue;
			}
			if (text[i] == open) depth++;
			else if (text[i] == close)
			{
				depth--;
				if (depth == 0) return i;
			}
		}
		return -1;
	}

	/// <summary>按顶层逗号切分（忽略字符串与嵌套括号内的逗号）。</summary>
	private static List<string> SplitTopLevel(string text, char separator)
	{
		var parts = new List<string>();
		int depth = 0;
		int start = 0;
		for (int i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c == '"')
			{
				int end = FindQuotedEnd(text, i);
				if (end < 0) break;
				i = end;
				continue;
			}
			if (c == '(' || c == '[') depth++;
			else if (c == ')' || c == ']') depth--;
			else if (c == separator && depth == 0)
			{
				parts.Add(text.Substring(start, i - start));
				start = i + 1;
			}
		}
		parts.Add(text.Substring(start));
		return parts;
	}

	private static string Unescape(string value)
	{
		if (value.IndexOf('\\') < 0) return value;
		var sb = new System.Text.StringBuilder(value.Length);
		for (int i = 0; i < value.Length; i++)
		{
			if (value[i] == '\\' && i + 1 < value.Length)
			{
				i++;
				switch (value[i])
				{
					case 'n': sb.Append('\n'); break;
					case 't': sb.Append('\t'); break;
					case 'r': sb.Append('\r'); break;
					case '"': sb.Append('"'); break;
					case '\\': sb.Append('\\'); break;
					default: sb.Append('\\').Append(value[i]); break;
				}
			}
			else
			{
				sb.Append(value[i]);
			}
		}
		return sb.ToString();
	}

	private static int IndexOfWhiteSpace(string text)
	{
		for (int i = 0; i < text.Length; i++)
		{
			if (char.IsWhiteSpace(text[i])) return i;
		}
		return -1;
	}
}
