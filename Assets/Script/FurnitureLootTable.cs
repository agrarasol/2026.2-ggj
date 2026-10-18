using System;
using UnityEngine;

public enum FurnitureItemRarity { Common, Uncommon, Rare, Legendary }

[Serializable]
public class FurnitureLootEntry
{
    public string itemName;
    [Tooltip("Imagem mostrada ao encontrar o item. Sem sprite, aparece um quadrado branco.")]
    public Sprite icon;
    public FurnitureItemRarity rarity;
    [Min(0f)] public float weight;
    [Min(0f)] public float timeBonus;

    public string RarityName => rarity switch
    {
        FurnitureItemRarity.Uncommon => "Incomum",
        FurnitureItemRarity.Rare => "Raro",
        FurnitureItemRarity.Legendary => "Lendário",
        _ => "Comum"
    };

    public Color Color => rarity switch
    {
        FurnitureItemRarity.Uncommon => new Color(0.52f, 0.9f, 0.61f),
        FurnitureItemRarity.Rare => new Color(0.49f, 0.74f, 1f),
        FurnitureItemRarity.Legendary => new Color(1f, 0.77f, 0.3f),
        _ => new Color(0.93f, 0.9f, 0.81f)
    };
}

[CreateAssetMenu(menuName = "Jogo/Itens dos moveis")]
public class FurnitureLootTable : ScriptableObject
{
    [Tooltip("Peso da chance de nao encontrar nada. Os pesos sao somados para calcular as chances.")]
    [SerializeField, Min(0f)] private float emptyWeight = 45f;
    [SerializeField] private FurnitureLootEntry[] items =
    {
        new FurnitureLootEntry { itemName = "D", rarity = FurnitureItemRarity.Common, weight = 35f, timeBonus = 5f },
        new FurnitureLootEntry { itemName = "C", rarity = FurnitureItemRarity.Uncommon, weight = 15f, timeBonus = 12f },
        new FurnitureLootEntry { itemName = "B", rarity = FurnitureItemRarity.Rare, weight = 4f, timeBonus = 25f },
        new FurnitureLootEntry { itemName = "A", rarity = FurnitureItemRarity.Legendary, weight = 1f, timeBonus = 45f }
    };

    public FurnitureLootEntry Roll() => Roll(UnityEngine.Random.value);

    // Aceita um valor entre 0 e 1 para permitir verificar as faixas do sorteio.
    public FurnitureLootEntry Roll(float value)
    {
        float total = ValidWeight(emptyWeight);
        if (items != null)
            foreach (FurnitureLootEntry item in items)
                if (item != null) total += ValidWeight(item.weight);
        if (total <= 0f || float.IsNaN(value))
            return null;

        float ticket = Mathf.Clamp(value, 0f, 0.999999f) * total;
        float cumulative = ValidWeight(emptyWeight);
        if (ticket < cumulative)
            return null;

        if (items != null)
            foreach (FurnitureLootEntry item in items)
            {
                if (item == null) continue;
                cumulative += ValidWeight(item.weight);
                if (ticket < cumulative) return item;
            }
        return null;
    }

    private static float ValidWeight(float weight) =>
        float.IsNaN(weight) || float.IsInfinity(weight) ? 0f : Mathf.Max(0f, weight);
}
