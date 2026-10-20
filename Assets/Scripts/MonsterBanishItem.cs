using UnityEngine;

public class MonsterBanishItem : MonoBehaviour
{
    [SerializeField] private float banishDuration = 15f;
    [SerializeField] private int uses = 1;
    [SerializeField] private KeyCode testKey = KeyCode.B;

    // hook your inventory/UI button up to this
    public void Use()
    {
        if (uses <= 0) return;

        MonsterAI monster = FindObjectOfType<MonsterAI>();
        if (monster == null) return;

        uses--;
        monster.RequestForceDespawn(banishDuration);
    }

    // remove once the item is wired into the inventory
    private void Update()
    {
        if (Input.GetKeyDown(testKey)) Use();
    }
}
