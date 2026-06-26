using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolve : MonoBehaviour
{
    public List<Renderer> objectsToDisolve;
    public float dissolveDuration = 1f;
    public bool needAppear = false;
    private void Awake()
    {
        ObtainCharacterModels();
        if (needAppear) NeedAppear();
    }
    public virtual void ObtainCharacterModels() { }
    [NaughtyAttributes.Button]
    public void NeedAppear()
    {
        for (int i = 0; i < objectsToDisolve.Count; i++)
        {
            objectsToDisolve[i].material.SetFloat("_DissolveAmount", 1);
        }
        Shader.SetGlobalFloat("_DissolveAmount", 1);
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
    public IEnumerator DissolveObj()
    {
        if (objectsToDisolve.Count > 0)
        {
            float elapsed = 0f;
                while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedValue = Mathf.Clamp01(elapsed / dissolveDuration);
                for (int i = 0; i < objectsToDisolve.Count; i++)
                {
                    for (int j = 0; j < objectsToDisolve[i].materials.Length; j++)
                    {
                        objectsToDisolve[i].materials[j].SetFloat("_DissolveAmount", normalizedValue);
                    }
                }
                Shader.SetGlobalFloat("_DissolveAmount", normalizedValue);
                yield return null;
            }
        }
    }
    public IEnumerator AppearObj()
    {
        if (objectsToDisolve.Count > 0)
        {
            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedValue = Mathf.Clamp01(1f - (elapsed / dissolveDuration));
                for (int i = 0; i < objectsToDisolve.Count; i++)
                {
                    for (int j = 0; j < objectsToDisolve[i].materials.Length; j++)
                    {
                        objectsToDisolve[i].materials[j].SetFloat("_DissolveAmount", normalizedValue);
                    }
                }
                Shader.SetGlobalFloat("_DissolveAmount", normalizedValue);
                yield return null;
            }
        }
    }
}