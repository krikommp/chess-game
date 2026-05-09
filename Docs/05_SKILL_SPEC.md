# 05 - 技能规格 (Skill Spec)

> 技能是战斗中除"基础移动"之外的所有主动行为。每个技能自定义 AP 消耗、目标、范围、效果。

## 1. 技能字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 唯一 ID |
| `displayName` | string | 显示名 |
| `apCost` | int | 行动点消耗（**由技能自己决定**） |
| `cooldown` | int | 冷却轮数（0 = 无） |
| `targetType` | enum | `Self` / `SingleEnemy` / `SingleAlly` / `GroundPoint` / `Area` |
| `range` | float | 施放距离（米） |
| `areaShape` | enum | `Single` / `Circle(r)` / `Cone(angle, r)` / `Line(width, length)` |
| `effects` | Effect[] | 命中后产生的效果列表（伤害、治疗、buff …） |
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

## 3. 数据载体

```csharp
[CreateAssetMenu(menuName = "Chess/SkillDefinition")]
public class SkillDefinition : ScriptableObject { /* 见 §1 字段 */ }
```
存放位置：`Assets/Data/Skills/`。

## 4. 技能列表

**当前为空**。需要用户给出具体技能（名称、效果、消耗）后再填入本文件下方表格：

| ID | 名称 | AP | 范围 | 效果 |
|---|---|---|---|---|
| _待补_ | | | | |

## 5. 待决问题（参见 `OPEN_QUESTIONS.md`）

- 技能是否消耗"法力值"等第二资源，还是只消耗 AP？
- 技能命中是否需要骰子检定（D&D 式 attack roll）？
- 暴击 / 闪避机制？
