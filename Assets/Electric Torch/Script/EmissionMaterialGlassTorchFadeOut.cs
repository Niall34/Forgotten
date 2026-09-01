//  - EmissionMaterialGlassTorchFadeOut - Script by Marcelli Michele

// This script is attached to child light of the Electric Torch
// and takes the emission material in conjunction with the light

using UnityEngine;

public class EmissionMaterialGlassTorchFadeOut : MonoBehaviour
{
    private Renderer _mat;
    private float _intensity = 0;

    Color _alphaStart;

    ElectricTorchOnOff _torchOnOff;

    private void Start()
    {
        _mat = GetComponent<Renderer>();
        _alphaStart = _mat.material.color;

        GameObject _torchLight = GameObject.Find("Torch Light");

        if (_torchLight != null) {_torchOnOff = _torchLight.GetComponent<ElectricTorchOnOff>();}
        if (_torchLight == null) {Debug.Log("Cannot find 'ElectricTorchOnOff' script");}

        _intensity = _torchOnOff.intensityLight;
    }

    private void Update()
    {
        _intensity = _torchOnOff.intensityLight;
    }

    public void TimeEmission(float t)
    {
        _intensity -= t * Time.deltaTime;
        _mat.material.SetColor("_EmissionColor", _alphaStart * _intensity);
        if (_intensity < 0)
        {
            _intensity = 0;
        }
    }

    public void OffEmission()
    {
        _mat.material.SetColor("_EmissionColor", _alphaStart * Color.black);
    }    
    public void OnEmission()
    {
        _mat.material.SetColor("_EmissionColor", _alphaStart * _intensity);
    }

}
