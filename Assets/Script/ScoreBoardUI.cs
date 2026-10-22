using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fica DENTRO do GameOverPanel: quando o painel liga (game over), o placar comeca sozinho.
public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject root;            // o bloco do placar (ScoreBoardBox)
    [SerializeField] private TMP_Text finalTimeText;
    [SerializeField] private string finalTimePrefix = "Tempo vivo: ";
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text listText;

    private bool initialized;
    private bool running;
    private bool submitted;

    private void Awake()
    {
        Init();
        if (root != null) root.SetActive(false);
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;

        if (submitButton != null)
            submitButton.onClick.AddListener(() => submitted = true);

        if (nameInput != null)
        {
            nameInput.characterLimit = ScoreBoard.MaxNameLength;
            nameInput.onSubmit.AddListener(_ => submitted = true);   // Enter tambem confirma
        }
    }

    private void OnEnable()
    {
        StartCoroutine(ShowRoutine());
    }

    private void OnDisable()
    {
        running = false;
        StopAllCoroutines();
    }

    public IEnumerator ShowRoutine()
    {
        if (running) yield break;
        running = true;
        Init();

        RunStats stats = RunStats.Instance != null ? RunStats.Instance : FindAnyObjectByType<RunStats>();
        float time = stats != null ? stats.TimeSurvived : 0f;
        string source = stats != null ? "RunStats" : "sem RunStats na cena";

        // se o RunStats nao contou, usa o relogio da cena (so avanca com o jogo rodando)
        if (time <= 0f)
        {
            time = Time.timeSinceLevelLoad;
            source += " -> usou o relogio da cena";
        }

        Debug.Log("Placar: tempo da partida = " + RunStats.Format(time) + " (" + source + ")", this);

        if (root != null) root.SetActive(true);
        if (finalTimeText != null) finalTimeText.text = finalTimePrefix + RunStats.Format(time);
        if (listText != null) listText.text = "";

        int rank = -1;
        bool qualifies = ScoreBoard.Qualifies(time);
        SetEntryVisible(qualifies);

        if (qualifies)
        {
            if (nameInput != null)
            {
                submitted = false;
                nameInput.text = "";

                yield return null;
                nameInput.Select();
                nameInput.ActivateInputField();

                while (!submitted)
                    yield return null;

                rank = ScoreBoard.Add(nameInput.text, time);
            }
            else
            {
                rank = ScoreBoard.Add("", time);
            }

            SetEntryVisible(false);
        }

        if (listText != null) listText.text = BuildList(rank);
        running = false;
    }

    private void SetEntryVisible(bool visible)
    {
        if (nameInput != null) nameInput.gameObject.SetActive(visible);
        if (submitButton != null) submitButton.gameObject.SetActive(visible);
    }

    private string BuildList(int highlightRank)
    {
        List<ScoreEntry> list = ScoreBoard.Load();
        if (list.Count == 0) return "";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < list.Count; i++)
        {
            string line = (i + 1) + ".<pos=14%>" + list[i].name + "<pos=66%>" + RunStats.Format(list[i].time);

            if (i == highlightRank)
                line = "<color=#ffffff>" + line + "</color>";   // destaca a sua posicao

            sb.Append(line).Append('\n');
        }

        return sb.ToString();
    }

    [ContextMenu("DEBUG/Apagar placar")]
    private void ClearBoard()
    {
        ScoreBoard.Clear();
    }
}