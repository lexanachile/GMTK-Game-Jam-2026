using UnityEngine;
using UnityEngine.InputSystem;

// МОТОЦИКЛ КИНЕМАТИЧЕСКИЙ — физика верёвки и груза НЕ влияет на мотоцикл.
// Мотоцикл тянет груз за собой через DistanceJoint2D, но груз не тянет мотоцикл назад.
// Все параметры движения работают одинаково с грузом и без него.

[RequireComponent(typeof(Rigidbody2D))]
public class MotorcycleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Rigidbody2D компонента мотоцикла. Используется для управления скоростью и вращением.\n\n" +
             "ВАЖНО: В Awake автоматически устанавливается bodyType = Kinematic.\n" +
             "Это значит что физика верёвки и груза НЕ влияет на мотоцикл.\n" +
             "Мотоцикл двигается через velocity напрямую, игнорируя все внешние силы.")]
    [SerializeField] private Rigidbody2D rb;
    [Tooltip("Визуальный Transform (дочерний объект мотоцикла). Используется для анимации наклона (lean) без влияния на физику.")]
    [SerializeField] private Transform visual;

    [Header("World Scale")]
    [Tooltip(
        "Масштаб мира для адаптации параметров движения под размер спрайта.\n\n" +
        "Если у вас большой спрайт (например, 512x512 пикселей с PPU=100),\n" +
        "единицы Unity представляют меньшее расстояние, и мотоцикл будет казаться медленным.\n\n" +
        "Этот параметр умножает ВСЕ параметры скорости и ускорения:\n" +
        "  - maxSpeed, maxReverseSpeed\n" +
        "  - brakeDeceleration, coastFriction\n" +
        "  - steeringSpeed\n\n" +
        "Как подобрать:\n" +
        "  1.0 = стандартный масштаб (спрайт ~1 единица Unity)\n" +
        "  2.0 = спрайт в 2 раза больше (умножает скорости в 2 раза)\n" +
        "  0.5 = спрайт в 2 раза меньше (делит скорости на 2)\n\n" +
        "Совет: начните с 1.0 и увеличивайте если мотоцикл слишком медленный,\n" +
        "или уменьшайте если слишком быстрый. Это проще чем настраивать каждый параметр отдельно.")]
    [SerializeField] private float worldScale = 1f;

    [Header("Speed Limits")]
    [Tooltip(
        "Максимальная скорость вперёд в единицах/секунду.\n\n" +
        "Мотоцикл никогда не превысит эту скорость при движении вперёд.\n" +
        "Рекомендуемый диапазон: 20–40 для 2D top-down игры.\n" +
        "Увеличьте если мотоцикл кажется медленным, уменьшите если слишком быстрый.")]
    [SerializeField] private float maxSpeed = 30f;
    [Tooltip(
        "Максимальная скорость заднего хода в единицах/секунду.\n\n" +
        "Ограничивает скорость движения назад. Обычно 25–40% от maxSpeed.\n" +
        "Слишком высокая = сложно контролировать на заднем ходу.")]
    [SerializeField] private float maxReverseSpeed = 10f;

    [Header("Acceleration — Target Speed Curve")]
    [Tooltip(
        "Кривая целевой скорости при разгоне.\n\n" +
        "X = нормализованная скорость (0 = стоит, 1 = максимальная скорость)\n" +
        "Y = множитель целевой скорости (0–1+)\n\n" +
        "Как читать этот график:\n" +
        "  Высокий Y при низком X = агрессивный старт, мотоцикл 'рвёт' с места\n" +
        "  Y ≈ 1 на среднем X = мотоцикл уверенно набирает крейсерскую скорость\n" +
        "  Y → 0 при X → 1 = плавное замедление разгона у максимальной скорости\n\n" +
        "Совет: держите Y высоким (0.9–1.0) на первых 70% графика для шустрого разгона,\n" +
        "и резко опускайте к 0 на последних 10–20% для мягкого выхода на максималку.\n\n" +
        "Эта кривая работает ВМЕСТЕ с Second Order Dynamics ниже:\n" +
        "кривая определяет КУДА мотоцикл хочет разогнаться,\n" +
        "а динамика определяет КАК он туда добирается (плавно/резко/с bounce).")]
    [SerializeField] private AnimationCurve accelerationCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f, 1f, 0.5f),
        new Keyframe(0.3f, 0.85f, 0.3f, 0.3f),
        new Keyframe(0.6f, 0.95f, 0f, 0f),
        new Keyframe(0.85f, 0.7f, -0.8f, -0.8f),
        new Keyframe(1f, 0f, -2f, -2f)
    );

    [Header("Acceleration — Second Order Dynamics")]
    [Tooltip(
        "Пружинная система (Second Order Dynamics) для плавного разгона.\n\n" +
        "Основана на концепции из процедурной анимации:\n" +
        "система отслеживает целевую скорость и плавно следует за ней.\n\n" +
        "Три параметра:\n" +
        "  Frequency (f) — скорость отклика. Выше = шустрее разгон.\n" +
        "  Damping (zeta) — затухание. Меньше 1 = небольшой 'bounce'/overshoot (punchy feel).\n" +
        "  Initial Response (r) — начальная реакция. 0 = плавный старт, 1 = мгновенный.\n\n" +
        "Для шустрого мотоцикла: f=3–5, zeta=0.5–0.8, r=0.5–1.5\n" +
        "Для тяжёлого трактора: f=1–2, zeta=1.2–2.0, r=0\n" +
        "Для аркадного карта: f=5–8, zeta=0.3–0.5, r=1.5–2.0\n\n" +
        "Эта система обходит проблему массы — мотоцикл разгоняется одинаково\n" +
        "независимо от веса груза на лассо!")]
    [SerializeField] private SecondOrderDynamics forwardDynamics = new SecondOrderDynamics();

    [Header("Braking")]
    [Tooltip(
        "Сила торможения (единицы/секунду²).\n\n" +
        "Определяет как быстро мотоцикл останавливается при нажатии назад на ходу.\n" +
        "Торможение применяется НАПРЯМУЮ (не через пружину), поэтому мотоцикл\n" +
        "тормозит уверенно и не feels как 'на льду'.\n\n" +
        "Рекомендуемый диапазон: 40–80.\n" +
        "  40–50 = мягкое торможение (как легковая машина)\n" +
        "  50–70 = уверенное торможение (как спортивный байк)\n" +
        "  70–90 = резкое торможение (как ABS)")]
    [SerializeField] private float brakeDeceleration = 65f;
    [Tooltip(
        "Кривая торможения.\n\n" +
        "X = нормализованная скорость (0–1)\n" +
        "Y = множитель силы торможения\n\n" +
        "Как настроить:\n" +
        "  Y > 1 при высоком X = сильнее тормозит на высокой скорости (безопаснее)\n" +
        "  Y ≈ 1 при среднем X = стабильное торможение на крейсерской\n" +
        "  Y < 1 при низком X = мягче торможение перед остановкой (не клюёт носом)\n\n" +
        "Совет: поднимите Y на правом конце для уверенного торможения на скорости.")]
    [SerializeField] private AnimationCurve brakeCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f, 0f, 1f),
        new Keyframe(0.3f, 0.9f, 0.5f, 0.5f),
        new Keyframe(0.7f, 1.1f, 0f, 0f),
        new Keyframe(1f, 1.3f, 0f, 0f)
    );

    [Header("Steering")]
    [Tooltip(
        "Базовая скорость поворота в градусах/секунду.\n\n" +
        "Определяет как быстро мотоцикл вращается при полном отклонении руля.\n" +
        "Рекомендуемый диапазон: 200–400.\n" +
        "  200–250 = спокойный, стабильный поворот\n" +
        "  250–350 = отзывчивый, манёвренный\n" +
        "  350+    = очень резкий, дрифт-ориентированный")]
    [SerializeField] private float steeringSpeed = 300f;
    [Tooltip(
        "Минимальная скорость для включения рулежки (единицы/секунду).\n\n" +
        "Мотоцикл не может поворачивать, если едет медленнее этого значения.\n" +
        "Предотвращает вращение на месте (нереалистично для мотоцикла).\n" +
        "Рекомендуемый диапазон: 2–5.")]
    [SerializeField] private float minSpeedForSteering = 3f;
    [Tooltip(
        "Диапазон плавного включения рулежки выше minSpeedForSteering.\n\n" +
        "Рулежка плавно нарастает от 0 до полной в этом диапазоне скоростей.\n" +
        "Больше = более плавное включение. Меньше = более резкое.\n" +
        "Рекомендуемый диапазон: 2–6.")]
    [SerializeField] private float steeringBlendRange = 4f;
    [Tooltip(
        "Кривая отзывчивости рулежки от скорости.\n\n" +
        "X = нормализованная скорость (0–1)\n" +
        "Y = множитель рулежки\n\n" +
        "Как настроить:\n" +
        "  Низкий Y при низком X = меньше рулежки на малой скорости (реалистично)\n" +
        "  Y = 1 при среднем X = полная рулежка на крейсерской (самое манёвренное)\n" +
        "  Y < 1 при высоком X = меньше рулежки на максимальной (стабильность)\n\n" +
        "Совет: для дрифт-геймплея поднимите Y на высоких скоростях.")]
    [SerializeField] private AnimationCurve steeringCurve = new AnimationCurve(
        new Keyframe(0f, 0.25f, 2.5f, 2.5f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.6f, 0.9f, -0.2f, -0.2f),
        new Keyframe(1f, 0.55f, -0.5f, -0.5f)
    );
    [Tooltip(
        "Замедление при повороте (0–1).\n\n" +
        "Когда мотоцикл рулит, он замедляется. Это помогает грузу успевать за мотоциклом\n" +
        "и предотвращает бесконечное растяжение лассо при резких поворотах.\n\n" +
        "  0.0 = нет замедления (мотоцикл не замедляется при повороте)\n" +
        "  0.2 = слабое замедление (мотоцикл теряет 20% скорости при полном руле)\n" +
        "  0.4 = среднее замедление (мотоцикл теряет 40% скорости)\n" +
        "  0.6 = сильное замедление (мотоцикл теряет 60% скорости — реалистично)\n\n" +
        "Рекомендуемый диапазон: 0.3–0.5.\n" +
        "Если лассо растягивается при повороте — увеличьте это значение.\n" +
        "Если поворот кажется слишком медленным — уменьшите.")]
    [SerializeField, Range(0f, 0.8f)] private float steeringSlowdown = 0.4f;

    [Header("Grip & Drift")]
    [Tooltip(
        "Боковое сцепление на низкой скорости.\n\n" +
        "Выше = мотоцикл меньше скользит боком на малых скоростях.\n" +
        "Очень высокое значение (>20) = ощущение 'рельсов'.\n" +
        "Рекомендуемый диапазон: 12–20.")]
    [SerializeField] private float lowSpeedGrip = 18f;
    [Tooltip(
        "Боковое сцепление на высокой скорости.\n\n" +
        "Ниже = мотоцикл больше заносит на высокой скорости (дрифт).\n" +
        "Разница между lowSpeedGrip и highSpeedGrip определяет\n" +
        "насколько сильно меняется поведение мотоцикла с ростом скорости.\n" +
        "Рекомендуемый диапазон: 3–8.")]
    [SerializeField] private float highSpeedGrip = 4f;
    [Tooltip(
        "Множитель сцепления при дрифте (рулежка на высокой скорости).\n\n" +
        "Применяется когда скорость выше driftSpeedThreshold И игрок рулит.\n" +
        "  0.2–0.3 = сильный занос (как на мокром асфальте)\n" +
        "  0.3–0.5 = лёгкий дрифт (как на грунтовке)\n" +
        "  0.5–0.8 = слабый дрифт (почти как на сухом асфальте)")]
    [SerializeField] private float driftGripMultiplier = 0.35f;
    [Tooltip(
        "Порог нормализованной скорости (0–1) для включения дрифта.\n\n" +
        "Дрифт активируется когда скорость выше этого порога И игрок рулит.\n" +
        "  0.4–0.5 = дрифт начинается рано (легко войти в занос)\n" +
        "  0.6–0.7 = дрифт только на высокой скорости (нужно разогнаться)\n" +
        "  0.8+    = дрифт только на максимальной (редкий, экстремальный)")]
    [SerializeField] private float driftSpeedThreshold = 0.5f;

    [Header("Coast & Friction")]
    [Tooltip(
        "Сила замедления при отпущенном газе (единицы/секунду²).\n\n" +
        "Определяет как быстро мотоцикл замедляется когда игрок ничего не нажимает.\n" +
        "  1–2  = длинный накат (как велосипед на ровной дороге)\n" +
        "  3–5  = средний накат (как машина с отпущенным газом)\n" +
        "  6–10 = быстрое замедление (как engine braking на низкой передаче)")]
    [SerializeField] private float coastFriction = 3.5f;

    [Header("Angular Damping")]
    [Tooltip(
        "Затухание вращения когда НЕ рулим.\n\n" +
        "Как быстро мотоцикл перестаёт вращаться после отпускания руля.\n" +
        "Выше = быстрее выравнивается. Ниже = дольше крутится по инерции.\n" +
        "Рекомендуемый диапазон: 5–12.")]
    [SerializeField] private float angularDamping = 8f;
    [Tooltip(
        "Затухание вращения ВО ВРЕМЯ рулежки.\n\n" +
        "Ниже = более отзывчивый поворот (мотоцикл не 'сопротивляется' рулежке).\n" +
        "Выше = более стабильный, но менее отзывчивый.\n" +
        "Рекомендуемый диапазон: 1–4.")]
    [SerializeField] private float angularDampingWhileSteering = 2f;
    [Tooltip(
        "Максимальная угловая скорость в градусах/секунду.\n\n" +
        "Предотвращает бесконечное вращение мотоцикла.\n" +
        "Рекомендуемый диапазон: 300–500.")]
    [SerializeField] private float maxAngularVelocity = 400f;

    [Header("Reverse")]
    [Tooltip(
        "Задержка в секундах перед включением заднего хода.\n\n" +
        "Защищает от случайного включения заднего хода при быстром торможении.\n" +
        "  0.2–0.3 = короткая (отзывчивый задний ход)\n" +
        "  0.4–0.6 = средняя (комфортный)\n" +
        "  0.7+    = длинная (нужно уверенно держать назад)")]
    [SerializeField] private float reverseDelay = 0.3f;

    [Header("Visual — Lean Animation")]
    [Tooltip(
        "Максимальный угол наклона визуала в градусах.\n\n" +
        "Определяет насколько сильно мотоцикл наклоняется при повороте/дрифте.\n" +
        "  15–20 = лёгкий наклон (реалистичный)\n" +
        "  20–30 = заметный наклон (аркадный, выразительный)\n" +
        "  30+   = экстремальный наклон (стилизированный)")]
    [SerializeField] private float visualLeanMax = 25f;
    [Tooltip(
        "Чувствительность наклона к боковой скорости.\n\n" +
        "Множитель, определяющий насколько сильно боковая скорость влияет на наклон.\n" +
        "Выше = больше наклон при том же заносе. Ниже = меньше.\n" +
        "Рекомендуемый диапазон: 1.5–4.0.")]
    [SerializeField] private float leanSensitivity = 2.5f;
    [Tooltip(
        "Пружинная система (Second Order Dynamics) для анимации наклона.\n\n" +
        "Управляет тем, КАК мотоцикл наклоняется:\n" +
        "  Frequency = скорость наклона\n" +
        "  Damping < 1 = наклон 'проскакивает' и возвращается (bounce)\n" +
        "  Initial Response < 0 = anticipation: наклоняется в ПРОТИВОПОЛОЖНУЮ сторону\n" +
        "                         перед тем как наклониться в поворот (juicy feel!)\n\n" +
        "Рекомендуемые настройки для сочного наклона:\n" +
        "  f=3, zeta=0.5, r=-1 (bounce + anticipation)\n" +
        "  f=4, zeta=0.7, r=0 (быстрый, без bounce)\n" +
        "  f=2, zeta=0.3, r=-2 (медленный, сильный anticipation)")]
    [SerializeField] private SecondOrderDynamics leanDynamics = new SecondOrderDynamics();

    private PlayerControls controls;
    private float inputX;
    private float inputY;
    private float reverseTimer;

    private Vector2 Forward => (Vector2)transform.up;
    private Vector2 Right => (Vector2)transform.right;

    private float ScaledMaxSpeed => maxSpeed * worldScale;
    private float ScaledMaxReverseSpeed => maxReverseSpeed * worldScale;
    private float ScaledBrakeDeceleration => brakeDeceleration * worldScale;
    private float ScaledCoastFriction => coastFriction * worldScale;
    private float ScaledSteeringSpeed => steeringSpeed * worldScale;

    public float Speed => rb.linearVelocity.magnitude;
    public float ForwardSpeed => Vector2.Dot(rb.linearVelocity, Forward);
    public float NormalizedSpeed => Mathf.Clamp01(Speed / ScaledMaxSpeed);
    public bool IsDrifting { get; private set; }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
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
        leanDynamics.Reset(0f);
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
    }

    private void FixedUpdate()
    {
        HandleForwardMovement();
        ApplySteering();
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
            
            // Steering slowdown: уменьшаем целевую скорость при рулежке
            // Это помогает грузу успевать за мотоциклом и предотвращает растяжение лассо
            float steeringFactor = 1f - Mathf.Abs(inputX) * steeringSlowdown;
            targetSpeed *= steeringFactor;
            
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
                forwardDynamics.Reset(newForwardSpeed);
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
                    forwardDynamics.Reset(newForwardSpeed);
                }
            }
        }
        else
        {
            reverseTimer = 0f;
            newForwardSpeed = Mathf.Max(0f, currentForward - ScaledCoastFriction * Time.fixedDeltaTime);
            forwardDynamics.Reset(newForwardSpeed);
        }

        rb.linearVelocity = Forward * newForwardSpeed + lateralVel;
    }

    private void ApplySteering()
    {
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, Forward);
        float speed = Mathf.Abs(forwardSpeed);

        if (speed < minSpeedForSteering)
        {
            rb.angularVelocity = 0f;
            return;
        }

        float blendFactor = Mathf.Clamp01((speed - minSpeedForSteering) / steeringBlendRange);
        float normalizedSpeed = Mathf.Clamp01(speed / ScaledMaxSpeed);
        float curveMultiplier = steeringCurve.Evaluate(normalizedSpeed);

        float targetAngularVelocity = -inputX * ScaledSteeringSpeed * curveMultiplier * blendFactor;

        if (forwardSpeed < 0f)
            targetAngularVelocity *= -1f;

        rb.angularVelocity = targetAngularVelocity;
    }

    private void ApplyGrip()
    {
        float speed = rb.linearVelocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(speed / ScaledMaxSpeed);

        float forwardSpeed = Vector2.Dot(rb.linearVelocity, Forward);
        Vector2 forwardVelocity = Forward * forwardSpeed;
        Vector2 sideVelocity = rb.linearVelocity - forwardVelocity;

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

        sideVelocity = Vector2.Lerp(sideVelocity, Vector2.zero, grip * Time.fixedDeltaTime);
        rb.linearVelocity = forwardVelocity + sideVelocity;
    }

    private void ApplyAngularDamping()
    {
        bool isSteering = Mathf.Abs(inputX) > 0.1f;
        float dampingRate = isSteering ? angularDampingWhileSteering : angularDamping;

        if (!isSteering && dampingRate > 0f)
        {
            rb.angularVelocity = Mathf.Lerp(
                rb.angularVelocity,
                0f,
                dampingRate * Time.fixedDeltaTime
            );
        }

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
}
