# Project Agent Instructions

## Project Identity

This project is the Unity tactics game "代号：战旗" / MiniChess. Treat it as a BG3/Divinity-style no-grid tactical RPG prototype, not as chess, FPS, roguelike, runner, or MOBA.

Core constraints:

- 45-degree top-down 3D camera.
- Camera may zoom in/out only; do not add rotation, first-person, or free-fly camera controls.
- Movement is no-grid click-to-ground movement using NavMesh.
- Combat happens in the same scene, without battle scene switching.
- Player party supports up to 4 characters.
- Turn order is based on initiative, higher value first.
- Initiative is determined on combat entry and does not dynamically change during combat unless `Docs/OPEN_QUESTIONS.md` records a resolved decision allowing it.
- AP is the action resource for movement, attacks, and skills.
- Movement budget follows `canWalkDistance = CurrentAP * MoveSpeed`.
- Skill costs are defined by each skill, not by a global fixed table.

## Implementation Discipline

- Keep each change small and runnable. Prefer one system at a time: camera, movement, party, combat state machine, AP, attack, AI, trigger, then first skill.
- Do not rewrite `Assets/Scripts/` wholesale.
- Treat existing placeholder scripts as scaffolding, not final design.
- Use Unity Skills for Unity Editor operations. Do not manually edit `.unity`, `.prefab`, or `.meta` files as text.
- After changing Unity scripts or assets, refresh/compile through Unity and verify via Unity Console or Play mode when practical.
- When using temporary values, placeholder art/audio, or prototype-only behavior, add a `TODO` comment that references the relevant `Docs/` section or `Q-XXXX`.
- When adding or changing system interfaces or design-facing fields, update the corresponding `Docs/0x_*.md` in the same work session.

## Code Style

- All future agents must follow `Docs/10_CODE_STYLE.md` before adding or changing C# code.
- Unless a local document or existing file pattern explicitly says otherwise, use Microsoft's C# coding conventions as the default style baseline.
- Prefer consistency with nearby project code when it is stricter or more specific than the baseline.

## Skill Creation Habit

If a workflow, rule set, domain note, validation checklist, or repeated operation appears reusable across future work:

- Prefer turning it into a Codex skill under `C:\Users\chenyifei\.codex\skills\`.
- Create a separate, clearly named skill folder instead of mixing unrelated workflows together.
- Keep the skill concise and focused; include only instructions and resources that another Codex session would actually need.
- If a matching skill already exists, update it instead of creating a duplicate.
- Use the `skill-creator` guidance when creating or substantially updating skills.
- Mention the new or updated skill path in the final report.

## Recommended MVP Order

1. Lock camera rotation and keep zoom only.
2. Add click-to-ground movement in exploration.
3. Add party selection for up to 4 characters.
4. Add `CombatManager` state machine: `Exploration -> InitiativeRoll -> TurnLoop -> End`.
5. Add AP reset and AP movement spending.
6. Add basic attack.
7. Add minimal enemy AI: nearest player, move into range, attack.
8. Add same-scene `CombatTrigger`.
9. Add first `SkillDefinition` ScriptableObject and one concrete skill.

Avoid skill trees, equipment, story dialogue, save/load, polished UI art, and effects before the first skill is playable.

## Requirement Clarification

Before confirming, refining, or implementing any requirement in this project:

1. Read the current contents of `Docs/`.
2. Treat the `Docs/*.md` files as the source of truth for design intent.
3. Check `Docs/OPEN_QUESTIONS.md` before making decisions about unclear design details.

## Open Questions

When a requirement, design detail, or implementation choice is unclear:

1. Do not silently decide it as final.
2. Add or update an entry in `Docs/OPEN_QUESTIONS.md`.
3. Use the existing `Q-XXXX` numbering and entry format from that file.
4. Include a temporary assumption only when needed to keep a prototype or implementation moving.
5. Mention any affected files or systems in the `影响` field.

If the user answers an open question, update the matching `Q-XXXX` entry with the decision instead of creating a duplicate.

## Reporting

When completing implementation work, report:

- Files changed.
- What was implemented.
- How it was verified.
- Any open questions or temporary assumptions, with `Q-XXXX` references when applicable.
