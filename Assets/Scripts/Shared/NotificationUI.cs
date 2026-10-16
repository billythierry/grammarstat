using System.Collections;
using TMPro;
using UnityEngine;

// Panel notifikasi di layar (jawaban benar/salah, info) dengan efek fade in/out
[RequireComponent(typeof(CanvasGroup))]
public class NotificationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Lama notifikasi tampil (detik) sebelum menghilang")]
    [SerializeField] private float defaultDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Warna teks")]
    [SerializeField] private Color correctColor = new Color(0.45f, 0.95f, 0.5f);
    [SerializeField] private Color wrongColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private Color infoColor = Color.white;

    private CanvasGroup group;
    private Coroutine routine;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // stay = true: notifikasi tidak hilang otomatis
    public void ShowCorrect(string message, bool stay = false) { Show(message, correctColor, stay ? 0f : defaultDuration); }
    public void ShowWrong(string message, bool stay = false) { Show(message, wrongColor, stay ? 0f : defaultDuration); }
    public void ShowInfo(string message, bool stay = false) { Show(message, infoColor, stay ? 0f : defaultDuration); }

    // duration <= 0: notifikasi tetap tampil sampai Hide() atau Show() berikutnya
    public void Show(string message, Color color, float duration)
    {
        messageText.text = message;
        messageText.color = color;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(duration));
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(0f));
    }

    IEnumerator ShowRoutine(float duration)
    {
        yield return Fade(1f);
        if (duration <= 0f) yield break;
        yield return new WaitForSeconds(duration);
        yield return Fade(0f);
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
    }
}
