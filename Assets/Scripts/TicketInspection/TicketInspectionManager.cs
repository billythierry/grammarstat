using System.Collections.Generic;
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
    [Tooltip("Teks penghitung penumpang, mis. 1/20")]
    [SerializeField] private TMP_Text passengerCounterText;

    [Header("Soal")]
    [SerializeField] private TicketBank ticketBank;
    [Tooltip("Jumlah penumpang per level. 0 = semua tiket di bank")]
    [SerializeField] private int passengerCount = 20;
    [Tooltip("Minimal jawaban benar untuk lulus level")]
    [SerializeField] private int passingScore = 10;
    [Tooltip("Acak urutan tiket setiap kali level dimulai")]
    [SerializeField] private bool shuffleTickets = true;

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

    // Tiket milik penumpang yang sedang di loket
    public TicketData CurrentTicket { get; private set; }
    public int CurrentPassengerNumber => Mathf.Clamp(orderIndex + 1, 1, Mathf.Max(1, ticketOrder.Count));
    public int TotalPassengers => ticketOrder.Count;
    public int CorrectCount { get; private set; }
    public bool IsFinished { get; private set; }

    private PassengerController passenger;
    private readonly List<int> ticketOrder = new List<int>();
    private int orderIndex = -1;

    void Start()
    {
        ticket.Clicked += OnTicketClicked;
        detailUI.AnswerSelected += OnAnswerSelected;

        if (ticketBank == null || ticketBank.tickets.Count == 0)
        {
            Debug.LogError("TicketInspectionManager: Ticket Bank belum diisi atau kosong");
            return;
        }
        BuildTicketOrder();
        CorrectCount = 0;
        IsFinished = false;
        NextPassenger();
    }

    void OnDestroy()
    {
        if (ticket != null) ticket.Clicked -= OnTicketClicked;
        if (detailUI != null) detailUI.AnswerSelected -= OnAnswerSelected;
    }

    void BuildTicketOrder()
    {
        ticketOrder.Clear();
        for (int i = 0; i < ticketBank.tickets.Count; i++) ticketOrder.Add(i);
        if (shuffleTickets)
        {
            for (int i = ticketOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = ticketOrder[i];
                ticketOrder[i] = ticketOrder[j];
                ticketOrder[j] = tmp;
            }
        }

        int count = passengerCount > 0 ? passengerCount : ticketOrder.Count;
        if (count > ticketOrder.Count)
        {
            Debug.LogWarning("TicketInspectionManager: level butuh " + count + " penumpang, tapi bank hanya punya " + ticketOrder.Count + " tiket");
            count = ticketOrder.Count;
        }
        ticketOrder.RemoveRange(count, ticketOrder.Count - count);
        orderIndex = -1;
    }

    void NextPassenger()
    {
        ticket.Hide();

        orderIndex++;
        if (orderIndex >= ticketOrder.Count)
        {
            FinishLevel();
            return;
        }
        CurrentTicket = ticketBank.tickets[ticketOrder[orderIndex]];
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
        if (correct) CorrectCount++;
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
        orderIndex = ticketOrder.Count - 1;
        UpdatePassengerCounter();

        bool passed = CorrectCount >= passingScore;
        if (notification != null)
        {
            string result = "<b>Pemeriksaan Selesai!</b>\n<size=65%>Benar " + CorrectCount + " dari " + TotalPassengers + " penumpang - "
                + (passed ? "LULUS" : "TIDAK LULUS") + " (minimal " + passingScore + ")</size>";
            if (passed) notification.ShowCorrect(result, true);
            else notification.ShowWrong(result, true);
        }
    }

    void UpdatePassengerCounter()
    {
        if (passengerCounterText != null) passengerCounterText.text = CurrentPassengerNumber + "/" + TotalPassengers;
    }
}
