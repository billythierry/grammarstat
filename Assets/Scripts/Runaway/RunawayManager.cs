using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Mengatur alur Runaway:
// kereta masuk dari kanan -> bandit muncul -> dialog pembuka -> panduan
// -> kereta terus terdorong ke arah bandit sambil soal tense muncul satu per satu
// -> benar: kereta meluncur ke kiri | salah / waktu habis: koreksi jawaban, kereta tidak mendapat dorongan
// -> menang: bertahan sampai soal terakhir, kereta sampai tujuan | kalah: kereta menyentuh garis jangkauan bandit
// -> panel hasil: nilai per tense dan bintang dari jumlah jawaban benar
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
    [Tooltip("Panel hasil di akhir permainan (nilai per tense, bintang, tombol Menu dan Lanjut / Ulangi)")]
    [SerializeField] private ResultPanel resultPanel;
    [Tooltip("Layar debug F3 untuk memantau bobot PCG (opsional)")]
    [SerializeField] private DebugOverlay debugOverlay;

    [Header("Intro & panduan")]
    [Tooltip("Transisi layar hitam ke gameplay saat scene dibuka (opsional)")]
    [SerializeField] private LevelIntro levelIntro;
    [Tooltip("Panel panduan yang tampil setelah dialog pembuka. Permainan baru dimulai setelah panduan ditutup. Kosongkan untuk langsung bermain")]
    [SerializeField] private GuidePanel guidePanel;
    [Tooltip("Dialog box yang tampil setelah bandit muncul, sebelum panduan (opsional)")]
    [SerializeField] private DialogPopup openingDialog;
    [Tooltip("Isi dialog pembuka. Pembicara kosong = narasi")]
    [SerializeField] private DialogLine[] openingLines =
    {
        new DialogLine { speaker = "", text = "Di tengah perjalanan, sekelompok bandit muncul dan mulai mengejar kereta." },
        new DialogLine { speaker = "Masinis", text = "Gawat! Aku harus mengirim pesan bahaya dan menjauh dari para bandit itu!" },
        new DialogLine { speaker = "", text = "Masinis menyalakan sinyal SOS ke stasiun terdekat. Lalu melajukan kereta secepat mungkin." }
    };

    [Header("Soal")]
    [SerializeField] private TenseQuestionBank questionBank;
    [Tooltip("Jumlah soal dalam satu permainan. Kereta selamat bila bertahan sampai soal terakhir")]
    [SerializeField] private int totalQuestions = 15;
    [Tooltip("Batas waktu menjawab per soal (detik). Waktu habis dihitung salah")]
    [SerializeField] private float questionTime = 10f;
    [Tooltip("Jeda setelah jawaban benar sebelum soal berikutnya")]
    [SerializeField] private float correctPause = 0.6f;
    [Tooltip("Lama koreksi jawaban tampil setelah jawaban salah. Kereta tetap terdorong selama jeda ini")]
    [SerializeField] private float wrongPause = 1.5f;
    [Tooltip("Lama adegan kereta mogok sebelum bandit menyusul (detik)")]
    [SerializeField] private float stallDuration = 1.5f;
    [Tooltip("Jeda setelah panduan ditutup sebelum soal pertama")]
    [SerializeField] private float startDelay = 0.5f;
    [Tooltip("Lama notifikasi akhir (Sampai Tujuan / Game Over) tampil sebelum panel hasil muncul")]
    [SerializeField] private float resultDelay = 1.5f;

    [Header("Bintang (hanya bila kereta selamat sampai tujuan)")]
    [Tooltip("Minimal jawaban benar untuk 1 bintang, sekaligus syarat lulus")]
    [SerializeField] private int oneStarScore = 7;
    [Tooltip("Minimal jawaban benar untuk 2 bintang")]
    [SerializeField] private int twoStarScore = 10;
    [Tooltip("Minimal jawaban benar untuk 3 bintang")]
    [SerializeField] private int threeStarScore = 15;

    [Header("PCG Weighted Random (bobot per tense)")]
    [SerializeField] private int initialWeight = 25;
    [SerializeField] private int minWeight = 5;
    [SerializeField] private int maxWeight = 45;
    [Tooltip("Perubahan bobot tiap jawaban: benar berkurang, salah / waktu habis bertambah")]
    [SerializeField] private int weightStep = 5;

    public int CorrectCount { get; private set; }
    // Nomor soal yang sedang / terakhir tampil, mulai dari 1
    public int QuestionNumber { get; private set; }
    public bool IsFinished { get; private set; }
    // Bintang hasil permainan, 0 bila tertangkap bandit atau jawaban benar di bawah batas 1 bintang
    public int Stars { get; private set; }

    public TenseQuestion CurrentQuestion { get; private set; }

    private WeightedRandomSelector tenseSelector;
    // Soal yang belum keluar per tense. Bila habis, diisi ulang dari bank karena jumlah soal per permainan tidak tetap
    private readonly Dictionary<string, List<TenseQuestion>> questionPool = new Dictionary<string, List<TenseQuestion>>();
    private readonly Dictionary<string, List<TenseQuestion>> bankByTense = new Dictionary<string, List<TenseQuestion>>();
    private readonly Dictionary<string, int> tenseCorrect = new Dictionary<string, int>();
    private readonly Dictionary<string, int> tenseTotal = new Dictionary<string, int>();
    private TenseQuestion lastQuestion;
    private Tense? chosenAnswer;
    private bool answered;
    private Coroutine gameRoutine;
    // Jumlah jawaban salah (termasuk waktu habis)
    private int wrongCount;

    // Batas salah sebelum kereta mogok: saat jawaban benar sudah tidak mungkin mencapai batas lulus.
    // 15 soal dengan batas lulus 7 berarti mogok di salah ke-9
    public int MaxWrongAnswers => Mathf.Max(1, totalQuestions - oneStarScore + 1);

    void Start()
    {
        quizUI.AnswerSelected += OnAnswerSelected;
        train.ReachedBandit += OnReachedBandit;

        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("RunawayManager: Question Bank belum diisi atau kosong");
            return;
        }
        BuildQuestionPool();
        if (debugOverlay != null) debugOverlay.TextProvider = BuildDebugText;
        CorrectCount = 0;
        QuestionNumber = 0;
        wrongCount = 0;
        IsFinished = false;
        StartCoroutine(IntroRoutine());
    }

    // Layar hitam memudar, kereta dan bandit masuk, dialog pembuka, lalu panduan. Tanpa panduan, permainan langsung dimulai
    IEnumerator IntroRoutine()
    {
        if (levelIntro != null) yield return levelIntro.Play("", "");
        yield return train.Enter();
        yield return bandits.Enter();
        if (openingDialog != null && openingLines.Length > 0) yield return openingDialog.Play(openingLines);

        if (guidePanel != null)
        {
            guidePanel.Closed += BeginGame;
            guidePanel.Show();
        }
        else
        {
            BeginGame();
        }
    }

    // Soal pertama dan dorongan ke arah bandit baru dimulai setelah panduan ditutup
    void BeginGame()
    {
        if (guidePanel != null) guidePanel.Closed -= BeginGame;
        gameRoutine = StartCoroutine(GameRoutine());
    }

    void OnDestroy()
    {
        if (quizUI != null) quizUI.AnswerSelected -= OnAnswerSelected;
        if (train != null) train.ReachedBandit -= OnReachedBandit;
        if (guidePanel != null) guidePanel.Closed -= BeginGame;
    }

    IEnumerator GameRoutine()
    {
        yield return new WaitForSeconds(startDelay);
        train.StartDrift(bandits.ReachLine);

        while (QuestionNumber < totalQuestions)
        {
            TenseQuestion question = PickQuestion();
            CurrentQuestion = question;
            QuestionNumber++;
            answered = false;
            chosenAnswer = null;
            quizUI.ShowQuestion(question);

            for (float t = 0f; t < questionTime && !answered; t += Time.deltaTime)
            {
                quizUI.SetTimer(1f - t / questionTime);
                yield return null;
            }
            if (!answered) quizUI.SetTimer(0f);

            // Waktu habis dihitung salah: bobot tense ikut naik
            bool correct = answered && chosenAnswer == question.tense;
            string tenseName = TenseNames.Get(question.tense);
            tenseTotal[tenseName]++;
            if (correct) tenseCorrect[tenseName]++;
            tenseSelector.ApplyAnswer(tenseName, correct);
            Debug.Log("PCG: " + question.id + " (" + tenseName + ") " + (correct ? "benar" : answered ? "salah" : "waktu habis") + " -> " + tenseSelector.Describe());
            quizUI.ShowResult(chosenAnswer, question.tense);
            if (correct)
            {
                CorrectCount++;
                train.Boost();
                notification.ShowCorrect("<b>Correct !!</b>");
                yield return new WaitForSeconds(correctPause);
            }
            else
            {
                wrongCount++;
                // Kemenangan sudah tidak mungkin: kereta langsung mogok dan tertangkap bandit
                if (wrongCount >= MaxWrongAnswers)
                {
                    yield return Breakdown();
                    yield break;
                }
                string title = answered ? "Wrong !!" : "Time's Up !!";
                notification.ShowWrong("<b>" + title + "</b>\n<size=65%><color=#FFFFFF>Jawaban: " + TenseNames.Get(question.tense) + "</color></size>");
                yield return new WaitForSeconds(wrongPause);
            }
        }

        yield return Win();
    }

    // Soal dikelompokkan per tense (urutan sesuai enum Tense). Bobot dibuat baru, jadi setiap percobaan mulai dari bobot awal
    void BuildQuestionPool()
    {
        var tenses = new List<string>();
        foreach (Tense tense in System.Enum.GetValues(typeof(Tense)))
        {
            string name = TenseNames.Get(tense);
            var group = questionBank.questions.FindAll(q => q.tense == tense);
            if (group.Count == 0)
            {
                Debug.LogWarning("RunawayManager: bank tidak punya soal " + name);
                continue;
            }
            tenses.Add(name);
            bankByTense[name] = group;
            questionPool[name] = new List<TenseQuestion>(group);
            tenseCorrect[name] = 0;
            tenseTotal[name] = 0;
        }
        tenseSelector = new WeightedRandomSelector(tenses, initialWeight, minWeight, maxWeight, weightStep);
    }

    // Tense dipilih dengan Weighted Random, lalu satu soal diambil acak dari tense itu dan dikeluarkan dari pool.
    // Pool tense yang habis diisi ulang; soal yang sama tidak muncul dua kali berturut-turut
    TenseQuestion PickQuestion()
    {
        string tense = tenseSelector.Pick();
        List<TenseQuestion> group = questionPool[tense];
        if (group.Count == 0) group.AddRange(bankByTense[tense]);

        int index = Random.Range(0, group.Count);
        if (group.Count > 1 && group[index] == lastQuestion) index = (index + 1) % group.Count;
        lastQuestion = group[index];
        group.RemoveAt(index);
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
        notification.ShowCorrect("<b>Berhasil kabur!!</b>\n<size=65%><color=#FFFFFF>Kereta lolos dari kejaran bandit</color></size>", true);
        yield return train.Escape();
        yield return ShowResult(true);
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
        notification.ShowWrong("<b>GAME OVER</b>\n<size=65%><color=#FFFFFF>Kereta dihentikan bandit di soal " + QuestionNumber + "/" + totalQuestions + "</color></size>", true);
        StartCoroutine(bandits.CatchUp());

        // Background melambat sampai berhenti: kereta sudah tertangkap
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            SetBackgroundSpeed(1f - t);
            yield return null;
        }
        SetBackgroundSpeed(0f);
        yield return ShowResult(false);
    }

    // Adegan kereta mogok: mesin mati, background melambat sampai berhenti, kereta tertinggal, lalu bandit menyusul
    IEnumerator Breakdown()
    {
        IsFinished = true;
        quizUI.Hide();
        notification.ShowWrong("<b>KERETA MOGOK!</b>\n<size=65%><color=#FFFFFF>Salah " + wrongCount + " kali, bandit berhasil menyusul</color></size>", true);

        StartCoroutine(train.Stall(stallDuration));
        for (float t = 0f; t < stallDuration; t += Time.deltaTime)
        {
            SetBackgroundSpeed(1f - t / stallDuration);
            yield return null;
        }
        SetBackgroundSpeed(0f);

        yield return bandits.CatchUp();
        yield return ShowResult(false);
    }


    // Panel hasil seperti chapter sebelumnya: nilai per tense dan bintang. Tertangkap bandit selalu tidak lulus
    IEnumerator ShowResult(bool escaped)
    {
        Stars = escaped ? CountStars() : 0;
        bool passed = Stars > 0;
        Debug.Log("Runaway selesai: " + (escaped ? "sampai tujuan" : "tertangkap di soal " + QuestionNumber)
            + ", benar " + CorrectCount + "/" + totalQuestions + ", bintang " + Stars);

        yield return new WaitForSeconds(resultDelay);
        // Sound effect menang / kalah, bersamaan dengan panel hasil muncul
        if (AudioManager.Instance != null)
        {
            if (passed) AudioManager.Instance.PlayVictorySound();
            else AudioManager.Instance.PlayDefeatSound();
        }
        if (resultPanel == null) yield break;

        var lines = new List<string>();
        foreach (string tense in tenseSelector.Categories)
            if (tenseTotal[tense] > 0) lines.Add(tense + " : " + tenseCorrect[tense] + "/" + tenseTotal[tense]);

        notification.Hide();
        // Runaway hanya punya 1 level (level terakhir): Lanjut memutar cutscene akhir (End Cutscene di ResultPanel)
        resultPanel.Show(passed, string.Join("\n", lines), Stars, false);
    }

    // Di bawah batas 1 bintang berarti tidak lulus (0 bintang)
    int CountStars()
    {
        if (CorrectCount >= threeStarScore) return 3;
        if (CorrectCount >= twoStarScore) return 2;
        if (CorrectCount >= oneStarScore) return 1;
        return 0;
    }

    void SetBackgroundSpeed(float multiplier)
    {
        foreach (ScrollingBackground bg in backgrounds) bg.SpeedMultiplier = multiplier;
    }

    // Bobot dan rentang yang ditampilkan adalah kondisi untuk pengundian soal berikutnya
    string BuildDebugText()
    {
        var text = new StringBuilder();
        text.Append("Grammar Station - PCG Weighted Random\n");
        text.Append("Runaway (").Append(questionBank.name).Append(")\n");
        text.Append("Soal: ").Append(QuestionNumber).Append("/").Append(totalQuestions)
            .Append(" | Benar: ").Append(CorrectCount).Append("\n\n");

        int total = tenseSelector.TotalWeight;
        text.Append("Total bobot: ").Append(total).Append(" (acak 0-").Append(total - 1).Append(")\n");
        foreach (string tense in tenseSelector.Categories)
        {
            int weight = tenseSelector.GetWeight(tense);
            int start = tenseSelector.GetRangeStart(tense);
            text.Append("\n").Append(tense).Append("\n");
            text.Append("Bobot ").Append(weight)
                .Append(" | Rentang ").Append(start).Append("-").Append(start + weight - 1)
                .Append(" | Peluang ").Append((tenseSelector.GetChance(tense) * 100f).ToString("0.0")).Append("%\n");
            text.Append("Benar ").Append(tenseCorrect[tense]).Append("/").Append(tenseTotal[tense])
                .Append(" | Sisa soal ").Append(questionPool[tense].Count).Append("\n");
        }

        if (tenseSelector.LastRoll >= 0)
        {
            text.Append("\nUndian terakhir: ").Append(tenseSelector.LastRoll)
                .Append(" dari 0-").Append(tenseSelector.LastRollTotal - 1)
                .Append(" -> ").Append(tenseSelector.LastPick).Append("\n");
        }
        if (CurrentQuestion != null && !IsFinished)
            text.Append("Soal: ").Append(CurrentQuestion.id).Append(" | kunci: ").Append(TenseNames.Get(CurrentQuestion.tense)).Append("\n");
        return text.ToString().TrimEnd('\n');
    }
}
