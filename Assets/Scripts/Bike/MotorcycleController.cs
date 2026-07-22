using UnityEngine;
using UnityEngine.InputSystem;

public class MotorcycleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform visual;

    [Header("Engine")]
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float reverseAcceleration = 18f;

    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float maxReverseSpeed = 6f;

    [Header("Brakes")]
    [SerializeField] private float brakeForce = 100f;
    [SerializeField] private float rollingResistance = 1.3f;

    [Header("Steering")]
    [SerializeField] private float steeringPower = 260f;
    [SerializeField] private float angularDamping = 4f;
    [SerializeField] private float maxAngularVelocity = 220f;

    [Header("Grip")]
    [SerializeField] private float lowSpeedGrip = 14f;
    [SerializeField] private float highSpeedGrip = 1.6f;
    [SerializeField] private float steeringDriftMultiplier = 0.45f;

    [Header("Visual")]
    [SerializeField] private float visualLean = 25f;
    [SerializeField] private float visualLeanSpeed = 8f;

    [Header("Reverse")]
    [SerializeField] private float reverseDelay = 0.45f;

    private float inputX;
    private float inputY;

    private float reverseTimer;

    private Vector2 Forward => (Vector2)transform.up;
    private Vector2 Right => (Vector2)transform.right;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        ReadInput();
        UpdateVisual();
    }

    private void FixedUpdate()
    {
        ApplyEngine();
        ApplySteering();
        ApplyGrip();
        ApplyRollingResistance();
        ApplyAngularDamping();
        ClampSpeed();
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;

        if (kb == null)
            return;

        inputX = 0;
        inputY = 0;

        if (kb.aKey.isPressed)
            inputX = -1f;

        if (kb.dKey.isPressed)
            inputX = 1f;

        if (kb.wKey.isPressed)
            inputY = 1f;

        if (kb.sKey.isPressed)
            inputY = -1f;
    }

    private void ApplyEngine()
    {
        float forwardSpeed =
            Vector2.Dot(
                rb.linearVelocity,
                Forward
            );

        if (inputY > 0f)
        {
            reverseTimer = 0f;

            if (forwardSpeed < maxSpeed)
            {
                rb.AddForce(
                    Forward * acceleration,
                    ForceMode2D.Force
                );
            }
        }
        else if (inputY < 0f)
        {
            if (forwardSpeed > 0.75f)
            {
                rb.AddForce(
                    -Forward * brakeForce,
                    ForceMode2D.Force
                );

                reverseTimer = 0f;
            }
            else
            {
                reverseTimer += Time.fixedDeltaTime;

                if (reverseTimer >= reverseDelay)
                {
                    if (forwardSpeed > -maxReverseSpeed)
                    {
                        rb.AddForce(
                            -Forward * reverseAcceleration,
                            ForceMode2D.Force
                        );
                    }
                }
            }
        }
        else
        {
            reverseTimer = 0f;
        }
    }

    private void ApplySteering()
    {
        float forwardSpeed =
            Vector2.Dot(
                rb.linearVelocity,
                Forward
            );

        float speed =
            Mathf.Abs(forwardSpeed);

        if (speed < 0.05f)
            return;

        float speedPercent =
            Mathf.Clamp01(
                speed / maxSpeed
            );

        float steeringMultiplier =
            Mathf.Lerp(
                0.35f,
                1f,
                speedPercent
            );

        float reverseMultiplier =
            forwardSpeed < 0f
                ? -1f
                : 1f;

        float torque =
            -inputX *
            steeringPower *
            steeringMultiplier *
            reverseMultiplier;

        rb.AddTorque(
            torque,
            ForceMode2D.Force
        );
    }

    private void ApplyGrip()
    {
        float forwardVelocity =
            Vector2.Dot(
                rb.linearVelocity,
                Forward
            );

        float sideVelocity =
            Vector2.Dot(
                rb.linearVelocity,
                Right
            );

        float speedPercent =
            Mathf.Clamp01(
                rb.linearVelocity.magnitude /
                maxSpeed
            );

        float grip =
            Mathf.Lerp(
                lowSpeedGrip,
                highSpeedGrip,
                speedPercent
            );

        grip *= Mathf.Lerp(
            1f,
            steeringDriftMultiplier,
            Mathf.Abs(inputX)
        );

        sideVelocity =
            Mathf.Lerp(
                sideVelocity,
                0f,
                grip *
                Time.fixedDeltaTime
            );

        rb.linearVelocity =
            Forward * forwardVelocity +
            Right * sideVelocity;
    }

    private void ApplyRollingResistance()
    {
        if (inputY != 0)
            return;

        rb.AddForce(
            -rb.linearVelocity *
            rollingResistance,
            ForceMode2D.Force
        );
    }

    private void ApplyAngularDamping()
    {
        rb.angularVelocity =
            Mathf.Lerp(
                rb.angularVelocity,
                0f,
                angularDamping *
                Time.fixedDeltaTime
            );

        rb.angularVelocity =
            Mathf.Clamp(
                rb.angularVelocity,
                -maxAngularVelocity,
                maxAngularVelocity
            );
    }

    private void ClampSpeed()
    {
        float forwardSpeed =
            Vector2.Dot(
                rb.linearVelocity,
                Forward
            );

        Vector2 sideVelocity =
            rb.linearVelocity -
            Forward * forwardSpeed;

        if (forwardSpeed > maxSpeed)
        {
            rb.linearVelocity =
                Forward * maxSpeed +
                sideVelocity;
        }

        if (forwardSpeed < -maxReverseSpeed)
        {
            rb.linearVelocity =
                -Forward * maxReverseSpeed +
                sideVelocity;
        }
    }

    private void UpdateVisual()
    {
        if (visual == null)
            return;

        float sideVelocity =
            Vector2.Dot(
                rb.linearVelocity,
                Right
            );

        float lean =
            Mathf.Clamp(
                -sideVelocity * 2.5f,
                -visualLean,
                visualLean
            );

        Quaternion target =
            Quaternion.Euler(
                0f,
                0f,
                lean
            );

        visual.localRotation =
            Quaternion.Lerp(
                visual.localRotation,
                target,
                visualLeanSpeed *
                Time.deltaTime
            );
    }

    public float Speed =>
        rb.linearVelocity.magnitude;

    public float ForwardSpeed =>
        Vector2.Dot(
            rb.linearVelocity,
            Forward
        );
}