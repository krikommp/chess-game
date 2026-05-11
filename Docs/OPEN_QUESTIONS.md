# OPEN QUESTIONS

> 所有"设计不清楚"的问题都先记到这里。**禁止 AI 擅自决定**。每条问题用 `Q-XXXX` 编号。

格式：
```
### Q-XXXX 标题
- 来源：哪个文档 / 哪次对话
- 问题：…
- 当前临时假设：…（仅用于让原型能跑）
- 决议：(空) / 用户回复
- 影响：列出会被影响的文件
```

---

### Q-0001 相机是否支持平移 (pan)
- 来源：`01_GAME_DESIGN_BRIEF.md` §1
- 问题：用户明确说"只能拉近拉远不能旋转"，但没提"平移"。BG3 / 神界原罪 2 都支持平移。
- 当前临时假设：**支持平移**（WASD 或边缘推屏），不支持旋转。
- 决议：**支持平移**。当前实现支持 `WASD / 方向键` 与 `鼠标中键拖拽`。角色行动开始时相机会重新聚焦角色；如果玩家随后继续移动相机，则停止强制聚焦，直到下一次角色行动。
- 影响：`Assets/Scripts/CameraController.cs`

### Q-0002 战斗触发方式
- 来源：`01_GAME_DESIGN_BRIEF.md` §2.2
- 问题：进入战斗的具体触发条件？接近敌人 / 主动攻击 / 剧情？
- 当前临时假设：**主动攻击 + 敌人感知半径双触发**。
- 决议：(空)
- 影响：`CombatTrigger`、AI 感知

### Q-0003 先攻值平局处理
- 来源：`02_SYSTEM_SPEC.md` §2
- 问题：两个单位 Initiative 相同时谁先动？
- 当前临时假设：**玩家方优先；玩家方内部按队伍槽位顺序**。
- 决议：(空)

### Q-0004 先攻值是否可被技能/状态修改
- 来源：`01_GAME_DESIGN_BRIEF.md` §2.3
- 问题：用户说"一般不会动态变"。"一般"是否意味着存在例外（例如 Haste/Slow 技能）？
- 当前临时假设：**MVP 阶段不可修改**，等技能列表确定后再评估。
- 决议：(空)

### Q-0005 MaxAP 与基础攻击 AP 消耗
- 来源：`02_SYSTEM_SPEC.md` §3
- 问题：每回合 AP 上限？基础攻击消耗多少 AP？
- 当前临时假设：`MaxAP = 6`；技能 AP 消耗由技能配置决定。
- 决议：**MVP 单位默认 `MaxAP = 6`；不再使用全局 `BasicAttackCost`。基础攻击作为 `basic_attack` 技能资产，其消耗由 `basic_attack.apCost` 配置。当前示例配置为 `basic_attack.apCost = 1`，后续所有技能均通过 `SkillDefinition.apCost` 单独配置。**
- 影响：所有战斗平衡

### Q-0006 回合结束时剩余 AP 是否保留
- 来源：`02_SYSTEM_SPEC.md` §3
- 问题：未用完的 AP 进下一回合是否累积？
- 当前临时假设：**不保留**（每回合重置）。
- 决议：(空)

### Q-0007 队伍人数是否存在硬性 4 人上限
- 来源：`01_GAME_DESIGN_BRIEF.md` §3
- 问题：玩家队伍是否固定或最多 4 人？单角色、多于 4 角色的队伍是否合法？
- 当前临时假设：**允许一个或多个玩家角色；不设置固定 4 人硬上限**。
- 决议：**取消 4 人硬性需求**。玩家队伍人数由关卡/场景配置和后续 UI 承载能力决定；原型阶段的数字键或界面只覆盖部分角色时，视为交互限制而非设计上限。

### Q-0008 移动过程是否可被打断
- 来源：`02_SYSTEM_SPEC.md` §5
- 问题：移动中遇到攻击/陷阱是否中断？
- 当前临时假设：**不打断**（MVP）。
- 决议：(空)

### Q-0009 角色属性是否采用 D&D 6 属性
- 来源：`03_CHARACTER_SPEC.md` §1
- 问题：是否引入 STR/DEX/CON/INT/WIS/CHA？
- 当前临时假设：**不引入**，只用 HP/Attack/Defense/MoveSpeed/Initiative 简化属性。
- 决议：(空)

