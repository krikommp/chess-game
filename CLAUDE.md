# CLAUDE.md — MiniChess 项目

## 核心规则

### Unity 操作必须通过 Unity Skills
**所有对 Unity Editor 的操作（创建资产、修改场景、运行测试、检查编译等）必须通过 Unity Skills REST API (`localhost:8765`) 进行。** 不允许绕过 API 直接操作 Unity，包括：
- 创建/修改 ScriptableObject 资产（如 SkillDefinition、EffectDefinition）
- 场景对象操作
- 运行测试
- 检查编译状态
- 刷新资产

使用前先确认服务器在线：`curl -s http://localhost:8765/skills`

Python 辅助脚本：`.claude/skills/unity-skills/scripts/unity_skills.py`

### Unity Skills 模式
- 默认 **Semi-Auto**：只使用 script / perception / scene / editor / asset / workflow / debug / console 模块
- 全自动模式需用户明确指示
