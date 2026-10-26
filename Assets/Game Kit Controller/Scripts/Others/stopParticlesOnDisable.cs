using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stopParticlesOnDisable : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool disableParticlesOnDisableEnabled = true;

	public bool clearParticlesEnabled = true;

	public ParticleSystem mainParticleSystem;

	bool mainParticleSystemLocated;


	void OnDisable ()
	{
		if (disableParticlesOnDisableEnabled) {
			if (!mainParticleSystemLocated) {
				mainParticleSystemLocated = mainParticleSystem != null;

				if (!mainParticleSystemLocated) {
					mainParticleSystem = GetComponentInChildren<ParticleSystem> ();

					mainParticleSystemLocated = mainParticleSystem != null;
				}
			}

			if (mainParticleSystemLocated) {
				mainParticleSystem.Stop ();

				if (clearParticlesEnabled) {
					mainParticleSystem.Clear ();
				}
			}
		}

	}
}

