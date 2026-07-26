using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Level 3 rope:
/// - Gameplay: one-way max-distance constraint (pulls cargo only, never slows the bike)
/// - Corner wrap (gameplay): rope path bends around obstacles via raycasts
/// - Visual: ONE always-on Verlet chain. No modes, no resets, no teleports.
///   * FIXED material rest length (= maxRopeLength) — never shrinks with path → no spring snap
///   * Extension-only constraints — slack folds/overlaps freely
///   * Ground sleep — laid segments (esp. cargo end) stay put until real tension arrives
///   * Bike-end slack injection on reverse — new coils stack on top of old ones
///   * Walls: penetration-only push (no outside magnetic shell)
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LassoRope : MonoBehaviour
{
    [Header("Connection Points")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Rope Length")]
    [Tooltip("Multiplier for rope length (>1 = longer slack rope)")]
    [SerializeField] private float ropeLengthMultiplier = 1.5f;
    [Tooltip("Absolute max rope length (0 = auto from start distance * multiplier)")]
    [SerializeField] private float cargoMaxDistance = 0f;

    [Header("Cargo Pull (one-way — bike is never pulled)")]
    [Tooltip("Отключить физическую коллизию байк↔коробка. Без этого коробка упирается в байк\n" +
             "(его масса намного больше) и не может перелететь его по инерции.")]
    [SerializeField] private bool ignoreBikeCargoCollision = true;
    [Tooltip("How hard cargo is reeled in when over max path length")]
    [SerializeField] private float stretchForceBase = 150f;
    [SerializeField, Range(1f, 4f)] private float stretchExponent = 2.5f;
    [Tooltip("Hard position correction strength when far over limit (0-1)")]
    [SerializeField, Range(0f, 1f)] private float positionCorrection = 0.65f;
    [Tooltip("Legacy stretch multiplier kept for inspector compatibility; effective max = base * this")]
    [SerializeField, Range(1f, 3f)] private float maxStretchMultiplier = 1.15f;

    [Header("Corner Wrap (gameplay)")]
    [Tooltip("If true, the rope computes a raycast wrap path for gameplay length. Disable this for purely physical wrapping around colliders.")]
    [SerializeField] private bool useCornerWrap = false;
    [Tooltip("Layers the rope wraps around (buildings, obstacles)")]
    [SerializeField] private LayerMask ropeCollisionMask = ~0;
    [Tooltip("Offset pins off wall surface so rope does not sink into colliders")]
    [SerializeField] private float wrapSkin = 0.35f;
    [SerializeField] private int maxWrapPoints = 12;
    [SerializeField] private int wrapSolveIterations = 3;

    [Header("Verlet Chain (always on)")]
    [Tooltip("Number of particles. More = smoother coils. Min 24 enforced.")]
    [SerializeField] private int segmentCount = 40;
    [Tooltip("Velocity damping per frame. Higher = rope settles faster, less bounce.")]
    [SerializeField, Range(0.8f, 0.999f)] private float verletDamping = 0.94f;
    [Tooltip("Wall collision: ONLY pushes particles that are inside geometry (no outside magnetic shell).")]
    [SerializeField] private float verletCollisionRadius = 0.12f;
    [SerializeField] private bool verletWallCollision = true;
    [Tooltip("Downward gravity for the chain. Top-down game usually keeps this at 0.")]
    [SerializeField, Range(0f, 20f)] private float verletGravity = 0f;
    [Tooltip("Segments push out of colliders when they physically penetrate. This lets the rope wrap around obstacles without a raycast wrap path.")]
    [SerializeField] private bool segmentWallCollision = true;
    [Tooltip("Thickness of each segment for physical wrap detection. Small value = less popping, more allowed clipping.")]
    [SerializeField] private float segmentCollisionRadius = 0.06f;

    [Header("Ground Friction (lasso lies still)")]
    [Tooltip("Below this speed (u/s) a particle hard-sleeps. Keeps cargo-end coils frozen until tension.")]
    [SerializeField] private float sleepSpeed = 2f;
    [Tooltip("Velocity kept when fully asleep. Lower = laid rope freezes harder.")]
    [SerializeField, Range(0f, 0.9f)] private float groundFriction = 0.5f;

    [Header("Coiling")]
    [Tooltip("Mild straightening only when nearly taut. 0 while slack so loops can stack/overlap.")]
    [SerializeField, Range(0f, 0.25f)] private float bendingStiffness = 0.08f;
    [Tooltip("Soft pull of free particles toward gameplay wrap path when taut (no hard pin snap).")]
    [SerializeField, Range(0f, 1f)] private float wrapFollowStrength = 0.35f;
    [Tooltip("Tension where wrap-follow begins (smooth ramp, not a mode switch).")]
    [SerializeField, Range(0.5f, 0.98f)] private float wrapFollowStartTension = 0.75f;
    [Tooltip("Hard wrap pinning starts only above this tension.")]
    [SerializeField, Range(0.5f, 0.99f)] private float wrapPinTension = 0.95f;
    [Tooltip("Enable repulsion between non-neighbour strands. Off by default so coils can overlap freely.")]
    [SerializeField] private bool selfCollision = false;
    [Tooltip("Separation distance used when self-collision is enabled.")]
    [SerializeField] private float selfCollisionRadius = 0.18f;

    [Header("Solver")]
    [Tooltip("Base constraint iterations per substep (low = slack drapes; high = stiffer)")]
    [SerializeField, Range(1, 12)] private int baseIterations = 3;
    [Tooltip("Extra iterations added with tension (continuous stiffening, no binary switch)")]
    [SerializeField, Range(0, 16)] private int tautExtraIterations = 6;
    [SerializeField, Range(1, 6)] private int minSubsteps = 2;
    [SerializeField, Range(1, 8)] private int maxSubsteps = 4;

    [Header("LOD")]
    [SerializeField] private bool lodCollision = true;
    [SerializeField] private float lodDistance = 30f;

    [Header("Tension Visualization")]
    [SerializeField] private bool visualizeTension = false;
    [SerializeField] private Color slackColor = Color.white;
    [SerializeField] private Color tautColor = Color.white;
    [SerializeField] private Color overstretchColor = Color.white;

    [Header("Visual")]
    [SerializeField] private float ropeWidth = 0.3f;
    [SerializeField] private float wobbleAmount = 0.005f;
    [SerializeField] private float wobbleSpeed = 2f;
    [SerializeField, Range(0, 4)] private int smoothSubdivisions = 1;
    [SerializeField] private Color ropeColor = Color.white;

    [Header("Legacy")]
    [Tooltip("Old DistanceJoint2D bike↔cargo — disabled at runtime. Can be removed from Cargo.")]
    [SerializeField] private DistanceJoint2D cargoJoint;

    private LineRenderer lineRenderer;
    private Material ropeMaterial;
    private bool ownsRopeMaterial;
    private Rigidbody2D bikeRb;
    private Rigidbody2D cargoRb;
    private float maxRopeLength;
    private float time;
    private float currentTension;
    private Camera mainCamera;

    // Gameplay path: start hitch → wrap pins → end hitch
    private readonly List<Vector2> path = new List<Vector2>(16);
    private readonly List<Vector2> wrapPins = new List<Vector2>(12);

    // Verlet chain — the ONLY visual representation
    private VerletParticle[] particles;
    private VerletConstraint[] constraints;
    private bool verletInitialized;
    private Vector2 prevBikePos;
    private Vector2 prevCargoPos;
    private float materialRest; // fixed segment rest = maxRopeLength / (n-1)

    // Draw buffers (no GC in LateUpdate)
    private readonly List<Vector3> drawPoints = new List<Vector3>(64);
    private readonly List<Vector3> smoothed = new List<Vector3>(96);
    private Vector3[] linePositions = new Vector3[96];

    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[4];
    private readonly Collider2D[] overlapHits = new Collider2D[8];
    private readonly ColliderDistance2D[] distScratch = new ColliderDistance2D[1];
    private readonly List<RaycastHit2D> circleCastResults = new List<RaycastHit2D>(4);
    private ContactFilter2D wrapFilter;
    private PhysicsMaterial2D cargoZeroFrictionMaterial;

    private struct VerletParticle
    {
        public Vector2 position;
        public Vector2 previousPosition;
        public bool pinned;
        public bool sleeping;
    }

    private struct VerletConstraint
    {
        public int a;
        public int b;
        public float restLength;
    }

    public float Tension => currentTension;
    public bool IsTaut => currentTension > 0.9f;
    public bool IsOverstretched => currentTension > 1f;

    /// <summary>
    /// Direction from cargo hitch toward the previous path point (wrap pin or bike).
    /// Used by cargo rotation / pull visuals so they respect corner wrap.
    /// </summary>
    public bool TryGetCargoPullDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (path.Count < 2 || endPoint == null)
            return false;

        Vector2 hitch = endPoint.position;
        Vector2 anchor = path[path.Count - 2];
        Vector2 toAnchor = anchor - hitch;
        float len = toAnchor.magnitude;
        if (len < 0.0001f)
            return false;

        direction = toAnchor / len;
        return true;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.sortingOrder = 0;
        lineRenderer.numCornerVertices = 5;
        lineRenderer.numCapVertices = 3;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        SetupSolidRopeMaterial();
        ApplyRopeColor(ropeColor);

        wrapFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true
        };
        wrapFilter.SetLayerMask(ropeCollisionMask);

        mainCamera = Camera.main;
    }

    /// <summary>
    /// Solid unlit color, no texture. Instance is owned so builds never get missing-shader pink.
    /// Prefers the LineRenderer/scene material shader (keeps URP Unlit in the player build).
    /// </summary>
    private void SetupSolidRopeMaterial()
    {
        if (ownsRopeMaterial && ropeMaterial != null)
            Destroy(ropeMaterial);

        Material source = lineRenderer.sharedMaterial;
        bool sourceOk = source != null && source.shader != null &&
                        source.shader.name != "Hidden/InternalErrorShader";

        if (sourceOk)
        {
            ropeMaterial = new Material(source) { name = "RopeSolid (Runtime)" };
        }
        else
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("LassoRope: no unlit shader found for solid rope color.", this);
                return;
            }

            ropeMaterial = new Material(shader) { name = "RopeSolid (Runtime)" };
        }

        ownsRopeMaterial = true;

        // Force solid white base — no rope texture / pink missing-tex
        if (ropeMaterial.HasProperty("_BaseMap"))
            ropeMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (ropeMaterial.HasProperty("_MainTex"))
            ropeMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        ropeMaterial.mainTexture = Texture2D.whiteTexture;

        lineRenderer.sharedMaterial = ropeMaterial;
    }

    private void ApplyRopeColor(Color color)
    {
        if (lineRenderer == null)
            return;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
        lineRenderer.colorGradient = gradient;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        if (ropeMaterial == null)
            return;

        ropeMaterial.color = color;
        if (ropeMaterial.HasProperty("_BaseColor"))
            ropeMaterial.SetColor("_BaseColor", color);
        if (ropeMaterial.HasProperty("_Color"))
            ropeMaterial.SetColor("_Color", color);
        if (ropeMaterial.HasProperty("_RendererColor"))
            ropeMaterial.SetColor("_RendererColor", color);
    }

    private void Start()
    {
        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("LassoRope: startPoint or endPoint not assigned!", this);
            enabled = false;
            return;
        }

        bikeRb = startPoint.GetComponentInParent<Rigidbody2D>();
        cargoRb = endPoint.GetComponentInParent<Rigidbody2D>();
        if (bikeRb == null || cargoRb == null)
        {
            Debug.LogError("LassoRope: Rigidbody2D missing on bike or cargo parent!", this);
            enabled = false;
            return;
        }

        if (cargoJoint != null)
            cargoJoint.enabled = false;

        float baseDist = Vector2.Distance(startPoint.position, endPoint.position);
        if (cargoMaxDistance > 0f)
            maxRopeLength = cargoMaxDistance;
        else
            maxRopeLength = baseDist * Mathf.Max(1f, ropeLengthMultiplier);

        maxRopeLength *= Mathf.Clamp(maxStretchMultiplier, 1f, 1.5f);

        prevBikePos = startPoint.position;
        prevCargoPos = endPoint.position;

        ConfigureCargoBody();
        if (ignoreBikeCargoCollision)
            IgnoreBikeCargoCollision();
        RebuildPath();
        InitializeVerlet();

        materialRest = maxRopeLength / Mathf.Max(particles.Length - 1, 1);
    }

    private void ConfigureCargoBody()
    {
        cargoRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        cargoRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        cargoRb.gravityScale = 0f;

        if (cargoRb.mass > 5f)
            cargoRb.mass = 2f;

        if (cargoZeroFrictionMaterial == null)
        {
            cargoZeroFrictionMaterial = new PhysicsMaterial2D("CargoZeroFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        cargoRb.sharedMaterial = cargoZeroFrictionMaterial;
        foreach (var col in cargoRb.GetComponentsInChildren<Collider2D>())
            col.sharedMaterial = cargoZeroFrictionMaterial;
    }

    /// <summary>
    /// Байк и коробка не сталкиваются физически: иначе коробка упирается в байк
    /// (масса байка в десятки раз больше) и не может перелететь его по инерции,
    /// когда байк тормозит. Перелёт и стоп об верёвку — геймплейная механика.
    /// </summary>
    private void IgnoreBikeCargoCollision()
    {
        Collider2D[] bikeCols = bikeRb.GetComponentsInChildren<Collider2D>();
        Collider2D[] cargoCols = cargoRb.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < bikeCols.Length; i++)
        {
            for (int j = 0; j < cargoCols.Length; j++)
            {
                if (bikeCols[i] != null && cargoCols[j] != null)
                    Physics2D.IgnoreCollision(bikeCols[i], cargoCols[j], true);
            }
        }
    }

    private void OnDestroy()
    {
        if (cargoZeroFrictionMaterial != null)
            Destroy(cargoZeroFrictionMaterial);
        if (ownsRopeMaterial && ropeMaterial != null)
            Destroy(ropeMaterial);
    }

    private void FixedUpdate()
    {
        if (bikeRb == null || cargoRb == null)
            return;

        RebuildPath();
        UpdateTension();
        ApplyCargoConstraint();
    }

    private void LateUpdate()
    {
        if (startPoint == null || endPoint == null || !verletInitialized)
            return;

        time += Time.deltaTime * wobbleSpeed;

        // Rebuild with interpolated hitch positions so visual matches rendered bike/cargo
        RebuildPath();
        UpdateTension();
        SimulateRope();
        DrawRope();
        UpdateTensionColor();
    }

    // -------------------------------------------------------------------------
    // Tension tracking
    // -------------------------------------------------------------------------

    private void UpdateTension()
    {
        if (path.Count < 2 || maxRopeLength <= 0f)
        {
            currentTension = 0f;
            return;
        }

        float pathLen = GetPathLength();
        currentTension = pathLen / maxRopeLength;
    }

    private void UpdateTensionColor()
    {
        if (!visualizeTension || ropeMaterial == null)
            return;

        Color targetColor;
        if (currentTension < 0.8f)
        {
            targetColor = slackColor;
        }
        else if (currentTension < 1f)
        {
            float t = (currentTension - 0.8f) / 0.2f;
            targetColor = Color.Lerp(slackColor, tautColor, t);
        }
        else
        {
            float t = Mathf.Clamp01((currentTension - 1f) / 0.3f);
            targetColor = Color.Lerp(tautColor, overstretchColor, t);
        }

        ApplyRopeColor(targetColor);
    }

    // -------------------------------------------------------------------------
    // Verlet simulation — runs EVERY frame, no modes, no resets
    // -------------------------------------------------------------------------

    private void InitializeVerlet()
    {
        int count = Mathf.Max(segmentCount, 24);
        particles = new VerletParticle[count];
        constraints = new VerletConstraint[count - 1];

        PlaceParticlesAlongPath(resetVelocity: true);
        verletInitialized = true;
    }

    private void PlaceParticlesAlongPath(bool resetVelocity)
    {
        if (particles == null || particles.Length < 2)
            return;

        if (path.Count < 2)
        {
            Vector2 a = startPoint.position;
            Vector2 b = endPoint.position;
            for (int i = 0; i < particles.Length; i++)
            {
                float t = i / (float)(particles.Length - 1);
                Vector2 pos = Vector2.Lerp(a, b, t);
                particles[i].position = pos;
                if (resetVelocity)
                    particles[i].previousPosition = pos;
                particles[i].pinned = (i == 0 || i == particles.Length - 1);
            }
            return;
        }

        float pathLen = Mathf.Max(GetPathLength(), 0.001f);
        for (int i = 0; i < particles.Length; i++)
        {
            float t = i / (float)(particles.Length - 1);
            Vector2 pos = SamplePath(t * pathLen);
            particles[i].position = pos;
            if (resetVelocity)
                particles[i].previousPosition = pos;
            particles[i].pinned = false;
        }

        particles[0].pinned = true;
        particles[particles.Length - 1].pinned = true;
    }

    private Vector2 SamplePath(float distanceAlong)
    {
        if (path.Count < 2)
            return startPoint != null ? (Vector2)startPoint.position : Vector2.zero;

        if (distanceAlong <= 0f)
            return path[0];

        float remaining = distanceAlong;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[i + 1];
            float seg = Vector2.Distance(a, b);
            if (seg < 0.0001f)
                continue;

            if (remaining <= seg)
                return Vector2.Lerp(a, b, remaining / seg);

            remaining -= seg;
        }

        return path[path.Count - 1];
    }

    /// <summary>
    /// The one and only rope simulation. Cable constraints (extension-only) mean
    /// compression is free: the chain folds and coils instead of springing.
    /// Ground friction stops slow particles, so distant laid rope — including the
    /// cargo end — stays motionless until genuine tension propagates through.
    /// Walls are corrected only where the rope has actually penetrated, removing
    /// the old "magnetic shell" behaviour.
    /// </summary>
    private void SimulateRope()
    {
        if (particles == null || particles.Length < 2)
            return;

        int n = particles.Length;

        // Fixed material rest length: never shrinks below maxRopeLength.
        // A tiny overstretch term lets the visual chain follow the cargo when
        // the gameplay constraint has pulled it slightly beyond the limit.
        float rest = materialRest * (1f + 0.05f * Mathf.Max(0f, currentTension - 1f));
        for (int i = 0; i < constraints.Length; i++)
        {
            constraints[i].a = i;
            constraints[i].b = i + 1;
            constraints[i].restLength = rest;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        // Substeps auto-scale with bike speed so the dragged end never tunnels
        float bikeSpeed = bikeRb != null ? bikeRb.linearVelocity.magnitude : 0f;
        int substeps = Mathf.Clamp(
            Mathf.CeilToInt(bikeSpeed * dt / Mathf.Max(rest * 0.75f, 0.01f)),
            minSubsteps, maxSubsteps);
        float subDt = dt / substeps;
        float subDt2 = subDt * subDt;

        // Normalize per-frame parameters to per-substep so feel is substep-independent
        float damp = Mathf.Pow(verletDamping, 1f / substeps);
        float friction = Mathf.Pow(groundFriction, 1f / substeps);
        float sleepThreshold = sleepSpeed * 0.25f;

        int iterations = baseIterations + Mathf.RoundToInt(tautExtraIterations * Mathf.Clamp01(currentTension));

        float wrapT = Mathf.Clamp01(
            (currentTension - wrapFollowStartTension) / Mathf.Max(1f - wrapFollowStartTension, 0.001f));
        float effectiveBending = bendingStiffness * Mathf.Clamp01(currentTension); // no bend while slack
        bool pinWraps = currentTension >= wrapPinTension;

        Vector2 bikeFrom = prevBikePos;
        Vector2 cargoFrom = prevCargoPos;
        Vector2 bikeTo = startPoint.position;
        Vector2 cargoTo = endPoint.position;

        for (int s = 0; s < substeps; s++)
        {
            float alpha = (s + 1) / (float)substeps;
            Vector2 bikePos = Vector2.Lerp(bikeFrom, bikeTo, alpha);
            Vector2 cargoPos = Vector2.Lerp(cargoFrom, cargoTo, alpha);

            // Release wrap pins from the previous substep; ends stay pinned
            for (int i = 1; i < n - 1; i++)
                particles[i].pinned = false;

            particles[0].position = bikePos;
            particles[0].pinned = true;
            particles[n - 1].position = cargoPos;
            particles[n - 1].pinned = true;

            // Integrate
            for (int i = 1; i < n - 1; i++)
            {
                Vector2 pos = particles[i].position;
                Vector2 vel = (pos - particles[i].previousPosition) * damp;

                // Static friction: very slow particles freeze to the ground.
                // This is what keeps the cargo-end coils in place until tension
                // arrives, and lets the bike lay new coils on top of old ones.
                float speed = vel.magnitude / Mathf.Max(subDt, 0.00001f);
                if (speed < sleepThreshold)
                {
                    vel = Vector2.zero;
                    particles[i].sleeping = true;
                }
                else
                {
                    particles[i].sleeping = false;
                    if (speed < sleepSpeed)
                        vel *= Mathf.Lerp(friction, 1f, speed / Mathf.Max(sleepSpeed, 0.001f));
                }

                if (verletGravity > 0f)
                    vel += Vector2.down * (verletGravity * (1f - Mathf.Clamp01(currentTension))) * subDt2;

                particles[i].previousPosition = pos;
                particles[i].position = pos + vel;
            }

            ApplyBendingResistance(effectiveBending);

            // Soft wrap-follow when taut: rope hugs the gameplay path smoothly
            // instead of snapping to it.
            if (wrapT > 0f && wrapFollowStrength > 0f)
                ApplyWrapFollow(wrapT * wrapFollowStrength, subDt);

            for (int it = 0; it < iterations; it++)
            {
                particles[0].position = bikePos;
                particles[n - 1].position = cargoPos;
                if (pinWraps)
                    PinWrapParticles(resetVelocity: false);
                SolveCableConstraints();
            }

            // Wake particles that were moved by constraints or the bike
            for (int i = 1; i < n - 1; i++)
            {
                if (particles[i].sleeping &&
                    (particles[i].position - particles[i].previousPosition).sqrMagnitude > 0.0001f)
                {
                    particles[i].sleeping = false;
                }
            }
        }

        prevBikePos = bikeTo;
        prevCargoPos = cargoTo;

        bool farLod = false;
        if (lodCollision && mainCamera != null)
        {
            Vector3 midpoint = (bikeTo + cargoTo) * 0.5f;
            farLod = Vector3.Distance(midpoint, mainCamera.transform.position) > lodDistance;
        }

        // Once per frame (cheap): wall push-out only on actual penetration
        if (verletWallCollision && !farLod)
        {
            for (int i = 1; i < n - 1; i++)
            {
                if (particles[i].pinned)
                    continue;
                PushOutOfWalls(ref particles[i]);
            }
        }

        // Physical wrap: segments push out of colliders they actually penetrate.
        // This lets the rope coil around corners without a raycast wrap path.
        if (segmentWallCollision && !farLod)
            SolveSegmentWallCollision();

        // Self-collision is disabled by default: the rope can coil on top of itself.
        if (selfCollision && !farLod && currentTension < wrapPinTension)
            SolveSelfCollision();
    }

    /// <summary>
    /// Extension-only distance constraints (rope/cable). Distance may shrink freely —
    /// that is what lets the lasso pile on the ground instead of springing.
    /// </summary>
    private void SolveCableConstraints()
    {
        for (int i = 0; i < constraints.Length; i++)
        {
            int a = constraints[i].a;
            int b = constraints[i].b;
            float rest = constraints[i].restLength;

            Vector2 delta = particles[b].position - particles[a].position;
            float dist = delta.magnitude;
            if (dist < 0.0001f || dist <= rest)
                continue;

            float diff = (dist - rest) / dist;
            Vector2 offset = delta * 0.5f * diff;

            if (!particles[a].pinned)
                particles[a].position += offset;
            if (!particles[b].pinned)
                particles[b].position -= offset;
        }
    }

    /// <summary>
    /// Weak pull toward the local straight line — only when taut.
    /// While slack the stiffness is zero, so loops can stack and overlap freely.
    /// </summary>
    private void ApplyBendingResistance(float stiffness)
    {
        if (stiffness <= 0f)
            return;

        float k = stiffness * 0.5f;
        for (int i = 1; i < particles.Length - 1; i++)
        {
            if (particles[i].pinned)
                continue;

            Vector2 mid = (particles[i - 1].position + particles[i + 1].position) * 0.5f;
            particles[i].position += (mid - particles[i].position) * k;
        }
    }

    /// <summary>
    /// Softly pull free particles toward the gameplay wrap path.
    /// Strength is ramped by tension so the rope is not magnetically attracted
    /// to corners while slack.
    /// </summary>
    private void ApplyWrapFollow(float strength, float subDt)
    {
        if (wrapPins.Count == 0 || particles == null || particles.Length < 3 || path.Count < 3)
            return;

        float pathLen = Mathf.Max(GetPathLength(), 0.001f);
        int n = particles.Length;
        float rate = strength * subDt * 5f;

        for (int i = 1; i < n - 1; i++)
        {
            if (particles[i].pinned)
                continue;

            float t = i / (float)(n - 1);
            Vector2 target = SamplePath(t * pathLen);
            particles[i].position = Vector2.Lerp(particles[i].position, target, rate);
        }
    }

    /// <summary>
    /// Optional weak repulsion between non-neighbour strands.
    /// Disabled by default because the requested behaviour is overlapping coils.
    /// </summary>
    private void SolveSelfCollision()
    {
        float r = selfCollisionRadius;
        if (r <= 0f)
            return;

        float r2 = r * r;
        int n = particles.Length;

        for (int i = 0; i < n - 3; i++)
        {
            for (int j = i + 3; j < n; j++)
            {
                Vector2 delta = particles[j].position - particles[i].position;
                float d2 = delta.sqrMagnitude;
                if (d2 >= r2 || d2 < 0.00000001f)
                    continue;

                float d = Mathf.Sqrt(d2);
                Vector2 push = delta / d * ((r - d) * 0.5f);
                bool pinnedI = particles[i].pinned;
                bool pinnedJ = particles[j].pinned;

                if (!pinnedI && !pinnedJ)
                {
                    particles[i].position -= push;
                    particles[j].position += push;
                }
                else if (!pinnedI)
                {
                    particles[i].position -= push * 2f;
                }
                else if (!pinnedJ)
                {
                    particles[j].position += push * 2f;
                }
            }
        }
    }

    /// <summary>
    /// path = [start, wrap0..wrapN, end]. Locks nearest free particle to each wrap corner.
    /// Only used near-taut; position snap is small because the rope is already near the corner.
    /// </summary>
    private void PinWrapParticles(bool resetVelocity)
    {
        if (wrapPins.Count == 0 || particles == null || particles.Length < 3 || path.Count < 3)
            return;

        float pathLen = Mathf.Max(GetPathLength(), 0.001f);
        float acc = 0f;

        for (int p = 0; p < wrapPins.Count; p++)
        {
            acc += Vector2.Distance(path[p], path[p + 1]);
            int nearest = Mathf.Clamp(
                Mathf.RoundToInt((acc / pathLen) * (particles.Length - 1)),
                1, particles.Length - 2);

            while (nearest < particles.Length - 2 && particles[nearest].pinned)
                nearest++;

            particles[nearest].position = wrapPins[p];
            if (resetVelocity)
                particles[nearest].previousPosition = wrapPins[p];
            particles[nearest].pinned = true;
        }
    }

    /// <summary>
    /// Penetration-only wall correction. Uses OverlapPoint to detect particles
    /// that are actually inside a collider, then pushes them out along the
    /// surface normal. No outside magnetic shell.
    /// </summary>
    private void PushOutOfWalls(ref VerletParticle p)
    {
        int count = Physics2D.OverlapCircle(p.position, 0.001f, wrapFilter, overlapHits);
        if (count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapHits[i];
            if (col == null)
                continue;

            Rigidbody2D colRb = col.attachedRigidbody;
            if (colRb != null && (colRb == bikeRb || colRb == cargoRb))
                continue;
            if (colRb != null && colRb.bodyType == RigidbodyType2D.Dynamic)
                continue;

            Vector2 closest = col.ClosestPoint(p.position);

            // If ClosestPoint is not the particle itself, the particle is outside.
            if ((closest - p.position).sqrMagnitude > 0.0001f)
                continue;

            // Choose an escape direction. Prefer the current velocity; if the
            // particle is stationary, pick an arbitrary up vector.
            Vector2 escape = (p.position - p.previousPosition).normalized;
            if (escape.sqrMagnitude < 0.0001f)
                escape = Vector2.up;

            // Raycast from inside toward the surface to get a real normal.
            RaycastHit2D hit = Physics2D.Raycast(p.position, escape, 0.5f, ropeCollisionMask);
            if (hit.collider != null)
                escape = hit.normal;
            else
                escape = -escape;

            p.position += escape * 0.02f;
        }
    }

    /// <summary>
    /// Physical wrapping around obstacles. Each segment casts a small circle
    /// along itself; if it touches/pierces geometry, both end particles are
    /// pushed out along the surface normal. Works for EdgeCollider2D, boxes,
    /// polygons, tilemaps and composite colliders. No raycast wrap path is
    /// required, so the rope never snaps to preset pins.
    /// </summary>
    private void SolveSegmentWallCollision()
    {
        if (particles == null || particles.Length < 2)
            return;

        float radius = segmentCollisionRadius;
        int n = particles.Length;

        for (int i = 0; i < n - 1; i++)
        {
            if (particles[i].pinned && particles[i + 1].pinned)
                continue;

            Vector2 a = particles[i].position;
            Vector2 b = particles[i + 1].position;
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.0001f)
                continue;
            Vector2 dir = delta / len;

            // CircleCast gives the segment some thickness and catches tunneling.
            circleCastResults.Clear();
            int hitCount = Physics2D.CircleCast(a, radius, dir, wrapFilter, circleCastResults, len);
            if (hitCount == 0)
                continue;

            RaycastHit2D hit = default;
            bool found = false;
            for (int h = 0; h < hitCount; h++)
            {
                RaycastHit2D candidate = circleCastResults[h];
                if (candidate.collider == null)
                    continue;

                Rigidbody2D hitRb = candidate.rigidbody;
                if (hitRb != null && (hitRb == bikeRb || hitRb == cargoRb))
                    continue;
                if (hitRb != null && hitRb.bodyType == RigidbodyType2D.Dynamic)
                    continue;

                hit = candidate;
                found = true;
                break;
            }

            if (!found)
                continue;

            // fraction along the segment where the cast touched
            float t = Mathf.Clamp01(hit.distance / Mathf.Max(len, 0.0001f));

            Vector2 push = hit.normal * (radius + 0.01f);

            if (!particles[i].pinned)
                particles[i].position += push * (1f - t);
            if (!particles[i + 1].pinned)
                particles[i + 1].position += push * t;
        }
    }

    // -------------------------------------------------------------------------
    // Path / corner wrap (gameplay)
    // -------------------------------------------------------------------------

    private void RebuildPath()
    {
        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;

        if (!useCornerWrap)
        {
            wrapPins.Clear();
            path.Clear();
            path.Add(start);
            path.Add(end);
            return;
        }

        for (int i = wrapPins.Count - 1; i >= 0; i--)
        {
            Vector2 prev = i == 0 ? start : wrapPins[i - 1];
            Vector2 next = i == wrapPins.Count - 1 ? end : wrapPins[i + 1];
            if (IsClear(prev, next))
                wrapPins.RemoveAt(i);
        }

        for (int iter = 0; iter < wrapSolveIterations; iter++)
        {
            bool added = false;
            int segmentCountLocal = wrapPins.Count + 1;

            for (int s = 0; s < segmentCountLocal; s++)
            {
                if (wrapPins.Count >= maxWrapPoints)
                    break;

                Vector2 a = s == 0 ? start : wrapPins[s - 1];
                Vector2 b = s >= wrapPins.Count ? end : wrapPins[s];

                if (!TryGetWrapPoint(a, b, out Vector2 pin))
                    continue;

                bool tooClose = false;
                for (int p = 0; p < wrapPins.Count; p++)
                {
                    if ((wrapPins[p] - pin).sqrMagnitude < wrapSkin * wrapSkin)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose || (pin - a).sqrMagnitude < 0.01f || (pin - b).sqrMagnitude < 0.01f)
                    continue;

                wrapPins.Insert(s, pin);
                added = true;
                break;
            }

            if (!added)
                break;
        }

        path.Clear();
        path.Add(start);
        for (int i = 0; i < wrapPins.Count; i++)
            path.Add(wrapPins[i]);
        path.Add(end);
    }

    private bool IsClear(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float dist = delta.magnitude;
        if (dist < 0.001f)
            return true;

        int count = Physics2D.Raycast(a, delta / dist, wrapFilter, rayHits, dist);
        return !TryGetWorldHit(count, dist, out _);
    }

    private bool TryGetWrapPoint(Vector2 a, Vector2 b, out Vector2 pin)
    {
        pin = default;
        Vector2 delta = b - a;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            return false;

        Vector2 dir = delta / dist;
        int count = Physics2D.Raycast(a, dir, wrapFilter, rayHits, dist);
        if (!TryGetWorldHit(count, dist, out RaycastHit2D hit))
            return false;

        pin = hit.point + hit.normal * wrapSkin;
        return true;
    }

    private bool TryGetWorldHit(int count, float segmentLength, out RaycastHit2D best)
    {
        return TryGetWorldHit(count, segmentLength, out best, ignoreNearEnds: true);
    }

    private bool TryGetWorldHit(int count, float segmentLength, out RaycastHit2D best, bool ignoreNearEnds)
    {
        best = default;
        float bestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = rayHits[i];
            if (hit.collider == null)
                continue;

            // Wrap raycasts: skip origins/endpoints. Cargo cast-slide needs near hits.
            if (ignoreNearEnds)
            {
                if (hit.distance < 0.05f || hit.distance > segmentLength - 0.05f)
                    continue;
            }
            else if (hit.distance > segmentLength)
            {
                continue;
            }

            Rigidbody2D hitRb = hit.rigidbody;
            if (hitRb != null)
            {
                if (hitRb == bikeRb || hitRb == cargoRb)
                    continue;
                if (hitRb.bodyType == RigidbodyType2D.Dynamic)
                    continue;
            }

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                best = hit;
                found = true;
            }
        }

        return found;
    }

    private float GetPathLength()
    {
        float len = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            len += Vector2.Distance(path[i], path[i + 1]);
        return len;
    }

    // -------------------------------------------------------------------------
    // Gameplay constraint — cargo only
    // -------------------------------------------------------------------------

    private void ApplyCargoConstraint()
    {
        if (path.Count < 2 || maxRopeLength <= 0f)
            return;

        float pathLen = GetPathLength();
        if (pathLen <= maxRopeLength)
            return;

        float excess = pathLen - maxRopeLength;

        Vector2 hitch = endPoint.position;
        Vector2 anchor = path[path.Count - 2];
        Vector2 toAnchor = anchor - hitch;
        float segLen = toAnchor.magnitude;
        if (segLen < 0.0001f)
            return;

        Vector2 pullDir = toAnchor / segLen;

        float normalized = Mathf.Clamp01(excess / Mathf.Max(maxRopeLength * 0.35f, 0.01f));
        float force = stretchForceBase * Mathf.Pow(normalized, stretchExponent);
        cargoRb.AddForce(pullDir * force, ForceMode2D.Force);

        if (positionCorrection > 0f && excess > 0.01f)
        {
            // Slide correction along walls — raw MovePosition can shove cargo into geometry
            MoveCargoSlid(pullDir, excess * positionCorrection);

            float outward = Vector2.Dot(cargoRb.linearVelocity, -pullDir);
            if (outward > 0f)
                cargoRb.linearVelocity += pullDir * outward;
        }
    }

    /// <summary>
    /// Move cargo along pull with cast-slide so hard rope correction does not tunnel walls.
    /// </summary>
    private void MoveCargoSlid(Vector2 pullDir, float distance)
    {
        if (distance <= 0.0001f || pullDir.sqrMagnitude < 0.0001f)
            return;

        Vector2 dir = pullDir.normalized;
        float remaining = distance;
        Vector2 pos = cargoRb.position;
        Vector2 savedPos = pos;
        const float skin = 0.06f;
        int slides = Mathf.Max(1, wrapSolveIterations);

        for (int i = 0; i < slides && remaining > 0.0001f; i++)
        {
            // Cast from the tentative pose so later slide iterations stay accurate
            cargoRb.position = pos;

            int hitCount = cargoRb.Cast(dir, wrapFilter, rayHits, remaining + skin);
            if (!TryGetWorldHit(hitCount, remaining + skin, out RaycastHit2D hit, ignoreNearEnds: false))
            {
                pos += dir * remaining;
                remaining = 0f;
                break;
            }

            float move = Mathf.Max(0f, hit.distance - skin);
            if (move > 0f)
            {
                pos += dir * move;
                remaining -= move;
            }
            else
            {
                // Already touching: only slide tangentially
                remaining *= 0.85f;
            }

            float intoWall = Vector2.Dot(dir, hit.normal);
            if (intoWall >= 0f)
                break;

            Vector2 slid = dir - hit.normal * intoWall;
            if (slid.sqrMagnitude < 0.0001f)
                break;

            dir = slid.normalized;
        }

        cargoRb.position = savedPos;
        cargoRb.MovePosition(pos);
    }

    // -------------------------------------------------------------------------
    // Visual — draw the particle chain directly, it IS the rope
    // -------------------------------------------------------------------------

    private void DrawRope()
    {
        drawPoints.Clear();

        int n = particles.Length;
        int step = 1;

        if (lodCollision && mainCamera != null)
        {
            Vector3 midpoint = (startPoint.position + endPoint.position) * 0.5f;
            if (Vector3.Distance(midpoint, mainCamera.transform.position) > lodDistance)
                step = 2;
        }

        for (int i = 0; i < n; i += step)
            drawPoints.Add(particles[i].position);
        if ((n - 1) % step != 0)
            drawPoints.Add(particles[n - 1].position);

        CatmullRomSmooth(drawPoints, smoothSubdivisions, smoothed);

        // Hitch pins after smooth — bike/cargo must stay exact
        if (smoothed.Count >= 2)
        {
            smoothed[0] = startPoint.position;
            smoothed[smoothed.Count - 1] = endPoint.position;
        }

        if (wobbleAmount > 0f && smoothed.Count > 1)
        {
            Vector3 mainDir = (smoothed[smoothed.Count - 1] - smoothed[0]).normalized;
            Vector3 wobbleAxis = new Vector3(-mainDir.y, mainDir.x, 0f);
            if (wobbleAxis.sqrMagnitude < 0.0001f)
                wobbleAxis = Vector3.right;

            for (int i = 0; i < smoothed.Count; i++)
            {
                float t = i / (float)(smoothed.Count - 1);
                float sagCurve = Mathf.Sin(t * Mathf.PI);
                float w = Mathf.Sin(time + t * 6f) * sagCurve * wobbleAmount;
                smoothed[i] += wobbleAxis * w;
            }
        }

        if (linePositions.Length < smoothed.Count)
            linePositions = new Vector3[smoothed.Count + 8];

        for (int i = 0; i < smoothed.Count; i++)
            linePositions[i] = smoothed[i];

        lineRenderer.positionCount = smoothed.Count;
        lineRenderer.SetPositions(linePositions);
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;
    }

    private static void CatmullRomSmooth(List<Vector3> pts, int sub, List<Vector3> result)
    {
        result.Clear();
        if (pts.Count < 3 || sub <= 0)
        {
            result.AddRange(pts);
            return;
        }

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? pts[i - 1] : pts[i];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = i + 2 < pts.Count ? pts[i + 2] : pts[i + 1];

            for (int j = 0; j < sub; j++)
            {
                float t = j / (float)sub;
                float t2 = t * t;
                float t3 = t2 * t;
                Vector3 p = 0.5f * (
                    2f * p1 +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );
                result.Add(p);
            }
        }

        result.Add(pts[pts.Count - 1]);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (path == null || path.Count < 2)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < path.Count - 1; i++)
            Gizmos.DrawLine(path[i], path[i + 1]);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < wrapPins.Count; i++)
            Gizmos.DrawWireSphere(wrapPins[i], 0.25f);

        if (particles != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            for (int i = 0; i < particles.Length; i++)
                Gizmos.DrawWireSphere(particles[i].position, verletCollisionRadius);
        }

        // Visual debug: all segments in green, hits in red
        if (particles != null && Application.isPlaying)
        {
            for (int i = 0; i < particles.Length - 1; i++)
            {
                Vector2 a = particles[i].position;
                Vector2 b = particles[i + 1].position;
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.0001f)
                    continue;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(a, b);

                if (segmentWallCollision)
                {
                    RaycastHit2D hit = Physics2D.CircleCast(a, segmentCollisionRadius, dir / len, len, ropeCollisionMask);
                    if (hit.collider != null)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(a, b);
                    }
                }
            }
        }

        // Tension indicator
        if (visualizeTension && startPoint != null && endPoint != null)
        {
            Vector3 mid = (startPoint.position + endPoint.position) * 0.5f;
            Color tensionColor = currentTension < 0.8f ? slackColor :
                                 currentTension < 1f ? tautColor : overstretchColor;
            Gizmos.color = tensionColor;
            Gizmos.DrawWireSphere(mid, 0.5f + currentTension * 0.3f);
        }
    }
#endif
}
