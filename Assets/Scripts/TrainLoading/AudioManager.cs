using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip trainMoveSound;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Menjaga agar tidak hancur saat ganti scene
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
}
