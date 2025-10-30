using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TransitionPanelAnimation : MonoBehaviour
{
    private bool fadeIn;
    private bool restart;
    private bool waitingForLoad;
    private float duration;
    private float fraction;
    private ScenesManager.Scene baseScene;
    private ScenesManager.Scene transitionScene;
    private Color baseColor;
    private Color transitionColor;

    [Header("Loading Screen")]
    public GameObject loadingIndicator; // Assign a loading spinner/text in inspector
    public TextMeshProUGUI loadingText; // Optional: for "Loading..." text

    public void SetAnimation(bool r, float t, ScenesManager.Scene bs, ScenesManager.Scene ts)
    {
        fadeIn = true;
        restart = r;
        duration = t;
        fraction = 0;
        baseScene = bs;
        transitionScene = ts;
        waitingForLoad = false;
        ScenesManager.ins.transition = true;

        // Check if we're loading into the game scene (not restart)
        if (ts == ScenesManager.Scene.Game && !restart)
        {
            if (loadingIndicator) loadingIndicator.SetActive(false);
            if (loadingText) loadingText.text = "Loading...";

            // Subscribe to level loaded event
            if (LevelManager.ins != null)
            {
                LevelManager.ins.OnLevelLoaded += OnLevelLoadComplete;
            }
        }
        else
        {
            if (loadingIndicator) loadingIndicator.SetActive(false);
        }
    }

    private void OnLevelLoadComplete()
    {
        // Unsubscribe
        if (LevelManager.ins != null)
        {
            LevelManager.ins.OnLevelLoaded -= OnLevelLoadComplete;
        }

        waitingForLoad = false;
        if (loadingIndicator) loadingIndicator.SetActive(false);
    }

    private void Awake()
    {
        baseColor = GetComponent<Image>().color;
        transitionColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1);

        if (loadingIndicator) loadingIndicator.SetActive(false);
    }

    private void Update()
    {
        if (fadeIn)
        {
            if (fraction >= 1)
            {
                // Check if we need to wait for level loading
                if (transitionScene == ScenesManager.Scene.Game && !restart)
                {
                    if (LevelManager.ins != null && !LevelManager.ins.ready)
                    {
                        // Still loading - show indicator and wait
                        if (!waitingForLoad)
                        {
                            waitingForLoad = true;
                            if (loadingIndicator) loadingIndicator.SetActive(true);
                        }
                        fraction = 1.0f;
                        return; // Keep waiting
                    }
                }

                // Level ready or not needed - continue with original logic
                if (waitingForLoad)
                {
                    waitingForLoad = false;
                    if (loadingIndicator) loadingIndicator.SetActive(false);
                }

                // ORIGINAL CODE FROM HERE:
                if (restart)
                {
                    GameManager.ins.RestartGame();
                }
                if (baseScene != transitionScene)
                {
                    ScenesManager.ins.HideScene(baseScene);
                    ScenesManager.ins.UnhideScene(transitionScene);
                }
                if (GameManager.ins.paused)
                {
                    GameManager.ins.UnpauseGame();
                }
                fraction = 1.0f;
                fadeIn = false;
            }
            else
            {
                fraction += Time.deltaTime / duration * 2;
            }
        }
        else
        {
            if (fraction <= 0)
            {
                ScenesManager.ins.transition = false;
                gameObject.SetActive(false);
            }
            fraction -= Time.deltaTime / duration * 2;
        }

        Color c = GetComponent<Image>().color;
        GetComponent<Image>().color = Color.Lerp(baseColor, transitionColor, fraction);
    }

    private void OnDestroy()
    {
        // Clean up event subscription
        if (LevelManager.ins != null)
        {
            LevelManager.ins.OnLevelLoaded -= OnLevelLoadComplete;
        }
    }
}