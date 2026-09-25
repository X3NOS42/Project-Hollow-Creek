using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

namespace HollowCreek.UI
{
    public class TextInspectUIManager : MonoBehaviour
    {
        [Header("Object Name UI")]
        [SerializeField] private TMP_Text objectNameText;
        [SerializeField] private GameObject objectNameBG;

        [Header("Object Name Customisation")]
        [SerializeField] private int nameTextSize = 20;
        [SerializeField] private TMP_FontAsset nameFontType;
        [SerializeField] private FontStyles nameFontStyle;
        [SerializeField] private Color nameFontColor;

        [Header("Object Details Settings")]
        [SerializeField] private TMP_Text objectDetailsText;
        [SerializeField] private GameObject objectDetailsBG;

        [Header("Object Details Customisation")]
        [SerializeField] private int detailsTextSize = 30;
        [SerializeField] private TMP_FontAsset detailsFontType;
        [SerializeField] private FontStyles detailsFontStyle;
        [SerializeField] private Color detailsFontColor;

        [Header("Note Sheet Settings")]
        [SerializeField] private TMP_Text noteSheetTitleText;
        [SerializeField] private TMP_Text noteSheetBodyText;
        [SerializeField] private GameObject noteSheet;
        [SerializeField] private ScrollRect noteSheetScrollRect;

        [Header("Timer")]
        [SerializeField] private float onScreenTimer = 5f;
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Crosshair")]
        [SerializeField] private Image crosshair;

        [Header("Should persist?")]
        [SerializeField] private bool persistAcrossScenes = true;

        private CanvasGroup objectDetailsCanvasGroup;
        private CanvasGroup noteSheetCanvasGroup;
        private bool startTimer;
        private float timer;
        private float shownAtTime;
        private bool isClosing;
        private Coroutine fadeRoutine;

        public static TextInspectUIManager instance;

        public bool IsObjectDetailsVisible =>
            (objectDetailsCanvasGroup != null && objectDetailsCanvasGroup.alpha > 0.01f) ||
            (noteSheetCanvasGroup != null && noteSheetCanvasGroup.alpha > 0.01f);

