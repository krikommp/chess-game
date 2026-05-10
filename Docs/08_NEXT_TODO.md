# 08 - 下一阶段 TODO

> 本文用于承接当前敌方 AI MVP 之后的开发。后续 Agent 应按本清单逐项推进；每一项在执行前需要与用户确认，确认后再修改代码或设计文档。
>
> 状态字段说明：`决议状态` 表示设计方向是否已由用户确认；`实现状态` 表示代码/资产是否已落地。不要把“已确认”理解为“已实现”。

## 0. 当前状态

- 已完成最小敌方 AI：敌方能选择最近玩家、沿 NavMesh 接近、进入基础攻击范围后攻击。
- 敌方 AI 已有软占位检查，避免多个 AI 选择同一个终点；AI 框架接入时再统一处理移动技能与技能候选的组合。
- 当前敌方行动可通过调试开关临时排在玩家前面，便于测试。
- 已有 `basic_move` 技能资产、`GroundMoveAbility` 与 `SkillExecutor` 基础入口；当前玩家侧只要求角色具备地面移动能力，主动攻击技能如何释放留到 AI 框架大体跑通后继续设计。
- 文档方向已经确定：敌方 AI 采用轻量 Utility AI + Scripted Tactic，不引入 Behavior Tree / GOAP 插件作为第一阶段方案。

## 0.5 下一轮开发范围

决议状态：已确认

实现状态：待后续实现

范围：
- 下一轮只专注“一轮战斗内”的内容：Tag、技能、Effect、SkillExecutor、AIProfile、敌方技能候选评分、AI debug 输出。
- 战斗外进入流程、战斗结束、胜负结算、奖励、任务、剧情、对话、保存加载均不进入本轮。
- `CombatTrigger` 与 `VictoryConditionEvaluator` 暂时保留在系统规格的待办中，不插入本轮执行顺序。
- 后续 Agent 如果需要验证一轮战斗内容，可以继续使用当前场景内已有玩家与敌人对象，不需要先实现正式战斗触发或胜负流程。

验证：
- 本轮成果能在当前战斗测试场景中验证至少一个完整敌方行动回合。
- 验证重点是玩家/敌方能通过同一套技能执行入口完成行动，而不是战斗如何开始或如何结束。

## 0.6 代码审查问题清单（后续 Agent 必须解决）

来源：2026-05-10 代码审查

执行要求：
- 后续 Agent 在继续 Tag、Skill、Effect、SkillExecutor、AIProfile、AI 候选评分或 AI debug 工作前，必须先检查本节问题是否仍然存在。
- 如果本节问题影响当前实现范围，必须在同一工作会话内修复；不要只新增功能而保留这些结构性问题。
- 修复后需要同步更新本节状态，并在最终报告中说明对应问题的处理结果。

本次复审结论（2026-05-10）：
- 已通过并从未通过清单中移除：`CR-0001`、`CR-0003`、`CR-0004`、`CR-0006`、`CR-0007`、`CR-0008`、`CR-0009`、`CR-0010`、`CR-0011`。
- `CR-0002` 不作为当前阻塞项处理，移动到 `## 7. 让 EnemyTurnRunner 枚举技能候选` 与 AI 框架一起完成；AI 框架必须注意 `basic_move` 与后续技能候选的组合执行。
- `CR-0005` 从当前要求中移除：主动攻击技能释放方式尚未定案，当前阶段只要求玩家角色能移动。
- 所有审查项均已通过。

### CR-0010 SkillExecutor.Execute 不应在 Ability.Apply 失败后吞掉 AP

严重级别：P2

状态：已通过（2026-05-10）

问题：
- `SkillExecutor.Execute` 当前先 `caster.TrySpendAP(apCost)`，再调用 `skill.Ability.Apply(context)`。
- 如果 `CanExecute` 通过但 `Apply` 阶段失败（例如 `TryStartMove` 因 NavMeshAgent 状态变化失败），本次技能失败但 AP 已扣除，冷却和 Effect 不会执行。

意见：
- 要么把会失败的启动条件全部前移到 `Ability.CanApply`，保证 `Apply` 不再失败；要么在 `Execute` 中让 Ability 明确分成“提交前检查/提交后执行”，避免失败后资源状态半提交。
- 如果保留当前接口，至少需要在 `Apply` 失败时回滚 AP，或把 AP 扣除移到 `Apply` 成功之后；移动技能要确认“开始移动成功”后再扣除技能 AP。
- 增加测试：构造一个 `CanApply` 成功但 `Apply` 失败的测试 Ability，确认失败不会扣 AP、不会记录冷却、不会执行 Effect。

验证：
- `Ability.Apply` 返回失败时，施法者 AP 保持不变。
- `Ability.Apply` 返回失败时，不记录 cooldown，不执行任何 Effect。

处理记录（2026-05-10）：
- `SkillExecutor.Execute` 中 `TrySpendAP` 移到 `Ability.Apply` 成功之后执行。
- 新增 3 个测试：`Execute_DoesNotDeductAp_WhenApplyFails`、`Execute_DoesNotRecordCooldown_WhenApplyFails`、`Execute_DoesNotApplyEffects_WhenApplyFails`。

### CR-0011 清理旧兼容 API 与公开 Inspector 字段

严重级别：P3

状态：已通过（2026-05-10）

