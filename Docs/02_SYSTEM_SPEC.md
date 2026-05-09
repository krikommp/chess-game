# 02 - 系统规格 (System Spec)

> 把 `01_GAME_DESIGN_BRIEF.md` 的玩法翻译成可实现的数据/算法。所有公式中带 `?` 的数值都是临时占位，需要在 `OPEN_QUESTIONS.md` 中跟踪。

## 1. 战斗状态机

```
Exploration ──(进入战斗触发)──▶ CombatStart
                                    │
                                    ▼
                              SortByInitiative
                                    │
                                    ▼
                              ┌───▶ UnitTurn ──(单位 AP 用完/结束回合)──┐
                              │                                          │
                              │                                          ▼
                              │                                  AnyUnitLeft?
                              │   ┌────────── Yes ────────────────┘
                              │   ▼
                              │  NextUnit
                              │
                              └─── No ───▶ RoundEnd ──(检查胜负)──▶ NextRound / End
```

## 2. 先攻 (Initiative)

| 字段 | 类型 | 说明 |
|---|---|---|
| `Initiative` | int | 进入战斗时计算并锁定，整场战斗不变 |
| 计算公式（占位） | — | `Initiative = baseInitiative + d20`（参考 D&D；最终公式 TBD） |
| 平局处理 | — | 待定（建议：玩家方优先 / 随机） |

实现要点：
- `CombatManager` 维护 `List<Unit> turnOrder`，按 `Initiative` 降序。
- 索引指针 `currentTurnIndex`，到末尾后归零并 `roundCount++`。

## 3. 行动点 (AP)

| 字段 | 默认 (占位) | 说明 |
|---|---|---|
| `MaxAP` | 6? | 每个单位每回合上限，待平衡 |
| `CurrentAP` | — | 回合开始时 = MaxAP；回合结束清零 |
| `MoveSpeed` | 米/AP | 角色属性，决定 1 AP 能走多远 |
| `BasicAttackCost` | 2 AP? | 基础攻击的固定消耗（占位） |

公式：
- **移动**：`canWalkDistance = CurrentAP × MoveSpeed`（实际走的距离按 NavMesh 路径长度算，不按直线）。
- **攻击/技能**：消耗由技能配置决定，详见 `05_SKILL_SPEC.md`。
- **结束回合**：玩家可主动结束（剩余 AP 不保留 / 是否保留 → OPEN_QUESTIONS）。

## 4. 单位通用接口（程序约定）

```csharp
// 占位伪代码，正式实现见后续 Scripts/Combat/
public interface ICombatUnit {
    int  Initiative   { get; }
    int  MaxAP        { get; }
    int  CurrentAP    { get; set; }
    float MoveSpeed   { get; }   // 米/AP
    bool IsAlive      { get; }
    Faction Faction   { get; }   // Player / Enemy / Neutral
    Vector3 Position  { get; }
    void OnTurnStart();
    void OnTurnEnd();
}
```

## 5. 移动子系统

- **寻路**：NavMesh（候选）。地图烘焙策略见 `06_MAP_SPEC.md`。
- **预览**：玩家悬停时实时显示路径长度 + AP 消耗。
- **超出 AP 的目标**：如果目标路径存在但当前 AP 不足，预览拆成可达段 / 不可达段；点击后单位移动到可达段末端，并消耗当前可用 AP。
- **打断**：移动过程中遇到 OpportunityAttack / 触发陷阱 → 是否打断？待定。

## 6. 胜负判定

| 条件 | 结果 |
|---|---|
| 玩家阵营全员 `!IsAlive` | Defeat |
| 敌方阵营全员 `!IsAlive` | Victory |
| 自定义剧情条件 | 由关卡脚本注入（占位） |

## 7. 数据驱动

- 单位/技能/怪物建议用 **ScriptableObject** 配置（详见各专项文档）。
- 配置位置：`Assets/Data/{Characters,Monsters,Skills,Maps}/`（目录尚未创建）。

