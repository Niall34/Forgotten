using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime-built, safe-area-aware inventory HUD for the local player.
/// The inventory owns all state; this class only presents it and forwards button input.
/// </summary>
public sealed class PlayerInventoryUI : MonoBehaviour
{
    private const int HotbarSlotCount = 4;
    private const int BackpackEntryCount = 8;
    private const int CapacityPipCount = 8;

    private static readonly Color Backdrop = new Color(0.005f, 0.008f, 0.012f, 0.78f);
    private static readonly Color Panel = new Color32(15, 18, 22, 250);
    private static readonly Color Surface = new Color32(25, 30, 35, 245);
    private static readonly Color Control = new Color32(40, 47, 53, 245);
    private static readonly Color ControlDisabled = new Color32(30, 35, 40, 220);
    private static readonly Color Accent = new Color32(171, 64, 49, 255);
    private static readonly Color TextColor = new Color32(237, 231, 220, 255);
    private static readonly Color MutedText = new Color32(162, 172, 179, 255);
    private static readonly Color FullColor = new Color32(117, 41, 41, 255);

    private readonly Button[] hotbarButtons = new Button[HotbarSlotCount];
    private readonly Image[] hotbarBackgrounds = new Image[HotbarSlotCount];
    private readonly Image[] hotbarIcons = new Image[HotbarSlotCount];
    private readonly TextMeshProUGUI[] hotbarNames = new TextMeshProUGUI[HotbarSlotCount];
    private readonly TextMeshProUGUI[] hotbarCosts = new TextMeshProUGUI[HotbarSlotCount];

    private readonly Button[] backpackButtons = new Button[BackpackEntryCount];
    private readonly Image[] backpackBackgrounds = new Image[BackpackEntryCount];
    private readonly Image[] backpackIcons = new Image[BackpackEntryCount];
    private readonly TextMeshProUGUI[] backpackNames = new TextMeshProUGUI[BackpackEntryCount];
    private readonly TextMeshProUGUI[] backpackCosts = new TextMeshProUGUI[BackpackEntryCount];
    private readonly Image[] capacityPips = new Image[CapacityPipCount];

    private PlayerInventory inventory;
    private GameObject canvasObject;
    private GameObject blockerObject;
    private GameObject backpackPanelObject;
    private Button backpackToggleButton;
    private TextMeshProUGUI backpackToggleLabel;
    private Button pickupButton;
    private Image pickupBackground;
    private TextMeshProUGUI pickupLabel;
    private GameObject installButtonObject;
    private Image installBackground;
    private TextMeshProUGUI installLabel;
    private TextMeshProUGUI capacityLabel;
    private TextMeshProUGUI selectedItemLabel;
    private Button dropButton;
    private TextMeshProUGUI dropLabel;
    private GameObject toastObject;
    private TextMeshProUGUI toastLabel;
    private Coroutine toastRoutine;

    private void Update()
    {
        if (inventory == null || canvasObject == null)
            return;

        bool shouldShow = inventory.CanInteract;
        if (!shouldShow && inventory.IsBackpackOpen)
            inventory.SetBackpackOpen(false);

        if (canvasObject.activeSelf == shouldShow)
            return;

        canvasObject.SetActive(shouldShow);
        if (shouldShow)
            Refresh();
    }

