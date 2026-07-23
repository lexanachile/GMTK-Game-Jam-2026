using UnityEngine;

[System.Serializable]
public class SecondOrderDynamics
{
    [Tooltip(
        "Natural frequency (Hz) — how fast the system responds.\n\n" +
        "Controls the SPEED of the motion without changing its shape.\n" +
        "Higher = snappier, reaches target faster. Lower = sluggish, takes longer.\n" +
        "Typical range: 1–10 Hz.\n" +
        "WARNING: Very high values relative to frame rate may cause instability.\n\n" +
        "Examples:\n" +
        "  1–2 Hz  = slow, heavy feel (large truck)\n" +
        "  2–3 Hz  = smooth, controlled (comfortable acceleration)\n" +
        "  3–5 Hz  = snappy, responsive (sporty bike)\n" +
        "  6–10 Hz = very fast, almost instant (twitchy arcade)")]
    public float frequency = 2.5f;

    [Tooltip(
        "Damping coefficient (zeta) — how the system settles at the target.\n\n" +
        "Controls the SETTLING BEHAVIOR of the motion:\n" +
        "  0.0     = undamped: oscillates forever, never settles\n" +
        "  0.0–1.0 = underdamped: bounces/overshoots before settling (punchy feel)\n" +
        "  1.0     = critically damped: fastest settle without overshoot (Unity SmoothDamp)\n" +
        "  >1.0    = overdamped: slowly creeps to target, no bounce (heavy/safe feel)\n\n" +
        "For smooth motorcycle acceleration, try 0.8–1.0.\n" +
        "For punchy feel with some bounce, try 0.5–0.7.\n" +
        "For firm braking settle, try 1.0–1.5.\n" +
        "For bouncy visual effects, try 0.3–0.6.")]
    public float damping = 0.8f;

    [Tooltip(
        "Initial response (r) — how the system reacts at the START of movement.\n\n" +
        "Controls the INITIAL REACTION to input changes:\n" +
        "  0   = slow acceleration from rest (gradual start)\n" +
        "  1   = immediate reaction to input\n" +
        "  >1  = overshoots the target initially (aggressive kick)\n" +
        "  2   = typical mechanical connection (springy)\n" +
        "  <0  = anticipates: briefly moves OPPOSITE before going to target\n\n" +
        "For smooth vehicle acceleration: 0–0.5 (gradual start).\n" +
        "For punchy acceleration: 0.5–1.0 (quick response).\n" +
        "For visual lean with anticipation: -1 to -2.\n" +
        "For snappy steering response: 1–2.")]
    public float initialResponse = 0.3f;

    [Tooltip(
        "Maximum rate of change of the output per second (0 = unlimited).\n\n" +
        "Limits how fast the output value can change. Prevents extreme velocity spikes\n" +
        "when the target changes suddenly (e.g., slamming the throttle from 0 to full).\n\n" +
        "Without this limit, high frequency + sudden input = huge internal velocity\n" +
        "that causes the output to overshoot wildly or 'stick' at clamped bounds.\n\n" +
        "For forward speed: set to maxSpeed * 6–10 for snappy but controlled acceleration.\n" +
        "For visual lean: set to 200–400 for fast but smooth tilting.\n" +
        "Set to 0 to disable (use only if you know the target changes gradually).")]
    public float maxVelocity = 200f;

    private float _xp;
    private float _y;
    private float _yd;
    private bool _initialized;

    public float Value => _y;
    public float Velocity => _yd;

    public void Reset(float value)
    {
        _xp = value;
        _y = value;
        _yd = 0f;
        _initialized = true;
    }

    public float Update(float target, float dt)
    {
        if (!_initialized)
        {
            Reset(target);
            return _y;
        }

        if (dt <= 0f)
            return _y;

        float omega = 2f * Mathf.PI * frequency;
        float k1 = 2f * damping / omega;
        float k2 = 1f / (omega * omega);
        float k3 = initialResponse / omega;

        float xd = (target - _xp) / dt;
        _xp = target;

        float k2Stable = (dt * dt + 2f * dt * k1) / 4f;
        float k2Used = Mathf.Max(k2, k2Stable);

        _y += dt * _yd;
        _yd += dt * (target + k3 * xd - _y - k1 * _yd) / k2Used;

        if (maxVelocity > 0f)
            _yd = Mathf.Clamp(_yd, -maxVelocity, maxVelocity);

        return _y;
    }
}
