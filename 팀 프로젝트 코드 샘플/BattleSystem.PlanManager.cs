using Battle.Plan;
using System.Collections.Generic;
using System;
using Report;
using UnityEngine;
namespace Battle
{
    namespace Plan
    {
        public class PlanTable
        {
            //턴 시작 시 AI가 플랜으로 지정할 스킬 리스트를 저장하고있다.
            Queue<List<int>> m_planQueue = new Queue<List<int>>();
            //다른 플랜이 작동되어 트리거가 발동했을 경우, planQueue보다 우선적으로 작동되는 스킬 리스트
            //턴 시작 시 additionalPlan이 등록되어있다면 우선적으로 소진한다.
            List<int> m_additionalPlanKey = new List<int>();
            public bool IsEmptyTable()
            {
                return m_additionalPlanKey.Count == 0 && m_planQueue.Count == 0;
            }
            public void ApplyPlan(SkillTableEntry tableSet)
            {
                foreach(var skillSet in tableSet.skillSet)
                {
                    m_planQueue.Enqueue(skillSet);
                }
            }
            public void GetNextPlan(out List<int> list)
            {
                list = new List<int>();
                if(m_additionalPlanKey.Count > 0)
                {
                    list.AddRange(m_additionalPlanKey);
                    m_additionalPlanKey.Clear();
                    return;
                }
                else
                {
                    if(m_planQueue.TryDequeue(out list) == false)
                    {
                        Debug.LogWarning("plan error! empty plan queue");
                        list = new List<int>();
                    }
                }
            }
        }
        //각 캐릭터 종류 별 스킬 자동 시전 로직을 정의한 클래스
        public class SkillAutoCaster
        {
            Dictionary<int, PlanTable> m_planTableDic = new Dictionary<int, PlanTable>();
            public List<int> GeneratePlanProcess(int charUnique)
            {
                List<int> plans = null;
                if(m_planTableDic.ContainsKey(charUnique) == false)
                {
                    m_planTableDic.Add(charUnique, new PlanTable());
                }
                if(m_planTableDic[charUnique].IsEmptyTable())
                {
                    var entry = MonsterSkillTable.Table.RandomSkillEntry(charUnique);

                    m_planTableDic[charUnique].ApplyPlan(entry);
                }

                m_planTableDic[charUnique].GetNextPlan(out plans);
                return plans;
            }
        }
        public enum PlanType
        {
            None,
            Skill
        }
        
        // 플랜 데이터 스키마 정의
        public static class PlanDataSchema
        {
            // 스킬 관련 키
            public const string SKILL_UNIQUE = "skillUnique";
            
            // 타겟 관련 키
            public const string TARGET_ACTOR = "targetActor";
            public const string TARGET_PARTS = "targetParts";
            
            // 아이템 관련 키 (확장용)
            public const string ITEM_UNIQUE = "itemUnique";
            public const string ITEM_COUNT = "itemCount";
            
            // 위치 관련 키 (확장용)
            public const string POSITION_X = "posX";
            public const string POSITION_Y = "posY";
            public const string POSITION_Z = "posZ";
        }
        
        //플랜을 빌드하기 위해 필요한 정보
        //플랜이 생성된 후 외부에서 주입되는 데이터를 스키마 형태로 저장한다 ex) 스킬 플랜의 목표 대상을 지정하여 타겟팅하는 기능
        public struct PlanInputData
        {
            public PlanType type;
            public Dictionary<string, string> schema;
            
            // 스키마 초기화 헬퍼
            public static PlanInputData CreateEmpty(PlanType planType)
            {
                return new PlanInputData
                {
                    type = planType,
                    schema = new Dictionary<string, string>()
                };
            }
            
            // 스키마 값 설정
            public void SetValue(string key, string value)
            {
                if (schema == null)
                    schema = new Dictionary<string, string>();
                schema[key] = value;
            }
            
            public void SetValue(string key, int value)
            {
                SetValue(key, value.ToString());
            }
            
            public void SetValue(string key, uint value)
            {
                SetValue(key, value.ToString());
            }
            
            public void SetValue(string key, float value)
            {
                SetValue(key, value.ToString());
            }
            
            // 스키마 값 읽기
            public bool TryGetValue(string key, out string value)
            {
                value = null;
                if (schema == null)
                    return false;
                return schema.TryGetValue(key, out value);
            }
            
            public bool TryGetInt(string key, out int value)
            {
                value = 0;
                if (TryGetValue(key, out string strValue))
                {
                    return int.TryParse(strValue, out value);
                }
                return false;
            }
            
