using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class ScoreEntry
{
    public string name;
    public float time;   // segundos sobrevividos
    public string date;
}

[Serializable]
public class ScoreFile
{
    public List<ScoreEntry> entries = new List<ScoreEntry>();
}

public static class ScoreBoard
{
    public const int MaxEntries = 10;
    public const int MaxNameLength = 12;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "scores.json");

    public static List<ScoreEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<ScoreEntry>();

            ScoreFile file = JsonUtility.FromJson<ScoreFile>(File.ReadAllText(FilePath));
            return file != null && file.entries != null ? file.entries : new List<ScoreEntry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Nao foi possivel ler scores.json: " + e.Message);
            return new List<ScoreEntry>();
        }
    }

    public static bool Qualifies(float time)
    {
        List<ScoreEntry> list = Load();
        return list.Count < MaxEntries || time > list[list.Count - 1].time;
    }

    // devolve a posição (0 = primeiro lugar) ou -1 se não entrou no top
    public static int Add(string name, float time)
    {
        List<ScoreEntry> list = Load();

        ScoreEntry entry = new ScoreEntry
        {
            name = Clean(name),
            time = time,
            date = DateTime.Now.ToString("dd/MM/yyyy")
        };

        list.Add(entry);
        list.Sort((a, b) => b.time.CompareTo(a.time));

        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        int rank = list.IndexOf(entry);

        File.WriteAllText(FilePath, JsonUtility.ToJson(new ScoreFile { entries = list }, true));
        Debug.Log("Placar salvo em: " + FilePath);
        return rank;
    }

    public static void Clear()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    // tira símbolos pra ninguém quebrar o texto do TMP digitando <tags>
    private static string Clean(string raw)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in raw ?? "")
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')
                sb.Append(c);

        string result = sb.ToString().Trim();
        if (result.Length > MaxNameLength) result = result.Substring(0, MaxNameLength);
        return result.Length > 0 ? result : "ANONIMO";
    }
}