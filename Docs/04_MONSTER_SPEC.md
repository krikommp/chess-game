# 04 - 怪物规格 (Monster Spec)

> 敌方单位的属性与 AI 行为框架。结构与角色（`03`）大量复用。

## 1. 属性

复用 `03_CHARACTER_SPEC.md` 的 §1 基础属性表（HP / Initiative / MaxAP / MoveSpeed / Attack / Defense）。

额外字段：

| 字段 | 说明 |
|---|---|
| `MonsterType` | 例如 `Beast` / `Humanoid` / `Undead`（占位，**未确认**） |
| `ChallengeRating` | 强度等级 / 推荐等级（待定具体公式） |
| `LootTable` | 战利品配置（→ OPEN_QUESTIONS：是否需要） |
| `AIProfile` | AI 行为档案（见 §3） |

## 2. 行动规则

- 与玩家共享同一套战斗系统（先攻、AP、移动、技能）。
- 怪物的"操控者"是 AI，不是玩家点击。

## 3. AI 行为框架（占位）

最小可行 AI：
1. 回合开始 → 评估目标（最近的敌人 / 最低 HP / 最高威胁）。
2. 选择行动：
   - 在攻击范围内 → 攻击
   - 不在范围 → 移动至范围内（消耗 AP）
   - AP 不足以攻击 → 防御 / 结束
3. 重复直到 AP 耗尽。

实现建议：**Behavior Tree** 或 **GOAP** 二选一 → OPEN_QUESTIONS。先用硬编码 if-else 占位即可。

## 4. 数据载体

```csharp
[CreateAssetMenu(menuName = "Chess/MonsterDefinition")]
public class MonsterDefinition : ScriptableObject {
    public string id;
    public string displayName;
    public CharacterStats baseStats;
    public List<SkillDefinition> skills;
    public AIProfile aiProfile;
    public GameObject prefab;
}
```
存放位置：`Assets/Data/Monsters/`。

## 5. 与现有占位代码的关系

- `Assets/Scripts/EnemySpawner.cs` 是早期占位，**不代表战旗的怪物生成机制**。
- 战旗的怪物应由**关卡数据**预先放置，而不是定时随机刷新。
- 重构 `EnemySpawner` 时间点：当 `MonsterDefinition` + 关卡数据格式确定后。
