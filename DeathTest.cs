using UnityEngine;
using Forgotten.Player;

public class DeathTest : MonoBehaviour
{
    [SerializeField] private PlayerHealthStateMachine healthState;
    [SerializeField] private KeyCode killKey = KeyCode.K;

    private void Update()
    {
        if (Input.GetKeyDown(killKey))
        {
            healthState.Debug_Kill();
        }
    }
}
