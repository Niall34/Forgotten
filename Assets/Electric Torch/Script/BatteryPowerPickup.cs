// - BatteryPowerPickup - Script by Marcelli Michele
// Attach this script on a GameObject (battery pickup) with collider component


using UnityEngine;

public class BatteryPowerPickup : MonoBehaviour
{
    ElectricTorchOnOff _torchOnOff;
    //
    public float PowerIntensityLight;

    private void Awake()
    {
        _torchOnOff = FindObjectOfType<ElectricTorchOnOff>();
    }
    void OnTriggerEnter(Collider other)
    {
        if (other)
        {
            _torchOnOff._PowerPickUp = true;
            _torchOnOff.intensityLight = PowerIntensityLight;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        _torchOnOff._PowerPickUp = false;
    }
}