        public bool IsNoteSheetVisible =>
            (noteSheetCanvasGroup != null && noteSheetCanvasGroup.alpha > 0.01f);

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null, false);
                DontDestroyOnLoad(gameObject);
            }

            objectDetailsCanvasGroup = objectDetailsBG.GetComponent<CanvasGroup>();
            if (noteSheet != null)
            {
                noteSheetCanvasGroup = noteSheet.GetComponent<CanvasGroup>();
            }

            objectNameBG.SetActive(false);
            SetHidden(objectDetailsCanvasGroup);
            if (noteSheetCanvasGroup != null)
            {
                SetHidden(noteSheetCanvasGroup);
            }

            SetTextSettings();
            Debug.Log($"[Insight UI] onScreenTimer={onScreenTimer}, fadeInDuration={fadeInDuration}, fadeOutDuration={fadeOutDuration}");
        }

        void SetTextSettings()
        {
            objectNameText.fontSize = nameTextSize;
            objectNameText.font = nameFontType;
            objectNameText.fontStyle = nameFontStyle;
            objectNameText.color = nameFontColor;

            objectDetailsText.fontSize = detailsTextSize;
            objectDetailsText.font = detailsFontType;
            objectDetailsText.fontStyle = detailsFontStyle;
            objectDetailsText.color = detailsFontColor;
        }

        private void Update()
        {
            if (startTimer)
            {
                float previousTimer = timer;
                timer -= Time.unscaledDeltaTime;

                if (Mathf.CeilToInt(timer) != Mathf.CeilToInt(previousTimer))
                {
                    Debug.Log($"[Insight UI] Note closes in {Mathf.CeilToInt(timer)}s.");
                }

                if (timer <= 0)
                {
                    timer = 0;
                    Debug.Log($"[Insight UI] Note closed (timer expired). Visible for {Time.unscaledTime - shownAtTime:F2}s.");
                    isClosing = true;
                    ClearNoteUI();
                    startTimer = false;
                }
            }

            if (!isClosing && IsObjectDetailsVisible && Time.unscaledTime > shownAtTime && WasClosePressed())
            {
                CloseNote();
            }

            if (noteSheetCanvasGroup != null && noteSheetCanvasGroup.alpha > 0.01f && noteSheetScrollRect != null)
            {
                if (Mouse.current != null)
                {
                    Vector2 scroll = Mouse.current.scroll.ReadValue();
                    if (scroll.y != 0f)
                    {
                        float target = noteSheetScrollRect.verticalNormalizedPosition + scroll.y * 0.0025f;
                        noteSheetScrollRect.verticalNormalizedPosition = Mathf.Clamp01(target);
                    }
                }
            }
        }

        private void StartFade(CanvasGroup cg, bool fadeIn, float duration)
        {
            if (cg == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            if (!fadeIn)
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            fadeRoutine = StartCoroutine(FadeUI(cg, fadeIn, duration));
        }

        public IEnumerator FadeUI(CanvasGroup cg, bool fadeIn, float duration)
        {
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = 1f - startAlpha;
            float elapsedTime = 0f;

            cg.alpha = startAlpha;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                yield return null;
            }
            cg.alpha = endAlpha;
            fadeRoutine = null;
        }

        private static void SetHidden(CanvasGroup cg)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        private void HideTooltipUI()
        {
            if (objectDetailsCanvasGroup != null)
            {
                SetHidden(objectDetailsCanvasGroup);
            }
        }

        private void HideNoteSheetUI()
        {
            if (noteSheetCanvasGroup != null)
            {
                SetHidden(noteSheetCanvasGroup);
            }
        }

        public void ShowName(string objectName, bool show)
        {
            if (show)
            {
                objectNameBG.SetActive(true);
                objectNameText.text = objectName;
            }
            else
            {
                objectNameBG.SetActive(false);
                objectNameText.text = "";
            }
        }

        public void ShowObjectDetails(string newInfo, float displaySeconds = -1f)
        {
            float duration = displaySeconds > 0f ? displaySeconds : onScreenTimer;
            shownAtTime = Time.unscaledTime;
            isClosing = false;
            Debug.Log($"[Insight UI] Showing details at {shownAtTime:F2}s, timer={duration}s.");

            HideNoteSheetUI();

            objectDetailsText.text = newInfo;
            if (objectDetailsCanvasGroup != null)
            {
                objectDetailsCanvasGroup.interactable = true;
                objectDetailsCanvasGroup.blocksRaycasts = true;
                StartFade(objectDetailsCanvasGroup, true, fadeInDuration);
            }

            timer = duration;
            startTimer = true;
            Debug.Log($"[Insight UI] ShowObjectDetails: onScreenTimer={timer}s");
        }

        public void ShowNoteSheet(string noteTitle, string noteBody, float displaySeconds = -1f)
        {
            shownAtTime = Time.unscaledTime;
            isClosing = false;

            HideTooltipUI();
            objectNameBG.SetActive(false);

            if (noteSheetTitleText != null)
            {
                noteSheetTitleText.text = noteTitle;
            }
            if (noteSheetBodyText != null)
            {
                noteSheetBodyText.text = noteBody;
            }
            if (noteSheetScrollRect != null)
            {
                noteSheetScrollRect.verticalNormalizedPosition = 1f;
            }
            if (noteSheetCanvasGroup != null)
            {
                noteSheetCanvasGroup.interactable = true;
                noteSheetCanvasGroup.blocksRaycasts = true;
                StartFade(noteSheetCanvasGroup, true, fadeInDuration);
            }

            timer = displaySeconds > 0f ? displaySeconds : onScreenTimer;
            startTimer = true;
            Debug.Log($"[Insight UI] ShowNoteSheet: title='{noteTitle}', timer={timer}s");
        }

        public void ShowObjectDetailsKeepOpen(string newInfo)
        {
            HideNoteSheetUI();
            objectDetailsText.text = newInfo;
            startTimer = false;
            timer = 0f;
            isClosing = false;
            if (objectDetailsCanvasGroup != null)
            {
                objectDetailsCanvasGroup.interactable = true;
                objectDetailsCanvasGroup.blocksRaycasts = true;
                StartFade(objectDetailsCanvasGroup, true, fadeInDuration);
            }
        }

        public void HideObjectDetails()
        {
            startTimer = false;
            timer = 0f;
            if (objectDetailsCanvasGroup != null && objectDetailsCanvasGroup.alpha > 0.01f)
            {
                Debug.Log($"[Insight UI] HideObjectDetails called. Visible for {Time.unscaledTime - shownAtTime:F2}s.");
                StartFade(objectDetailsCanvasGroup, false, fadeOutDuration);
            }
        }

        void ClearNoteUI()
        {
            objectNameBG.SetActive(false);

            if (objectDetailsCanvasGroup != null && objectDetailsCanvasGroup.alpha > 0.01f)
            {
                StartFade(objectDetailsCanvasGroup, false, fadeOutDuration);
            }
            if (noteSheetCanvasGroup != null && noteSheetCanvasGroup.alpha > 0.01f)
            {
                StartFade(noteSheetCanvasGroup, false, fadeOutDuration);
            }
        }

        private static bool WasClosePressed()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                return true;
            }
            return false;
        }

        public void CloseNote()
        {
            if (!IsObjectDetailsVisible || isClosing)
            {
                return;
            }

            isClosing = true;
            Debug.Log($"[Insight UI] Note closed manually (right-click). Visible for {Time.unscaledTime - shownAtTime:F2}s.");
            startTimer = false;
            timer = 0f;
            ClearNoteUI();
        }

        public void HighlightCrosshair(bool on)
        {
            crosshair.color = on ? Color.red : Color.white;
        }
    }
}