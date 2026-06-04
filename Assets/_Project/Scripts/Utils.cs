using System;
using System.Collections;
using UnityEngine;

public static class Utils
{
    // This function returns a coroutine you can start from any MonoBehaviour
    public static IEnumerator Delay(float delaySeconds, Action callback)
    {
        yield return new WaitForSeconds(delaySeconds);
        callback?.Invoke();
    }

    public static void CallAfterDelay(MonoBehaviour caller, float delaySeconds, Action callback)
    {
        caller.StartCoroutine(Delay(delaySeconds, callback));
    }

    // Delay with return value
    public static IEnumerator Delay<T>(float delaySeconds, Func<T> resultFunc, Action<T> callback)
    {
        yield return new WaitForSeconds(delaySeconds);
        if (resultFunc != null && callback != null)
        {
            T result = resultFunc();
            callback(result);
        }
    }

    public static void CallAfterDelay<T>(MonoBehaviour caller, float delaySeconds, Func<T> resultFunc, Action<T> callback)
    {
        caller.StartCoroutine(Delay(delaySeconds, resultFunc, callback));
    }

    public static void StandOnTop(GameObject standing, GameObject baseObj)
    {
        var baseBounds = GetWorldBounds(baseObj);
        StandOnTop(standing, baseBounds);
    }

    public static void StandOnTop(GameObject standing, Bounds baseBounds)
    {
        var standBounds = GetWorldBounds(standing);

        // Move standing object so its bottom touches base's top
        float deltaY = baseBounds.max.y - standBounds.min.y;

        standing.transform.position += new Vector3(0f, deltaY, 0f);
    }

    private static Bounds GetWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b;
    }
}
