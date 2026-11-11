using System.Collections;
using UnityEngine;

public class EMPExplosionFX : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float despawnTime = 3f;
    [SerializeField] private float lightDuration = 0.08f;

    [Header("Light")]
    [SerializeField] private Light lightFlash;

    [Header("Audio")]
    [SerializeField] private AudioClip[] explosionSounds;
    [SerializeField] private AudioSource audioSource;

    private void Start()
    {
        StartCoroutine(DestroyTimer());
        StartCoroutine(LightFlash());

        if (audioSource != null && explosionSounds != null && explosionSounds.Length > 0)
        {
            audioSource.clip = explosionSounds[Random.Range(0, explosionSounds.Length)];
            audioSource.Play();
        }
    }

    private IEnumerator LightFlash()
    {
        if (lightFlash != null)
        {
            lightFlash.enabled = true;
            yield return new WaitForSeconds(lightDuration);
            lightFlash.enabled = false;
        }
    }

    private IEnumerator DestroyTimer()
    {
        yield return new WaitForSeconds(despawnTime);
        Destroy(gameObject);
    }
}