            public bool TryGetUInt(string key, out uint value)
            {
                value = 0;
                if (TryGetValue(key, out string strValue))
                {
                    return uint.TryParse(strValue, out value);
                }
                return false;
            }
            
            public bool TryGetFloat(string key, out float value)
            {
                value = 0f;
                if (TryGetValue(key, out string strValue))
                {
                    return float.TryParse(strValue, out value);
                }
                return false;
            }
            
            // 값 존재 여부 확인
            public bool HasValue(string key)
            {
                return schema != null && schema.ContainsKey(key);
            }
        }
        //특정 개체가 받은 명령을 계획화한 객체
        //스킬, 채집 등의 기능을 플랜 형태로 저장한다
        //플랜 상태에서는 외부 개입을 통해 목표나 행동이 변형될 수 있다
        public class Plan
        {
            public Plan(uint ownerID, PlanInputData data)
            {
                m_ownerActorKey = ownerID;
                m_inputData = data;
                m_targetActorKey = UniqueKeyGenerator.InvalidValue;
            }
            
            uint m_ownerActorKey;
            uint m_targetActorKey;
            PlanInputData m_inputData;

            public uint ownerActorKey { get => m_ownerActorKey; }
            
            public uint targetActorKey 
            { 
                get 
                {
                    if (m_inputData.TryGetUInt(PlanDataSchema.TARGET_ACTOR, out uint target))
                        return target;
                    return m_targetActorKey;
                }
            }
            
            public int targetPartsUnique 
            { 
                get 
                {
                    if (m_inputData.TryGetInt(PlanDataSchema.TARGET_PARTS, out int parts))
                        return parts;
                    return -1;
                }
            }
            
            public bool hasPartsTarget 
            { 
                get => m_inputData.HasValue(PlanDataSchema.TARGET_PARTS) && targetPartsUnique >= 0; 
            }
            
            public PlanInputData planInputData { get => m_inputData; }
            public PlanType planType => planInputData.type;
            
            public void SetTarget(uint targetKey)
            {
                m_targetActorKey = targetKey;
                m_inputData.SetValue(PlanDataSchema.TARGET_ACTOR, targetKey);
            }
            
            public void SetPartTarget(int partsUnique)
            {
                m_inputData.SetValue(PlanDataSchema.TARGET_PARTS, partsUnique);
            }
            
            public SkillContext GetSkillContext()
            {
                SkillContext context = null;
                if (m_inputData.type == PlanType.Skill)
                {
                    context = m_inputData.ReadInputData<SkillContext>(m_ownerActorKey);
                }
                return context;
            }
            
            public bool TryGetSkillContext(out SkillContext info)
            {
                info = null;
                if (m_inputData.type == PlanType.Skill)
                {
                    info = m_inputData.ReadInputData<SkillContext>(m_ownerActorKey);
                    return info != null;
                }
                return false;
            }
        }

        public static class PlanExtension
        {
            public static T ReadInputData<T>(this PlanInputData input, uint casterID) where T : class, IPlanContext
            {
                if (input.type == PlanType.Skill)
                {
                    uint actorID = casterID;
                    
                    if (input.TryGetUInt(PlanDataSchema.SKILL_UNIQUE, out uint skillUnique))
                    {
                        SkillContext context = new SkillContext(actorID, skillUnique);
                        return context as T;
                    }
                }

                return null;
            }
        }
    }
    
    public struct PlanConfig
    {
        public int planCount;
    }
    
    public class PlanManager : ISystemInterface<BattleSystem>
    {
        public BattleSystem Main { get; private set; }
        public PlanManager(BattleSystem main)
        {
            Main = main;
        }

        Dictionary<uint, List<Plan.Plan>>[] m_reservedPlans = null;
        Dictionary<uint, PlanConfig> m_playerCharacterConfig = null;

        event Action<uint> m_onPlanChanged = null;
        public event Action<uint> OnPlanChanged
        {
            add
            {
                if (m_onPlanChanged == null || m_onPlanChanged.IsHaveDelegate(value) == false)
                {
                    m_onPlanChanged += value;
                }
            }
            remove
            {
                if (m_onPlanChanged != null && m_onPlanChanged.IsHaveDelegate(value))
                {
                    m_onPlanChanged -= value;
                }
            }
        }

        void NotifyPlanChanged(uint planOwnerKey)
        {
            if (m_onPlanChanged != null)
                m_onPlanChanged(planOwnerKey);
        }
        
