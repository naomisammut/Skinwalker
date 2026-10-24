using UnityEngine;

[System.Serializable]
public class Stamina
{
    public float max = 8f;          // seconds of full-speed running
    public float drainRate = 1f;    // per second while running
    public float regenRate = 0.8f;  // per second while not running
    public float recoverAt = 0.3f;  // after exhaustion, can run again at this fraction of max

    public float current;
    public bool Exhausted { get; private set; }

    public void Init() => current = max;

    public void Tick(bool running, float dt)
    {
        if (running && !Exhausted)
        {
            current -= drainRate * dt;
            if (current <= 0f) { current = 0f; Exhausted = true; }
        }
        else
        {
            current = Mathf.Min(max, current + regenRate * dt);
            if (Exhausted && current >= max * recoverAt) Exhausted = false;
        }
    }
}