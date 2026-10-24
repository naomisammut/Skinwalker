using UnityEngine;

public class RangeCircle : MonoBehaviour
{
    public float radius = 5f;
    public Color color = Color.red;
    public float width = 0.1f;

    LineRenderer line;
    const int Segments = 64;

    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = Segments;
        line.startWidth = line.endWidth = width;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = color;
    }

    void LateUpdate()
    {
        Collider col = GetComponent<Collider>();
        float y = (col ? col.bounds.min.y : transform.position.y) + 0.05f; // just above the floor

        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(
                transform.position.x + Mathf.Cos(a) * radius, y,
                transform.position.z + Mathf.Sin(a) * radius));
        }
    }
}