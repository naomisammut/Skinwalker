using UnityEngine;

public static class Sight
{
    // True if nothing (like a tree) blocks the line between a and b
    public static bool Clear(Transform a, Transform b)
    {
        if (Physics.Linecast(a.position, b.position, out RaycastHit hit))
            return hit.transform.IsChildOf(a) || hit.transform.IsChildOf(b);
        return true;
    }
}