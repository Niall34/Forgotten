using UnityEngine;

// simple flashlight helper, put this on whatever child object has the Light component (the torch model's light).
// no networking in here at all - Photon RPCs can't target components on child objects, so PlayerController
// owns the actual RPC/toggle logic and just calls SetLightOn on this from up there
public class PlayerFlashLight : MonoBehaviour
{
    public Light torchLight;
    public float onIntensity = 300f;

    [Header("Flicker")]
    public float flickerSpeed = 7f; // higher = faster flicker
    public float flickerMinIntensity = 290f; // how low the dips go

    private bool isOn = false;
    private float flickerSeed; // random offset so multiple players' torches don't flicker in sync with each other

    private void Start() // make sure it starts off, in case the light was left on in the editor
    {
        torchLight.intensity = 0f;
        flickerSeed = Random.Range(0f, 100f);
    }

    private void Update() // gentle Perlin-noise flicker while the light is on, feels more like an old bulb than random jitter would
    {
        if (isOn == false)
        {
            return;
        }

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, flickerSeed);
        torchLight.intensity = Mathf.Lerp(flickerMinIntensity, onIntensity, noise);
    }

    public void SetLightOn(bool state) // called by PlayerController after the RPC comes in, runs on every client
    {
        isOn = state;
        torchLight.intensity = isOn ? onIntensity : 0f;
    }
}
