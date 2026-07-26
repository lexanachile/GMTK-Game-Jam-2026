using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject SoundSlider;
    private bool flag = false;

    public void PlayGame()
    {
        SceneManager.LoadScene("MainGame"); // имя твоей игровой сцены (без расширения)
    }

    public void SoundControl()
    {
        flag = !flag;
        SoundSlider.SetActive(flag);
    }
}