问题：
- `SkillExecutor.CanCastGroundPoint` / `ExecuteGroundPoint` 兼容 API 和相关测试仍存在，容易让后续代码继续绕过新的 `SkillExecutionContext` 入口。
- `SkillExecutor.availableSkills`、`CombatRoundManager.basicAttackSkillOverride`、`basicMoveSkillOverride` 仍是 public 字段，不符合 `Docs/10_CODE_STYLE.md` 对 Inspector 字段的要求。
- TODO 中旧 `MoveInputController` 名称仍大量出现在历史描述里，虽然旧类已删除，但会干扰后续 Agent 判断当前入口。

意见：
- 删除或标记废弃 `CanCastGroundPoint` / `ExecuteGroundPoint`，测试改名并统一走 `CanExecute` / `Execute(SkillExecutionContext)`。
- 把公开 Inspector 字段改成 `[SerializeField] private m_availableSkills` / `m_basicAttackSkillOverride` / `m_basicMoveSkillOverride`，通过只读属性或方法暴露。
- 历史段落若必须保留，应统一注明“历史名：MoveInputController；当前名：InputController”，不要在当前待办里继续引用旧类名。

验证：
- `rg "CanCastGroundPoint|ExecuteGroundPoint|MoveInputController" Assets/Scripts Assets/Tests` 不再出现旧入口。
- Inspector 配置仍能正常序列化技能列表与覆盖技能。

处理记录（2026-05-10）：
- 删除 `SkillExecutor.CanCastGroundPoint` / `ExecuteGroundPoint`，统一使用 `CanExecute` / `Execute(SkillExecutionContext.ForGroundPoint(...))`。
- `availableSkills` → `[SerializeField] private m_availableSkills`；`basicAttackSkillOverride` / `basicMoveSkillOverride` → `[SerializeField] private m_basicAttackSkillOverride` / `m_basicMoveSkillOverride`。
- GroundPoint 测试全部改写为 `CanExecute(SkillExecutionContext.ForGroundPoint(...))` 入口，移除 wrapper 专属的"非 GroundPoint 类型"检查测试（该检查现在由 `GroundMoveAbility.CanApply` 负责）。

## 1. 确认当前 AI 调试状态

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 明确 `enemyFirstForDebug` 是否继续作为调试开关保留。
- 避免调试用“敌方先行动”被误认为正式先攻规则。

确认方案：
- 保留 `enemyFirstForDebug`，但只作为 Inspector 调试字段。
- 默认值后续改回 `false`，正式轮次仍按 Initiative 排序。
- 若需要继续调 AI，可临时在场景对象上打开该字段。

产出：
- `CombatRoundManager` 中调试字段命名/注释更清晰。
- `enemyFirstForDebug` 默认值改为 `false`；测试 AI 时可在 Inspector 临时打开。
- 如有必要，在 `Docs/02_SYSTEM_SPEC.md` 说明这是调试行为，不属于正式规则。

验证：
- 关闭调试开关时，敌我按 Initiative 行动。
- 打开调试开关时，敌方优先行动，方便测试 AI。

关联问题：
- `Q-0003` 先攻值平局处理。
- `Q-0024` 玩家可控块规则。

## 1.5 规划 AI / 技能编辑器入口

决议状态：已确认方向

实现状态：暂不进入本轮

目标：
- AI 与技能系统必须保持数据驱动，方便策划和 Agent 通过配置完成内容扩展。
- 后续需要一个统一编辑器界面，用于浏览、创建、打开和维护技能、AI、全局战斗配置。

确认方向：
- 后续增加 Unity Editor 工具窗口，例如 `MiniChess/Combat Config`。
- 工具窗口负责集中管理战斗相关数据资产，而不是把配置散落在场景对象上。
- 工具窗口可以自动创建或持有全局配置资产，例如：
  - 技能数据库 / 技能索引。
  - AIProfile 数据库 / AI 行为模板索引。
  - 全局战斗配置，例如默认 AP、基础攻击配置、AI 调试开关、默认评分参数。
- 工具窗口需要提供快捷入口，能直接打开单个 `SkillDefinition`、`AIProfile` 或全局配置资产。
- 运行时系统只读取数据资产；编辑器界面只是配置入口，不承载战斗逻辑。
- Agent 后续应优先通过创建或修改 ScriptableObject 配置来扩展技能和 AI，而不是继续硬编码行为。

产出：
- `CombatConfig` 或同等全局配置 ScriptableObject。
- `SkillDatabase` / `AIDatabase` 或同等索引资产。
- Unity Editor 工具窗口，用于创建、定位、打开和校验相关配置。

验证：
- 新建项目配置时，编辑器窗口能自动创建缺失的全局配置资产。
- 能从统一窗口打开单个技能和 AIProfile。
- 修改配置资产后，运行时行为随配置变化，不需要改代码。

关联问题：
- `Q-0023` 数据目录是否新建。
- `Q-0025` 第一阶段技能池是否确认。

## 2. 抽出 `SkillDefinition` 基础结构

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 建立技能系统的静态数据结构，让攻击、治疗、buff、debuff 都能由配置表达。
- 后续角色添加技能、敌方 AI 枚举技能候选、编辑器管理技能，都以 `SkillDefinition` 资产为来源。

