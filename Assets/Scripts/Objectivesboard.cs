using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to the board object in the cabin (e.g. a flat Cube).
// When the game starts, it puts a paper sticker with an "OBJECTIVES" title on the board.
// The title shrinks automatically if it is wider than the paper.
// The text is hidden when you look at the board from behind.
// Not interactive. There is no E prompt.
public class ObjectivesBoard : MonoBehaviour
{
    [Header("Sticker")]
    public string title = "OBJECTIVES";
    public string[] objectives;                              // leave empty for now
    public Vector2 stickerSize = new Vector2(1.9f, 2.2f);    // world units
    public Vector2 stickerOffset;                            // move the sticker on the board (world units)
    public Color paperColor = new Color(1f, 0.96f, 0.72f);
    public Color inkColor = new Color(0.12f, 0.1f, 0.1f);

    [Header("Placement")]
    public float surfaceZ = -0.5f;     // -0.5 = a Cube's -Z face. Use 0 for a Quad.
    public bool useBackFace;           // tick if the sticker shows on the wrong side

    [Header("Text size")]
    public float titleSize = 0.03f;
    public float bodySize = 0.018f;
    [Range(0.3f, 1f)] public float titleFitWidth = 0.85f;   // title may use this much of the paper width

    private const string StickerName = "Sticker";
    private Font _font;
    private Transform _stickerRoot;
    private Vector3 _inverseScale = Vector3.one;   // cancels the board's own scale for the paper and text
    private Camera _camera;
    private bool _ready;
    private readonly List<Renderer> _textRenderers = new List<Renderer>();

    private void Start()
    {
        if (transform.Find(StickerName) == null) BuildSticker();
    }

    // Hide the text when the camera is behind the sticker (it would look mirrored)
    private void Update()
    {
        if (!_ready || _stickerRoot == null) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Vector3 toCamera = _camera.transform.position - _stickerRoot.position;
        bool inFront = Vector3.Dot(-_stickerRoot.forward, toCamera) > 0f;

        foreach (Renderer r in _textRenderers)
        {
            if (r != null) r.enabled = inFront;
        }
    }

    private void BuildSticker()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // The Sticker object itself keeps Scale 1,1,1.
        // The paper and text cancel out the board's scale so sizes below are real world units.
        Vector3 ls = transform.lossyScale;
        _inverseScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);

        _stickerRoot = new GameObject(StickerName).transform;
        _stickerRoot.SetParent(transform, false);

        float z = useBackFace ? -surfaceZ : surfaceZ;
        _stickerRoot.localPosition = new Vector3(stickerOffset.x * _inverseScale.x, stickerOffset.y * _inverseScale.y, z);
        _stickerRoot.localRotation = useBackFace ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        // Paper
        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
        paper.name = "Paper";
        Destroy(paper.GetComponent<Collider>());
        paper.transform.SetParent(_stickerRoot, false);
        paper.transform.localPosition = Vector3.Scale(new Vector3(0f, 0f, -0.002f), _inverseScale);
        paper.transform.localScale = Vector3.Scale(new Vector3(stickerSize.x, stickerSize.y, 1f), _inverseScale);
        paper.GetComponent<Renderer>().material.color = paperColor;

        // Spacing scales with the sticker and the text size
        float padding = Mathf.Max(0.04f, stickerSize.x * 0.06f);
        float top = stickerSize.y / 2f;

        // Title
        TextMesh titleMesh = CreateText(_stickerRoot, "Title", title,
            new Vector3(0f, top - padding, -0.004f),
            TextAnchor.UpperCenter, TextAlignment.Center, titleSize, FontStyle.Bold);
        StartCoroutine(FitTitleNextFrame(titleMesh));

        // Objectives list (nothing is drawn while the list is empty)
        if (objectives != null && objectives.Length > 0)
        {
            string body = "";
            for (int i = 0; i < objectives.Length; i++)
            {
                body += "- " + objectives[i];
                if (i < objectives.Length - 1) body += "\n";
            }

            float titleHeight = titleSize * 11f;   // rough height of the title text
            CreateText(_stickerRoot, "Objectives", body,
                new Vector3(-stickerSize.x / 2f + padding, top - padding - titleHeight - padding, -0.004f),
                TextAnchor.UpperLeft, TextAlignment.Left, bodySize, FontStyle.Normal);
        }
    }

    // Waits one frame so the text mesh exists, then shrinks the title if it is wider than the paper
    private IEnumerator FitTitleNextFrame(TextMesh titleMesh)
    {
        yield return null;

        if (titleMesh != null)
        {
            float width = titleMesh.GetComponent<MeshRenderer>().localBounds.size.x;
            float target = stickerSize.x * titleFitWidth;

            if (width > 0.0001f && width > target)
            {
                titleMesh.transform.localScale = _inverseScale * (target / width);
            }
        }

        _ready = true;   // from now on, Update can hide the text when seen from behind
    }

    // worldLocalPos is in real world units relative to the sticker
    private TextMesh CreateText(Transform parent, string objectName, string text, Vector3 worldLocalPos,
        TextAnchor anchor, TextAlignment alignment, float characterSize, FontStyle style)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.Scale(worldLocalPos, _inverseScale);
        go.transform.localScale = _inverseScale;

        var textMesh = go.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.font = _font;
        textMesh.fontSize = 100;               // high font size + small character size = sharp text
        textMesh.characterSize = characterSize;
        textMesh.anchor = anchor;
        textMesh.alignment = alignment;
        textMesh.fontStyle = style;
        textMesh.color = inkColor;

        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _font.material;
        _textRenderers.Add(meshRenderer);
        return textMesh;
    }
}