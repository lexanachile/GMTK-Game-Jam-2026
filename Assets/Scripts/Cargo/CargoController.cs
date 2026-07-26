using UnityEngine;

public class CargoController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [Header("References")]
    [Tooltip("Transform мотоцикла (или точка привязки верёвки на мотоцикле).\n" +
             "Используется для определения скорости мотоцикла и натяжения верёвки.")]
    [SerializeField] private Transform bikeTransform;
    [Tooltip("Опционально: LassoRope для tension по path (с учётом wrap). Если пусто — ищется в сцене.")]
    [SerializeField] private LassoRope lassoRope;

    [Header("Forward Friction")]
    [Tooltip("Базовое трение — замедляет груз когда нет внешних сил.")]
    [SerializeField] private float forwardFriction = 1.5f;
    [Tooltip("Множитель трения на высокой скорости (1 = одинаковое, 2 = двойное на максимуме).")]
    [SerializeField] private float highSpeedFrictionMultiplier = 1.5f;

    [Header("Inertia")]
    [Tooltip("Насколько коробка сохраняет импульс, пока верёвка не натянута.\n" +
             "  0 = сразу подстраивается под скорость байка (старое поведение)\n" +
             "  1 = едет по инерции; при торможении байка перелетает и стопорится об верёвку")]
    [SerializeField, Range(0f, 1f)] private float inertia = 1f;
    [Tooltip("С какой натяжки (0-1) включается торможение «в ногу» с байком.\n" +
             "Ниже порога при inertia>0 скорость почти не режется — только трение и LassoRope.")]
    [SerializeField, Range(0f, 1f)] private float brakeEngageTension = 0.75f;

    [Header("Braking")]
    [Tooltip("Базовая сила торможения (ед/с²) когда inertia низкая или верёвка натянута.")]
    [SerializeField] private float brakeDeceleration = 40f;
    [Tooltip("X: нормализованная скорость груза (0-1), Y: множитель силы торможения.\n" +
             "  Y > 1 при высокой скорости = сильнее торможение на скорости\n" +
             "  Y < 1 при низкой = мягкое торможение при малой скорости")]
    [SerializeField] private AnimationCurve brakeCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f, 0f, 2f),
        new Keyframe(0.3f, 0.7f, 1f, 1f),
        new Keyframe(0.7f, 1.0f, 0f, 0f),
        new Keyframe(1f, 1.4f, 0f, 0f)
    );
    [Tooltip("Минимальная разница скоростей (груз - мотоцикл) для начала торможения.")]
    [SerializeField] private float brakeSpeedThreshold = 1f;
    [Tooltip("Максимальная скорость груза для нормализации (ед/с).")]
    [SerializeField] private float maxCargoSpeed = 30f;

    [Header("Rope Tension Braking")]
    [Tooltip("Множитель торможения при полностью натянутой верёвке.")]
    [SerializeField] private float ropeTensionBrakeMultiplier = 2f;
    [Tooltip("Расстояние, на котором верёвка считается полностью натянутой (0 = авто из начальной позиции).")]
    [SerializeField] private float ropeTensionDistance = 0f;

    [Header("Lateral Damping")]
    [Tooltip("Базовое боковое демпфирование.")]
    [SerializeField] private float lateralDamping = 2f;
    [Tooltip("X: нормализованная скорость (0-1), Y: множитель бокового демпфирования.\n" +
             "Выше Y на высокой скорости = груз стабильнее.")]
    [SerializeField] private AnimationCurve lateralDampingCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f, 0f, 1f),
        new Keyframe(0.4f, 1.0f, 0f, 0f),
        new Keyframe(1f, 1.8f, 0f, 0f)
    );

    [Header("Angular Damping")]
    [Tooltip("Демпфирование угловой скорости.")]
    [SerializeField] private float angularDamping = 6f;

    [Header("Rotation")]
    [Tooltip("Скорость выравнивания по направлению движения.")]
    [SerializeField] private float rotationSpeed = 5f;
    [Tooltip("Минимальная скорость для поворота.")]
    [SerializeField] private float minVelocityForRotation = 0.1f;
    [Tooltip("X: нормализованная скорость (0-1), Y: множитель скорости поворота.\n" +
             "Ниже Y на высокой скорости = груз плавнее поворачивает.")]
    [SerializeField] private AnimationCurve rotationSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1.5f, 0f, -1f),
        new Keyframe(0.5f, 1.0f, -0.5f, -0.5f),
        new Keyframe(1f, 0.5f, -0.3f, 0f)
    );

    [Header("Sway (Inertia Oscillation)")]
    [Tooltip("Сила раскачки при резком торможении/ускорении мотоцикла.")]
    [SerializeField] private float swayForce = 3f;
    [Tooltip("Затухание раскачки.")]
    [SerializeField] private float swayDamping = 4f;

    private float ropeBaseDistance;
    private float prevBikeSpeed;
    private float swayVelocity;
    private Rigidbody2D bikeRb;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        ResolveBikeReference();
    }

    private void Start()
    {
        ResolveBikeReference();

        if (bikeTransform != null && ropeTensionDistance <= 0f)
        {
            ropeBaseDistance = Vector2.Distance(rb.position, bikeTransform.position) * 1.3f;
        }
        else if (ropeTensionDistance > 0f)
        {
            ropeBaseDistance = ropeTensionDistance;
        }

        prevBikeSpeed = 0f;
        swayVelocity = 0f;
    }

    private void ResolveBikeReference()
    {
        if (bikeTransform == null)
        {
            var bike = FindFirstObjectByType<MotorcycleController>();
            if (bike != null)
                bikeTransform = bike.transform;
        }

        if (bikeTransform != null)
            bikeRb = bikeTransform.GetComponentInParent<Rigidbody2D>();

        if (lassoRope == null)
            lassoRope = FindFirstObjectByType<LassoRope>();
    }

    private void FixedUpdate()
    {
        float normalizedSpeed = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(maxCargoSpeed, 0.01f));
        float bikeSpeed = GetBikeSpeed();
        float dt = Time.fixedDeltaTime;
        float bikeAccel = dt > 0f ? (bikeSpeed - prevBikeSpeed) / dt : 0f;
        prevBikeSpeed = bikeSpeed;

        ApplyForwardBraking(normalizedSpeed, bikeSpeed);
        ApplyForwardFriction(normalizedSpeed);
        ApplyLateralDamping(normalizedSpeed);
        ApplySway(bikeAccel);
        ApplyAngularDamping();
        ApplyRotation(normalizedSpeed);
    }

    private float GetBikeSpeed()
    {
        if (bikeRb == null)
            return 0f;
        return bikeRb.linearVelocity.magnitude;
    }

    /// <summary>
    /// Direction cargo should face when taut: along last rope segment toward anchor/wrap pin.
    /// </summary>
    private Vector2 GetRopePullDirection()
    {
        if (lassoRope != null && lassoRope.TryGetCargoPullDirection(out Vector2 dir))
            return dir;

        if (bikeTransform == null)
            return Vector2.up;

        Vector2 toBike = (Vector2)bikeTransform.position - rb.position;
        return toBike.sqrMagnitude > 0.0001f ? toBike.normalized : Vector2.up;
    }

    private float GetRopeTension()
    {
        // Path-based tension (wrap-aware) — matches LassoRope gameplay constraint
        if (lassoRope != null)
            return Mathf.Clamp01(lassoRope.Tension);

        if (bikeTransform == null || ropeBaseDistance <= 0f) return 0f;
        float dist = Vector2.Distance(rb.position, bikeTransform.position);
        return Mathf.Clamp01(dist / ropeBaseDistance);
    }

    private void ApplyForwardBraking(float normalizedSpeed, float bikeSpeed)
    {
        float cargoSpeed = rb.linearVelocity.magnitude;
        if (cargoSpeed < 0.01f) return;

        float speedDiff = cargoSpeed - bikeSpeed;
        if (speedDiff < brakeSpeedThreshold) return;

        float tension = GetRopeTension();
        // inertia=1 + slack rope → no speed-match brake (cargo overshoots, rope stops it)
        // inertia=0 → always brake like before
        float tensionEngage = Mathf.InverseLerp(brakeEngageTension, 1f, tension);
        float brakeAuthority = Mathf.Lerp(1f, tensionEngage, inertia);
        if (brakeAuthority <= 0.001f) return;

        // Ключевое: на буксире верёвка всегда натянута (tension≈1), поэтому gate
        // только по натяжке не работает — тормоз не даёт коробке начать сближение.
        // При inertia=1 тормозим только компоненту скорости, растягивающую верёвку
        // (полёт ОТ якоря). Импульс, несущий коробку К байку/мимо него, сохраняется:
        // байк тормозит → коробка перелетает → стопорится об верёвку (LassoRope).
        Vector2 pullDir = GetRopePullDirection();
        float outwardSpeed = Vector2.Dot(rb.linearVelocity, -pullDir);
        float brakeCap = Mathf.Lerp(speedDiff, Mathf.Max(outwardSpeed, 0f), inertia);

        float curveValue = brakeCurve.Evaluate(normalizedSpeed);
        float tensionMultiplier = Mathf.Lerp(1f, ropeTensionBrakeMultiplier, tension);

        float brakeAmount = brakeDeceleration * curveValue * tensionMultiplier
            * brakeAuthority * Time.fixedDeltaTime;
        brakeAmount = Mathf.Min(brakeAmount, brakeCap);
        if (brakeAmount <= 0f) return;

        Vector2 brakeDir = -rb.linearVelocity.normalized;
        rb.linearVelocity += brakeDir * brakeAmount;
    }

    private void ApplyForwardFriction(float normalizedSpeed)
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.01f) return;

        float speedMultiplier = Mathf.Lerp(1f, highSpeedFrictionMultiplier, normalizedSpeed);
        float frictionAmount = forwardFriction * speedMultiplier * Time.fixedDeltaTime;
        frictionAmount = Mathf.Min(frictionAmount, speed);

        rb.linearVelocity -= rb.linearVelocity.normalized * frictionAmount;
    }

    private void ApplyLateralDamping(float normalizedSpeed)
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f) return;

        // Dampen sideways relative to cargo facing — not velocity dir (that made lateral always 0)
        Vector2 forward = (Vector2)transform.up;
        float forwardComponent = Vector2.Dot(rb.linearVelocity, forward);
        Vector2 lateralVelocity = rb.linearVelocity - forward * forwardComponent;

        float curveMultiplier = lateralDampingCurve.Evaluate(normalizedSpeed);
        float damping = lateralDamping * curveMultiplier;

        lateralVelocity = Vector2.Lerp(lateralVelocity, Vector2.zero, damping * Time.fixedDeltaTime);
        rb.linearVelocity = forward * forwardComponent + lateralVelocity;
    }

    private void ApplySway(float bikeAccel)
    {
        if (bikeTransform == null || swayForce <= 0f) return;

        float accelImpulse = -bikeAccel * swayForce * Time.fixedDeltaTime;
        swayVelocity += accelImpulse;
        swayVelocity = Mathf.Lerp(swayVelocity, 0f, swayDamping * Time.fixedDeltaTime);

        if (Mathf.Abs(swayVelocity) < 0.001f) return;

        Vector2 alongRope = GetRopePullDirection();
        Vector2 lateralDir = new Vector2(-alongRope.y, alongRope.x);
        rb.linearVelocity += lateralDir * swayVelocity * Time.fixedDeltaTime;
    }

    private void ApplyAngularDamping()
    {
        rb.angularVelocity = Mathf.Lerp(
            rb.angularVelocity,
            0f,
            angularDamping * Time.fixedDeltaTime
        );
    }

    private void ApplyRotation(float normalizedSpeed)
    {
        float tension = GetRopeTension();
        bool hasVelocity = rb.linearVelocity.sqrMagnitude >= minVelocityForRotation * minVelocityForRotation;

        float targetAngle;

        if (tension > 0.7f && bikeTransform != null)
        {
            // Pull direction along last rope segment (wrap-aware), not straight through walls
            Vector2 pullDir = GetRopePullDirection();
            float ropeAngle = Mathf.Atan2(pullDir.y, pullDir.x) * Mathf.Rad2Deg;

            if (hasVelocity)
            {
                float velAngle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                float blendFactor = Mathf.Clamp01((tension - 0.7f) / 0.3f);
                targetAngle = Mathf.LerpAngle(velAngle, ropeAngle, blendFactor) - 90f;
            }
            else
            {
                targetAngle = ropeAngle - 90f;
            }
        }
        else if (hasVelocity)
        {
            targetAngle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg - 90f;
        }
        else
        {
            return;
        }

        float speedMultiplier = rotationSpeedCurve.Evaluate(normalizedSpeed);
        float tensionBoost = 1f + tension * 0.5f;
        float effectiveSpeed = rotationSpeed * speedMultiplier * tensionBoost;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, effectiveSpeed * Time.fixedDeltaTime));
    }
}
