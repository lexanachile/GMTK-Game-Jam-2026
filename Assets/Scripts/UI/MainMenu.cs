using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject SoundSlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private AudioMixer audioMixer;
    private bool flag = false;

    void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        // Инициализация слайдера при старте
        if (volumeSlider != null && audioMixer != null)
        {
            // Получаем текущее значение громкости из микшера
            if (audioMixer.GetFloat("MasterVolume", out float currentVolume))
            {
                // Преобразуем из dB в нормализованное значение (0-1)
                float normalizedValue = (currentVolume + 50) / 50;
                volumeSlider.value = Mathf.Clamp01(normalizedValue);
            }
            else
            {
                volumeSlider.value = 0.75f; // Значение по умолчанию
                SetVolume(volumeSlider.value);
            }
        }
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("MainGame");
    }

    public void SoundControl()
    {
        flag = !flag;
        SoundSlider.SetActive(flag);
    }

    public void SetVolume(float sliderValue)
    {
        if (audioMixer != null)
        {
            // Используем -80 dB для полной тишины
            float volumeInDB = Mathf.Lerp(-80f, 0f, sliderValue);
            audioMixer.SetFloat("MasterVolume", volumeInDB);
        
            PlayerPrefs.SetFloat("MasterVolume", sliderValue);
            PlayerPrefs.Save();
        }
    }
}