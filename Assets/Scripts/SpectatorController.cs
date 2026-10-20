using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace Forgotten.Player
{
    public class SpectatorController : MonoBehaviour
    {
        [SerializeField] private Camera spectatorCamera;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, -4f);

        private List<PlayerHealthStateMachine> livingPlayers = new List<PlayerHealthStateMachine>();
        private int currentIndex;
        private bool spectating;
        private PhotonView ownerView; // only the copy you own is allowed to take over a camera
        private PlayerHealthStateMachine ownHealth;

        public void SetCamera(Camera camera) => spectatorCamera = camera;

        private void Awake()
        {
            ownerView = GetComponentInParent<PhotonView>();
            ownHealth = GetComponent<PlayerHealthStateMachine>();
        }

        public void BeginSpectating(PlayerHealthStateMachine self)
        {
            // every client runs the death for every player, only the owner's copy should touch a camera
            if (ownerView != null && !ownerView.IsMine) return;

            spectating = true;

            // the player camera is made in code at runtime so the inspector can't point at it, borrow it here
            if (spectatorCamera == null)
            {
                PlayerController controller = GetComponentInParent<PlayerController>();
                if (controller != null) spectatorCamera = controller.PlayerCamera;
            }
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
            // the health script should start us, but if its call never got here start anyway once we know you are dead
            if (!spectating && ownHealth != null && ownHealth.IsDead) BeginSpectating(ownHealth);

            if (RoomChatUI.CapturesInput) return;
            if (!spectating || spectatorCamera == null || !spectatorCamera.enabled) return;

            livingPlayers.RemoveAll(p => p == null || p.CurrentState == PlayerLifeState.Dead);
            if (livingPlayers.Count == 0) return;
            currentIndex %= livingPlayers.Count;

            if (Input.GetKeyDown(KeyCode.D)) currentIndex = (currentIndex + 1) % livingPlayers.Count;
            if (Input.GetKeyDown(KeyCode.A)) currentIndex = (currentIndex - 1 + livingPlayers.Count) % livingPlayers.Count;

            var target = livingPlayers[currentIndex].transform;
            spectatorCamera.transform.position = target.position + target.TransformDirection(followOffset);
            spectatorCamera.transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}