### Q-0010 等级与成长机制
- 来源：`03_CHARACTER_SPEC.md` §3
- 问题：经验值升级 / 里程碑升级 / 无成长？
- 当前临时假设：**MVP 阶段无成长**，关卡固定数值。
- 决议：(空)

### Q-0011 装备战斗中可否切换
- 来源：`03_CHARACTER_SPEC.md` §4
- 问题：战斗中换武器是否消耗 AP？
- 当前临时假设：**不允许战斗中切换**（MVP）。
- 决议：(空)

### Q-0012 怪物 AI 实现方案
- 来源：`04_MONSTER_SPEC.md` §3
- 问题：Behavior Tree / GOAP / 硬编码 / Utility AI？
- 当前临时假设：**轻量 Utility AI + ScriptableObject 配置 + Scripted Tactic 覆盖**。暂不引入 Behavior Tree / GOAP 插件；第一阶段只实现最小可跑闭环。
- 决议：**采用轻量 Utility AI + 脚本化战术覆盖**。原因：敌方技能数量较少，但需要策划配置开场行为、条件触发特殊决策，以及支援/治疗/进攻等不同倾向。GOAP 对当前范围过重；Behavior Tree / NodeCanvas / Behavior Designer 保留为后续可视化编辑需求出现后的升级路径。
- 影响：`Docs/04_MONSTER_SPEC.md`、`Docs/05_SKILL_SPEC.md`、`EnemyController`、后续 `AIProfile` / `AIActionEvaluator` / `AIExecutor`

### Q-0013 战利品系统是否需要
- 来源：`04_MONSTER_SPEC.md` §1
- 问题：是否需要怪物掉落？
- 当前临时假设：**MVP 阶段不做**。
- 决议：(空)

### Q-0014 是否需要"法力值"等第二资源
- 来源：`05_SKILL_SPEC.md` §11
- 问题：技能除 AP 外是否还消耗 MP / 怒气 / 充能？
- 当前临时假设：**只消耗 AP**（用户原话："技能的消耗由技能自己决定"，理解为只消耗 AP）。
- 决议：**技能不会消耗 AP 之外的其他资源**。特定技能可以配置冷却时间；冷却由 `SkillDefinition.cooldown` 表达，战斗中按回合计，探索实时状态下按秒刷新，暂定 `1 回合 = 6 秒`。

### Q-0015 命中是否需要检定
- 来源：`05_SKILL_SPEC.md` §11
- 问题：D&D 式 attack roll vs 100% 命中？
- 当前临时假设：**100% 命中 + 闪避概率扣减**。
- 决议：(空)

### Q-0016 暴击 / 闪避机制
- 来源：`05_SKILL_SPEC.md` §11
- 问题：是否引入？
- 当前临时假设：**有暴击和闪避**（默认 5% / 5%），具体由属性派生公式 TBD。
- 决议：(空)

### Q-0017 寻路方案
- 来源：`06_MAP_SPEC.md` §2
- 问题：Unity 自带 NavMesh / A* Pathfinding Project / 自研？
- 当前临时假设：**Unity NavMesh**。
- 决议：(空)

### Q-0018 高度差影响战斗
- 来源：`06_MAP_SPEC.md` §5
- 问题：高低差是否影响命中 / 射程 / 伤害？
- 当前临时假设：**MVP 不考虑**。
- 决议：(空)

### Q-0019 破坏性环境（神界原罪式）
- 来源：`06_MAP_SPEC.md` §5
- 问题：是否做爆桶 / 元素地表？
- 当前临时假设：**MVP 不做**，留作后续扩展。
- 决议：(空)

### Q-0020 多层建筑 / 楼层
- 来源：`06_MAP_SPEC.md` §5
- 问题：如何处理楼层切换？
- 当前临时假设：**MVP 只做单层**。
- 决议：(空)

### Q-0021 世界观 / 主线
- 来源：`07_STORY_SPEC.md` §1
- 问题：时代背景、主线、派系全部空白。
- 当前临时假设：**无世界观**，关卡用占位文本（"Level 1 - Tutorial"）。
- 决议：(空)

### Q-0022 对话系统选型
- 来源：`07_STORY_SPEC.md` §4
- 问题：自研 / Yarn Spinner / Ink？
- 当前临时假设：**MVP 阶段先不做对话**。
- 决议：(空)

