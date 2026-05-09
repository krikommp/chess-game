# 05 - 技能规格 (Skill Spec)

> 技能是战斗中除"基础移动"之外的所有主动行为。每个技能自定义 AP 消耗、目标、范围、效果。

## 1. 技能字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 唯一 ID |
| `displayName` | string | 显示名 |
| `description` | string | 给策划/调试 UI 使用的简短说明 |
| `apCost` | int | 行动点消耗（**由技能自己决定**） |
| `cooldown` | int | 冷却轮数（0 = 无） |
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

## 5. 数据载体

```csharp
[CreateAssetMenu(menuName = "Chess/SkillDefinition")]
public class SkillDefinition : ScriptableObject { /* 见 §1 字段 */ }
```
存放位置：`Assets/Data/Skills/`。

`SkillDefinition` 只保存静态配置。每个单位的当前冷却、已施放次数、临时禁用等运行时状态应放在单位运行时组件中，不写回 ScriptableObject。

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
2. `DamageEffect` / `HealEffect` / `StatusEffect` 三类效果。
3. 基础冷却与 AP 校验。
4. 单体目标和自身目标。
5. 敌方 AI 能读取 `aiTags` 与 `aiBaseWeight`。

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
