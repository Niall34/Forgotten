using Photon.Pun;
using UnityEngine;

// Compatibility bridge for win messages and existing scene references.
// The backpack UI owns the pickup/install prompts and updates hold progress once per frame.
public class UIPrompt : MonoBehaviourPun
{
    public enum PromptType { Info, Warning, Success, Error }
    public void ShowMessage(string message, PromptType type = PromptType.Info, float duration = 2f)
    {
        GetComponent<PlayerInventory>()?.ShowMessage(message);
    }
    public void ShowPickupPrompt(GeneratorPiece piece, PlayerInventory inventory) { }
    public void HidePickupPrompt() { }
    public void ShowInstallPrompt(PlayerInventory inventory) { }
    public void HideInstallPrompt() { }
    public void UpdateInstallProgress(float progress) { }
}