### Q-0023 数据目录是否新建
- 来源：`02_SYSTEM_SPEC.md` §7
- 问题：`Assets/Data/{Characters,Monsters,Skills,Maps,Quests}/` 何时创建？
- 当前临时假设：**等真正写第一个 ScriptableObject 时再创建**，避免空目录。
- 决议：(空)

### Q-0024 玩家-only 回合模拟是否严格锁定先攻顺序
- 来源：2026-05-09 用户需求 / `02_SYSTEM_SPEC.md` §1-2
- 问题：文档要求按先攻顺序行动；本次需求同时要求玩家可以选中不同 player 进行控制。最终规则是严格只能控制当前先攻单位，还是玩家方可在本轮未行动角色之间自由切换？
- 当前临时假设：**MVP 玩家-only 模拟允许在本轮未结束的玩家角色之间手动选择**；`CombatRoundManager` 仍用先攻排序决定默认选择和自动跳转顺序。
- 决议：**连续玩家块内自由切换**。规则如下：
  1. Turn order 中，将连续的玩家单位视为一个"可控块"。
  2. 当可控块排在队首时（即下一个待行动的是玩家单位），玩家可以在该块内的任意未结束角色间自由切换。
  3. 一旦队首出现敌方单位，则当前可控块必须全部结束行动，之后轮到敌方行动。
  4. 示例：
     - `P1 → P2 → P3`：可自由切换 P1/P2/P3。
     - `P1 → Enemy → P2`：只能操作 P1；P1 结束后敌方行动；敌方结束后进入下一块 P2。
     - `P1 → P2 → Enemy`：可自由切换 P1/P2；两者都结束后敌方行动。
  5. 当前 MVP 没有敌方单位，因此所有角色都属于同一个可控块（规则退化为当前行为）。
- 影响：`CombatRoundManager`（需增加可控块计算逻辑）、`02_SYSTEM_SPEC.md`（补充规则描述）

### Q-0025 第一阶段技能池是否确认
- 来源：2026-05-09 用户需求 / `05_SKILL_SPEC.md` §7
- 问题：MVP 第一阶段是否采用 `basic_attack`、`guarding_shout`、`minor_heal`、`power_strike`、`crippling_hex` 作为技能系统与敌方 AI 的验证技能池？
- 当前临时假设：**采用这 5 个技能作为草案**，用于验证伤害、治疗、buff、debuff 与 AI 行为倾向；具体数值后续平衡。
- 决议：**第一批按 `basic_attack` → `minor_heal` → `guarding_shout` 实现**。`basic_attack` 验证最小技能纵切片；`minor_heal` 验证友方目标和治疗 AI；`guarding_shout` 验证 Status、Tag、buff 和支援型 AI。`power_strike` 与 `crippling_hex` 放到第二批。每个 Effect 必须配置至少一个 GameplayTag。
- 影响：`Docs/05_SKILL_SPEC.md`、后续 `SkillDefinition` / `AIProfile` / `EnemyAISystem`

### Q-0026 GameplayTag 命名空间与校验来源
- 来源：2026-05-10 用户需求 / `05_SKILL_SPEC.md` §4.5 / `09_GAMEPLAY_TAG_SPEC.md`
- 问题：GameplayTag 是先采用自由字符串 + 集中匹配工具，还是马上建立全局 TagRegistry / TagDatabase 来统一命名空间、自动补全和非法 Tag 校验？
- 当前临时假设：**第一阶段就建立 `GameplayTagRegistry.asset` 与 Combat Config 的 Tags / Validation 入口**；底层序列化可以暂时使用字符串，但配置必须通过 TagRegistry 和集中校验访问。
- 决议：**Tag 系统优先完成，并包含编辑器第一版**。先实现运行时 Tag 闭环，再实现 TagRegistry 与 Combat Config 的 Tags / Validation 页，之后再进入 SkillDefinition / Effect / SkillExecutor。
- 影响：`GameplayTag` / `GameplayTagSet` / `SkillDefinition` / `EffectDefinition` / `Status` / `AIProfile` / Combat Config 编辑器

### Q-0027 GameplayTag 运行时注册规则
- 来源：2026-05-10 用户需求 / `09_GAMEPLAY_TAG_SPEC.md`
- 问题：运行时是否允许运行时创建未注册 Tag？还是所有 Tag 必须来自 `TagRegistry`？
- 当前临时假设：**MVP 允许字符串 Tag，但通过配置校验提示未注册 Tag；正式编辑器阶段倾向要求所有长期配置 Tag 注册到 TagRegistry。**
- 决议：(空)
- 影响：`GameplayTag` / `GameplayTagSet` / `TagRegistry` / Combat Config 编辑器 / debug 工具

