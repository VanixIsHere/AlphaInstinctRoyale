using UnityEngine;

public class UIBlockerManager : MonoBehaviour
{
    public bool IsBlockingInput { get; private set; }

    public void SetBlocking(bool isBlocked)
    {
        IsBlockingInput = isBlocked;
    }
}
