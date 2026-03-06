using System;
using UnityEngine;

public class ManagementLoaderSceneBlackOut : ManagementLoaderScene
{
    public override async Awaitable ValidateChargeIsComplete()
    {
        try
        {
            while (true)
            {
                if (currentLoad >= 100)
                {
                    break;
                }
                await Awaitable.NextFrameAsync();
            }
            _ = FinishLoad();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public async Awaitable FinishLoad()
    {
        try
        {
            while (loaderAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                await Awaitable.NextFrameAsync();
            }
            loaderAnimator.SetBool("Out", false);
            while (true)
            {
                if (loaderAnimator.GetCurrentAnimatorStateInfo(0).IsName("LoaderOpen") && loaderAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1)
                {
                    break;
                }
                await Awaitable.NextFrameAsync();
            }
            OnFinishOpenAnimation?.Invoke();
            OnFinishOpenAnimation = null;
            Destroy(gameObject);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
}
