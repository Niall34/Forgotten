namespace Forgotten.Player
{
    public class SpectatorController : MonoBehaviour
    {
        [SerializeField] private Camera spectatorCamera;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, -4f);

        private List<PlayerHealthStateMachine> livingPlayers = new List<PlayerHealthStateMachine>();
        private int currentIndex;

        public void BeginSpectating(PlayerHealthStateMachine self)
        {
            var ownCamera = GetComponentInChildren<Camera>(true);
            if (ownCamera != null && ownCamera != spectatorCamera) ownCamera.enabled = false;
            if (spectatorCamera != null) spectatorCamera.enabled = true;

            livingPlayers = FindObjectsOfType<PlayerHealthStateMachine>()
                .Where(p => p != self && p.CurrentState != PlayerLifeState.Dead)
                .ToList();

            currentIndex = 0;
        }

        private void Update()
        {
            if (spectatorCamera == null || !spectatorCamera.enabled) return;

            livingPlayers.RemoveAll(p => p == null || p.CurrentState == PlayerLifeState.Dead);
            if (livingPlayers.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.D)) currentIndex = (currentIndex + 1) % livingPlayers.Count;
            if (Input.GetKeyDown(KeyCode.A)) currentIndex = (currentIndex - 1 + livingPlayers.Count) % livingPlayers.Count;

            var target = livingPlayers[currentIndex].transform;
            spectatorCamera.transform.position = target.position + target.TransformDirection(followOffset);
            spectatorCamera.transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}
