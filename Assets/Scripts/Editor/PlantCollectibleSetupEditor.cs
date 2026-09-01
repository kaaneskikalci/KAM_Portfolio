using System.IO;
using KAM.Inventory;
using KAM.SaveSystem;
using UnityEditor;
using UnityEngine;

namespace KAM.EditorTools
{
    public static class PlantCollectibleSetupEditor
    {
        private const string ItemFolder = "Assets/ScriptableObjects/Items/Plants";

        private static readonly PlantDefinition[] Plants =
        {
            new PlantDefinition("bellflower", "Bellflower", "BellFlower.prefab", "bellflower.PNG",
                "Restores a small amount of health.", PlantConsumableEffect.HealFlat, 25f, 0, false, ItemRarity.Common),
            new PlantDefinition("brugmansia", "Brugmansia", "Brugmansia.prefab", "brugmansia.PNG",
                "A dangerous flower that sharpens attacks for a short time.", PlantConsumableEffect.StrengthUp, 0.35f, 2, true, ItemRarity.Rare),
            new PlantDefinition("esfand", "Esfand", "Esfand.prefab", "esfand.png",
                "Purifying smoke herb. Removes harmful effects.", PlantConsumableEffect.CleanseDebuffs, 0f, 0, true, ItemRarity.Uncommon),
            new PlantDefinition("koloss", "Koloss", "Koloss.prefab", "koloss.PNG",
                "A dense root that restores combat focus.", PlantConsumableEffect.GainActionPoint, 1f, 0, true, ItemRarity.Rare),
            new PlantDefinition("moon_pharynx", "Moon Pharynx", "Moon Pharynx.prefab", "moon_pahrynx.PNG",
                "A pale night plant that makes Kam harder to hit.", PlantConsumableEffect.Evasion, 0.35f, 2, true, ItemRarity.Rare),
            new PlantDefinition("morning_glory", "Morning Glory", "Morning Glory.prefab", "morning_glory.PNG",
                "A bright flower that quickens movement.", PlantConsumableEffect.SpeedUp, 0.3f, 2, true, ItemRarity.Uncommon),
            new PlantDefinition("thorny_yellowbloom", "Thorny Yellowbloom", "Thorny Yellowbloom.prefab", "yellowbloom.PNG",
                "A thorned bloom that boosts damage for the next exchange.", PlantConsumableEffect.StrengthUp, 0.2f, 2, true, ItemRarity.Uncommon),
            new PlantDefinition("vyushnista", "Vyushnista", "Vyushnista.prefab", "vyushnitsa.PNG",
                "A restorative plant that heals over time.", PlantConsumableEffect.Regeneration, 8f, 3, true, ItemRarity.Uncommon)
        };

        [MenuItem("KAM/Inventory/Setup Plant Collectibles")]
        public static void SetupPlantCollectibles()
        {
            CreateOrUpdatePlantItemAssets();
            ConfigureAllPlantPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlantCollectibleSetup] Plant item assets and prefab pickups configured.");
        }

        [MenuItem("KAM/Inventory/Create Plant Item Assets Only")]
        public static void CreateOrUpdatePlantItemAssets()
        {
            EnsureFolder(ItemFolder);

            foreach (PlantDefinition plant in Plants)
            {
                PlantConsumableData item = LoadOrCreateItemAsset(plant);
                SerializedObject so = new SerializedObject(item);

                SetBaseItemFields(so, plant);
                so.FindProperty("effect").enumValueIndex = (int)plant.Effect;
                so.FindProperty("effectValue").floatValue = plant.EffectValue;
                so.FindProperty("durationTurns").intValue = plant.DurationTurns;
                so.FindProperty("requiresCombat").boolValue = plant.RequiresCombat;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PlantCollectibleSetup] Plant item assets created/updated.");
        }

        [MenuItem("KAM/Inventory/Configure All Plant Prefabs")]
        public static void ConfigureAllPlantPrefabs()
        {
            foreach (PlantDefinition plant in Plants)
            {
                ConfigurePlantPrefab(plant);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PlantCollectibleSetup] All plant prefabs configured as pickups.");
        }

