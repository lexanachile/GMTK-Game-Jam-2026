using UnityEngine;

public class CargoController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [Header("References")]
    [Tooltip("Transform мотоцикла (или точка привязки верёвки на мотоцикле).\n" +
             "Используется для определения скорости мотоцикла и натяжения верёвки.")]
    [SerializeField] private Transform bikeTransform;

    [Header("Forward Friction")]
    [Tooltip("Базовое трение — замедляет груз когда нет внешних сил.")]
    [SerializeField] private float forwardFriction = 1.5f;
    [Tooltip("Множитель трения на высокой скорости (1 = одинаковое, 2 = двойное на максимуме).")]
    [SerializeField] private float highSpeedFrictionMultiplier = 1.5f;

    [Header("Braking")]
    [Tooltip("Базовая сила торможения (ед/с²).")]
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

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
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

    private void FixedUpdate()
    {
        float normalizedSpeed = Mathf.Clamp01(rb.linearVelocity.magnitude / maxCargoSpeed);
        float bikeSpeed = GetBikeSpeed();
        float bikeAccel = (bikeSpeed - prevBikeSpeed) / Time.fixedDeltaTime;
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
        if (bikeTransform == null) return 0f;
        Rigidbody2D bikeRb = bikeTransform.GetComponentInParent<Rigidbody2D>();
        if (bikeRb == null) return 0f;
        return bikeRb.linearVelocity.magnitude;
    }

    private float GetRopeTension()
    {
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

        float curveValue = brakeCurve.Evaluate(normalizedSpeed);
        float tension = GetRopeTension();
        float tensionMultiplier = Mathf.Lerp(1f, ropeTensionBrakeMultiplier, tension);

        float brakeAmount = brakeDeceleration * curveValue * tensionMultiplier * Time.fixedDeltaTime;
        brakeAmount = Mathf.Min(brakeAmount, speedDiff);

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

        Vector2 forward = rb.linearVelocity.normalized;
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

        Vector2 toBike = ((Vector2)bikeTransform.position - rb.position).normalized;
        Vector2 lateralDir = new Vector2(-toBike.y, toBike.x);
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
        if (rb.linearVelocity.sqrMagnitude < minVelocityForRotation * minVelocityForRotation)
            return;

        float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
        float speedMultiplier = rotationSpeedCurve.Evaluate(normalizedSpeed);
        float effectiveSpeed = rotationSpeed * speedMultiplier;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, angle - 90f, effectiveSpeed * Time.fixedDeltaTime));
    }
}
