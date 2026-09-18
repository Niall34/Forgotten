using UnityEngine;
using UnityEngine.UIElements;

public class UIPrompt : MonoBehaviour
{
    public enum PromptType { Info, Warning, Success, Error }

    private UIDocument uiDocument;
    private Button pickupButton;
    private Button installButton;
    private VisualElement pickupPromptPanel;
    private VisualElement installPromptPanel;
    private Label installProgressText;
    private Label messageText;

    private GeneratorPiece currentPiece;
    private PlayerInventory currentInventory;

    private float messageShowTimer = 0f;
    private float messageDuration = 2f;

private void Start()
{
    uiDocument = GetComponent<UIDocument>();
    if (uiDocument == null)
    {
        Debug.LogError("UIDocument not found!");
        return;
    }

    var root = uiDocument.rootVisualElement;

    pickupButton = root.Q<Button>("pickupButton");
    installButton = root.Q<Button>("installButton");
    pickupPromptPanel = root.Q<VisualElement>("pickupPromptPanel");
    installPromptPanel= root.Q<VisualElement>("installPromptPanel");
    installProgressText= root.Q<Label>("installProgressText");
    messageText = root.Q<Label>("messageText");

    if (pickupButton != null)
        {
            pickupButton.RegisterCallback<ClickEvent>(OnPickUpButtonClicked);
        }
        if (installButton != null)
        {
            installButton.RegisterCallback<PointerDownEvent>(OnInstallButtonDown);
            installButton.RegisterCallback<PointerUpEvent>(OnInstallButtonUp);
        }

        HidePickupPrompt();
        HideInstallPrompt();
        HideMessage();
}

    private void Update()
    {
        // MESSAGE TIME
        if (messageShowTimer > 0)
        {
            messageShowTimer -= Time.deltaTime;
            if (messageShowTimer <= 0)
            {
                HideMessage();
            }
        }
    }
    
    // PICK UP GENERATOR PIECE METHODS 

    public void ShowPickupPrompt(GeneratorPiece piece, PlayerInventory inventory)
    {
        currentPiece = piece;
        currentInventory = inventory;

        if (pickupPromptPanel != null)
        {
            pickupPromptPanel.style.display = DisplayStyle.Flex;
            Debug.Log("Pickup prompt shown");
        }
    }

    public void HidePickupPrompt()
    {
        if (pickupPromptPanel != null)
        {
            pickupPromptPanel.style.display = DisplayStyle.None;
        }
    }

    private void OnPickUpButtonClicked(ClickEvent evt)
    {
        Debug.Log("Pickup button clicked!");

        if (currentPiece != null && currentInventory != null)
        {
            currentInventory.PickUpPiece(currentPiece);            
            HidePickupPrompt();

        }
        else
        {
            ShowMessage("No piece selected!", PromptType.Error);
        }
    }

    // INSTALL METHODS 

    public void ShowInstallPrompt(PlayerInventory inventory)
    {
        currentInventory = inventory;

        if (installPromptPanel != null)
        {
            installPromptPanel.style.display = DisplayStyle.Flex;

            if (installProgressText != null)
            {
                installProgressText.text = "0%";
            }
            Debug.Log("Install prompt shown");
        }
    }

    public void HideInstallPrompt()
    {
        if (installPromptPanel != null)
        {
            installPromptPanel.style.display = DisplayStyle.None;
        }
    }

    private void OnInstallButtonDown(PointerDownEvent evt)
    {
        Debug.Log("Install button clicked!");

        if (currentInventory != null)
        {
            currentInventory.OnInstallButtonDown();
        }
    }

    private void OnInstallButtonUp(PointerUpEvent evt)
    {
        Debug.Log("Install button released!");

        if (currentInventory != null)
        {
            currentInventory.OnInstallButtonUp();
        }
    }

    

    public void UpdateInstallProgress(float progress)
    {
        if (installProgressText != null)
        {
            int percentage = Mathf.RoundToInt(progress * 100);
            installProgressText.text = $"{percentage}%";
        }
    }

    // MESSAGE METHODSSSSS

    public void ShowMessage(string message, PromptType type = PromptType.Info)
    {
        if (messageText == null)
        return;

        messageText.text = message;
        messageShowTimer = messageDuration;
        messageText.style.display = DisplayStyle.Flex;

        Debug.Log($"Message: {message} ({type})");
    }

    private void HideMessage()
    {
        if (messageText != null)
        {
            messageText.style.display = DisplayStyle.None;
        }
    }
}


