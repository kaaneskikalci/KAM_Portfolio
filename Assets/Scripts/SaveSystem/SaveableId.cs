using System;
using UnityEngine;

namespace KAM.SaveSystem
{
    [DisallowMultipleComponent]
    public class SaveableId : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;
        public bool HasId => !string.IsNullOrWhiteSpace(id);

        private void Reset()
        {
            GenerateIdIfNeeded();
        }

        private void OnValidate()
        {
            GenerateIdIfNeeded();
        }

        [ContextMenu("Generate New Saveable Id")]
        public void GenerateNewId()
        {
            id = Guid.NewGuid().ToString("N");
        }

        private void GenerateIdIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(id))
                GenerateNewId();
        }
    }
}
