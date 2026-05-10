# 10 - Code Style

> 本文定义后续 Agent 修改 C# / Unity 代码时必须严格遵守的默认代码规范。除非本文件明确允许例外，否则本项目规范优先于通用 Microsoft C# Coding Conventions。

## 1. 默认基线

- C# 代码默认以 Unity 官方的 C# 命名与格式建议为基线，并参考 Microsoft C# Coding Conventions。
- 参考来源：
  - Unity: `https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity`
  - Unity: `https://unity.com/how-to/formatting-best-practices-c-scripting-unity`
  - Microsoft: `https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`
- Unity 生命周期函数和 Unity API 名称保持 Unity 原生写法，例如 `Awake`、`Start`、`Update`、`OnValidate`。
- 如果现有同目录代码与本文冲突，修改该文件时必须向本文收敛；不要继续扩大旧写法。
- 不为了风格一次性重排无关文件。修改业务逻辑时，只格式化本次实际触碰的代码区域。

## 2. 命名

- 类型、方法、属性、事件使用 `PascalCase`。
- 局部变量和参数使用 `camelCase`。
- 所有非静态字段必须使用 `m_camelCase`，包括 `[SerializeField] private` 字段和运行时缓存字段。
- 所有常量和静态只读数据字段必须使用 `k_PascalCase`，例如 `private const string k_MenuPath`、`public static readonly GameplayTag k_Damage`。
- 普通静态方法和静态属性仍使用 `PascalCase`，不要加 `k_`。
- 枚举类型必须使用 `E` 前缀和 `PascalCase`，例如 `EFaction`、`ESkillTargetType`。枚举值使用 `PascalCase`，不加前缀。
- 接口类型必须使用 `I` 前缀和 `PascalCase`，例如 `ICombatUnit`。
- 不使用匈牙利命名法，不用类型缩写作为前缀。
- Serialized 字段仍按字段规则命名，例如 `[SerializeField] private float m_moveSpeed;`。
- 不为 Inspector 公开 `public` 字段；使用 `[SerializeField] private` 加只读/受控属性暴露。
- ScriptableObject 资源字段命名应表达策划含义，避免只写 `value`、`data`、`config` 这类含糊名称。

## 3. 格式

- 使用 4 个空格缩进，不使用 tab。
- 花括号使用 C# 常规 Allman 风格，左花括号独占一行。
- 每行只放一条语句。
- 优先使用清晰的早返回减少嵌套。
- 简单表达式可以用表达式体成员；一旦逻辑包含分支、副作用或调试输出，改用普通块。
- `using` 按系统命名空间、第三方/Unity、本项目命名空间分组；删除未使用 `using`。
- 机械重命名后必须检查命名参数、泛型类型名、字符串中的 SerializedProperty 路径，避免把类型名或参数名误改成字段名。

## 4. Unity 约定

- Inspector 暴露字段优先使用 `[SerializeField] private`，不要为了 Inspector 直接公开字段。
- 需要只读访问时提供属性，例如 `public int CurrentAP => m_currentAP;`。

- 运行时状态不要写回 ScriptableObject 配置资产；每单位、每回合、冷却、临时 Buff 等状态必须放在运行时对象中。
- 临时数值、占位行为或原型实现必须加 `TODO`，并引用相关 `Docs/` 章节或 `Q-XXXX`。
- 不手工编辑 `.unity`、`.prefab`、`.meta` 文件文本；需要场景或资产操作时使用 Unity Skills / Unity Editor。

## 5. 注释与文档

- 代码应通过命名和结构自解释；不要给显而易见的赋值、判断、循环写注释。
- 复杂规则、设计妥协、临时假设、跨系统约束可以写简短注释，并引用对应文档。
- 公共接口、设计-facing 字段、ScriptableObject 配置字段发生变化时，同步更新对应 `Docs/0x_*.md`。

## 6. 项目特定约束

- 新增玩法语义判断前，优先检查是否应使用 GameplayTag；不要散落硬编码字符串前缀判断。
- 技能、AI、Effect、Status 等系统优先保持数据驱动，避免把策划参数写死在控制器里。
- 新增或修改 C# 代码时，先检查本文件；后续 Agent 不得回退到 `_camelCase`、无 `E` 前缀枚举、或 public Inspector 字段。
