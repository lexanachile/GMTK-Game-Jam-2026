using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class MotorcycleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform visual;

    [Header("World Scale")]
    [SerializeField] private float worldScale = 1f;

    [Header("Speed Limits")]
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float maxReverseSpeed = 10f;

    [Header("Acceleration — Target Speed Curve")]
    [Tooltip("X: нормализованная скорость (0-1), Y: множитель целевой скорости.\n" +
             "Форма кривой определяет мощность на разных скоростях:\n" +
             "  Высокий Y в начале = мощный старт\n" +
             "  Падение Y к концу = ограничение максимальной скорости")]
    [SerializeField] private AnimationCurve accelerationCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f, 1f, 0.5f),
        new Keyframe(0.3f, 0.85f, 0.3f, 0.3f),
        new Keyframe(0.6f, 0.95f, 0f, 0f),
        new Keyframe(0.85f, 0.7f, -0.8f, -0.8f),
        new Keyframe(1f, 0f, -2f, -2f)
    );

    [Header("Acceleration — Second Order Dynamics")]
    [Tooltip("Плавное ускорение/торможение с инерцией.\n" +
             "  frequency: скорость реакции (2-4 для плавности)\n" +
             "  damping: плавность (0.8-1.0 без колебаний)\n" +
             "  initialResponse: начальная реакция (0.2-0.5 для постепенного старта)")]
    [SerializeField] private SecondOrderDynamics forwardDynamics = new SecondOrderDynamics();

    [Header("Braking")]
    [SerializeField] private float brakeDeceleration = 65f;
    [Tooltip("X: нормализованная скорость (0-1), Y: множитель силы торможения.\n" +
             "  Y > 1 = торможение сильнее базового\n" +
             "  Y < 1 = торможение слабее")]
    [SerializeField] private AnimationCurve brakeCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f, 0f, 1f),
        new Keyframe(0.3f, 0.9f, 0.5f, 0.5f),
        new Keyframe(0.7f, 1.1f, 0f, 0f),
        new Keyframe(1f, 1.3f, 0f, 0f)
    );

    [Header("Arc Steering")]
    [Tooltip("Минимальный радиус поворота при полном руле (единицы Unity).\n" +
             "  3-5  = очень резкий (карт)\n" +
             "  5-8  = манёвренный (спортбайк)\n" +
             "  8-12 = плавный (круизер)")]
    [SerializeField] private float minTurnRadius = 6f;

    [Tooltip("X: нормализованная скорость (0-1), Y: множитель угловой скорости.\n" +
             "  Y > 1 при среднем X = отзывчивее на крейсерской\n" +
             "  Y < 1 при высоком X = стабильнее на максимуме")]
    [SerializeField] private AnimationCurve steeringCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f, 2f, 2f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.6f, 1f, -0.1f, -0.1f),
        new Keyframe(1f, 0.7f, -0.3f, -0.3f)
    );

    [Header("Steering Smoothing")]
    [Tooltip("Скорость плавного нарастания угловой скорости.\n" +
             "  5-8  = мгновенный\n" +
             "  8-12 = отзывчивый, но плавный (рекомендуется)\n" +
             "  12-20 = очень плавный")]
    [SerializeField] private float steeringSmoothSpeed = 10f;

    [Header("Grip & Drift")]
    [Tooltip("Сила сцепления на низкой скорости. Выше = меньше заноса.")]
    [SerializeField] private float lowSpeedGrip = 18f;
    [Tooltip("Сила сцепления на высокой скорости. Выше = меньше заноса.")]
    [SerializeField] private float highSpeedGrip = 4f;
    [Tooltip("Множитель сцепления при дрифте. Ниже = сильнее занос.")]
    [SerializeField] private float driftGripMultiplier = 0.35f;
    [Tooltip("Порог скорости для начала дрифта (нормализованный 0-1).")]
    [SerializeField] private float driftSpeedThreshold = 0.5f;
    [Tooltip("SecondOrderDynamics для плавного бокового скольжения.\n" +
             "  frequency: скорость реакции на занос (3-6)\n" +
             "  damping: плавность (0.6-0.9 для упругого заноса)\n" +
             "  initialResponse: начальная реакция (0.5-1.0)")]
    [SerializeField] private SecondOrderDynamics lateralDynamics = new SecondOrderDynamics();

    [Header("Coast & Friction")]
    [Tooltip("Сила трения при отпущенном газе. Выше = быстрее замедление.")]
    [SerializeField] private float coastFriction = 3.5f;

    [Header("Angular Limits")]
    [Tooltip("Максимальная угловая скорость (градусы/сек).")]
    [SerializeField] private float maxAngularVelocity = 400f;

    [Header("Reverse")]
    [Tooltip("Задержка перед включением заднего хода (сек).")]
    [SerializeField] private float reverseDelay = 0.3f;

    [Header("Visual — Lean Animation")]
    [Tooltip("Максимальный угол наклона спрайта (градусы).")]
    [SerializeField] private float visualLeanMax = 25f;
    [Tooltip("Чувствительность наклона к боковой скорости.")]
    [SerializeField] private float leanSensitivity = 2.5f;
    [Tooltip("SecondOrderDynamics для плавного наклона спрайта.\n" +
             "  frequency: скорость наклона (3-5)\n" +
             "  damping: плавность (0.5-0.8)\n" +
             "  initialResponse: -1 для упреждения (наклон в противоположную сторону перед поворотом)")]
    [SerializeField] private SecondOrderDynamics leanDynamics = new SecondOrderDynamics();

    [Header("Drift Visual Effects")]
    [Tooltip("Порог боковой скорости для отрисовки следов дрифта.")]
    [SerializeField] private float driftTrailThreshold = 3f;
    [Tooltip("Цвет следов дрифта.")]
    [SerializeField] private Color driftTrailColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [Tooltip("Ширина следа дрифта.")]
    [SerializeField] private float driftTrailWidth = 0.1f;

    private PlayerControls controls;
    private float inputX;
    private float inputY;
    private float reverseTimer;
    private float currentAngularVelocity;
    private float currentLateralSpeed;
    private TrailRenderer leftTrail;
    private TrailRenderer rightTrail;

    private Vector2 Forward => (Vector2)transform.up;
    private Vector2 Right => (Vector2)transform.right;

    private float ScaledMaxSpeed => maxSpeed * worldScale;
    private float ScaledMaxReverseSpeed => maxReverseSpeed * worldScale;
    private float ScaledBrakeDeceleration => brakeDeceleration * worldScale;
    private float ScaledCoastFriction => coastFriction * worldScale;
    private float ScaledMinTurnRadius => minTurnRadius * worldScale;

    public float Speed => rb.linearVelocity.magnitude;
    public float ForwardSpeed => Vector2.Dot(rb.linearVelocity, Forward);
    public float NormalizedSpeed => Mathf.Clamp01(Speed / ScaledMaxSpeed);
    public bool IsDrifting { get; private set; }
    public float DriftIntensity { get; private set; }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        forwardDynamics.frequency = 2.5f;
        forwardDynamics.damping = 0.9f;
        forwardDynamics.initialResponse = 0.3f;
        forwardDynamics.maxVelocity = 200f;

        lateralDynamics.frequency = 4f;
        lateralDynamics.damping = 0.7f;
        lateralDynamics.initialResponse = 0.8f;
        lateralDynamics.maxVelocity = 150f;

        leanDynamics.frequency = 3f;
        leanDynamics.damping = 0.5f;
        leanDynamics.initialResponse = -1f;
        leanDynamics.maxVelocity = 300f;
    }

    private void Awake()
    {
        controls = new PlayerControls();

        rb.bodyType = RigidbodyType2D.Kinematic;

        forwardDynamics.Reset(0f);
        forwardDynamics.maxVelocity = ScaledMaxSpeed * 8f;
        lateralDynamics.Reset(0f);
        leanDynamics.Reset(0f);
        currentAngularVelocity = 0f;
        currentLateralSpeed = 0f;

        SetupDriftTrails();
    }

    private void SetupDriftTrails()
    {
        var leftTrailObj = new GameObject("LeftDriftTrail");
        leftTrailObj.transform.SetParent(transform);
        leftTrailObj.transform.localPosition = new Vector3(-0.3f, -0.5f, 0f);
        leftTrail = leftTrailObj.AddComponent<TrailRenderer>();
        ConfigureTrail(leftTrail);

        var rightTrailObj = new GameObject("RightDriftTrail");
        rightTrailObj.transform.SetParent(transform);
        rightTrailObj.transform.localPosition = new Vector3(0.3f, -0.5f, 0f);
        rightTrail = rightTrailObj.AddComponent<TrailRenderer>();
        ConfigureTrail(rightTrail);
    }

    private void ConfigureTrail(TrailRenderer trail)
    {
        trail.time = 2f;
        trail.startWidth = driftTrailWidth;
        trail.endWidth = driftTrailWidth * 0.5f;
        trail.startColor = driftTrailColor;
        trail.endColor = new Color(driftTrailColor.r, driftTrailColor.g, driftTrailColor.b, 0f);
        trail.emitting = false;
        trail.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void OnEnable()
    {
        controls.Motorcycle.Enable();
    }

    private void OnDisable()
    {
        controls.Motorcycle.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void Update()
    {
        ReadInput();
        UpdateVisual();
        UpdateDriftTrails();
    }

    private void FixedUpdate()
    {
        HandleForwardMovement();
        ApplyArcSteering();
        ApplyGrip();
        ApplyAngularDamping();
    }

    private void ReadInput()
    {
        Vector2 move = controls.Motorcycle.Move.ReadValue<Vector2>();
        inputX = move.x;
        inputY = move.y;
    }

    private void HandleForwardMovement()
    {
        float currentForward = Vector2.Dot(rb.linearVelocity, Forward);
        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(currentForward) / ScaledMaxSpeed);
        Vector2 lateralVel = rb.linearVelocity - Forward * currentForward;
        float newForwardSpeed = currentForward;

        if (inputY > 0f)
        {
            reverseTimer = 0f;
            float curveValue = accelerationCurve.Evaluate(normalizedSpeed);
            float targetSpeed = ScaledMaxSpeed * curveValue * inputY;

            newForwardSpeed = forwardDynamics.Update(targetSpeed, Time.fixedDeltaTime);
            newForwardSpeed = Mathf.Clamp(newForwardSpeed, -ScaledMaxReverseSpeed, ScaledMaxSpeed);
        }
        else if (inputY < 0f)
        {
            if (currentForward > 1f)
            {
                float curveValue = brakeCurve.Evaluate(normalizedSpeed);
                float brakeAmount = ScaledBrakeDeceleration * curveValue * Time.fixedDeltaTime;
                newForwardSpeed = Mathf.Max(0f, currentForward - brakeAmount);
                forwardDynamics.Update(newForwardSpeed, Time.fixedDeltaTime);
            }
            else
            {
                reverseTimer += Time.fixedDeltaTime;
                if (reverseTimer >= reverseDelay)
                {
                    float targetSpeed = -ScaledMaxReverseSpeed * Mathf.Abs(inputY);
                    newForwardSpeed = forwardDynamics.Update(targetSpeed, Time.fixedDeltaTime);
                    newForwardSpeed = Mathf.Clamp(newForwardSpeed, -ScaledMaxReverseSpeed, ScaledMaxSpeed);
                }
                else
                {
                    newForwardSpeed = Mathf.Max(0f, currentForward - ScaledCoastFriction * Time.fixedDeltaTime);
                    forwardDynamics.Update(newForwardSpeed, Time.fixedDeltaTime);
                }
            }
        }
        else
        {
            reverseTimer = 0f;
            float frictionAmount = ScaledCoastFriction * Time.fixedDeltaTime * 0.3f;
            newForwardSpeed = currentForward > 0f
                ? Mathf.Max(0f, currentForward - frictionAmount)
                : Mathf.Min(0f, currentForward + frictionAmount);
            forwardDynamics.Update(newForwardSpeed, Time.fixedDeltaTime);
        }

        rb.linearVelocity = Forward * newForwardSpeed + lateralVel;

        float totalSpeed = rb.linearVelocity.magnitude;
        float maxTotalSpeed = ScaledMaxSpeed * 1.1f;
        if (totalSpeed > maxTotalSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxTotalSpeed;
        }
    }

    private void ApplyArcSteering()
    {
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, Forward);
        float speed = Mathf.Abs(forwardSpeed);

        float normalizedSpeed = Mathf.Clamp01(speed / ScaledMaxSpeed);
        float curveMultiplier = steeringCurve.Evaluate(normalizedSpeed);

        float targetAngularVelocity = 0f;

        if (Mathf.Abs(inputX) > 0.01f)
        {
            float effectiveRadius = ScaledMinTurnRadius / Mathf.Abs(inputX);

            float angularVelocityRad = speed / effectiveRadius;
            targetAngularVelocity = angularVelocityRad * Mathf.Rad2Deg * curveMultiplier;

            if (inputX > 0f)
                targetAngularVelocity = -targetAngularVelocity;

            if (forwardSpeed < 0f)
                targetAngularVelocity *= -1f;
        }

        currentAngularVelocity = Mathf.Lerp(
            currentAngularVelocity,
            targetAngularVelocity,
            steeringSmoothSpeed * Time.fixedDeltaTime
        );

        rb.angularVelocity = currentAngularVelocity;
    }

    private void ApplyGrip()
    {
        float speed = rb.linearVelocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(speed / ScaledMaxSpeed);

        float forwardSpeed = Vector2.Dot(rb.linearVelocity, Forward);
        Vector2 forwardVelocity = Forward * forwardSpeed;
        Vector2 sideVelocity = rb.linearVelocity - forwardVelocity;
        float targetLateralSpeed = Vector2.Dot(sideVelocity, Right);

        float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, normalizedSpeed);

        bool isSteering = Mathf.Abs(inputX) > 0.1f;
        IsDrifting = isSteering && normalizedSpeed > driftSpeedThreshold;

        if (isSteering)
        {
            float driftFactor = normalizedSpeed > driftSpeedThreshold
                ? driftGripMultiplier
                : Mathf.Lerp(1f, driftGripMultiplier, normalizedSpeed / driftSpeedThreshold);
            grip *= driftFactor;
        }

        float targetLateral = 0f;
        currentLateralSpeed = lateralDynamics.Update(targetLateral, Time.fixedDeltaTime);

        float gripForce = grip * Time.fixedDeltaTime;
        float dampedLateral = Mathf.Lerp(targetLateralSpeed, currentLateralSpeed, gripForce);

        Vector2 newSideVelocity = Right * dampedLateral;
        rb.linearVelocity = forwardVelocity + newSideVelocity;

        DriftIntensity = Mathf.Clamp01(Mathf.Abs(dampedLateral) / 5f);
    }

    private void ApplyAngularDamping()
    {
        rb.angularVelocity = Mathf.Clamp(
            rb.angularVelocity,
            -maxAngularVelocity,
            maxAngularVelocity
        );
    }

    private void UpdateVisual()
    {
        if (visual == null) return;

        float sideVelocity = Vector2.Dot(rb.linearVelocity, Right);
        float targetLean = Mathf.Clamp(-sideVelocity * leanSensitivity, -visualLeanMax, visualLeanMax);

        float currentLean = leanDynamics.Update(targetLean, Time.deltaTime);

        visual.localRotation = Quaternion.Euler(0f, 0f, currentLean);
    }

    private void UpdateDriftTrails()
    {
        float sideVelocity = Mathf.Abs(Vector2.Dot(rb.linearVelocity, Right));
        bool shouldEmit = sideVelocity > driftTrailThreshold && IsDrifting;

        leftTrail.emitting = shouldEmit;
        rightTrail.emitting = shouldEmit;
    }
}
