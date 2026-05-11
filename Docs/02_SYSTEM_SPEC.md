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
| 技能 AP 消耗 | — | 由每个 `SkillDefinition.apCost` 配置；基础攻击也是一个技能 |

公式：
- **移动**：`canWalkDistance = CurrentAP × MoveSpeed`（实际走的距离按 NavMesh 路径长度算，不按直线）。
- 移动 AP 扣除按本轮累计实际移动距离结算：单次或多次移动累计每达到 `MoveSpeed` 米扣除 1 AP；未满 1 AP 的距离暂存到本轮后续移动继续累计。
- **攻击/技能**：AP 消耗由技能配置决定，详见 `05_SKILL_SPEC.md`。当前基础攻击会迁移为 `basic_attack` 技能资产，其 AP 消耗来自 `basic_attack.apCost`。
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
- **预览**：玩家悬停时实时显示路径长度 + AP 消耗；超出 AP 预算时显示可达距离 / 总距离。角色移动过程中保留一条独立的当前移动目标路径线，同时允许鼠标 hover 预览继续更新。
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

## 7.5 Tag First 原则

GameplayTag 是跨系统的第一层语义表达，完整规格见 `09_GAMEPLAY_TAG_SPEC.md`。后续新增技能、状态、AI 条件、关卡条件、交互物、事件通知或 debug 信息时，应优先判断能否用 Tag 表达，而不是立即新增硬编码枚举、布尔字段或专用事件类型。

约定：
- 技能、Effect、Status、AIProfile、Scripted Tactic、可交互物和战斗事件都应优先暴露 GameplayTag。
- 代码中传递游戏语义事件时，应优先携带相关 Tag / TagSet，例如 `Event.Skill.Cast`、`Event.Status.Added`、`Effect.Damage.Physical`。
- Tag 不替代必要的强类型数据，例如 HP 数值、世界坐标、AP 数量、对象引用；Tag 用于表达“这个数据意味着什么”。
- 新系统若需要条件判断，应优先复用 Tag 匹配，而不是创建只服务单个系统的特殊判断。
- Tag 匹配逻辑必须集中封装，禁止各处散落字符串前缀判断。

## 8. 待办接口清单（占位，后续实现）

- [x] `CombatRoundManager`（MVP 轮次 + initiative turn order + 可控块 + 敌我回合）
- [x] `ICombatUnit` 接口（`Player1Controller` + `EnemyController` 均实现）
> ⚠️ 迁移决议(2026-05-11): ICombatUnit + Player1Controller + EnemyController 已决议删除。
> 单位属性通过 AttributeSet 访问，阵营通过 GameplayTagComponent 的 Faction Tag，
> 控制权通过 Control.Human / Control.AI Tag。见 Docs/15 §控制权标识。
- [x] `APSystem`（MVP：`Player1Controller` 内维护 CurrentAP/MaxAP）
> ⚠️ 同上迁移决议。AP 现在由 AttributeSet 通过 GameplayTag("Attribute.AP") 管理，
> MovementController 负责按移动距离实时扣 AP。
- [x] `InitiativeSystem`（MVP：`Player1Controller.Initiative`，进入模拟时排序）
> ⚠️ 同上迁移决议。Initiative 现在由 AttributeSet.Get(WellKnownAttributeTags.Initiative) 读取。
- [ ] `GameplayTagSystem`（Tag First 基础设施：`GameplayTag` + `GameplayTagSet` + 匹配 + 来源追踪，见 `09_GAMEPLAY_TAG_SPEC.md`）
- [ ] `MovementController`（NavMesh 包装；玩家输入已由 `InputController` 纯输入路由，移动解释逻辑临时在 `GroundMoveAbility` 内）
- [ ] `SkillSystem`（`SkillDefinition` + `Effect` + `SkillExecutor` + AP/冷却/目标校验；挂载 `SkillExecutor` 的对象即可成为技能系统目标，见 `05_SKILL_SPEC.md`）
- [x] `EnemyAISystem`（MVP：`EnemyTurnRunner` 最近玩家 → 移动进基础攻击范围 → 攻击一次；完整 Utility AI 见 `04_MONSTER_SPEC.md` §3）
- [ ] `CombatTrigger`（探索 → 战斗）
- [ ] `VictoryConditionEvaluator`

## 9. MVP 回合模拟（玩家 + 敌方单位）

当前实现范围：场景内若干参战单位（通过 `CombatUnit` 标记发现），有最小敌方基础 AI，无胜负判定。队伍人数不设硬性 4 人上限；具体可操作数量由场景配置和 UI/输入支持决定。
> ⚠️ 迁移决议(2026-05-11): Player1Controller/EnemyController 已决议删除。单位统一通过 CombatUnit + AttributeSet + SkillExecutor + MovementController + GameplayTagComponent 组件栈表达。控制权由 Control.Human / Control.AI Tag 标记。

