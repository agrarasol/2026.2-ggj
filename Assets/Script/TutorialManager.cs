using UnityEngine;
using UnityEngine.UIElements;

public class TutorialManager : MonoBehaviour
{
    public static bool chaseEnemyTutorial = false;      // Primera vez que apareceu? Mostra mensagem
    public static bool candleEnemyTutorial = false;     // Primeira vez que apagou? Mostra mensagem

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
}
