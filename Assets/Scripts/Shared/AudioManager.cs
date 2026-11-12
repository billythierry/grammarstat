using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip trainMoveSound;
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip paperSlideSound;
    [SerializeField] private AudioClip paperCollectSound;

    // AudioSource khusus suara UI: tetap berbunyi saat game di-pause (AudioListener.pause)
    private AudioSource uiSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Menjaga agar tidak hancur saat ganti scene
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.ignoreListenerPause = true;
            if (audioSource != null)
            {
                uiSource.volume = audioSource.volume;
                uiSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayTrainMoveSound()
    {
        audioSource.PlayOneShot(trainMoveSound);
    }

    public void PlayCorrectSound()
    {
        audioSource.PlayOneShot(correctSound);
    }

    public void PlayWrongSound()
    {
        audioSource.PlayOneShot(wrongSound);
    }

    public void PlayButtonClickSound()
    {
        if (buttonClickSound == null) return;
        (uiSource != null ? uiSource : audioSource).PlayOneShot(buttonClickSound);
    }

    public void PlayVictorySound()
    {
        audioSource.PlayOneShot(victorySound);
    }

    public void PlayDefeatSound()
    {
        audioSource.PlayOneShot(defeatSound);
    }

    public void PlayPaperSlideSound()
    {
        if (paperSlideSound != null) audioSource.PlayOneShot(paperSlideSound);
    }

    public void PlayPaperCollectSound()
    {
        if (paperCollectSound != null) audioSource.PlayOneShot(paperCollectSound);
    }


}
