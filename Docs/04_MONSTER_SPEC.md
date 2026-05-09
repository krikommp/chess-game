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

## 3. AI 行为框架

敌方 AI 采用 **轻量 Utility AI + 脚本化战术覆盖**。目标是让敌人能按职业/技能类型做出可解释的配合，同时允许策划在特定战斗中配置开场行为或条件触发行为。

### 3.1 设计目标

- AI 不是完全自主系统，关卡可以注入特殊战术，例如开场 buff、Boss 血量低时治疗、玩家聚集时释放范围技。
- 怪物技能数量较少，主要依靠数值、地形、站位和敌我配合制造决策压力。
- 敌人按 `AIProfile` 表现出不同倾向：进攻、支援、治疗、控制、保护关键单位等。
- 决策必须可调试：每次选择行动应能输出候选行动、评分和最终原因。

### 3.2 决策优先级

每个敌方单位回合按以下顺序决策：

1. **Scripted Tactic**：检查本场战斗/本单位配置的强制或高优先级战术。
2. **Utility Evaluation**：枚举可行动作并评分，选择最高分。
3. **Fallback**：若没有可执行动作，移动接近最合理目标；仍无法行动则结束回合。

脚本化战术只用于明确的战斗设计意图，通用 AI 行为仍由 Utility Evaluation 负责。

### 3.3 AIProfile（怪物行为档案）

`AIProfile` 是 ScriptableObject，挂在 `MonsterDefinition` 上。

建议字段：

| 字段 | 说明 |
|---|---|
| `role` | `Aggressive` / `Support` / `Healer` / `Controller` / `Defender` 等倾向标签 |
| `skillWeights` | 每个技能的基础权重或行为标签权重 |
| `skillTagWeights` | 根据技能 `aiTags` 或 GameplayTag 加权 |
| `targetTagWeights` | 根据目标身上的 GameplayTag 加权 |
| `statusTagWeights` | 根据目标或友方身上的 Status Tag 加权 |
| `targetPreference` | 优先最近、最低 HP、最高威胁、正在濒死、关键友军等 |
| `healHpThreshold` | 低于多少生命比例时治疗行为加权 |
| `buffOpeningTurns` | 前几轮更倾向于 buff / 站位准备 |
| `protectImportantAllies` | 是否优先保护高威胁友军或 Boss |
| `aggression` | 进攻倾向，影响伤害技能/靠近玩家的评分 |
| `supportBias` | 支援倾向，影响 buff / 治疗 / 保护的评分 |

`role` 只表示默认倾向，不应写成硬编码分支。AIProfile 必须保持数据驱动，通过权重与 GameplayTag 系统影响技能、目标、状态和战术条件的评分。

### 3.4 Scripted Tactic（脚本化战术）

`ScriptedTactic` 用于关卡或怪物的特殊决策覆盖，不替代通用 AI。

触发条件示例：

- `RoundIndex == 1`：开场给友军施加攻击 buff。
- `SelfHpPercent < 0.5`：Boss 转入自保或治疗逻辑。
- `AnyAllyHpPercent < 0.35`：治疗者优先治疗。
- `PlayersClustered(radius, count)`：释放范围技能。
- `ImportantAllyThreatened`：支援单位给关键敌方单位加防御/护盾。

执行结果示例：

- 使用指定技能。
- 强制选择指定目标类型。
- 临时提高某类行为权重。
- 禁止某类行为若干轮。

### 3.5 Utility Evaluation（通用评分）

AI 每次行动时生成候选 `AIActionCandidate`：

- 对敌方玩家使用伤害技能 / 基础攻击。
- 对友方使用治疗、buff、保护技能。
- 移动到技能范围内。
- 防御 / 结束回合（占位）。

候选至少包含：
- 技能。
- 目标。
- 是否需要移动。
- 移动目标点。
- 是否本回合可释放。
- 评分。
- 失败原因。
- Debug reason。

评分建议：

```text
score =
  baseSkillWeight
  + roleBias
  + targetValue
  + hpUrgency
  + apEfficiency
  + distanceScore
  + tacticalBonus
  - riskPenalty
```

示例倾向：

- `Aggressive`：提高伤害、击杀低血量玩家、靠近脆弱目标的评分。
- `Support`：提高给高威胁友军加 buff、保护 Boss、让友方形成集火优势的评分。
- `Healer`：当友方低血量时，治疗评分显著高于进攻；没有治疗目标时退化为支援或攻击。
- `Controller`：优先限制玩家关键角色或对聚集玩家使用范围/控制技能。

第一阶段实现时，AI 决策应拆成：

1. 生成候选：遍历敌人技能列表和挂载 `SkillExecutor` 的可选目标。
2. 过滤候选：检查 AP、冷却、目标类型、阵营、Tag 免疫、范围和路径。
3. 评分候选：读取 `SkillDefinition`、`AIProfile`、GameplayTag、Status Tag、HP、距离和 AP 效率。
4. 选择候选：选择最高分；同分时使用稳定规则，例如更近目标或更低 HP 目标。
5. 执行候选：交给敌人自身的 `SkillExecutor`，由执行器负责移动、扣 AP、应用 Effect 和记录冷却。
6. Debug 输出：输出候选列表、失败原因、评分组成和最终选择原因。日志应尽量详细，方便后续 Agent 调试技能配置、Tag 权重、AIProfile、AP、冷却和 NavMesh 路径问题。

### 3.6 执行层边界

AI 决策层只产出 `AICommand`，不直接移动或扣 AP。

执行层负责：

- 使用 NavMesh 计算能否进入技能/攻击范围。
- 消耗移动 AP 与技能 AP。
- 等待移动结束后释放技能。
- 若执行中目标死亡或路径失效，重新评估一次；仍失败则结束回合。

### 3.7 第一阶段实现范围

第一版只做最小闭环：

1. `EnemyController` 拥有 AP、MoveSpeed、NavMeshAgent，并能移动。（已实现）
2. 敌方基础攻击复用玩家基础攻击规则：移动到范围内，消耗移动 AP + 攻击 AP，造成固定伤害；若本回合无法攻击，则向最近玩家移动到最大可达点，并通过软占位采样避免多个敌人选择同一终点。（MVP 已实现）
3. `AIProfile` 支持 3 个角色倾向：`Aggressive` / `Support` / `Healer`；倾向通过数据权重与 GameplayTag 评分实现，不写成硬编码分支。
4. `ScriptedTactic` 先支持开场第 1 轮提高 buff/支援行为权重，不做复杂图形编辑器。
5. Debug 输出本次 AI 选择原因。（MVP 已输出移动/攻击/失败原因）

暂不引入 Behavior Tree / GOAP 插件。若后续策划需要可视化节点编辑，再评估 Behavior Designer 或 NodeCanvas。

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