        public void AddPlan(Plan.Plan plan, uint planOwnerKey)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {

                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if (dic.ContainsKey(planOwnerKey) == false)
                {
                    dic.Add(planOwnerKey, new List<Plan.Plan>());
                }
                if(info.identity == Identity.Ally)
                {

                    int maxPlanCount = 1;
                    if(m_playerCharacterConfig.ContainsKey(planOwnerKey))
                    {
                        maxPlanCount = m_playerCharacterConfig[planOwnerKey].planCount;
                    }
                    if(maxPlanCount < dic[planOwnerKey].Count)
                    {
                        return;
                    }
                }
                dic[planOwnerKey].Add(plan);
                NotifyPlanChanged(planOwnerKey);
            }
        }
        
        public IReadOnlyList<Plan.Plan> GetReservedPlanList(uint planOwnerKey)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {
                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if (dic.ContainsKey(planOwnerKey))
                {
                    return dic[planOwnerKey];
                }
            }
            return Array.Empty<Plan.Plan>();
        }

        public Plan.Plan GetFirstPlan(uint planOwnerKey)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {
                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if (dic.ContainsKey(planOwnerKey) && dic[planOwnerKey].Count > 0)
                {
                    return dic[planOwnerKey][0];
                }
            }
            return null;
        }
        
        public void SetPlan(Plan.Plan plan, uint planOwnerKey,int planIndex)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {
                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if (dic.ContainsKey(planOwnerKey) == false || dic[planOwnerKey].Count <= planIndex)
                {
                    Debug.LogWarning($"SetPlan Fail, OwnerKey : {planOwnerKey}, targetIndex : {planIndex}");
                    return;
                }
                dic[planOwnerKey][planIndex] = plan;
                NotifyPlanChanged(planOwnerKey);
            }
        }
        
        public bool IsPlanRegistered(Plan.Plan plan, uint planOwnerKey)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {
                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if(dic.ContainsKey(planOwnerKey) == false)
                {
                    return false;
                }
                return dic[planOwnerKey].Find((iter)=> iter != null && iter == plan) != null;
            }
            return false;
        }
        
        public bool IsPlanRegistered(uint planOwnerKey)
        {
            var mgr = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = mgr.SeekInfoByActorKey(planOwnerKey);
            if (info != null)
            {
                Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)info.identity];
                if (dic.ContainsKey(planOwnerKey) == false)
                {
                    return false;
                }
                return dic[planOwnerKey].Count > 0;
            }
            return false;
        }
        
        public List<ReportHeader> PlanLoop(Identity team, Func<Plan.Plan, List<ReportHeader>> planLoop)
        {
            List<ReportHeader> headers = new List<ReportHeader>();
            foreach (var list in m_reservedPlans[(uint)team])
            {
                foreach (var plan in list.Value)
                {
                    headers.AddRange(planLoop(plan));
                }
            }
            return headers;
        }
        
        public void RemoveAll()
        {
            if(m_reservedPlans != null)
            {
                foreach (var iter in m_reservedPlans)
                {
                    iter.Clear();
                }
            }
            if(m_playerCharacterConfig != null)
            {
                m_playerCharacterConfig.Clear();
            }
            NotifyPlanChanged(UniqueKeyGenerator.InvalidValue);
        }
        
        public void TurnEndProcess()
        {
            if (m_reservedPlans != null)
            {
                foreach (var plans in m_reservedPlans)
                {
                    foreach(var plan in plans)
                    {
                        plan.Value.Clear();
                    }
                    plans.Clear();
                }
            }
            NotifyPlanChanged(UniqueKeyGenerator.InvalidValue);
        }

        public void RemovePlan(BattleActor actor)
        {
            var charManager = Main.GetSystem<BattleSystem.CharacterManager>();
            CharInfo info = charManager.SeekInfoByActorKey(actor.actorKey,true);
            if (info != null)
            {
                RemovePlan(info.identity, actor.actorKey);
            }
        }
        
        public void RemovePlan(Identity identity, uint planOwnerKey)
        {
            Dictionary<uint, List<Plan.Plan>> dic = m_reservedPlans[(uint)identity];
            if (dic.Remove(planOwnerKey))
            {
                NotifyPlanChanged(planOwnerKey);
            }
        }
        
        #region ISystemInterface
        public void Start()
        {
            m_reservedPlans = new Dictionary<uint, List<Plan.Plan>>[(int)Identity.Object];
            m_reservedPlans[(int)Identity.Ally] = new Dictionary<uint, List<Plan.Plan>>();
            m_reservedPlans[(int)Identity.Enemy] = new Dictionary<uint, List<Plan.Plan>>();

            m_playerCharacterConfig = new Dictionary<uint, PlanConfig>();
        }
        
        public void Reset()
        {
        }
        
        public void Release()
        {
            RemoveAll();
            m_reservedPlans = null;
            m_playerCharacterConfig = null;
            m_onPlanChanged = null;
        }
        #endregion
    }

}
