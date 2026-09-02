using UnityEngine;

// simple flashlight helper, PlayerController owns the actual toggle logic and just calls SetLightOn on this from up there
public class PlayerFlashLight : MonoBehaviour
{
    public Light torchLight;
    public float onIntensity = 2.5f;

    private void Start() // make sure it starts off, in case the light was left on in the editor
    {
        torchLight.intensity = 0f;
    }

    public void SetLightOn(bool isOn) // called by PlayerController, runs on every client
    {
        torchLight.intensity = isOn ? onIntensity : 0f;
    }
}
