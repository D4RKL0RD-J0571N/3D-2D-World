using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;
using UnityEngine.Events;

public class flamethrowerWeapon : MonoBehaviour
{
    [Space]
    [Header ("Main Settings")]
    [Space]

    public bool useEnergyEnabled = true;
    public float useEnergyRate;
    public int amountEnergyUsed;

    [Space]
    [Header ("Sound Settings")]
    [Space]

    public float playSoundRate;
    public float minDelayToPlaySound;

    [Space]

    public AudioClip soundEffect;
    public AudioElement soundEffectAudioElement;

    public AudioClip startSoundEffect;
    public AudioElement startSoundEffectAudioElement;

    public AudioClip endSoundEffect;
    public AudioElement endSoundEffectAudioElement;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool weaponEnabled;

    public bool reloading;

    public bool updateCoroutineActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnWeaponStateChange;
    public UnityEvent eventOnWeaponEnabled;
    public UnityEvent eventOnWeaponDisabled;

    [Space]
    [Header ("Components")]
    [Space]

    public bool configuredOnWeapon = true;
    public playerWeaponSystem weaponManager;

    public ParticleSystem mainParticleSystem;

    public AudioSource mainAudioSource;

    public AudioSource startAudioSource;
    public AudioSource endAudioSource;


    float lastTimeUsed;
    float lastTimeSoundPlayed;

    bool initialSoundWaitChecked;

    Coroutine updateCoroutine;

    bool ignoreDisableCoroutine;

    bool canPlaySound;

    bool isWeaponReloading;

    bool remainAmmoInClip;

