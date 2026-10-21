using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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

    // ---------------- Supabase (preencha com seus dados) ----------------
    // Crie um projeto em https://supabase.com, crie a tabela 'leaderboard' e pegue
    // a URL e a 'anon key' em Configurações > API.
    private const string SupabaseUrl = "https://SEU_PROJETO.supabase.co/rest/v1/leaderboard";
    private const string SupabaseAnonKey = "SUA_ANON_KEY_AQUI";
    private const float RequestTimeout = 8f;

    // Caminho local usado como fallback quando não há internet ou a Supabase falha.

    public static List<ScoreEntry> Load()
    {
        // Tenta buscar o top 10 ordenado por tempo desc na Supabase.
        var wrapper = SupabaseRequest<SupabaseList>("GET", query: "select=*&order=time.desc&limit=" + MaxEntries);
        if (wrapper != null && wrapper.entries != null && wrapper.entries.Count > 0)
        {
            return wrapper.entries;
        }

        // Fallback: arquivo local.
        return LoadLocal();
    }

    private static List<ScoreEntry> LoadLocal()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<ScoreEntry>();

            ScoreFile file = JsonUtility.FromJson<ScoreFile>(File.ReadAllText(FilePath));
            return file != null && file.entries != null ? file.entries : new List<ScoreEntry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Não foi possível ler scores.json: " + e.Message);
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
        ScoreEntry entry = new ScoreEntry
        {
            name = Clean(name),
            time = time,
            date = DateTime.Now.ToString("dd/MM/yyyy")
        };

        // Tenta enviar pra Supabase.
        var wrapper = SupabaseRequest<SupabaseList>(
            "POST",
            body: JsonUtility.ToJson(entry),
            query: null
        );

        if (wrapper != null && wrapper.entries != null)
        {
            // A API retorna o que foi inserido; recalculamos o rank com base na lista atualizada.
            int rank = ComputeRank(wrapper.entries, entry);
            return rank;
        }

        // Fallback local: insere no JSON local.
        List<ScoreEntry> list = LoadLocal();
        list.Add(entry);
        list.Sort((a, b) => b.time.CompareTo(a.time));

        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);

        int localRank = list.IndexOf(entry);

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(new ScoreFile { entries = list }, true));
            Debug.Log("Placar salvo localmente em: " + FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Não foi possível salvar scores.json: " + e.Message);
        }

        return localRank;
    }

    private static int ComputeRank(List<ScoreEntry> list, ScoreEntry entry)
    {
        if (list == null) return -1;
        list.Sort((a, b) => b.time.CompareTo(a.time));
        return list.IndexOf(entry);
    }

    public static void Clear()
    {
        // Tenta limpar online (deleta todos os registros).
        SupabaseRequest<string>("DELETE", query: "id=is.not.null");

        // Também limpa o local.
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    // ---------------- Utilitários ----------------

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

    // Como ScoreBoard é estático (não é MonoBehaviour), as chamadas de rede são
    // feitas de forma síncrona com timeout. Elas travam o frame por uns ms, mas
    // evitam precisar de corrotinas/MonoBehaviour só pra ler o placar.
    private static T SupabaseRequest<T>(string method, string body = null, string query = null)
    {
        string url = SupabaseUrl;
        if (!string.IsNullOrEmpty(query)) url += "?" + query;

        using UnityWebRequest req = new UnityWebRequest(url, method);
        req.SetRequestHeader("apikey", SupabaseAnonKey);
        req.SetRequestHeader("Authorization", "Bearer " + SupabaseAnonKey);
        req.SetRequestHeader("Accept", "application/json");

        if (body != null)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(jsonBytes);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "return=representation");
        }

        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = (int)RequestTimeout;

        req.SendWebRequest();

        // Espera terminar (síncrono com travamento de frame curto).
        float start = Time.realtimeSinceStartup;
        while (!req.isDone)
        {
            if (Time.realtimeSinceStartup - start > RequestTimeout) break;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Supabase {method} falhou: {req.error} {(req.downloadHandler != null ? req.downloadHandler.text : "")}");
            return default;
        }

        string json = req.downloadHandler.text;
        if (string.IsNullOrEmpty(json)) return default;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao fazer parse da resposta da Supabase: {e.Message}");
            return default;
        }
    }

    // Wrapper para resposta de lista da Supabase (JSON é um array direto, não objeto).
    [Serializable]
    private class SupabaseList
    {
        public List<ScoreEntry> entries;
    }
}