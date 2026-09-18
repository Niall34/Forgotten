using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIPrompt : MonoBehaviourPun
{
    public enum PromptType { Info, Warning, Success, Error }

    [Header("UI Colours")]
    public Color infoColor = new Color(0.2f, 0.4f, 0.8f, 0.9f);
    public Color warningColor = new Color(0.8f, 0.6f, 0.2f, 0.9f);
    public Color successColor = new Color(0.2f, 0.8f, 0.4f, 0.9f);
    public Color errorColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
    
    private Canvas promptCanvas;
    private GameObject pickupPromptPanel;
    private GameObject installPromptPanel;
    private Button pickupButton;
    private Button installButton;
    private TextMeshProUGUI installProgressText;
    private TextMeshProUGUI messageText;
    private Image messageBackground;
    private float messageShowTimer = 0f;
    private float messageDuration = 2f;

    private PlayerInventory playerInventory;
    private GeneratorPiece currentPiece;

    private void Start()
    {
        CreatePromptUI();
        playerInventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        // this handles install button 
        if (installButton != null && installPromptPanel.activeInHierarchy)
        {
            if (Input.GetMouseButton(0)) //simulate touch as mouse click (TESTING)
            {
                RectTransform installRect = installButton.GetComponent<RectTransform>();
                Vector2 mousePos = Input.mousePosition;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(installRect, mousePos, null, out Vector2 localPoint) && installRect.rect.Contains(localPoint))
                {
                    playerInventory.OnInstallButtonDown();
                    playerInventory.UpdateInstallProgress();     
                }
            }
            else 
            {
                playerInventory.OnInstallButtonUp();
            }
        }

        if (messageShowTimer > 0)
        {
            messageShowTimer-= Time.deltaTime;
            if (messageShowTimer <= 0)
            {
                messageText.canvas.gameObject.SetActive(false);
            }
        }
    }

    private void CreatePromptUI()
    {
        GameObject canvasObj = new GameObject("Prompt Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = Vector3.zero;

        promptCanvas = canvasObj.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 100;

        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // PICK UP PROMPT 
        pickupPromptPanel = new GameObject("PickupPrompt");
        pickupPromptPanel.transform.SetParent(canvasObj.transform);
        pickupPromptPanel.transform.localPosition = Vector3.zero;

        RectTransform pickupRect = pickupPromptPanel.AddComponent<RectTransform>();
        pickupRect.sizeDelta = new Vector2(300, 120);
        pickupRect.anchoredPosition = new Vector2(0, -150);
 
        Image pickupBg = pickupPromptPanel.AddComponent<Image>();
        pickupBg.color = infoColor;
 
        // Pickup Button
        GameObject pickupButtonObj = new GameObject("Button");
        pickupButtonObj.transform.SetParent(pickupPromptPanel.transform);
        pickupButtonObj.transform.localPosition = Vector3.zero;
 
        RectTransform pickupBtnRect = pickupButtonObj.AddComponent<RectTransform>();
        pickupBtnRect.sizeDelta = new Vector2(280, 100);
 
        pickupButton = pickupButtonObj.AddComponent<Button>();
        pickupButton.targetGraphic = pickupBg;
 
        Image pickupBtnImage = pickupButtonObj.AddComponent<Image>();
        pickupBtnImage.color = infoColor;
 
        // Pickup Button Text
        GameObject pickupTextObj = new GameObject("Text");
        pickupTextObj.transform.SetParent(pickupButtonObj.transform);
        pickupTextObj.transform.localPosition = Vector3.zero;
 
        RectTransform pickupTextRect = pickupTextObj.AddComponent<RectTransform>();
        pickupTextRect.sizeDelta = new Vector2(280, 100);
 
        TextMeshProUGUI pickupText = pickupTextObj.AddComponent<TextMeshProUGUI>();
        pickupText.text = "PICK UP PIECE";
        pickupText.alignment = TextAlignmentOptions.Center;
        pickupText.fontSize = 40;
        pickupText.color = Color.white;
 
        pickupButton.onClick.AddListener(OnPickupButtonClicked);
        pickupPromptPanel.SetActive(false);
 
        // ========== INSTALL PROMPT ==========
        installPromptPanel = new GameObject("InstallPrompt");
        installPromptPanel.transform.SetParent(canvasObj.transform);
        installPromptPanel.transform.localPosition = Vector3.zero;
 
        RectTransform installRect = installPromptPanel.AddComponent<RectTransform>();
        installRect.sizeDelta = new Vector2(300, 150);
        installRect.anchoredPosition = new Vector2(0, -150);
 
        Image installBg = installPromptPanel.AddComponent<Image>();
        installBg.color = warningColor;
 
        // Install Button
        GameObject installButtonObj = new GameObject("Button");
        installButtonObj.transform.SetParent(installPromptPanel.transform);
        installButtonObj.transform.localPosition = new Vector3(0, 20, 0);
 
        RectTransform installBtnRect = installButtonObj.AddComponent<RectTransform>();
        installBtnRect.sizeDelta = new Vector2(280, 80);
 
        installButton = installButtonObj.AddComponent<Button>();
        installButton.targetGraphic = installBg;
 
        Image installBtnImage = installButtonObj.AddComponent<Image>();
        installBtnImage.color = warningColor;
 
        // Install Button Text
        GameObject installTextObj = new GameObject("Text");
        installTextObj.transform.SetParent(installButtonObj.transform);
        installTextObj.transform.localPosition = Vector3.zero;
 
        RectTransform installTextRect = installTextObj.AddComponent<RectTransform>();
        installTextRect.sizeDelta = new Vector2(280, 80);
 
        TextMeshProUGUI installText = installTextObj.AddComponent<TextMeshProUGUI>();
        installText.text = "HOLD TO INSTALL";
        installText.alignment = TextAlignmentOptions.Center;
        installText.fontSize = 36;
        installText.color = Color.black;
 
        // Progress Text
        GameObject progressTextObj = new GameObject("Progress");
        progressTextObj.transform.SetParent(installPromptPanel.transform);
        progressTextObj.transform.localPosition = new Vector3(0, -40, 0);
 
        RectTransform progressTextRect = progressTextObj.AddComponent<RectTransform>();
        progressTextRect.sizeDelta = new Vector2(280, 60);
 
        installProgressText = progressTextObj.AddComponent<TextMeshProUGUI>();
        installProgressText.text = "0%";
        installProgressText.alignment = TextAlignmentOptions.Center;
        installProgressText.fontSize = 32;
        installProgressText.color = Color.black;
 
        installPromptPanel.SetActive(false);
 
        // ========== MESSAGE DISPLAY ==========
        GameObject messageObj = new GameObject("MessageDisplay");
        messageObj.transform.SetParent(canvasObj.transform);
        messageObj.transform.localPosition = new Vector3(0, 200, 0);
 
        RectTransform messageRect = messageObj.AddComponent<RectTransform>();
        messageRect.sizeDelta = new Vector2(400, 100);
 
        messageBackground = messageObj.AddComponent<Image>();
        messageBackground.color = successColor;
 
        // Message Text
        GameObject messageTxtObj = new GameObject("Text");
        messageTxtObj.transform.SetParent(messageObj.transform);
        messageTxtObj.transform.localPosition = Vector3.zero;
 
        RectTransform messageTxtRect = messageTxtObj.AddComponent<RectTransform>();
        messageTxtRect.sizeDelta = new Vector2(380, 80);
 
        messageText = messageTxtObj.AddComponent<TextMeshProUGUI>();
        messageText.text = "Ready!";
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 36;
        messageText.color = Color.white;
 
        messageObj.SetActive(false);
    }
 
    private void OnPickupButtonClicked()
    {
        Debug.Log($"Pickup button clicked! currentPiece : {currentPiece}, playerInventory : {playerInventory}");

        if (currentPiece != null && playerInventory != null)
        {
            playerInventory.PickUpPiece(currentPiece);
        }
    }
 
    public void ShowPickupPrompt(GeneratorPiece piece, PlayerInventory inventory)
    {
        currentPiece = piece;
        playerInventory = inventory;
        if (pickupPromptPanel != null)
        {
            pickupPromptPanel.SetActive(true);
        }
    }
 
    public void HidePickupPrompt()
    {
        if (pickupPromptPanel != null)
        {
            pickupPromptPanel.SetActive(false);
        }
    }
 
    public void ShowInstallPrompt(PlayerInventory inventory)
    {
        playerInventory = inventory;
        if (installPromptPanel != null)
        {
            installPromptPanel.SetActive(true);
        }
    }
 
    public void HideInstallPrompt()
    {
        if (installPromptPanel != null)
        {
            installPromptPanel.SetActive(false);
        }
    }
 
    public void UpdateInstallProgress(float progress)
    {
        if (installProgressText != null)
        {
            int percentage = Mathf.RoundToInt(progress * 100);
            installProgressText.text = $"{percentage}%";
 
            // Change color based on progress
            if (progress < 0.5f)
            {
                installProgressText.color = Color.black;
            }
            else
            {
                installProgressText.color = Color.white;
            }
        }
    }
 
    public void ShowMessage(string message, PromptType type = PromptType.Info)
    {
        messageText.text = message;
        messageShowTimer = messageDuration;
 
        // Change color based on type
        switch (type)
        {
            case PromptType.Info:
                messageBackground.color = infoColor;
                messageText.color = Color.white;
                break;
            case PromptType.Warning:
                messageBackground.color = warningColor;
                messageText.color = Color.black;
                break;
            case PromptType.Success:
                messageBackground.color = successColor;
                messageText.color = Color.white;
                break;
            case PromptType.Error:
                messageBackground.color = errorColor;
                messageText.color = Color.white;
                break;
        }
 
        messageText.canvas.gameObject.SetActive(true);
    }
}