    bool soundEffectAudioElementLocated;
    bool startSoundEffectAudioElementLocated;
    bool endSoundEffectAudioElementLocated;

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }

        updateCoroutineActive = false;
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        canPlaySound = true;

        if (configuredOnWeapon) {
            isWeaponReloading = weaponManager.isWeaponReloading ();

            remainAmmoInClip = weaponManager.remainAmmoInClip ();

            if (reloading) {
                if (remainAmmoInClip && weaponManager.carryingWeapon () && !isWeaponReloading) {
                    reloading = false;
                } else {
                    return;
                }
            }

            if (reloading) {
                return;
            }

            if (!weaponEnabled) {
                return;
            }

            if(useEnergyEnabled){
                if (Time.time > lastTimeUsed + useEnergyRate) {
                    if (remainAmmoInClip && !isWeaponReloading) {
                        lastTimeUsed = Time.time;

                        weaponManager.useAmmo (amountEnergyUsed);

                        weaponManager.checkToUpdateInventoryWeaponAmmoTextFromWeaponSystem ();
                    }

                    if (!remainAmmoInClip || isWeaponReloading) {
                        ignoreDisableCoroutine = true;

                        setWeaponState (false);

                        ignoreDisableCoroutine = false;

                        reloading = true;

                        return;
                    }
                }
            }

            if (!remainAmmoInClip || isWeaponReloading) {
                canPlaySound = false;
            }
        }

        if (weaponEnabled && canPlaySound) {
            if (Time.time > lastTimeSoundPlayed + playSoundRate) {
                if (initialSoundWaitChecked || Time.time > lastTimeSoundPlayed + minDelayToPlaySound) {
                    lastTimeSoundPlayed = Time.time;

                    playWeaponSoundEffect ();

                    initialSoundWaitChecked = true;
                }
            }
        }
    }

    public void enableWeapon ()
    {
        setWeaponState (true);
    }

    public void disableWeapon ()
    {
        setWeaponState (false);

        reloading = false;
    }

    public void setWeaponState (bool state)
    {
        if (reloading) {
            bool weaponEnabledPreviously = weaponEnabled;

            weaponEnabled = false;

            if (!weaponEnabledPreviously) {
                return;
            } else {
                if (!state) {
                    weaponEnabled = true;
                }
            }
        }

        initializeComponents ();

        if (weaponEnabled == state) {
            return;
        }

        weaponEnabled = state;

        if (mainParticleSystem != null) {
            if (weaponEnabled) {
                mainParticleSystem.Play ();
            } else {
                mainParticleSystem.Stop ();
            }
        }

        if (weaponEnabled) {
            playWeaponStartSoundEffect ();
        } else {
            playWeaponEndSoundEffect ();
        }

        checkEventsOnStateChange (weaponEnabled);

        initialSoundWaitChecked = false;

        lastTimeSoundPlayed = 0;

        if (!weaponEnabled) {
            stopWeaponSoundEffect ();
        }

        if (ignoreDisableCoroutine) {
            return;
        }

        if (weaponEnabled) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());

            updateCoroutineActive = true;
        } else {
            stopUpdateCoroutine ();
        }
    }

    public void stopUpdateCoroutineIfActive ()
    {
        if (updateCoroutineActive) {
            stopUpdateCoroutine ();
        }
    }

    void playWeaponSoundEffect ()
    {
        if (soundEffectAudioElementLocated) {
            AudioPlayer.PlayOneShot (soundEffectAudioElement, gameObject);
        }
    }

    void stopWeaponSoundEffect ()
    {
        if (soundEffectAudioElementLocated) {
            AudioPlayer.Stop (soundEffectAudioElement, gameObject);
        }
    }

    void playWeaponStartSoundEffect ()
    {
        if (startSoundEffectAudioElementLocated) {
            AudioPlayer.PlayOneShot (startSoundEffectAudioElement, gameObject);
        }
    }

    void playWeaponEndSoundEffect ()
    {
        if (endSoundEffectAudioElementLocated) {
            AudioPlayer.PlayOneShot (endSoundEffectAudioElement, gameObject);
        }
    }

    void checkEventsOnStateChange (bool state)
    {
        if (useEventsOnWeaponStateChange) {
            if (state) {
                eventOnWeaponEnabled.Invoke ();
            } else {
                eventOnWeaponDisabled.Invoke ();
            }
        }
    }

    bool componentsInitialized;

    void initializeComponents ()
    {
        if (componentsInitialized) {
            return;
        }

        setObjectParentSystem mainSetObjectParentSystem = GetComponent<setObjectParentSystem> ();

        if (mainSetObjectParentSystem != null) {
            if (mainSetObjectParentSystem.getParentTransform () == null) {
                if (weaponManager != null) {
                    playerWeaponsManager mainPlayerWeaponsManager = weaponManager.getPlayerWeaponsManger ();

                    GameObject playerControllerGameObject = mainPlayerWeaponsManager.gameObject;

                    playerComponentsManager mainPlayerComponentsManager = playerControllerGameObject.GetComponent<playerComponentsManager> ();

                    if (mainPlayerComponentsManager != null) {
                        playerController mainPlayerController = mainPlayerComponentsManager.getPlayerController ();

                        if (mainPlayerController != null) {
                            GameObject playerParentGameObject = mainPlayerController.getPlayerManagersParentGameObject ();

                            if (playerParentGameObject != null) {
                                mainSetObjectParentSystem.setParentTransform (playerParentGameObject.transform);
                            }
                        }
                    }
                }
            }
        }

        if (soundEffect != null) {
            soundEffectAudioElement.clip = soundEffect;

            soundEffectAudioElementLocated = true;
        }

        if (mainAudioSource != null) {
            soundEffectAudioElement.audioSource = mainAudioSource;
        }

        if (startSoundEffect != null) {
            startSoundEffectAudioElement.clip = startSoundEffect;

            startSoundEffectAudioElementLocated = true;
        }

        if (startAudioSource != null) {
            startSoundEffectAudioElement.audioSource = startAudioSource;
        }

        if (endSoundEffect != null) {
            endSoundEffectAudioElement.clip = endSoundEffect;

            endSoundEffectAudioElementLocated = true;
        }

        if (endAudioSource != null) {
            endSoundEffectAudioElement.audioSource = endAudioSource;
        }

        componentsInitialized = true;
    }
}