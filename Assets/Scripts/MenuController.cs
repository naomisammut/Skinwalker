using UnityEngine;
using UnityEngine.InputSystem;

// Attach to any object in the scene (e.g. an empty GameObject called "MenuManager").
// The Shop opens it (E). X (key) or the X button closes it.
public class MenuController : MonoBehaviour
{
    [Header("References")]
    public FirstPersonController playerController;   // auto-found if left empty

    [Header("Menu look")]
    public string title = "MENU";
    public Vector2 panelSize = new Vector2(700f, 450f);   // at 1080p

    [Header("Extra")]
    public bool alsoOpenWithP;   // tick this if you want P to open it too

    public static bool IsOpen { get; private set; }

    private GUIStyle _titleStyle;
    private GUIStyle _hintStyle;
    private GUIStyle _closeButtonStyle;

    private void Start()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<FirstPersonController>();
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (alsoOpenWithP && keyboard.pKey.wasPressedThisFrame)
        {
            if (IsOpen) CloseMenu();
            else OpenMenu();
        }

        if (IsOpen && keyboard.xKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }

    public void OpenMenu()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (playerController != null) playerController.enabled = false;   // stop walking / looking
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (playerController != null) playerController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (!IsOpen) return;

        float s = Screen.height / 1080f;     // scale with resolution

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _closeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
        _titleStyle.fontSize = Mathf.RoundToInt(40 * s);
        _hintStyle.fontSize = Mathf.RoundToInt(18 * s);
        _closeButtonStyle.fontSize = Mathf.RoundToInt(22 * s);
        _titleStyle.normal.textColor = Color.white;
        _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

        // Dim the whole screen behind the menu
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Panel (centered): border, then fill
        float w = panelSize.x * s;
        float h = panelSize.y * s;
        Rect panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        float border = 3f * s;

        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(panel.x - border, panel.y - border, panel.width + border * 2, panel.height + border * 2), Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.97f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Title
        GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 60 * s), title, _titleStyle);

        // Hint at the bottom
        GUI.Label(new Rect(panel.x, panel.yMax - 50 * s, panel.width, 30 * s), "Press X to close", _hintStyle);

        // X button in the top-right corner
        float btn = 40 * s;
        Rect closeRect = new Rect(panel.xMax - btn - 12 * s, panel.y + 12 * s, btn, btn);
        if (GUI.Button(closeRect, "X", _closeButtonStyle))
        {
            CloseMenu();
        }
    }
}