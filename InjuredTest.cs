using UnityEngine;
using Forgotten.Player;

public class InjuredTest : MonoBehaviour
{
    [SerializeField] private PlayerHealthStateMachine healthState;
    [SerializeField] private KeyCode injureKey = KeyCode.I;

    private void Update()
    {
        if (Input.GetKeyDown(injureKey))
        {
            healthState.Debug_ApplyInjuryDamage();
        }
    }
}
