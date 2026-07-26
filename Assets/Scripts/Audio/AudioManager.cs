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

    [Header("Двигатель — кривая питча от скорости")]
    [Tooltip("X: нормализованная скорость (0-1), Y: множитель питча (0.5-2.5). Определяет как питч растёт со скоростью.")]
    [SerializeField] private AnimationCurve pitchSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0.8f, 0.5f, 0.5f),
        new Keyframe(0.5f, 1.2f, 0.8f, 0.8f),
        new Keyframe(1f, 1.8f, 1.5f, 1.5f)
    );

    [Header("Двигатель — плавный переход")]
    [SerializeField] private float crossfadeSpeed = 2f;
    [SerializeField] private float drivingSpeedThreshold = 0.1f;

    [Header("Двигатель — обрезка клипов (секунды)")]
    [SerializeField] private float idleTrimStart = 0f;
    [SerializeField] private float idleTrimEnd = 0f;
    [SerializeField] private float drivingTrimStart = 0f;
    [SerializeField] private float drivingTrimEnd = 0f;
    [SerializeField] private float reverseTrimStart = 0f;
    [SerializeField] private float reverseTrimEnd = 0f;

    [Header("Таймер")]
    [SerializeField] private AudioClip timerEndClip;
    [Range(0f, 1f)] [SerializeField] private float timerEndVolume = 1f;

    [Header("End Panel")]
    [SerializeField] private AudioClip endPanelClip;
    [Range(0f, 1f)] [SerializeField] private float endPanelVolume = 0.7f;

    private AudioSource sfxSource;
    private AudioSource idleSource;
    private AudioSource drivingSource;
    private AudioSource reverseSource;

    private float idleVolumeTarget;
    private float drivingVolumeTarget;
    private float reverseVolumeTarget;

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

    private void Update()
    {
        UpdateEngineCrossfade();
        UpdateTrimLoop(idleSource, idleTrimStart, idleTrimEnd);
        UpdateTrimLoop(drivingSource, drivingTrimStart, drivingTrimEnd);
        UpdateTrimLoop(reverseSource, reverseTrimStart, reverseTrimEnd);
    }

    private void InitSources()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        idleSource = gameObject.AddComponent<AudioSource>();
        idleSource.playOnAwake = false;
        idleSource.loop = true;

        drivingSource = gameObject.AddComponent<AudioSource>();
        drivingSource.playOnAwake = false;
        drivingSource.loop = true;

        reverseSource = gameObject.AddComponent<AudioSource>();
        reverseSource.playOnAwake = false;
        reverseSource.loop = true;
    }

    private void UpdateEngineCrossfade()
    {
        float fadeDelta = crossfadeSpeed * Time.deltaTime;

        float idleVol = Mathf.MoveTowards(idleSource.volume, idleVolumeTarget, fadeDelta);
        float drivingVol = Mathf.MoveTowards(drivingSource.volume, drivingVolumeTarget, fadeDelta);
        float reverseVol = Mathf.MoveTowards(reverseSource.volume, reverseVolumeTarget, fadeDelta);

        idleSource.volume = idleVol;
        drivingSource.volume = drivingVol;
        reverseSource.volume = reverseVol;

        if (idleVol <= 0f && idleSource.isPlaying) idleSource.Stop();
        if (drivingVol <= 0f && drivingSource.isPlaying) drivingSource.Stop();
        if (reverseVol <= 0f && reverseSource.isPlaying) reverseSource.Stop();
    }

    private void UpdateTrimLoop(AudioSource source, float trimStart, float trimEnd)
    {
        if (!source.isPlaying || source.clip == null) return;

        if (source.time < trimStart)
            source.time = trimStart;

        float endTime = source.clip.length - trimEnd;
        if (source.time >= endTime)
            source.time = trimStart;
    }

    public void PlayExplosion()
    {
        if (explosionClips == null || explosionClips.Length == 0) return;
        AudioClip clip = explosionClips[Random.Range(0, explosionClips.Length)];
        sfxSource.pitch = Random.Range(explosionPitchMin, explosionPitchMax);
        sfxSource.PlayOneShot(clip, explosionVolume);
    }

    public void UpdateEngine(float normalizedSpeed, float forwardSpeed)
    {
        bool isDriving = forwardSpeed > drivingSpeedThreshold * ScaledMaxSpeed();
        bool isReverse = forwardSpeed < -drivingSpeedThreshold * ScaledMaxSpeed() * 0.5f;

        idleVolumeTarget = (!isDriving && !isReverse) ? engineVolume : 0f;
        drivingVolumeTarget = isDriving ? engineVolume : 0f;
        reverseVolumeTarget = isReverse ? engineVolume : 0f;

        if (idleVolumeTarget > 0f && engineIdleClip != null)
        {
            if (idleSource.clip != engineIdleClip || !idleSource.isPlaying)
            {
                idleSource.clip = engineIdleClip;
                idleSource.pitch = idlePitch;
                idleSource.time = idleTrimStart;
                idleSource.Play();
            }
        }

        if (drivingVolumeTarget > 0f && engineDrivingClip != null)
        {
            if (drivingSource.clip != engineDrivingClip || !drivingSource.isPlaying)
            {
                drivingSource.clip = engineDrivingClip;
                drivingSource.time = drivingTrimStart;
                drivingSource.Play();
            }
            drivingSource.pitch = pitchSpeedCurve.Evaluate(normalizedSpeed);
        }

        if (reverseVolumeTarget > 0f && engineReverseClip != null)
        {
            if (reverseSource.clip != engineReverseClip || !reverseSource.isPlaying)
            {
                reverseSource.clip = engineReverseClip;
                reverseSource.pitch = reversePitch;
                reverseSource.time = reverseTrimStart;
                reverseSource.Play();
            }
        }
    }

    private float ScaledMaxSpeed()
    {
        return 30f;
    }

    public void StopEngine()
    {
        idleVolumeTarget = 0f;
        drivingVolumeTarget = 0f;
        reverseVolumeTarget = 0f;
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
