# 05 - 技能规格 (Skill Spec)

> 技能是战斗中除"基础移动"之外的所有主动行为。每个技能自定义 AP 消耗、目标、范围、效果。

## 1. 技能字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 唯一 ID |
| `displayName` | string | 显示名 |
| `description` | string | 给策划/调试 UI 使用的简短说明 |
| `apCost` | int | 行动点消耗（**由技能自己决定**） |
| `cooldown` | int | 冷却单位（0 = 无）。战斗中按回合计；探索实时状态下按 `cooldown × 6` 秒计 |
| `targetType` | enum | `Self` / `SingleEnemy` / `SingleAlly` / `GroundPoint` / `Area` |
| `range` | float | 施放距离（米） |
| `areaShape` | enum | `Single` / `Circle(r)` / `Cone(angle, r)` / `Line(width, length)` |
| `requiresLineOfSight` | bool | 是否需要视线检查（MVP 可先不实现，保留字段） |
| `endsTurnAfterCast` | bool | 使用后是否强制结束该单位回合；默认 false |
| `effects` | Effect[] | 命中后产生的效果列表（伤害、治疗、buff …） |
| `aiTags` | enum[] | AI 决策标签，例如 `Damage` / `Heal` / `Buff` / `Debuff` / `Control` / `Protect` / `Area` |
| `aiBaseWeight` | float | AI 默认评分权重；只影响敌方决策，不影响玩家使用 |
| `animation` | AnimationClipRef | 表现层 |
| `vfx` | GameObject | 表现层 |

## 2. 效果 (Effect) 结构

```csharp
public abstract class Effect : ScriptableObject {
    public abstract void Apply(ICombatUnit caster, ICombatUnit target);
}
```

`SkillDefinition` 可以持有多个 `Effect`，并按配置顺序依次执行。技能负责描述消耗、目标、范围、冷却和 AI 标签；Effect 负责实际产生结果，例如伤害、治疗、添加状态或位移。

Effect 使用独立 ScriptableObject 资产保存静态配置，存放在 `Assets/Data/Effects/`。`SkillDefinition.effects[]` 引用这些独立 Effect 资产；同一个 Effect 资产可以被多个技能复用。运行时状态、冷却、临时禁用、施放次数等不得写回 Effect 或 SkillDefinition 资产。

每个 Effect 必须配置至少一个 GameplayTag，用于标记自身效果类型。这个 Tag 用于查询、免疫判断、AI 决策、debug 输出和后续编辑器筛选。没有 Tag 的 Effect 视为非法配置。

预期子类（占位）：
- `DamageEffect`（物理 / 魔法 / 真实）
- `HealEffect`
- `StatusEffect`（buff/debuff，附带持续轮数）
- `MoveEffect`（推 / 拉 / 传送）

效果执行规则：

1. 技能先检查可用性：AP、冷却、目标阵营、范围、路径/视线条件。
2. 通过检查后消耗 AP，并记录冷却。
3. 对命中的目标依次执行 `effects`。
4. 若任一效果触发死亡、状态变化或位移，由战斗系统广播状态变化。

同一个技能可以包含多个效果，例如“造成伤害 + 降低防御 1 回合”。

## 3. 状态 (Status) 结构

状态用于表达 buff / debuff，不直接等同于技能。

建议字段：

| 字段 | 说明 |
|---|---|
| `id` | 唯一 ID |
| `displayName` | 显示名 |
| `durationRounds` | 持续轮数 |
| `stackRule` | `RefreshDuration` / `StackValue` / `IgnoreIfExists` |
| `statModifiers` | 对 Attack / Defense / MoveSpeed / MaxAP 等的临时修正 |
| `tags` | `Buff` / `Debuff` / `Control` / `DamageOverTime` / `Protect` |

MVP 阶段优先支持数值型状态，例如攻击提高、防御提高、受到持续伤害。眩晕、魅惑、改先攻等强控制先不做，避免破坏回合系统。

### 3.1 Status 与 Effect 的关系

- `Effect` 是一次性执行的结果单元，例如造成伤害、恢复 HP、添加状态。
- `Status` 是挂在单位身上的持续性运行时状态，例如中毒、持续恢复、防御提高。
- 技能本身不直接实现“每回合毒伤”或“每回合恢复”；技能通过 `AddStatusEffect` 添加一个 Status。
- Status 可以在特定时机触发自己的 Effect，例如：
  - 回合开始：触发 `DamageEffect`，表现为毒伤。
  - 回合开始：触发 `HealEffect`，表现为持续恢复。
  - 状态存在期间：提供 `statModifiers`，表现为 buff/debuff。
- 这样持续恢复、持续毒伤、持续属性修正都使用同一套 Status 概念，避免把持续逻辑分散到多个技能里。

## 4. 技能分类

技能按功能标签分类，而不是绑定固定职业树：

