using UnityEngine;
using UnityEngine.UI;

// Pasang di Canvas: semua Button di dalamnya (termasuk yang nonaktif) berbunyi saat diklik
public class ButtonClickSound : MonoBehaviour
{
    void Awake()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
            button.onClick.AddListener(Play);
    }

    void Play()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClickSound();
    }
}
