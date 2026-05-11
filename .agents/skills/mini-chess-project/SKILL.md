---
name: mini-chess-project
description: "Project-specific guidance for working in D:\\SandBox\\MiniChess, the Unity tactics game 代号：战旗 / MiniChess. Use whenever confirming requirements, updating Docs, recording OPEN_QUESTIONS, changing Unity scripts/scenes/assets, creating reusable Codex skills from repeated workflows, or implementing BG3/Divinity-style no-grid SRPG mechanics in this project. Triggers: MiniChess, 代号战旗, 代号：战旗, 战旗, this project, 当前项目, Docs, OPEN_QUESTIONS, skill化, Unity tactics, SRPG, no-grid tactics, initiative AP combat."
---

# MiniChess Project

Use this skill before requirement clarification, documentation work, or Unity implementation in `D:\SandBox\MiniChess`.

## First Steps

1. Read `D:\SandBox\MiniChess\AGENTS.md`.
2. Read the current relevant files in `D:\SandBox\MiniChess\Docs\`.
3. Check `D:\SandBox\MiniChess\Docs\OPEN_QUESTIONS.md` before resolving unclear requirements.
4. If Unity Editor interaction is needed, load `unity-skills` and use its helper/API instead of hand-editing `.unity`, `.prefab`, or `.meta` text.

## Requirement Workflow

When confirming, refining, or implementing a requirement:

1. Treat `Docs/*.md` as the design source of truth.
2. If a detail is unclear, add or update a `Q-XXXX` entry in `Docs/OPEN_QUESTIONS.md`.
3. Do not silently turn temporary assumptions into final design.
4. If a temporary assumption is needed to keep a prototype moving, record it under the matching open question.
5. If the user answers a question, update the existing `Q-XXXX` decision instead of creating a duplicate.

Use the existing `OPEN_QUESTIONS.md` format:

```markdown
### Q-XXXX 标题
- 来源：哪个文档 / 哪次对话
- 问题：...
- 当前临时假设：...
- 决议：(空) / 用户回复
- 影响：列出会被影响的文件
```

## Game Constraints

- Build a 45-degree top-down 3D no-grid tactical RPG prototype, not chess/FPS/runner/roguelike/MOBA.
- Use BG3 / Divinity: Original Sin 2 as style references.
- Keep camera fixed at 45 degrees. Allow zoom only. Do not add rotation, first-person, or free-fly controls.
- Use click-to-ground movement with NavMesh.
- Keep combat in the same scene; do not switch to a separate battle scene.
- Player party size is not hard-capped by design; prototype UI shortcuts may expose only a subset of quick-select keys.
- Sort turn order by initiative, higher first.
- Determine initiative when combat starts and keep it stable during combat unless an open question is resolved otherwise.
- Use AP for movement, attacks, and skills.
- Calculate movement budget as `canWalkDistance = CurrentAP * MoveSpeed`.
- Let each skill define its own cost.

## Implementation Discipline

- Keep changes small and runnable.
- Work one system at a time.
- Treat placeholder scripts as scaffolding, not design truth.
- Do not rewrite `Assets/Scripts/` wholesale.
- Follow `Docs/10_CODE_STYLE.md` before adding or changing C# code; default to Microsoft's C# coding conventions unless local project guidance is more specific.
- Add `TODO` comments for temporary values or prototype behavior, referencing a `Docs/` section or `Q-XXXX`.
- When changing a system interface or design-facing field, update the relevant `Docs/0x_*.md` in the same work session.

## NavMesh Workflow

For scene navigation work:

1. Ensure the scene has a root `[NavMeshSurface]` GameObject with a `NavMeshSurface` component.
2. Do not use Unity Skills `navmesh_bake` for this project; it calls the legacy `UnityEditor.AI.NavMeshBuilder.BuildNavMesh()` path and can revive old scene-level NavMesh data.
3. Rebuild through `MiniChess/NavMesh/Rebuild Surface NavMesh`, which clears legacy `NavMeshSettings.m_NavMeshData`, bakes `[NavMeshSurface]`, creates `Assets/Scenes/SampleScene/NavMesh-[NavMeshSurface].asset`, and saves the scene.
4. Verify bake results with `navmesh_sample_position` near `player1` and at representative map points.
5. Verify at least one `navmesh_calculate_path` query returns `PathComplete` from sampled NavMesh positions, not from the player transform's capsule-center height.

## Skill Creation Habit

When a repeated operation, project rule, checklist, domain note, or fragile workflow would help future sessions:

1. Prefer creating or updating a Codex skill under `C:\Users\chenyifei\.codex\skills\`.
2. Use a separate, clearly named folder for each focused skill.
3. Update an existing matching skill instead of creating a duplicate.
4. Keep the skill concise and load only essential instructions.
5. Use `skill-creator` guidance for new skills or substantial revisions.
6. Report the created or updated skill path when done.

Preferred MVP order:

1. Camera zoom-only lock.
2. Exploration click-to-ground movement.
3. Party selection for player-controlled characters.
4. `CombatManager` state machine: `Exploration -> InitiativeRoll -> TurnLoop -> End`.
5. AP reset and AP movement spending.
6. Basic attack.
7. Minimal enemy AI: nearest player, move into range, attack.
8. Same-scene `CombatTrigger`.
9. First `SkillDefinition` ScriptableObject and one concrete skill.

Avoid skill trees, equipment, story dialogue, save/load, polished UI art, and effects before the first skill is playable.

## Reporting

When finishing implementation work, report:

- Files changed.
- What was implemented.
- How it was verified.
- Open questions or temporary assumptions, with `Q-XXXX` references.