| 类型 | 说明 | AI 用途 |
|---|---|---|
| `Damage` | 单体或范围伤害 | 进攻型敌人优先 |
| `Heal` | 回复友方 HP | 治疗型敌人在友方低血量时优先 |
| `Buff` | 提高友方威胁或生存 | 支援型敌人优先 |
| `Debuff` | 降低玩家伤害、防御或移动 | 控制/支援敌人使用 |
| `Control` | 限制行动或改变站位 | 后续扩展，MVP 谨慎使用 |
| `Protect` | 护盾、防御提高、替关键单位承压 | Boss 战或支援怪使用 |
| `Mobility` | 位移、冲刺、拉近/拉远 | 依赖地形时使用 |

## 4.5 游戏 Tag 系统

Tag 是贯穿整个游戏的通用条件语言，用于连接技能、Effect、Status、AI 决策、关卡脚本和事件通知。完整规格见 `09_GAMEPLAY_TAG_SPEC.md`。Tag 是后续系统设计的第一原则：任何新的条件、事件或跨系统语义，第一时间都要考虑是否能用 Tag 表达。

Tag 格式：
- 使用点分层级，例如 `Element.Fire.Burning`、`State.Poisoned`、`Faction.Undead`。
- Tag 可以完整匹配，也可以按层级前缀匹配。
- 例如 `Element` 可以匹配所有元素类 Tag，`Element.Fire` 可以匹配所有火焰类 Tag，`Element.Fire.Burning` 只匹配燃烧本身。

使用场景：
- 技能可以给目标添加 Tag，例如 `State.Burning`、`State.Marked`。
- 技能或 Effect 可以移除 Tag，例如移除 `State.Burning`。
- 技能可以检查目标 Tag 来决定是否生效，例如拥有 `State.Immune.Fire` 时，火焰技能无效。
- Status 和 Effect 应尽量关联一个或多个 Tag，便于查询角色身上有哪些效果或状态。
- Effect 必须至少配置一个 Tag；Status 也应配置表示自身状态的 Tag。
- AI 可以读取目标 Tag 进行决策，例如优先攻击 `State.Marked` 的目标，或避免对 `State.Immune.Poison` 的目标使用毒系技能。
- 关卡脚本和 Scripted Tactic 可以通过 Tag 判断条件。

实现约定：
- Tag 本身是数据，不应写死在某个技能逻辑里。
- 第一阶段可用字符串表达 Tag，但应集中封装匹配逻辑，避免各系统各自写 `StartsWith`。
- 代码中任何传递游戏语义事件的操作，都应优先携带 Tag / TagSet。
- 新增布尔字段、专用 enum、专用事件类型前，应先判断是否能用 Tag 表达。
- Tag 不替代强类型数值和对象引用；例如伤害值、HP、AP、世界坐标仍然是强类型数据，Tag 用来标记它们的玩法语义。
- 匹配模式至少支持：
  - Exact：完整匹配。
  - Prefix：层级前缀匹配。
- 单位运行时需要能查询当前 Tag，并能追踪 Tag 来源，例如来自 Status、装备、地形、剧情或临时战术。
- 当 Tag 来自 Status 时，Status 结束应自动移除对应 Tag。

## 5. 数据载体

```csharp
[CreateAssetMenu(menuName = "Chess/SkillDefinition")]
public class SkillDefinition : ScriptableObject { /* 见 §1 字段 */ }
```
存放位置：`Assets/Data/Skills/`。

`SkillDefinition` 只保存静态配置。每个单位的当前冷却、已施放次数、临时禁用等运行时状态应放在单位运行时组件中，不写回 ScriptableObject。

Effect 配置资产存放在 `Assets/Data/Effects/`。Effect 资产同样只保存静态配置，允许多个技能共享，例如多个物理攻击技能复用同一个固定伤害或同类型伤害 Effect 模板。

### 5.1 技能执行器与目标

- `SkillExecutor` 是运行时组件，挂在能够释放技能或能够被技能影响的对象上，例如玩家、敌人、木桶、墙壁或后续特殊机关。
- 玩家输入、敌方 AI、脚本化战术都应向对象自身的 `SkillExecutor` 提交施放请求，而不是各自实现技能流程。
- 技能目标不应限定为敌人或玩家。木桶、墙壁、机关等可交互物体也可以成为技能目标。
- 不额外引入 `ISkillTarget` / `SkillTarget`。目标合法性统一由目标对象自身的 `SkillExecutor` 判断。
- 只要对象挂载 `SkillExecutor`，它就默认进入技能系统，可以被技能查询和影响。
- `SkillExecutor` 应提供最小配置来声明该对象承受技能效果的能力，例如：
  - 是否可被伤害。
  - 是否可被治疗。
  - 是否可添加 Status。
  - 是否属于玩家、敌方、中立或环境目标。
  - 是否阻挡、可破坏或可互动。
- 第一阶段可以先让玩家和敌人的 `SkillExecutor` 承担释放与受击能力；后续扩展木桶、墙壁等环境目标时，只需要给对象挂载并配置 `SkillExecutor`。

## 6. 释放流程

玩家和 AI 共用同一套技能执行流程：

