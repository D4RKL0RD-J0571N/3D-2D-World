using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GKC_PoolingElementAI : GKC_PoolingElement
{
	[Space]
	[Header ("Custom Settings")]
	[Space]

	public bool setRandomPlayerModeOnSpawnEnabled;

	public bool setMainPlayerAsTargetOnSpawnEnabled;

	[Space]
	[Header ("Components")]
	[Space]

	public playerController mainPlayerController;
	public health mainHealth;
	public ragdollActivator mainRagdollActivator;
	public findObjectivesSystem mainFindObjectivesSystem;
	public AINavMesh mainAINavMesh;
	public gravitySystem mainGravitySystem;
	public playerStatesManager mainPlayerStatesManager;


	public override void checkObjectStateAfterSpawn ()
	{
		bool characterWasDead = mainHealth.isDead ();

		if (characterWasDead) {
			mainPlayerController.resetAnimator ();

			mainRagdollActivator.setActivateQuickGetUpInsteadOfRegularGetUpState (true);

			mainRagdollActivator.setForceQuickGetUpOnCharacterState (true);

			mainRagdollActivator.disabledCheckGetUpPaused ();

			mainRagdollActivator.setCharacterBodyParentOut ();

			mainHealth.resurrectFromExternalCall ();

			mainRagdollActivator.setForceQuickGetUpOnCharacterState (false);
		} 

		if (!mainHealth.isDead ()) {
			mainFindObjectivesSystem.clearFullEnemiesList ();

			mainFindObjectivesSystem.removeCharacterAsTargetOnSameFaction ();

			mainFindObjectivesSystem.resetAITargets ();

			mainFindObjectivesSystem.removeTargetInfo ();

			if (characterWasDead) {
				mainHealth.setHealthAmountOnMaxValue ();
			}

			if (characterWasDead) {
				mainPlayerController.setCharacterMeshGameObjectState (true);

				mainRagdollActivator.setCharacterBodyActiveState (true);
			}

			mainAINavMesh.pauseAI (false);

			if (setRandomPlayerModeOnSpawnEnabled) {
				if (mainPlayerStatesManager.isUseRandomPlayerModeOnStartActive ()) {
					mainPlayerStatesManager.checkUseRandomPlayerModeOnStart ();

					if (characterWasDead) {
						mainPlayerStatesManager.setDefaultPlayersModeAsCurrentOne ();
					}
				}
			}

			if (characterWasDead) {
				mainFindObjectivesSystem.resetAIToOriginalPosition ();
			}

			mainFindObjectivesSystem.checkAIBehaviorStateOnCharacterSpawn ();

			mainAINavMesh.resetInitialTargetCheckedOnCharacterSpawn ();

			if (setMainPlayerAsTargetOnSpawnEnabled) {
				Transform newTarget = GKC_Utils.findMainPlayerTransformOnScene ();

				if (newTarget != null) {
					mainAINavMesh.follow (newTarget);
				}
			}

			mainPlayerController.getRigidbody ().isKinematic = false;
		}
	}

	public override void checkObjectStateBeforeDespawn ()
	{
		mainAINavMesh.pauseAI (true);

		mainRagdollActivator.setModelAndSkeletonParentInsideCharacter ();

		mainGravitySystem.setNewParent (mainPoolGameObject.transform);

		mainFindObjectivesSystem.checkAIBehaviorStateOnCharacterDespawn ();

		mainFindObjectivesSystem.resetAIToOriginalPosition ();

		mainPlayerController.getRigidbody ().isKinematic = true;
	}
}
