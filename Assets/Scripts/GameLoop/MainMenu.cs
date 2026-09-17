using UC;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup menuGroup;
    [SerializeField]
    private CanvasGroup creditsGroup;
    [SerializeField]
    private BigTextScroll creditsScroller;

    public void StartGame()
    {
        FullscreenWiper.WipeOut(0.5f, WipeType.CurtainDown, () =>
        {
            SceneManager.LoadScene("GameScene");
        });
    }

    public void ShowCredits()
    {
        menuGroup.FadeOut(0.5f);
        creditsGroup.FadeIn(0.5f);

        creditsScroller.Reset();
        creditsScroller.onEndScroll += CreditsScroller_onEndScroll;
    }

    private void CreditsScroller_onEndScroll()
    {
        creditsScroller.onEndScroll -= CreditsScroller_onEndScroll;

        creditsGroup.FadeOut(0.5f);

        menuGroup.FadeIn(0.5f);
    }

    public void QuitGame()
    {
        FullscreenWiper.WipeOut(0.5f, WipeType.CurtainDown, () =>
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        });
    }
}
