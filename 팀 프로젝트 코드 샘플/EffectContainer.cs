using CSV;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
namespace Battle
{
    namespace Skill
    {
        public enum TryAddEffectType
        {
            Success,
            MaxStack,
            AddFail_Error
        }
        public enum EffectContainerType
        {
            Multi,Single
        }
        public interface IEffectContainer
        {
            StatusEffectType effectType { get; }

            int GetStackCount(EffectUniqueCode unique);
            int GetTurnCount(EffectUniqueCode unique);
            TryAddEffectType AddEffect (EffectInstance instance, TryAddEffectType effectAddType);
            void Clear();
            //종료 처리된 인스턴스를 return
            List<EffectInstance> NextTurnProcess();
        }

        public class MultiInstanceContainer : Dictionary<EffectUniqueCode, List<EffectInstance>>
            , IEffectContainer
        {
            public MultiInstanceContainer(StatusEffectType type)
            {
                m_effectType = type;
            }
            public StatusEffectType effectType => m_effectType;
            StatusEffectType m_effectType = StatusEffectType.Once_Effect;
            public TryAddEffectType AddEffect(EffectInstance instance, TryAddEffectType inputAddType)
            {
                TryAddEffectType addType = inputAddType;
                EffectUniqueCode unique = instance.currentInfo.uniqueCode;

                StatusEffectType type = SkillEffect.Table.SeekEffectType(unique);

                //이미 최대 스택이면 return
                if (addType == TryAddEffectType.MaxStack)
                    return addType;
                switch(type)
                {
                    case StatusEffectType.Cum_Effect:
                        if(ContainsKey(unique) == false)
                        {
                            List<EffectInstance> list = new List<EffectInstance>();
                            Add(unique,list);
                        }
                        this[unique].Add(instance);
                        break;
                    case StatusEffectType.HybridStack_Effect:
                        if (ContainsKey(unique) == false)
                        {
                            List<EffectInstance> list = new List<EffectInstance>();
                            Add(unique, list);
                        }
                        this[unique].Add(instance);
                        break;
                    default:
                        Debug.LogWarning($"container type error, type : {type}");
                        return TryAddEffectType.AddFail_Error;
                }

                var format = SkillEffect.Table.SeekEffectFormat(unique);
                //최대 스택이 정해져있고 추가 후 최대 스택에 달성했을 경우
                if (0 < format.Value.maxStack &&
                    format.Value.maxStack <= GetStackCount(unique))
                {
                    instance.MaxStackProcess();
                }
                return addType;
            }

            public int GetStackCount(EffectUniqueCode unique)
            {
                if (ContainsKey(unique) == false)
                    return 0;
                return this[unique].Count;
            }
            public int GetTurnCount(EffectUniqueCode unique)
            {
                if (ContainsKey(unique) == false)
                    return 0;
                int topCount = 0;
                foreach(var instance in this[unique])
                {
                    if(topCount < instance.currentInfo.turnCount)
                    {
                        topCount = instance.currentInfo.turnCount;
                    }
                }
                return topCount;
            }
            public List<EffectInstance> NextTurnProcess()
            {
                List<EffectInstance> expired = new List<EffectInstance>(); 
                foreach (var effect in this)
                {
                    List<EffectInstance> expiredInLoop = new List<EffectInstance>();
                    foreach(var instance in effect.Value)
                    {
                        if (instance.NextTurnProcess() == false)
                        {
                            expiredInLoop.Add(instance);
                        }
                    }
                    //만료된 인스턴스 제거
                    foreach(var removedEffect in expiredInLoop)
                    {
                        effect.Value.Remove(removedEffect);
                    }
                    expired.AddRange(expiredInLoop);
                }
                return expired;
            }
        }
        public class SingleInstanceContainer : Dictionary<EffectUniqueCode, (EffectInstance instance,int stackCount)>
            , IEffectContainer
        {
            public SingleInstanceContainer (StatusEffectType type)
            {
                m_effectType = type;
            }
            public StatusEffectType effectType => m_effectType;
            StatusEffectType m_effectType = StatusEffectType.Once_Effect;

            public TryAddEffectType AddEffect(EffectInstance instance, TryAddEffectType inputAddType)
            {
                TryAddEffectType addType = inputAddType;
                EffectUniqueCode unique = instance.currentInfo.uniqueCode;

                StatusEffectType type = SkillEffect.Table.SeekEffectType(unique);
                //이미 최대 스택이면 return
                if (addType == TryAddEffectType.MaxStack)
                    return addType;
                switch (type)
                {
                    case StatusEffectType.Stack_Effect:
                        //기존 카운트가 더 높으면 +1, 아니면 높은 카운트로 덮어씌우기
                        if (ContainsKey(unique))
                        {
                            //턴 카운트 지정
                            int count = math.max(instance.currentInfo.turnCount, this[unique].instance.currentInfo.turnCount + 1);
                            this[unique].instance.ResetTurn(count);
                            //스택 갱신
                            (EffectInstance, int) value = (instance, this[unique].stackCount +1);
                            this[unique] = value;
                        }
                        else
                            Add(unique, (instance,1));
                        break;
                    case StatusEffectType.Once_Effect:
                        //일단 효과 무시
                        if (ContainsKey(unique))
                        {

                        }
                        else
                            Add(unique, (instance,1));
                        break;
                    default:
                        Debug.LogWarning($"container type error, type : {type}");
                        return TryAddEffectType.AddFail_Error;
                }

                var format = SkillEffect.Table.SeekEffectFormat(unique);
                //최대 스택이 정해져있고 추가 후 최대 스택에 달성했을 경우
                if (0 < format.Value.maxStack &&
                    format.Value.maxStack <= GetStackCount(unique))
                {
                    instance.MaxStackProcess();
                }
                return addType;
            }

            public int GetStackCount(EffectUniqueCode unique)
            {
                if(ContainsKey(unique) == false)
                {
                    return 0;
                }
                return this[unique].stackCount;
            }
            public int GetTurnCount(EffectUniqueCode unique)
            {
                if (ContainsKey(unique) == false)
                {
                    return 0;
                }
                return this[unique].instance.currentInfo.turnCount;
            }
            public List<EffectInstance> NextTurnProcess()
            {
                List<EffectInstance> expired = new List<EffectInstance>();
                List<EffectUniqueCode> removeTarget = new List<EffectUniqueCode>();
                foreach (var effect in this)
                {
                    if(effect.Value.instance.NextTurnProcess() == false)
                    {
                        expired.Add(effect.Value.instance);
                        removeTarget.Add(effect.Key);
                    }
                }
                foreach(var unique in removeTarget)
                {
                    Remove(unique);
                }
                return expired;
            }
        }

    }
}
