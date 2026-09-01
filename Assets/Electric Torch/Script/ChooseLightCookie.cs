// - ChooseLightCookie - Script by Marcelli Michele

// This script is attached in primary model (default) of the Electric Torch.
// You can insert in the List any cookie light texture and choose it to be used in "Cookie" Light for Electric Torch
// It's possible to choose any letter on the keyboard to control the texture change

using System.Collections.Generic;
using UnityEngine;

public class ChooseLightCookie : MonoBehaviour
{
    public string chooseKeyForCookie = "E";
    private KeyCode _keyCode;
    [Space]
    public List<Texture> lightCookie = new List<Texture>();
    private Light _thisLight;
    private int _scroolList = 0;

    void Awake()
    {
        _thisLight = GetComponent<Light>();
    }

     void Start()
    {
        _keyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), chooseKeyForCookie);
    }

    void Update()
    {
        // detecting parse error keyboard type
        if (System.Enum.TryParse(chooseKeyForCookie, out _keyCode))
        {
            _keyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), chooseKeyForCookie);
        }
        //

        ChooseCookie();
    }

    void ChooseCookie()
    {
        if (Input.GetKeyDown(_keyCode))
        {
            _scroolList += 1;

            if (_scroolList >= lightCookie.Count)
            {
                _scroolList = 0;
            }

            _thisLight.cookie = lightCookie[_scroolList];

        }
    }
}
