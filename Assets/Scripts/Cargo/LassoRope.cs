using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Level 3 rope:
/// - Gameplay: one-way max-distance constraint (pulls cargo only, never slows the bike)
/// - Corner wrap: rope path bends around obstacles via raycasts
/// - Visual: Verlet simulation (no Rigidbody) + wall push-out + LineRenderer
/// - Optimization: adaptive segment count, LOD, tension visualization
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
    [Tooltip("How hard cargo is reeled in when over max path length")]
    [SerializeField] private float stretchForceBase = 150f;
    [SerializeField, Range(1f, 4f)] private float stretchExponent = 2.5f;
    [Tooltip("Hard position correction strength when far over limit (0-1)")]
    [SerializeField, Range(0f, 1f)] private float positionCorrection = 0.65f;
    [Tooltip("Legacy stretch multiplier kept for inspector compatibility; effective max = base * this")]
    [SerializeField, Range(1f, 3f)] private float maxStretchMultiplier = 1.15f;

    [Header("Corner Wrap")]
    [Tooltip("Layers the rope wraps around (buildings, obstacles)")]
    [SerializeField] private LayerMask ropeCollisionMask = ~0;
    [Tooltip("Offset pins off wall surface so rope does not sink into colliders")]
    [SerializeField] private float wrapSkin = 0.35f;
    [SerializeField] private int maxWrapPoints = 12;
    [SerializeField] private int wrapSolveIterations = 3;

    [Header("Verlet Simulation")]
    [Tooltip("Number of Verlet particles (more = smoother rope, more CPU)")]
    [SerializeField] private int segmentCount = 16;
    [Tooltip("Solver iterations per frame (higher = stiffer rope)")]
    [SerializeField] private int verletIterations = 3;
    [Tooltip("Velocity damping per frame (0.9-0.99)")]
    [SerializeField, Range(0.8f, 0.999f)] private float verletDamping = 0.98f;
    [Tooltip("Downward gravity for natural sag")]
    [SerializeField] private float verletGravity = 3f;
    [Tooltip("Collision radius for wall push-out")]
    [SerializeField] private float verletCollisionRadius = 0.15f;
    [Tooltip("Enable wall collision for Verlet particles")]
    [SerializeField] private bool verletWallCollision = true;

    [Header("Adaptive Optimization")]
    [Tooltip("Reduce segments when rope is taut (0 = disabled, 0.5 = half segments when fully taut)")]
    [SerializeField, Range(0f, 0.8f)] private float adaptiveSegmentReduction = 0.4f;
    [Tooltip("When taut, snap visual particles to wrap path (no free sag)")]
    [SerializeField] private bool skipVerletWhenTaut = true;
    [Tooltip("Reduce collision checks when rope is far from camera")]
    [SerializeField] private bool lodCollision = true;
    [Tooltip("Distance from camera to reduce quality")]
    [SerializeField] private float lodDistance = 30f;

    [Header("Tension Visualization")]
    [Tooltip("Change rope color based on tension")]
    [SerializeField] private bool visualizeTension = true;
    [Tooltip("Color when rope is slack")]
    [SerializeField] private Color slackColor = new Color(0.45f, 0.25f, 0.08f, 1f);
    [Tooltip("Color when rope is fully taut")]
    [SerializeField] private Color tautColor = new Color(0.8f, 0.3f, 0.1f, 1f);
    [Tooltip("Color when rope is over-stretched (pulling cargo)")]
    [SerializeField] private Color overstretchColor = new Color(1f, 0.2f, 0.1f, 1f);

    [Header("Visual")]
    [SerializeField] private float sagForce = 2f;
    [SerializeField] private float ropeWidth = 0.3f;
    [SerializeField] private float textureTiling = 4f;
    [SerializeField] private float wobbleAmount = 0.005f;
    [SerializeField] private float wobbleSpeed = 2f;
    [SerializeField] private int smoothSubdivisions = 3;
    [SerializeField] private Color ropeColor = new Color(0.45f, 0.25f, 0.08f, 1f);

    [Header("Legacy")]
    [Tooltip("Old DistanceJoint2D bike↔cargo — disabled at runtime. Can be removed from Cargo.")]
    [SerializeField] private DistanceJoint2D cargoJoint;

    private LineRenderer lineRenderer;
    private Material ropeMaterial;
    private Rigidbody2D bikeRb;
    private Rigidbody2D cargoRb;
    private float maxRopeLength;
    private float time;
    private float currentTension;
    private int activeSegmentCount;
    private Camera mainCamera;

    // Path: start hitch → wrap pins → end hitch
    private readonly List<Vector2> path = new List<Vector2>(16);
    private readonly List<Vector2> wrapPins = new List<Vector2>(12);

    // Verlet particles
    private VerletParticle[] particles;
    private VerletConstraint[] constraints;
    private bool verletInitialized;

    // Draw buffers (no GC in LateUpdate)
    private readonly List<Vector3> drawPoints = new List<Vector3>(32);
    private readonly List<Vector3> smoothed = new List<Vector3>(64);
    private Vector3[] linePositions = new Vector3[64];

    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[4];
    private readonly Collider2D[] overlapHits = new Collider2D[8];
    private ContactFilter2D wrapFilter;
    private PhysicsMaterial2D cargoZeroFrictionMaterial;

    private struct VerletParticle
    {
        public Vector2 position;
        public Vector2 previousPosition;
        public bool pinned;
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
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.sortingOrder = 10;
        lineRenderer.startColor = ropeColor;
        lineRenderer.endColor = ropeColor;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        ropeMaterial = lineRenderer.material;
        if (ropeMaterial != null)
        {
            ropeMaterial.color = ropeColor;
            ropeMaterial.SetColor("_Color", ropeColor);
        }

        wrapFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true
        };
        wrapFilter.SetLayerMask(ropeCollisionMask);

        mainCamera = Camera.main;
        activeSegmentCount = segmentCount;
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

        ConfigureCargoBody();
        RebuildPath();
        InitializeVerlet();
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

    private void OnDestroy()
    {
        if (cargoZeroFrictionMaterial != null)
            Destroy(cargoZeroFrictionMaterial);
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
        if (startPoint == null || endPoint == null)
            return;

        time += Time.deltaTime * wobbleSpeed;

        // Rebuild with interpolated hitch positions so visual wrap matches rendered bike/cargo
        RebuildPath();
        UpdateTension();
        UpdateAdaptiveSegments();
        UpdateVerlet();
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

        ropeMaterial.color = targetColor;
        lineRenderer.startColor = targetColor;
        lineRenderer.endColor = targetColor;
    }

    // -------------------------------------------------------------------------
    // Adaptive optimization
    // -------------------------------------------------------------------------

    private void UpdateAdaptiveSegments()
    {
        if (adaptiveSegmentReduction <= 0f)
        {
            activeSegmentCount = segmentCount;
            return;
        }

        float tautness = Mathf.Clamp01(currentTension);
        float reduction = adaptiveSegmentReduction * tautness;
        activeSegmentCount = Mathf.Max(4, Mathf.RoundToInt(segmentCount * (1f - reduction)));

        if (lodCollision && mainCamera != null)
        {
            Vector3 midpoint = (startPoint.position + endPoint.position) * 0.5f;
            float distToCamera = Vector3.Distance(midpoint, mainCamera.transform.position);
            if (distToCamera > lodDistance)
            {
                activeSegmentCount = Mathf.Max(4, activeSegmentCount / 2);
            }
        }
    }

    private bool ShouldSkipVerlet()
    {
        if (!skipVerletWhenTaut)
            return false;

        return currentTension > 0.95f && currentTension < 1.05f;
    }

    // -------------------------------------------------------------------------
    // Verlet simulation
    // -------------------------------------------------------------------------

    private void InitializeVerlet()
    {
        int count = Mathf.Max(segmentCount, 4);
        particles = new VerletParticle[count];
        constraints = new VerletConstraint[count - 1];

        PlaceParticlesAlongPath(resetVelocity: true);
        UpdateVerletRestLengths();

        verletInitialized = true;
    }

    /// <summary>
    /// Fixed rope length split across constraints. Slack (path shorter than max)
    /// leaves leftover length for sag; taut path uses path length so rope hugs wraps.
    /// </summary>
    private void UpdateVerletRestLengths()
    {
        if (particles == null || constraints == null || particles.Length < 2)
            return;

        float pathLen = GetPathLength();
        float visualLen = Mathf.Max(pathLen, maxRopeLength);
        if (currentTension >= 0.98f)
            visualLen = Mathf.Max(pathLen, 0.01f);

        float rest = visualLen / (particles.Length - 1);
        for (int i = 0; i < constraints.Length; i++)
        {
            constraints[i] = new VerletConstraint
            {
                a = i,
                b = i + 1,
                restLength = rest
            };
        }
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
        PinWrapParticles(resetVelocity);
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

    private void UpdateVerlet()
    {
        if (!verletInitialized)
            return;

        // Taut / skip: snap visual to wrap path (fixes freeze + through-wall look)
        if (ShouldSkipVerlet() || currentTension >= 0.98f)
        {
            PlaceParticlesAlongPath(resetVelocity: true);
            UpdateVerletRestLengths();
            return;
        }

        UpdateVerletRestLengths();

        // Keep ends and wrap pins locked to gameplay path
        particles[0].position = startPoint.position;
        particles[0].pinned = true;
        particles[particles.Length - 1].position = endPoint.position;
        particles[particles.Length - 1].pinned = true;
        // Clear stale wrap pins from previous frame, then re-pin ends + corners
        for (int i = 1; i < particles.Length - 1; i++)
            particles[i].pinned = false;
        PinWrapParticles(resetVelocity: false);

        float dt = Time.deltaTime;
        float dt2 = dt * dt;
        float gravity = verletGravity + sagForce;

        float pathLen = Mathf.Max(GetPathLength(), 0.001f);
        // Pull free particles toward path corridor so slack rope still follows wraps
        float pathAttract = Mathf.Lerp(0.08f, 0.4f, Mathf.Clamp01(currentTension));

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].pinned)
                continue;

            Vector2 pos = particles[i].position;
            Vector2 prev = particles[i].previousPosition;
            Vector2 vel = (pos - prev) * verletDamping;

            vel += Vector2.down * gravity * dt2;

            Vector2 guide = SamplePath((i / (float)(particles.Length - 1)) * pathLen);
            pos = Vector2.Lerp(pos + vel, guide, pathAttract);

            particles[i].previousPosition = particles[i].position;
            particles[i].position = pos;
        }

        int iterations = verletIterations;
        bool doCollision = verletWallCollision;

        if (lodCollision && mainCamera != null)
        {
            Vector3 midpoint = (startPoint.position + endPoint.position) * 0.5f;
            float distToCamera = Vector3.Distance(midpoint, mainCamera.transform.position);
            if (distToCamera > lodDistance)
            {
                iterations = Mathf.Max(1, iterations / 2);
                doCollision = false;
            }
        }

        for (int iter = 0; iter < iterations; iter++)
        {
            particles[0].position = startPoint.position;
            particles[particles.Length - 1].position = endPoint.position;
            PinWrapParticles(resetVelocity: false);

            for (int i = 0; i < constraints.Length; i++)
            {
                int a = constraints[i].a;
                int b = constraints[i].b;
                float rest = constraints[i].restLength;

                Vector2 delta = particles[b].position - particles[a].position;
                float dist = delta.magnitude;
                if (dist < 0.0001f)
                    continue;

                float diff = (dist - rest) / dist;
                Vector2 offset = delta * 0.5f * diff;

                if (!particles[a].pinned)
                    particles[a].position += offset;
                if (!particles[b].pinned)
                    particles[b].position -= offset;
            }

            if (doCollision)
            {
                for (int i = 1; i < particles.Length - 1; i++)
                {
                    if (particles[i].pinned)
                        continue;
                    PushOutOfWalls(ref particles[i]);
                }
            }
        }
    }

    /// <summary>
    /// path = [start, wrap0..wrapN, end]. Locks nearest free particle to each wrap corner.
    /// </summary>
    private void PinWrapParticles(bool resetVelocity)
    {
        if (wrapPins.Count == 0 || particles == null || particles.Length < 3 || path.Count < 3)
            return;

        float pathLen = Mathf.Max(GetPathLength(), 0.001f);
        float acc = 0f;

        // For each wrap pin at path[i+1], accumulate distance start→pin
        for (int p = 0; p < wrapPins.Count; p++)
        {
            acc += Vector2.Distance(path[p], path[p + 1]);
            int nearest = Mathf.Clamp(
                Mathf.RoundToInt((acc / pathLen) * (particles.Length - 1)),
                1, particles.Length - 2);

            // Avoid collapsing multiple pins onto the same particle
            while (nearest < particles.Length - 2 && particles[nearest].pinned)
                nearest++;

            particles[nearest].position = wrapPins[p];
            if (resetVelocity)
                particles[nearest].previousPosition = wrapPins[p];
            particles[nearest].pinned = true;
        }
    }

    private void PushOutOfWalls(ref VerletParticle p)
    {
        int count = Physics2D.OverlapCircle(p.position, verletCollisionRadius, wrapFilter, overlapHits);
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
            Vector2 push = p.position - closest;
            float pushDist = push.magnitude;

            if (pushDist < 0.0001f)
            {
                Vector2 delta = p.position - p.previousPosition;
                push = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
                p.position = closest + push * (verletCollisionRadius + 0.01f);
            }
            else if (pushDist < verletCollisionRadius)
            {
                p.position = closest + (push / pushDist) * verletCollisionRadius;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Path / corner wrap
    // -------------------------------------------------------------------------

    private void RebuildPath()
    {
        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;

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
    // Visual
    // -------------------------------------------------------------------------

    private void DrawRope()
    {
        drawPoints.Clear();

        // Taut: draw gameplay path so adaptive LOD cannot skip corner pins (through-wall look)
        if (currentTension >= 0.98f && path.Count >= 2)
        {
            for (int i = 0; i < path.Count; i++)
                drawPoints.Add(path[i]);
        }
        else if (!verletInitialized || particles == null || particles.Length < 2)
        {
            if (path.Count >= 2)
            {
                for (int i = 0; i < path.Count; i++)
                    drawPoints.Add(path[i]);
            }
            else
            {
                drawPoints.Add(startPoint.position);
                drawPoints.Add(endPoint.position);
            }
        }
        else
        {
            int step = Mathf.Max(1, particles.Length / Mathf.Max(activeSegmentCount, 2));
            int last = particles.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                bool mustKeep = i == 0 || i == last || particles[i].pinned || (i % step == 0);
                if (!mustKeep)
                    continue;

                // Avoid duplicate consecutive points when pin lands on step sample
                Vector3 p = particles[i].position;
                if (drawPoints.Count > 0 && (drawPoints[drawPoints.Count - 1] - p).sqrMagnitude < 0.0001f)
                    continue;

                drawPoints.Add(p);
            }
        }

        CatmullRomSmooth(drawPoints, smoothSubdivisions, smoothed);

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

        if (ropeMaterial != null && smoothed.Count > 1)
        {
            float len = 0f;
            for (int i = 0; i < smoothed.Count - 1; i++)
                len += Vector3.Distance(smoothed[i], smoothed[i + 1]);
            ropeMaterial.mainTextureScale = new Vector2(len * textureTiling, 1f);
        }
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

        // Tension indicator
        if (visualizeTension)
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
