using UnityEngine;

public class TerrainCollider : MonoBehaviour
{
    void Awake()
    {
        // Önce Terrain üzerindeki Collider bileþenini bul ve kapat
        GetComponent<TerrainCollider>().enabled = false;

        // Sonra hemen geri aç. Bu iþlem collider'ý 'reset'ler.
        GetComponent<TerrainCollider>().enabled = true;

        Debug.Log("Terrain Collider yenilendi!");
    }
}