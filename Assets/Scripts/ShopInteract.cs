using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the Shop object.
// Shows a "[E] TO INTERACT" prompt when the player is close AND looking at the shop.
public class ShopInteract : MonoBehaviour
{
    [Header("References")]
    public Transform player;            // auto-found if left empty
    public Camera playerCamera;         // auto-found (Main Camera) if left empty

    [Header("Interaction")]
    public float interactDistance = 3f;
    [Range(5f, 90f)] public float lookAngle = 35f;   // how directly you must look at the shop

    [Header("Prompt look")]
    public string promptText = "TO INTERACT";
    public string keyLabel = "E";
    public float fadeSpeed = 8f;

    private bool _canInteract;
    private float _alpha;
    private bool _pressedFlash;
    private GUIStyle _keyStyle;
    private GUIStyle _textStyle;

    private void Start()
    {
        if (player == null)
        {
            var fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null) player = fpc.transform;
        }
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (player == null || playerCamera == null) return;

        // Close enough?
        bool near = Vector3.Distance(player.position, transform.position) <= interactDistance;

        // Looking at it?
        Vector3 toShop = (transform.position - playerCamera.transform.position).normalized;
        bool looking = Vector3.Angle(playerCamera.transform.forward, toShop) <= lookAngle;

        _canInteract = near && looking;

        // Smooth fade in/out
        _alpha = Mathf.MoveTowards(_alpha, _canInteract ? 1f : 0f, fadeSpeed * Time.deltaTime);

        _pressedFlash = false;
        if (_canInteract && Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed) _pressedFlash = true;
            if (Keyboard.current.eKey.wasPressedThisFrame) Interact();
        }
    }

    private void Interact()
    {
        // Nothing happens yet. Put shop logic here later.
        Debug.Log("Player used the shop!");
    }

    private void OnGUI()
    {
        if (_alpha <= 0.01f) return;

        float s = Screen.height / 1080f;     // scale with resolution
        int keySize = Mathf.RoundToInt(38 * s);

        if (_keyStyle == null)
        {
            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
        }
        _keyStyle.fontSize = Mathf.RoundToInt(24 * s);
        _textStyle.fontSize = Mathf.RoundToInt(22 * s);

        float textWidth = 220 * s;
        float gap = 12 * s;
        float totalWidth = keySize + gap + textWidth;

        // Center of the screen
        float x = (Screen.width - totalWidth) / 2f;
        float y = (Screen.height - keySize) / 2f;

        Rect keyRect = new Rect(x, y, keySize, keySize);
        Rect textRect = new Rect(x + keySize + gap, y, textWidth, keySize);

        // Key box (white square, turns gray while held)
        GUI.color = new Color(0f, 0f, 0f, 0.45f * _alpha);
        GUI.DrawTexture(new Rect(keyRect.x + 2 * s, keyRect.y + 2 * s, keySize, keySize), Texture2D.whiteTexture); // shadow

        float c = _pressedFlash ? 0.6f : 1f;
        GUI.color = new Color(c, c, c, _alpha);
        GUI.DrawTexture(keyRect, Texture2D.whiteTexture);

        // "E" in black
        _keyStyle.normal.textColor = new Color(0f, 0f, 0f, _alpha);
        GUI.color = Color.white;
        GUI.Label(keyRect, keyLabel, _keyStyle);

        // Text with drop shadow
        _textStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * _alpha);
        GUI.Label(new Rect(textRect.x + 2 * s, textRect.y + 2 * s, textRect.width, textRect.height), promptText, _textStyle);
        _textStyle.normal.textColor = new Color(1f, 1f, 1f, _alpha);
        GUI.Label(textRect, promptText, _textStyle);
    }

    // Shows the interact range in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}