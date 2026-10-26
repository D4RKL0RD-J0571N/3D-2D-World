using UnityEngine;
using System.Collections;
using GameKitController.Audio;

public class simpleLamp : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool hasLamp;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject lampLight;
    public AudioClip switchSound;
    public AudioElement switchAudioElement;

    public AudioSource audioSource;

    void Start ()
    {
        if (audioSource == null) {
            audioSource = GetComponent<AudioSource> ();
        }

        if (switchSound != null) {
            switchAudioElement.clip = switchSound;
        }

        if (audioSource != null) {
            switchAudioElement.audioSource = audioSource;
        }
    }

    public void lampPlaced ()
    {
        hasLamp = true;
    }

    public void activateDevice ()
    {
        setActivateDeviceState (!lampLight.activeSelf);
    }

    public void setActivateDeviceState (bool state)
    {
        if (hasLamp) {
            AudioPlayer.PlayOneShot (switchAudioElement, gameObject);

            if (lampLight.activeSelf != state) {
                lampLight.SetActive (state);
            }
        }
    }
}
