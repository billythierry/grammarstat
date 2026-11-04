using System.Collections;
using TMPro;
using UnityEngine;

// Transisi masuk level: layar hitam dengan nama materi (mis. Simple Present Tense), lalu fade ke gameplay
public class LevelIntro : MonoBehaviour
{
    [Tooltip("Layar hitam penutup seluruh layar")]
    [SerializeField] private CanvasGroup fader;
    [Tooltip("Kelompok teks intro (judul + subjudul), di-fade terpisah dari layar hitam")]
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [Tooltip("Durasi teks muncul / hilang")]
    [SerializeField] private float textFadeDuration = 0.8f;
    [Tooltip("Lama teks tampil penuh")]
    [SerializeField] private float holdDuration = 1.8f;
    [Tooltip("Durasi layar hitam memudar ke gameplay")]
    [SerializeField] private float screenFadeDuration = 1f;

    void Awake()
    {
        // Mulai hitam agar gameplay tidak sempat terlihat sebelum intro
        if (fader != null)
        {
            fader.alpha = 1f;
            fader.blocksRaycasts = true;
        }
        if (textGroup != null) textGroup.alpha = 0f;
    }

    public IEnumerator Play(string title, string subtitle)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;

        if (textGroup != null)
        {
            yield return Fade(textGroup, 1f, textFadeDuration);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Fade(textGroup, 0f, textFadeDuration);
        }
        if (fader != null)
        {
            yield return Fade(fader, 0f, screenFadeDuration);
            fader.blocksRaycasts = false;
        }
    }

    // Layar kembali hitam sebelum pindah scene (ke menu atau level berikutnya)
    public IEnumerator FadeOut()
    {
        if (fader == null) yield break;
        fader.blocksRaycasts = true;
        yield return Fade(fader, 1f, screenFadeDuration * 0.6f);
    }

    static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += FrameStep())
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    // Langkah waktu per frame dibatasi agar frame pertama yang lambat (saat scene dimuat) tidak melompati fade
    static float FrameStep()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
    }
}