### Q-0028 GameplayTag 多来源叠加规则
- 来源：2026-05-10 用户需求 / `09_GAMEPLAY_TAG_SPEC.md`
- 问题：同一个 Tag 来自多个来源时，移除其中一个来源是否保留该 Tag？例如装备和 Status 都提供 `State.Fire.Resistance`。
- 当前临时假设：**TagSet 追踪来源计数或来源列表；只有所有来源都移除后，该 Tag 才从对象上消失。**
- 决议：(空)
- 影响：`GameplayTagSet` / Status / Equipment / Terrain / ScriptedTactic

### Q-0029 Status 属性修正值追踪方式
- 来源：2026-05-11 代码审查 / `Docs/13_FUTURE_DESIGN.md` §1.2
- 问题：Status 的属性修正（StatModifier，如 Attack+5）是直接修改 AttributeSet 的值（Add/Remove 配对），还是维护独立的"修正值快照"在查询时动态计算？
- 当前临时假设：**直接修改 AttributeSet（Add/Remove 配对）**。添加 Status 时 `Modify(attr, +delta)`，移除时 `Modify(attr, -delta)`。
- 决议：(空)
- 影响：`StatusComponent`、`AttributeSet`、`StatModifier`

### Q-0030 AI 评分公式参数是否策划可配置
- 来源：2026-05-11 代码审查 / `Docs/13_FUTURE_DESIGN.md` §2.3
- 问题：AI 候选评分的各分项（距离分、AP 效率分、HP 紧迫度阈值等）权重是写死在 `AIActionEvaluator` 中，还是作为 `AIProfile` 或 `CombatConfig` 的可配置字段？
- 当前临时假设：**部分可配置**。`AIProfile` 中已有的字段可配置；距离/AP 效率等通用参数暂时硬编码，后续提升到 `CombatConfig`。
- 决议：(空)
- 影响：`AIActionEvaluator`、`AIProfile`、`CombatConfig`

### Q-0031 战斗事件是否需要附带属性快照
- 来源：2026-05-11 代码审查 / `Docs/13_FUTURE_DESIGN.md` §4
- 问题：`CombatEvent` 中是否附带事件前后 HP/AP 快照？UI 动效可能需要前后值计算动画。
- 当前临时假设：**附带变化前后值**。`CombatEvent` 包含 `ValueBefore` 和 `ValueAfter` 字段。
- 决议：(空)
- 影响：`CombatEventBus`、`CombatEvent`、`AttributeSet.Modify`

### Q-0032 ScriptedTactic 配置方式
- 来源：2026-05-11 代码审查 / `Docs/13_FUTURE_DESIGN.md` §2.5
- 问题：ScriptedTactic 配置方式：独立 ScriptableObject 资产 vs 关卡数据内嵌 vs 在 CombatTrigger 上直接配置？
- 当前临时假设：**独立 ScriptableObject 资产 + CombatTrigger 引用**，可被多个战斗复用。
- 决议：(空)
- 影响：`ScriptedTactic`、`CombatTrigger`、关卡数据格式

### Q-0033 同一 Status 多次叠加时属性修正回滚
- 来源：2026-05-11 代码审查 / `Docs/13_FUTURE_DESIGN.md` §1
- 问题：若同一 Status 多次叠加（StackValue 规则），移除一层时需精确回退一层修正值。Add/Remove 配对方案是否够用？
- 当前临时假设：**每层独立追踪修正量**。`StatusComponent` 记录每层修正快照，移除时精确回退。
- 决议：(空)
- 影响：`StatusComponent`、`StatusInstance`、属性修正回退逻辑

### Q-0034 basic_move 是否必须显式挂载 Ability 资产
- 来源：2026-05-11 代码审查 / IS-0004
- 问题：修复 IS-0004 后是否要求所有 `basic_move` 资产必须通过 Unity Editor 显式挂载 Ability？
- 当前临时假设：**要求显式配置**。移除 `DefaultInstance` fallback；Config Validation 检查 GroundPoint 技能是否配置了 Ability。
- 决议：(空)
- 影响：`SkillDefinition.Ability` getter、`GroundMoveAbility.DefaultInstance`、Config Validation

