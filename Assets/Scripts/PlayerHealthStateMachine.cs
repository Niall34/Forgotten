using UnityEngine;

namespace Forgotten.Player
{
    public enum PlayerLifeState { Healthy, Injured, Dead }

    [RequireComponent(typeof(CharacterController))]
    public class PlayerHealthStateMachine : MonoBehaviour
    {
        [Header("Health Value")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int injuredHealthThreshold = 50;
        [SerializeField] private int deadHealthThreshold = 0;
        [Range(0.1f, 1f)]
        [SerializeField] private float injuredSpeedMultiplier = 0.5f;
        [SerializeField] private MonoBehaviour movementController;
        [SerializeField] private SpectatorController spectatorController;
        private int currentHealth;

        public PlayerLifeState CurrentState { get; private set; } = PlayerLifeState.Healthy;
        public float SpeedMultiplier { get; private set; } = 1f;
        public bool IsDead => CurrentState == PlayerLifeState.Dead;

        public int CurrentHealth
        {
            get => currentHealth;
            set
            {
                if (IsDead) return;
                currentHealth = Mathf.Clamp(value, 0, maxHealth);
                if (currentHealth <= deadHealthThreshold) SetDead();
                else if (currentHealth <= injuredHealthThreshold) SetInjured();
                else
                {
                    CurrentState = PlayerLifeState.Healthy;
                    SpeedMultiplier = 1f;
                }
            }
        }

        private void Awake() => currentHealth = maxHealth;

        public void Configure(MonoBehaviour movement, SpectatorController spectator)
        {
            movementController = movement;
            spectatorController = spectator;
        }

        public void SetInjured()
        {
            if (IsDead) return;
            CurrentState = PlayerLifeState.Injured;
            SpeedMultiplier = injuredSpeedMultiplier;
        }

        public void SetDead()
        {
            if (IsDead) return;
            CurrentState = PlayerLifeState.Dead;
            SpeedMultiplier = 0f;
            if (movementController != null) movementController.enabled = false;
            GetComponent<CharacterController>().enabled = false;
            spectatorController?.BeginSpectating(this);
        }
    }
}
