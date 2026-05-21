using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dissolve : MonoBehaviour
{
    [SerializeField] GameObject[] objectsToDisolve;
    List<Material> materials = new List<Material>();
    public float refreshRate = 0.025f;
    public float dissolveRate = 0.0125f;
    public bool needAppear = false;
    private void Awake()
    {
        foreach (var renderer in objectsToDisolve)
        {
            foreach (Material material in renderer.GetComponent<Renderer>().materials)
            {
                materials.Add(material);
            }
        }
        if (needAppear) NeedAppear();
    }
    public void NeedAppear()
    {
        for (int i = 0; i < materials.Count; i++)
        {
            materials[i].SetFloat("_DissolveAmount", 1);
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
        if (materials.Count > 0)
        {
            float counter = 0;
            while (materials[0].GetFloat("_DissolveAmount") < 1)
            {
                counter += dissolveRate;
                for (int i = 0; i < materials.Count; i++)
                {
                    materials[i].SetFloat("_DissolveAmount", counter);
                }
                yield return new WaitForSeconds(refreshRate);
            }
        }
    }
    IEnumerator AppearObj()
    {
        if (materials.Count > 0)
        {
            float counter = 1;
            while (materials[0].GetFloat("_DissolveAmount") > 0)
            {
                counter -= dissolveRate;
                for (int i = 0; i < materials.Count; i++)
                {
                    materials[i].SetFloat("_DissolveAmount", counter);
                }
                yield return new WaitForSeconds(refreshRate);
            }
        }
    }
}