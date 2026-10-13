using Photon.Pun;                  
using UnityEngine;                 
using UnityEngine.UI;             

public class HealthIndicator : MonoBehaviourPun  
{
    public Sprite fullHeartSprite;    
    public Sprite emptyHeartSprite;   
    public int maxHearts = 3;         
    public int maxHealth = 100;       

    private Forgotten.Player.PlayerHealthStateMachine health;   
    private Image[] redHearts;                                   

    private void Start() {                                         
        if (photonView.IsMine == false) return;                  
        health = GetComponent<Forgotten.Player.PlayerHealthStateMachine>();   
        BuildHearts();                                            // draw the hearts
    }

    private void Update()                                         // runs every frame
    {
        if (redHearts == null || health == null) return;          

        int halvesTotal = maxHearts * 2;                                              // each heart has 2 halves
        float healthFraction = (float)health.CurrentHealth / maxHealth;               // 1 = full health, 0 = dead
        int halvesLeft = Mathf.CeilToInt(healthFraction * halvesTotal);               // how many half hearts remain
        int halvesLost = halvesTotal - halvesLeft;                                    // how many half hearts are gone

        for (int i = 0; i < redHearts.Length; i++)                                    // go through each heart
        {
            int lostOnThisHeart = Mathf.Clamp(halvesLost - i * 2, 0, 2);              // 0, 1 or 2 halves lost on this heart
            redHearts[i].fillAmount = 1f - lostOnThisHeart / 2f;                      // 1 = full, 0.5 = half, 0 = empty
        }
    }

    private void BuildHearts()
    {
        GameObject canvasObject = new GameObject("Health Canvas");                    
        Canvas canvas = canvasObject.AddComponent<Canvas>();                          
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;                           
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();           
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        redHearts = new Image[maxHearts];                                            

        for (int i = 0; i < maxHearts; i++)                                           
        {
            float x = (i - (maxHearts - 1) / 2f) * 90f;                              
            MakeImage(canvasObject.transform, emptyHeartSprite, x);                   
            redHearts[i] = MakeImage(canvasObject.transform, fullHeartSprite, x);    
            redHearts[i].type = Image.Type.Filled;                                    
            redHearts[i].fillMethod = Image.FillMethod.Horizontal;                   
            redHearts[i].fillOrigin = (int)Image.OriginHorizontal.Left;   
        }
    }

    private Image MakeImage(Transform parent, Sprite sprite, float x)
    {
        GameObject obj = new GameObject("Heart");                      
        obj.transform.SetParent(parent, false);                         
        RectTransform rect = obj.AddComponent<RectTransform>();      
        rect.anchorMin = new Vector2(0.5f, 1f);                        
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(70f, 70f);                         
        rect.anchoredPosition = new Vector2(x, -60f);                
        Image image = obj.AddComponent<Image>();                       
        image.sprite = sprite;                                    
        return image;                                                   
    }
}