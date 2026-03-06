using System;
using UnityEngine;
using UnityEngine.UI;

public class ManagementLoaderSceneWithProgressBar : ManagementLoaderScene
{
    public float speedFill;
    public Image loaderImage;
    public override async Awaitable ValidateChargeIsComplete()
    {
        loaderImage.fillAmount = 0;
        try
        {
            while (true)
            {
                float value = currentLoad / 100;
                loaderImage.fillAmount = Mathf.MoveTowards(loaderImage.fillAmount, value, speedFill * Time.deltaTime);
                await Awaitable.NextFrameAsync();
                if (loaderImage.fillAmount >= 1)
                {
                    break;
                }
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
            loaderImage.fillAmount = 0;
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
