# 03 - 角色规格 (Character Spec)

> 玩家可操控角色（PC）的属性、职业、成长、装备框架。**具体数值与职业列表均待定**，本文件先锁结构。

## 1. 基础属性框架

| 属性 | 类型 | 用途 | 占位默认 |
|---|---|---|---|
| `MaxHP` | int | 生命上限 | TBD |
| `CurrentHP` | int | 当前生命 | = MaxHP |
| `Initiative` | int | 先攻值（进入战斗锁定，见 `02`） | TBD |
| `MaxAP` | int | 每回合行动点 | 6? |
| `MoveSpeed` | float | 米/AP | TBD |
| `Attack` | int | 基础攻击力 | TBD |
| `Defense` | int | 基础防御 | TBD |

> 是否引入 D&D 式 6 属性（STR/DEX/CON/INT/WIS/CHA）→ **OPEN_QUESTIONS**。

## 2. 职业 (Class)

- 职业列表：**未定**。
- 暂用占位标签：`Warrior` / `Mage` / `Ranger` / `Support`（仅作为开发占位，**非确认设计**）。
- 每个职业应包含：基础属性模板、初始技能列表、可学技能树。

## 3. 成长系统

- 等级机制：**待定**（经验值 / 里程碑式 / 无成长）。
- 升级带来的：HP/属性增长、新技能解锁、AP 上限调整？

## 4. 装备槽（占位）

| 槽位 | 说明 |
|---|---|
| 武器 | 影响 Attack / 技能列表 |
| 护甲 | 影响 Defense / MoveSpeed |
| 饰品 ×N | 各种被动 |

> 装备是否可在战斗中切换？→ OPEN_QUESTIONS。

## 5. 数据载体（程序约定）

```csharp
[CreateAssetMenu(menuName = "Chess/CharacterDefinition")]
public class CharacterDefinition : ScriptableObject {
    public string id;
    public string displayName;
    public ClassType classType;
    public CharacterStats baseStats;
    public List<SkillDefinition> startingSkills;
    public GameObject prefab;  // 视觉模型
}
```
存放位置：`Assets/Data/Characters/`（目录待创建）。

## 6. 队伍

- 队伍人数：不设固定 4 人硬上限；由关卡/场景配置和后续 UI 承载能力决定。
- 队伍组织 UI / 切换队伍 / 离队入队规则：**待定**。