## 8. 待办接口清单（占位，后续实现）

- [x] `CombatRoundManager`（MVP 轮次 + initiative turn order + 可控块 + 敌我回合）
- [x] `ICombatUnit` 接口（`Player1Controller` + `EnemyController` 均实现）
- [x] `APSystem`（MVP：`Player1Controller` 内维护 CurrentAP/MaxAP）
- [x] `InitiativeSystem`（MVP：`Player1Controller.Initiative`，进入模拟时排序）
- [ ] `MovementController`（NavMesh 包装，当前逻辑在 `MoveInputController` 内）
- [ ] `CombatTrigger`（探索 → 战斗）
- [ ] `VictoryConditionEvaluator`

## 9. MVP 回合模拟（玩家 + 敌方单位）

当前实现范围：场景内最多 4 个 `Player1Controller` + 任意数量 `EnemyController`，无 AI、无胜负判定。

- `CombatRoundManager` 启动时收集所有 `ICombatUnit`，按 `Initiative` 降序排序。
- 一轮开始时，所有存活单位 `CurrentAP = MaxAP`，并清除本轮结束标记。
- 玩家可用数字键 `1-4`（映射到可控块内角色）或点击角色在本块内切换。
- 点击地面按 NavMesh 路径消耗 AP 移动。
- 点击敌方单位 = 攻击行为：寻路至攻击范围（默认 1.5m），消耗移动 AP + 1 AP，造成 20 伤害。
- 按 `Space` 标记当前角色本轮不再行动，并将当前 AP 清零。
- 敌方单位回合自动跳过（无 AI）。
- 当所有存活单位本轮结束后，自动进入下一轮。
- 敌方 HP 归零时自动 `Destroy(gameObject)`。

## 10. 玩家可控块规则（Q-0024 已决议，已实现）

回合处理时，将 turn order 中**连续的玩家单位**视为一个"可控块"：

1. 当可控块排在队首时（下一个待行动的是玩家单位），玩家可在该块内的任意未结束角色间自由切换。
2. 一旦队首出现敌方单位，当前可控块必须全部结束行动，之后才轮到敌方行动。
3. 示例：
   - `P1 → P2 → P3`：可自由切换 P1/P2/P3。
   - `P1 → Enemy → P2`：只能操作 P1；P1 结束后敌方行动；敌方结束后才进入 P2。
   - `P1 → P2 → Enemy`：可自由切换 P1/P2；两者都结束后敌方行动。

已实现：`CombatRoundManager.RefreshControllableBlock()` 在每次轮到玩家单位时重新计算可控块；敌方自动跳过。`TrySelectPlayer` 仅允许选择当前块内的角色。

## 11. 基础攻击（MVP 占位）

| 参数 | 值 | 说明 |
|---|---|---|
| 攻击 AP 消耗 | 1 | 每次点击敌方单位消耗 |
| 攻击伤害 | 20 | 固定伤害，无属性/防御计算 |
| 攻击范围 | 1.5m | 玩家导航至距敌方此距离的点后执行攻击 |
| 敌方 HP | 100 | `EnemyController.maxHP` 默认值 |
| 玩家 HP | 100 | `Player1Controller.maxHP` 默认值 |
| 死亡处理 | — | 敌方 `HP ≤ 0` 时 `Destroy(gameObject)`；玩家暂不销毁 |

攻击流程：
1. 玩家点击敌方单位。
2. 计算从玩家到敌方的 NavMesh 全路径。
3. 从路径末端（敌方位置）反向遍历，找到第一个距敌方 ≥ `attackRange` 的 corner 作为目标点。
4. 若已在范围内 → 直接消耗 1 AP 造成伤害。
5. 若需移动 → 消耗移动 AP + 1 AP，导航至目标点后造成伤害。
6. 若 AP 不足以移动 + 攻击，则不执行。
