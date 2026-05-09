# 代号：战旗 (MiniChess)

45° 俯视 3D 战旗（战术）游戏。Unity 2022.3.53f1c1 + URP。

## 一句话描述

类博德之门 3 / 神界原罪 2 的**无格子**战棋：直接点击地面移动；进入战斗不切场景，原地按先攻值排序回合制；玩家最多操控 4 角色；所有行动消耗行动点（AP）。

## 当前阶段

原型期。占位脚本（Player/EnemySpawner/CameraController）尚未对接战斗系统，等设计文档稳定后逐步替换。

## 文档导航（**开工前必读**）

| 文件 | 内容 |
|---|---|
| `Docs/01_GAME_DESIGN_BRIEF.md` | 核心玩法、视角、战斗触发、回合制规则 |
| `Docs/02_SYSTEM_SPEC.md` | 系统规格：先攻、AP、移动、攻击的具体计算 |
| `Docs/03_CHARACTER_SPEC.md` | 角色属性、成长、装备框架 |
| `Docs/04_MONSTER_SPEC.md` | 怪物属性、AI 行为框架 |
| `Docs/05_SKILL_SPEC.md` | 技能系统、消耗、目标、效果 |
| `Docs/06_MAP_SPEC.md` | 地图编辑、寻路、可阻挡区域 |
| `Docs/07_STORY_SPEC.md` | 剧情、叙事、对话 |
| `Docs/OPEN_QUESTIONS.md` | 所有待用户拍板的设计问题 |

## 项目结构

- `Assets/Scenes/SampleScene.unity` — 当前唯一场景
- `Assets/Scripts/` — 占位代码，待重构
- `.opencode/skills/chess/SKILL.md` — AI 协作规范（每次开工自动加载）

## 协作规范（给 AI / 给人）

参见 `.opencode/skills/chess/SKILL.md`。核心要点：
1. 先读全部 Docs 再动手。
2. 设计不清楚 → 写 `OPEN_QUESTIONS.md`，**不要擅自决定**。
3. 每次提交说明：改了什么 / 实现了什么 / 怎么验证。
