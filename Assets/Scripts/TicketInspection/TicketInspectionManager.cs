using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Mengatur alur Ticket Inspection:
// penumpang datang -> tiket keluar dari slot -> klik tiket -> detail tiket + tombol jawaban
// -> notifikasi benar/salah -> penumpang lanjut ke kanan (benar) atau ditolak ke kiri (salah) -> Destroy
public class TicketInspectionManager : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Prefab penumpang. Satu objek dibuat per penumpang dan di-Destroy setelah diperiksa")]
    [SerializeField] private PassengerController passengerPrefab;
    [SerializeField] private TicketItem ticket;
    [SerializeField] private TicketDetailUI detailUI;
    [SerializeField] private Transform ticketSlot;
    [Tooltip("Panel UI untuk notifikasi jawaban benar/salah")]
    [SerializeField] private NotificationUI notification;
    [Tooltip("Teks penghitung penumpang, mis. 1/10")]
    [SerializeField] private TMP_Text passengerCounterText;
    [Tooltip("Panel hasil akhir level. Bila kosong, hasil tampil di notifikasi")]
    [SerializeField] private ResultPanel resultPanel;
    [Tooltip("Layar debug F3 untuk memantau bobot PCG (opsional)")]
    [SerializeField] private DebugOverlay debugOverlay;

    [Header("Soal")]
    [Tooltip("Bank soal per level, urut dari Level 1. Level dipilih dari menu (LevelSelection.Level)")]
    [SerializeField] private TicketBank[] levelBanks;
    [Tooltip("Bank cadangan bila level yang dipilih belum punya bank")]
    [SerializeField] private TicketBank ticketBank;
    [Tooltip("Jumlah penumpang per level. 0 = semua tiket di bank")]
    [SerializeField] private int passengerCount = 10;

    [Header("PCG Weighted Random (bobot per tense)")]
    [SerializeField] private int initialWeight = 50;
    [SerializeField] private int minWeight = 20;
    [SerializeField] private int maxWeight = 80;
    [Tooltip("Perubahan bobot tiap jawaban: benar berkurang, salah bertambah")]
    [SerializeField] private int weightStep = 10;

    [Header("Bintang")]
    [Tooltip("Minimal jawaban benar untuk 1 bintang, sekaligus syarat lulus level")]
    [SerializeField] private int oneStarScore = 4;
    [Tooltip("Minimal jawaban benar untuk 2 bintang")]
    [SerializeField] private int twoStarScore = 7;
    [Tooltip("Minimal jawaban benar untuk 3 bintang")]
    [SerializeField] private int threeStarScore = 10;

    [Header("Posisi penumpang (world)")]
    [Tooltip("Posisi X di luar layar kiri: tempat penumpang muncul, juga arah pergi penumpang yang ditolak")]
    [SerializeField] private float enterX = -13f;
    [Tooltip("Posisi X di depan jendela")]
    [SerializeField] private float windowX = 0f;
    [Tooltip("Posisi X di luar layar kanan: arah pergi penumpang yang lolos pemeriksaan")]
    [SerializeField] private float exitX = 13f;
    [SerializeField] private float passengerY = -2.3f;
    [SerializeField] private float walkInDuration = 3.2f;
    [SerializeField] private float walkOutDuration = 3.2f;

    [Header("Tiket")]
    [Tooltip("Posisi tiket di atas meja setelah keluar dari slot")]
    [SerializeField] private Vector3 ticketDeskPosition = new Vector3(0f, -2.9f, 0f);
    [Tooltip("Jeda setelah penumpang sampai sebelum tiket keluar")]
    [SerializeField] private float ticketDelay = 0.3f;

    [Header("Intro & panduan")]
    [Tooltip("Transisi layar hitam ke gameplay saat scene dibuka (opsional)")]
    [SerializeField] private LevelIntro levelIntro;
    [Tooltip("Panel panduan yang tampil di awal level. Permainan baru dimulai setelah panduan ditutup. Kosongkan untuk langsung bermain")]
    [SerializeField] private GuidePanel guidePanel;

    // Tiket milik penumpang yang sedang di loket
    public TicketData CurrentTicket { get; private set; }
    public int CurrentPassengerNumber => Mathf.Clamp(passengersServed, 1, Mathf.Max(1, TotalPassengers));
    public int TotalPassengers { get; private set; }
    public int CorrectCount { get; private set; }
    public int Stars { get; private set; }
    public bool IsFinished { get; private set; }

    private PassengerController passenger;
    private WeightedRandomSelector tenseSelector;
    // Tiket yang belum keluar per tense, supaya soal tidak berulang dalam satu permainan
    private readonly Dictionary<string, List<TicketData>> ticketPool = new Dictionary<string, List<TicketData>>();
    // Urutan tense sesuai bank, juga dipakai sebagai urutan baris di panel hasil
    private readonly List<string> tenses = new List<string>();
    private readonly Dictionary<string, int> tenseCorrect = new Dictionary<string, int>();
    private readonly Dictionary<string, int> tenseTotal = new Dictionary<string, int>();
    private int passengersServed;

    void Start()
    {
        ticket.Clicked += OnTicketClicked;
        detailUI.AnswerSelected += OnAnswerSelected;

        int level = LevelSelection.Level;
        if (levelBanks != null && level >= 1 && level <= levelBanks.Length && levelBanks[level - 1] != null)
            ticketBank = levelBanks[level - 1];
        else
            Debug.LogWarning("TicketInspectionManager: Level " + level + " belum punya bank soal, memakai bank cadangan");

        if (ticketBank == null || ticketBank.tickets.Count == 0)
        {
            Debug.LogError("TicketInspectionManager: Ticket Bank belum diisi atau kosong");
            return;
        }
        BuildTicketPool();
        if (debugOverlay != null) debugOverlay.TextProvider = BuildDebugText;
        CorrectCount = 0;
        IsFinished = false;
        UpdatePassengerCounter();

        // Tiket di meja disembunyikan sampai penumpang pertama datang
        ticket.Hide();

        StartCoroutine(IntroRoutine());
    }

    // Layar hitam memudar dulu, lalu panduan muncul. Tanpa panduan, level langsung dimulai
    IEnumerator IntroRoutine()
    {
        if (levelIntro != null) yield return levelIntro.Play("", "");

        if (guidePanel != null)
        {
            guidePanel.Closed += BeginLevel;
            guidePanel.Show();
        }
        else
        {
            BeginLevel();
        }
    }

    void OnDestroy()
    {
        if (ticket != null) ticket.Clicked -= OnTicketClicked;
        if (detailUI != null) detailUI.AnswerSelected -= OnAnswerSelected;
        if (guidePanel != null) guidePanel.Closed -= BeginLevel;
    }

    // Penumpang pertama baru datang setelah panduan ditutup
    void BeginLevel()
    {
        if (guidePanel != null) guidePanel.Closed -= BeginLevel;
        NextPassenger();
    }

    // Tiket dikelompokkan per tense. Bobot dibuat baru di sini, jadi setiap level atau percobaan mulai dari bobot awal
    void BuildTicketPool()
    {
        ticketPool.Clear();
        tenses.Clear();
        tenseCorrect.Clear();
        tenseTotal.Clear();

        foreach (TicketData data in ticketBank.tickets)
        {
            if (!ticketPool.TryGetValue(data.tense, out List<TicketData> group))
            {
                group = new List<TicketData>();
                ticketPool[data.tense] = group;
                tenses.Add(data.tense);
                tenseCorrect[data.tense] = 0;
                tenseTotal[data.tense] = 0;
            }
            group.Add(data);
        }
        tenseSelector = new WeightedRandomSelector(tenses, initialWeight, minWeight, maxWeight, weightStep);

        int count = passengerCount > 0 ? passengerCount : ticketBank.tickets.Count;
        if (count > ticketBank.tickets.Count)
        {
            Debug.LogWarning("TicketInspectionManager: level butuh " + count + " penumpang, tapi bank hanya punya " + ticketBank.tickets.Count + " tiket");
            count = ticketBank.tickets.Count;
        }
        TotalPassengers = count;
        passengersServed = 0;
    }

    // Tense dipilih dengan Weighted Random, lalu satu tiket diambil acak dari tense itu dan dikeluarkan dari pool
    TicketData PickTicket()
    {
        var available = new List<string>();
        foreach (string tense in tenses)
            if (ticketPool[tense].Count > 0) available.Add(tense);

        string pickedTense = tenseSelector.Pick(available);
        List<TicketData> group = ticketPool[pickedTense];
        int index = Random.Range(0, group.Count);
        TicketData picked = group[index];
        group.RemoveAt(index);
        return picked;
    }

    void NextPassenger()
    {
        ticket.Hide();

        if (passengersServed >= TotalPassengers)
        {
            FinishLevel();
            return;
        }
        passengersServed++;
        CurrentTicket = PickTicket();
        UpdatePassengerCounter();

        // Penumpang baru dibuat dari prefab, masuk dari kiri menuju jendela
        Vector3 spawn = new Vector3(enterX, passengerY, passengerPrefab.transform.position.z);
        passenger = Instantiate(passengerPrefab, spawn, Quaternion.identity);
        passenger.name = "Passenger (" + CurrentTicket.passengerName + ")";
        passenger.WalkTo(new Vector3(windowX, passengerY, spawn.z), walkInDuration, OnPassengerArrived);
    }

    void OnPassengerArrived()
    {
        Invoke(nameof(GiveTicket), ticketDelay);
    }

    void GiveTicket()
    {
        ticket.Appear(ticketSlot.position, ticketDeskPosition);
    }

    void OnTicketClicked()
    {
        if (detailUI.IsOpen || CurrentTicket == null) return;
        ticket.IsClickable = false;
        detailUI.Show(CurrentTicket);
    }

    void OnAnswerSelected(string answer)
    {
        if (CurrentTicket == null || passenger == null) return;

        TicketAnswer chosen = answer == TicketDetailUI.AnswerA ? TicketAnswer.A
            : answer == TicketDetailUI.AnswerB ? TicketAnswer.B
            : TicketAnswer.NoError;
        bool correct = chosen == CurrentTicket.correctAnswer;
        if (correct)
        {
            CorrectCount++;
            tenseCorrect[CurrentTicket.tense]++;
        }
        tenseTotal[CurrentTicket.tense]++;
        tenseSelector.ApplyAnswer(CurrentTicket.tense, correct);
        Debug.Log("PCG: " + CurrentTicket.id + " (" + CurrentTicket.tense + ") " + (correct ? "benar" : "salah") + " -> " + tenseSelector.Describe());
        ShowFeedback(correct);

        detailUI.Hide();
        ticket.Hide();

        // Benar: penumpang lanjut naik kereta (ke kanan). Salah: penumpang ditolak (kembali ke kiri)
        PassengerController leaving = passenger;
        passenger = null;
        float targetX = correct ? exitX : enterX;
        Vector3 target = new Vector3(targetX, passengerY, leaving.transform.position.z);
        leaving.WalkAway(target, walkOutDuration, () =>
        {
            Destroy(leaving.gameObject);
            if (notification != null) notification.Hide();
            NextPassenger();
        });
    }

    void ShowFeedback(bool correct)
    {
        if (notification == null) return;

        // Notifikasi tetap tampil selama penumpang berjalan pergi
        if (correct)
        {
            notification.ShowCorrect("<b>Jawaban Benar!</b>", true);
            return;
        }

        string answerLabel = CurrentTicket.correctAnswer == TicketAnswer.NoError
            ? "No Error"
            : "(" + CurrentTicket.correctAnswer + ")";
        notification.ShowWrong("<b>Jawaban Salah!</b>\n<size=65%><color=#FFFFFF>Jawaban yang benar adalah " + answerLabel
            + "\n<i>" + CurrentTicket.correction + "</i></color></size>", true);
    }

    void FinishLevel()
    {
        IsFinished = true;
        CurrentTicket = null;
        UpdatePassengerCounter();

        Stars = CountStars();
        bool passed = Stars > 0;
        Debug.Log("Level selesai: benar " + CorrectCount + "/" + TotalPassengers + ", bintang " + Stars);

        if (resultPanel != null)
        {
            var lines = new List<string>();
            foreach (string tense in tenses)
                if (tenseTotal[tense] > 0) lines.Add(tense + " : " + tenseCorrect[tense] + "/" + tenseTotal[tense]);

            if (notification != null) notification.Hide();
            bool hasNextLevel = levelBanks != null && LevelSelection.Level < levelBanks.Length;
            resultPanel.Show(passed, string.Join("\n", lines), Stars, hasNextLevel);
            return;
        }

        if (notification != null)
        {
            string result = "<b>Pemeriksaan Selesai!</b>\n<size=65%>Benar " + CorrectCount + " dari " + TotalPassengers + " penumpang - "
                + (passed ? "LULUS" : "TIDAK LULUS") + " (minimal " + oneStarScore + ")</size>";
            if (passed) notification.ShowCorrect(result, true);
            else notification.ShowWrong(result, true);
        }
    }

    // Di bawah batas 1 bintang berarti tidak lulus (0 bintang)
    int CountStars()
    {
        if (CorrectCount >= threeStarScore) return 3;
        if (CorrectCount >= twoStarScore) return 2;
        if (CorrectCount >= oneStarScore) return 1;
        return 0;
    }

    // Bobot dan rentang yang ditampilkan adalah kondisi untuk pengundian giliran berikutnya
    string BuildDebugText()
    {
        var text = new StringBuilder();
        text.Append("Grammar Station - PCG Weighted Random\n");
        text.Append("Level ").Append(LevelSelection.Level).Append(" (").Append(ticketBank.name).Append(")\n");
        text.Append("Penumpang: ").Append(CurrentPassengerNumber).Append("/").Append(TotalPassengers)
            .Append("   Benar: ").Append(CorrectCount).Append("\n\n");

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
                .Append(" | Sisa soal ").Append(ticketPool[tense].Count).Append("\n");
        }

        if (tenseSelector.LastRoll >= 0)
        {
            text.Append("\nUndian terakhir: ").Append(tenseSelector.LastRoll)
                .Append(" dari 0-").Append(tenseSelector.LastRollTotal - 1)
                .Append(" -> ").Append(tenseSelector.LastPick).Append("\n");
        }
        if (CurrentTicket != null)
            text.Append("Soal: ").Append(CurrentTicket.id).Append(" | kunci: ").Append(CurrentTicket.correctAnswer).Append("\n");
        return text.ToString().TrimEnd('\n');
    }

    void UpdatePassengerCounter()
    {
        if (passengerCounterText != null) passengerCounterText.text = CurrentPassengerNumber + "/" + TotalPassengers;
    }
}
