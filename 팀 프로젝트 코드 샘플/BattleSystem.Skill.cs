using CSV;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Battle.Skill;
using ActorEffectContainer = System.Collections.Generic.Dictionary<uint, Battle.Skill.EffectTypeContainer>;
using Report;

namespace Battle
{
    namespace Skill
    {
        #region Effect
        public struct EffectStatusInfo
        {
            public EffectStatusInfo(EffectUniqueCode uniqueCode, int stackCount, int turnCount)
            {
                this.uniqueCode = uniqueCode;
                this.stackCount = stackCount;
                this.turnCount = turnCount;
            }
            public EffectUniqueCode uniqueCode;
            public int stackCount;
            public int turnCount;
        }

        public class EffectTypeContainer
        {
            Dictionary<StatusEffectType, IEffectContainer> m_effectContainer = new Dictionary<StatusEffectType, IEffectContainer>()
            {
                { StatusEffectType.Cum_Effect,new MultiInstanceContainer(StatusEffectType.Cum_Effect)},
                { StatusEffectType.HybridStack_Effect,new MultiInstanceContainer(StatusEffectType.HybridStack_Effect)},
                { StatusEffectType.Once_Effect,new SingleInstanceContainer(StatusEffectType.Once_Effect)},
                { StatusEffectType.Stack_Effect,new SingleInstanceContainer(StatusEffectType.Stack_Effect)},
            };

            public List<EffectStatusInfo> GetActiveEffects()
            {
                List<EffectStatusInfo> list = new List<EffectStatusInfo>();
                foreach (var container in m_effectContainer)
                {
                    if (container.Value is MultiInstanceContainer multi)
                    {
                        foreach (var unique in multi.Keys)
                        {
                            list.Add(new EffectStatusInfo(unique, GetStackCount(unique), GetTurnCount(unique)));
                        }
                    }
                    else if (container.Value is SingleInstanceContainer single)
                    {
                        foreach (var unique in single.Keys)
                        {
                            list.Add(new EffectStatusInfo(unique, GetStackCount(unique), GetTurnCount(unique)));
                        }
                    }
                }
                return list;
            }

            public int GetStackCount(EffectUniqueCode unique)
            {
                StatusEffectType type = SkillEffect.Table.SeekEffectType(unique);
                return m_effectContainer[type].GetStackCount(unique);
            }
            public int GetTurnCount(EffectUniqueCode unique)
            {
                StatusEffectType type = SkillEffect.Table.SeekEffectType(unique);
                return m_effectContainer[type].GetTurnCount(unique);
            }
            public TryAddEffectType AddEffect(EffectInstance instance)
            {
                EffectUniqueCode unique = instance.currentInfo.uniqueCode;
                StatusEffectType type = SkillEffect.Table.SeekEffectType(unique);
                var format = SkillEffect.Table.SeekEffectFormat(unique);
                if(format == null)
                {
                    Debug.LogWarning($"effect format is null, unique : {unique}");
                    return TryAddEffectType.AddFail_Error;
                }
                TryAddEffectType addType = TryAddEffectType.Success;
                //max stack이 지정되어 있고 스택이 이미 최대치일 경우
                if(0 < format.Value.maxStack &&
                    format.Value.maxStack <= m_effectContainer[type].GetStackCount(unique))
                {
                    //스택 외에 갱신될 요소가 있을 수도 있기 때문에 Add Effect는 시도한다
                    addType = TryAddEffectType.MaxStack;
                }
                return m_effectContainer[type].AddEffect(instance, addType);
                
            }
            public void NextTurnProcess()
            {
                List<EffectInstance> expired = new List<EffectInstance>();
                foreach(var container in m_effectContainer)
                {
                    expired.AddRange(container.Value.NextTurnProcess());
                }
            }

            public void Clear()
            {
                foreach(var iter in  m_effectContainer)
                {
                    iter.Value.Clear();
                }
            }
        }
        //상태 이상 관리
        public class EffectManager : ISystemInterface<BattleSystem>
        {
            public BattleSystem Main { get; private set; }
            public EffectManager(BattleSystem main)
            {
                Main = main;
            }

            private ActorEffectContainer m_actorEffects = new ActorEffectContainer();
            public void RefreshNextTurn(Identity target,List<ReportHeader> headers)
            {
                var charManager = Main.GetSystem<BattleSystem.CharacterManager>();
                var targetActors = m_actorEffects.Where((iter)=>
                    {
                        CharInfo info = charManager.SeekInfoByActorKey(iter.Key);
                        if(info != null)
                        {
                            return info.identity == target;
                        }
                        return false;
                    }
                )
                .ToList();
                if(targetActors.Count() > 0)
                {
                    using(ApplyEffectTurnStart report = new ApplyEffectTurnStart())
                    {
                        foreach (var container in targetActors)
                        {
                            container.Value.NextTurnProcess();
                        }
                    }
                }
            }
            public void RegisterEffect(uint casterKey,uint targetKey, EffectDefinition definition)
            {
                //이펙트에 따라 생성 또는 갱신 로직을 시행한다!!
                EffectBaseFormat? format = SkillEffect.Table.SeekEffectFormat(definition.effectUnique);
                if(format == null)
                {
                    Debug.LogWarning($"Effect Register Fail, unique : {definition.effectUnique}");
                    return;
                }
                //상태 이상 인스턴스 생성
                EffectInstance instance = Factory.Create(casterKey,targetKey,format.Value,definition);
                if (instance == null) return;
                // actorKey에 해당하는 EffectTypeContainer 가져오기 또는 생성
                if (!m_actorEffects.TryGetValue(targetKey, out var typeContainer))
                {
                    typeContainer = new EffectTypeContainer();
                    m_actorEffects[targetKey] = typeContainer;
                }
                // 타입에 맞는 컨테이너 선택
                TryAddEffectType addType = typeContainer.AddEffect(instance);
                //이펙트 인스턴스 활성화
                instance.Initialize();
                //리포트 생성
                ApplyEffect report = new ApplyEffect(instance.currentInfo, addType, targetKey);
                ReportWriter.AddChild(report);
            }
            public void UnregisterEffect(uint actorKey,string uniqueName)
            {
                Debug.LogWarning("아직 작업중! 타입 별로 다른 제거 방식 구현 필요합니다");
            }
            public EffectTypeContainer GetContainer(uint actorKey)
            {
                if (m_actorEffects.ContainsKey(actorKey) == false)
                    return null;
                return m_actorEffects[actorKey];
            }
            public void Release()
            {
                foreach(var actorLoop in m_actorEffects)
                {
                    //이펙트 인스턴스 클리어
                    actorLoop.Value.Clear();
                }
                //엑터 단위 클리어
                m_actorEffects.Clear();
            }
            #region ISystemInterface Method
            public void Start()
            {
            }
            public void Reset()
            {
            }
            #endregion
        }
        #endregion

    }
}