        [MenuItem("KAM/Inventory/Configure Selected Plant Prefabs")]
        public static void ConfigureSelectedPlantPrefabs()
        {
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab"))
                    continue;

                PlantDefinition plant = FindPlantByPrefabName(Path.GetFileName(path));
                if (plant == null)
                {
                    Debug.LogWarning($"[PlantCollectibleSetup] No plant definition for {path}");
                    continue;
                }

                ConfigurePlantPrefab(plant);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePlantPrefab(PlantDefinition plant)
        {
            string prefabPath = plant.PrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[PlantCollectibleSetup] Could not load prefab: {prefabPath}");
                return;
            }

            try
            {
                EnsureRootCollider(root);

                SaveableId saveableId = root.GetComponent<SaveableId>();
                if (saveableId == null)
                    saveableId = root.AddComponent<SaveableId>();
                if (!saveableId.HasId)
                    saveableId.GenerateNewId();

                ItemPickup pickup = root.GetComponent<ItemPickup>();
                if (pickup == null)
                    pickup = root.AddComponent<ItemPickup>();

                PlantConsumableData item = AssetDatabase.LoadAssetAtPath<PlantConsumableData>(plant.ItemAssetPath);
                if (item == null)
                    item = LoadOrCreateItemAsset(plant);

                SerializedObject so = new SerializedObject(pickup);
                so.FindProperty("item").objectReferenceValue = item;
                so.FindProperty("quantity").intValue = 1;
                so.FindProperty("autoPickup").boolValue = false;
                so.FindProperty("destroyOnPickup").boolValue = true;
                so.FindProperty("bobUpAndDown").boolValue = false;
                so.FindProperty("rotate").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PlantCollectibleSetup] Configured {prefabPath} -> {item.ItemName}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureRootCollider(GameObject root)
        {
            if (root.GetComponent<Collider>() != null)
                return;

            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = CalculatePickupRadius(root);
            collider.center = Vector3.up * collider.radius;
        }

        private static float CalculatePickupRadius(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return 0.75f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.5f, 1.5f);
        }

        private static PlantConsumableData LoadOrCreateItemAsset(PlantDefinition plant)
        {
            PlantConsumableData item = AssetDatabase.LoadAssetAtPath<PlantConsumableData>(plant.ItemAssetPath);
            if (item != null)
                return item;

            item = ScriptableObject.CreateInstance<PlantConsumableData>();
            AssetDatabase.CreateAsset(item, plant.ItemAssetPath);
            return item;
        }

        private static void SetBaseItemFields(SerializedObject so, PlantDefinition plant)
        {
            so.FindProperty("itemId").stringValue = plant.ItemId;
            so.FindProperty("itemName").stringValue = plant.DisplayName;
            so.FindProperty("description").stringValue = plant.Description;
            so.FindProperty("icon").objectReferenceValue = LoadSprite(plant.SpritePath);
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Potion;
            so.FindProperty("rarity").enumValueIndex = (int)plant.Rarity;
            so.FindProperty("maxStackSize").intValue = 10;
            so.FindProperty("isConsumable").boolValue = true;
            so.FindProperty("isDroppable").boolValue = true;
            so.FindProperty("isSellable").boolValue = true;
            so.FindProperty("sellValue").intValue = plant.Rarity switch
            {
                ItemRarity.Rare => 5,
                ItemRarity.Uncommon => 3,
                _ => 1
            };
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            if (sprite == null)
                Debug.LogWarning($"[PlantCollectibleSetup] Sprite not found or not importable: {path}");

            return sprite;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static PlantDefinition FindPlantByPrefabName(string prefabName)
        {
            foreach (PlantDefinition plant in Plants)
            {
                if (plant.PrefabName == prefabName)
                    return plant;
            }

            return null;
        }

        private sealed class PlantDefinition
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly string PrefabName;
            public readonly string SpriteName;
            public readonly string Description;
            public readonly PlantConsumableEffect Effect;
            public readonly float EffectValue;
            public readonly int DurationTurns;
            public readonly bool RequiresCombat;
            public readonly ItemRarity Rarity;

            public string ItemAssetPath => $"{ItemFolder}/{ItemId}.asset";
            public string PrefabPath => $"Assets/Prefabs/Asset Prefabs/Plants/{PrefabName}";
            public string SpritePath => $"Assets/Assets/Art/DSU LAST/ds/items/{SpriteName}";

            public PlantDefinition(
                string itemId,
                string displayName,
                string prefabName,
                string spriteName,
                string description,
                PlantConsumableEffect effect,
                float effectValue,
                int durationTurns,
                bool requiresCombat,
                ItemRarity rarity)
            {
                ItemId = itemId;
                DisplayName = displayName;
                PrefabName = prefabName;
                SpriteName = spriteName;
                Description = description;
                Effect = effect;
                EffectValue = effectValue;
                DurationTurns = durationTurns;
                RequiresCombat = requiresCombat;
                Rarity = rarity;
            }
        }
    }
}
