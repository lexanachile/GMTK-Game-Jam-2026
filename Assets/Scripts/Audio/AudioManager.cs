using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Взрывы")]
    [SerializeField] private AudioClip[] explosionClips;
    [Range(0f, 1f)] [SerializeField] private float explosionVolume = 0.8f;
    [SerializeField] private float explosionPitchMin = 0.9f;
    [SerializeField] private float explosionPitchMax = 1.1f;

    [Header("Мотоцикл — двигатель")]
    [SerializeField] private AudioClip engineIdleClip;
    [SerializeField] private AudioClip engineDrivingClip;
    [SerializeField] private AudioClip engineReverseClip;
    [Range(0f, 1f)] [SerializeField] private float engineVolume = 0.5f;
    [SerializeField] private float idlePitch = 0.8f;
    [SerializeField] private float drivingPitchMin = 1f;
    [SerializeField] private float drivingPitchMax = 1.8f;
    [SerializeField] private float reversePitch = 0.7f;

    [Header("Таймер")]
    [SerializeField] private AudioClip timerEndClip;
    [Range(0f, 1f)] [SerializeField] private float timerEndVolume = 1f;

    [Header("End Panel")]
    [SerializeField] private AudioClip endPanelClip;
    [Range(0f, 1f)] [SerializeField] private float endPanelVolume = 0.7f;

    private AudioSource sfxSource;
    private AudioSource engineSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitSources()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.playOnAwake = false;
        engineSource.loop = true;
    }

    public void PlayExplosion()
    {
        if (explosionClips == null || explosionClips.Length == 0) return;
        AudioClip clip = explosionClips[Random.Range(0, explosionClips.Length)];
        sfxSource.pitch = Random.Range(explosionPitchMin, explosionPitchMax);
        sfxSource.PlayOneShot(clip, explosionVolume);
    }

    public void PlayEngineIdle()
    {
        if (engineIdleClip == null) return;
        if (engineSource.clip == engineIdleClip && engineSource.isPlaying) return;
        engineSource.clip = engineIdleClip;
        engineSource.pitch = idlePitch;
        engineSource.volume = engineVolume;
        engineSource.Play();
    }

    public void PlayEngineDriving(float normalizedSpeed)
    {
        if (engineDrivingClip == null) return;
        if (engineSource.clip != engineDrivingClip)
        {
            engineSource.clip = engineDrivingClip;
            engineSource.Play();
        }
        engineSource.pitch = Mathf.Lerp(drivingPitchMin, drivingPitchMax, normalizedSpeed);
        engineSource.volume = engineVolume;
    }

    public void PlayEngineReverse()
    {
        if (engineReverseClip == null) return;
        if (engineSource.clip == engineReverseClip && engineSource.isPlaying) return;
        engineSource.clip = engineReverseClip;
        engineSource.pitch = reversePitch;
        engineSource.volume = engineVolume;
        engineSource.Play();
    }

    public void StopEngine()
    {
        if (engineSource.isPlaying)
            engineSource.Stop();
    }

    public void PlayTimerEnd()
    {
        if (timerEndClip == null) return;
        sfxSource.PlayOneShot(timerEndClip, timerEndVolume);
    }

    public void PlayEndPanel()
    {
        if (endPanelClip == null) return;
        sfxSource.PlayOneShot(endPanelClip, endPanelVolume);
    }
}
