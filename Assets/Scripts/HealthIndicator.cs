using Photon.Pun;                  
using UnityEngine;                 
using UnityEngine.UI;             

public class HealthIndicator : MonoBehaviourPun  
{
    public Sprite fullHeartSprite;    
    public Sprite emptyHeartSprite;   
    public int maxHearts = 3;         
    public int maxHealth = 100;       

    private PlayerDownedState downedState;   
    private Image[] redHearts;                                   

    private void Start() {                                         
        if (photonView.IsMine == false) return;                  
        downedState = GetComponent<PlayerDownedState>();  
        BuildHearts();                                            // draw the hearts
    }

    private void Update()                                         // runs every frame
    {
        if (redHearts == null || downedState == null)
        {
            return;
        }

        for (int i = 0; i < redHearts.Length; i++)
        {
            // how many hits have landed on this particular heart
            int hitsOnThisHeart = Mathf.Clamp(downedState.HitsTaken - i * downedState.hitsPerHeart, 0, downedState.hitsPerHeart);

            // 0 hits = whole heart, 1 hit = half, 2 hits = empty
            redHearts[i].fillAmount = 1f - (float)hitsOnThisHeart / downedState.hitsPerHeart;
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
            float x = (i - (maxHearts - 1) / 2f) * 60f;                              
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
        rect.sizeDelta = new Vector2(45f, 45f);                         
        rect.anchoredPosition = new Vector2(x, -40f);                
        Image image = obj.AddComponent<Image>();                       
        image.sprite = sprite;                                    
        return image;                                                   
    }
}