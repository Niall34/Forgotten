public class DeathTest : MonoBehaviour
{
    [SerializeField] private PlayerHealthStateMachine healthState;
    [SerializeField] private KeyCode killKey = KeyCode.K;

    private void Update()
    {
        if (Input.GetKeyDown(killKey))
        {
            healthState.CurrentHealth = 0;
            Debug.Log($"Health: {healthState.CurrentHealth}, State: {healthState.CurrentState}, IsDead: {healthState.IsDead}");
        }
    }
}
