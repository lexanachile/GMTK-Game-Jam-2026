using UnityEngine;

public class CargoController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;

    [SerializeField] private Transform hitch;

    [SerializeField] private float ropeLength = 3f;

    [SerializeField] private float ropeStrength = 45f;

    [SerializeField] private float damping = 2f;

    [SerializeField] private float angularDamping = 6f;

    [SerializeField] private float maxDistance = 4f;

    private void FixedUpdate()
    {
        Vector2 target =
            hitch.position;

        Vector2 toTarget =
            target - rb.position;

        float distance =
            toTarget.magnitude;

        if (distance > ropeLength)
        {
            float stretch =
                distance - ropeLength;

            rb.AddForce(
                toTarget.normalized *
                stretch *
                ropeStrength,
                ForceMode2D.Force
            );
        }

        rb.linearVelocity *=
            1f -
            damping *
            Time.fixedDeltaTime;

        rb.angularVelocity =
            Mathf.Lerp(
                rb.angularVelocity,
                0,
                angularDamping *
                Time.fixedDeltaTime
            );

        if (distance > maxDistance)
        {
            rb.position =
                target -
                toTarget.normalized *
                ropeLength;

            rb.linearVelocity *= 0.5f;
        }

        RotateCargo();
    }

    private void RotateCargo()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.05f)
            return;

        float angle =
            Mathf.Atan2(
                rb.linearVelocity.y,
                rb.linearVelocity.x
            ) * Mathf.Rad2Deg;

        rb.MoveRotation(
            Mathf.LerpAngle(
                rb.rotation,
                angle - 90f,
                5f * Time.fixedDeltaTime
            )
        );
    }
}