using UnityEngine;

public class CargoController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [Header("Damping")]
    [Tooltip("Lateral velocity damping")]
    [SerializeField] private float lateralDamping = 2f;
    [Tooltip("Angular velocity damping")]
    [SerializeField] private float angularDamping = 6f;

    [Header("Rotation")]
    [Tooltip("How fast cargo aligns with movement direction")]
    [SerializeField] private float rotationSpeed = 5f;
    [Tooltip("Minimum velocity to apply rotation")]
    [SerializeField] private float minVelocityForRotation = 0.1f;

    private void FixedUpdate()
    {
        ApplyLateralDamping();
        ApplyAngularDamping();
        ApplyRotation();
    }

    private void ApplyLateralDamping()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f) return;

        Vector2 forward = rb.linearVelocity.normalized;
        float forwardComponent = Vector2.Dot(rb.linearVelocity, forward);
        Vector2 lateralVelocity = rb.linearVelocity - forward * forwardComponent;

        lateralVelocity = Vector2.Lerp(lateralVelocity, Vector2.zero, lateralDamping * Time.fixedDeltaTime);
        rb.linearVelocity = forward * forwardComponent + lateralVelocity;
    }

    private void ApplyAngularDamping()
    {
        rb.angularVelocity = Mathf.Lerp(
            rb.angularVelocity,
            0f,
            angularDamping * Time.fixedDeltaTime
        );
    }

    private void ApplyRotation()
    {
        if (rb.linearVelocity.sqrMagnitude < minVelocityForRotation * minVelocityForRotation)
            return;

        float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, angle - 90f, rotationSpeed * Time.fixedDeltaTime));
    }
}