确认方案：
- 新增 `SkillDefinition` ScriptableObject。
- 首批字段包括：`id`、`displayName`、`description`、`apCost`、`cooldown`、`targetType`、`range`、`effects`、`skillTags`、`aiTags`、`aiBaseWeight`。
- `effects` 引用独立的 `EffectDefinition` ScriptableObject 资产，不使用 `[SerializeReference]` 内嵌多态数据，也不把 Effect 仅作为 `SkillDefinition` 子资产。独立 Effect 资产可被多个 `SkillDefinition` 复用。
- 创建 `Assets/Data/Skills/`，用于保存技能配置资产。
- 创建 `Assets/Data/Effects/`，用于保存可复用 Effect 配置资产。
- 第一阶段只确定最小技能配置资产，不急着实现完整编辑器窗口；后续编辑器窗口基于这些资产做统一管理。

产出：
- [x] Assets/Scripts/Combat/Skills/SkillDefinition.cs`
- [x] Assets/Scripts/Combat/Skills/SkillTargetType.cs`
- [x] Assets/Scripts/Combat/Skills/AISkillTag.cs`
- [x] Assets/Data/Skills/
- [x] Assets/Data/Effects/

验证：
- Unity 编译无错误。
- 可以在 Project 窗口创建 `SkillDefinition` 资产。
- 可以在 Project 窗口创建 `EffectDefinition` 派生资产，并在多个 `SkillDefinition` 中复用同一个 Effect 资产。

关联问题：
- `Q-0023` 数据目录是否新建。
- `Q-0025` 第一阶段技能池是否确认。

## 3. 后续把基础攻击改成 `basic_attack`

决议状态：已确认

实现状态：部分资产已存在；主动释放方式暂缓，待 AI 框架大体跑通后继续技能设计

目标：
- 后续让玩家和敌方都通过同一套技能流程执行基础攻击。
- 后续移除玩家/敌方各自维护的硬编码攻击参数。
- 当前阶段不要求玩家点击敌方单位触发 `basic_attack`；玩家角色只要求具备地面移动能力。

确认方案：
- 创建 `basic_attack` 技能资产。
- `apCost = 1`、`range = 1.5m`、固定伤害 20；这些数值只属于 `basic_attack` 配置资产，后续可直接改配置调整。
- 现有点击敌人攻击不再作为当前阶段要求；主动攻击释放方式（技能栏、快捷键、点击目标、或其他交互）待后续技能设计确认。
- 后续可战斗角色通过自身 `SkillExecutor.availableSkills` 配置 `basic_attack`，不通过全局管理器补发技能。
- `basic_attack` 使用自身配置的 `range` 判断攻击距离，而不是使用单独硬编码的攻击范围。
- 若 `CurrentAP >= basic_attack.apCost + 移动所需 AP`，并且角色能通过 NavMesh 移动到 `basic_attack.range` 内，则执行“移动到攻击距离内 + 攻击”。
- 若本回合无法走到 `basic_attack.range` 内，则不攻击，只使用剩余可移动 AP 沿路径向目标靠近。
- 若已经在 `basic_attack.range` 内，则只消耗 `basic_attack.apCost` 并执行攻击。

产出：
- `basic_attack` 技能资产。
- 后续玩家攻击逻辑改为调用技能执行器。
- 后续敌方 AI 普攻逻辑改为调用技能执行器。

验证：
- 后续主动攻击释放方式确定后，玩家能通过该方式移动到攻击范围并攻击。
- 后续 AI 框架接入后，敌方 AI 能通过技能候选移动到攻击范围并攻击。
- AP 扣除、伤害、死亡处理与当前 MVP 行为一致。

关联问题：
- `Q-0005` MaxAP 与基础攻击 AP 消耗。

## 4. 实现最小 `Effect` 系统

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 技能不直接写死效果，而是通过 Effect 列表执行。
- 明确 `SkillDefinition` 可以持有多个 Effect，运行时按顺序执行。
- 保持持续性效果的概念统一：技能通过 Effect 添加 Status，Status 再在回合/时间节点触发持续效果。

确认方案：
- 第一阶段只实现 `DamageEffect`，满足 `basic_attack` 纵切片。
- `HealEffect` 与 `AddStatusEffect` 先在概念和接口上预留，等 `minor_heal` / `guarding_shout` 时再实现。
- Effect 使用独立 ScriptableObject 资产承载静态配置，路径为 `Assets/Data/Effects/`。
- `SkillDefinition.effects[]` 存储独立 Effect 资产引用；不要在运行时修改 Effect 资产状态。
- `Effect` 是一次性执行的结果单元，例如伤害、治疗、添加状态。
- `Status` 是挂在单位身上的持续性运行时状态，例如中毒、持续恢复、防御提高。
- 持续恢复、持续毒伤等不直接写在技能里；技能通过 `AddStatusEffect` 添加 Status，Status 在回合开始或实时 tick 时触发自己的 Effect。
- 第一阶段不做眩晕、魅惑、改变先攻顺序等强控制。

产出：
- `EffectDefinition` 基类或接口。
- `DamageEffectDefinition`。
- `HealEffectDefinition` 与 `AddStatusEffectDefinition` 的后续扩展位置。
- 至少一个可复用的 `DamageEffectDefinition` 资产，例如 `basic_attack_damage`。

验证：
- `basic_attack` 可通过 `DamageEffect` 造成伤害。
- `SkillDefinition.effects[]` 可以配置并执行至少一个效果。
- 同一个 `DamageEffectDefinition` 资产可以被多个技能引用，且运行时不会把单位状态写回资产。
- 后续 `minor_heal` 可通过新增 `HealEffect` 恢复 HP。
- 后续持续效果可通过 `AddStatusEffect` + Status 触发 Effect 实现。

关联问题：
- `Q-0004` 先攻值是否可被技能/状态修改。
- `Q-0015` 命中是否需要检定。
- `Q-0016` 暴击 / 闪避机制。

## 5. 增加 `SkillExecutor`

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 统一处理技能释放流程，避免玩家和 AI 重复实现 AP、范围、目标、冷却校验。
- `SkillExecutor` 应作为可挂载组件存在于释放技能的对象上，而不是只写在敌方 AI 或全局管理器里。
- 技能目标不应限定为 `EnemyController`；木桶、墙壁、机关等可交互物体只要挂载 `SkillExecutor`，也可以成为技能目标。

确认方案：
- 新增 `SkillExecutor` 或同等职责组件。
- 玩家、敌方、以及后续其他可释放技能或可被技能影响的对象，都可以持有自己的 `SkillExecutor`。
- 不额外引入 `ISkillTarget` / `SkillTarget` 目标声明层。
- 目标合法性由 `SkillExecutor` 统一判断：如果一个对象挂了 `SkillExecutor`，它就默认是技能系统可识别、可影响的对象。
- 敌人、玩家、木桶、墙壁、机关等对象都可以通过挂载 `SkillExecutor` 成为合法技能目标。
- `SkillExecutor` 可以通过自身配置声明可承受的效果类型，例如可被伤害、可被治疗、可添加 Status、可互动或可破坏。
- 执行流程：
  1. 检查技能、施法者、目标是否合法。
  2. 检查 AP、冷却、目标阵营、范围。
  3. 若目标不在范围内，允许先移动到可释放位置。
  4. 扣除移动 AP 与技能 AP。
  5. 应用 Effects。
  6. 记录冷却。

产出：
- `SkillExecutor`
- `SkillCastResult`
- 可供 AI 调试使用的失败原因。
- `SkillExecutor` 上用于描述目标承受能力的最小配置。

验证：
- 后续主动攻击释放方式确定后，玩家和敌方都通过同一入口释放 `basic_attack`。
- AP 不足、目标死亡、路径失败时能安全失败并输出原因。
- `basic_attack` 不再只能作用于 `EnemyController`；只要木桶、墙壁等对象挂载 `SkillExecutor` 并允许承受伤害，后续就可以被技能影响。

关联问题：
- `Q-0017` 寻路方案。
- `Q-0008` 移动过程是否可被打断。

## 5.1 抽出共享移动与范围解析

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 后续玩家主动攻击、敌方 AI 追击、`SkillExecutor` 自动移动进范围，必须复用同一套 NavMesh 解析逻辑。
- 避免 `MoveInputController` 与 `EnemyTurnRunner` 各自维护“进入攻击范围点”“AP 是否够”“无法攻击时靠近目标”等重复规则。

确认方案：
- 新增 `CombatMovementResolver`、`SkillRangeResolver` 或同等职责的纯逻辑类。
- 该类负责：
  - 从 caster 位置采样 NavMesh 起点。
  - 计算目标是否已在 `skill.range` 内。
  - 计算进入 `skill.range` 的可释放位置。
  - 计算移动路径长度与移动 AP 预估。
  - 判断 `CurrentAP >= skill.apCost + moveApCost`。
  - 在无法进入技能范围时，为允许追击退化的技能生成 fallback 靠近点。
  - 复用现有软占位检查，避免多个 AI 选择同一终点。
- `MoveInputController` 只负责玩家输入和预览；释放技能时交给 `SkillExecutor`。
- `EnemyTurnRunner` 只负责选择 AI 候选；候选移动可行性由共享 resolver / executor 判断。

产出：
- 共享移动/范围解析类。
- 表达解析结果的数据结构，例如 `SkillPositioningResult`。
- 失败原因枚举或字符串，供玩家输入、AI debug 和配置校验复用。

验证：
- 后续主动攻击释放方式确定后，玩家攻击预览/执行使用的攻击范围和 AP 判断一致。
- 敌方 AI 使用 `basic_attack` 时，移动进范围与当前 MVP 行为基本一致。
- 同一技能的玩家与 AI 行为不会因为各自硬编码而出现不同 AP 或范围判断。

## 5.2 明确技能运行时状态与冷却

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- `SkillDefinition` 与 `EffectDefinition` 只保存静态配置，不能保存单位运行时状态。
- 同一个技能资产挂给多个单位时，每个单位的冷却互不影响。

确认方案：
- 在 `SkillExecutor` 内维护运行时技能状态，或新增 `SkillRuntimeState` / `SkillCooldownTracker`。
- 运行时状态至少包含：
  - 每个技能当前剩余冷却。
  - 本单位可用技能列表。
  - 最近一次释放失败原因（可选，用于 debug）。
- 回合开始或回合结束时推进冷却；第一阶段按回合推进，不实现探索实时冷却。
- 任何 cooldown 值都来自 `SkillDefinition.cooldown`，运行时只记录剩余值，不写回资产。

产出：
- `SkillRuntimeState` / `SkillCooldownTracker` 或 `SkillExecutor` 内部等价实现。
- 回合推进冷却的调用点。
- `SkillCastResult` 中包含冷却失败原因。

验证：
- 两个单位引用同一个 `basic_attack` 或后续同一个技能资产时，冷却状态互不污染。
- 技能释放后进入冷却；冷却未结束时释放失败并返回明确原因。
- `cooldown = 0` 的 `basic_attack` 可以每次有 AP 时使用。

## 5.5 建立游戏 Tag 系统

决议状态：已确认

实现状态：已完成（2026-05-10）

	已完成内容：
	- GameplayTag readonly struct：IsValid(string) 校验（空值/前后点/双点/空白符）、大小写不敏感等值比较、==/!= 运算符、string->tag 隐式转换、Matches(TagMatchMode Exact/Prefix)
	- GameplayTagRef 可序列化包装：IsValid / TryGetTag / ToTag、string->ref 和 ref->tag 隐式转换
	- GameplayTagSet 运行时容器：Add(source追踪) / Remove(按source或全删) / RemoveAllFromSource / Clear、Has/HasAny/HasAll(Exact/Prefix)
	- TagQuery 条件查询：RequiredAll / RequiredAny / BlockedAny + TagMatchMode
	- TagRegistry ScriptableObject + TagEntry：全局 Tag 注册与查询
	- GameplayTagComponent MonoBehaviour：挂载 GameObject 提供运行时 TagSet
	- CombatConfigWindow EditorWindow：统一配置入口（Tags/Skills/Effects/Statuses/AIProfiles/Validation 标签页）
	- TagMatchMode enum（Exact/Prefix）、TagSourceType enum
	- 52 个 EditMode 单元测试全部通过（Assets/Tests/Editor/GameplayTags/GameplayTagTests.cs）


目标：
- 建立贯穿整个游戏的通用 Tag 机制，服务技能、Effect、Status、AI、关卡脚本和条件判断。
- 让技能和状态之间通过数据化 Tag 交互，而不是依赖硬编码类型判断。
- 将 Tag 调整为后续设计第一原则：新增条件、事件、AI 决策、技能效果和跨系统语义时，优先考虑是否能用 Tag 完成。
- 详细设计见 `Docs/09_GAMEPLAY_TAG_SPEC.md`。

确认方向：
- Tag 使用点分层级格式，例如 `Element.Fire.Burning`、`State.Poisoned`、`Faction.Undead`。
- Tag 支持完整匹配与前缀匹配。
- 技能可以添加 Tag、移除 Tag、检查 Tag。
- 某些技能可以根据目标 Tag 判定无效，例如目标拥有 `State.Immune.Fire` 时火焰技能不生效。
- Effect 和 Status 应尽量关联 Tag，便于查询角色身上的效果或状态。
- AI 可以读取 Tag 作为决策输入。
- 代码中任何传递游戏语义事件的操作，都应优先携带 Tag / TagSet。
- 新增布尔字段、专用 enum、专用事件类型前，应先判断是否能用 Tag 表达。
- Tag 不替代 HP、AP、坐标、对象引用等强类型数据；Tag 用于表达玩法语义。
- 第一阶段可以用字符串表达 Tag，但必须集中封装匹配逻辑，避免散落的字符串匹配。

产出：
- [x] GameplayTag 或等价轻量结构。
- [x] GameplayTagSet 或等价运行时容器。
- [x] Exact / Prefix 匹配工具。
- [ ] SkillExecutor 或单位运行时组件上的当前 Tag 查询入口。
- [ ] 事件/debug 输出中携带 Tag 的约定。

验证：
- [x] 能给单位添加、移除、查询 Tag。
- [x] 能用 Element.Fire 匹配 `Element.Fire.Burning`。
- [x] 能用完整匹配区分 `Element.Fire.Burning` 与 `Element.Fire.Resistance`。
- [ ] Status 结束时能移除它带来的 Tag。
- [ ] 一个技能/状态/AI 条件/事件都能通过 Tag 表达至少一层语义。

关联问题：
- `Q-0012` 怪物 AI 实现方案。
- `Q-0025` 第一阶段技能池是否确认。

## 6. 建立 `AIProfile` 原型

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 让敌方按职业/定位产生不同倾向，而不是所有敌人都只追最近玩家。
- AIProfile 必须保持数据驱动，能够利用 Tag 系统区分技能、目标、状态和战术条件。

确认方案：
- 新增 `AIProfile` ScriptableObject。
- 第一阶段只支持三类 role：
  - `Aggressive`
  - `Support`
  - `Healer`
- role 只是默认倾向，不应成为硬编码分支。
- 通过数据化权重影响技能选择，例如：
  - `skillTagWeights`：根据技能 `aiTags` 或 GameplayTag 加权。
  - `targetTagWeights`：根据目标身上的 GameplayTag 加权。
  - `statusTagWeights`：根据目标或友方身上的 Status Tag 加权。
  - `healHpThreshold`：治疗倾向阈值。
- AIProfile 后续由统一 Combat Config 编辑器管理。
- 暂时不做复杂可视化行为树编辑器。

产出：
- `AIProfile`
- `AIRole`
- Tag 权重配置结构。
- 可挂到敌方或怪物定义上的 AIProfile 引用字段。

验证：
- Aggressive 更倾向伤害技能。
- Support 更倾向 buff / protect 标签技能。
- Healer 在友方低血量时更倾向治疗技能。
- 配置目标 Tag 权重后，AI 能优先选择带有特定 Tag 的目标。

关联问题：
- `Q-0012` 怪物 AI 实现方案。

## 7. 让 `EnemyTurnRunner` 枚举技能候选

决议状态：已确认

实现状态：待后续实现

审查迁入：
- `CR-0002` 合并到本步骤处理。原因：当前 AI 框架尚未接入，敌方移动通过 `basic_move` 技能、敌方攻击/治疗/支援技能候选、以及“移动后施放技能”的组合逻辑需要在 AI 候选框架中一起设计。
- 本步骤实现前不要单独为了 `CR-0002` 修改 `EnemyTurnRunner` 的局部追击逻辑，避免在正式 AIActionCandidate 框架外再制造一套临时分支。

目标：
- 敌方 AI 不再固定“追最近玩家 + 普攻”，而是能根据技能、目标和职责评分。
- 将 AI 决策拆成候选生成、可用性过滤、评分、执行和 debug 输出，避免把复杂逻辑堆在 `EnemyTurnRunner` 中。
- AI 框架必须同时处理技能与移动搭配：`basic_move` 是候选行动的一部分，不是攻击失败后的底层兜底移动。

确认方案：
- 新增 `AIActionCandidate`。
- 每个候选包含：技能、目标、移动点、评分、失败原因。
- 初始评分公式保持简单：
  - 技能基础权重
  - AIProfile 职责加成
  - 目标血量/友方低血量加成
  - 距离与 AP 效率
  - 是否能本回合释放
- 评分应读取 `SkillDefinition`、`AIProfile`、GameplayTag、Status Tag、AP、冷却、距离和路径结果。

详细计划：

1. 输入上下文
   - 当前行动敌人。
   - 敌人自己的 `SkillExecutor`。
   - 敌人当前技能列表。
   - 场上所有可被技能系统识别的对象，也就是挂载 `SkillExecutor` 的对象。
   - 当前战斗轮次、阵营信息、AP、冷却状态。
   - 敌人挂载的 `AIProfile`。

2. 候选生成
   - 遍历敌人可用技能。
   - 根据技能 `targetType` 生成目标集合：
     - `Self`：自己。
     - `SingleEnemy`：敌对阵营目标。
     - `SingleAlly`：友方目标。
     - `GroundPoint` / `Area`：第一阶段可先跳过或只生成占位失败候选。
   - 对每个“技能 + 目标”生成一个 `AIActionCandidate`。

3. 可用性过滤
   - 检查技能是否为空、目标是否为空、目标是否仍有效。
   - 检查 AP 是否至少能支付技能 AP。
   - 检查冷却是否结束。
   - 检查目标阵营是否符合 `targetType`。
   - 检查目标 Tag 是否使技能无效，例如免疫类 Tag。
   - 检查技能是否能在本回合直接释放，或能否通过移动进入范围。
   - 对不可用候选保留失败原因，用于 debug；不可用候选不参与最终选择。

4. 移动与范围评估
   - 若目标已在技能范围内，移动消耗为 0。
   - 若目标不在范围内，使用 NavMesh 计算进入 `skill.range` 的可释放点。
   - 若 `CurrentAP >= skill.apCost + moveApCost`，候选标记为“本回合可释放”。
   - 若无法进入范围，但技能允许追击退化，例如 `basic_attack`，则生成“靠近目标”的 fallback 候选。
   - fallback 候选只移动，不释放技能，评分低于可释放候选。
   - 所有移动候选必须引用行动单位自身的 `basic_move` 或等价移动技能，并通过 `SkillExecutor.Execute(SkillExecutionContext.ForGroundPoint(...))` 执行，不能直接调用 `EnemyController.TryMove`。

5. 评分
   - 基础分来自 `SkillDefinition.aiBaseWeight`。
   - 根据 `AIProfile.skillTagWeights` 加成技能 Tag。
   - 根据 `AIProfile.targetTagWeights` 加成目标 Tag。
   - 根据 `AIProfile.statusTagWeights` 加成目标或友方 Status Tag。
   - 根据 `AIProfile.role` 给出默认倾向，但 role 不能写成硬编码行为。
   - 治疗技能根据目标 HP 百分比与 `healHpThreshold` 加权。
   - 伤害技能可以根据目标低 HP、距离、可击杀可能性加权。
   - 支援技能可以根据友方威胁值、是否开场、是否关键单位加权。
   - AP 效率影响评分：同等价值下，消耗更少 AP 或本回合可释放的候选更优。
   - 距离影响评分：能直接释放优于需要长距离移动；无法释放只靠近的评分更低。

6. 选择
   - 从可用候选中选择最高分。
   - 若最高分低于最低行动阈值，可以选择 fallback 靠近目标或结束回合。
   - 分数相同时使用稳定规则，例如更高技能权重、更近目标、更低目标 HP。

7. 执行
   - `EnemyTurnRunner` 不直接应用效果。
   - `EnemyTurnRunner` 只把最终候选交给敌人自身的 `SkillExecutor`。
   - `SkillExecutor` 负责移动、扣 AP、执行 Effect、记录冷却。
   - 如果候选需要“先移动再施放技能”，执行顺序为：执行自身移动技能 -> 等待移动完成 -> 重新校验目标技能 -> 执行目标技能。
   - 若执行时目标死亡、路径失效或状态改变导致失败，可允许重新评估一次；仍失败则结束回合。

8. Debug 输出
   - 输出候选数量。
   - 输出每个候选的技能、目标、是否可用、失败原因、评分组成。
   - 输出最终选择原因。
   - 输出 fallback 触发原因。
   - Debug 应可开关，避免正式测试刷屏。

产出：
- `AIActionCandidate`
- `AIActionEvaluator`
- `EnemyTurnRunner` 改为执行最高分候选。
- AI 决策 debug 结构。

验证：
- 只有 `basic_attack` 时，行为与当前 MVP 基本一致。
- 加入治疗或 buff 技能后，敌方能在合理条件下选择非攻击行动。
- Tag 权重配置变化后，AI 目标选择或技能选择随配置变化。
- AI 不再依赖硬编码“最近玩家 + 普攻”作为唯一策略。
- 禁用 `Action.Move` 后，AI 不会通过底层移动绕过限制；若还能使用非移动技能，则仍可正常选择。

关联问题：
- `Q-0012` 怪物 AI 实现方案。
- `Q-0025` 第一阶段技能池是否确认。

## 8. 创建第一批验证技能

决议状态：已确认

实现状态：待后续实现

目标：
- 用少量技能验证 AI 配合，而不是一次性做完整技能池。
- 每个 Effect 必须配置至少一个 GameplayTag，用于标记效果类型。

确认方案：
- 第一批按顺序实现：
  - `basic_attack`
  - `minor_heal`
  - `guarding_shout`
- 第二批再实现：
  - `power_strike`
  - `crippling_hex`
- `basic_attack` 后续用于验证 `SkillDefinition`、`DamageEffect`、`SkillExecutor` 和 AI 普攻；玩家主动攻击释放交互待后续技能设计确认。
- `minor_heal` 用于验证友方目标、治疗 Effect 和治疗型 AI。
- `guarding_shout` 用于验证 Status、Tag、buff 和支援型 AI。
- 每个 Effect 都必须带 Tag，例如：
  - `DamageEffect`：`Effect.Damage.Physical` 或 `Effect.Damage.BasicAttack`。
  - `HealEffect`：`Effect.Heal.Direct`。
  - `AddStatusEffect`：`Effect.Status.Add`，并由 Status 自身提供具体状态 Tag。

产出：
- 三个可用技能资产。
- 可挂给不同敌人的技能列表。
- 每个技能下的 Effect 都带有合法 Tag。

验证：
- 攻击型敌人能攻击。
- 支援型敌人能给友方加 buff。
- 治疗型敌人在友方低血量时能治疗。
- 配置检查能发现没有 Tag 的 Effect。

关联问题：
- `Q-0025` 第一阶段技能池是否确认。

## 8.5 增加技能与 Effect 配置校验

决议状态：已确认

实现状态：已完成（2026-05-10）

目标：
- 让后续 Agent 新增技能或 Effect 后，能通过明确校验发现配置遗漏，而不是在运行时猜错。
- 校验重点服务本轮战斗内闭环，不做完整 Combat Config 编辑器。

确认方案：
- 第一阶段提供菜单命令或轻量编辑器工具，例如 `MiniChess/Validate Combat Config`。
- 校验范围包括：
  - `SkillDefinition.id` 非空。
  - `SkillDefinition.id` 在当前项目内唯一。
  - `apCost >= 0`。
  - `cooldown >= 0`。
  - `range >= 0`。
  - `effects` 不为空。
  - 每个 Effect 引用不为空。
  - 每个 `EffectDefinition` 至少配置一个 GameplayTag。
  - `basic_attack` 技能资产存在。
  - `basic_attack` 引用了有效 `DamageEffectDefinition`。
  - 单位或测试敌人缺少默认技能时输出明确警告。
- 校验只报告问题，不自动修复配置，除非用户明确要求。

产出：
- 配置校验入口。
- 清晰的 Console 输出，包含资产路径、字段名和错误原因。

验证：
- 删除 Effect Tag 后，校验能报出具体 Effect 资产。
- 重复 `SkillDefinition.id` 后，校验能列出冲突资产。
- 后续进入基础攻击纵切片时，缺少 `basic_attack` 或其 DamageEffect 时，校验能明确指出阻塞项。

## 9. 增加 AI 调试输出

决议状态：已确认

实现状态：待后续实现

目标：
- 每次敌方行动都能解释“为什么选这个行动”。
- 输出尽量详细，方便后续 Agent 调试 AI、技能配置、Tag 权重和寻路问题。

确认方案：
- 第一阶段使用 Unity Console 日志，后续可接入 Combat Config 编辑器开关。
- 输出候选技能、目标、评分、失败原因、最终选择。
- 后续需要时再做场景内 Debug HUD。
- Debug 输出应结构化，至少包含：
  - 行动单位名称 / id。
  - 当前回合、当前 AP、可移动距离。
  - AIProfile 名称、role。
  - 参与评估的技能列表。
  - 候选数量。
  - 每个候选的技能、目标、目标 Tag、技能 Tag / aiTags。
  - AP 校验结果、冷却校验结果、范围校验结果、路径校验结果。
  - 移动目标点、预计移动 AP、是否本回合可释放。
  - 各评分项明细，例如 base、skillTag、targetTag、hpUrgency、distance、apEfficiency、tacticalBonus。
  - 候选总分。
  - 不可用候选的失败原因。
  - 最终选择与选择原因。
  - fallback 行为触发原因。
- Debug 输出必须可以关闭，避免正式测试时刷屏。

产出：
- AI 决策日志。
- 可控的日志开关，避免正式测试时刷屏。
- 后续 Agent 能根据日志定位是技能配置、Tag、AIProfile、AP、冷却、目标选择还是 NavMesh 路径问题。

验证：
- 每个敌方回合能看到候选评分。
- 当 AI 不行动时能看到明确原因，例如 AP 不足、无合法目标、路径失败。
- 仅通过日志可以复盘一次 AI 决策过程。

关联问题：
- `Q-0012` 怪物 AI 实现方案。

## 10. 明确技能 AP 消耗来自技能配置

决议状态：已确认

实现状态：文档已完成，代码待迁移

目标：
- 消除“全局基础攻击消耗”和“技能自身配置消耗”之间的概念混淆。
- 明确 AP 消耗属于技能配置，不应该由全局固定表决定。

当前冲突：
- `OPEN_QUESTIONS.md` 的 `Q-0005` 临时假设为 `BasicAttackCost = 2`。
- 当前 MVP 实现与 `Docs/02_SYSTEM_SPEC.md` §11 中的基础攻击为 `1 AP`。
- `Docs/05_SKILL_SPEC.md` 已说明技能 AP 消耗由技能自身配置决定。

确认方案：
- 第一阶段只确认 `MaxAP = 6` 作为 MVP 单位默认 AP。
- 删除“全局 BasicAttackCost”概念，基础攻击消耗改为 `basic_attack.apCost`。
- `basic_attack.apCost = 1` 只作为 `basic_attack` 技能资产的示例配置，不代表所有基础攻击或所有技能必须固定消耗 1 AP。
- 后续技能全部通过 `SkillDefinition.apCost` 单独配置，例如治疗、buff、重击、debuff 都可以有不同 AP 消耗。

产出：
- 更新 `Q-0005` 决议。
- 更新 `Docs/02_SYSTEM_SPEC.md` 中 AP 表格，移除或改写 `BasicAttackCost = 2 AP?` 这种全局固定消耗描述。
- 确保 `Docs/05_SKILL_SPEC.md` 继续作为技能 AP 消耗规则的来源。

验证：
- 文档不再出现“所有基础攻击固定消耗全局 AP 表”的误导。
- `basic_attack` 的 AP 消耗来自技能资产配置。
- 玩家攻击、敌方攻击都读取同一个 `basic_attack.apCost`。

关联问题：
- `Q-0005` MaxAP 与基础攻击 AP 消耗。

## 推荐执行顺序

1. 收口当前 AI 调试开关。
   - `enemyFirstForDebug` 默认值改为 `false`。
   - Inspector 注释说明它只用于调试，不影响正式 Initiative 规则。
2. 完成 GameplayTag 运行时闭环。
   - `GameplayTag`
   - `GameplayTagSet`
   - Exact / Prefix 匹配
   - Tag 来源追踪
   - 基础配置校验
3. 完成 TagRegistry 与编辑器第一版。
   - 创建 `GameplayTagRegistry.asset`。
   - 支持 Tag 创建、搜索、分组浏览。
   - 支持未注册 Tag 检查。
   - 支持 Effect 缺失 Tag 检查。
   - 在 Combat Config 中提供 Tags / Validation 入口。
4. 建立 Tag First 代码使用约定。
   - 事件/debug 输出携带 Tag / TagSet。
   - 新增条件判断前先检查能否使用 Tag。
   - 禁止散落字符串前缀判断。
5. 实现 `SkillDefinition` 与独立 Effect 资产的最小结构。
   - 技能字段必须支持 Tag / aiTags / effects。
   - Effect 使用独立 ScriptableObject 资产，可被多个技能复用。
   - `SkillDefinition.effects[]` 引用独立 Effect 资产。
6. 实现配置校验的最小入口。
   - 检查技能 id、AP、range、Effect 引用、Effect Tag、`basic_attack` 是否存在。
   - 校验入口挂到 Combat Config 的 Validation 页。
7. 抽出共享移动与范围解析。
   - 玩家攻击与 AI 攻击复用同一套 NavMesh 进入范围、AP 预估和 fallback 靠近逻辑。
8. 实现 `DamageEffect` 与 `SkillExecutor` 的最小路径。
   - `SkillExecutor` 使用 Tag 判断目标语义。
   - `SkillCastResult` / debug 带 Tag。
   - 技能冷却状态保存在单位运行时，不写回 `SkillDefinition`。
9. 将基础攻击迁移为 `basic_attack`。
   - `basic_attack` 和 `DamageEffect` 都必须带 Tag。
   - 玩家点击攻击与敌方普攻都调用 `SkillExecutor`。
10. 实现 `AIProfile`。
   - `skillTagWeights`
   - `targetTagWeights`
   - `statusTagWeights`
11. 让 `EnemyTurnRunner` 枚举技能候选。
   - 候选生成、过滤、评分、debug 都读取 Tag。
12. 增加 AI 调试输出。
   - 日志优先服务 Tag / 配置 / 路径问题定位。
13. 增加 `minor_heal` 与 `guarding_shout`。
    - 验证治疗、Status、Tag、支援型 AI。
14. 根据测试结果再决定是否实现 `power_strike` 与 `crippling_hex`。

## 暂不进入下一阶段的内容

- 战斗触发方式与正式 `CombatTrigger`。
- 胜负判定、战斗结束流程与 `VictoryConditionEvaluator`。
- 战斗奖励、掉落、任务推进、剧情对话。
- Skill / AIProfile 的完整编辑器页。
- 技能树。
- 装备提供技能。
- 大量 VFX / 动画表现。
- 复杂命中检定、暴击、闪避公式。
- 会改变先攻顺序的状态。
- 眩晕、魅惑、强控类状态。
- Behavior Tree / GOAP / 可视化节点编辑器。
