## 1. 项目概况

- 引擎：Godot 4.7.2 stable mono，C# .NET 8，Forward+，Jolt 物理。
- Godot 可执行文件：
  `D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
- CLI 编译：`dotnet build MySimCity.csproj -c Debug`。Godot.NET.Sdk 会把输出重定向到
  `.godot/mono/temp/bin/Debug/`——这就是编辑器实际加载程序集的目录，CLI 构建即生效。
- 主场景 `Scene/world.tscn`；Autoload：`GameConfig`、`Inventory`。

## 2. 无头验证方法

```powershell
& 'D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path 'D:\Godot\MySimCity\my-sim-city' --quit
```

- exit 0 且无 ERROR 行 = 主场景（含所有被引用资源）解析/加载正常。.tscn 解析错误会精确报出行号。
- 输出重定向到文件（`*> out.txt`）再读，不要用管道取尾部——重要错误常出现在开头，管道会截掉。
- headless 用 dummy 渲染器，不会做 shader GPU 编译：shader 语法错误只能靠编辑器运行确认。

## 3. Godot 注意事项

### 3.1 手写 .tscn
- Node 类型不能当 sub_resource，只有 Resource 派生类型能进 `[sub_resource]`；sub_resource 必须先于引用它的节点声明。
