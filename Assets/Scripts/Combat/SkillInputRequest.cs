using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat
{
    public readonly struct SkillInputRequest
    {
        public readonly GameplayTag SignalTag;
        public readonly GameplayTag TargetTag;
        public readonly GameObject TargetObject;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;

        public SkillInputRequest(
            GameplayTag signalTag,
            GameplayTag targetTag,
            GameObject targetObject,
            Vector3 worldPosition,
            bool hasWorldPosition)
        {
            SignalTag = signalTag;
            TargetTag = targetTag;
            TargetObject = targetObject;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
        }

        public bool IsSignal(GameplayTag tag)
        {
            return SignalTag == tag;
        }

        public bool IsTarget(GameplayTag tag)
        {
            return TargetTag == tag;
        }
    }
}
