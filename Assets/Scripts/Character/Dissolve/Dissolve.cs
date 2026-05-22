using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolve : MonoBehaviour
{
    [SerializeField] Renderer[] objectsToDisolve;
    public float dissolveDuration = 1f;
    public bool needAppear = false;
    private void Awake()
    {
        if (needAppear) NeedAppear();
    }
    [NaughtyAttributes.Button]
    public void NeedAppear()
    {
        for (int i = 0; i < objectsToDisolve.Length; i++)
        {
            objectsToDisolve[i].material.SetFloat("_DissolveAmount", 1);
        }
        AppearObject();
    }
    [NaughtyAttributes.Button]
    public void AppearObject()
    {
        StartCoroutine(AppearObj());
    }
    [NaughtyAttributes.Button]
    public void DissolveObject()
    {
        StartCoroutine(DissolveObj());
    }
    IEnumerator DissolveObj()
    {
        if (objectsToDisolve.Length > 0)
        {
            float elapsed = 0f;
                while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedValue = Mathf.Clamp01(elapsed / dissolveDuration);
                for (int i = 0; i < objectsToDisolve.Length; i++)
                {
                    objectsToDisolve[i].material.SetFloat("_DissolveAmount", normalizedValue);
                }
                yield return null;
            }
        }
    }
    IEnumerator AppearObj()
    {
        if (objectsToDisolve.Length > 0)
        {
            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedValue = Mathf.Clamp01(1f - (elapsed / dissolveDuration));
                for (int i = 0; i < objectsToDisolve.Length; i++)
                {
                    objectsToDisolve[i].material.SetFloat("_DissolveAmount", normalizedValue);
                }
                yield return null;
            }
        }
    }
}