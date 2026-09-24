using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace TextInspectSystem
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

        [Header("Timer")]
        [SerializeField] private float onScreenTimer = 5f;
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Crosshair")]
        [SerializeField] private Image crosshair;

        [Header("Should persist?")]
        [SerializeField] private bool persistAcrossScenes = true;

        private CanvasGroup objectDetailsCanvasGroup;
        private bool startTimer;
        private float timer;
        private float shownAtTime;
        private Coroutine fadeRoutine;

        public static TextInspectUIManager instance;

        public bool IsObjectDetailsVisible => objectDetailsCanvasGroup != null && objectDetailsCanvasGroup.alpha > 0.01f;

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
                if (persistAcrossScenes)
                {
                    transform.SetParent(null, false);
                    DontDestroyOnLoad(gameObject);
                }
            }

            objectDetailsCanvasGroup = objectDetailsBG.GetComponent<CanvasGroup>();

            objectNameBG.SetActive(false);
            objectDetailsCanvasGroup.alpha = 0;

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
                    ClearObjectDetails();
                    startTimer = false;
                }
            }
        }

        private void StartFade(bool fadeIn, float duration)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }
            fadeRoutine = StartCoroutine(FadeUI(fadeIn, duration));
        }

        public IEnumerator FadeUI(bool fadeIn, float duration)
        {
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = 1f - startAlpha;
            float elapsedTime = 0f;

            objectDetailsCanvasGroup.alpha = startAlpha;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                objectDetailsCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                yield return null;
            }
            objectDetailsCanvasGroup.alpha = endAlpha;
            fadeRoutine = null;
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

        public void ShowObjectDetails(string newInfo)
        {
            shownAtTime = Time.unscaledTime;
            Debug.Log($"[Insight UI] Showing details at {shownAtTime:F2}s, timer={onScreenTimer}s.");

            objectDetailsText.text = newInfo;
            StartFade(true, fadeInDuration);
            timer = onScreenTimer;
            startTimer = true;
            Debug.Log($"[Insight UI] ShowObjectDetails: onScreenTimer={timer}s");
        }

        public void ShowObjectDetailsKeepOpen(string newInfo)
        {
            objectDetailsText.text = newInfo;
            startTimer = false;
            timer = 0f;
            StartFade(true, fadeInDuration);
        }

        public void HideObjectDetails()
        {
            startTimer = false;
            timer = 0f;
            StartFade(false, fadeOutDuration);
            if (objectDetailsCanvasGroup.alpha > 0.01f)
            {
                Debug.Log($"[Insight UI] HideObjectDetails called. Visible for {Time.unscaledTime - shownAtTime:F2}s.");
            }
        }

        void ClearObjectDetails()
        {
            objectNameBG.SetActive(false);
            StartFade(false, fadeOutDuration);
        }

        public void HighlightCrosshair(bool on)
        {
            crosshair.color = on ? Color.red : Color.white;
        }
    }
}
