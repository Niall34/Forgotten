//This script is for demo purposes only and is not required for the animations to work.
//Human Basic Motions
//Kevin Iglesias
//www.keviniglesias.com

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KevinIglesias
{
    [System.Serializable]
    public class AnimationEntry
    {
        public string displayName;
        public string folderName;
        public string folderPath;
        public int folderLevel;
        public AnimationClip clip;
        public GameObject[] prop;
        public bool disableSpineProxy;
        [HideInInspector] public Button animationButton;
    }
    
    [System.Serializable]
    public class CharacterEntry
    {
        public GameObject characterObject;
        public Transform characterTransform;
        public Animator animator;
        public Transform leftFoot;
        public Transform rightFoot;
        [HideInInspector] public Transform spineProxyBone;
        [HideInInspector] public Button characterButton;
    }
    
    public class HumanBasicMotionsDemo : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float zoomStep = 5f;
        [SerializeField] private float minFOV = 35f;
        [SerializeField] private float maxFOV = 75f;
        [SerializeField] private float rotationSpeed = 120f;
        private float rotationInput = 0f;
        
        [Header("Base")]
        [SerializeField] private Transform baseT;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private float baseTreadmillMultiplier = 0.5f;
        private float baseOffsetX;
        private float baseOffsetY;
        private float baseUVRotation;
        
        [Header("Shadow")]
        [SerializeField] private Transform shadowT;
        [SerializeField] private SpriteRenderer shadowSprite;
        [SerializeField] private float shadowFloorY = 0.001f;
        [SerializeField] private float groundedScale = 0.6f;
        [SerializeField] private float groundedAlpha = 0.78f;
        [SerializeField] private float maxJumpHeight = 1f;
        [SerializeField] private float airScale = 1f;
        [SerializeField] private float airAlpha = 0.05f;
        private Transform currentLeftFoot;
        private Transform currentRightFoot; 
        [SerializeField] private float shadowFootPadding = 0.35f;
        [SerializeField] private float shadowMinScale = 0.75f;
        [SerializeField] private float shadowMaxScale = 2.25f;
                
        [Header("Background")]
        [SerializeField] private Material backgroundMaterial;
        [SerializeField] private Vector3 backgroundLightDir = new Vector3(0f, -0.1f, 1f);
        
        [Header("UI")]
        [SerializeField] private Transform zoomIn;
        [SerializeField] private Transform zoomOut;
        [SerializeField] private Transform leftArrow;
        [SerializeField] private Transform rightArrow;
        [SerializeField] private Color normalButtonColor = Color.white;
        [SerializeField] private Color selectedButtonColor = Color.black;
        [SerializeField] private Color normalTextColor = Color.black;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Vector3 normalScale = Vector3.one;
        [SerializeField] private Vector3 pressedScale = Vector3.one * 0.85f;
        
        [Header("Characters")]
        [SerializeField] private CharacterEntry[] availableCharacters;
        [SerializeField] private int currentCharacterIndex = 0;
        [SerializeField] private GameObject characterUIPrefab;
        [SerializeField] private Transform charactersUIRoot;
        [SerializeField] private Transform character;
        [SerializeField] private Animator animator;
        private Button currentSelectedCharacterButton;
        private bool characterInitialized = false;
        
        [Header("Animations")]
        [SerializeField] private AnimationEntry[] availableAnimations;
        [SerializeField] private Text currentAnimationText;
        [SerializeField] private Text currentAnimationFolderText;
        [SerializeField] private Transform animationsUIRoot;
        [SerializeField] private GameObject folderButtonPrefab;
        [SerializeField] private GameObject animationUIPrefab;
        [SerializeField] private string currentAnimationName;
        [SerializeField] private string currentUpperAnimationName;
        private int baseAnimationToPlay = 0;
        private int upperAnimationToPlay = -1;
        private AnimatorOverrideController overrideController;
        private string currentFolder = "";
        private Button currentSelectedAnimationButton;
        private GameObject[] currentBaseProps;
        private GameObject[] currentUpperProps;
        [SerializeField] private int animationType = 0;
        [SerializeField] private Button baseTypeButton;
        [SerializeField] private Button upperTypeButton;

    ///UNITY DEFAULT FUNCTIONS
        private void Start()
        {
            ResetBase();

            CreateAnimationButtons();
            CreateCharacterButtons();

            if(availableCharacters != null && availableCharacters.Length > 0)
            {
                overrideController = new AnimatorOverrideController(availableCharacters[0].animator.runtimeAnimatorController);
            }

            for(int i = 0; i < availableCharacters.Length; i++)
            {
                if(availableCharacters[i].characterObject != null)
                {
                    availableCharacters[i].characterObject.SetActive(false);
                    
                    Transform[] transforms = availableCharacters[i].characterObject.GetComponentsInChildren<Transform>(true);

                    foreach(Transform t in transforms)
                    {
                        if (t.name == "B-spineProxy")
                        {
                            availableCharacters[i].spineProxyBone = t;
                            break;
                        }
                    }
                }
            }

            SelectCharacter(currentCharacterIndex);
            SelectCharacterButton(availableCharacters[currentCharacterIndex].characterButton);
            
            int defaultIndex = FindAnimationIndex("HumanM@Idle01");

            if(defaultIndex >= 0)
            {
                PlayAnimation(defaultIndex, 0);

                if(defaultIndex < availableAnimations.Length)
                {
                    SelectAnimationButton(availableAnimations[defaultIndex].animationButton);
                }
            }
            
            SetButtonSelected(baseTypeButton, true);
        }
        private void Update()
        {
            baseT.position = new Vector3(character.position.x, 0, character.position.z);
            UpdateBlobShadowFromFeet();
            
            UpdateBaseMaterialOffset();

            Vector3 correctedLightDir = cameraPivot.rotation * backgroundLightDir;

            backgroundMaterial.SetVector("_LightDir", new Vector4(correctedLightDir.x, correctedLightDir.y, correctedLightDir.z, 0f));
            
            if(rotationInput != 0f)
            {
                cameraPivot.Rotate(new Vector3(0f, rotationInput * rotationSpeed * Time.deltaTime, 0f), Space.World);
            }
            
            character.position = new Vector3(0, character.position.y, 0);
        }
        private void FixedUpdate()
        {
            Vector3 correctedLightDir = cameraPivot.rotation * backgroundLightDir;
            backgroundMaterial.SetVector("_LightDir", new Vector4(correctedLightDir.x, correctedLightDir.y, correctedLightDir.z, 0f));
        }
        private void LateUpdate()
        {
            Vector3 correctedLightDir = cameraPivot.rotation * backgroundLightDir;
            backgroundMaterial.SetVector("_LightDir", new Vector4(correctedLightDir.x, correctedLightDir.y, correctedLightDir.z, 0f));
        }
        private void OnDisable()
        {
            ResetBase();
        }
    ///

    ///CHARACTER SELECTION
        public void SelectCharacter(int index)
        {
            if(availableCharacters == null || index < 0 || index >= availableCharacters.Length)
            {
                return;
            }

            if(characterInitialized && index == currentCharacterIndex)
            {
                return;
            }

            characterInitialized = true;
            currentCharacterIndex = index;

            for(int i = 0; i < availableCharacters.Length; i++)
            {
                CharacterEntry entry = availableCharacters[i];

                bool selected = i == index;

                if(entry.animator != null)
                {
                    entry.animator.runtimeAnimatorController = selected ? overrideController : null;
                }

                if(entry.characterObject != null)
                {
                    entry.characterObject.SetActive(selected);
                }
            }

            CharacterEntry selectedEntry = availableCharacters[index];

            character = selectedEntry.characterTransform;
            animator = selectedEntry.animator;
            
            currentLeftFoot = selectedEntry.leftFoot;
            currentRightFoot = selectedEntry.rightFoot;

            if(animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            ResetProps();

            ResetBase();
            
            string equivalentAnimationName = currentAnimationName;
            if(index == 1)
            {
                equivalentAnimationName = currentAnimationName.Replace("HumanM@", "HumanF@");
            }else{
                equivalentAnimationName = currentAnimationName.Replace("HumanF@", "HumanM@");
            }
            int equivalentAnimation = FindAnimationIndex(equivalentAnimationName);

            if(equivalentAnimation >= 0)
            {
                PlayAnimation(equivalentAnimation, 0);
            }else{
                PlayAnimation(baseAnimationToPlay, 0);
            }
            
            string equivalentUpperAnimationName = currentUpperAnimationName;
            if(index == 1)
            {
                equivalentUpperAnimationName = currentUpperAnimationName.Replace("HumanM@", "HumanF@");
            }else{
                equivalentUpperAnimationName = currentUpperAnimationName.Replace("HumanF@", "HumanM@");
            }
            int equivalentUpperAnimation = FindAnimationIndex(equivalentUpperAnimationName);

            if(equivalentUpperAnimation >= 0)
            {
                PlayAnimation(equivalentUpperAnimation, 1);
            }else{
                if(upperAnimationToPlay >= 0)
                {
                    PlayAnimation(upperAnimationToPlay, 1);
                }else{
                    ClearUpperAnimation();
                }
            }
            
            string newFolder = currentFolder;
            if(index == 1)
            {
                newFolder = currentFolder.Replace("Male", "Female");
            }else{
                newFolder = currentFolder.Replace("Female", "Male");
            }
            ShowFolder(newFolder);
            
            SelectCurrentAnimationButtonInOpenFolder();
        }
        private void CreateCharacterButtons()
        {
            if(characterUIPrefab == null || charactersUIRoot == null || availableCharacters == null)
            {
                return;
            }

            for(int i = 0; i < availableCharacters.Length; i++)
            {
                int index = i;

                GameObject buttonGO = Instantiate(characterUIPrefab, charactersUIRoot);

                Button button = buttonGO.GetComponent<Button>();
                availableCharacters[i].characterButton = button;
                Image image = buttonGO.GetComponent<Image>();
                Text text = buttonGO.GetComponentInChildren<Text>();

                if(image != null)
                {
                    image.color = normalButtonColor;
                }

                if(text != null)
                {
                    text.color = normalTextColor;
                    text.text = availableCharacters[i].characterObject.name;
                }

                if(button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        SelectCharacter(index);
                        SelectCharacterButton(button);
                    });
                }

                buttonGO.SetActive(true);
            }
        }
        private void SelectCharacterButton(Button selectedButton)
        {
            if(currentSelectedCharacterButton != null)
            {
                SetButtonSelected(currentSelectedCharacterButton, false);
            }

            currentSelectedCharacterButton = selectedButton;

            if(currentSelectedCharacterButton != null)
            {
                SetButtonSelected(currentSelectedCharacterButton, true);
            }
        }
    ///

    ///ANIMATION SELECTION
        public void PlayAnimation(int index, int newAnimationType)
        {
            if(animator == null || overrideController == null)
                return;

            if(!animator.gameObject.activeInHierarchy)
                return;

            if(availableAnimations == null || index < 0 || index >= availableAnimations.Length)
                return;

            ResetBase();

            if(newAnimationType == 0)
            {
                baseAnimationToPlay = index;

                overrideController["HumanM@Idle01"] = availableAnimations[index].clip;

                animator.Play("BaseAnimation", 0, 0f);
                animator.Update(0f);
                
                currentAnimationName = availableAnimations[index].clip.name;
                currentAnimationText.text = currentAnimationName;
            }else{
                upperAnimationToPlay = index;
                
                overrideController["HumanM@Idle02"] = availableAnimations[index].clip;

                animator.Play("UpperAnimation", 1, 0f);
                animator.Update(0f);
                
                currentUpperAnimationName = availableAnimations[index].clip.name;
                currentAnimationText.text = currentUpperAnimationName+"\n"+currentAnimationName;
            }

            if(currentUpperAnimationName != "")
            {
                currentAnimationText.text = currentUpperAnimationName+"\n"+currentAnimationName;
            }

            character.position = Vector3.zero;
            
            if(availableCharacters[currentCharacterIndex].spineProxyBone)
            {
                if(availableAnimations[index].disableSpineProxy)
                {
                    availableCharacters[currentCharacterIndex].spineProxyBone.gameObject.SetActive(false);
                }else{
                    availableCharacters[currentCharacterIndex].spineProxyBone.gameObject.SetActive(true);
                }
            }
            
            UpdateProps(index, newAnimationType);
        }
        private void ResetProps()
        {
            DisableProps(currentBaseProps, currentUpperProps);
            DisableProps(currentUpperProps, currentBaseProps);

            currentBaseProps = null;
            currentUpperProps = null;
        }

        private void UpdateProps(int activeIndex, int animationLayerType)
        {
            if(availableAnimations == null)
                return;

            AnimationEntry entry = availableAnimations[activeIndex];
            GameObject[] newProps = entry != null ? entry.prop : null;

            if(animationLayerType == 0)
            {
                if(currentBaseProps == newProps)
                    return;

                DisableProps(currentBaseProps, currentUpperProps);
                currentBaseProps = newProps;
                EnableProps(currentBaseProps);
            }
            else
            {
                if(currentUpperProps == newProps)
                    return;

                DisableProps(currentUpperProps, currentBaseProps);
                currentUpperProps = newProps;
                EnableProps(currentUpperProps);
            }
        }

        private void EnableProps(GameObject[] props)
        {
            if(props == null)
                return;

            foreach(GameObject prop in props)
            {
                if(prop != null)
                    prop.SetActive(true);
            }
        }

        private void DisableProps(GameObject[] propsToDisable, GameObject[] propsStillUsed)
        {
            if(propsToDisable == null)
                return;

            foreach(GameObject prop in propsToDisable)
            {
                if(prop == null)
                    continue;

                if(IsPropUsedBy(prop, propsStillUsed))
                    continue;

                prop.SetActive(false);
            }
        }

        private bool IsPropUsedBy(GameObject prop, GameObject[] props)
        {
            if(props == null)
                return false;

            foreach(GameObject p in props)
            {
                if(p == prop)
                    return true;
            }

            return false;
        }
        private void CreateAnimationButtons()
        {
            ClearAnimationUI();
            ShowFolder("");
        }
        private void ClearAnimationUI()
        {
            if(availableAnimations != null)
            {
                for(int i = 0; i < availableAnimations.Length; i++)
                {
                    availableAnimations[i].animationButton = null;
                }
            }

            for(int i = animationsUIRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(animationsUIRoot.GetChild(i).gameObject);
            }
            
            clearUpperAnimationButton = null;
            currentSelectedAnimationButton = null;
        }
        private void ShowFolder(string folderPath)
        {
            currentFolder = folderPath;
            ClearAnimationUI();

            if(!string.IsNullOrEmpty(currentFolder))
            {
                CreateBackButton();
            }

            CreateSubFolderButtons(currentFolder);
            CreateAnimationButtonsInFolder(currentFolder);
            SelectCurrentAnimationButtonInOpenFolder();
            
            currentAnimationFolderText.text = "/"+currentFolder;
        }
        private void CreateBackButton()
        {
            GameObject backGO = Instantiate(folderButtonPrefab, animationsUIRoot);

            Text text = backGO.GetComponentInChildren<Text>();
            if(text != null)
            {
                text.text = "← Back";
            }

            Button button = backGO.GetComponent<Button>();
            if(button != null)
            {
                button.onClick.AddListener(() => ShowFolder(GetParentFolder(currentFolder)));
            }

            backGO.SetActive(true);
        }
        private void CreateSubFolderButtons(string folderPath)
        {
            HashSet<string> folders = new HashSet<string>();

            foreach (AnimationEntry entry in availableAnimations)
            {
                if(entry == null || entry.clip == null)
                {
                    continue;
                }

                if(!IsDirectChildFolder(entry, folderPath, out string childPath, out string childName))
                {
                    continue;
                }

                folders.Add(childPath);
            }

            foreach(string folder in folders)
            {
                GameObject folderGO = Instantiate(folderButtonPrefab, animationsUIRoot);

                Text text = folderGO.GetComponentInChildren<Text>();
                if(text != null)
                {
                    text.text = GetLastFolderName(folder);
                }

                Button button = folderGO.GetComponent<Button>();
                if(button != null)
                {
                    button.onClick.AddListener(() => ShowFolder(folder));
                }

                folderGO.SetActive(true);
            }
        }
        private void CreateAnimationButtonsInFolder(string folderPath)
        {
            folderPath = NormalizeFolder(folderPath);

            for(int i = 0; i < availableAnimations.Length; i++)
            {
                AnimationEntry entry = availableAnimations[i];

                if(entry == null || entry.clip == null)
                {
                    continue;
                }

                string entryPath = NormalizeFolder(entry.folderPath);

                if(entryPath != folderPath)
                {
                    continue;
                }

                int index = i;

                GameObject buttonGO = Instantiate(animationUIPrefab, animationsUIRoot);

                Button button = buttonGO.GetComponent<Button>();
                availableAnimations[i].animationButton = button;
                Image image = buttonGO.GetComponent<Image>();
                Text text = buttonGO.GetComponentInChildren<Text>();

                if(image != null)
                {
                    image.color = normalButtonColor;
                }

                if(text != null)
                {
                    text.color = normalTextColor;
                    text.text = entry.displayName;
                }

                if(button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        PlayAnimation(index, animationType);
                        SelectAnimationButton(button);
                    });
                }

                buttonGO.SetActive(true);
            }
            
            if(animationType == 1)
            {
                GenerateClearUpperAnimationButton();
            }
        }
        private void SelectCurrentAnimationButtonInOpenFolder()
        {
            if(availableAnimations == null)
                return;

            int selectedIndex = animationType == 0 ? baseAnimationToPlay : upperAnimationToPlay;

            if(selectedIndex < 0 || selectedIndex >= availableAnimations.Length)
                return;

            AnimationEntry currentEntry = availableAnimations[selectedIndex];

            if(currentEntry == null)
                return;

            string entryFolder = NormalizeFolder(currentEntry.folderPath);
            string openFolder = NormalizeFolder(currentFolder);

            if(entryFolder != openFolder)
                return;

            SelectAnimationButton(currentEntry.animationButton);
        }
        private void SelectAnimationButton(Button selectedButton)
        {
            if(currentSelectedAnimationButton != null)
            {
                SetButtonSelected(currentSelectedAnimationButton, false);
            }

            currentSelectedAnimationButton = selectedButton;

            if(currentSelectedAnimationButton != null)
            {
                SetButtonSelected(currentSelectedAnimationButton, true);
            }

            if(animationType == 1)
            {
                GenerateClearUpperAnimationButton();
            }
        }
        private int FindAnimationIndex(string clipName)
        {
            if(availableAnimations == null)
            {
                return -1;
            }

            for(int i = 0; i < availableAnimations.Length; i++)
            {
                if(availableAnimations[i] != null && availableAnimations[i].clip != null && availableAnimations[i].clip.name == clipName)
                {
                    return i;
                }
            }

            return -1;
        }
        private GameObject clearUpperAnimationButton = null;
        public void SelectAnimationType(int newType)
        {
            animationType = newType;

            if(newType == 0)
            {
                SetButtonSelected(baseTypeButton, true);
                SetButtonSelected(upperTypeButton, false);

                if(clearUpperAnimationButton != null)
                {
                    Destroy(clearUpperAnimationButton);
                    clearUpperAnimationButton = null;
                }
            }
            else
            {
                SetButtonSelected(baseTypeButton, false);
                SetButtonSelected(upperTypeButton, true);

                GenerateClearUpperAnimationButton();
            }

            SelectCurrentAnimationButtonInOpenFolder();
        }
        private void GenerateClearUpperAnimationButton()
        {
            if(currentUpperAnimationName == "")
            {
                return;
            }
            if(clearUpperAnimationButton == null)
            {
                clearUpperAnimationButton = Instantiate(animationUIPrefab, animationsUIRoot);

                Button button = clearUpperAnimationButton.GetComponent<Button>();
                Image image = clearUpperAnimationButton.GetComponent<Image>();
                Text text = clearUpperAnimationButton.GetComponentInChildren<Text>();

                if(image != null)
                {
                    image.color = normalButtonColor;
                }

                if(text != null)
                {
                    text.color = normalTextColor;
                    text.text = "[X] Clear upper animation";
                }

                if(button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        ClearUpperAnimation();
                        SelectAnimationButton(button);
                        SetButtonSelected(button, false);
                    });
                }

                clearUpperAnimationButton.SetActive(true);
            }
        }
        public void ClearUpperAnimation()
        {
            DisableProps(currentUpperProps, currentBaseProps);
            currentUpperAnimationName = "";
            currentAnimationText.text = currentAnimationName;
            animator.SetTrigger("ClearUpper");
            currentUpperProps = null;
            upperAnimationToPlay = -1;
        }
    ///
        
    ///ANIMATION FOLDER HELPERS
        private bool IsDirectChildFolder(AnimationEntry entry, string currentFolder, out string childPath, out string childName)
        {
            childPath = null;
            childName = null;

            string entryPath = NormalizeFolder(entry.folderPath);
            currentFolder = NormalizeFolder(currentFolder);

            if(string.IsNullOrEmpty(entryPath))
            {
                return false;
            }

            string[] entryParts = entryPath.Split('/');

            int currentLevel = string.IsNullOrEmpty(currentFolder)
                ? 0
                : currentFolder.Split('/').Length;

            if(!string.IsNullOrEmpty(currentFolder))
            {
                if(!entryPath.StartsWith(currentFolder + "/"))
                {
                    return false;
                }
            }

            if(entryParts.Length <= currentLevel)
            {
                return false;
            }

            childName = entryParts[currentLevel];

            childPath = string.IsNullOrEmpty(currentFolder) ? childName : currentFolder + "/" + childName;

            return true;
        }
        private string NormalizeFolder(string folder)
        {
            if(string.IsNullOrEmpty(folder))
            {
                return "";
            }

            return folder.Replace("\\", "/").Trim('/');
        }
        private string GetParentFolder(string folder)
        {
            if(string.IsNullOrEmpty(folder))
            {
                return "";
            }

            int index = folder.LastIndexOf('/');
            if(index < 0)
            {
                return "";
            }

            return folder.Substring(0, index);
        }
        private string GetLastFolderName(string folder)
        {
            if(string.IsNullOrEmpty(folder))
            {
                return "";
            }

            int index = folder.LastIndexOf('/');
            if(index < 0)
            {
                return folder;
            }

            return folder.Substring(index + 1);
        }
    ///
    
    ///CAMERA CONTROLS
        public void StartRotateLeft()
        {
            rotationInput = -1f;
            if(leftArrow != null)
            {
                leftArrow.localScale = pressedScale;
            }
        }
        public void StartRotateRight()
        {
            rotationInput = 1f;
            if(rightArrow != null)
            {
                rightArrow.localScale = pressedScale;
            }
        }
        public void StopRotate()
        {
            rotationInput = 0f;

            if(leftArrow != null)
            {
                leftArrow.localScale = normalScale;
            }

            if(rightArrow != null)
            {
                rightArrow.localScale = normalScale;
            }
        }
        public void ZoomIn()
        {
            if(targetCamera == null)
            {
                return;
            }

            targetCamera.fieldOfView = Mathf.Clamp(targetCamera.fieldOfView - zoomStep, minFOV, maxFOV);
            
            zoomIn.localScale = pressedScale;
        }
        public void ZoomOut()
        {
            if(targetCamera == null)
            {
                return;
            }

            targetCamera.fieldOfView = Mathf.Clamp(targetCamera.fieldOfView + zoomStep, minFOV, maxFOV);
            
            zoomOut.localScale = pressedScale;
        }
        public void ZoomUp()
        {
            zoomIn.localScale = normalScale;
            zoomOut.localScale = normalScale;
        }

        
    ///SCENE
        private void ResetBase()
        {
            baseUVRotation = 0f;
            baseOffsetX = 0f;
            baseOffsetY = 0f;
            baseMaterial.mainTextureOffset = Vector2.zero;
            baseMaterial.SetFloat("_UVRotation", 0);
            backgroundMaterial.SetVector("_LightDir", backgroundLightDir);
            character.localEulerAngles = Vector3.zero;
        }
        private void UpdateBaseMaterialOffset()
        {
            if(animator == null || baseMaterial == null)
            {
                return;
            }

            Vector3 localDelta = character.InverseTransformDirection(animator.deltaPosition);

            baseOffsetX -= localDelta.x * baseTreadmillMultiplier;
            baseOffsetY += localDelta.z * baseTreadmillMultiplier;

            float deltaYaw = Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
            baseUVRotation += deltaYaw;

            if(baseUVRotation > 180f)
            {
                baseUVRotation -= 360f;
            }
            if(baseUVRotation < -180f)
            {
                baseUVRotation += 360f;
            }

            Vector2 offset = baseMaterial.mainTextureOffset;
            offset.x = baseOffsetX;
            offset.y = baseOffsetY;
            baseMaterial.mainTextureOffset = offset;

            baseMaterial.SetFloat("_UVRotation", baseUVRotation);
        }
        private void UpdateBlobShadowFromFeet()
        {
            if(character == null || shadowT == null || shadowSprite == null)
            {
                return;
            }

            Vector3 center = character.position;
            Vector3 left = currentLeftFoot != null ? currentLeftFoot.position : center;
            Vector3 right = currentRightFoot != null ? currentRightFoot.position : center;

            shadowT.position = new Vector3(center.x, shadowFloorY, center.z);

            center.y = shadowFloorY;
            left.y = shadowFloorY;
            right.y = shadowFloorY;

            float leftDistance = Vector3.Distance(center, left);
            float rightDistance = Vector3.Distance(center, right);

            float footScale = Mathf.Max(leftDistance, rightDistance);
            footScale = Mathf.Clamp(footScale + shadowFootPadding, shadowMinScale, shadowMaxScale);

            float distanceY = Mathf.Abs(character.position.y);
            float t = Mathf.Clamp01(distanceY / maxJumpHeight);

            float heightScale = Mathf.Lerp(groundedScale, airScale, t);
            float alpha = Mathf.Lerp(groundedAlpha, airAlpha, t);

            shadowT.localScale = Vector3.one * footScale * heightScale;

            Color c = shadowSprite.color;
            c.a = alpha;
            shadowSprite.color = c;
        }
    ///
        
    ///UI HELPERS
        private void SetButtonSelected(Button button, bool selected)
        {
            Image image = button.GetComponent<Image>();
            Text text = button.GetComponentInChildren<Text>();
            
            if(image != null)
            {
                image.color = selected ? selectedButtonColor : normalButtonColor;
            }

            if(text != null)
            {
                text.color = selected ? selectedTextColor : normalTextColor;
            }
        }
    ///

    ///ANIMATIONS LOAD (EDITOR ONLY)
    #if UNITY_EDITOR
        [SerializeField] private string animationsFolder = "Assets/Kevin Iglesias/Human Animations/Animations";
        private string[] skipSpineProxyAnimations =
        {
            "HumanF@Turn01_Left [RM]",
            "HumanF@Turn01_Right [RM]",
            "HumanM@Turn01_Left [RM]",
            "HumanM@Turn01_Right [RM]",
            "HumanF@CrouchTurn01_R [RM]",
            "HumanF@CrouchTurn01_L [RM]",
            "HumanM@CrouchTurn01_R [RM]",
            "HumanM@CrouchTurn01_L [RM]"
        };

        [SerializeField] private GameObject propLowChair;
        private string[] propLowChairAnimations =
        {
            "HumanF@SitLow01 - Begin",
            "HumanF@SitLow01 - Loop",
            "HumanF@SitLow01 - Stop",
            "HumanM@SitLow01 - Begin",
            "HumanM@SitLow01 - Loop",
            "HumanM@SitLow01 - Stop"
        };

        [SerializeField] private GameObject propMediumChair;
        private string[] propMediumChairAnimations =
        {
            "HumanF@SitMedium01 - Begin",
            "HumanF@SitMedium01 - Loop",
            "HumanF@SitMedium01 - Stop",
            "HumanM@SitMedium01 - Begin",
            "HumanM@SitMedium01 - Loop",
            "HumanM@SitMedium01 - Stop"
        };

        [SerializeField] private GameObject propHighChair;
        private string[] propHighChairAnimations =
        {
            "HumanF@SitHigh01 - Begin",
            "HumanF@SitHigh01 - Loop",
            "HumanF@SitHigh01 - Stop",
            "HumanM@SitHigh01 - Begin",
            "HumanM@SitHigh01 - Loop",
            "HumanM@SitHigh01 - Stop"
        };

        [SerializeField] private GameObject[] propDrinkablesL;
        private string[] propDrinkablesAnimationsL =
        {
            "HumanF@Drink01_L",
            "HumanF@Drink01_L - Loop",
            "HumanM@Drink01_L",
            "HumanM@Drink01_L - Loop",
        };
        
        [SerializeField] private GameObject[] propDrinkablesR;
        private string[] propDrinkablesAnimationsR =
        {
            "HumanF@Drink01_R",
            "HumanF@Drink01_R - Loop",
            "HumanM@Drink01_R",
            "HumanM@Drink01_R - Loop"
        };

        [SerializeField] private GameObject[] propEdiblesL;
        private string[] propEdiblesAnimationsL =
        {
            "HumanF@Eat01_L",
            "HumanF@Eat01_L - Loop",
            "HumanM@Eat01_L",
            "HumanM@Eat01_L - Loop",
        };
        [SerializeField] private GameObject[] propEdiblesR;
        private string[] propEdiblesAnimationsR =
        {
            "HumanF@Eat01_R",
            "HumanF@Eat01_R - Loop",
            "HumanM@Eat01_R",
            "HumanM@Eat01_R - Loop"
        };
        [ContextMenu("Populate Animations From Folder")]
        void PopulateAnimationsFromFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { animationsFolder });

            System.Collections.Generic.List<AnimationEntry> entries = new();

            foreach(string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                if(clip == null || clip.name.StartsWith("__preview__"))
                {
                    continue;
                }

                string folderPath = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                string root = animationsFolder.Replace("\\", "/");

                string relativeFolder = folderPath.StartsWith(root) ? folderPath.Substring(root.Length).Trim('/') : folderPath;

                string folderName = string.IsNullOrEmpty(relativeFolder) ? "Root" : System.IO.Path.GetFileName(relativeFolder);

                int folderLevel = string.IsNullOrEmpty(relativeFolder) ? 0 : relativeFolder.Split('/').Length;

                entries.Add(new AnimationEntry
                {
                    clip = clip,
                    displayName = clip.name,
                    folderName = folderName,
                    folderPath = relativeFolder,
                    folderLevel = folderLevel,

                    prop = GetPropsForAnimation(clip.name),
                    disableSpineProxy = ShouldDisableSpineProxy(clip.name)
                });
            }

            entries.Sort((a, b) =>
            {
                int folderCompare = string.Compare(a.folderPath, b.folderPath, System.StringComparison.Ordinal);
                if (folderCompare != 0) return folderCompare;

                return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
            });

            availableAnimations = entries.ToArray();

            EditorUtility.SetDirty(this);
        }
        private bool ShouldDisableSpineProxy(string animationName)
        {
            return System.Array.Exists(
                skipSpineProxyAnimations,
                x => x == animationName
            );
        }
        private GameObject[] GetPropsForAnimation(string animationName)
        {
            if (System.Array.Exists(propLowChairAnimations, x => x == animationName))
                return propLowChair != null ? new[] { propLowChair } : System.Array.Empty<GameObject>();

            if (System.Array.Exists(propMediumChairAnimations, x => x == animationName))
                return propMediumChair != null ? new[] { propMediumChair } : System.Array.Empty<GameObject>();

            if (System.Array.Exists(propHighChairAnimations, x => x == animationName))
                return propHighChair != null ? new[] { propHighChair } : System.Array.Empty<GameObject>();

            if (System.Array.Exists(propDrinkablesAnimationsL, x => x == animationName))
                return propDrinkablesL ?? System.Array.Empty<GameObject>();

            if (System.Array.Exists(propDrinkablesAnimationsR, x => x == animationName))
                return propDrinkablesR ?? System.Array.Empty<GameObject>();

            if (System.Array.Exists(propEdiblesAnimationsL, x => x == animationName))
                return propEdiblesL ?? System.Array.Empty<GameObject>();

            if (System.Array.Exists(propEdiblesAnimationsR, x => x == animationName))
                return propEdiblesR ?? System.Array.Empty<GameObject>();

            return System.Array.Empty<GameObject>();
        }
        #endif
    ///
    }
}
