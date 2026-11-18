using System.Collections;
using TMPro;
using UnityEngine;

// Transisi masuk level: layar hitam dengan nama chapter (mis. Chapter 1 / Train Loading) dan/atau level beserta materinya
// (mis. Level 1 / Simple Present Tense), lalu fade ke gameplay
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

    [Header("Chapter")]
    [Tooltip("Nomor chapter scene ini, tampil di subjudul intro chapter, mis. Chapter 1")]
    [SerializeField] private int chapterNumber = 1;
    [SerializeField] private string chapterName = "Train Loading";

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

    // Intro chapter saja: subjudul "Chapter N", judul nama chapter (mis. Chapter 2 / Ticket Inspection)
    public IEnumerator PlayChapter()
    {
        yield return ShowText(chapterName, "Chapter " + chapterNumber);
        yield return RevealScreen();
    }

    // Intro level: subjudul "Level N", judul materinya. Bila withChapter aktif (level pertama),
    // intro chapter tampil lebih dulu di layar hitam yang sama
    public IEnumerator PlayLevel(int level, string material, bool withChapter)
    {
        if (withChapter) yield return ShowText(chapterName, "Chapter " + chapterNumber);
        yield return ShowText(material, "Level " + level);
        yield return RevealScreen();
    }

    // Teks muncul, bertahan, lalu hilang; layar tetap hitam
    IEnumerator ShowText(string title, string subtitle)
    {
        if (textGroup == null) yield break;
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;

        yield return Fade(textGroup, 1f, textFadeDuration);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return Fade(textGroup, 0f, textFadeDuration);
    }

    // Layar hitam memudar ke gameplay
    IEnumerator RevealScreen()
    {
        if (fader == null) yield break;
        yield return Fade(fader, 0f, screenFadeDuration);
        fader.blocksRaycasts = false;
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
