using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Export / import bank soal (ScriptableObject berisi satu daftar soal) ke file CSV,
// supaya soal bisa diedit di Excel / Google Sheets tanpa membuka Unity.
// Berlaku untuk QuestionBank, TicketBank, TenseQuestionBank, dan bank lain dengan pola yang sama.
public static class BankCsvTool
{
    const string BanksFolder = "Assets/Data/Banks";
    // Folder CSV di luar Assets agar tidak ikut di-import Unity
    const string CsvFolder = "BankSoal";
    // Excel dengan pengaturan regional Indonesia memakai ';' sebagai pemisah kolom
    const char ExportDelimiter = ';';

    // ---------- Menu ----------

    [MenuItem("Assets/Grammar Station/Export Bank ke CSV", false, 2000)]
    static void ExportSelected()
    {
        var bank = Selection.activeObject as ScriptableObject;
        string defaultPath = Path.Combine(CsvFolderFullPath(), bank.name + ".csv");
        string path = EditorUtility.SaveFilePanel("Export Bank ke CSV", Path.GetDirectoryName(defaultPath), bank.name + ".csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        int count = Export(bank, path);
        EditorUtility.RevealInFinder(path);
        Debug.Log("Bank " + bank.name + " diekspor (" + count + " soal) ke " + path);
    }

    [MenuItem("Assets/Grammar Station/Import CSV ke Bank", false, 2001)]
    static void ImportSelected()
    {
        var bank = Selection.activeObject as ScriptableObject;
        string path = EditorUtility.OpenFilePanel("Import CSV ke " + bank.name, CsvFolderFullPath(), "csv");
        if (string.IsNullOrEmpty(path)) return;

        if (!EditorUtility.DisplayDialog("Import CSV",
            "Seluruh isi bank \"" + bank.name + "\" akan diganti dengan isi file:\n" + Path.GetFileName(path) + "\n\nLanjutkan?",
            "Import", "Batal")) return;

        try
        {
            var warnings = new List<string>();
            int count = Import(bank, path, warnings);
            string message = count + " soal berhasil di-import ke " + bank.name + ".";
            if (warnings.Count > 0) message += "\n\nPeringatan (" + warnings.Count + "):\n- " + string.Join("\n- ", warnings.GetRange(0, Mathf.Min(warnings.Count, 15)));
            EditorUtility.DisplayDialog("Import CSV", message, "OK");
            foreach (string w in warnings) Debug.LogWarning(bank.name + ": " + w);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Import CSV gagal", e.Message + "\n\nIsi bank tidak diubah.", "OK");
        }
    }

    [MenuItem("Assets/Grammar Station/Export Bank ke CSV", true)]
    [MenuItem("Assets/Grammar Station/Import CSV ke Bank", true)]
    static bool ValidateSelected()
    {
        var so = Selection.activeObject as ScriptableObject;
        return so != null && FindListField(so.GetType()) != null;
    }

    [MenuItem("Tools/Grammar Station/Export Semua Bank ke CSV")]
    static void ExportAll()
    {
        string folder = CsvFolderFullPath();
        Directory.CreateDirectory(folder);
        var exported = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { BanksFolder }))
        {
            var bank = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (bank == null || FindListField(bank.GetType()) == null) continue;
            int count = Export(bank, Path.Combine(folder, bank.name + ".csv"));
            exported.Add(bank.name + " (" + count + " soal)");
        }
        EditorUtility.RevealInFinder(folder);
        Debug.Log("Export semua bank ke " + folder + ":\n" + string.Join("\n", exported));
    }

    static string CsvFolderFullPath()
    {
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), CsvFolder);
    }

    // ---------- Export ----------

    public static int Export(ScriptableObject bank, string path)
    {
        FieldInfo listField = FindListField(bank.GetType());
        Type itemType = GetItemType(listField.FieldType);
        List<FieldInfo> columns = GetColumns(itemType);
        var list = (IList)listField.GetValue(bank);

        var sb = new StringBuilder();
        sb.AppendLine(JoinRow(columns.ConvertAll(c => c.Name)));
        if (list != null)
        {
            foreach (object item in list)
                sb.AppendLine(JoinRow(columns.ConvertAll(c => ValueToText(c.GetValue(item)))));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // UTF-8 dengan BOM agar Excel membaca huruf non-ASCII dengan benar
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        return list != null ? list.Count : 0;
    }

    static string JoinRow(List<string> cells)
    {
        var escaped = cells.ConvertAll(Escape);
        return string.Join(ExportDelimiter.ToString(), escaped.ToArray());
    }

    static string Escape(string value)
    {
        if (value == null) return "";
        bool needsQuotes = value.IndexOf(ExportDelimiter) >= 0 || value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
            || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    static string ValueToText(object value)
    {
        if (value == null) return "";
        if (value is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString();   // enum ditulis sebagai nama, mis. A / B / NoError
    }

    // ---------- Import ----------

    public static int Import(ScriptableObject bank, string path, List<string> warnings)
    {
        FieldInfo listField = FindListField(bank.GetType());
        Type itemType = GetItemType(listField.FieldType);
        List<FieldInfo> columns = GetColumns(itemType);

        string text = File.ReadAllText(path, Encoding.UTF8);
        List<List<string>> rows = ParseCsv(text);
        if (rows.Count > 0 && rows[0].Count == 1 && rows[0][0].StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) rows.RemoveAt(0);
        if (rows.Count == 0) throw new Exception("File CSV kosong.");

        // Cocokkan kolom berdasarkan nama header (urutan kolom boleh berbeda)
        List<string> header = rows[0];
        var columnIndex = new Dictionary<FieldInfo, int>();
        foreach (FieldInfo c in columns)
        {
            int index = header.FindIndex(h => string.Equals(h.Trim(), c.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) columnIndex[c] = index;
            else warnings.Add("Kolom \"" + c.Name + "\" tidak ada di CSV, nilainya dikosongkan.");
        }
        if (columnIndex.Count == 0) throw new Exception("Header CSV tidak cocok dengan bank ini. Kolom yang diharapkan: " + string.Join(", ", columns.ConvertAll(c => c.Name).ToArray()));

        // Baca semua baris dulu; bank baru diubah bila tidak ada error
        var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.TrueForAll(string.IsNullOrWhiteSpace)) continue;

            object item = Activator.CreateInstance(itemType);
            foreach (var pair in columnIndex)
            {
                string cell = pair.Value < row.Count ? row[pair.Value].Trim() : "";
                try { pair.Key.SetValue(item, TextToValue(cell, pair.Key.FieldType)); }
                catch (Exception) { throw new Exception("Baris " + (r + 1) + ", kolom \"" + pair.Key.Name + "\": nilai \"" + cell + "\" tidak valid." + EnumHint(pair.Key.FieldType)); }
            }
            items.Add(item);
        }

        Undo.RecordObject(bank, "Import CSV");
        if (listField.FieldType.IsArray)
        {
            Array array = Array.CreateInstance(itemType, items.Count);
            items.CopyTo(array, 0);
            listField.SetValue(bank, array);
        }
        else listField.SetValue(bank, items);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();

        if (bank is TicketBank ticketBank) ValidateTickets(ticketBank, warnings);
        return items.Count;
    }

    static object TextToValue(string cell, Type type)
    {
        if (type == typeof(string)) return cell;
        if (type == typeof(int)) return string.IsNullOrEmpty(cell) ? 0 : int.Parse(cell);
        if (type == typeof(float)) return string.IsNullOrEmpty(cell) ? 0f : float.Parse(cell.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return cell == "1" || cell.Equals("true", StringComparison.OrdinalIgnoreCase) || cell.Equals("ya", StringComparison.OrdinalIgnoreCase);
        if (type.IsEnum)
        {
            // Terima nama enum ("NoError"), nama dengan spasi ("No Error"), atau angka ("2")
            string compact = cell.Replace(" ", "");
            if (int.TryParse(compact, out int number) && Enum.IsDefined(type, number)) return Enum.ToObject(type, number);
            return Enum.Parse(type, compact, true);
        }
        throw new NotSupportedException(type.Name);
    }

    static string EnumHint(Type type)
    {
        return type.IsEnum ? " Pilihan yang valid: " + string.Join(", ", Enum.GetNames(type)) : "";
    }

    static void ValidateTickets(TicketBank bank, List<string> warnings)
    {
        var markers = new Regex(@"\[(A|B):([^\]]+)\]");
        var ids = new HashSet<string>();
        foreach (TicketData t in bank.tickets)
        {
            string label = string.IsNullOrEmpty(t.id) ? t.passengerName : t.id;
            MatchCollection m = markers.Matches(t.note ?? "");
            if (m.Count != 2 || m[0].Groups[1].Value != "A" || m[1].Groups[1].Value != "B")
                warnings.Add(label + ": note harus berisi [A:...] lalu [B:...]");
            if (!ids.Add(t.id ?? "")) warnings.Add(label + ": id dobel");
            if (t.correctAnswer == TicketAnswer.NoError && t.correction != t.GetPlainNote())
                warnings.Add(label + ": jawaban NoError tapi correction berbeda dari note");
        }
    }

    // CSV parser: mendukung tanda kutip, baris baru di dalam kutip, dan pemisah ';' atau ','
    static List<List<string>> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        char delimiter = DetectDelimiter(text);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(cell.ToString()); cell.Length = 0; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Length = 0;
                rows.Add(row); row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    static char DetectDelimiter(string text)
    {
        int end = text.IndexOfAny(new[] { '\n', '\r' });
        string firstLine = end >= 0 ? text.Substring(0, end) : text;
        if (firstLine.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && firstLine.Length > 4) return firstLine[4];
        int semicolons = firstLine.Split(';').Length, commas = firstLine.Split(',').Length;
        return semicolons >= commas ? ';' : ',';
    }

    // ---------- Reflection ----------

    // Field daftar soal pertama di bank (List<T> atau T[] dengan T class [Serializable])
    static FieldInfo FindListField(Type bankType)
    {
        foreach (FieldInfo f in bankType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!IsSerialized(f)) continue;
            Type item = GetItemType(f.FieldType);
            if (item != null && item.IsClass && item != typeof(string) && item.IsDefined(typeof(SerializableAttribute), false)) return f;
        }
        return null;
    }

    static Type GetItemType(Type listType)
    {
        if (listType.IsArray) return listType.GetElementType();
        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) return listType.GetGenericArguments()[0];
        return null;
    }

    static List<FieldInfo> GetColumns(Type itemType)
    {
        var columns = new List<FieldInfo>();
        foreach (FieldInfo f in itemType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type t = f.FieldType;
            bool supported = t == typeof(string) || t == typeof(int) || t == typeof(float) || t == typeof(bool) || t.IsEnum;
            if (supported && IsSerialized(f)) columns.Add(f);
        }
        return columns;
    }

    static bool IsSerialized(FieldInfo f)
    {
        return (f.IsPublic && !f.IsNotSerialized && !f.IsStatic) || f.IsDefined(typeof(SerializeField), false);
    }
}
