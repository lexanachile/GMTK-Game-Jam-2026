using UnityEngine;

public class CargoExplosion : MonoBehaviour
{
    public GameObject explosionPrefab;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Explode();
    }

    void Explode()
    {
        if(explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
