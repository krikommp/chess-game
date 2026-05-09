# 09 - GameplayTag 规格

> GameplayTag 是贯穿整个游戏的第一层语义系统。后续技能、Effect、Status、AI、交互物、关卡条件、事件通知和 debug 输出，都应优先考虑是否能用 Tag 表达。

## 1. 设计目标

- 用统一的 Tag 表达玩法语义，避免每个系统各自创建专用 enum、bool 或字符串判断。
- 支持数据驱动：策划和 Agent 可以通过配置修改技能、状态、AI 和交互条件。
- 支持层级匹配：既能精确匹配 `Element.Fire.Burning`，也能用 `Element.Fire` 匹配所有火焰相关 Tag。
- 支持编辑器管理：统一创建、搜索、校验、引用和打开 Tag 配置。
- 支持代码使用：运行时代码可以安全地添加、移除、查询和匹配 Tag。

Tag 不替代强类型数据。HP、AP、坐标、对象引用、伤害数值仍应使用强类型字段；Tag 用来表达这些数据的玩法含义。

## 2. 命名规则

Tag 使用点分层级：

```text
Root.Category.Name.Detail
```

示例：

```text
Element.Fire
Element.Fire.Burning
Element.Fire.Resistance
State.Poisoned
State.Guarding
Effect.Damage.Physical
Effect.Heal.Direct
Event.Skill.Cast
Faction.Player
Faction.Enemy
Target.Environment.Breakable
AI.Role.Healer
AI.Target.Marked
```

命名约定：

- 使用 PascalCase 分段。
- 不使用空格。
- 不使用中文作为内部 Tag 名称；显示名可以中文。
- Root 段应代表大类，例如 `Element`、`State`、`Effect`、`Event`、`Faction`、`Target`、`AI`。
- 不在代码中散落硬编码字符串；代码可通过常量、配置资产或 Tag 引用访问。

## 3. 匹配规则

最小支持两种匹配模式：

| 模式 | 说明 | 示例 |
|---|---|---|
| `Exact` | 完整匹配 | `Element.Fire.Burning` 只匹配 `Element.Fire.Burning` |
| `Prefix` | 层级前缀匹配 | `Element.Fire` 匹配 `Element.Fire.Burning` 与 `Element.Fire.Resistance` |

Prefix 匹配必须按层级边界匹配：

- `Element.Fire` 可以匹配 `Element.Fire.Burning`。
- `Element.Fire` 不应匹配 `Element.Firestorm`。

后续可扩展：

- `Any`：TagSet 中任意一个匹配即可。
- `All`：TagSet 中所有要求都必须满足。
- `None`：目标不能拥有指定 Tag。

## 4. 核心数据结构

### 4.1 GameplayTag

轻量值对象，表示单个 Tag。

建议字段：

```csharp
[Serializable]
public readonly struct GameplayTag {
    public string Value { get; }
}
```

约束：

- 空字符串非法。
- 前后空格非法。
- 连续点号非法，例如 `State..Poisoned`。
- 以点号开头或结尾非法。

### 4.2 GameplayTagSet

运行时容器，表示一个对象当前拥有的 Tag 集合。

建议能力：

```csharp
public sealed class GameplayTagSet {
    bool Has(GameplayTag tag, TagMatchMode mode = TagMatchMode.Exact);
    bool HasAny(IEnumerable<GameplayTag> tags, TagMatchMode mode);
    bool HasAll(IEnumerable<GameplayTag> tags, TagMatchMode mode);
    void Add(GameplayTag tag, object source = null);
    void Remove(GameplayTag tag, object source = null);
}
```

Tag 来源需要可追踪：

- Status
- Skill
- Effect
- Equipment
- Terrain
- ScriptedTactic
- Debug / 临时测试

当来源结束时，应能移除它带来的 Tag。例如 Status 结束时，自动移除该 Status 添加的 Tag。

### 4.3 TagQuery

用于技能条件、AI 条件、关卡条件。

建议字段：

```text
requiredAll
requiredAny
blockedAny
matchMode
```

示例：

```text
requiredAny = [State.Burning, State.Oiled]
blockedAny = [State.Immune.Fire]
matchMode = Prefix
```

## 5. 配置资产

### 5.1 TagRegistry

`TagRegistry` 是全局 Tag 数据库，用于编辑器、校验、自动补全和文档化。

MVP 临时方案可以先用字符串 Tag + 集中匹配工具；但最终应建立 `TagRegistry`。

建议路径：

```text
Assets/Data/Tags/GameplayTagRegistry.asset
```

建议字段：

