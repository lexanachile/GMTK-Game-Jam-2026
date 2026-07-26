using UnityEngine;

public class Monster : MonoBehaviour
{
    public float speed = 2f;
    private float sleepDistance;          // устанавливается спавнером
    private Transform player;
    private Rigidbody2D rb;
    private bool isSleeping = false;

    public void Initialize(float sleepDist)
    {
        sleepDistance = sleepDist;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Предполагаем, что у префаба есть Rigidbody2D и коллайдер
        rb.gravityScale = 0;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
            Debug.LogError("Player not found!");
    }

    private void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > sleepDistance)
            isSleeping = true;
        else if (dist <= sleepDistance)
            isSleeping = false;
    }

    private void FixedUpdate()
    {
        if (isSleeping || player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * speed;
    }
}