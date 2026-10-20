using UnityEngine;

public class MonsterBanishItem : MonoBehaviour
{
    [SerializeField] private float banishDuration = 15f;
    [SerializeField] private int uses = 1;
    [SerializeField] private KeyCode testKey = KeyCode.B;

    public void Use()
    {
        if (uses <= 0) return;

        MonsterAI monster = FindObjectOfType<MonsterAI>();
        if (monster == null) return;

        uses--;
        monster.RequestForceDespawn(banishDuration);
    }

    private void Update()
    {
        if (Input.GetKeyDown(testKey)) Use();
    }
}