### Q-0035~Q-0039: Ability-Effect 模型重设计 — 已决议
- 决议：见下方 §Ability-Effect 设计决议

---

## Ability-Effect 设计决议（2026-05-11）

### 讨论进度

| 议题 | 状态 |
|------|------|
| Effect 统一接口 `Compute(ctx)→Result` + `Apply(ctx, result)` | ✅ 已确认 |
| Effect Instant vs Persistent (`EEffectDuration`) | ✅ 已确认 |
| Effect Tag 互作用：GrantTags / RemoveTags / RequiredTags / BlockedTags | ✅ 已确认 |
| Ability 四个槽位：Costs / Cooldowns / Effects / BlockedTags | ✅ 已确认 |
| Ability 唯一方法：`Execute(context, fireEffect)` | ✅ 已确认 |
| AP 消耗迁移到 Costs: Effect[]，不设独立 `GetApCost` | ✅ 已确认 |
| 冷却 = Persistent Status + `Cooldown.{id}` Tag + BlockedTags 检查 | ✅ 已确认 |
| HandleInput 从 Ability 移出，输入解析由 UnitTurnHandler 负责 | ✅ 已确认 |
| MovementController 删除 AP 扣除，变为纯移动工具 | ✅ 已确认 |
| GroundMoveAbility 复用 CombatMovementResolver | ✅ 已确认 |
| SkillDefinition 层 Tag 条件字段是否保留 | ⬜ 待讨论 |
| Player1Controller/EnemyController 删除后的 Control.Human/AI Tag 方案 | ✅ 已确认（之前讨论） |
| 死亡技能化 `sys_on_death` | ✅ 已确认（之前讨论） |

### 核心原则

1. **Effect 是纯数据载体 + 简单计算逻辑**。Effect 不驱动流程，只"被触发"。
2. **Ability 是流程驱动器**。Ability 决定何时、以什么顺序触发 Effect。
3. **MovementController 是纯移动工具**。不负责 AP 扣除、校验等业务逻辑。
4. **HandleInput 从 Ability 移出**。输入解析由 UnitTurnHandler 负责。

### Ability 结构

```
SkillAbility (ScriptableObject)
├── BlockedTags[]          ← 释放者命中任一 Tag → 不能释放
├── Costs: Effect[]        ← 先 Compute(算消耗)→能负担→Execute时Apply(扣)
├── Cooldowns: Effect[]    ← 先 Compute(在冷却?)→能释放→Execute时Apply(设冷却)
└── Effects: Effect[]      ← 实际游戏效果（伤害、治疗、移动、加Status）
```

唯一抽象方法：`Execute(context, fireEffect)`

### Effect 统一接口

```
EffectDefinition (ScriptableObject)
├── Compute(context) → EffectResult   ← 纯计算，不改状态
├── Apply(context, result) → void     ← 真实执行
└── EEffectDuration                  ← Instant（瞬时） / Persistent（持久）
```

- **Instant Effect**：执行后不追踪生命周期，即使它有 Tag（如 DamageEffect 带 `Effect.Damage.Physical`）
- **Persistent Effect**：注册到目标 `SkillExecutor` 的内置定时器，追踪 `RemainingRounds`。每回合 tick，过期后自动 Remove

### 冷却机制

冷却 = Persistent Status（不可驱散） + BlockedTags 检查：

```
释放 power_strike (cooldown=2):
  Ability.Cooldowns: [SetCooldownEffect]
  
  SetCooldownEffect.Compute:
    → 检查 HasTag(Cooldown.power_strike) → 有则失败
  SetCooldownEffect.Apply:
    → 施加 Status(power_strike_cooldown):
        appliedTags: [Cooldown.power_strike]
        durationRounds: 2
        stackRule: RefreshDuration

SkillExecutor.CanExecute:
  → 检查 SkillDefinition 的 BlockedCasterTags
    → 若包含 Cooldown.{skill_id} 系列前缀
    → HasTag(Cooldown.power_strike) → true → 拒绝释放
```

冷却 Tag 生命周期由 Status 的定时器管理——Status 过期 → `RemoveAllTagsFromSource` → 冷却自动清除。

### 移动技能示例

