using UnityEngine;
using HorrorGame.Player;

public class InjuredTest : MonoBehaviour
{
    [SerializeField] private PlayerHealthStateMachine healthState;
    [SerializeField] private KeyCode injureKey = KeyCode.I;
    [SerializeField] private int damageAmount = 60;

    private void Update()
    {
        if (Input.GetKeyDown(injureKey))
        {
            healthState.CurrentHealth -= damageAmount;
            Debug.Log($"Health: {healthState.CurrentHealth}, State: {healthState.CurrentState}");
        }
    }
}