1. 选择施法者。
2. 枚举可选技能。
3. 选择目标或地面点。
4. 校验 AP、冷却、目标类型、范围、路径/视线。
5. 如果目标不在范围内，允许先移动到可施放位置，再释放技能。
6. 扣除移动 AP 与技能 AP。
7. 执行效果并更新状态/冷却。
8. 若技能配置 `endsTurnAfterCast = true`，结束该单位回合。

技能 AP 消耗永远来自技能自身配置，不使用全局固定表。

### 6.1 点击敌方单位与基础攻击

鼠标直接点击敌方单位时，本质上是请求当前角色释放自己的 `basic_attack`：

1. 读取当前角色持有的 `basic_attack` 技能资产。
2. 使用 `basic_attack.range` 判断是否已经在攻击距离内。
3. 若已经在范围内，并且 AP 足够支付 `basic_attack.apCost`，直接释放技能。
4. 若不在范围内，执行器计算能否通过 NavMesh 移动到 `basic_attack.range` 内。
5. 若 AP 足够支付移动 AP + `basic_attack.apCost`，则移动后释放。
6. 若 AP 不足以抵达攻击距离，则不释放攻击，只用剩余可移动 AP 沿路径向目标靠近。

这个规则也应被敌方 AI 复用：AI 选择 `basic_attack` 候选时，同样依据技能范围、移动 AP 与技能 AP 判断本回合能否攻击；不能攻击时退化为向目标靠近。

### 6.2 冷却规则

- 技能不会消耗 AP 之外的其他资源。
- 特定技能可以配置 `cooldown`，用于避免强力技能被连续使用。
- `cooldown` 不允许小数。
- 战斗中，`cooldown = 1` 表示使用后需要等待 1 个回合单位的冷却。
- 探索 / 非战斗实时状态下，世界实时更新，技能冷却按秒刷新。
- 暂定 `1 回合 = 6 秒`，因此探索状态下 `cooldown = 1` 等价于冷却 6 秒。
- 运行时需要维护每个单位自己的技能冷却状态；冷却状态不能写回 `SkillDefinition` 资产。
- 同一个技能配置在战斗和探索中共用同一个 `cooldown` 整数，只是刷新方式不同。

## 7. MVP 技能池草案

第一阶段不要做技能树，只做能验证 AI 配合的少量技能。

| ID | 名称 | AP | 范围 | 类型 | 效果 |
|---|---|---:|---:|---|---|
| `basic_attack` | 基础攻击 | 1 | 1.5m | `Damage` | 对单体敌人造成固定伤害 |
| `guarding_shout` | 战吼 | 2 | 6m | `Buff` / `Protect` | 友方单体攻击或防御提高 1-2 回合 |
| `minor_heal` | 小治疗 | 2 | 6m | `Heal` | 回复友方单体少量 HP |
| `power_strike` | 重击 | 3 | 1.5m | `Damage` | 造成高于基础攻击的单体伤害 |
| `crippling_hex` | 虚弱诅咒 | 2 | 6m | `Debuff` | 降低玩家攻击或移动 1 回合 |

这些技能足够验证：

- 进攻型敌人会追击和攻击。
- 支援型敌人会在开场或关键时刻 buff 友军。
- 治疗型敌人会在友方低血量时优先治疗。
- AI 能在进攻、治疗、支援之间做评分选择。

## 8. 技能列表

正式技能列表需要在具体角色/怪物设计时继续填入：

| ID | 名称 | AP | 范围 | 效果 | 状态 |
|---|---|---:|---:|---|---|
| _待补_ | | | | | |

## 9. AI 使用规则

- AI 不直接理解技能效果代码，而是优先读取 `aiTags`、`targetType`、`range`、`apCost`、`cooldown` 与 `aiBaseWeight`。
- 治疗、buff、保护、控制等行为需要通过 `aiTags` 暴露给 `AIProfile`，否则 AI 只能把技能当作普通可用技能处理。
- 若技能有复杂条件（例如只能对中毒目标使用），应在技能可用性检查中返回不可用原因，供 AI 过滤候选行动和 debug 输出。
- `aiBaseWeight` 只提供技能自身的默认吸引力；最终选择仍由 `AIProfile`、目标状态、距离、AP 效率和脚本化战术共同决定。

## 10. 第一阶段实现范围

第一版技能系统只实现：

1. `SkillDefinition` 数据结构。
2. `DamageEffect`，用于完成 `basic_attack` 纵切片。
3. `HealEffect` 与 `AddStatusEffect` 的扩展位置，暂不实现完整行为。
4. 基础冷却与 AP 校验。
5. 单体目标和自身目标。
6. 敌方 AI 能读取 `aiTags` 与 `aiBaseWeight`。

暂不实现：

- 技能树。
- 装备提供技能。
- 复杂连锁反应。
- 大量 VFX / 动画表现。
- 会改变先攻顺序的状态。
- 强控制类状态（眩晕、魅惑等）。

## 11. 待决问题（参见 `OPEN_QUESTIONS.md`）

- 技能是否消耗"法力值"等第二资源，还是只消耗 AP？
- 技能命中是否需要骰子检定（D&D 式 attack roll）？
- 暴击 / 闪避机制？
