using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using SandBox.Conversation.MissionLogics;
using StoryMode.Missions;
using StoryMode.StoryModeObjects;
using StoryMode.StoryModePhases;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;
using TaleWorlds.ObjectSystem;

namespace TestingMod.patches.TournamentStuff
{
    public class TrainingMissionController : TrainingFieldMissionController
    {
        public TextObject InitialCurrentObjective { get; private set; }

        public override void OnCreated()
        {
            base.OnCreated();
            base.Mission.DoesMissionRequireCivilianEquipment = false;
        }

        public override void AfterStart()
        {
            base.AfterStart();
            base.Mission.IsInventoryAccessible = false;
            base.Mission.IsQuestScreenAccessible = false;
            base.Mission.IsCharacterWindowAccessible = false;
            base.Mission.IsPartyWindowAccessible = false;
            base.Mission.IsKingdomWindowAccessible = false;
            base.Mission.IsClanWindowAccessible = false;
            base.Mission.IsEncyclopediaWindowAccessible = false;
            base.Mission.IsBannerWindowAccessible = false;
            this._missionConversationHandler = base.Mission.GetMissionBehavior<MissionConversationLogic>();
            SandBoxHelpers.MissionHelper.SpawnPlayer(base.Mission.DoesMissionRequireCivilianEquipment, true, false, false, "");
            this.LoadTutorialScores();
            this.SpawnConversationBrother();
            this.CollectWeaponsAndObjectives();
            this.InitializeMeleeTraining();
            this.InitializeMountedTraining();
            this.InitializeAdvancedMeleeTraining();
            this.InitializeBowTraining();
            this.MakeAllAgentsImmortal();
            this.SetHorseMountable(false);
            this.InitialCurrentObjective = new TextObject("{=BTY2aZCt}Enter a training area.", null);
            this._playerCampaignHealth = Agent.Main.Health;
        }

        private void LoadTutorialScores()
        {
            this._tutorialScores = StoryModeManager.Current.MainStoryLine.GetTutorialScores();
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            Agent.Main.Health = this._playerCampaignHealth;
            StoryModeManager.Current.MainStoryLine.SetTutorialScores(this._tutorialScores);
        }

        public override void OnRenderingStarted()
        {
            base.OnRenderingStarted();
            if (this._brotherConversationAgent != null)
            {
                base.Mission.GetMissionBehavior<MissionConversationLogic>().StartConversation(this._brotherConversationAgent, false, true);
            }
        }

        public override void OnMissionTick(float dt)
        {
            this.TrainingAreaUpdate();
            this.UpdateHorseBehavior();
            this.UpdateBowTraining();
            this.UpdateMountedAIBehavior();
            if (TrainingFieldMissionController._updateObjectivesWillBeCalled)
            {
                this.UpdateObjectives();
            }
            for (int i = this._delayedActions.Count - 1; i >= 0; i--)
            {
                if (this._delayedActions[i].Update())
                {
                    this._delayedActions.RemoveAt(i);
                }
            }
        }

        private void UpdateObjectives()
        {
            if (this._trainingSubTypeIndex == -1 || this._showTutorialObjectivesAnyway)
            {
                Action<List<TrainingFieldMissionController.TutorialObjective>> allObjectivesTick = this.AllObjectivesTick;
                if (allObjectivesTick != null)
                {
                    allObjectivesTick(this._tutorialObjectives);
                }
            }
            else
            {
                Action<List<TrainingFieldMissionController.TutorialObjective>> allObjectivesTick2 = this.AllObjectivesTick;
                if (allObjectivesTick2 != null)
                {
                    allObjectivesTick2(this._detailedObjectives);
                }
            }
            TrainingFieldMissionController._updateObjectivesWillBeCalled = false;
        }

        private int GetSelectedTrainingSubTypeIndex()
        {
            TrainingIcon activeTrainingIcon = this._activeTutorialArea.GetActiveTrainingIcon();
            if (activeTrainingIcon != null)
            {
                this.EnableAllTrainingIcons();
                activeTrainingIcon.DisableIcon();
                this._activeTrainingSubTypeTag = activeTrainingIcon.GetTrainingSubTypeTag();
                return this._activeTutorialArea.GetIndexFromTag(activeTrainingIcon.GetTrainingSubTypeTag());
            }
            return -1;
        }

        private string GetHighlightedWeaponRack()
        {
            foreach (TrainingIcon trainingIcon in this._activeTutorialArea.TrainingIconsReadOnly)
            {
                if (trainingIcon.Focused)
                {
                    return trainingIcon.GetTrainingSubTypeTag();
                }
            }
            return "";
        }

        private void EnableAllTrainingIcons()
        {
            foreach (TrainingIcon trainingIcon in this._activeTutorialArea.TrainingIconsReadOnly)
            {
                trainingIcon.EnableIcon();
            }
        }

        private void TrainingAreaUpdate()
        {
            this.CheckMainAgentEquipment();
            if (this._activeTutorialArea != null)
            {
                string[] array;
                if (this._activeTutorialArea.IsPositionInsideTutorialArea(Agent.Main.Position, out array))
                {
                    this.InTrainingArea();
                    if (this._trainingSubTypeIndex != -1)
                    {
                        this._activeTutorialArea.CheckWeapons(this._trainingSubTypeIndex);
                    }
                }
                else
                {
                    this.OnTrainingAreaExit(true);
                    this._activeTutorialArea = null;
                }
            }
            else
            {
                foreach (TutorialArea tutorialArea in this._trainingAreas)
                {
                    string[] array;
                    if (tutorialArea.IsPositionInsideTutorialArea(Agent.Main.Position, out array))
                    {
                        this._activeTutorialArea = tutorialArea;
                        this.OnTrainingAreaEnter();
                        break;
                    }
                }
            }
            this.UpdateConversationPermission();
        }

        private void UpdateConversationPermission()
        {
            if (this._brotherConversationAgent == null || Mission.Current.MainAgent == null || (this._brotherConversationAgent.Position - Mission.Current.MainAgent.Position).LengthSquared > 4f)
            {
                this._missionConversationHandler.DisableStartConversation(true);
                return;
            }
            this._missionConversationHandler.DisableStartConversation(false);
        }

        private void ResetTrainingArea()
        {
            this.OnTrainingAreaExit(true);
            this.OnTrainingAreaEnter();
        }

