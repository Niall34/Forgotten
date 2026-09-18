using UnityEngine;

namespace Forgotten.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerHealthStateMachine : MonoBehaviour
    {
        [Header("Health Value")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int injuredHealthThreshold = 50;
        [SerializeField] private int deadHealthThreshold = 0;

        private int currentHealth;

        public int CurrentHealth
        {
            get => currentHealth;
            set
            {
                currentHealth = value;

                if (currentHealth <= deadHealthThreshold && CurrentState != PlayerLifeState.Dead)
                {
                    SetDead();
                }
                else if (currentHealth <= injuredHealthThreshold && CurrentState == PlayerLifeState.Healthy)
                {
                    SetInjured();
                }
            }
        }

        [Range(0.1f, 1f)]
        [SerializeField] private float injuredSpeedMultiplier = 0.5f;

        [SerializeField] private MonoBehaviour movementController;
        [SerializeField] private SpectatorController spectatorController;

        public PlayerLifeState CurrentState { get; private set; } = PlayerLifeState.Healthy;
        public float SpeedMultiplier { get; private set; } = 1f;

        public bool IsDead => CurrentState == PlayerLifeState.Dead;

        private void Start()
        {
            CurrentHealth = maxHealth;
        }

        public void SetInjured()
        {
            CurrentState = PlayerLifeState.Injured;
            SpeedMultiplier = injuredSpeedMultiplier;
        }

        public void SetDead()
        {
            CurrentState = PlayerLifeState.Dead;
            SpeedMultiplier = 0f;

            if (movementController != null) movementController.enabled = false;
            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            spectatorController?.BeginSpectating(this);
        }

        // --- Test helpers below. Remove once real damage/heal logic exists. ---

        [ContextMenu("Test: Apply Injury Damage")]
        public void Debug_ApplyInjuryDamage()
        {
            CurrentHealth -= (maxHealth - injuredHealthThreshold);
            Debug.Log($"[HealthTest] Health: {CurrentHealth}, State: {CurrentState}");
        }

        [ContextMenu("Test: Kill Player")]
        public void Debug_Kill()
        {
            CurrentHealth = deadHealthThreshold;
            Debug.Log($"[HealthTest] Health: {CurrentHealth}, State: {CurrentState}, IsDead: {IsDead}");
        }
    }
}
