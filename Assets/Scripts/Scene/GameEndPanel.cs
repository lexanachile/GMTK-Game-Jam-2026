using UnityEngine;
using TMPro;

public class GameEndPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private string timeFormat = "mm':'ss";
    [SerializeField] private string resultMessage = "You made it in {time}. Try to do better next time!";

    public void DisplayTime(float seconds)
    {
        if (timeText != null)
        {
            int totalSec = Mathf.FloorToInt(seconds);
            int minutes = totalSec / 60;
            int secs = totalSec % 60;

            string formatted = timeFormat
                .Replace("mm", minutes.ToString("00"))
                .Replace("ss", secs.ToString("00"));

            string finalMessage = resultMessage.Replace("{time}", formatted);
            timeText.text = finalMessage;
        }
    }
}