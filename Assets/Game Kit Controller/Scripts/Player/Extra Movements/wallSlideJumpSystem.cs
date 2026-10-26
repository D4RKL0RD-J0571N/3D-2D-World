using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class wallSlideJumpSystem : externalControllerBehavior
{
	[Header ("Main Settings")]
	[Space]

	public bool slideEnabled = true;

	public LayerMask raycastLayermask;

	public float raycastDistance = 0.6f;

	public float slideActiveRaycastDistance = 3;

	public float adhereToWallSpeed = 5;

	public float jumpRotationSpeedThirdPerson = 1;
	public float jumpRotationSpeedFirstPerson = 0.5f;

	public float minAngleToAdhereToWall = 20;
	public float downRaycastDistanceToCheckToAdhereToWall = 3;

	public float minWaitTimeToAdhereAgain = 0.3f;

	[Space]
	[Header ("Slide Down Settings")]
	[Space]

	public bool slideDownOnWall;
	public float slideDownOnWallSpeed;

	[Space]

	public float waitTimeToStartToSlideDown;
	public bool stopSlideDownAfterTime;
	public float timeToStopSlideDown;

	[Space]

	public bool disableSlideOnWallAfterTime;
	public float timeToDisableSlideDown;

	[Space]

	public bool slideDownIfInputNotPressed;

	public bool disableSlideIfInputPressedDown;

	public bool disableSlideIfInputPressedDown2_5d;

	public bool useInputPressUpToAdhereToSurface;

	public bool slideDownIfInputPressed;

	public bool slideDownIfInputPressed2_5d = true;

	public bool useDoubleTapDownToStopSlide;

	[Space]
	[Header ("Speed Settings")]
	[Space]

	public float slideRotationSpeedThirdPerson = 200;
	public float slideRotationSpeedFirstPerson = 100;

	public Vector3 impulseOnJump;

	public Vector3 impulseOnJump2_5d = new Vector3 (0, 15, -30);

	public float maxVelocityChangeSlide;

	[Space]
	[Header ("Other Settings")]
	[Space]

	public bool sliderCanBeUsed = true;

	public bool keepWeapons;
	public bool drawWeaponsIfCarriedPreviously;

	[Space]
	[Header ("Third Person Settings")]
	[Space]

	public int actionID = 08632946;

	public string externalControlleBehaviorActiveAnimatorName = "External Behavior Active";
	public string actionIDAnimatorName = "Action ID";

	public string horizontalAnimatorName = "Horizontal Action";

	public float inputLerpSpeed = 3;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool showGizmo;

	public bool showDebugPrint;

	public bool checkIfDetectSlideActive;

	public bool slideActive;

	public bool isFirstPersonActive;

	public float currentVerticalMovement;

	public float currentHorizontalMovement;

	public float slideDownSpeedMultiplier = 1;

	public bool forceSlowDownOnSurfaceActive;

	public bool carryingWeaponsPreviously;

	public bool slideDownInProcess;

	public bool wallJumpSlidePaused;

	public Vector3 moveInput;

	public Vector3 localMove;

	public bool useDoubleTapDownToStopSlideActive;

	[Space]
	[Header ("First Person Events Settings")]
	[Space]

	public bool useEventsOnFirstPerson;

	public UnityEvent eventOnStartFirstPerson;
	public UnityEvent eventOnEndFirstPerson;

	[Space]
	[Header ("Third Person Events Settings")]
	[Space]

	public bool useEventsOnThirdPerson;

	public UnityEvent eventOnStartThirdPerson;
	public UnityEvent eventOnEndThirdPerson;

	[Space]
	[Header ("Components")]
	[Space]

	public playerController mainPlayerController;
	public Transform playerTransform;
	public Rigidbody mainRigidbody;
	public Transform playerCameraTransform;
	public playerCamera mainPlayerCamera;
	public playerWeaponsManager mainPlayerWeaponsManager;

	public Animator mainAnimator;

	public headTrack mainHeadTrack;

	bool originalSlideEnabled;

	Vector3 playerTransformUp;
	Vector3 playerTransformForward;

	RaycastHit hit;

	Vector3 velocityChange;

	int externalControlleBehaviorActiveAnimatorID;

	int actionIDAnimatorID;

	float targetRotation;

	float lastTimeSlideActive;

	float lastTimeInputPressed;

	bool resetAnimatorIDValue;

	int horizontalAnimatorID;

	bool jumpInputUsed;

	Vector3 adherePosition;

	Coroutine jumpCoroutine;

	float currentSlideRotationSpeed;

	bool ignoreHorizontalCameraRotationInputState;

	Transform currentLockedCameraTransform;

	float lastTimePressedDownFirstTime;
	float lastTimePressedDownSecondTime;
	bool pressedDownReleasedFirstTime;


	void Start ()
	{
		originalSlideEnabled = slideEnabled;

		externalControlleBehaviorActiveAnimatorID = Animator.StringToHash (externalControlleBehaviorActiveAnimatorName);
		actionIDAnimatorID = Animator.StringToHash (actionIDAnimatorName);

		horizontalAnimatorID = Animator.StringToHash (horizontalAnimatorName);
	}

	void updateInputValues ()
	{
		Vector2 rawAxisValues = mainPlayerController.getRawAxisValues ();

		currentVerticalMovement = rawAxisValues.y;
		currentHorizontalMovement = rawAxisValues.x;

		bool isCameraTypeFree = mainPlayerCamera.isCameraTypeFree ();

		bool isPlayerMovingOn3dWorld = mainPlayerController.isPlayerMovingOn3dWorld ();

		if (!isPlayerMovingOn3dWorld || !isCameraTypeFree) {
			currentLockedCameraTransform = mainPlayerCamera.getLockedCameraTransform ();
		}

		if (isPlayerMovingOn3dWorld) {
			if (isCameraTypeFree) {
				moveInput = playerCameraTransform.forward * (currentVerticalMovement) +
				playerCameraTransform.right * (currentHorizontalMovement);
			} else {
				moveInput = currentLockedCameraTransform.forward * (currentVerticalMovement) +
				currentLockedCameraTransform.right * (currentHorizontalMovement);
			}
		} else {
			moveInput = currentLockedCameraTransform.up * (currentVerticalMovement) +
			currentLockedCameraTransform.right * (currentHorizontalMovement);
		}
			
		if (moveInput.magnitude > 1) {
			moveInput.Normalize ();
		}

		localMove = playerTransform.InverseTransformDirection (moveInput);
	}

	public override void updateControllerBehavior ()
	{
		if (slideActive) {
			if (resetAnimatorIDValue) {
				if (Time.time > lastTimeSlideActive + 0.3f) {
					mainAnimator.SetInteger (actionIDAnimatorID, 0);

					resetAnimatorIDValue = false;
				}
			}

			float currentFixedUpdateDeltaTime = mainPlayerController.getCurrentDeltaTime ();

			playerTransformUp = playerTransform.up;

			playerTransformForward = playerTransform.forward;

			Vector3 currentRaycastPosition = playerTransform.position;
			//+ playerTransformUp;
			Vector3 currentRaycastDirection = playerTransformForward;

			bool isFullBodyAwarenessActive = mainPlayerCamera.isFullBodyAwarenessActive ();

			float surfaceAngle = 0;

			float currentSlideActiveRaycastDistance = slideActiveRaycastDistance;

			if (isFullBodyAwarenessActive) {
				currentSlideActiveRaycastDistance += 0.2f;
			}

			if (Physics.Raycast (currentRaycastPosition, currentRaycastDirection, out hit, currentSlideActiveRaycastDistance, raycastLayermask)) {
				surfaceAngle = Vector3.SignedAngle (-playerTransformForward, hit.normal, playerTransformUp);
			} else {
				if (Time.time > lastTimeSlideActive + 0.4f) {
					setCheckIfDetectSlideActiveState (false);

					return;
				}
			}

			updateInputValues ();

			int slidingDownAnimatorValue = 0;

			slideDownInProcess = false;

			float currentSlideDownSpeed = slideDownOnWallSpeed;

			if (slideDownOnWall || slideDownIfInputNotPressed) {
				if (Time.time > waitTimeToStartToSlideDown + lastTimeSlideActive &&
				    Time.time > waitTimeToStartToSlideDown + lastTimeInputPressed) {

					slideDownInProcess = true;

					slidingDownAnimatorValue = 1;

					if (slideDownIfInputNotPressed) {
						
						if (localMove.z > 0) {
							slideDownInProcess = false;

							slidingDownAnimatorValue = 0;

							lastTimeInputPressed = Time.time;
						}
					}
				}

				if (stopSlideDownAfterTime) {
					if (Time.time > lastTimeSlideActive + timeToStopSlideDown) {
						slideDownInProcess = false;

						slidingDownAnimatorValue = 0;
					}
				}
			}

			if (forceSlowDownOnSurfaceActive) {
				slideDownInProcess = true;

				slidingDownAnimatorValue = 1;
			}

			bool isPlayerMovingOn3dWorld = mainPlayerController.isPlayerMovingOn3dWorld ();

			if (isPlayerMovingOn3dWorld) { 
				if (slideDownIfInputPressed) {
					if (Mathf.Abs (localMove.y) < 0.01f && localMove.z < 0) {
						slideDownInProcess = true;

						slidingDownAnimatorValue = 1;
					}
				}
			} else {
				if (slideDownIfInputPressed2_5d) {
					if (Mathf.Abs (localMove.z) < 0.01f && localMove.y < 0) {
						slideDownInProcess = true;

						slidingDownAnimatorValue = 1;
					}
				}
			}

			if ((isPlayerMovingOn3dWorld && disableSlideIfInputPressedDown) || (!isPlayerMovingOn3dWorld && disableSlideIfInputPressedDown2_5d)) {

				if (localMove.z < 0) {
					setSlideActiveState (false);

					useDoubleTapDownToStopSlideActive = true;

					return;
				}
			}

			if (useDoubleTapDownToStopSlide) {
				float verticalRawAxis = 0;

				if (isPlayerMovingOn3dWorld) {
					verticalRawAxis = localMove.z;
				} else {
					verticalRawAxis = mainPlayerController.getRawAxisValues ().y;
				}

				if (verticalRawAxis < 0) {
					if (lastTimePressedDownFirstTime == 0) {
						lastTimePressedDownFirstTime = Time.time;
					} else {
						if (pressedDownReleasedFirstTime) {
								
							lastTimePressedDownSecondTime = Time.time;

							float timeBetweenPress = Mathf.Abs (lastTimePressedDownSecondTime - lastTimePressedDownFirstTime);

							if (showDebugPrint) {
								print ("second press " + timeBetweenPress);
							}

							if (timeBetweenPress < 0.5f) {
								setSlideActiveState (false);

								useDoubleTapDownToStopSlideActive = true;

								return;
							} else {
								lastTimePressedDownFirstTime = 0;

								lastTimePressedDownSecondTime = 0;

								pressedDownReleasedFirstTime = false;
							}
						}
					}
				} else {
					if (!pressedDownReleasedFirstTime) {
						if (lastTimePressedDownFirstTime != 0) {
							pressedDownReleasedFirstTime = true;

							if (showDebugPrint) {
								print ("first press");
							}
						}
					}
				}
			}

			if (disableSlideOnWallAfterTime) {
				if (Time.time > timeToDisableSlideDown + lastTimeSlideActive) {
					setCheckIfDetectSlideActiveState (false);

					return;
				}
			}

			mainAnimator.SetFloat (horizontalAnimatorID, slidingDownAnimatorValue, inputLerpSpeed, Time.fixedDeltaTime);

			if (slideDownInProcess) {
				currentRaycastPosition = playerTransform.position - playerTransformUp;
				currentRaycastDirection = playerTransformForward;

				if (Physics.Raycast (currentRaycastPosition, currentRaycastDirection, out hit, currentSlideActiveRaycastDistance, raycastLayermask)) {
					adherePosition = hit.point + hit.normal * 0.1f;

					currentSlideDownSpeed *= slideDownSpeedMultiplier;
				
					mainRigidbody.position = Vector3.MoveTowards (mainRigidbody.position, adherePosition, currentFixedUpdateDeltaTime * currentSlideDownSpeed);
				} else {
					setSlideActiveState (false);

					return;
				}

				currentRaycastPosition = playerTransform.position;
				currentRaycastDirection = playerTransformUp;

				if (Physics.Raycast (currentRaycastPosition, -currentRaycastDirection, out hit, slideActiveRaycastDistance, raycastLayermask)) {
					slideStopFromGroundDetected = true;

					setSlideActiveState (false);

					return;
				} 
			} else {
				if (GKC_Utils.distance (mainRigidbody.position, adherePosition) > 0.01f) {
					mainRigidbody.position = Vector3.MoveTowards (mainRigidbody.position, adherePosition, currentFixedUpdateDeltaTime * adhereToWallSpeed);
				}
			}
				
			mainPlayerController.setCurrentVelocityValue (mainRigidbody.linearVelocity);

			currentSlideRotationSpeed = slideRotationSpeedThirdPerson;

			if (isFirstPersonActive) {
				currentSlideRotationSpeed = slideRotationSpeedFirstPerson;
			}

			targetRotation = Mathf.Lerp (targetRotation, surfaceAngle, currentSlideRotationSpeed);

			if (Mathf.Abs (targetRotation) > 0.001f) {
				if (isFirstPersonActive) {
					playerCameraTransform.Rotate (0, targetRotation * currentFixedUpdateDeltaTime, 0);
				} else {
					if (isFullBodyAwarenessActive) {
						if (!ignoreHorizontalCameraRotationInputState) {
							mainPlayerCamera.setIgnoreHorizontalCameraRotationOnFBAState (true);

							ignoreHorizontalCameraRotationInputState = true;
						}

						Quaternion targetRotation = playerCameraTransform.rotation * Quaternion.Euler (playerCameraTransform.up * surfaceAngle);

						playerCameraTransform.rotation = 
							Quaternion.Lerp (playerCameraTransform.rotation, targetRotation, currentFixedUpdateDeltaTime);
					} else {
						playerTransform.Rotate (0, targetRotation * currentFixedUpdateDeltaTime, 0);

						if (ignoreHorizontalCameraRotationInputState) {
							mainPlayerCamera.setIgnoreHorizontalCameraRotationOnFBAState (false);

							ignoreHorizontalCameraRotationInputState = false;
						}
					}
				}
			}
		} else {
			if (checkIfDetectSlideActive) {
				if (slideEnabled && sliderCanBeUsed && !mainPlayerController.useFirstPersonPhysicsInThirdPersonActive) {
					if (!slideActive && !mainPlayerController.pauseAllPlayerDownForces && !mainPlayerController.ignoreExternalActionsActiveState) {
						if (!mainPlayerController.isPlayerOnGround ()) {

							updateInputValues ();

							if (disableSlideIfInputPressedDown) {
								if (localMove.z < 0) {
									if (showDebugPrint) {
										print ("disableSlideIfInputPressedDown, cancelling");
									}

									return;
								}
							}

							if (useInputPressUpToAdhereToSurface) {
								if (localMove.z <= 0) {
									if (showDebugPrint) {
										print ("useInputPressUpToAdhereToSurface, cancelling");
									}

									return;
								}
							}

							if (useDoubleTapDownToStopSlideActive) {
								if (localMove.z > 0) {
									useDoubleTapDownToStopSlideActive = false;
								} else {
									if (showDebugPrint) {
										print ("useInputPressUpToAdhereToSurface, cancelling");
									}

									return;
								}
							}

							if (Time.time > lastTimeSlideActive + minWaitTimeToAdhereAgain) {
								playerTransformUp = playerTransform.up;

								playerTransformForward = playerTransform.forward;

								Vector3 currentRaycastPosition = playerTransform.position;
								//+ playerTransformUp;
								Vector3 currentRaycastDirection = playerTransformForward;

								bool canAdhereToWall = true;

								float currentRaycastDistance = raycastDistance;

								if (mainPlayerCamera.isFullBodyAwarenessActive ()) {
									currentRaycastDistance += 0.2f;
								}

								if (Physics.Raycast (currentRaycastPosition, currentRaycastDirection, out hit, currentRaycastDistance, raycastLayermask)) {
									float angle = Vector3.SignedAngle (-playerTransformForward, hit.normal, playerTransformUp);

									if (angle > minAngleToAdhereToWall) {
										canAdhereToWall = false;

										if (showDebugPrint) {
											print ("canAdhereToWall result negative, cancelling");
										}
									} else {
										adherePosition = hit.point + hit.normal * 0.1f + playerTransformUp;
									}
								} else {
									canAdhereToWall = false;

									if (showDebugPrint) {
										print ("canAdhereToWall result negative, cancelling");
									}
								}

								if (showGizmo) {
									if (canAdhereToWall) {
										Debug.DrawRay (adherePosition, 4 * currentRaycastDirection, Color.green, 5);
									}
								}

								currentRaycastDirection = -playerTransformUp;

								if (Physics.Raycast (currentRaycastPosition, currentRaycastDirection, out hit, downRaycastDistanceToCheckToAdhereToWall, raycastLayermask)) {
									canAdhereToWall = false;

									if (showDebugPrint) {
										print ("canAdhereToWall result negative, cancelling");
									}
								}

								if (canAdhereToWall) {
									setSlideActiveState (true);
								}
							}
						}
					}
				}
			}
		}
	}

	public override void setExtraImpulseForce (Vector3 forceAmount, bool useCameraDirection)
	{
		setSlideImpulseForce (forceAmount, useCameraDirection);
	}

	public void setSlideImpulseForce (Vector3 forceAmount, bool useCameraDirection)
	{
		Vector3 impulseForce = forceAmount;

		if (maxVelocityChangeSlide > 0) {
			velocityChange = impulseForce - mainRigidbody.linearVelocity;

			velocityChange = Vector3.ClampMagnitude (velocityChange, maxVelocityChangeSlide);

		} else {
			velocityChange = impulseForce;
		}

		mainPlayerController.setVelocityChangeValue (velocityChange);

		mainRigidbody.AddForce (velocityChange, ForceMode.VelocityChange);
	}

	public override void setJumpActiveForExternalForce ()
	{
		Vector3 newImpulseOnJump = impulseOnJump;

		if (!mainPlayerController.isPlayerMovingOn3dWorld ()) {
			newImpulseOnJump = impulseOnJump2_5d;
		} 

		setJumpActive (newImpulseOnJump);
	}

	public void setJumpActive (Vector3 newImpulseOnJumpAmount)
	{
		if (slideActive) {
			jumpInputUsed = true;

			setSlideActiveState (false);

			Vector3 totalForce = newImpulseOnJumpAmount.y * playerTransform.up + newImpulseOnJumpAmount.z * playerTransform.forward;

			mainPlayerController.resetLastMoveInputOnJumpValue ();

			mainPlayerController.useJumpPlatform (totalForce, ForceMode.Impulse);

			setCheckIfDetectSlideActiveState (false);

			rotateCharacterOnJump ();

			if (!mainPlayerController.isPlayerMovingOn3dWorld ()) {
				mainPlayerController.resetPlayerControllerInput ();

				mainPlayerController.setMoveInputPausedWithDuration (true, 0.2f);

				mainPlayerController.setCurrentVelocityValue (Vector3.zero);
			}
		}
	}

	public override void setExternalForceActiveState (bool state)
	{
		setSlideActiveState (state);
	}

	public void setCheckIfDetectSlideActiveState (bool state)
	{
		if (checkIfDetectSlideActive == state) {
			return;
		}

		if (mainPlayerController.isUseExternalControllerBehaviorPaused ()) {
			return;
		}

		if (state) {
			externalControllerBehavior currentExternalControllerBehavior = mainPlayerController.getCurrentExternalControllerBehavior ();

			if (currentExternalControllerBehavior != null && currentExternalControllerBehavior != this) {
				if (canBeActivatedIfOthersBehaviorsActive && checkIfCanEnableBehavior (currentExternalControllerBehavior.behaviorName)) {
					currentExternalControllerBehavior.disableExternalControllerState ();
				} else {
					return;
				}
			}
		}

		bool checkIfDetectSlideActivePrevioulsy = checkIfDetectSlideActive;

		checkIfDetectSlideActive = state;

		if (checkIfDetectSlideActive) {

			mainPlayerController.setExternalControllerBehavior (this);
		} else {
			if (checkIfDetectSlideActivePrevioulsy) {
				externalControllerBehavior currentExternalControllerBehavior = mainPlayerController.getCurrentExternalControllerBehavior ();

				if (currentExternalControllerBehavior == null || currentExternalControllerBehavior == this) {
					mainPlayerController.setExternalControllerBehavior (null);
				}
			}
		}

		mainPlayerController.setFallDamageCheckPausedState (state);

		if (!checkIfDetectSlideActive) {
			setSlideActiveState (false);
		}

		useDoubleTapDownToStopSlideActive = false;

//		bool isFullBodyAwarenessActive = mainPlayerCamera.isFullBodyAwarenessActive ();
//
//		if (isFullBodyAwarenessActive) {
//			if (state) {
//				mainPlayerCamera.setHeadColliderRadiusOnFBA (0.2f);
//			} else {
//				mainPlayerCamera.setOriginalHeadColliderRadiusOnFBA ();
//			}
//		}
	}

	public void setSlideActiveState (bool state)
	{
		if (!slideEnabled) {
			return;
		}

		if (slideActive == state) {
			return;
		}


		if (state && wallJumpSlidePaused) {
			return;
		}

		slideActive = state;

		mainPlayerController.setAddExtraRotationPausedState (state);

		mainPlayerController.setExternalControlBehaviorForAirTypeActiveState (state);

		mainPlayerController.removeJumpInputState ();

		setBehaviorCurrentlyActiveState (state);

		setCurrentPlayerActionSystemCustomActionCategoryID ();

		if (state) {
			mainPlayerController.setCheckOnGroungPausedState (true);

			mainPlayerController.setPlayerOnGroundState (false);

			mainPlayerController.setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (false);

			mainPlayerController.overrideOnGroundAnimatorValue (0);

			mainPlayerController.setPlayerOnGroundAnimatorStateOnOverrideOnGround (false);

			mainPlayerController.setOnGroundAnimatorIDValue (false);

			mainPlayerController.setPlayerVelocityToZero ();
		} else {
			mainPlayerController.setCheckOnGroungPausedState (false);

			mainPlayerController.setPlayerOnGroundState (false);

			mainPlayerController.setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (true);

			mainPlayerController.disableOverrideOnGroundAnimatorValue ();

			mainPlayerController.setPauseResetAnimatorStateFOrGroundAnimatorState (true);

			if (jumpInputUsed) {
				mainPlayerController.setOnGroundAnimatorIDValue (false);
			} else {
				if (mainPlayerController.checkIfPlayerOnGroundWithRaycast () || mainPlayerController.isPlayerOnGround () || slideStopFromGroundDetected) {

					mainPlayerController.setPlayerOnGroundState (true);

					mainPlayerController.setOnGroundAnimatorIDValue (true);
				}
			}

			if (ignoreHorizontalCameraRotationInputState) {
				mainPlayerCamera.setIgnoreHorizontalCameraRotationOnFBAState (false);

				ignoreHorizontalCameraRotationInputState = false;
			}
		}

		mainPlayerController.setFootStepManagerState (state);

		jumpInputUsed = false;

		if (showDebugPrint) {
			print ("Slide active state " + state);
		}

		isFirstPersonActive = mainPlayerController.isPlayerOnFirstPerson ();

		if (slideActive) {
			checkEventsOnStateChange (true);

			if (!isFirstPersonActive) {
				mainAnimator.SetInteger (actionIDAnimatorID, actionID);

				mainAnimator.SetBool (externalControlleBehaviorActiveAnimatorID, state);
			}

			mainPlayerController.setJumpsAmountValue (0);

			mainPlayerCamera.enableOrDisableChangeCameraView (false);

			if (!isFirstPersonActive) {
				if (keepWeapons) {
					carryingWeaponsPreviously = mainPlayerWeaponsManager.isPlayerCarringWeapon ();

					if (carryingWeaponsPreviously) {
						mainPlayerWeaponsManager.checkIfDisableCurrentWeapon ();
					}

					mainPlayerWeaponsManager.setGeneralWeaponsInputActiveState (false);
				}
			}

			resetAnimatorIDValue = true;
		} else {
			checkEventsOnStateChange (false);

			if (!isFirstPersonActive) {
				mainAnimator.SetBool (externalControlleBehaviorActiveAnimatorID, state);

				mainAnimator.SetInteger (actionIDAnimatorID, 0);
			}

			mainPlayerCamera.setOriginalchangeCameraViewEnabledValue ();


			if (keepWeapons) {
				mainPlayerWeaponsManager.setGeneralWeaponsInputActiveState (true);
			}

			if (carryingWeaponsPreviously) {
				if (!drawWeaponsIfCarriedPreviously) {
					carryingWeaponsPreviously = false;
				}
			}

			resetAnimatorIDValue = false;
		}

		if (mainHeadTrack != null) {
			mainHeadTrack.setHeadTrackSmoothPauseState (slideActive);
		}

		mainPlayerCamera.setPausePlayerCameraViewChangeState (slideActive);

		mainPlayerController.setLastTimeFalling ();

		mainPlayerCamera.stopShakeCamera ();

		lastTimeSlideActive = Time.time;

		lastTimeInputPressed = 0;

		targetRotation = 0;

		slideStopFromGroundDetected = false;

		lastTimePressedDownFirstTime = 0;

		lastTimePressedDownSecondTime = 0;

		pressedDownReleasedFirstTime = false;

		useDoubleTapDownToStopSlideActive = false;
	}

	bool slideStopFromGroundDetected;

	public override void setExternalForceEnabledState (bool state)
	{
		setSlideEnabledState (state);
	}

	public void setSlideEnabledState (bool state)
	{
		if (!state) {
			setSlideActiveState (false);
		}

		slideEnabled = state;
	}

	public void setSlideCanBeUsedState (bool state)
	{
		sliderCanBeUsed = state;
	}

	public void setSliderEnabledState ()
	{
		setSlideEnabledState (originalSlideEnabled);
	}

	public void setOriginalSliderEnabledState ()
	{
		if (slideActive) {
			setCheckIfDetectSlideActiveState (false);
		}

		setSlideEnabledState (originalSlideEnabled);
	}

	public void setSlideDownSpeedMultiplier (float newValue)
	{
		slideDownSpeedMultiplier = newValue;
	}

	public void setForceSlowDownOnSurfaceActiveState (bool state)
	{
		forceSlowDownOnSurfaceActive = state;
	}

	public void enableCheckIfDetectSlideActiveStateExternally ()
	{
		if (checkIfDetectSlideActive) {
			return;
		}

		setCheckIfDetectSlideActiveState (true);
	}

	public void disableCheckIfDetectSlideActiveStateExternally ()
	{
		if (!checkIfDetectSlideActive) {
			return;
		}

		setCheckIfDetectSlideActiveState (false);
	}


	public override void checkIfResumeExternalControllerState ()
	{
		if (checkIfDetectSlideActive) {
			if (showDebugPrint) {
				print ("resuming wall slide jump state");
			}

			externalControllerBehavior currentExternalControllerBehavior = mainPlayerController.getCurrentExternalControllerBehavior ();

			if (currentExternalControllerBehavior != null && currentExternalControllerBehavior != this) {
				currentExternalControllerBehavior.disableExternalControllerState ();
			}

			checkIfDetectSlideActive = false;

			setCheckIfDetectSlideActiveState (true);
		}
	}

	public override void disableExternalControllerState ()
	{
		setCheckIfDetectSlideActiveState (false);
	}

	public void checkEventsOnStateChange (bool state)
	{
		if (isFirstPersonActive) {
			if (useEventsOnFirstPerson) {
				if (state) {
					eventOnStartFirstPerson.Invoke ();
				} else {
					eventOnEndFirstPerson.Invoke ();
				}
			} 
		} else {
			if (useEventsOnThirdPerson) {
				if (state) {
					eventOnStartThirdPerson.Invoke ();
				} else {
					eventOnEndThirdPerson.Invoke ();
				}
			}
		}
	}

	public void setWallJumpSlidePausedState (bool state)
	{
		if (state) {
			if (slideActive) {
				setSlideActiveState (false);
			}
		}

		wallJumpSlidePaused = state;
	}

	public void rotateCharacterOnJump ()
	{
		stopRotateCharacterOnJumpCoroutine ();

		jumpCoroutine = StartCoroutine (rotateCharacterOnJumpCoroutine ());
	}

	void stopRotateCharacterOnJumpCoroutine ()
	{
		if (jumpCoroutine != null) {
			StopCoroutine (jumpCoroutine);
		}
	}

	public IEnumerator rotateCharacterOnJumpCoroutine ()
	{
		bool targetReached = false;

		float movementTimer = 0;

		float t = 0;

		float duration = 0;

		if (isFirstPersonActive) {
			duration = 0.5f / jumpRotationSpeedFirstPerson;
		} else {
			duration = 0.5f / jumpRotationSpeedThirdPerson;
		}

		float angleDifference = 0;

		bool isFullBodyAwarenessActive = mainPlayerCamera.isFullBodyAwarenessActive ();

		Transform objectToRotate = playerTransform;

		if (isFirstPersonActive || isFullBodyAwarenessActive) {
			objectToRotate = playerCameraTransform;
		}

		Quaternion targetRotation = Quaternion.LookRotation (-objectToRotate.forward, objectToRotate.up);

		while (!targetReached) {
			t += Time.deltaTime / duration; 

			objectToRotate.rotation = Quaternion.Slerp (objectToRotate.rotation, targetRotation, t);

			angleDifference = Quaternion.Angle (objectToRotate.rotation, targetRotation);

			movementTimer += Time.deltaTime;

			if (angleDifference < 0.2f || movementTimer > (duration + 1)) {
				targetReached = true;
			}
			yield return null;
		}
	}

	public override void setCurrentPlayerActionSystemCustomActionCategoryID ()
	{
		if (behaviorCurrentlyActive) {
			if (customActionCategoryID > -1) {
				mainPlayerController.setCurrentCustomActionCategoryID (customActionCategoryID);
			}
		} else {
			if (regularActionCategoryID > -1) {
				mainPlayerController.setCurrentCustomActionCategoryID (regularActionCategoryID);
			}
		}
	}
}