```
basic_move:
  Ability: GroundMoveAbility
    ├── BlockedTags: [State.Rooted]
    ├── Costs: [SpendAPEffect]
    ├── Cooldowns: []
    └── Effects: [MoveEffect]

  执行流程:
  1. Compute 阶段（hover预览 / AI评估）:
     MoveEffect.Compute: 计算 NavMesh 路径 → 得出 pathLength
     SpendAPEffect.Compute: pathLength/speed → 预计AP消耗, 检查AP是否够
  
  2. Execute 阶段（确认）:
     MoveEffect.Apply: MovementController.TryStartMove(path) ← 纯移动
     SpendAPEffect.Apply: AttributeSet.TrySpend(AP, computed.cost)
```

### 攻击技能示例

```
basic_attack:
  Ability: null (纯Effect技能)
    ← SkillExecutor 直接遍历 skill.Effects，全部 Instant 触发
  
  或带 Ability:
  Ability: MeleeAttackAbility
    ├── BlockedTags: [State.Disarmed]
    ├── Costs: [SpendAPEffect(amount=1, blockedCasterTags=[State.FreeCast])]
    ├── Cooldowns: []  ← basic_attack 无冷却
    └── Effects: [DamageEffect(amount=20)]

  执行流程:
  1. SpendAPEffect.Compute → 检查 AP>=1 且 caster 没有 State.FreeCast
  2. SpendAPEffect.Apply → 扣 1 AP（若有 State.FreeCast 则跳过）
  3. DamageEffect.Apply → AttributeSet.Modify(HP, -20)
```

### 改动范围

| 文件 | 改动 |
|------|------|
| `SkillAbility.cs` | 接口精简为 `Execute(context, fireEffect)`；新增 Costs/Cooldowns/Effects/BlockedTags 槽位 |
| `SkillExecutor.cs` | Execute 流程改为：Compute 全部 Costs+Cooldowns → AllSuccess → Execute Ability → Apply Costs+Cooldowns+Effects。内置定时器管理 Persistent Effect |
| `EffectDefinition.cs` | 新增 `Compute()` / `Apply()` 两步接口；新增 `EEffectDuration` 枚举 |
| `GroundMoveAbility.cs` | 重写：内部调用 CombatMovementResolver，编排 Compute→Execute 流程 |
| `MovementController.cs` | 删除 AP 扣除逻辑，变为纯移动工具 |
| `CombatMovementResolver.cs` | GroundMoveAbility 直接复用，消除功能重叠 |
| `SkillDefinition.cs` | `m_apCost` / `m_cooldown` 不再需要（迁移到 Costs/Cooldowns Effect 上） |

### Effect 的 Tag 互作用字段

每个 Effect 自身携带四个 Tag 字段，表达它对目标 Tag 的交互：

| 字段 | 类型 | 作用 |
|------|------|------|
| **Identity Tags** (`m_tags`) | `GameplayTag[]` | Effect 的身份标记，如 `Effect.Damage.Physical`（已有） |
| **GrantTags** (`m_grantedTags`) | `GameplayTag[]` | Apply 时给目标添加这些 Tag。Persistent Effect 过期时自动通过 `RemoveAllTagsFromSource` 移除（已有） |
| **RemoveTags** | `GameplayTag[]` | Apply 时从目标移除匹配的 Tag。例如 `MoveEffect` 带 RemoveTags: `[State.Rooted]`，移动后自动驱散定身 |
| **RequiredTags** | `GameplayTag[]` | Compute 时检查目标是否拥有这些 Tag。不满足 → Compute 返回失败，Effect 不应用。例如 `ExecuteEffect` 带 RequiredTags: `[State.Weakened]` |
| **BlockedTags** | `GameplayTag[]` | Compute 时检查目标是否拥有这些 Tag。命中任一 → Compute 返回失败。例如 `DamageEffect` 带 BlockedTags: `[State.Immune.Fire]` |

RequiredTags/BlockedTags 当前在 `SkillDefinition` 层（`RequiredCasterTags`/`BlockedCasterTags`/`RequiredTargetTags`/`BlockedTargetTags`），应下沉到 Effect 自身——每个 Effect 携带自己的条件，SkillDefinition 层的 Tag 条件保留用于技能级的全局判断。

### Q-0035 Ability-Effect 模型已决议（详见下文 §Ability-Effect 设计决议）
- 决议状态：**已确认**（2026-05-11）

### Q-0036 已合并到 Q-0035 决议
### Q-0037 已合并到 Q-0035 决议
### Q-0038 已合并到 Q-0035 决议
### Q-0039 已合并到 Q-0035 决议