        private void OnTrainingAreaExit(bool enableTrainingIcons)
        {
            this._activeTutorialArea.MarkTrainingIcons(false);
            TrainingFieldMissionController.TutorialObjective tutorialObjective = this._tutorialObjectives.Find((TrainingFieldMissionController.TutorialObjective x) => x.Id == this._activeTutorialArea.TypeOfTraining.ToString());
            tutorialObjective.SetActive(false);
            tutorialObjective.SetAllSubTasksInactive();
            this.DropAllWeaponsOfMainAgent();
            this.SpecialTrainingAreaExit(this._activeTutorialArea.TypeOfTraining);
            this._activeTutorialArea.DeactivateAllWeapons(true);
            this._trainingProgress = 0;
            this._trainingSubTypeIndex = -1;
            this.EnableAllTrainingIcons();
            if (this.CheckAllObjectivesFinished())
            {
                this.CurrentObjectiveTick(new TextObject("{=77TavbOY}You have completed all tutorials. You can always come back to improve your score.", null));
                if (!this._courseFinished)
                {
                    this._courseFinished = true;
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/finish_course"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                }
            }
            else
            {
                this.CurrentObjectiveTick(new TextObject("{=BTY2aZCt}Enter a training area.", null));
            }
            this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
            this.UIEndTimer();
        }

        private bool CheckAllObjectivesFinished()
        {
            using (List<TrainingFieldMissionController.TutorialObjective>.Enumerator enumerator = this._tutorialObjectives.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (!enumerator.Current.IsFinished)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void OnTrainingAreaEnter()
        {
            this._tutorialObjectives.Find((TrainingFieldMissionController.TutorialObjective x) => x.Id == this._activeTutorialArea.TypeOfTraining.ToString()).SetActive(true);
            this.DropAllWeaponsOfMainAgent();
            this._trainingProgress = 0;
            this._trainingSubTypeIndex = -1;
            this.SpecialTrainingAreaEnter(this._activeTutorialArea.TypeOfTraining);
            this.CurrentObjectiveTick(new TextObject("{=WIUbM9Hc}Choose a weapon to begin training.", null));
            this._activeTutorialArea.MarkTrainingIcons(true);
        }

        private void InTrainingArea()
        {
            int selectedTrainingSubTypeIndex = this.GetSelectedTrainingSubTypeIndex();
            if (selectedTrainingSubTypeIndex >= 0)
            {
                this.OnStartTraining(selectedTrainingSubTypeIndex);
            }
            else
            {
                string highlightedWeaponRack = this.GetHighlightedWeaponRack();
                if (highlightedWeaponRack != "")
                {
                    using (List<TrainingFieldMissionController.TutorialObjective>.Enumerator enumerator = this._tutorialObjectives.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            TrainingFieldMissionController.TutorialObjective tutorialObjective = enumerator.Current;
                            if (tutorialObjective.Id == this._activeTutorialArea.TypeOfTraining.ToString())
                            {
                                using (List<TrainingFieldMissionController.TutorialObjective>.Enumerator enumerator2 = tutorialObjective.SubTasks.GetEnumerator())
                                {
                                    while (enumerator2.MoveNext())
                                    {
                                        TrainingFieldMissionController.TutorialObjective tutorialObjective2 = enumerator2.Current;
                                        if (tutorialObjective2.Id == highlightedWeaponRack)
                                        {
                                            tutorialObjective2.SetActive(true);
                                        }
                                        else
                                        {
                                            tutorialObjective2.SetActive(false);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        goto IL_FB;
                    }
                }
                this._tutorialObjectives.Find((TrainingFieldMissionController.TutorialObjective x) => x.Id == this._activeTutorialArea.TypeOfTraining.ToString()).SetAllSubTasksInactive();
            }
        IL_FB:
            this.SpecialInTrainingAreaUpdate(this._activeTutorialArea.TypeOfTraining);
        }

        private void OnStartTraining(int index)
        {
            this._showTutorialObjectivesAnyway = false;
            this._activeTutorialArea.MarkTrainingIcons(false);
            this.SpecialTrainingStart(this._activeTutorialArea.TypeOfTraining);
            this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
            this.UIEndTimer();
            this.DropAllWeaponsOfMainAgent();
            this._activeTutorialArea.DeactivateAllWeapons(true);
            this._activeTutorialArea.ActivateTaggedWeapons(index);
            this._activeTutorialArea.EquipWeaponsToPlayer(index);
            this._trainingProgress = 1;
            this._trainingSubTypeIndex = index;
            this.UpdateObjectives();
        }

        private void EndTraining()
        {
            this._trainingProgress = 0;
            this._trainingSubTypeIndex = -1;
            this._activeTutorialArea = null;
        }

        private void SuccessfullyFinishTraining(float score)
        {
            this._tutorialObjectives.Find((TrainingFieldMissionController.TutorialObjective x) => x.Id == this._activeTutorialArea.TypeOfTraining.ToString()).FinishSubTask(this._activeTrainingSubTypeTag, score);
            if (this._tutorialScores.ContainsKey(this._activeTrainingSubTypeTag))
            {
                this._tutorialScores[this._activeTrainingSubTypeTag] = score;
            }
            else
            {
                this._tutorialScores.Add(this._activeTrainingSubTypeTag, score);
            }
            this._activeTutorialArea.MarkTrainingIcons(true);
            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/finish_task"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
            this._showTutorialObjectivesAnyway = true;
            this.UpdateObjectives();
        }

        private void RefillAmmoOfAgent(Agent agent)
        {
            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)
            {
                if (agent.Equipment[equipmentIndex].IsAnyConsumable() && agent.Equipment[equipmentIndex].Amount <= 1)
                {
                    agent.SetWeaponAmountInSlot(equipmentIndex, agent.Equipment[equipmentIndex].ModifiedMaxAmount, true);
                }
            }
        }

        private void SpecialTrainingAreaExit(TutorialArea.TrainingType trainingType)
        {
            if (this._trainingSubTypeIndex != -1)
            {
                this._activeTutorialArea.ResetBreakables(this._trainingSubTypeIndex, true);
            }
            switch (trainingType)
            {
                case TutorialArea.TrainingType.Bow:
                    this.OnBowTrainingExit();
                    return;
                case TutorialArea.TrainingType.Melee:
                    break;
                case TutorialArea.TrainingType.Mounted:
                    this.OnMountedTrainingExit();
                    return;
                case TutorialArea.TrainingType.AdvancedMelee:
                    this.OnAdvancedTrainingExit();
                    break;
                default:
                    return;
            }
        }

        private void SpecialTrainingAreaEnter(TutorialArea.TrainingType trainingType)
        {
            switch (trainingType)
            {
                case TutorialArea.TrainingType.Bow:
                    this.OnBowTrainingEnter();
                    return;
                case TutorialArea.TrainingType.Melee:
                case TutorialArea.TrainingType.Mounted:
                    break;
                case TutorialArea.TrainingType.AdvancedMelee:
                    this.OnAdvancedTrainingAreaEnter();
                    break;
                default:
                    return;
            }
        }

        private void SpecialTrainingStart(TutorialArea.TrainingType trainingType)
        {
            if (this._trainingSubTypeIndex != -1)
            {
                this._activeTutorialArea.ResetBreakables(this._trainingSubTypeIndex, true);
            }
            switch (trainingType)
            {
                case TutorialArea.TrainingType.Bow:
                    this.OnBowTrainingStart();
                    return;
                case TutorialArea.TrainingType.Melee:
                    break;
                case TutorialArea.TrainingType.Mounted:
                    this.OnMountedTrainingStart();
                    return;
                case TutorialArea.TrainingType.AdvancedMelee:
                    this.OnAdvancedTrainingStart();
                    break;
                default:
                    return;
            }
        }

        private void SpecialInTrainingAreaUpdate(TutorialArea.TrainingType trainingType)
        {
            switch (trainingType)
            {
                case TutorialArea.TrainingType.Bow:
                    this.BowInTrainingAreaUpdate();
                    return;
                case TutorialArea.TrainingType.Melee:
                    this.MeleeTrainingUpdate();
                    return;
                case TutorialArea.TrainingType.Mounted:
                    this.MountedTrainingUpdate();
                    return;
                case TutorialArea.TrainingType.AdvancedMelee:
                    this.AdvancedMeleeTrainingUpdate();
                    return;
                default:
                    return;
            }
        }

        private void DropAllWeaponsOfMainAgent()
        {
            Mission.Current.MainAgent.SetActionChannel(1, ActionIndexCache.act_none, true, (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex <= EquipmentIndex.Weapon3; equipmentIndex++)
            {
                if (!Mission.Current.MainAgent.Equipment[equipmentIndex].IsEmpty)
                {
                    Mission.Current.MainAgent.DropItem(equipmentIndex, WeaponClass.Undefined);
                }
            }
        }

        private void RemoveAllWeaponsFromMainAgent()
        {
            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex <= EquipmentIndex.Weapon3; equipmentIndex++)
            {
                if (!Mission.Current.MainAgent.Equipment[equipmentIndex].IsEmpty)
                {
                    Mission.Current.MainAgent.RemoveEquippedWeapon(equipmentIndex);
                }
            }
        }

        private void CollectWeaponsAndObjectives()
        {
            List<GameEntity> list = new List<GameEntity>();
            Mission.Current.Scene.GetEntities(ref list);
            foreach (GameEntity gameEntity in list)
            {
                if (gameEntity.HasTag("bow_training_shooting_position"))
                {
                    this._shootingPosition = gameEntity;
                }
                if (gameEntity.GetFirstScriptOfType<TutorialArea>() != null)
                {
                    this._trainingAreas.Add(gameEntity.GetFirstScriptOfType<TutorialArea>());
                    this._tutorialObjectives.Add(new TrainingFieldMissionController.TutorialObjective(this._trainingAreas[this._trainingAreas.Count - 1].TypeOfTraining.ToString(), false, false, false));
                    foreach (string text in this._trainingAreas[this._trainingAreas.Count - 1].GetSubTrainingTags())
                    {
                        this._tutorialObjectives[this._tutorialObjectives.Count - 1].AddSubTask(new TrainingFieldMissionController.TutorialObjective(text, false, false, false));
                        if (this._tutorialScores.ContainsKey(text))
                        {
                            this._tutorialObjectives[this._tutorialObjectives.Count - 1].SubTasks.Last<TrainingFieldMissionController.TutorialObjective>().RestoreScoreFromSave(this._tutorialScores[text]);
                        }
                    }
                }
                if (gameEntity.HasTag("mounted_checkpoint") && gameEntity.GetFirstScriptOfType<VolumeBox>() != null)
                {
                    bool flag = false;
                    for (int i = 0; i < this._checkpoints.Count; i++)
                    {
                        if (int.Parse(gameEntity.Tags[1]) < int.Parse(this._checkpoints[i].Item1.GameEntity.Tags[1]))
                        {
                            this._checkpoints.Insert(i, ValueTuple.Create<VolumeBox, bool>(gameEntity.GetFirstScriptOfType<VolumeBox>(), false));
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        this._checkpoints.Add(ValueTuple.Create<VolumeBox, bool>(gameEntity.GetFirstScriptOfType<VolumeBox>(), false));
                    }
                }
                if (gameEntity.HasScriptOfType<DestructableComponent>())
                {
                    if (gameEntity.HasTag("_ranged_npc_target"))
                    {
                        this._targetsForRangedNpc.Add(gameEntity.GetFirstScriptOfType<DestructableComponent>());
                    }
                    else if (gameEntity.HasTag("_mounted_ai_target"))
                    {
                        int j = int.Parse(gameEntity.Tags[1]);
                        while (j > this._mountedAITargets.Count - 1)
                        {
                            this._mountedAITargets.Add(null);
                        }
                        this._mountedAITargets[j] = gameEntity.GetFirstScriptOfType<DestructableComponent>();
                    }
                }
            }
        }

        private void MakeAllAgentsImmortal()
        {
            foreach (Agent agent in Mission.Current.Agents)
            {
                agent.SetMortalityState(Agent.MortalityState.Immortal);
                if (!agent.IsMount)
                {
                    agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
                }
            }
        }

        private bool HasAllWeaponsPicked()
        {
            return this._activeTutorialArea.HasMainAgentPickedAll(this._trainingSubTypeIndex);
        }

        private void CheckMainAgentEquipment()
        {
            if (this._trainingSubTypeIndex == -1)
            {
                this.RemoveAllWeaponsFromMainAgent();
                return;
            }
            this._activeTutorialArea.CheckMainAgentEquipment(this._trainingSubTypeIndex);
        }

        private void StartTimer()
        {
            this._beginningTime = base.Mission.CurrentTime;
        }

        private void EndTimer()
        {
            this._timeScore = base.Mission.CurrentTime - this._beginningTime;
        }

        private void SpawnConversationBrother()
        {
            if (!TutorialPhase.Instance.TalkedWithBrotherForTheFirstTime)
            {
                WorldFrame worldFrame = new WorldFrame(Agent.Main.Frame.rotation, new WorldPosition(base.Mission.Scene, Agent.Main.Position));
                worldFrame.Origin.SetVec2(Agent.Main.GetWorldFrame().Origin.AsVec2 + Vec2.Forward * 3f);
                worldFrame.Rotation.RotateAboutUp(3.1415927f);
                MatrixFrame matrixFrame = worldFrame.ToGroundMatrixFrame();
                CharacterObject characterObject = StoryModeHeroes.ElderBrother.CharacterObject;
                AgentBuildData agentBuildData = new AgentBuildData(characterObject).Team(base.Mission.SpectatorTeam).InitialPosition(matrixFrame.origin);
                Vec2 vec = matrixFrame.rotation.f.AsVec2;
                vec = vec.Normalized();
                AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(vec).CivilianEquipment(false).NoHorses(true).NoWeapons(true).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, characterObject, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(characterObject.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, characterObject.GetMountKeySeed()));
                this._brotherConversationAgent = base.Mission.SpawnAgent(agentBuildData2, false);
            }
        }

        private void InitializeBowTraining()
        {
            this._shootingPosition.SetVisibilityExcludeParents(false);
            this._bowNpc = this.SpawnBowNPC();
            this._rangedNpcSpawnPosition = this._bowNpc.GetWorldPosition();
            this._bowNpc.SetAIBehaviorValues(HumanAIComponent.AISimpleBehaviorKind.Ranged, 0f, 6f, 0f, 66f, 0f);
            this._bowNpc.SetAIBehaviorValues(HumanAIComponent.AISimpleBehaviorKind.GoToPos, 0f, 6f, 0f, 66f, 0f);
            this._bowNpc.SetAIBehaviorValues(HumanAIComponent.AISimpleBehaviorKind.AttackEntityRanged, 66666f, 6f, 66666f, 120f, 66666f);
            this.GiveMoveOrderToRangedAgent(this._shootingPosition.GlobalPosition.ToWorldPosition(), this._shootingPosition.GetGlobalFrame().rotation.f.NormalizedCopy());
        }

        private void GiveMoveOrderToRangedAgent(WorldPosition worldPosition, Vec3 rotation)
        {
            if (worldPosition.AsVec2.NearlyEquals(this._rangedTargetPosition.AsVec2, 0.001f))
            {
                Vec3 groundVec = worldPosition.GetGroundVec3();
                Vec3 groundVec2 = this._rangedTargetPosition.GetGroundVec3();
                if (groundVec.NearlyEquals(groundVec2, 0.001f) && rotation.NearlyEquals(this._rangedTargetRotation, 1E-05f))
                {
                    return;
                }
            }
            this._rangedTargetPosition = worldPosition;
            this._rangedTargetRotation = rotation;
            this._bowNpc.SetWatchState(Agent.WatchState.Patrolling);
            this._targetPositionSet = false;
            this._delayedActions.Add(new TrainingFieldMissionController.DelayedAction(delegate ()
            {
                this._bowNpc.ClearTargetFrame();
                this._bowNpc.SetScriptedPositionAndDirection(ref worldPosition, this._rangedTargetRotation.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            }, 2f, "move order for ranged npc."));
        }

        private WeakGameEntity GetValidTarget()
        {
            foreach (DestructableComponent destructableComponent in this._targetsForRangedNpc)
            {
                if (!destructableComponent.IsDestroyed)
                {
                    this._lastTargetGiven = destructableComponent;
                    return this._lastTargetGiven.GameEntity;
                }
            }
            foreach (DestructableComponent destructableComponent2 in this._targetsForRangedNpc)
            {
                destructableComponent2.Reset();
            }
            this._lastTargetGiven = this._targetsForRangedNpc[0];
            return this._lastTargetGiven.GameEntity;
        }

        private void UpdateBowTraining()
        {
            if ((this._bowNpc.MovementFlags & Agent.MovementControlFlag.MoveMask) == Agent.MovementControlFlag.None && (this._rangedTargetPosition.GetGroundVec3() - this._bowNpc.Position).LengthSquared < 0.16000001f)
            {
                if (!this._targetPositionSet)
                {
                    this._bowNpc.DisableScriptedMovement();
                    Agent bowNpc = this._bowNpc;
                    Vec2 asVec = this._bowNpc.Position.AsVec2;
                    bowNpc.SetTargetPositionAndDirection(asVec, this._rangedTargetRotation);
                    this._targetPositionSet = true;
                    if ((this._bowNpc.Position - this._shootingPosition.GlobalPosition).LengthSquared > (this._bowNpc.Position - this._rangedNpcSpawnPosition.GetGroundVec3()).LengthSquared)
                    {
                        this._atShootingPosition = false;
                        return;
                    }
                    this._bowNpc.SetWatchState(Agent.WatchState.Alarmed);
                    this._bowNpc.SetScriptedTargetEntityAndPosition(this.GetValidTarget(), this._bowNpc.GetWorldPosition(), Agent.AISpecialCombatModeFlags.None, false);
                    this._atShootingPosition = true;
                    return;
                }
                else if (this._atShootingPosition && this._lastTargetGiven.IsDestroyed)
                {
                    this._bowNpc.SetScriptedTargetEntityAndPosition(this.GetValidTarget(), this._bowNpc.GetWorldPosition(), Agent.AISpecialCombatModeFlags.None, false);
                }
            }
        }

        private void OnBowTrainingEnter()
        {
        }

        private Agent SpawnBowNPC()
        {
            MatrixFrame matrixFrame = MatrixFrame.Identity;
            this._rangedNpcSpawnPoint = base.Mission.Scene.FindEntityWithTag("spawner_ranged_npc_tag");
            if (this._rangedNpcSpawnPoint != null)
            {
                matrixFrame = this._rangedNpcSpawnPoint.GetGlobalFrame();
                matrixFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            else
            {
                Debug.FailedAssert("There are no spawn points for bow npc.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "SpawnBowNPC", 1129);
            }
            Location locationWithId = LocationComplex.Current.GetLocationWithId("training_field");
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("tutorial_npc_ranged");
            Monster baseMonsterFromRace = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(@object.Race);
            AgentData agentData = new AgentData(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).Monster(baseMonsterFromRace).NoHorses(true);
            locationWithId.AddCharacter(new LocationCharacter(agentData, new LocationCharacter.AddBehaviorsDelegate(SandBoxManager.Instance.AgentBehaviorManager.AddFixedCharacterBehaviors), null, true, LocationCharacter.CharacterRelations.Friendly, null, true, false, null, false, true, true, null, false));
            AgentBuildData agentBuildData = new AgentBuildData(@object).Team(base.Mission.PlayerTeam).InitialPosition(matrixFrame.origin);
            Vec2 asVec = matrixFrame.rotation.f.AsVec2;
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(asVec).CivilianEquipment(false).NoHorses(true).NoWeapons(false).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(@object.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, @object.GetMountKeySeed())).Controller(AgentControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.SetTeam(Mission.Current.PlayerAllyTeam, false);
            return agent;
        }

        private void BowInTrainingAreaUpdate()
        {
            if (this._trainingProgress == 1)
            {
                if (this.HasAllWeaponsPicked())
                {
                    this._rangedLastBrokenTargetCount = 0;
                    this.LoadCrossbowForStarting();
                    this._trainingProgress++;
                    this.CurrentObjectiveTick(new TextObject("{=kwW6v202}Go to shooting position", null));
                    this._shootingPosition.SetVisibilityExcludeParents(true);
                    this._detailedObjectives = this._rangedObjectives.ConvertAll<TrainingFieldMissionController.TutorialObjective>((TrainingFieldMissionController.TutorialObjective x) => new TrainingFieldMissionController.TutorialObjective(x.Id, x.IsFinished, x.IsActive, x.HasBackground));
                    this._detailedObjectives[1].SetTextVariableOfName("HIT", this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex));
                    this._detailedObjectives[1].SetTextVariableOfName("ALL", this._activeTutorialArea.GetBreakablesCount(this._trainingSubTypeIndex));
                    this._detailedObjectives[0].SetActive(true);
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/archery/pick_" + this._trainingSubTypeIndex), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                    return;
                }
            }
            else if (this._trainingProgress == 2)
            {
                if ((this._shootingPosition.GetGlobalFrame().origin - Agent.Main.Position).LengthSquared < 4f)
                {
                    this._trainingProgress++;
                    this._shootingPosition.SetVisibilityExcludeParents(false);
                    this._activeTutorialArea.MarkAllTargets(this._trainingSubTypeIndex, true);
                    this._remainingTargetText.SetTextVariable("REMAINING_TARGET", this._activeTutorialArea.GetUnbrokenBreakableCount(this._trainingSubTypeIndex));
                    this.CurrentObjectiveTick(this._remainingTargetText);
                    this._detailedObjectives[0].FinishTask();
                    this._detailedObjectives[1].SetActive(true);
                    return;
                }
            }
            else if (this._trainingProgress == 4)
            {
                int brokenBreakableCount = this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex);
                this._remainingTargetText.SetTextVariable("REMAINING_TARGET", this._activeTutorialArea.GetUnbrokenBreakableCount(this._trainingSubTypeIndex));
                this.CurrentObjectiveTick(this._remainingTargetText);
                this._detailedObjectives[1].SetTextVariableOfName("HIT", brokenBreakableCount);
                if (brokenBreakableCount != this._rangedLastBrokenTargetCount)
                {
                    this._rangedLastBrokenTargetCount = brokenBreakableCount;
                    this._activeTutorialArea.ResetMarkingTargetTimers(this._trainingSubTypeIndex);
                }
                if (MBRandom.NondeterministicRandomInt % 4 == 3)
                {
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/archery/hit_target"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                }
                if (this._activeTutorialArea.AllBreakablesAreBroken(this._trainingSubTypeIndex))
                {
                    this._detailedObjectives[1].FinishTask();
                    this._trainingProgress++;
                    this.BowTrainingEndedSuccessfully();
                }
            }
        }

        public void LoadCrossbowForStarting()
        {
            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)
            {
                MissionWeapon missionWeapon = Agent.Main.Equipment[equipmentIndex];
                if (!missionWeapon.IsEmpty && missionWeapon.Item.PrimaryWeapon.WeaponClass == WeaponClass.Crossbow && missionWeapon.Ammo == 0)
                {
                    int num;
                    EquipmentIndex ammoSlotIndex;
                    Agent.Main.Equipment.GetAmmoCountAndIndexOfType(missionWeapon.Item.Type, out num, out ammoSlotIndex, EquipmentIndex.None);
                    Agent.Main.SetReloadAmmoInSlot(equipmentIndex, ammoSlotIndex, 1);
                    Agent.Main.SetWeaponReloadPhaseAsClient(equipmentIndex, missionWeapon.ReloadPhaseCount);
                }
            }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex = -1)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
            TutorialArea activeTutorialArea = this._activeTutorialArea;
            if (activeTutorialArea != null && activeTutorialArea.TypeOfTraining == TutorialArea.TrainingType.Bow && this._trainingProgress == 3)
            {
                this._trainingProgress++;
                this._activeTutorialArea.MakeDestructible(this._trainingSubTypeIndex);
                this.UIStartTimer();
                this.CurrentObjectiveTick(new TextObject("{=9kGnzjrU}Timer Started.", null));
                this.StartTimer();
                Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/archery/start_training"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
            }
            this.RefillAmmoOfAgent(shooterAgent);
        }

        private void BowTrainingEndedSuccessfully()
        {
            this.EndTimer();
            this._activeTutorialArea.HideBoundaries();
            this.CurrentObjectiveTick(this._trainingFinishedText);
            TextObject textObject = new TextObject("{=xVFupnFu}You've successfully hit all of the targets in ({TIME_SCORE}) seconds.", null);
            float score = this.UIEndTimer();
            textObject.SetTextVariable("TIME_SCORE", new TextObject(score.ToString("0.0"), null));
            MBInformationManager.AddQuickInformation(textObject, 0, null, null, "");
            this.SuccessfullyFinishTraining(score);
            this._shootingPosition.SetVisibilityExcludeParents(false);
            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/archery/finish"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
        }

        private void OnBowTrainingStart()
        {
            this._shootingPosition.SetVisibilityExcludeParents(false);
            this.GiveMoveOrderToRangedAgent(this._rangedNpcSpawnPoint.GlobalPosition.ToWorldPosition(), this._rangedNpcSpawnPoint.GetGlobalFrame().rotation.f.NormalizedCopy());
            foreach (DestructableComponent destructableComponent in this._targetsForRangedNpc)
            {
                destructableComponent.Reset();
                destructableComponent.GameEntity.SetVisibilityExcludeParents(false);
            }
        }

        private void OnBowTrainingExit()
        {
            this._shootingPosition.SetVisibilityExcludeParents(false);
            this.GiveMoveOrderToRangedAgent(this._shootingPosition.GlobalPosition.ToWorldPosition(), this._shootingPosition.GetGlobalFrame().rotation.f.NormalizedCopy());
            foreach (DestructableComponent destructableComponent in this._targetsForRangedNpc)
            {
                destructableComponent.Reset();
                destructableComponent.GameEntity.SetVisibilityExcludeParents(true);
            }
        }

        private void InitializeAdvancedMeleeTraining()
        {
            this._advancedMeleeTrainerEasy = this.SpawnAdvancedMeleeTrainerEasy();
            this._advancedMeleeTrainerEasy.SetAgentFlags(this._advancedMeleeTrainerEasy.GetAgentFlags() & ~AgentFlag.CanGetAlarmed);
            this._advancedMeleeTrainerEasyInitialPosition = base.Mission.Scene.FindEntityWithTag("spawner_adv_melee_npc_easy").GetGlobalFrame();
            this._advancedMeleeTrainerEasySecondPosition = base.Mission.Scene.FindEntityWithTag("adv_melee_npc_easy_second_pos").GetGlobalFrame();
            this._advancedMeleeTrainerNormal = this.SpawnAdvancedMeleeTrainerNormal();
            this._advancedMeleeTrainerNormal.SetAgentFlags(this._advancedMeleeTrainerNormal.GetAgentFlags() & ~AgentFlag.CanGetAlarmed);
            this._advancedMeleeTrainerNormalInitialPosition = base.Mission.Scene.FindEntityWithTag("spawner_adv_melee_npc_normal").GetGlobalFrame();
            this._advancedMeleeTrainerNormalSecondPosition = base.Mission.Scene.FindEntityWithTag("adv_melee_npc_normal_second_pos").GetGlobalFrame();
            this.BeginNPCFight();
        }

        private Agent SpawnAdvancedMeleeTrainerEasy()
        {
            this._advancedMeleeTrainerEasyInitialPosition = MatrixFrame.Identity;
            GameEntity gameEntity = base.Mission.Scene.FindEntityWithTag("spawner_adv_melee_npc_easy");
            if (gameEntity != null)
            {
                this._advancedMeleeTrainerEasyInitialPosition = gameEntity.GetGlobalFrame();
                this._advancedMeleeTrainerEasyInitialPosition.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            else
            {
                Debug.FailedAssert("There are no spawn points for advanced melee trainer.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "SpawnAdvancedMeleeTrainerEasy", 1347);
            }
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("tutorial_npc_advanced_melee_easy");
            AgentBuildData agentBuildData = new AgentBuildData(@object).Team(base.Mission.PlayerTeam).InitialPosition(this._advancedMeleeTrainerEasyInitialPosition.origin);
            Vec2 asVec = this._advancedMeleeTrainerEasyInitialPosition.rotation.f.AsVec2;
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(asVec).CivilianEquipment(false).NoHorses(true).NoWeapons(false).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(@object.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, @object.GetMountKeySeed())).Controller(AgentControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.SetTeam(Mission.Current.DefenderTeam, false);
            return agent;
        }

        private Agent SpawnAdvancedMeleeTrainerNormal()
        {
            this._advancedMeleeTrainerNormalInitialPosition = MatrixFrame.Identity;
            GameEntity gameEntity = base.Mission.Scene.FindEntityWithTag("spawner_adv_melee_npc_normal");
            if (gameEntity != null)
            {
                this._advancedMeleeTrainerNormalInitialPosition = gameEntity.GetGlobalFrame();
                this._advancedMeleeTrainerNormalInitialPosition.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            else
            {
                Debug.FailedAssert("There are no spawn points for advanced melee trainer.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "SpawnAdvancedMeleeTrainerNormal", 1379);
            }
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("tutorial_npc_advanced_melee_normal");
            AgentBuildData agentBuildData = new AgentBuildData(@object).Team(base.Mission.PlayerTeam).InitialPosition(this._advancedMeleeTrainerNormalInitialPosition.origin);
            Vec2 asVec = this._advancedMeleeTrainerNormalInitialPosition.rotation.f.AsVec2;
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(asVec).CivilianEquipment(false).NoHorses(true).NoWeapons(false).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(@object.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, @object.GetMountKeySeed())).Controller(AgentControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.SetTeam(Mission.Current.DefenderTeam, false);
            return agent;
        }

        private void AdvancedMeleeTrainingUpdate()
        {
            if (this._trainingSubTypeIndex != -1)
            {
                if (this._trainingProgress == 1)
                {
                    if (this.HasAllWeaponsPicked())
                    {
                        this._playerLeftBattleArea = false;
                        this._detailedObjectives = this._advMeleeObjectives.ConvertAll<TrainingFieldMissionController.TutorialObjective>((TrainingFieldMissionController.TutorialObjective x) => new TrainingFieldMissionController.TutorialObjective(x.Id, x.IsFinished, x.IsActive, x.HasBackground));
                        this._detailedObjectives[0].SetActive(true);
                        this._trainingProgress++;
                        this.CurrentObjectiveTick(new TextObject("{=HhuBPfJn}Go to the trainer.", null));
                        WorldPosition worldPosition = this._advancedMeleeTrainerNormalSecondPosition.origin.ToWorldPosition();
                        this._advancedMeleeTrainerNormal.SetScriptedPositionAndDirection(ref worldPosition, this._advancedMeleeTrainerNormalSecondPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
                        this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.PlayerAllyTeam, false);
                        this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.PlayerAllyTeam, false);
                        return;
                    }
                }
                else if (this._trainingProgress == 2)
                {
                    if ((this._advancedMeleeTrainerEasy.Position - Agent.Main.Position).LengthSquared < 6f)
                    {
                        this._detailedObjectives[0].FinishTask();
                        this._detailedObjectives[1].SetActive(true);
                        this._timer = base.Mission.CurrentTime;
                        this._trainingProgress++;
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 3);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                }
                else if (this._trainingProgress == 3)
                {
                    if (base.Mission.CurrentTime - this._timer > 3f)
                    {
                        this._playerHealth = Agent.Main.HealthLimit;
                        this._advancedMeleeTrainerEasyHealth = this._advancedMeleeTrainerEasy.HealthLimit;
                        this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.PlayerEnemyTeam, false);
                        this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.PlayerEnemyTeam, false);
                        this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Alarmed);
                        this._advancedMeleeTrainerEasy.DisableScriptedMovement();
                        this._trainingProgress++;
                        this.CurrentObjectiveTick(new TextObject("{=4hdp6SK0}Defeat the trainer!", null));
                        return;
                    }
                    if (base.Mission.CurrentTime - this._timer > 2f)
                    {
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 1);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                    if (base.Mission.CurrentTime - this._timer > 1f)
                    {
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 2);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                }
                else if (this._trainingProgress == 4)
                {
                    if (this._playerHealth <= 1f)
                    {
                        this._trainingProgress = 9;
                        this.CurrentObjectiveTick(new TextObject("{=SvYCz6z6}You've lost. You can restart the training by interacting weapon rack.", null));
                        this._timer = base.Mission.CurrentTime;
                        Agent.Main.SetActionChannel(0, ActionIndexCache.act_strike_fall_back_back_rise, false, (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                        Agent.Main.Health = 1.1f;
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/fighting/player_lose"), this._advancedMeleeTrainerNormal.GetEyeGlobalPosition(), true, false, -1, -1);
                        this.OnLost();
                        return;
                    }
                    if (this._advancedMeleeTrainerEasyHealth <= 1f)
                    {
                        this._detailedObjectives[1].FinishTask();
                        this._detailedObjectives[2].SetActive(true);
                        this.CurrentObjectiveTick(new TextObject("{=ikhWkw7T}You've successfully defeated rookie trainer. Go to veteran trainer.", null));
                        this._timer = base.Mission.CurrentTime;
                        this._trainingProgress++;
                        this.OnEasyTrainerBeaten();
                        this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.PlayerAllyTeam, false);
                        this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.PlayerAllyTeam, false);
                        this._advancedMeleeTrainerEasy.SetActionChannel(0, ActionIndexCache.act_strike_fall_back_back_rise, false, (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                        return;
                    }
                    Agent.Main.Health = this._playerHealth;
                    this.CheckAndHandlePlayerInsideBattleArea();
                    return;
                }
                else if (this._trainingProgress == 5)
                {
                    if ((this._advancedMeleeTrainerNormal.Position - Agent.Main.Position).LengthSquared < 6f && (this._advancedMeleeTrainerNormal.Position - this._advancedMeleeTrainerNormalInitialPosition.origin).LengthSquared < 6f)
                    {
                        this._timer = base.Mission.CurrentTime;
                        this._trainingProgress++;
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 3);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                }
                else if (this._trainingProgress == 6)
                {
                    if (base.Mission.CurrentTime - this._timer > 3f)
                    {
                        this._playerHealth = Agent.Main.HealthLimit;
                        this._advancedMeleeTrainerNormalHealth = this._advancedMeleeTrainerNormal.HealthLimit;
                        this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.PlayerEnemyTeam, false);
                        this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.PlayerEnemyTeam, false);
                        this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Alarmed);
                        this._advancedMeleeTrainerNormal.DisableScriptedMovement();
                        this._trainingProgress++;
                        this.CurrentObjectiveTick(new TextObject("{=4hdp6SK0}Defeat the trainer!", null));
                        return;
                    }
                    if (base.Mission.CurrentTime - this._timer > 2f)
                    {
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 1);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                    if (base.Mission.CurrentTime - this._timer > 1f)
                    {
                        this._fightStartsIn.SetTextVariable("REMAINING_TIME", 2);
                        this.CurrentObjectiveTick(this._fightStartsIn);
                        return;
                    }
                }
                else if (this._trainingProgress == 7)
                {
                    if (this._playerHealth <= 1f)
                    {
                        this.ResetTrainingArea();
                        this.CurrentObjectiveTick(new TextObject("{=SvYCz6z6}You've lost. You can restart the training by interacting weapon rack.", null));
                        this._timer = base.Mission.CurrentTime;
                        this._trainingProgress++;
                        Agent.Main.SetActionChannel(0, ActionIndexCache.act_strike_fall_back_back_rise, false, (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                        Agent.Main.Health = 1.1f;
                        this.OnLost();
                        return;
                    }
                    if (this._advancedMeleeTrainerNormalHealth <= 1f)
                    {
                        this._detailedObjectives[2].FinishTask();
                        this.SuccessfullyFinishTraining(0f);
                        this.CurrentObjectiveTick(new TextObject("{=1RaUauBS}You've successfully finished the training.", null));
                        this._timer = base.Mission.CurrentTime;
                        this._trainingProgress++;
                        this.MakeTrainersPatrolling();
                        this._advancedMeleeTrainerNormal.SetActionChannel(0, ActionIndexCache.act_strike_fall_back_back_rise, false, (AnimFlags)0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/fighting/player_win"), this._advancedMeleeTrainerNormal.GetEyeGlobalPosition(), true, false, -1, -1);
                        return;
                    }
                    Agent.Main.Health = this._playerHealth;
                    this.CheckAndHandlePlayerInsideBattleArea();
                }
            }
        }

        private void CheckAndHandlePlayerInsideBattleArea()
        {
            string[] source;
            if (this._activeTutorialArea.IsPositionInsideTutorialArea(Agent.Main.Position, out source))
            {
                if (string.IsNullOrEmpty(source.FirstOrDefault((string x) => x == "battle_area")))
                {
                    if (!this._playerLeftBattleArea)
                    {
                        this._playerLeftBattleArea = true;
                        this.OnPlayerLeftBattleArea();
                        return;
                    }
                }
                else if (this._playerLeftBattleArea)
                {
                    this._playerLeftBattleArea = false;
                    this.OnPlayerReEnteredBattleArea();
                }
            }
        }

        private void OnPlayerLeftBattleArea()
        {
            if (this._trainingProgress == 4)
            {
                this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Patrolling);
                WorldPosition worldPosition = this._advancedMeleeTrainerEasyInitialPosition.origin.ToWorldPosition();
                this._advancedMeleeTrainerEasy.SetScriptedPositionAndDirection(ref worldPosition, this._advancedMeleeTrainerEasySecondPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
                return;
            }
            if (this._trainingProgress == 7)
            {
                this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Patrolling);
                WorldPosition worldPosition2 = this._advancedMeleeTrainerNormalInitialPosition.origin.ToWorldPosition();
                this._advancedMeleeTrainerNormal.SetScriptedPositionAndDirection(ref worldPosition2, this._advancedMeleeTrainerNormalInitialPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            }
        }

        private void OnPlayerReEnteredBattleArea()
        {
            if (this._trainingProgress == 4)
            {
                this._advancedMeleeTrainerEasy.DisableScriptedMovement();
                this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Alarmed);
                return;
            }
            if (this._trainingProgress == 7)
            {
                this._advancedMeleeTrainerNormal.DisableScriptedMovement();
                this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Alarmed);
            }
        }

        private void OnEasyTrainerBeaten()
        {
            this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Patrolling);
            WorldPosition worldPosition = this._advancedMeleeTrainerEasySecondPosition.origin.ToWorldPosition();
            this._advancedMeleeTrainerEasy.SetScriptedPositionAndDirection(ref worldPosition, this._advancedMeleeTrainerEasySecondPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Patrolling);
            WorldPosition worldPosition2 = this._advancedMeleeTrainerNormalInitialPosition.origin.ToWorldPosition();
            this._advancedMeleeTrainerNormal.SetScriptedPositionAndDirection(ref worldPosition2, this._advancedMeleeTrainerNormalInitialPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            Agent.Main.Health = Agent.Main.HealthLimit;
        }

        private void MakeTrainersPatrolling()
        {
            WorldPosition worldPosition = this._advancedMeleeTrainerEasyInitialPosition.origin.ToWorldPosition();
            this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Patrolling);
            this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.PlayerAllyTeam, false);
            this._advancedMeleeTrainerEasy.SetScriptedPositionAndDirection(ref worldPosition, this._advancedMeleeTrainerEasyInitialPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            this.SetAgentDefensiveness(this._advancedMeleeTrainerNormal, 0f);
            WorldPosition worldPosition2 = this._advancedMeleeTrainerNormalInitialPosition.origin.ToWorldPosition();
            this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Patrolling);
            this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.PlayerAllyTeam, false);
            this._advancedMeleeTrainerNormal.SetScriptedPositionAndDirection(ref worldPosition2, this._advancedMeleeTrainerNormalInitialPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            this.SetAgentDefensiveness(this._advancedMeleeTrainerNormal, 0f);
            this._delayedActions.Add(new TrainingFieldMissionController.DelayedAction(delegate ()
            {
                Agent.Main.Health = Agent.Main.HealthLimit;
            }, 1.5f, "Agent health recover after advanced melee fight"));
        }

        private void OnLost()
        {
            this.MakeTrainersPatrolling();
        }

        private void BeginNPCFight()
        {
            this._advancedMeleeTrainerEasy.DisableScriptedMovement();
            this._advancedMeleeTrainerEasy.SetWatchState(Agent.WatchState.Alarmed);
            this._advancedMeleeTrainerEasy.SetTeam(Mission.Current.DefenderTeam, false);
            this.SetAgentDefensiveness(this._advancedMeleeTrainerEasy, 4f);
            this._advancedMeleeTrainerNormal.DisableScriptedMovement();
            this._advancedMeleeTrainerNormal.SetWatchState(Agent.WatchState.Alarmed);
            this._advancedMeleeTrainerNormal.SetTeam(Mission.Current.AttackerTeam, false);
            this.SetAgentDefensiveness(this._advancedMeleeTrainerNormal, 4f);
        }

        private void OnAdvancedTrainingStart()
        {
            this.MakeTrainersPatrolling();
            Agent.Main.Health = Agent.Main.HealthLimit;
        }

        private void OnAdvancedTrainingExit()
        {
            Agent.Main.Health = Agent.Main.HealthLimit;
            this.BeginNPCFight();
        }

        private void OnAdvancedTrainingAreaEnter()
        {
            this.MakeTrainersPatrolling();
            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/fighting/greet"), this._advancedMeleeTrainerNormal.GetEyeGlobalPosition(), true, false, -1, -1);
        }

        private void SetAgentDefensiveness(Agent agent, float formationOrderDefensivenessFactor)
        {
            agent.Defensiveness = formationOrderDefensivenessFactor;
        }

        private void InitializeMeleeTraining()
        {
            MatrixFrame matrixFrame = MatrixFrame.Identity;
            GameEntity gameEntity = base.Mission.Scene.FindEntityWithTag("spawner_melee_npc");
            if (gameEntity != null)
            {
                matrixFrame = gameEntity.GetGlobalFrame();
                matrixFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            else
            {
                Debug.FailedAssert("There are no spawn points for basic melee trainer.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "InitializeMeleeTraining", 1729);
            }
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("tutorial_npc_basic_melee");
            AgentBuildData agentBuildData = new AgentBuildData(@object).Team(base.Mission.PlayerTeam).InitialPosition(matrixFrame.origin);
            Vec2 asVec = matrixFrame.rotation.f.AsVec2;
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(asVec).CivilianEquipment(false).NoHorses(true).NoWeapons(false).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(@object.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, @object.GetMountKeySeed())).Controller(AgentControllerType.None);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.SetTeam(Mission.Current.DefenderTeam, false);
            this._meleeTrainer = agent;
            this._meleeTrainerDefaultPosition = this._meleeTrainer.GetWorldPosition();
        }

        private void MeleeTrainingUpdate()
        {
            float lengthSquared = (this._meleeTrainer.Position - this._meleeTrainerDefaultPosition.GetGroundVec3()).LengthSquared;
            if (lengthSquared > 1f)
            {
                if (this._meleeTrainer.MovementFlags == Agent.MovementControlFlag.DefendDown)
                {
                    this._meleeTrainer.MovementFlags &= ~Agent.MovementControlFlag.DefendDown;
                }
                else if ((this._meleeTrainer.MovementFlags & Agent.MovementControlFlag.AttackMask) > Agent.MovementControlFlag.None)
                {
                    this._meleeTrainer.MovementFlags &= ~(Agent.MovementControlFlag.AttackLeft | Agent.MovementControlFlag.AttackRight | Agent.MovementControlFlag.AttackUp | Agent.MovementControlFlag.AttackDown);
                    this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendDown;
                }
                else
                {
                    this._meleeTrainer.SetTargetPosition(this._meleeTrainerDefaultPosition.AsVec2);
                }
                this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
                return;
            }
            if (lengthSquared < 0.1f)
            {
                this.SwordTraining();
            }
        }

        private void SwordTraining()
        {
            if (this._trainingProgress == 1)
            {
                if (this.HasAllWeaponsPicked())
                {
                    this._detailedObjectives = this._meleeObjectives.ConvertAll<TrainingFieldMissionController.TutorialObjective>((TrainingFieldMissionController.TutorialObjective x) => new TrainingFieldMissionController.TutorialObjective(x.Id, x.IsFinished, x.IsActive, x.HasBackground));
                    this._detailedObjectives[1].SetTextVariableOfName("HIT", 0);
                    this._detailedObjectives[1].SetTextVariableOfName("ALL", 4);
                    this._detailedObjectives[2].SetTextVariableOfName("HIT", 0);
                    this._detailedObjectives[2].SetTextVariableOfName("ALL", 4);
                    this._detailedObjectives[0].SetActive(true);
                    this._trainingProgress++;
                    this.CurrentObjectiveTick(new TextObject("{=Zb1uFhsY}Go to trainer.", null));
                }
                this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
                return;
            }
            Vec3 vec = this._meleeTrainer.Position - Agent.Main.Position;
            if (vec.LengthSquared < 4f)
            {
                Agent meleeTrainer = this._meleeTrainer;
                vec = this._meleeTrainer.Position;
                Vec2 asVec = vec.AsVec2;
                vec = Agent.Main.GetEyeGlobalPosition() - this._meleeTrainer.GetWorldFrame().Rotation.s * 0.1f - this._meleeTrainer.GetEyeGlobalPosition();
                meleeTrainer.SetTargetPositionAndDirection(asVec, vec);
                if (this._trainingProgress == 2)
                {
                    this._detailedObjectives[0].FinishTask();
                    this._detailedObjectives[1].SetActive(true);
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/block_left"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                    this.CurrentObjectiveTick(new TextObject("{=Db98U6fF}Defend from left.", null));
                    this._trainingProgress++;
                    return;
                }
                if (this._trainingProgress == 3)
                {
                    if (base.Mission.CurrentTime - this._timer > 2f && Agent.Main.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendLeft && Agent.Main.GetCurrentActionProgress(1) > 0.1f && Agent.Main.GetCurrentActionType(1) != Agent.ActionCodeType.Guard)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                    }
                    else
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackRight;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.DefendLeft);
                    return;
                }
                if (this._trainingProgress == 4)
                {
                    if (base.Mission.CurrentTime - this._timer > 1.5f && Agent.Main.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendRight && Agent.Main.GetCurrentActionProgress(1) > 0.1f && Agent.Main.GetCurrentActionType(1) != Agent.ActionCodeType.Guard)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                    }
                    else
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackLeft;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.DefendRight);
                    return;
                }
                if (this._trainingProgress == 5)
                {
                    if (base.Mission.CurrentTime - this._timer > 1.5f && Agent.Main.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackEnd && Agent.Main.GetCurrentActionProgress(1) > 0.1f && Agent.Main.GetCurrentActionType(1) != Agent.ActionCodeType.Guard)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                    }
                    else
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackUp;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.DefendUp);
                    return;
                }
                if (this._trainingProgress == 6)
                {
                    if (base.Mission.CurrentTime - this._timer > 1.5f && Agent.Main.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendDown && Agent.Main.GetCurrentActionProgress(1) > 0.1f && Agent.Main.GetCurrentActionType(1) != Agent.ActionCodeType.Guard)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                    }
                    else
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackDown;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.DefendDown);
                    return;
                }
                if (this._trainingProgress == 7)
                {
                    this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendRight;
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.AttackLeft);
                    return;
                }
                if (this._trainingProgress == 8)
                {
                    if (base.Mission.CurrentTime - this._timer > 1f)
                    {
                        this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendLeft;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.AttackRight);
                    return;
                }
                if (this._trainingProgress == 9)
                {
                    if (base.Mission.CurrentTime - this._timer > 1f)
                    {
                        this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendUp;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.AttackUp);
                    return;
                }
                if (this._trainingProgress == 10)
                {
                    if (base.Mission.CurrentTime - this._timer > 1f)
                    {
                        this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendDown;
                    }
                    this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.AttackDown);
                    return;
                }
                if (this._trainingProgress == 11)
                {
                    this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                    this._trainingProgress++;
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/praise"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                    this.SuccessfullyFinishTraining(0f);
                    return;
                }
            }
            else
            {
                this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
                if (this._meleeTrainer.MovementFlags == Agent.MovementControlFlag.DefendDown)
                {
                    this._meleeTrainer.MovementFlags &= ~Agent.MovementControlFlag.DefendDown;
                    return;
                }
                if ((this._meleeTrainer.MovementFlags & Agent.MovementControlFlag.AttackMask) > Agent.MovementControlFlag.None)
                {
                    this._meleeTrainer.MovementFlags &= ~(Agent.MovementControlFlag.AttackLeft | Agent.MovementControlFlag.AttackRight | Agent.MovementControlFlag.AttackUp | Agent.MovementControlFlag.AttackDown);
                    this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendDown;
                }
            }
        }

        private void ShieldTraining()
        {
            if (this._trainingProgress == 1)
            {
                if (this.HasAllWeaponsPicked())
                {
                    this._trainingProgress++;
                    MBInformationManager.AddQuickInformation(new TextObject("{=Zb1uFhsY}Go to trainer.", null), 0, null, null, "");
                    return;
                }
            }
            else if ((this._meleeTrainer.Position - Agent.Main.Position).LengthSquared < 3f)
            {
                if (this._trainingProgress == 2)
                {
                    this._meleeTrainer.SetLookAgent(Agent.Main);
                    if ((this._meleeTrainer.Position - Agent.Main.Position).LengthSquared < 1.5f)
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=WysXGbM6}Right click to defend", null), 0, null, null, "");
                        this._trainingProgress++;
                        return;
                    }
                }
                else if (this._trainingProgress == 3)
                {
                    if (base.Mission.CurrentTime - this._timer > 2f && (Agent.Main.MovementFlags & Agent.MovementControlFlag.DefendMask) > Agent.MovementControlFlag.None)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                        return;
                    }
                    this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackLeft;
                    return;
                }
                else if (this._trainingProgress == 4)
                {
                    if (base.Mission.CurrentTime - this._timer > 2f && (Agent.Main.MovementFlags & Agent.MovementControlFlag.DefendMask) > Agent.MovementControlFlag.None)
                    {
                        this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                        this._timer = base.Mission.CurrentTime;
                        return;
                    }
                    this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.AttackRight;
                    return;
                }
                else if (this._trainingProgress == 5)
                {
                    this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                    return;
                }
            }
            else
            {
                if (this._meleeTrainer.MovementFlags == Agent.MovementControlFlag.DefendDown)
                {
                    this._meleeTrainer.MovementFlags &= ~Agent.MovementControlFlag.DefendDown;
                    return;
                }
                if ((this._meleeTrainer.MovementFlags & Agent.MovementControlFlag.AttackMask) > Agent.MovementControlFlag.None)
                {
                    this._meleeTrainer.MovementFlags &= ~(Agent.MovementControlFlag.AttackLeft | Agent.MovementControlFlag.AttackRight | Agent.MovementControlFlag.AttackUp | Agent.MovementControlFlag.AttackDown);
                    this._meleeTrainer.MovementFlags |= Agent.MovementControlFlag.DefendDown;
                }
            }
        }

        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
        {
            base.OnScoreHit(affectedAgent, affectorAgent, attackerWeapon, isBlocked, isSiegeEngineHit, blow, collisionData, damagedHp, hitDistance, shotDifficulty);
            if (isBlocked)
            {
                for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex <= EquipmentIndex.Weapon3; equipmentIndex++)
                {
                    if (!affectedAgent.Equipment[equipmentIndex].IsEmpty && affectedAgent.Equipment[equipmentIndex].IsShield())
                    {
                        affectedAgent.ChangeWeaponHitPoints(equipmentIndex, affectedAgent.Equipment[equipmentIndex].ModifiedMaxHitPoints);
                    }
                }
            }
            TutorialArea activeTutorialArea = this._activeTutorialArea;
            if (activeTutorialArea != null && activeTutorialArea.TypeOfTraining == TutorialArea.TrainingType.Melee)
            {
                if (affectedAgent.Controller == AgentControllerType.Player)
                {
                    if (this._trainingProgress >= 3 && this._trainingProgress <= 6 && isBlocked)
                    {
                        this._timer = base.Mission.CurrentTime;
                        if (this._trainingProgress == 3 && affectedAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendLeft)
                        {
                            this._detailedObjectives[1].SetTextVariableOfName("HIT", 1);
                            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/block_right"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                            this.CurrentObjectiveTick(new TextObject("{=7wmkPNbI}Defend from right.", null));
                            this._trainingProgress++;
                        }
                        else if (this._trainingProgress == 4 && affectedAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendRight)
                        {
                            this._detailedObjectives[1].SetTextVariableOfName("HIT", 2);
                            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/block_up"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                            this.CurrentObjectiveTick(new TextObject("{=CEqKkY3m}Defend from up.", null));
                            this._trainingProgress++;
                        }
                        else if (this._trainingProgress == 5 && affectedAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackEnd)
                        {
                            this._detailedObjectives[1].SetTextVariableOfName("HIT", 3);
                            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/block_down"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                            this.CurrentObjectiveTick(new TextObject("{=Qdz5Hely}Defend from down.", null));
                            this._trainingProgress++;
                        }
                        else if (this._trainingProgress == 6 && affectedAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.DefendDown)
                        {
                            this._detailedObjectives[1].SetTextVariableOfName("HIT", 4);
                            this._detailedObjectives[1].FinishTask();
                            this._detailedObjectives[2].SetActive(true);
                            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/attack_left"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                            this.CurrentObjectiveTick(new TextObject("{=8QX1QHAJ}Attack from left.", null));
                            this._trainingProgress++;
                        }
                    }
                }
                else if (affectedAgent == this._meleeTrainer && affectorAgent != null && affectorAgent.Controller == AgentControllerType.Player && (this._trainingProgress >= 7 && this._trainingProgress <= 10 && isBlocked))
                {
                    this._meleeTrainer.MovementFlags = Agent.MovementControlFlag.None;
                    this._timer = base.Mission.CurrentTime;
                    if (this._trainingProgress == 7 && affectorAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackLeft)
                    {
                        this._detailedObjectives[2].SetTextVariableOfName("HIT", 1);
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/attack_right"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                        this.CurrentObjectiveTick(new TextObject("{=fC60rYwy}Attack from right.", null));
                        this._trainingProgress++;
                    }
                    else if (this._trainingProgress == 8 && affectorAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackRight)
                    {
                        this._detailedObjectives[2].SetTextVariableOfName("HIT", 2);
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/attack_up"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                        this.CurrentObjectiveTick(new TextObject("{=j2dW9fZt}Attack from up.", null));
                        this._trainingProgress++;
                    }
                    else if (this._trainingProgress == 9 && affectorAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackUp)
                    {
                        this._detailedObjectives[2].SetTextVariableOfName("HIT", 3);
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/attack_down"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                        this.CurrentObjectiveTick(new TextObject("{=X9Vmjipn}Attack from down.", null));
                        this._trainingProgress++;
                    }
                    else if (this._trainingProgress == 10 && affectorAgent.GetCurrentActionDirection(1) == Agent.UsageDirection.AttackDown)
                    {
                        this._detailedObjectives[2].SetTextVariableOfName("HIT", 4);
                        this._detailedObjectives[2].FinishTask();
                        this.CurrentObjectiveTick(this._trainingFinishedText);
                        this.TickMouseObjective(TrainingFieldMissionController.MouseObjectives.None);
                        if (Agent.Main.Equipment.HasShield())
                        {
                            MBInformationManager.AddQuickInformation(new TextObject("{=PiOiQ3u5}You've successfully finished the sword and shield tutorial.", null), 0, null, null, "");
                        }
                        else
                        {
                            MBInformationManager.AddQuickInformation(new TextObject("{=GZaYmg95}You've successfully finished the sword tutorial.", null), 0, null, null, "");
                        }
                        this._trainingProgress++;
                    }
                    else
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=fBJRdxh2}Try again.", null), 0, null, null, "");
                        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/parrying/remark"), this._meleeTrainer.GetEyeGlobalPosition(), true, false, -1, -1);
                    }
                }
            }
            if (!isBlocked)
            {
                if (affectedAgent.Controller == AgentControllerType.Player)
                {
                    this._playerHealth -= (float)blow.InflictedDamage;
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/fighting/warning"), this._advancedMeleeTrainerNormal.GetEyeGlobalPosition(), true, false, -1, -1);
                    return;
                }
                if (affectedAgent == this._advancedMeleeTrainerEasy)
                {
                    this._advancedMeleeTrainerEasyHealth -= (float)blow.InflictedDamage;
                    return;
                }
                if (affectedAgent == this._advancedMeleeTrainerNormal)
                {
                    this._advancedMeleeTrainerNormalHealth -= (float)blow.InflictedDamage;
                }
            }
        }

        private void TickMouseObjective(TrainingFieldMissionController.MouseObjectives objective)
        {
            Action<TrainingFieldMissionController.MouseObjectives, TrainingFieldMissionController.ObjectivePerformingType> currentMouseObjectiveTick = this.CurrentMouseObjectiveTick;
            if (currentMouseObjectiveTick == null)
            {
                return;
            }
            currentMouseObjectiveTick(this.GetAdjustedMouseObjective(objective), this.GetObjectivePerformingType(objective));
        }

        private bool IsAttackDirection(TrainingFieldMissionController.MouseObjectives objective)
        {
            return objective - TrainingFieldMissionController.MouseObjectives.AttackLeft <= 3 || (objective - TrainingFieldMissionController.MouseObjectives.DefendLeft > 3 && false);
        }

        private TrainingFieldMissionController.MouseObjectives GetAdjustedMouseObjective(TrainingFieldMissionController.MouseObjectives baseObjective)
        {
            if (this.IsAttackDirection(baseObjective))
            {
                if (BannerlordConfig.AttackDirectionControl == 0)
                {
                    return this.GetInverseDirection(baseObjective);
                }
                return baseObjective;
            }
            else
            {
                if (BannerlordConfig.DefendDirectionControl == 0)
                {
                    return baseObjective;
                }
                return baseObjective;
            }
        }

        private TrainingFieldMissionController.ObjectivePerformingType GetObjectivePerformingType(TrainingFieldMissionController.MouseObjectives baseObjective)
        {
            if (this.IsAttackDirection(baseObjective))
            {
                int attackDirectionControl = BannerlordConfig.AttackDirectionControl;
                if (attackDirectionControl == 0)
                {
                    return TrainingFieldMissionController.ObjectivePerformingType.ByLookDirection;
                }
                if (attackDirectionControl == 1)
                {
                    return TrainingFieldMissionController.ObjectivePerformingType.ByLookDirection;
                }
                return TrainingFieldMissionController.ObjectivePerformingType.ByMovement;
            }
            else
            {
                int defendDirectionControl = BannerlordConfig.DefendDirectionControl;
                if (defendDirectionControl == 0)
                {
                    return TrainingFieldMissionController.ObjectivePerformingType.ByLookDirection;
                }
                if (defendDirectionControl == 1)
                {
                    return TrainingFieldMissionController.ObjectivePerformingType.ByMovement;
                }
                return TrainingFieldMissionController.ObjectivePerformingType.AutoBlock;
            }
        }

        private TrainingFieldMissionController.MouseObjectives GetInverseDirection(TrainingFieldMissionController.MouseObjectives objective)
        {
            switch (objective)
            {
                case TrainingFieldMissionController.MouseObjectives.None:
                    return TrainingFieldMissionController.MouseObjectives.None;
                case TrainingFieldMissionController.MouseObjectives.AttackLeft:
                    return TrainingFieldMissionController.MouseObjectives.AttackRight;
                case TrainingFieldMissionController.MouseObjectives.AttackRight:
                    return TrainingFieldMissionController.MouseObjectives.AttackLeft;
                case TrainingFieldMissionController.MouseObjectives.AttackUp:
                    return TrainingFieldMissionController.MouseObjectives.AttackDown;
                case TrainingFieldMissionController.MouseObjectives.AttackDown:
                    return TrainingFieldMissionController.MouseObjectives.AttackUp;
                case TrainingFieldMissionController.MouseObjectives.DefendLeft:
                    return TrainingFieldMissionController.MouseObjectives.DefendRight;
                case TrainingFieldMissionController.MouseObjectives.DefendRight:
                    return TrainingFieldMissionController.MouseObjectives.DefendLeft;
                case TrainingFieldMissionController.MouseObjectives.DefendUp:
                    return TrainingFieldMissionController.MouseObjectives.DefendDown;
                case TrainingFieldMissionController.MouseObjectives.DefendDown:
                    return TrainingFieldMissionController.MouseObjectives.DefendUp;
                default:
                    Debug.FailedAssert(string.Format("Inverse direction is not defined for: {0}", objective), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "GetInverseDirection", 2304);
                    return TrainingFieldMissionController.MouseObjectives.None;
            }
        }

        private void InitializeMountedTraining()
        {
            this._horse = this.SpawnHorse();
            this._horse.Controller = AgentControllerType.None;
            this._horseBeginningPosition = this._horse.GetWorldPosition();
            this._finishGateClosed = base.Mission.Scene.FindEntityWithTag("finish_gate_closed");
            this._finishGateOpen = base.Mission.Scene.FindEntityWithTag("finish_gate_open");
            this._mountedAIWaitingPosition = base.Mission.Scene.FindEntityWithTag("_mounted_ai_waiting_position").GetGlobalFrame();
            this._mountedAI = this.SpawnMountedAI();
            this._mountedAI.SetWatchState(Agent.WatchState.Alarmed);
            for (int i = 0; i < this._checkpoints.Count; i++)
            {
                this._mountedAICheckpointList.Add(this._checkpoints[i].Item1.GameEntity.GlobalPosition);
                if (i < this._checkpoints.Count - 1)
                {
                    this._mountedAICheckpointList.Add((this._checkpoints[i].Item1.GameEntity.GlobalPosition + this._checkpoints[i + 1].Item1.GameEntity.GlobalPosition) / 2f);
                }
            }
        }

        private Agent SpawnMountedAI()
        {
            this._mountedAISpawnPosition = MatrixFrame.Identity;
            GameEntity gameEntity = base.Mission.Scene.FindEntityWithTag("_mounted_ai_spawn_position");
            if (gameEntity != null)
            {
                this._mountedAISpawnPosition = gameEntity.GetGlobalFrame();
                this._mountedAISpawnPosition.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            else
            {
                Debug.FailedAssert("There are no spawn points for mounted ai.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\StoryMode\\Missions\\TrainingFieldMissionController.cs", "SpawnMountedAI", 2348);
            }
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("tutorial_npc_mounted_ai");
            AgentBuildData agentBuildData = new AgentBuildData(@object).Team(base.Mission.PlayerTeam).InitialPosition(this._mountedAISpawnPosition.origin);
            Vec2 asVec = this._mountedAISpawnPosition.rotation.f.AsVec2;
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(asVec).CivilianEquipment(false).NoHorses(false).NoWeapons(false).ClothingColor1(base.Mission.PlayerTeam.Color).ClothingColor2(base.Mission.PlayerTeam.Color2).TroopOrigin(new PartyAgentOrigin(PartyBase.MainParty, @object, -1, default(UniqueTroopDescriptor), false, false)).MountKey(MountCreationKey.GetRandomMountKeyString(@object.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, @object.GetMountKeySeed())).Controller(AgentControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.SetTeam(Mission.Current.PlayerTeam, false);
            return agent;
        }

        private void UpdateMountedAIBehavior()
        {
            if (this._mountedAICurrentCheckpointTarget == -1)
            {
                if (this._continueLoop && (this._mountedAISpawnPosition.origin - this._mountedAI.Position).LengthSquared < 6.25f)
                {
                    this._mountedAICurrentCheckpointTarget++;
                    MatrixFrame globalFrame = this._checkpoints[this._mountedAICurrentCheckpointTarget].Item1.GameEntity.GetGlobalFrame();
                    WorldPosition worldPosition = globalFrame.origin.ToWorldPosition();
                    this._mountedAI.SetScriptedPositionAndDirection(ref worldPosition, globalFrame.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
                    this.SetFinishGateStatus(false);
                    this._mountedAI.SetWatchState(Agent.WatchState.Alarmed);
                    return;
                }
            }
            else
            {
                bool flag = false;
                if ((this._checkpoints[this._mountedAICurrentCheckpointTarget].Item1.GameEntity.GetGlobalFrame().origin.ToWorldPosition().AsVec2 - this._mountedAI.Position.ToWorldPosition().AsVec2).LengthSquared < 25f)
                {
                    flag = true;
                    this._mountedAICurrentCheckpointTarget++;
                    if (this._mountedAICurrentCheckpointTarget > this._checkpoints.Count - 1)
                    {
                        this._mountedAICurrentCheckpointTarget = -1;
                        if (this._continueLoop)
                        {
                            this.GoToStartingPosition();
                        }
                        else
                        {
                            WorldPosition worldPosition2 = this._mountedAIWaitingPosition.origin.ToWorldPosition();
                            this._mountedAI.SetScriptedPositionAndDirection(ref worldPosition2, this._mountedAISpawnPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
                        }
                    }
                    else if (this._mountedAICurrentCheckpointTarget == this._checkpoints.Count - 1)
                    {
                        this.SetFinishGateStatus(true);
                        this._mountedAI.SetWatchState(Agent.WatchState.Patrolling);
                    }
                }
                else if ((this._mountedAITargets[this._mountedAICurrentHitTarget].GameEntity.GetGlobalFrame().origin.ToWorldPosition().AsVec2 - this._mountedAI.Position.ToWorldPosition().AsVec2).LengthSquared < 169f)
                {
                    this._enteredRadiusOfTarget = true;
                }
                else if ((!this._allTargetsDestroyed && this._mountedAITargets[this._mountedAICurrentHitTarget].IsDestroyed) || (this._enteredRadiusOfTarget && (this._mountedAITargets[this._mountedAICurrentHitTarget].GameEntity.GetGlobalFrame().origin.ToWorldPosition().AsVec2 - this._mountedAI.Position.ToWorldPosition().AsVec2).LengthSquared > 169f))
                {
                    this._enteredRadiusOfTarget = false;
                    flag = true;
                    this._mountedAICurrentHitTarget++;
                    if (this._mountedAICurrentHitTarget > this._mountedAITargets.Count - 1)
                    {
                        this._mountedAICurrentHitTarget = 0;
                        this._allTargetsDestroyed = true;
                    }
                }
                if (flag && this._mountedAICurrentCheckpointTarget != -1)
                {
                    MatrixFrame globalFrame2 = this._checkpoints[this._mountedAICurrentCheckpointTarget].Item1.GameEntity.GetGlobalFrame();
                    WorldPosition worldPosition3 = globalFrame2.origin.ToWorldPosition();
                    this._mountedAI.SetScriptedPositionAndDirection(ref worldPosition3, globalFrame2.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
                    if (!this._allTargetsDestroyed)
                    {
                        this._mountedAI.SetScriptedTargetEntityAndPosition(this._mountedAITargets[this._mountedAICurrentHitTarget].GameEntity, default(WorldPosition), Agent.AISpecialCombatModeFlags.None, false);
                    }
                }
            }
        }

        private void GoToStartingPosition()
        {
            WorldPosition worldPosition = this._mountedAISpawnPosition.origin.ToWorldPosition();
            this._mountedAI.SetScriptedPositionAndDirection(ref worldPosition, this._mountedAISpawnPosition.rotation.f.AsVec2.RotationInRadians, true, Agent.AIScriptedFrameFlags.None);
            this.RestoreAndShowAllMountedAITargets();
        }

        private void RestoreAndShowAllMountedAITargets()
        {
            this._allTargetsDestroyed = false;
            foreach (DestructableComponent destructableComponent in this._mountedAITargets)
            {
                destructableComponent.Reset();
                destructableComponent.GameEntity.SetVisibilityExcludeParents(true);
            }
        }

        private void HideAllMountedAITargets()
        {
            this._allTargetsDestroyed = true;
            foreach (DestructableComponent destructableComponent in this._mountedAITargets)
            {
                destructableComponent.Reset();
                destructableComponent.GameEntity.SetVisibilityExcludeParents(false);
            }
        }

        private void UpdateHorseBehavior()
        {
            if (this._horse != null && this._horse.RiderAgent == null)
            {
                if (this._horse.IsAIControlled && this._horse.CommonAIComponent.IsPanicked)
                {
                    this._horse.CommonAIComponent.StopRetreating();
                }
                if (this._horseBehaviorMode != TrainingFieldMissionController.HorseReturningSituation.BeginReturn)
                {
                    string[] array;
                    if (!this._trainingAreas.Find((TutorialArea x) => x.TypeOfTraining == TutorialArea.TrainingType.Mounted).IsPositionInsideTutorialArea(this._horse.Position, out array))
                    {
                        this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.BeginReturn;
                        TutorialArea activeTutorialArea = this._activeTutorialArea;
                        if (activeTutorialArea != null && activeTutorialArea.TypeOfTraining == TutorialArea.TrainingType.Mounted && this._trainingProgress > 1)
                        {
                            this.ResetTrainingArea();
                            goto IL_127;
                        }
                        goto IL_127;
                    }
                }
                TutorialArea activeTutorialArea2 = this._activeTutorialArea;
                if ((activeTutorialArea2 == null || activeTutorialArea2.TypeOfTraining != TutorialArea.TrainingType.Mounted) && (this._horseBehaviorMode == TrainingFieldMissionController.HorseReturningSituation.NotInPosition || this._horseBehaviorMode == TrainingFieldMissionController.HorseReturningSituation.Following))
                {
                    this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.BeginReturn;
                }
                else
                {
                    TutorialArea activeTutorialArea3 = this._activeTutorialArea;
                    if (activeTutorialArea3 != null && activeTutorialArea3.TypeOfTraining == TutorialArea.TrainingType.Mounted && !Agent.Main.HasMount && this._trainingProgress > 2)
                    {
                        this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.Following;
                    }
                }
            IL_127:
                switch (this._horseBehaviorMode)
                {
                    case TrainingFieldMissionController.HorseReturningSituation.BeginReturn:
                        if ((this._horse.Position - this._horseBeginningPosition.GetGroundVec3()).Length > 1f)
                        {
                            this._horse.Controller = AgentControllerType.AI;
                            this._horse.SetScriptedPosition(ref this._horseBeginningPosition, false, Agent.AIScriptedFrameFlags.None);
                            this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.Returning;
                            return;
                        }
                        this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.ReturnCompleted;
                        return;
                    case TrainingFieldMissionController.HorseReturningSituation.Returning:
                        if ((this._horse.Position - this._horseBeginningPosition.GetGroundVec3()).Length < 0.5f)
                        {
                            if (this._horse.GetCurrentVelocity().LengthSquared <= 0f)
                            {
                                this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.ReturnCompleted;
                                return;
                            }
                            if (this._horse.Controller == AgentControllerType.AI)
                            {
                                this._horse.Controller = AgentControllerType.None;
                                this._horse.MovementFlags &= ~(Agent.MovementControlFlag.Forward | Agent.MovementControlFlag.Backward | Agent.MovementControlFlag.StrafeRight | Agent.MovementControlFlag.StrafeLeft | Agent.MovementControlFlag.TurnRight | Agent.MovementControlFlag.TurnLeft);
                                this._horse.MovementInputVector = Vec2.Zero;
                                return;
                            }
                        }
                        else if (this._horse.Controller == AgentControllerType.None)
                        {
                            this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.BeginReturn;
                            return;
                        }
                        break;
                    case TrainingFieldMissionController.HorseReturningSituation.ReturnCompleted:
                        if ((this._horse.Position - this._horseBeginningPosition.GetGroundVec3()).Length > 1f)
                        {
                            TutorialArea activeTutorialArea4 = this._activeTutorialArea;
                            if (activeTutorialArea4 != null && activeTutorialArea4.TypeOfTraining == TutorialArea.TrainingType.Mounted)
                            {
                                this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.NotInPosition;
                                this._horse.Controller = AgentControllerType.None;
                                this._horse.MovementFlags &= ~(Agent.MovementControlFlag.Forward | Agent.MovementControlFlag.Backward | Agent.MovementControlFlag.StrafeRight | Agent.MovementControlFlag.StrafeLeft | Agent.MovementControlFlag.TurnRight | Agent.MovementControlFlag.TurnLeft);
                                this._horse.MovementInputVector = Vec2.Zero;
                                return;
                            }
                        }
                        break;
                    case TrainingFieldMissionController.HorseReturningSituation.Following:
                        if ((this._horse.Position - Agent.Main.Position).Length > 3f)
                        {
                            this._horse.Controller = AgentControllerType.AI;
                            Vec3 position = Agent.Main.Position + (this._horse.Position - Agent.Main.Position).NormalizedCopy() * 3f;
                            WorldPosition worldPosition = new WorldPosition(Agent.Main.Mission.Scene, position);
                            this._horse.SetScriptedPosition(ref worldPosition, false, Agent.AIScriptedFrameFlags.None);
                            return;
                        }
                        break;
                    default:
                        return;
                }
            }
            else if (this._horse.RiderAgent != null && this._horseBehaviorMode != TrainingFieldMissionController.HorseReturningSituation.NotInPosition)
            {
                this._horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.NotInPosition;
                this._horse.Controller = AgentControllerType.None;
                this._horse.MovementFlags &= ~(Agent.MovementControlFlag.Forward | Agent.MovementControlFlag.Backward | Agent.MovementControlFlag.StrafeRight | Agent.MovementControlFlag.StrafeLeft | Agent.MovementControlFlag.TurnRight | Agent.MovementControlFlag.TurnLeft);
                this._horse.MovementInputVector = Vec2.Zero;
            }
        }

        private Agent SpawnHorse()
        {
            MatrixFrame globalFrame = base.Mission.Scene.FindEntityWithTag("spawner_horse").GetGlobalFrame();
            ItemObject @object = MBObjectManager.Instance.GetObject<ItemObject>("old_horse");
            ItemRosterElement itemRosterElement = new ItemRosterElement(@object, 1, null);
            ItemObject object2 = MBObjectManager.Instance.GetObject<ItemObject>("light_harness");
            ItemRosterElement itemRosterElement2 = new ItemRosterElement(object2, 0, null);
            Agent agent = null;
            if (@object.HasHorseComponent)
            {
                Mission mission = Mission.Current;
                ItemRosterElement rosterElement = itemRosterElement;
                ItemRosterElement harnessRosterElement = itemRosterElement2;
                Vec2 vec = globalFrame.rotation.f.AsVec2;
                vec = vec.Normalized();
                agent = mission.SpawnMonster(rosterElement, harnessRosterElement, globalFrame.origin, vec, -1);
                AnimalSpawnSettings.CheckAndSetAnimalAgentFlags(base.Mission.Scene.FindEntityWithTag("spawner_melee_npc"), agent);
            }
            return agent;
        }

        private void MountedTrainingUpdate()
        {
            bool flag = false;
            if (this._trainingProgress > 2 && this._trainingProgress < 5)
            {
                flag = this.CheckpointUpdate();
            }
            if (Agent.Main.HasMount)
            {
                this._activeTutorialArea.ActivateBoundaries();
            }
            else
            {
                this._activeTutorialArea.HideBoundaries();
            }
            if (this._trainingProgress == 1)
            {
                if (this.HasAllWeaponsPicked())
                {
                    this._detailedObjectives = this._mountedObjectives.ConvertAll<TrainingFieldMissionController.TutorialObjective>((TrainingFieldMissionController.TutorialObjective x) => new TrainingFieldMissionController.TutorialObjective(x.Id, x.IsFinished, x.IsActive, x.HasBackground));
                    this._detailedObjectives[1].SetTextVariableOfName("HIT", this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex));
                    this._detailedObjectives[1].SetTextVariableOfName("ALL", this._activeTutorialArea.GetBreakablesCount(this._trainingSubTypeIndex));
                    this._detailedObjectives[0].SetActive(true);
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/riding/pick_" + this._trainingSubTypeIndex), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                    this.SetHorseMountable(true);
                    this._mountedLastBrokenTargetCount = 0;
                    this._trainingProgress++;
                    this.CurrentObjectiveTick(new TextObject("{=h31YaM4b}Mount the horse.", null));
                    return;
                }
            }
            else if (this._trainingProgress == 2)
            {
                if (Agent.Main.HasMount)
                {
                    this._detailedObjectives[0].FinishTask();
                    this._detailedObjectives[1].SetActive(true);
                    this._activeTutorialArea.ActivateBoundaries();
                    this._trainingProgress++;
                    this.CurrentObjectiveTick(new TextObject("{=gJBNUAJd}Finish the track and hit as many targets as you can.", null));
                    return;
                }
            }
            else if (this._trainingProgress == 3)
            {
                if (this._checkpoints[0].Item2)
                {
                    this._activeTutorialArea.MakeDestructible(this._trainingSubTypeIndex);
                    this._activeTutorialArea.ResetBreakables(this._trainingSubTypeIndex, false);
                    this.ResetCheckpoints();
                    ValueTuple<VolumeBox, bool> value = this._checkpoints[0];
                    value.Item2 = true;
                    this._checkpoints[0] = value;
                    this.StartTimer();
                    this.UIStartTimer();
                    MBInformationManager.AddQuickInformation(new TextObject("{=HvGW2DvS}Track started.", null), 0, null, null, "");
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/riding/start_course"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                    this._trainingProgress++;
                    return;
                }
                if (!Agent.Main.HasMount)
                {
                    this._trainingProgress = 1;
                    return;
                }
            }
            else if (this._trainingProgress == 4)
            {
                int brokenBreakableCount = this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex);
                this._detailedObjectives[1].SetTextVariableOfName("HIT", brokenBreakableCount);
                if (brokenBreakableCount != this._mountedLastBrokenTargetCount)
                {
                    Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/hit_target"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                    this._mountedLastBrokenTargetCount = brokenBreakableCount;
                }
                if (flag)
                {
                    this._detailedObjectives[1].FinishTask();
                    this._trainingProgress++;
                    this.MountedTrainingEndedSuccessfully();
                    return;
                }
            }
            else if (this._trainingProgress == 5 && !Agent.Main.HasMount)
            {
                this._trainingProgress++;
                this.SetHorseMountable(false);
                this.CurrentObjectiveTick(this._trainingFinishedText);
            }
        }

        private void ResetCheckpoints()
        {
            for (int i = 0; i < this._checkpoints.Count; i++)
            {
                this._checkpoints[i] = ValueTuple.Create<VolumeBox, bool>(this._checkpoints[i].Item1, false);
            }
            this._currentCheckpointIndex = -1;
        }

        private bool CheckpointUpdate()
        {
            for (int i = 0; i < this._checkpoints.Count; i++)
            {
                if (this._checkpoints[i].Item1.IsPointIn(Agent.Main.Position))
                {
                    if (this._currentCheckpointIndex == -1)
                    {
                        this._enteringDotProduct = Vec3.DotProduct(Agent.Main.Velocity, this._checkpoints[i].Item1.GameEntity.GetFrame().rotation.f);
                        this._currentCheckpointIndex = i;
                    }
                    return false;
                }
            }
            bool result = false;
            if (this._currentCheckpointIndex != -1)
            {
                float num = Vec3.DotProduct(this._checkpoints[this._currentCheckpointIndex].Item1.GameEntity.GetFrame().rotation.f, Agent.Main.Velocity);
                if (num > 0f == this._enteringDotProduct > 0f)
                {
                    if ((this._currentCheckpointIndex == 0 || this._checkpoints[this._currentCheckpointIndex - 1].Item2) && num > 0f)
                    {
                        this._checkpoints[this._currentCheckpointIndex] = ValueTuple.Create<VolumeBox, bool>(this._checkpoints[this._currentCheckpointIndex].Item1, true);
                        int num2 = 0;
                        for (int j = 0; j < this._checkpoints.Count; j++)
                        {
                            if (this._checkpoints[j].Item2)
                            {
                                num2++;
                            }
                        }
                        if (this._currentCheckpointIndex == this._checkpoints.Count - 1)
                        {
                            result = true;
                        }
                        if (this._currentCheckpointIndex == this._checkpoints.Count - 2)
                        {
                            this.SetFinishGateStatus(true);
                        }
                    }
                    else if (num < 0f)
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=kvTEeUWO}Wrong way!", null), 0, null, null, "");
                    }
                }
            }
            this._currentCheckpointIndex = -1;
            return result;
        }

        private void SetHorseMountable(bool mountable)
        {
            if (mountable)
            {
                Agent.Main.SetAgentFlags(Agent.Main.GetAgentFlags() | AgentFlag.CanRide);
                return;
            }
            Agent.Main.SetAgentFlags(Agent.Main.GetAgentFlags() & ~AgentFlag.CanRide);
        }

        private void OnMountedTrainingStart()
        {
            this.ResetCheckpoints();
            this._continueLoop = false;
            this.HideAllMountedAITargets();
        }

        private void OnMountedTrainingExit()
        {
            this.SetHorseMountable(false);
            this.ResetCheckpoints();
            this._continueLoop = true;
            this.GoToStartingPosition();
        }

        private void SetFinishGateStatus(bool open)
        {
            if (open)
            {
                this._finishGateStatus++;
                if (this._finishGateStatus == 1)
                {
                    this._finishGateClosed.SetVisibilityExcludeParents(false);
                    this._finishGateOpen.SetVisibilityExcludeParents(true);
                    return;
                }
            }
            else
            {
                this._finishGateStatus = MathF.Max(0, this._finishGateStatus - 1);
                if (this._finishGateStatus == 0)
                {
                    this._finishGateClosed.SetVisibilityExcludeParents(true);
                    this._finishGateOpen.SetVisibilityExcludeParents(false);
                }
            }
        }

        private void MountedTrainingEndedSuccessfully()
        {
            this.UIEndTimer();
            this.EndTimer();
            int brokenBreakableCount = this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex);
            int breakablesCount = this._activeTutorialArea.GetBreakablesCount(this._trainingSubTypeIndex);
            float num = this._timeScore + (float)(this._activeTutorialArea.GetBreakablesCount(this._trainingSubTypeIndex) - this._activeTutorialArea.GetBrokenBreakableCount(this._trainingSubTypeIndex));
            TextObject textObject = new TextObject("{=W49eUmpT}You can dismount from horse with {CROUCH_KEY}, or {ACTION_KEY} while looking at the horse.", null);
            textObject.SetTextVariable("CROUCH_KEY", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("CombatHotKeyCategory", 15), 1f));
            textObject.SetTextVariable("ACTION_KEY", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("CombatHotKeyCategory", 13), 1f));
            this.CurrentObjectiveTick(textObject);
            if (breakablesCount - brokenBreakableCount == 0)
            {
                Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/riding/course_perfect"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                TextObject textObject2 = new TextObject("{=veHe94Ec}You've successfully finished the track in ({TIME_SCORE}) seconds without missing any targets!", null);
                textObject2.SetTextVariable("TIME_SCORE", new TextObject(num.ToString("0.0"), null));
                MBInformationManager.AddQuickInformation(textObject2, 0, null, null, "");
            }
            else
            {
                Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/tutorial/vo/riding/course_finish"), Agent.Main.GetEyeGlobalPosition(), true, false, -1, -1);
                TextObject textObject3 = new TextObject("{=QLgkR3qN}You've successfully finished the track in ({TIME_SCORE}) seconds. You've received ({PENALTY_SECONDS}) seconds penalty from ({MISSED_TARGETS}) missed targets.", null);
                textObject3.SetTextVariable("TIME_SCORE", new TextObject(num.ToString("0.0"), null));
                textObject3.SetTextVariable("PENALTY_SECONDS", new TextObject((num - this._timeScore).ToString("0.0"), null));
                textObject3.SetTextVariable("MISSED_TARGETS", breakablesCount - brokenBreakableCount);
                MBInformationManager.AddQuickInformation(textObject3, 0, null, null, "");
            }
            this.SetFinishGateStatus(false);
            this.SuccessfullyFinishTraining(num);
        }

        private const string SoundBasicMeleeGreet = "event:/mission/tutorial/vo/parrying/greet";
        private const string SoundBasicMeleeBlockLeft = "event:/mission/tutorial/vo/parrying/block_left";
        private const string SoundBasicMeleeBlockRight = "event:/mission/tutorial/vo/parrying/block_right";
        private const string SoundBasicMeleeBlockUp = "event:/mission/tutorial/vo/parrying/block_up";
        private const string SoundBasicMeleeBlockDown = "event:/mission/tutorial/vo/parrying/block_down";
        private const string SoundBasicMeleeAttackLeft = "event:/mission/tutorial/vo/parrying/attack_left";
        private const string SoundBasicMeleeAttackRight = "event:/mission/tutorial/vo/parrying/attack_right";
        private const string SoundBasicMeleeAttackUp = "event:/mission/tutorial/vo/parrying/attack_up";
        private const string SoundBasicMeleeAttackDown = "event:/mission/tutorial/vo/parrying/attack_down";
        private const string SoundBasicMeleeRemark = "event:/mission/tutorial/vo/parrying/remark";
        private const string SoundBasicMeleePraise = "event:/mission/tutorial/vo/parrying/praise";
        private const string SoundAdvancedMeleeGreet = "event:/mission/tutorial/vo/fighting/greet";
        private const string SoundAdvancedMeleeWarning = "event:/mission/tutorial/vo/fighting/warning";
        private const string SoundAdvancedMeleePlayerLose = "event:/mission/tutorial/vo/fighting/player_lose";
        private const string SoundAdvancedMeleePlayerWin = "event:/mission/tutorial/vo/fighting/player_win";
        private const string SoundRangedPickPrefix = "event:/mission/tutorial/vo/archery/pick_";
        private const string SoundRangedStartTraining = "event:/mission/tutorial/vo/archery/start_training";
        private const string SoundRangedHitTarget = "event:/mission/tutorial/vo/archery/hit_target";
        private const string SoundRangedMissTarget = "event:/mission/tutorial/vo/archery/miss_target";
        private const string SoundRangedFinish = "event:/mission/tutorial/vo/archery/finish";
        private const string SoundMountedPickPrefix = "event:/mission/tutorial/vo/riding/pick_";
        private const string SoundMountedMountHorse = "event:/mission/tutorial/vo/riding/mount_horse";
        private const string SoundMountedStartCourse = "event:/mission/tutorial/vo/riding/start_course";
        private const string SoundMountedCourseFinish = "event:/mission/tutorial/vo/riding/course_finish";
        private const string SoundMountedCoursePerfect = "event:/mission/tutorial/vo/riding/course_perfect";
        private const string FinishCourseSound = "event:/mission/tutorial/finish_course";
        private const string FinishTaskSound = "event:/mission/tutorial/finish_task";
        private const string HitTargetSound = "event:/mission/tutorial/hit_target";
        private TextObject _trainingFinishedText = new TextObject("{=cRvSuYC8}Choose another weapon or go to another training area.", null);
        private List<TrainingFieldMissionController.DelayedAction> _delayedActions = new List<TrainingFieldMissionController.DelayedAction>();
        private MissionConversationLogic _missionConversationHandler;
        private const string RangedNpcCharacter = "tutorial_npc_ranged";
        private const string BowTrainingShootingPositionTag = "bow_training_shooting_position";
        private const string SpawnerRangedNpcTag = "spawner_ranged_npc_tag";
        private const string RangedNpcTargetTag = "_ranged_npc_target";
        private const float ShootingPositionActivationDistance = 2f;
        private const string BasicMeleeNpcSpawnPointTag = "spawner_melee_npc";
        private const string BasicMeleeNpcCharacter = "tutorial_npc_basic_melee";
        private const string AdvancedMeleeNpcSpawnPointTagEasy = "spawner_adv_melee_npc_easy";
        private const string AdvancedMeleeNpcSpawnPointTagNormal = "spawner_adv_melee_npc_normal";
        private const string AdvancedMeleeNpcEasySecondPositionTag = "adv_melee_npc_easy_second_pos";
        private const string AdvancedMeleeNpcNormalSecondPositionTag = "adv_melee_npc_normal_second_pos";
        private const string AdvancedMeleeEasyNpcCharacter = "tutorial_npc_advanced_melee_easy";
        private const string AdvancedMeleeNormalNpcCharacter = "tutorial_npc_advanced_melee_normal";
        private const string AdvancedMeleeBattleAreaTag = "battle_area";
        private const string MountedAISpawnPositionTag = "_mounted_ai_spawn_position";
        private const string MountedAICharacter = "tutorial_npc_mounted_ai";
        private const string MountedAITargetTag = "_mounted_ai_target";
        private const string MountedAIWaitingPositionTag = "_mounted_ai_waiting_position";
        private const string CheckpointTag = "mounted_checkpoint";
        private const string HorseSpawnPositionTag = "spawner_horse";
        private const string FinishGateClosedTag = "finish_gate_closed";
        private const string FinishGateOpenTag = "finish_gate_open";
        private const string NameOfTheHorse = "old_horse";
        private readonly List<TutorialArea> _trainingAreas = new List<TutorialArea>();
        private TutorialArea _activeTutorialArea;
        private bool _courseFinished;
        private int _trainingProgress;
        private int _trainingSubTypeIndex = -1;
        private string _activeTrainingSubTypeTag = "";
        private float _beginningTime;
        private float _timeScore;
        private bool _showTutorialObjectivesAnyway;
        private Dictionary<string, float> _tutorialScores;
        private GameEntity _shootingPosition;
        private Agent _bowNpc;
        private WorldPosition _rangedNpcSpawnPosition;
        private WorldPosition _rangedTargetPosition;
        private Vec3 _rangedTargetRotation;
        private GameEntity _rangedNpcSpawnPoint;
        private int _rangedLastBrokenTargetCount;
        private List<DestructableComponent> _targetsForRangedNpc = new List<DestructableComponent>();
        private DestructableComponent _lastTargetGiven;
        private bool _atShootingPosition;
        private bool _targetPositionSet;
        private List<TrainingFieldMissionController.TutorialObjective> _rangedObjectives = new List<TrainingFieldMissionController.TutorialObjective>
        {
            new TrainingFieldMissionController.TutorialObjective("ranged_go_to_shooting_position", false, false, false),
            new TrainingFieldMissionController.TutorialObjective("ranged_shoot_targets", false, false, false)
        };
        private TextObject _remainingTargetText = new TextObject("{=gBbm9beO}Hit all of the targets. {REMAINING_TARGET} {?REMAINING_TARGET>1}targets{?}target{\\?} left.", null);
        private Agent _meleeTrainer;
        private WorldPosition _meleeTrainerDefaultPosition;
        private float _timer;
        private List<TrainingFieldMissionController.TutorialObjective> _meleeObjectives = new List<TrainingFieldMissionController.TutorialObjective>
        {
            new TrainingFieldMissionController.TutorialObjective("melee_go_to_trainer", false, false, false),
            new TrainingFieldMissionController.TutorialObjective("melee_defense", false, false, true),
            new TrainingFieldMissionController.TutorialObjective("melee_attack", false, false, true)
        };
        private Agent _advancedMeleeTrainerEasy;
        private Agent _advancedMeleeTrainerNormal;
        private float _playerCampaignHealth;
        private float _playerHealth = 100f;
        private float _advancedMeleeTrainerEasyHealth = 100f;
        private float _advancedMeleeTrainerNormalHealth = 100f;
        private MatrixFrame _advancedMeleeTrainerEasyInitialPosition;
        private MatrixFrame _advancedMeleeTrainerEasySecondPosition;
        private MatrixFrame _advancedMeleeTrainerNormalInitialPosition;
        private MatrixFrame _advancedMeleeTrainerNormalSecondPosition;
        private readonly TextObject _fightStartsIn = new TextObject("{=TNxWBS07}Fight will start in {REMAINING_TIME} {?REMAINING_TIME>1}seconds{?}second{\\?}...", null);
        private readonly List<TrainingFieldMissionController.TutorialObjective> _advMeleeObjectives = new List<TrainingFieldMissionController.TutorialObjective>
        {
            new TrainingFieldMissionController.TutorialObjective("adv_melee_go_to_trainer", false, false, false),
            new TrainingFieldMissionController.TutorialObjective("adv_melee_beat_easy_trainer", false, false, false),
            new TrainingFieldMissionController.TutorialObjective("adv_melee_beat_normal_trainer", false, false, false)
        };
        private bool _playerLeftBattleArea;
        private GameEntity _finishGateClosed;
        private GameEntity _finishGateOpen;
        private int _finishGateStatus;
        private List<ValueTuple<VolumeBox, bool>> _checkpoints = new List<ValueTuple<VolumeBox, bool>>();
        private int _currentCheckpointIndex = -1;
        private int _mountedLastBrokenTargetCount;
        private float _enteringDotProduct;
        private Agent _horse;
        private WorldPosition _horseBeginningPosition;
        private TrainingFieldMissionController.HorseReturningSituation _horseBehaviorMode = TrainingFieldMissionController.HorseReturningSituation.ReturnCompleted;
        private List<TrainingFieldMissionController.TutorialObjective> _mountedObjectives = new List<TrainingFieldMissionController.TutorialObjective>
        {
            new TrainingFieldMissionController.TutorialObjective("mounted_mount_the_horse", false, false, false),
            new TrainingFieldMissionController.TutorialObjective("mounted_hit_targets", false, false, false)
        };

        private Agent _mountedAI;
        private MatrixFrame _mountedAISpawnPosition;
        private MatrixFrame _mountedAIWaitingPosition;
        private int _mountedAICurrentCheckpointTarget = -1;
        private int _mountedAICurrentHitTarget;
        private bool _enteredRadiusOfTarget;
        private bool _allTargetsDestroyed;
        private List<DestructableComponent> _mountedAITargets = new List<DestructableComponent>();
        private bool _continueLoop = true;
        private List<Vec3> _mountedAICheckpointList = new List<Vec3>();
        private List<TrainingFieldMissionController.TutorialObjective> _detailedObjectives = new List<TrainingFieldMissionController.TutorialObjective>();
        private readonly List<TrainingFieldMissionController.TutorialObjective> _tutorialObjectives = new List<TrainingFieldMissionController.TutorialObjective>();
        public Action UIStartTimer;
        public Func<float> UIEndTimer;
        public Action<string> TimerTick;
        public Action<TextObject> CurrentObjectiveTick;
        public Action<TrainingFieldMissionController.MouseObjectives, TrainingFieldMissionController.ObjectivePerformingType> CurrentMouseObjectiveTick;
        public Action<List<TrainingFieldMissionController.TutorialObjective>> AllObjectivesTick;
        private static bool _updateObjectivesWillBeCalled;
        private Agent _brotherConversationAgent;
        public class TutorialObjective
        {
            public string Id { get; private set; }
            public bool IsFinished { get; private set; }
            public bool HasBackground { get; private set; }
            public bool IsActive { get; private set; }
            public List<TrainingFieldMissionController.TutorialObjective> SubTasks { get; private set; }
            public float Score { get; private set; }

            public TutorialObjective(string id, bool isFinished = false, bool isActive = false, bool hasBackground = false)
            {
                this._name = GameTexts.FindText("str_tutorial_" + id, null);
                this.Id = id;
                this.IsFinished = isFinished;
                this.IsActive = isActive;
                this.SubTasks = new List<TrainingFieldMissionController.TutorialObjective>();
                this.Score = 0f;
                this.HasBackground = hasBackground;
            }

            public void SetTextVariableOfName(string tag, int variable)
            {
                string a = this._name.ToString();
                this._name.SetTextVariable(tag, variable);
                if (a != this._name.ToString())
                {
                    TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
                }
            }

            public string GetNameString()
            {
                if (this._name == null)
                {
                    return "";
                }
                return this._name.ToString();
            }

            public bool SetActive(bool isActive)
            {
                if (this.IsActive == isActive)
                {
                    return false;
                }
                this.IsActive = isActive;
                TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
                return true;
            }

            public bool FinishTask()
            {
                if (this.IsFinished)
                {
                    return false;
                }
                this.IsFinished = true;
                TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
                return true;
            }

            public void FinishSubTask(string subTaskName, float score)
            {
                TrainingFieldMissionController.TutorialObjective tutorialObjective = this.SubTasks.Find((TrainingFieldMissionController.TutorialObjective x) => x.Id == subTaskName);
                tutorialObjective.FinishTask();
                if (score != 0f && (tutorialObjective.Score > score || tutorialObjective.Score == 0f))
                {
                    tutorialObjective.Score = score;
                }
                if (!this.SubTasks.Exists((TrainingFieldMissionController.TutorialObjective x) => !x.IsFinished))
                {
                    this.FinishTask();
                }
                TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
            }

            public bool SetAllSubTasksInactive()
            {
                bool flag = false;
                foreach (TrainingFieldMissionController.TutorialObjective tutorialObjective in this.SubTasks)
                {
                    bool flag2 = tutorialObjective.SetActive(false);
                    flag = (flag || flag2);
                    if (tutorialObjective.SubTasks.Count > 0)
                    {
                        bool flag3 = tutorialObjective.SetAllSubTasksInactive();
                        flag = (flag || flag3);
                    }
                }
                if (flag)
                {
                    TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
                }
                return flag;
            }

            public void AddSubTask(TrainingFieldMissionController.TutorialObjective newSubTask)
            {
                this.SubTasks.Add(newSubTask);
                TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
            }

            public void RestoreScoreFromSave(float score)
            {
                this.Score = score;
                TrainingFieldMissionController._updateObjectivesWillBeCalled = true;
            }

            private TextObject _name;
        }

        public struct DelayedAction
        {
            public DelayedAction(Action order, float delayTime, string explanation)
            {
                this._orderGivenTime = Mission.Current.CurrentTime;
                this._delayTime = delayTime;
                this._order = order;
                this._explanation = explanation;
            }

            public bool Update()
            {
                if (Mission.Current.CurrentTime - this._orderGivenTime > this._delayTime)
                {
                    this._order();
                    return true;
                }
                return false;
            }

            private float _orderGivenTime;
            private float _delayTime;
            private Action _order;
            private string _explanation;
        }

        // Token: 0x02000082 RID: 130
        public enum MouseObjectives
        {
            None,
            AttackLeft,
            AttackRight,
            AttackUp,
            AttackDown,
            DefendLeft,
            DefendRight,
            DefendUp,
            DefendDown
        }

        // Token: 0x02000083 RID: 131
        public enum ObjectivePerformingType
        {
            None,
            ByLookDirection,
            ByMovement,
            AutoBlock
        }

        // Token: 0x02000084 RID: 132
        private enum HorseReturningSituation
        {
            NotInPosition,
            BeginReturn,
            Returning,
            ReturnCompleted,
            Following
        }
    }
}
