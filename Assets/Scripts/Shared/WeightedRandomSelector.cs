using System.Collections.Generic;
using UnityEngine;

// Algoritma Weighted Random untuk PCG: memilih kategori soal secara acak sesuai bobotnya.
// Jawaban salah menaikkan bobot kategori soal itu, jawaban benar menurunkannya,
// sehingga materi yang belum dikuasai pemain lebih sering muncul
public class WeightedRandomSelector
{
    private readonly List<string> categories = new List<string>();
    private readonly Dictionary<string, int> weights = new Dictionary<string, int>();
    private readonly int minWeight;
    private readonly int maxWeight;
    private readonly int step;

    public IReadOnlyList<string> Categories => categories;

    // Hasil pengundian terakhir, untuk layar debug
    public int LastRoll { get; private set; } = -1;
    public int LastRollTotal { get; private set; }
    public string LastPick { get; private set; }

    public int TotalWeight
    {
        get
        {
            int total = 0;
            foreach (string category in categories) total += weights[category];
            return total;
        }
    }

    public WeightedRandomSelector(IEnumerable<string> categories, int initialWeight, int minWeight, int maxWeight, int step)
    {
        this.minWeight = minWeight;
        this.maxWeight = maxWeight;
        this.step = step;
        foreach (string category in categories)
        {
            if (weights.ContainsKey(category)) continue;
            this.categories.Add(category);
            weights[category] = Mathf.Clamp(initialWeight, minWeight, maxWeight);
        }
    }

    public int GetWeight(string category)
    {
        return weights.TryGetValue(category, out int weight) ? weight : 0;
    }

    // Rentang angka tiap kategori disusun berurutan dari 0 dengan panjang sama dengan bobotnya.
    // Angka acak diambil dari 0 sampai (total bobot - 1), lalu dicari rentang yang memuatnya.
    // available: kategori yang masih punya soal; null berarti semua kategori
    public string Pick(ICollection<string> available = null)
    {
        int total = 0;
        foreach (string category in categories)
            if (IsAvailable(category, available)) total += weights[category];
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        LastRoll = roll;
        LastRollTotal = total;
        int rangeEnd = 0;
        foreach (string category in categories)
        {
            if (!IsAvailable(category, available)) continue;
            rangeEnd += weights[category];
            if (roll < rangeEnd)
            {
                LastPick = category;
                return category;
            }
        }
        return null;
    }

    // Peluang kategori keluar pada giliran berikutnya, 0..1
    public float GetChance(string category)
    {
        int total = TotalWeight;
        return total > 0 ? (float)GetWeight(category) / total : 0f;
    }

    // Angka awal rentang kategori
    public int GetRangeStart(string category)
    {
        int start = 0;
        foreach (string other in categories)
        {
            if (other == category) break;
            start += weights[other];
        }
        return start;
    }

    // Benar: bobot turun. Salah: bobot naik. Bobot kategori lain tidak berubah
    public void ApplyAnswer(string category, bool correct)
    {
        if (!weights.ContainsKey(category)) return;
        int change = correct ? -step : step;
        weights[category] = Mathf.Clamp(weights[category] + change, minWeight, maxWeight);
    }

    // Bobot dan rentang saat ini, mis. Simple Present 40 (0-39) | Present Continuous 50 (40-89)
    public string Describe()
    {
        var parts = new List<string>();
        int start = 0;
        foreach (string category in categories)
        {
            int weight = weights[category];
            parts.Add(category + " " + weight + " (" + start + "-" + (start + weight - 1) + ")");
            start += weight;
        }
        return string.Join(" | ", parts);
    }

    static bool IsAvailable(string category, ICollection<string> available)
    {
        return available == null || available.Contains(category);
    }
}
