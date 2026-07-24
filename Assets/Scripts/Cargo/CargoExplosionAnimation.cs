using UnityEngine;
using System.Collections;

public class CargoExplosionAnimation : MonoBehaviour
{
    public Sprite[] frames;
    public float frameTime = 0.1f;

    void Start()
    {
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        for(int i = 0; i < frames.Length; i++)
        {
            sr.sprite = frames[i];
            yield return new WaitForSeconds(frameTime);
        }
        Destroy(gameObject);
    }
}
