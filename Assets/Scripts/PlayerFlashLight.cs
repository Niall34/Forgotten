using UnityEngine;
using Photon.Pun;

// simple on/off flashlight for the player, put this on whatever child object has the Light component
// (the torch model's light, basically). call ToggleFlashlight() from a UI button's OnClick
public class PlayerFlashLight : MonoBehaviourPun
{
    public Light torchLight;
    public float onIntensity = 2.5f;
    private bool isOn = false;

    private void Start() // make sure it starts off, in case the light was left on in the editor
    {
        torchLight.intensity = 0f;
    }

    // hook this up to your touch UI button's OnClick, only reacts if this is your own player
    public void ToggleFlashLight()
    {
        if (photonView.IsMine == false)
        {
            return;
        }

        bool newState = isOn == false;
        photonView.RPC(nameof(SetFlashLightState), RpcTarget.All, newState);
    }

    [PunRPC]
    private void SetFlashLightState(bool state) // runs on every client so everyone sees the light turn on/off
    {
        isOn = state;
        torchLight.intensity = isOn ? onIntensity : 0f;
    }
}
