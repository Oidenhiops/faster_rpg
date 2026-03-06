using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ManagementLoaderScene : MonoBehaviour
{
    public static ManagementLoaderScene Instance { get; private set; }
    public Animator loaderAnimator;
    public bool _finishLoad;
    public Action OnFinishOpenAnimation;
    public float currentLoad;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public async Awaitable AutoCharge()
    {
        _ = ValidateChargeIsComplete();
        int i = 0;
        while (true)
        {
            AdjustLoading(10 * i);
            i++;
            if (i > 10) break;
            await Awaitable.NextFrameAsync();
        }
    }
    public void AdjustLoading(float amount)
    {
        currentLoad = amount;
    }
    public bool ValidateLoaderIsOnIdle()
    {
        return loaderAnimator.GetCurrentAnimatorStateInfo(0).IsName("LoaderIdle");
    }
    public virtual async Awaitable ValidateChargeIsComplete()
    {
        await Awaitable.NextFrameAsync();
    }
}