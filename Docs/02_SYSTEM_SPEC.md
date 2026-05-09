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

- [x] `CombatRoundManager`（MVP 玩家-only 轮次 + initiative turn order）
- [ ] `ICombatUnit` 接口与 `CombatUnitBase` 抽象类
- [x] `APSystem`（MVP：`Player1Controller` 内维护 CurrentAP/MaxAP）
- [x] `InitiativeSystem`（MVP：`Player1Controller.Initiative`，进入模拟时排序）
- [ ] `MovementController`（NavMesh 包装）
- [ ] `CombatTrigger`（探索 → 战斗）
- [ ] `VictoryConditionEvaluator`

## 9. MVP 玩家-only 回合模拟

当前实现范围：场景内最多 4 个 `Player1Controller`，无敌人 AI、无胜负判定。

- `CombatRoundManager` 启动时收集玩家，按 `Initiative` 降序排序。
- 一轮开始时，所有玩家 `CurrentAP = MaxAP`，并清除本轮结束标记。
- 玩家可用数字键 `1-4` 或点击角色选择本轮尚未结束的角色。
- 点击地面仍按 NavMesh 路径消耗 AP；可走距离仍为 `CurrentAP * MoveSpeed`。
- 按 `Space` 标记当前角色本轮不再行动，并将当前 AP 清零。
- 当所有玩家都被标记为本轮结束后，自动进入下一轮并恢复全部 AP。

## 10. 玩家可控块规则（Q-0024 已决议）

回合处理时，将 turn order 中**连续的玩家单位**视为一个"可控块"：

1. 当可控块排在队首时（下一个待行动的是玩家单位），玩家可在该块内的任意未结束角色间自由切换。
2. 一旦队首出现敌方单位，当前可控块必须全部结束行动，之后才轮到敌方行动。
3. 示例：
   - `P1 → P2 → P3`：可自由切换 P1/P2/P3。
   - `P1 → Enemy → P2`：只能操作 P1；P1 结束后敌方行动；敌方结束后才进入 P2。
   - `P1 → P2 → Enemy`：可自由切换 P1/P2；两者都结束后敌方行动。

当前 MVP 无敌方单位，所有角色属于同一个可控块（规则退化为自由切换）。引入敌方后，`CombatRoundManager` 需增加可控块计算逻辑。
