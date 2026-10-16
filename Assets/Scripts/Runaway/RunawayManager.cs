using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mengatur alur Runaway:
// kereta masuk dari kanan -> bandit muncul -> kereta terus terdorong ke arah bandit sambil soal tense muncul
// -> benar: kereta meluncur ke kiri sejenak | salah / waktu habis: koreksi jawaban, kereta tetap terdorong
// -> menang: jawaban benar mencapai target, kereta kabur ke kiri | kalah: kereta menyentuh garis jangkauan bandit
public class RunawayManager : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private RunawayTrain train;
    [SerializeField] private BanditGroup bandits;
    [SerializeField] private RunawayQuizUI quizUI;
    [Tooltip("Panel UI untuk notifikasi benar/salah, Game Over, dan menang")]
    [SerializeField] private NotificationUI notification;
    [Tooltip("Semua layer background yang bergerak")]
    [SerializeField] private ScrollingBackground[] backgrounds;
    [Tooltip("Teks penghitung jawaban benar, mis. Benar 3/10")]
    [SerializeField] private TMP_Text correctCounterText;
    [Tooltip("Tombol main lagi, muncul setelah menang / kalah")]
    [SerializeField] private GameObject retryButton;

    [Header("Soal")]
    [SerializeField] private TenseQuestionBank questionBank;
    [Tooltip("Jumlah jawaban benar untuk berhasil kabur")]
    [SerializeField] private int targetCorrect = 10;
    [Tooltip("Batas waktu menjawab per soal (detik). Waktu habis dihitung salah")]
    [SerializeField] private float questionTime = 7f;
    [Tooltip("Jeda setelah jawaban benar sebelum soal berikutnya")]
    [SerializeField] private float correctPause = 0.6f;
    [Tooltip("Lama koreksi jawaban tampil setelah jawaban salah. Kereta tetap terdorong selama jeda ini")]
    [SerializeField] private float wrongPause = 1.5f;
    [Tooltip("Jeda setelah bandit muncul sebelum soal pertama")]
    [SerializeField] private float startDelay = 0.5f;

    public int CorrectCount { get; private set; }
    public bool IsFinished { get; private set; }

    private readonly List<TenseQuestion> queue = new List<TenseQuestion>();
    private TenseQuestion lastQuestion;
    private Tense? chosenAnswer;
    private bool answered;
    private Coroutine gameRoutine;

    void Start()
    {
        quizUI.AnswerSelected += OnAnswerSelected;
        train.ReachedBandit += OnReachedBandit;
        if (retryButton != null) retryButton.SetActive(false);

        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("RunawayManager: Question Bank belum diisi atau kosong");
            return;
        }
        CorrectCount = 0;
        IsFinished = false;
        UpdateCounter();
        gameRoutine = StartCoroutine(GameRoutine());
    }

    void OnDestroy()
    {
        if (quizUI != null) quizUI.AnswerSelected -= OnAnswerSelected;
        if (train != null) train.ReachedBandit -= OnReachedBandit;
    }

    IEnumerator GameRoutine()
    {
        yield return train.Enter();
        yield return bandits.Enter();
        yield return new WaitForSeconds(startDelay);
        train.StartDrift(bandits.ReachLine);

        while (CorrectCount < targetCorrect)
        {
            TenseQuestion question = NextQuestion();
            answered = false;
            chosenAnswer = null;
            quizUI.ShowQuestion(question);

            for (float t = 0f; t < questionTime && !answered; t += Time.deltaTime)
            {
                quizUI.SetTimer(1f - t / questionTime);
                yield return null;
            }
            if (!answered) quizUI.SetTimer(0f);

            bool correct = answered && chosenAnswer == question.tense;
            quizUI.ShowResult(chosenAnswer, question.tense);
            if (correct)
            {
                CorrectCount++;
                UpdateCounter();
                train.Boost();
                notification.ShowCorrect("<b>Correct !!</b>");
                yield return new WaitForSeconds(correctPause);
            }
            else
            {
                string title = answered ? "Wrong !!" : "Time's Up !!";
                notification.ShowWrong("<b>" + title + "</b>\n<size=65%><color=#FFFFFF>Jawaban: " + TenseNames.Get(question.tense) + "</color></size>");
                yield return new WaitForSeconds(wrongPause);
            }
        }

        yield return Win();
    }

    // Soal diacak dan diulang bila bank habis; soal yang sama tidak muncul dua kali berturut-turut
    TenseQuestion NextQuestion()
    {
        if (queue.Count == 0)
        {
            queue.AddRange(questionBank.questions);
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                TenseQuestion tmp = queue[i];
                queue[i] = queue[j];
                queue[j] = tmp;
            }
            if (queue.Count > 1 && queue[0] == lastQuestion)
            {
                queue[0] = queue[queue.Count - 1];
                queue[queue.Count - 1] = lastQuestion;
            }
        }
        lastQuestion = queue[0];
        queue.RemoveAt(0);
        return lastQuestion;
    }

    void OnAnswerSelected(Tense tense)
    {
        if (answered || IsFinished) return;
        chosenAnswer = tense;
        answered = true;
    }

    IEnumerator Win()
    {
        IsFinished = true;
        quizUI.Hide();
        notification.ShowCorrect("<b>Berhasil Kabur!</b>\n<size=65%><color=#FFFFFF>Kereta lolos dari kejaran bandit</color></size>", true);
        yield return train.Escape();
        if (retryButton != null) retryButton.SetActive(true);
    }

    void OnReachedBandit()
    {
        if (IsFinished) return;
        IsFinished = true;
        if (gameRoutine != null) StopCoroutine(gameRoutine);
        StartCoroutine(Lose());
    }

    IEnumerator Lose()
    {
        quizUI.Hide();
        notification.ShowWrong("<b>GAME OVER</b>\n<size=65%><color=#FFFFFF>Kereta dirampok bandit! Benar " + CorrectCount + "/" + targetCorrect + "</color></size>", true);
        StartCoroutine(bandits.CatchUp());

        // Background melambat sampai berhenti: kereta sudah tertangkap
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            SetBackgroundSpeed(1f - t);
            yield return null;
        }
        SetBackgroundSpeed(0f);
        if (retryButton != null) retryButton.SetActive(true);
    }

    void SetBackgroundSpeed(float multiplier)
    {
        foreach (ScrollingBackground bg in backgrounds) bg.SpeedMultiplier = multiplier;
    }

    void UpdateCounter()
    {
        if (correctCounterText != null) correctCounterText.text = "Benar " + CorrectCount + "/" + targetCorrect;
    }

    // Dipasang pada OnClick tombol Retry. Scene harus terdaftar di Build Settings
    public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
