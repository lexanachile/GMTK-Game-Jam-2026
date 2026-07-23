using UnityEngine;

public class DestinationPoint : MonoBehaviour
{
    public static DestinationPoint Instance;

    private void Awake()
    {
        Instance = this;
    }
}