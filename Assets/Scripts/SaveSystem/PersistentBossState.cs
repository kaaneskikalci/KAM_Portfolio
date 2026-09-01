using System;
using KAM.Core.Combat;
using UnityEngine;

namespace KAM.SaveSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SaveableId))]
    [RequireComponent(typeof(EnemyCombatUnit))]
    public class PersistentBossState : MonoBehaviour, ISaveable
    {
        private bool _isDefeated;

        public string SaveableType => "PersistentBossState";
        public bool IsDefeated => _isDefeated;

        public void MarkDefeated()
        {
            SetDefeatedState(true);
        }

        public string CaptureStateJson()
        {
            return JsonUtility.ToJson(new PersistentBossSaveState
            {
                isDefeated = _isDefeated
            });
        }

        public void RestoreStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            PersistentBossSaveState state = JsonUtility.FromJson<PersistentBossSaveState>(json);
            SetDefeatedState(state.isDefeated);
        }

        private void SetDefeatedState(bool isDefeated)
        {
            _isDefeated = isDefeated;
            gameObject.SetActive(!isDefeated);
        }
    }

    [Serializable]
    public class PersistentBossSaveState
    {
        public bool isDefeated;
    }
}
