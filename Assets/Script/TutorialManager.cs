using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private const string START_TEXT = "Teste";
    private const string CHASE_TEXT = "";
    private const string CANDLE_TEXT = "";
    private const string HIDEN_TEXT = "";

    public static bool chaseEnemyTutorial = false;      // Primera vez que apareceu? Mostra mensagem
    public static bool candleEnemyTutorial = false;     // Primeira vez que apagou? Mostra mensagem
    public static bool hidenEnemyTutorial = false;      // Primeira vez que se escondeu? Mostra mensagem

    public static void CallChaseTutorial()
    {
        if (chaseEnemyTutorial)
        {
            return;
        }

        chaseEnemyTutorial = true;
    }


    public static void CallCandleTutorial()
    {
        if (candleEnemyTutorial)
        {
            return;
        }

        candleEnemyTutorial = true;
    }

    public static void CallHidenTutorial()
    {
        if (hidenEnemyTutorial)
        {
            return;
        }

        hidenEnemyTutorial = true;
    }
}
