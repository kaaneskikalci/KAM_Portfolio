using System.Collections.Generic;
using System.Linq;
using KAM.Core.Combat;
using UnityEditor;
using UnityEngine;

public static class TurnOrderPortraitAssignmentEditor
{
    private const string PortraitFieldName = "portrait";

    private static readonly PortraitEntry[] Portraits =
    {
        new PortraitEntry("Kam", "Assets/Assets/Art/DSU LAST/ds/characters/kam.jpg", "player", "kam"),
        new PortraitEntry("Aabasi", "Assets/Assets/Art/DSU LAST/ds/characters/aabasi.png", "aabasi", "abasi"),
        new PortraitEntry("Azar", "Assets/Assets/Art/DSU LAST/ds/characters/azar.png", "azar"),
        new PortraitEntry("Bazar", "Assets/Assets/Art/DSU LAST/ds/characters/bazar.png", "bazar"),
        new PortraitEntry("Erbuke", "Assets/Assets/Art/DSU LAST/ds/characters/erbüke.png", "erbuke", "erbukevariant", "esbuke", "esbukevariant"),
        new PortraitEntry("Jalamuz Kempir", "Assets/Assets/Art/DSU LAST/ds/characters/jalamuz_kempir.png", "jalamuz", "jalamuzkempir"),
        new PortraitEntry("Kazar", "Assets/Assets/Art/DSU LAST/ds/characters/kazar.png", "kazar"),
        new PortraitEntry("Cak", "Assets/Assets/Art/DSU LAST/ds/characters/çak.png", "cak", "cakvariant")
    };

    [MenuItem("KAM/Combat UI/Assign Turn Order Portraits In Open Scene", false, 40)]
    public static void AssignPortraitsInOpenScene()
    {
        CombatUnit[] units = Resources.FindObjectsOfTypeAll<CombatUnit>()
            .Where(unit => unit != null && unit.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(unit))
            .ToArray();

        AssignPortraits(units);
    }

    [MenuItem("KAM/Combat UI/Assign Turn Order Portraits To Selection", false, 41)]
    public static void AssignPortraitsToSelection()
    {
        CombatUnit[] units = Selection.gameObjects
            .SelectMany(go => go.GetComponentsInChildren<CombatUnit>(true))
            .Where(unit => unit != null)
            .Distinct()
            .ToArray();

        AssignPortraits(units);
    }

    private static void AssignPortraits(IEnumerable<CombatUnit> units)
    {
        Dictionary<PortraitEntry, Sprite> loadedSprites = LoadSprites();
        int assigned = 0;
        int skipped = 0;

        foreach (CombatUnit unit in units)
        {
            PortraitEntry entry = FindPortraitEntry(unit);
            if (entry == null || !loadedSprites.TryGetValue(entry, out Sprite sprite) || sprite == null)
            {
                skipped++;
                Debug.LogWarning($"[Turn Order Portraits] No portrait match for {unit.name} / {unit.UnitName}");
                continue;
            }

            SerializedObject serialized = new SerializedObject(unit);
            SerializedProperty portraitProperty = serialized.FindProperty(PortraitFieldName);
            if (portraitProperty == null)
            {
                skipped++;
                Debug.LogWarning($"[Turn Order Portraits] CombatUnit portrait field not found on {unit.name}");
                continue;
            }

            Undo.RecordObject(unit, "Assign Turn Order Portrait");
            serialized.Update();
            portraitProperty.objectReferenceValue = sprite;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(unit);
            PrefabUtility.RecordPrefabInstancePropertyModifications(unit);

            assigned++;
            Debug.Log($"[Turn Order Portraits] Assigned {entry.Label} portrait to {unit.name} / {unit.UnitName}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Turn Order Portraits] Done. Assigned={assigned}, Skipped={skipped}");
    }

    private static Dictionary<PortraitEntry, Sprite> LoadSprites()
    {
        Dictionary<PortraitEntry, Sprite> loaded = new Dictionary<PortraitEntry, Sprite>();

        foreach (PortraitEntry entry in Portraits)
        {
            Sprite sprite = LoadSprite(entry.AssetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Turn Order Portraits] Sprite not found: {entry.AssetPath}");
                continue;
            }

            loaded[entry] = sprite;
        }

        return loaded;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
    }

    private static PortraitEntry FindPortraitEntry(CombatUnit unit)
    {
        string combined = Normalize($"{unit.UnitName} {unit.gameObject.name}");

        return Portraits.FirstOrDefault(entry =>
            entry.Keys.Any(key => combined.Contains(key)));
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("'", string.Empty)
            .Replace("ı", "i")
            .Replace("İ", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c");
    }

    private sealed class PortraitEntry
    {
        public PortraitEntry(string label, string assetPath, params string[] keys)
        {
            Label = label;
            AssetPath = assetPath;
            Keys = keys.Select(Normalize).ToArray();
        }

        public string Label { get; }
        public string AssetPath { get; }
        public IReadOnlyList<string> Keys { get; }
    }
}
