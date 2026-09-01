using System;
using KAM.Progression;

namespace KAM.SaveSystem
{
    [Serializable]
    public class GameSaveData
    {
        public int saveVersion = 1;
        public string savedAtUtc;
        public string activeSceneName;
        public InventorySaveData inventory;
        public PlayerProgression.ProgressionSaveData progression;
        public BonfireSaveData bonfire;
        public BonfireSaveData[] discoveredBonfires;
        public SceneSaveData[] scenes;

        public bool HasBonfire => bonfire != null && !string.IsNullOrWhiteSpace(bonfire.bonfireId);
    }

    [Serializable]
    public class InventorySaveData
    {
        public int spiritFragments;
    }

    [Serializable]
    public class BonfireSaveData
    {
        public string bonfireId;
        public string sceneName;
        public SerializableVector3 position;
        public SerializableVector3 eulerAngles;
    }

    [Serializable]
    public class SceneSaveData
    {
        public string sceneName;
        public SaveableObjectData[] objects;
    }

    [Serializable]
    public class SaveableObjectData
    {
        public string saveableId;
        public string type;
        public string json;
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(UnityEngine.Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public UnityEngine.Vector3 ToVector3()
        {
            return new UnityEngine.Vector3(x, y, z);
        }
    }
}