- `CombatRoundManager` 启动时收集所有 `ICombatUnit`，按 `Initiative` 降序排序。
- `CombatRoundManager` 提供 `enemyFirstForDebug` 调试开关（默认关闭）：开启时所有敌方单位排在玩家之前，仅用于 AI 测试，不代表正式先攻规则。
- 轮到敌方单位时，`CombatRoundManager` 使用与玩家选择相同的 `CameraController` 聚焦逻辑将相机聚焦到该敌方单位。
- 一轮开始时，所有存活单位 `CurrentAP = MaxAP`，并清除本轮结束标记。
- 玩家可用数字键快捷选择可控块内角色，也可点击角色在本块内切换；当前数字键支持数量是输入方案限制，不代表队伍人数上限。
- 选中玩家角色后，系统自动激活该角色自身 `SkillExecutor` 上配置的 `basic_move` 技能作为当前默认行为。若该角色没有配置 `basic_move`，该角色不能执行地面移动，并输出明确警告。
- `InputController` 是纯输入接收器，只把鼠标 hover / 主键点击翻译成 `SkillInputRequest`，其中包含输入信号 Tag、目标语义 Tag、命中对象和世界坐标参数。
- 点击地面本质上是 `Input.Target.Ground` + `Input.Pointer.PrimaryPressed` 输入请求；当前激活的 `basic_move` 由 `GroundMoveAbility` 解释该请求、计算 NavMesh 路径、更新预览并通过 `SkillExecutor` 统一执行。输入层不能直接调用 `Player1Controller.TryMove` 绕过技能系统。
- 角色必须通过自身 `SkillExecutor.availableSkills` 配置可用技能资产。`CombatRoundManager` 只做缺失技能的警告，不再启动时向角色自动注入 `basic_move` 或其他默认技能；运行时生成敌人由 `EnemySpawner.m_defaultSkills` 写入生成对象的 `SkillExecutor`。
- 移动中继续刷新鼠标 hover 路径，移动中再次点击会从当前位置改道到新目标。
- 移动 AP 不在下达指令时预扣，而是在角色实际移动距离累计达到 `MoveSpeedMetersPerAp` 时扣除；未满 1 AP 的累计距离会保留到本轮后续移动。
- 当前玩家输入只要求支持点击地面移动。主动攻击技能如何释放（点击敌人、技能栏选择、快捷键等）待 AI 框架大体跑通后继续设计。
- 按 `Space` 标记当前角色本轮不再行动，并将当前 AP 清零。
- 敌方单位回合由 `EnemyTurnRunner` 执行最小基础 AI：选择最近存活玩家，若能在本回合移动到攻击范围并保留攻击 AP，则移动后攻击；若本回合无法攻击，则沿 NavMesh 路径向目标移动到本回合最大可达点。
- 敌方 AI 选终点时使用软占位检查：避开其他存活玩家/敌人的当前位置；理想攻击点或追击点被占用时，在附近采样替代 NavMesh 点，避免多个 AI 抢同一个终点。
- 当所有存活单位本轮结束后，自动进入下一轮。
- 敌方 HP 归零时自动 `Destroy(gameObject)`。
> ⚠️ 迁移决议(2026-05-11): 死亡改为技能化。HP 归零 → AttributeDepleted 事件 → `sys_on_death` 系统技能（注销 + 死亡 VFX + Destroy）。不再由 EnemyController 直接 Destroy。

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

基础攻击后续会迁移为 `basic_attack` 技能资产。当前阶段不要求鼠标直接点击敌方单位触发攻击；主动攻击释放交互待后续技能设计确认。无论最终交互如何，攻击本身不应走独立硬编码逻辑。

| 参数 | 值 | 说明 |
|---|---|---|
| 攻击 AP 消耗 | 1 | `basic_attack.apCost` 示例配置 |
| 攻击伤害 | 20 | `basic_attack` 的 `DamageEffect` 示例配置，暂不计算属性/防御 |
| 攻击范围 | 1.5m | `basic_attack.range` 示例配置 |
| 敌方 HP | 100 | `AttributeSet.GetMax(HP)` 默认值（通过 `AttributeSetDef` 配置） |
| 玩家 HP | 100 | 同上 |
| 死亡处理 | — | `HP ≤ 0` → `AttributeDepleted` 事件 → `sys_on_death` 系统技能（统一处理，不再区分玩家/敌方） |

后续攻击流程草案：
1. 玩家通过待定交互选择主动攻击技能与目标。
2. 系统取得当前角色自身配置的 `basic_attack` 或其他攻击技能配置。
3. 使用技能自身 `range` 判断当前是否已经在攻击距离内。
4. 若已在范围内，且 `CurrentAP >= skill.apCost`，则消耗技能 AP 并造成效果。
5. 若不在范围内，AI/技能计划系统可计算从施法者到目标的 NavMesh 全路径，并寻找能进入 `skill.range` 的可施放点。
6. 若 `CurrentAP >= skill.apCost + 移动所需 AP`，且能走到可施放点，则先移动到技能范围内，再消耗 `skill.apCost` 执行技能。
7. 若本回合 AP 不足以抵达技能范围，则是否只移动靠近由 AI 候选评分或玩家交互规则决定。
8. 若路径不存在或没有合法可施放点，则不执行技能，并输出失败原因。
