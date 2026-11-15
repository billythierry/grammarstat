using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Notifikasi jawaban benar/salah di tengah layar bertema peti muatan: cap centang/silang, judul, dan kalimat
[RequireComponent(typeof(CanvasGroup))]
public class AnswerPopup : MonoBehaviour
{
    [Tooltip("Papan peti, diberi animasi membesar saat muncul")]
    [SerializeField] private RectTransform card;
    [SerializeField] private Image stamp;
    [SerializeField] private Sprite correctStamp;
    [SerializeField] private Sprite wrongStamp;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text sentenceText;

    [Header("Teks")]
    [SerializeField] private string correctTitle = "Jawaban Benar!";
    [SerializeField] private string wrongTitle = "Jawaban Salah!";
    [Tooltip("Label di atas kalimat pemain saat benar")]
    [SerializeField] private string correctLabel = "Kalimatmu:";
    [Tooltip("Label di atas kalimat yang benar saat salah")]
    [SerializeField] private string wrongLabel = "Jawaban yang benar:";
    [SerializeField] private Color correctColor = new Color32(0x2F, 0x8A, 0x3C, 0xFF);
    [SerializeField] private Color wrongColor = new Color32(0xB3, 0x33, 0x28, 0xFF);

    [Header("Animasi")]
    [Tooltip("Lama tampil (detik) sebelum menghilang, bila tidak diminta tetap tampil")]
    [SerializeField] private float defaultDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.2f;
    [Tooltip("Jeda sebelum cap muncul setelah papan tampil")]
    [SerializeField] private float stampDelay = 0.1f;

    private CanvasGroup group;
    private Coroutine routine;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        SetVisible(false);
        group.alpha = 0f;
    }

    // stay = true: tetap tampil sampai Hide() (mis. selama kereta pindah gerbong)
    public void Show(bool correct, string sentence, bool stay)
    {
        Show(correct, null, sentence, stay);
    }

    // label: teks di atas kalimat; null memakai Correct Label / Wrong Label
    public void Show(bool correct, string label, string sentence, bool stay)
    {
        Color color = correct ? correctColor : wrongColor;
        if (titleText != null)
        {
            titleText.text = correct ? correctTitle : wrongTitle;
            titleText.color = color;
        }
        if (labelText != null) labelText.text = label ?? (correct ? correctLabel : wrongLabel);
        if (sentenceText != null) sentenceText.text = sentence;
        if (stamp != null)
        {
            stamp.sprite = correct ? correctStamp : wrongStamp;
            stamp.rectTransform.localScale = Vector3.zero;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(stay ? 0f : defaultDuration));
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        if (group.alpha <= 0f) return;
        routine = StartCoroutine(FadeOut());
    }

    IEnumerator ShowRoutine(float duration)
    {
        SetVisible(true);
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeDuration;
            group.alpha = Mathf.Lerp(start, 1f, k);
            if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        group.alpha = 1f;
        if (card != null) card.localScale = Vector3.one;

        // Cap dihentakkan: muncul besar lalu mengecil ke ukuran normal
        if (stamp != null)
        {
            yield return new WaitForSecondsRealtime(stampDelay);
            const float stampDuration = 0.22f;
            for (float t = 0f; t < stampDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / stampDuration;
                stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, k * k);
                yield return null;
            }
            stamp.rectTransform.localScale = Vector3.one;
        }

        if (duration <= 0f) yield break;
        yield return new WaitForSeconds(duration);
        yield return FadeOut();
    }

    IEnumerator FadeOut()
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }
        group.alpha = 0f;
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        group.blocksRaycasts = visible;
        group.interactable = false;
    }
}