    /// <summary>Builds the HUD once and binds it to the supplied local-player inventory.</summary>
    public void Initialize(PlayerInventory playerInventory)
    {
        if (playerInventory == null)
        {
            Debug.LogError("PlayerInventoryUI requires a PlayerInventory to initialize.", this);
            return;
        }

        if (inventory != null)
        {
            inventory.Changed -= Refresh;
            inventory.Message -= ShowMessage;
        }

        inventory = playerInventory;
        EnsureEventSystem();

        if (canvasObject == null)
            BuildInterface();

        inventory.Changed += Refresh;
        inventory.Message += ShowMessage;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
            inventory.Message -= ShowMessage;
        }

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void BuildInterface()
    {
        canvasObject = new GameObject(
            "Player Inventory Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // UIPrompt uses order 100; the backpack blocker must also stop pickup/install touches.
        canvas.sortingOrder = 110;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform blocker = CreateRect("Backpack Touch Blocker", canvasObject.transform);
        Stretch(blocker);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = Backdrop;
        blockerImage.raycastTarget = true;
        Button blockerButton = blocker.gameObject.AddComponent<Button>();
        blockerButton.targetGraphic = blockerImage;
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.navigation = NoNavigation();
        blockerButton.onClick.AddListener(() => inventory?.SetBackpackOpen(false));
        blockerObject = blocker.gameObject;

        RectTransform safeArea = CreateRect("Safe Area", canvasObject.transform);
        Stretch(safeArea);
        safeArea.gameObject.AddComponent<MobileSafeArea>();

        BuildHotbar(safeArea);
        BuildPickupButton(safeArea);
        BuildInstallButton(safeArea);
        BuildBackpackPanel(safeArea);
        BuildToast(safeArea);
    }

    private void BuildHotbar(Transform parent)
    {
        RectTransform hotbar = CreateRect("Quick Slots", parent);
        Anchor(hotbar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        hotbar.anchoredPosition = new Vector2(0f, 16f);
        hotbar.sizeDelta = new Vector2(426f, 82f);
        hotbar.localScale = Vector3.one * ForgottenGameSettings.HudScale;
        Image hotbarBackground = hotbar.gameObject.AddComponent<Image>();
        hotbarBackground.color = new Color(0.025f, 0.03f, 0.04f, 0.78f);
        hotbarBackground.raycastTarget = false;

        const float slotSize = 66f;
        const float slotGap = 8f;
        const float slotStartX = -204f;

        for (int i = 0; i < HotbarSlotCount; i++)
        {
            int itemIndex = i;
            RectTransform slot = CreateRect("Quick Slot " + (i + 1), hotbar);
            Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slot.anchoredPosition = new Vector2(slotStartX + slotSize * 0.5f + i * (slotSize + slotGap), 0f);
            slot.sizeDelta = new Vector2(slotSize, slotSize);

            Image background = slot.gameObject.AddComponent<Image>();
            background.color = Control;
            Button button = slot.gameObject.AddComponent<Button>();
            ConfigureButton(button, background);
            button.onClick.AddListener(() => inventory?.SelectSlot(itemIndex));

            Image icon = CreateImage("Icon", slot, Color.white);
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(38f, 38f), new Vector2(0f, 7f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI name = CreateText(slot, "Name", "EMPTY", 11f, TextColor, TextAlignmentOptions.Center);
            SetRect(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(58f, 18f), new Vector2(0f, 3f), new Vector2(0.5f, 0f));
            name.enableAutoSizing = true;
            name.fontSizeMin = 8f;
            name.fontSizeMax = 11f;

            TextMeshProUGUI number = CreateText(slot, "Slot Number", (i + 1).ToString(), 10f, MutedText, TextAlignmentOptions.TopLeft);
            Stretch(number.rectTransform);
            number.rectTransform.offsetMin = new Vector2(5f, 3f);
            number.rectTransform.offsetMax = new Vector2(-4f, -3f);

            TextMeshProUGUI cost = CreateText(slot, "Capacity Cost", string.Empty, 10f, TextColor, TextAlignmentOptions.TopRight);
            Stretch(cost.rectTransform);
            cost.rectTransform.offsetMin = new Vector2(4f, 3f);
            cost.rectTransform.offsetMax = new Vector2(-5f, -3f);

            hotbarButtons[i] = button;
            hotbarBackgrounds[i] = background;
            hotbarIcons[i] = icon;
            hotbarNames[i] = name;
            hotbarCosts[i] = cost;
        }

        backpackToggleButton = CreateButton(hotbar, "Backpack Button", "BACKPACK", Accent);
        RectTransform toggleRect = (RectTransform)backpackToggleButton.transform;
        Anchor(toggleRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        toggleRect.anchoredPosition = new Vector2(144f, 0f);
        toggleRect.sizeDelta = new Vector2(116f, 66f);
        backpackToggleLabel = backpackToggleButton.GetComponentInChildren<TextMeshProUGUI>();
        backpackToggleLabel.fontSize = 13f;
        backpackToggleButton.onClick.AddListener(ToggleBackpack);
    }

    private void BuildPickupButton(Transform parent)
    {
        pickupButton = CreateButton(parent, "Pickup Button", "PICK UP", Accent);
        RectTransform rect = (RectTransform)pickupButton.transform;
        Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        rect.anchoredPosition = new Vector2(0f, 108f);
        rect.sizeDelta = new Vector2(330f, 64f);
        rect.localScale = Vector3.one * ForgottenGameSettings.HudScale;
        pickupBackground = pickupButton.targetGraphic as Image;
        pickupLabel = pickupButton.GetComponentInChildren<TextMeshProUGUI>();
        pickupLabel.fontSize = 16f;
        pickupButton.onClick.AddListener(() => inventory?.TryPickUpNearestPiece());
    }

    private void BuildInstallButton(Transform parent)
    {
        RectTransform root = CreateRect("Install Hold Button", parent);
        Anchor(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        root.anchoredPosition = new Vector2(0f, 108f);
        root.sizeDelta = new Vector2(330f, 64f);
        root.localScale = Vector3.one * ForgottenGameSettings.HudScale;

        installBackground = root.gameObject.AddComponent<Image>();
        installBackground.color = Accent;
        installBackground.raycastTarget = true;
        InventoryInstallHoldButton holdButton = root.gameObject.AddComponent<InventoryInstallHoldButton>();
        holdButton.Initialize(inventory);

        installLabel = CreateText(root, "Label", "HOLD TO INSTALL  0%", 16f, TextColor, TextAlignmentOptions.Center);
        Stretch(installLabel.rectTransform);
        installLabel.rectTransform.offsetMin = new Vector2(10f, 4f);
        installLabel.rectTransform.offsetMax = new Vector2(-10f, -4f);
        installButtonObject = root.gameObject;
    }

    private void BuildToast(Transform parent)
    {
        RectTransform toast = CreateRect("Inventory Message", parent);
        Anchor(toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        toast.anchoredPosition = new Vector2(0f, -18f);
        toast.sizeDelta = new Vector2(410f, 44f);
        toast.localScale = Vector3.one * ForgottenGameSettings.HudScale;
        Image background = toast.gameObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.045f, 0.055f, 0.94f);
        background.raycastTarget = false;
        toastLabel = CreateText(toast, "Message", string.Empty, 16f, TextColor, TextAlignmentOptions.Center);
        Stretch(toastLabel.rectTransform);
        toastLabel.rectTransform.offsetMin = new Vector2(12f, 4f);
        toastLabel.rectTransform.offsetMax = new Vector2(-12f, -4f);
        toastObject = toast.gameObject;
        toastObject.SetActive(false);
    }

    private void BuildBackpackPanel(Transform parent)
    {
        RectTransform panel = CreateRect("Backpack Panel", parent);
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.sizeDelta = new Vector2(660f, 500f);
        panel.localScale = Vector3.one * ForgottenGameSettings.HudScale;
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = Panel;
        panelImage.raycastTarget = true;
        backpackPanelObject = panel.gameObject;

        TextMeshProUGUI title = CreateText(panel, "Title", "BACKPACK", 28f, TextColor, TextAlignmentOptions.Left);
        SetAnchors(title.rectTransform, new Vector2(0.045f, 0.875f), new Vector2(0.55f, 0.97f));

        Button closeButton = CreateButton(panel, "Close Button", "X", Control);
        SetRect(
            (RectTransform)closeButton.transform,
            Vector2.one,
            new Vector2(64f, 64f),
            new Vector2(-18f, -18f),
            Vector2.one);
        closeButton.onClick.AddListener(() => inventory?.SetBackpackOpen(false));

        capacityLabel = CreateText(panel, "Capacity Label", "CAPACITY 0 / 8", 15f, MutedText, TextAlignmentOptions.Right);
        SetAnchors(capacityLabel.rectTransform, new Vector2(0.53f, 0.89f), new Vector2(0.88f, 0.965f));

        RectTransform pips = CreateRect("Capacity Pips", panel);
        SetAnchors(pips, new Vector2(0.045f, 0.79f), new Vector2(0.955f, 0.865f));
        const float pipGap = 7f;
        const float pipWidth = 67f;
        for (int i = 0; i < CapacityPipCount; i++)
        {
            Image pip = CreateImage("Capacity " + (i + 1), pips, Control);
            RectTransform pipRect = pip.rectTransform;
            Anchor(pipRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            pipRect.anchoredPosition = new Vector2(i * (pipWidth + pipGap), 0f);
            pipRect.sizeDelta = new Vector2(pipWidth, 10f);
            pip.raycastTarget = false;
            capacityPips[i] = pip;
        }

        RectTransform entries = CreateRect("Items", panel);
        SetAnchors(entries, new Vector2(0.035f, 0.23f), new Vector2(0.965f, 0.775f));
        for (int i = 0; i < BackpackEntryCount; i++)
        {
            int itemIndex = i;
            int column = i % 2;
            int row = i / 2;
            RectTransform entry = CreateRect("Item " + (i + 1), entries);
            Anchor(entry, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            entry.anchoredPosition = new Vector2(column * 310f, -row * 70f);
            entry.sizeDelta = new Vector2(300f, 64f);

            Image background = entry.gameObject.AddComponent<Image>();
            background.color = Control;
            Button button = entry.gameObject.AddComponent<Button>();
            ConfigureButton(button, background);
            button.onClick.AddListener(() => inventory?.SelectSlot(itemIndex));

            Image icon = CreateImage("Icon", entry, Color.white);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 40f), new Vector2(7f, 0f), new Vector2(0f, 0.5f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI name = CreateText(entry, "Name", "EMPTY SLOT", 15f, TextColor, TextAlignmentOptions.Left);
            SetAnchors(name.rectTransform, new Vector2(0.19f, 0.18f), new Vector2(0.79f, 0.82f));
            name.enableAutoSizing = true;
            name.fontSizeMin = 11f;
            name.fontSizeMax = 15f;

            TextMeshProUGUI cost = CreateText(entry, "Capacity Cost", string.Empty, 13f, MutedText, TextAlignmentOptions.Right);
            SetAnchors(cost.rectTransform, new Vector2(0.78f, 0.18f), new Vector2(0.96f, 0.82f));

            backpackButtons[i] = button;
            backpackBackgrounds[i] = background;
            backpackIcons[i] = icon;
            backpackNames[i] = name;
            backpackCosts[i] = cost;
        }

        RectTransform details = CreateRect("Selected Item", panel);
        SetAnchors(details, new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.215f));
        Image detailsBackground = details.gameObject.AddComponent<Image>();
        detailsBackground.color = Surface;
        detailsBackground.raycastTarget = true;

        TextMeshProUGUI detailHeading = CreateText(details, "Heading", "SELECTED ITEM", 12f, MutedText, TextAlignmentOptions.Left);
        SetAnchors(detailHeading.rectTransform, new Vector2(0.035f, 0.58f), new Vector2(0.68f, 0.91f));
        selectedItemLabel = CreateText(details, "Item Name", "No item selected", 18f, TextColor, TextAlignmentOptions.Left);
        SetAnchors(selectedItemLabel.rectTransform, new Vector2(0.035f, 0.12f), new Vector2(0.68f, 0.61f));
        selectedItemLabel.enableAutoSizing = true;
        selectedItemLabel.fontSizeMin = 13f;
        selectedItemLabel.fontSizeMax = 18f;

        dropButton = CreateButton(details, "Drop Button", "DROP", Accent);
        SetAnchors((RectTransform)dropButton.transform, new Vector2(0.72f, 0.12f), new Vector2(0.965f, 0.88f));
        dropLabel = dropButton.GetComponentInChildren<TextMeshProUGUI>();
        dropLabel.fontSize = 16f;
        dropButton.onClick.AddListener(() => inventory?.DropSelected());
    }

    private void ToggleBackpack()
    {
        if (inventory != null)
            inventory.SetBackpackOpen(!inventory.IsBackpackOpen);
    }

    private void Refresh()
    {
        if (inventory == null || canvasObject == null)
            return;

        IReadOnlyList<GeneratorPiece> items = inventory.Items;
        int itemCount = items?.Count ?? 0;
        int selectedIndex = inventory.SelectedIndex;

        for (int i = 0; i < HotbarSlotCount; i++)
        {
            GeneratorPiece item = i < itemCount ? items[i] : null;
            bool selected = item != null && i == selectedIndex;
            ApplyItemPresentation(
                item,
                hotbarButtons[i],
                hotbarBackgrounds[i],
                hotbarIcons[i],
                hotbarNames[i],
                hotbarCosts[i],
                selected,
                "EMPTY");
        }

        for (int i = 0; i < BackpackEntryCount; i++)
        {
            GeneratorPiece item = i < itemCount ? items[i] : null;
            bool selected = item != null && i == selectedIndex;
            ApplyItemPresentation(
                item,
                backpackButtons[i],
                backpackBackgrounds[i],
                backpackIcons[i],
                backpackNames[i],
                backpackCosts[i],
                selected,
                "EMPTY SLOT");
        }

        int usedCapacity = Mathf.Clamp(inventory.UsedCapacity, 0, inventory.Capacity);
        capacityLabel.text = "CAPACITY " + usedCapacity + " / " + inventory.Capacity;
        for (int i = 0; i < capacityPips.Length; i++)
            capacityPips[i].color = i < usedCapacity ? Accent : Control;

        GeneratorPiece selectedItem = inventory.SelectedItem;
        bool hasSelection = selectedItem != null;
        selectedItemLabel.text = hasSelection
            ? selectedItem.DisplayName + "  •  " + UnitText(selectedItem.CapacityCost)
            : "No item selected";
        dropButton.interactable = hasSelection && inventory.CanInteract;
        dropLabel.text = hasSelection ? "DROP" : "SELECT ITEM";

        bool isOpen = inventory.IsBackpackOpen;
        blockerObject.SetActive(isOpen);
        backpackPanelObject.SetActive(isOpen);
        for (int i = 0; i < hotbarBackgrounds.Length; i++)
            hotbarBackgrounds[i].raycastTarget = !isOpen;
        backpackToggleLabel.text = (isOpen ? "CLOSE  " : "BACKPACK  ")
            + usedCapacity + "/" + inventory.Capacity;

        RefreshInstall();
        RefreshPickup();
    }

    private void RefreshInstall()
    {
        bool canInstall = inventory.CanInstall;
        installButtonObject.SetActive(canInstall);
        if (!canInstall)
            return;

        int percentage = Mathf.RoundToInt(inventory.InstallProgress * 100f);
        installLabel.text = "HOLD TO INSTALL  " + percentage + "%";
        installBackground.color = Color.Lerp(Accent, TextColor, inventory.InstallProgress * 0.25f);
    }

    private void RefreshPickup()
    {
        GeneratorPiece nearby = inventory.NearbyItem;
        if (nearby == null || inventory.IsBackpackOpen || inventory.CanInstall)
        {
            pickupButton.gameObject.SetActive(false);
            return;
        }

        pickupButton.gameObject.SetActive(true);
        int itemCost = Mathf.Max(1, nearby.CapacityCost);
        bool full = inventory.UsedCapacity + itemCost > inventory.Capacity;
        bool canPickUp = inventory.CanInteract && !full;
        pickupButton.interactable = canPickUp;
        pickupBackground.color = full ? FullColor : canPickUp ? Accent : ControlDisabled;

        if (full)
        {
            pickupLabel.text = "BACKPACK FULL  •  " + inventory.UsedCapacity + " / " + inventory.Capacity;
        }
        else if (!inventory.CanInteract)
        {
            pickupLabel.text = "CAN'T PICK UP RIGHT NOW";
        }
        else
        {
            pickupLabel.text = "PICK UP  " + nearby.DisplayName + "  •  " + UnitText(itemCost);
        }
    }

    private static void ApplyItemPresentation(
        GeneratorPiece item,
        Button button,
        Image background,
        Image icon,
        TextMeshProUGUI name,
        TextMeshProUGUI cost,
        bool selected,
        string emptyLabel)
    {
        bool occupied = item != null;
        button.interactable = occupied;
        background.color = selected ? Accent : occupied ? Control : ControlDisabled;
        name.text = occupied ? item.DisplayName : emptyLabel;
        name.color = occupied ? TextColor : MutedText;
        cost.text = occupied ? Mathf.Max(1, item.CapacityCost) + "u" : string.Empty;
        icon.sprite = occupied ? item.Icon : null;
        icon.enabled = occupied && item.Icon != null;
    }

    private void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || toastObject == null)
            return;

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ShowToast(message));
    }

    private IEnumerator ShowToast(string message)
    {
        toastLabel.text = message;
        toastObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2.25f);
        toastObject.SetActive(false);
        toastRoutine = null;
    }

    private static string UnitText(int capacityCost)
    {
        int units = Mathf.Max(1, capacityCost);
        return units + (units == 1 ? " unit" : " units");
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color)
    {
        RectTransform root = CreateRect(name, parent);
        Image image = root.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = root.gameObject.AddComponent<Button>();
        ConfigureButton(button, image);
        TextMeshProUGUI text = CreateText(root, "Label", label, 16f, TextColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(6f, 3f);
        text.rectTransform.offsetMax = new Vector2(-6f, -3f);
        return button;
    }

    private static void ConfigureButton(Button button, Graphic target)
    {
        button.targetGraphic = target;
        button.navigation = NoNavigation();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.78f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static Navigation NoNavigation()
    {
        return new Navigation { mode = Navigation.Mode.None };
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 size,
        Vector2 position,
        Vector2? pivot = null)
    {
        Anchor(rect, anchor, anchor, pivot ?? new Vector2(0.5f, 0.5f));
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Anchor(RectTransform rect, Vector2 minimum, Vector2 maximum, Vector2 pivot)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.pivot = pivot;
    }

    private static void SetAnchors(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }
}

/// <summary>Forwards a touch-and-hold gesture to PlayerInventory's install lifecycle.</summary>
public sealed class InventoryInstallHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private PlayerInventory inventory;
    private bool pressed;

    public void Initialize(PlayerInventory playerInventory)
    {
        inventory = playerInventory;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventory == null)
            return;

        pressed = true;
        inventory.OnInstallButtonDown();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (!pressed)
            return;

        pressed = false;
        if (inventory != null)
            inventory.OnInstallButtonUp();
    }
}