| 字段 | 说明 |
|---|---|
| `tags` | 所有已注册 Tag |
| `displayName` | 中文显示名 |
| `description` | 用途说明 |
| `category` | 编辑器分组 |
| `deprecated` | 是否废弃 |

### 5.2 Tag 引用方式

配置资产中不要直接裸写字符串字段，最终应使用可序列化 Tag 引用：

```csharp
[Serializable]
public struct GameplayTagRef {
    public string value;
}
```

第一阶段可以使用字符串，但必须集中校验。

## 6. 编辑器工具

后续 Combat Config 编辑器应包含 Tag 管理页。

功能：

- 创建 Tag。
- 搜索 Tag。
- 按 Root 分组浏览。
- 自动补全。
- 显示引用位置。
- 检查未注册 Tag。
- 检查没有 Tag 的 Effect。
- 检查废弃 Tag。
- 一键打开引用该 Tag 的技能、Effect、Status、AIProfile。

建议菜单：

```text
MiniChess/Combat Config
```

Tag 页签：

```text
Tags
Skills
Effects
Statuses
AI Profiles
Validation
```

## 7. 与技能系统的关系

- `SkillDefinition` 可以拥有 Tag，用于描述技能语义。
- 每个 `EffectDefinition` 必须至少拥有一个 Tag。
- `StatusDefinition` 必须拥有表示自身状态的 Tag。
- 技能可通过 `TagQuery` 判断目标是否可受影响。
- Effect 可添加或移除目标 Tag。
- Status 结束时应移除由该 Status 添加的 Tag。

示例：

```text
basic_attack
skillTags:
  Skill.Attack.Basic
  Skill.Target.Single
effects:
  DamageEffect
    tags:
      Effect.Damage.Physical
      Effect.Damage.BasicAttack
```

## 8. 与 AI 的关系

AIProfile 不应只依赖硬编码 role。

AI 应读取：

- 技能 Tag
- Effect Tag
- 目标当前 Tag
- Status Tag
- 环境目标 Tag
- 事件 Tag

AIProfile 可配置：

```text
skillTagWeights
targetTagWeights
statusTagWeights
blockedTargetTags
preferredTargetTags
```

示例：

```text
Healer Profile
skillTagWeights:
  Skill.Heal: +50
targetTagWeights:
  State.LowHP: +80
  State.Marked: -20
```

## 9. 与事件系统的关系

代码中传递游戏语义事件时，应优先携带 Tag。

示例事件 Tag：

```text
Event.Skill.Cast
Event.Skill.Hit
Event.Effect.Applied
Event.Status.Added
Event.Status.Removed
Event.Unit.Damaged
Event.Unit.Healed
Event.Object.Destroyed
```

事件仍可携带强类型数据：

```text
tags: [Event.Unit.Damaged, Effect.Damage.Physical]
source: caster object
target: target object
amount: 20
position: world position
```

Tag 表达事件语义，强类型字段表达事件数据。

## 10. 与交互物的关系

只要对象挂载 `SkillExecutor`，它就进入技能系统。

交互物可通过 Tag 表达能力：

```text
Target.Environment
Target.Environment.Breakable
Target.Environment.Explosive
Target.Environment.Cover
```

示例：

- 木桶：`Target.Environment.Breakable`、`Target.Environment.Explosive`
- 墙壁：`Target.Environment.Breakable`、`Target.Environment.Cover`
- 机关：`Target.Environment.Interactable`

## 11. 第一阶段实现范围

第一阶段必须优先实现 Tag 最小闭环。后续 `SkillDefinition`、`Effect`、`SkillExecutor`、`AIProfile`、AI 候选评分和事件 debug 都应建立在这个闭环之上。

1. `GameplayTag`。
2. `GameplayTagSet`。
3. Exact / Prefix 匹配。
4. Tag 来源追踪。
5. 基础配置校验。
6. `SkillExecutor` 或单位运行时组件可以持有当前对象的运行时 TagSet。
7. 事件/debug 输出可以携带 Tag / TagSet。

Tag 最小闭环完成后，再进入：

1. `EffectDefinition` 必须配置 Tag。
2. `basic_attack` 的 `DamageEffect` 带 Tag。
3. AI debug 输出候选技能/目标相关 Tag。

暂不实现：

- 完整 TagRegistry 编辑器。
- 自动补全。
- 引用反查。
- 废弃 Tag 迁移。
- 复杂 TagQuery 图形编辑器。

## 12. 新问题

- TagRegistry 何时从临时字符串升级为正式资产？见 `OPEN_QUESTIONS.md` 的 `Q-0026`。
- Tag 命名空间是否需要锁定 Root 白名单？
- 是否允许运行时动态创建未注册 Tag？
- Tag 来源追踪是否需要支持多个来源叠加同一个 Tag？
