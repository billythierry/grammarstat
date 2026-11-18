using UnityEngine;

// Progres pemain: bintang terbaik tiap level, disimpan di PlayerPrefs agar tetap ada setelah game ditutup.
// Chapter dan level dimulai dari 1. Level yang belum pernah dimainkan bernilai 0 bintang
public static class ProgressData
{
    private const string KeyPrefix = "Stars_C";
    // Batas chapter / level yang ikut dihapus saat reset progres
    private const int MaxChapters = 9;
    private const int MaxLevels = 9;

    static string Key(int chapter, int level) => KeyPrefix + chapter + "_L" + level;

    public static int GetStars(int chapter, int level)
    {
        return PlayerPrefs.GetInt(Key(chapter, level), 0);
    }

    // Hanya disimpan bila lebih banyak dari rekor sebelumnya, jadi mengulang level tidak menurunkan bintang
    public static void RecordStars(int chapter, int level, int stars)
    {
        if (chapter < 1 || level < 1) return;
        stars = Mathf.Clamp(stars, 0, 3);
        if (stars <= GetStars(chapter, level)) return;
        PlayerPrefs.SetInt(Key(chapter, level), stars);
        PlayerPrefs.Save();
    }

    // Hapus semua bintang. Pengaturan volume tidak ikut terhapus
    public static void ResetAll()
    {
        for (int chapter = 1; chapter <= MaxChapters; chapter++)
            for (int level = 1; level <= MaxLevels; level++)
                PlayerPrefs.DeleteKey(Key(chapter, level));
        PlayerPrefs.Save();
    }
}
