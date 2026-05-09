# 06 - 地图规格 (Map Spec)

> 地图同时承担**探索**和**战斗**（不切场景）。

## 1. 场景组织

- 一张地图 = 一个 Unity Scene（候选方案；也可以用 Addressables 子场景，待定）。
- 单一地图内允许多个战斗触发区。

## 2. 寻路

- 候选：**Unity NavMesh**（项目已是 Unity 2022.3，原生支持）。
- 战斗与探索**共享同一套 NavMesh**，避免双重维护。
- 每个可寻路场景应保留一个根对象 `[NavMeshSurface]`，挂载 `NavMeshSurface` 作为重新烘焙入口；不要只依赖旧式场景级 NavMesh 数据。
- 本项目重新烘焙使用菜单 `MiniChess/NavMesh/Rebuild Surface NavMesh`。不要使用旧 API `UnityEditor.AI.NavMeshBuilder.BuildNavMesh()`，它会写入 `NavMeshSettings` 并与 `NavMeshSurface` 数据混用。
- 动态阻挡：单位本身用 `NavMeshObstacle`（carve）或自定义阻挡层。

## 3. 地图编辑流程（占位）

1. 美术 / 设计师在场景中摆放静态环境（地形、墙、道具）。
2. 烘焙 NavMesh。
3. 放置：
   - 玩家出生点（`PlayerSpawnPoint`）
   - 怪物预置点（`MonsterSpawnPoint` + `MonsterDefinition` 引用）
   - 战斗触发器（`CombatTrigger` 区域）
   - 剧情触发器（`StoryTrigger`）
   - 可交互物（宝箱、NPC、传送点）

## 4. 必备组件占位（程序约定）

```csharp
public class PlayerSpawnPoint : MonoBehaviour { public int slotIndex; }
public class MonsterSpawnPoint : MonoBehaviour { public MonsterDefinition def; }
public class CombatTrigger     : MonoBehaviour { public List<MonsterSpawnPoint> participants; }
```
（实际命名空间 / 字段以后续实现为准）

## 5. 待决问题

- 地图大小上限？相机拉远的最大距离？
- 高度差是否影响战斗（射程 / 视野 / 命中）？
- 破坏性环境（爆桶、可点燃地表）？—— 神界原罪 2 风格
- 室内 / 多层建筑如何处理（楼层切换）？
