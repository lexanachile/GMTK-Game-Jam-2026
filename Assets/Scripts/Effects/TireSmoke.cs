using UnityEngine;

public class TireSmoke : MonoBehaviour
{
    [SerializeField] ParticleSystem smoke;

    [SerializeField] Rigidbody2D rb;

    public float minSpeed = 5f;

    public float driftAngle = 25f;

    void Update()
    {
        float speed = rb.linearVelocity.magnitude;

        float angle = Vector2.Angle(
            rb.linearVelocity.normalized,
            transform.up);

        bool drifting =
            speed > minSpeed &&
            angle > driftAngle;

        var emission = smoke.emission;
        emission.enabled = drifting;
    }
}