# 07 - 剧情规格 (Story Spec)

> 叙事、对话、任务、过场。**当前几乎全部待定**，本文件先锁框架。

## 1. 世界观

**未定**。需要用户提供：
- 时代背景（中世纪奇幻 / 蒸汽朋克 / 现代 / 科幻 / …）
- 主线一句话概述
- 主要派系 / 阵营

## 2. 叙事载体（候选）

- 对话气泡 / 全屏对话面板
- 过场动画（Timeline）
- 文字日志

## 3. 任务系统（占位）

| 字段 | 说明 |
|---|---|
| `QuestID` | 唯一 ID |
| `Title` | 标题 |
| `Description` | 描述 |
| `Stages` | 多阶段任务步骤 |
| `Rewards` | 奖励（经验 / 物品 / 剧情进度） |

数据载体：`QuestDefinition : ScriptableObject`，位置 `Assets/Data/Quests/`。

## 4. 对话系统

- 候选方案：自研 ScriptableObject 树 / Yarn Spinner / Ink。**待定**。
- 关键需求：
  - 多分支
  - 检定（属性骰子）影响选项可用性？—— 参考 BG3
  - 角色之间的"队员闲谈"

## 5. 触发与状态

- 全局剧情进度：建议用一个 `StoryFlags` ScriptableObject 单例。
- 触发器：`StoryTrigger` 组件挂在场景物体上。

## 6. 待决问题

参见 `OPEN_QUESTIONS.md` 的"剧情/叙事"段落。
