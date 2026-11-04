using UnityEngine;

// Tata letak slot box (spawn area di peron dan drop area di gerbong) untuk jumlah kartu yang dinamis.
// Slot selalu simetris di tengah; lebarnya mengecil bila kartu terlalu banyak agar tetap muat di gerbong.
public static class SlotLayout
{
    // Jumlah kartu maksimal per soal (jumlah spawn area & drop area yang disediakan)
    public const int MaxSlots = 7;
    // Lebar normal box / slot
    public const float MaxWidth = 3.3f;
    // Lebar area muatan: bagian gelap gerbong (~17.7) dikurangi jarak 0.5 di kiri-kanan,
    // agar drop area tidak menempel / melewati dinding gerbong (bagian terang)
    public const float AreaWidth = 16.7f;
    // Jarak antar slot
    public const float Gap = 0.5f;

    public static float Width(int count)
    {
        if (count <= 1) return MaxWidth;
        return Mathf.Min(MaxWidth, (AreaWidth - Gap * (count - 1)) / count);
    }

    // Posisi x lokal slot ke-i (0 = paling kiri) dari total count slot
    public static float X(int index, int count)
    {
        return (index - (count - 1) / 2f) * (Width(count) + Gap);
    }
}
