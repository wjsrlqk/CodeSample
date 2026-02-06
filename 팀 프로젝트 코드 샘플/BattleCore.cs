using Battle.Plan;
using Battle.Skill;
using Cinema;
using CSV;
using Report;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Battle
{
    public class BattleCore : SystemCore<BattleSystem>
    {
        Dictionary<uint, BattleActor> m_actors = new Dictionary<uint, BattleActor>();
        UniqueKeyGenerator m_actorKeyGen = new UniqueKeyGenerator();
        bool m_isBattleInit = false;
        SkillAutoCaster m_autoCaster = null;
        PhaseUtility m_phaseUtil = null;
        public PhaseUtility phaseUtil => m_phaseUtil;

        Action m_onDirectionCompleted = null;
        public event Action OnDirectionCompleted
        {
            add
            {
                if(m_onDirectionCompleted == null || m_onDirectionCompleted.IsHaveDelegate(value) == false)
                    m_onDirectionCompleted += value;
            }
            remove
            {
                if(m_onDirectionCompleted != null && m_onDirectionCompleted.IsHaveDelegate(value))
                    m_onDirectionCompleted -= value;
            }
        }
        public BattleCore(BattleSystem main)
        {
            Main = main;
        }
        protected override IEnumerator CoreProcessCoroutine()
        {
            /*
                - Initialize 단계
                    게임 시작 후 첫 번째로 단 한번 적용됨. 
                -----------------------------------------
                - 전략 단계 
                    - 전략 진입 프로세스
                        효과 처리 단계 (Callback 처리)
                    - 전략 설정
                        적 캐릭터들의 행동 표시 + 아군 행동 설정 단계
                - 행동 단계
                    - 행동 진입 프로세스
                        효과 처리 단계 (Callback 처리)
                    - 행동 적용
                        각 진영마다 설정된 전략에 따라 계산 및 행동 연출
                    - 행동 종료 후 프로세스
                        효과 처리 단계 (Callback 처리)
                - 턴 후처리 페이즈
                    각종 버프, 디버프 적용 및 턴 차감
            */
            //battle Init
            yield return BattleInitialize();
            
            // 턴 시작 시 이전 턴에 죽은 캐릭터 목록 초기화
            var characterManager = Main.GetSystem<BattleSystem.CharacterManager>();
            if (characterManager != null)
            {
                characterManager.ClearDiedThisTurn();
            }
            
            //------------------------------------------------
            //turn loop
            List<ReportHeader> report = new List<ReportHeader>();
            var effectManager = Main.GetSystem<EffectManager>();
            if (effectManager != null)
            {
                Debug.Log("Effect Next Turn Process");
                using(ReportWriter writer = new ReportWriter())
                {
                    /////////////////////////////////////////////////////////////////////////////////////////
                    ///아군 턴 시작 효과
                    List<uint> targets = characterManager.ListByPredicate((info)=>info.identity == Identity.Ally)
                            .Select(info => info.actorKey)
                            .ToList();
                    using (TurnStartReport turnStart = new TurnStartReport(targets))
                    {
                        effectManager.RefreshNextTurn(Identity.Ally,report);
                    }
                    /////////////////////////////////////////////////////////////////////////////////////////
                    //적군 턴 시작 효과
                    targets = characterManager.ListByPredicate((info) => info.identity == Identity.Enemy)
                            .Select(info => info.actorKey)
                            .ToList();
                    using (TurnStartReport turnStart = new TurnStartReport(targets))
                    {
                        effectManager.RefreshNextTurn(Identity.Enemy, report);
                    }
                    /////////////////////////////////////////////////////////////////////////////////////////
                    report.AddRange(writer.Write());
                }
            }
            Debug.Log("DirectionTurnStart");
            yield return DirectionTurnStart(report);
            if(m_onDirectionCompleted != null)
                m_onDirectionCompleted();
            report.Clear();
            
            // 승패 체크 (턴 시작 효과로 인한 죽음 체크)
            if (characterManager != null && characterManager.IsBattleEnded)
            {
                yield return ProcessBattleEnd(characterManager.CurrentBattleResult);
                yield break;
            }
            
            Debug.Log("GenerateEnemyPlan");
            GenerateEnemyPlan();
            Debug.Log("PlanningPhase");
            yield return PlanningPhase();
            Debug.Log("Ally ActionPhase");
            //플랜 페이즈 종료 후 포커싱 해제
            BattleTargetSupporter.Instance.CancleTargeting();
            yield return ActionPhase(report,Identity.Ally);
            Debug.Log("Ally DirectionBattle");
            yield return DirectionBattle(report);
            if(m_onDirectionCompleted != null)
                m_onDirectionCompleted();
            report.Clear();
            
            // 승패 체크 (아군 행동 후)
            if (characterManager != null && characterManager.IsBattleEnded)
            {
                yield return ProcessBattleEnd(characterManager.CurrentBattleResult);
                yield break;
            }
            
            Debug.Log("Enemy ActionPhase");
            yield return ActionPhase(report, Identity.Enemy);
            Debug.Log("Enemy DirectionBattle");
            yield return DirectionBattle(report);
            if(m_onDirectionCompleted != null)
                m_onDirectionCompleted();
            report.Clear();
            
            // 승패 체크 (적군 행동 후)
            if (characterManager != null && characterManager.IsBattleEnded)
            {
                yield return ProcessBattleEnd(characterManager.CurrentBattleResult);
                yield break;
            }
            
            //턴 후처리 페이즈
            PlanManager planManager = Main.GetSystem<PlanManager>();
            planManager.TurnEndProcess();
        }

        protected override IEnumerator HandleEvent(object eventToken)
        {
            // 기타 이벤트 처리
            yield break;
        }
        public uint RegisterBattleActor(BattleActor actor)
        {
            uint unique = m_actorKeyGen.GetUnique();

            m_actors.Add(unique, actor);
            return unique;
        }
        public void UnregisterBattleActor(uint key)
        {
            m_actors.Remove(key);
        }
        public BattleActor SeekActorByKey(uint key)
        {
            if (m_actors.ContainsKey(key) == false)
                return null;
            return m_actors[key];
        }
        public void ForEachActor(Action<BattleActor> loop)
        {
            foreach(var actor in m_actors.Values)
            {
                if(loop != null)
                    loop(actor);
            }
        }
        public override void Start()
        {
            base.Start();
            m_isBattleInit = false;
            m_autoCaster = new SkillAutoCaster();
            m_phaseUtil = new PhaseUtility();
        }
        public override void Release()
        {
            base.Release();
            m_actors.Clear();
            m_autoCaster = null;
            m_phaseUtil = null;

            UIManager.Instance.CloseAllUI();
        }
        
        IEnumerator BattleInitialize()
        {
            if (m_isBattleInit == false)
            {
                m_isBattleInit = true;
            }
            yield break;
        }
        IEnumerator PlanningPhase()
        {
            m_phaseUtil.PhaseStart(PhaseUtility.PhaseType.Plan,new PlanLock());
            {
                // 각 요소들의 플랜을 유저에게 보여준다.
                // 적의 다음 행동, 플레이어가 명령내릴 수 있는 행동 등

                while (m_phaseUtil.Current.IsLock())
                {
                    yield return null;
                }
            }
            m_phaseUtil.PhaseEnd();
            yield break;
        }
        IEnumerator ActionPhase(List<ReportHeader> headers, Identity planIdentity)
        {
            m_phaseUtil.PhaseStart(PhaseUtility.PhaseType.Action, null);
            {
                PlanManager planManager = Main.GetSystem<PlanManager>();
                if (planManager != null)
                {
                    headers.AddRange(planManager.PlanLoop(planIdentity, ActionPhase_PlanResolve));
                }
            }
            m_phaseUtil.PhaseEnd();
            yield break;
        }
        //각 캐릭터 별로 적용된 플랜을 실행한다.
        //이 때 플랜 간 연계에 따라 예상된 결과가 달라질 수 있다.
        List<ReportHeader> ActionPhase_PlanResolve(Plan.Plan plan)
        {
            SkillContext context = null;
            BattleSystem.CharacterManager charManager = Main.GetSystem<BattleSystem.CharacterManager>();
            if (charManager == null) return null;
            using (ReportWriter writer = new ReportWriter())
            {
                context = plan.GetSkillContext();
                if (context != null)
                {
                    if(charManager.IsDead(context.actorKey))
                    {
                        ///////////////////////////////////////////////////////
                        ///죽은 개체의 Plan 처리
                        ///////////////////////////////////////////////////////
                        return writer.Write();
                    }
                    using (PlanResolve report = new PlanResolve(plan))
                    {
                        SkillInfoTableData skillData;
                        IReadOnlyList<int> definitionList = SkillInfoTable.Table.SeekSkillDefinition((int)context.skillUnique);
                        //스킬에 입력된 효과 처리
                        foreach(var definitionUnique in definitionList)
                        {
                            //플랜 단계에서 설정한 타겟 정보를 기준으로 대상 검색
                            SkillDefinitionTarget targetType = SkillDefinition.Table.SeekDefinitionTarget(definitionUnique);
                            List<CharInfo> formulaTargetList = charManager.SeekInfoByDefinitionTarget(targetType, plan.ownerActorKey, plan.targetActorKey);
                            List<uint> definitionTargetKeys = formulaTargetList.Select(x => x.actorKey).ToList();
                            report.AddTarget(definitionTargetKeys);
                            using(ActiveDefinition damageFormula = new ActiveDefinition())
                            {
                                if(SkillInfoTable.Table.TryGetByUnique((int)context.skillUnique, out skillData))
                                {
                                    IReadOnlyList<FormulaDefinition> skillDefinitions = SkillDefinition.Table.SeekFormulaDefinition(definitionUnique);
                                    //formula definition 적용
                                    for (int i = 0; i < skillDefinitions.Count; ++i)
                                    {
                                        foreach(var formulaTarget in formulaTargetList)
                                        {
                                            if(charManager.IsDead(formulaTarget.actorKey))
                                            {
                                                //////////////////////////////////////////////////
                                                ///죽은 대상에 대한 Plan 처리
                                                //////////////////////////////////////////////////
                                                continue;
                                            }
                                            FormulaDefinition fd = skillDefinitions[i];
                                            Formula skillFormula = SkillFormula.Table.SeekFormula(fd.formulaID);
                                            if (skillFormula != null)
                                            {
                                                List<ReportHeader> reportContainer = new List<ReportHeader>();

                                                //실제 데미지 계산 처리
                                                int result = SkillFormula.Table.Evalaute(Main, fd.formulaID, context.actorKey, formulaTarget.actorKey);

                                                DamageRequest req = new DamageRequest();
                                                req.target = charManager.SeekInfoByActorKey(formulaTarget.actorKey);
                                                req.owner = charManager.SeekInfoByActorKey(context.actorKey);
                                                req.skillUniqueKey = (int)context.skillUnique;
                                                req.damageAmount = result;
                                                req.attackType = skillFormula.attackType;
                                                
                                                // 파츠 타겟팅 처리
                                                if (plan.hasPartsTarget)
                                                {
                                                    // 파츠 데미지 처리
                                                    ApplyPartsTargetDamage(req, plan.targetPartsUnique);
                                                }
                                                else
                                                {
                                                    // 일반 데미지 처리
                                                    DamageProcess.Instance.ApplyCommonAttack(req);
                                                }
                                                break;
                                            }
                                        
                                        }
                                    }
                                }
                                ///////////////////////////////////////////////////////////////////////////
                                //effect definition 적용
                                IReadOnlyList<EffectDefinition> effectDefinitions = SkillDefinition.Table.SeekEffectDefinition(definitionUnique);
                                if (effectDefinitions.Count > 0)
                                {
                                    var effectManager = Main.GetSystem<EffectManager>();
                                    if (effectManager != null)
                                    {
                                        foreach (var iter in effectDefinitions)
                                        {
                                            foreach (var effectTarget in formulaTargetList)
                                            {
                                                if(charManager.IsDead(effectTarget.actorKey))
                                                {
                                                    //////////////////////////////////////////////////
                                                    ///죽은 대상에 대한 Plan 처리
                                                    //////////////////////////////////////////////////
                                                    continue;
                                                }
                                                effectManager.RegisterEffect(context.actorKey, effectTarget.actorKey, iter);
                                            }

                                        }
                                    }
                                }
                            }
                        }

                        //
                    }
                }
                return writer.Write();
            }
        }
        //적 플랜 자동 계산 함수
        void GenerateEnemyPlan()
        {
            m_phaseUtil.PhaseStart(PhaseUtility.PhaseType.CalcEnemyPlan, null);
            {
                BattleSystem.CharacterManager charManager = Main.GetSystem<BattleSystem.CharacterManager>();
                PlanManager planManager = Main.GetSystem<PlanManager>();
                if(charManager == null || planManager == null)
                {
                    Debug.LogError("Manager is null");
                    return;
                }
                List<CharInfo> list = charManager.ListByPredicate((charInfo) => charInfo.identity == Identity.Enemy);
                foreach(var info in list)
                {

                    List<int> plans = m_autoCaster.GeneratePlanProcess(info.characterUnique);
                    IReadOnlyList<int> skillSlots = CharacterInfoTable.Table.SeekSkillSetByCharacterUnique(info.characterUnique);
                    foreach(var planSkillIndex in plans)
                    {
                        if(skillSlots.Count <= planSkillIndex)
                        {
                            Debug.LogWarning($"plan error! 플랜이 제거됩니다 - skill slot array overflow, slot count : {skillSlots.Count}, index : {planSkillIndex}");
                            continue;
                        }
                        int skillUnique = skillSlots[planSkillIndex];
                        PlanInputData data = PlanInputData.CreateEmpty(PlanType.Skill);
                        data.SetValue(PlanDataSchema.SKILL_UNIQUE, (uint)skillUnique);
                        Plan.Plan plan = new Plan.Plan(info.actorKey, data);

                        SkillInfoTableData skillData = SkillInfoTable.Table.GetByUnique(skillUnique);
                        if(SkillInfoTable.Table.TryGetByUnique(skillUnique, out skillData) == false)
                        {
                            Debug.LogWarning($"plan error! 플랜이 제거됩니다 - Skill info not found, Skill info unique : {skillUnique}");
                            continue;

                        }
                        Condition<CharInfo> condition = skillData.GetTargetingConditions(info.actorKey);
                        ///////////////////////////////////////////////////////////
                        //타겟 설정 로직
                        //일단 임시로 랜덤 처리합니다
                        List<CharInfo> possibleTargets = charManager.ListByPredicate(condition.CheckCondition);
                        if(possibleTargets.Count == 0)
                        {
                            Debug.Log($"대상 없음!");
                            continue;
                        }
                        int randomValue = UnityEngine.Random.Range(0, possibleTargets.Count);
                        CharInfo targetInfo = possibleTargets[randomValue];
                        //////////////////////////////////////////////////////////////


                        plan.SetTarget(targetInfo.actorKey);
                        planManager.AddPlan(plan, info.actorKey);

                    }
                }
            }
            m_phaseUtil.PhaseEnd();

        }
        /// <summary>
        /// 파츠 타겟팅 데미지를 처리합니다
        /// 약점 공격: 파츠에 100%, 본체에 파츠 데미지의 100%
        /// 일반 공격: 파츠에 30%, 본체에 파츠 데미지의 100%
        /// </summary>
        private void ApplyPartsTargetDamage(DamageRequest request, int partsUnique)
        {
            var bodyPartManager = Main.GetSystem<BodyPart.BodyPartSystemManager>();
            if (bodyPartManager == null)
            {
                Debug.LogWarning("BodyPartSystemManager not found, applying full damage to character");
                DamageProcess.Instance.ApplyCommonAttack(request);
                return;
            }

            var charInfo = request.target as CharInfo;
            if (charInfo == null)
            {
                Debug.LogWarning("Target is not CharInfo, applying normal damage");
                DamageProcess.Instance.ApplyCommonAttack(request);
                return;
            }

            // 파츠 존재 여부 확인
            var collection = charInfo.GetBodyPartCollection();
            if (collection == null || !collection.HasActivePart(partsUnique))
            {
                Debug.LogWarning($"Parts {partsUnique} not found or destroyed, applying full damage to character");
                DamageProcess.Instance.ApplyCommonAttack(request);
                return;
            }

            // 공격 타입 가져오기 (SkillInfoTable에서)
            CSV.SkillAttackType attackType = CSV.SkillAttackType.Normal;
            if (request.skillUniqueKey > 0)
            {
                attackType = SkillInfoTable.Table.GetSkillAttackType(request.skillUniqueKey);
            }

            // 약점 속성 확인 (중복 제거, 존재 여부만 확인)
            bool isWeaknessAttack = PartsInfoTable.Table.IsWeaknessAttackType(partsUnique, attackType);

            // 1. 파츠에 데미지 적용
            // 약점 공격: 100%, 일반 공격: 30%
            float partsDamageRatio = isWeaknessAttack ? 1.0f : 0.3f;
            int partsDamage = Mathf.RoundToInt(request.damageAmount * partsDamageRatio);
            
            if (partsDamage > 0)
            {
                bodyPartManager.ApplyDamageToPart(charInfo, partsUnique, partsDamage, request.owner);
                Debug.Log($"Applied {partsDamage} damage ({partsDamageRatio * 100}%) to part {partsUnique} of character {charInfo.actorKey}");
            }

            // 2. 본체에 데미지 적용
            // 파츠에 들어간 데미지의 100%를 본체에 적용
            int bodyDamage = partsDamage;
            
            if (bodyDamage > 0)
            {
                DamageRequest bodyRequest = request;
                bodyRequest.damageAmount = bodyDamage;
                DamageProcess.Instance.ApplyCommonAttack(bodyRequest);
                Debug.Log($"Applied {bodyDamage} damage (100% of parts damage) to body of character {charInfo.actorKey}");
            }

            Debug.Log($"Parts damage complete - Original: {request.damageAmount}, " +
                     $"Parts({partsUnique}): {partsDamage} ({partsDamageRatio * 100}%), " +
                     $"Body: {bodyDamage} (100% of parts damage), " +
                     $"AttackType: {attackType}, IsWeakness: {isWeaknessAttack}");
        }

        //연출 프로세스
        IEnumerator DirectionBattle(IReadOnlyList<ReportHeader> header)
        {
            if (Main.cinemaMechine != null)
            {
                bool isCinemaReady = false;
                foreach (var iter in header)
                {
                    var builder = iter.Build(Main.cinemaMechine,null);
                    if(builder != null)
                    {
                        //하나의 빌더라도 정상적으로 준비되면 시작
                        isCinemaReady = true;
                    }
                }

                if (Main.cinemaMechine.IsEmptyCinema() == false && isCinemaReady)
                {

                    yield return Main.cinemaMechine.darkMakeRenderer.MaskEnable();
                    yield return Main.cinemaMechine.Run(header);
                    yield return Main.cinemaMechine.darkMakeRenderer.MaskDisable();
                }
            }
            else
                Debug.LogError("CinemaMechine is null");
            
            // ✅ Direction 종료 후 이번 턴에 죽은 캐릭터 화면에서 제거
            yield return ProcessDeadCharacters();
            
            //yield return YieldCache.WaitForSeconds(1.2f);
        }
        
        IEnumerator DirectionTurnStart(IReadOnlyList<ReportHeader> header)
        {
            if (Main.cinemaMechine != null)
            {
                bool isCinemaReady = false;
                foreach (var iter in header)
                {
                    var builder = iter.Build(Main.cinemaMechine, null);
                    if (builder != null && builder.IsEmptyAction() == false)
                    {
                        //하나의 빌더라도 정상적으로 준비되면 시작
                        isCinemaReady = true;
                    }
                }

                if (Main.cinemaMechine.IsEmptyCinema() == false && isCinemaReady)
                {
                    yield return Main.cinemaMechine.darkMakeRenderer.MaskEnable();
                    yield return Main.cinemaMechine.Run(header);
                    yield return Main.cinemaMechine.darkMakeRenderer.MaskDisable();
                }
            }
            else
                Debug.LogError("CinemaMechine is null");
            
            // ✅ Direction 종료 후 이번 턴에 죽은 캐릭터 화면에서 제거
            yield return ProcessDeadCharacters();
        }
        
        /// <summary>
        /// 이번 턴에 죽은 캐릭터를 화면에서 제거합니다
        /// </summary>
        IEnumerator ProcessDeadCharacters()
        {
            var charManager = Main.GetSystem<BattleSystem.CharacterManager>();
            if (charManager == null)
            {
                yield break;
            }
            
            var diedThisTurn = charManager.GetDiedThisTurn();
            if (diedThisTurn.Count == 0)
            {
                yield break;
            }
            
            Debug.Log($"Processing {diedThisTurn.Count} dead characters for removal");
            
            foreach (var actorKey in diedThisTurn)
            {
                // BattleActor 찾기
                BattleActor actor = SeekActorByKey(actorKey);
                if (actor != null)
                {
                    // 사망 애니메이션 재생 (있다면)
                    if (actor.animController != null)
                    {
                        // 페이드 아웃 효과
                        actor.animController.FadeAlpha(0f, 0.5f);
                    }
                    
                    Debug.Log($"Removing dead character from screen: {actorKey}");
                }
            }
            
            // 페이드 아웃 대기
            yield return YieldCache.WaitForSeconds(0.5f);

            // 이번 턴 죽은 목록은 다음 턴 시작 전까지 유지
            // (턴 시작 시 ClearDiedThisTurn 호출)
            foreach(var actorKey in diedThisTurn)
            {
                // CharacterSocket 찾기 및 비활성화
                if(Main.battleRoomController != null)
                {
                    CharacterSocket socket = Main.battleRoomController.FindSocket(actorKey);
                    if(socket != null)
                    {
                        // Socket 비활성화 또는 제거
                        socket.gameObject.SetActive(false);
                        Debug.Log($"Disabled socket for dead character: {actorKey}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 전투 종료 처리를 수행합니다
        /// </summary>
        IEnumerator ProcessBattleEnd(BattleSystem.CharacterManager.BattleResult result)
        {
            Debug.Log($"=== ProcessBattleEnd: {result} ===");
            //배틀 시스템 중단
            Main.systemState.ChangeState(SystemState.Clear);
            // 1. 남은 죽은 캐릭터 화면 제거
            yield return ProcessDeadCharacters();
            
            // 2. 전투 종료 연출
            yield return ShowBattleEndDirection(result);
            
            // 3. 전투 종료 처리 (UI 업데이트, 보상 등)
            yield return HandleBattleEndResult(result);
            
            Debug.Log($"=== Battle End Process Complete ===");
        }
    }
}
