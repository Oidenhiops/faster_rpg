using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolve : MonoBehaviour
{
    [SerializeField] Renderer[] objectsToDisolve;
    public float refreshRate = 0.025f;
    public float dissolveRate = 0.0125f;
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
            float counter = 0;
            while (objectsToDisolve[0].material.GetFloat("_DissolveAmount") < 1)
            {
                counter += dissolveRate;
                for (int i = 0; i < objectsToDisolve.Length; i++)
                {
                    objectsToDisolve[i].material.SetFloat("_DissolveAmount", counter);
                }
                yield return new WaitForSeconds(refreshRate);
            }
        }
    }
    IEnumerator AppearObj()
    {
        if (objectsToDisolve.Length > 0)
        {
            float counter = 1;
            while (objectsToDisolve[0].material.GetFloat("_DissolveAmount") > 0)
            {
                counter -= dissolveRate;
                for (int i = 0; i < objectsToDisolve.Length; i++)
                {
                    objectsToDisolve[i].material.SetFloat("_DissolveAmount", counter);
                }
                yield return new WaitForSeconds(refreshRate);
            }
        }
    }
}