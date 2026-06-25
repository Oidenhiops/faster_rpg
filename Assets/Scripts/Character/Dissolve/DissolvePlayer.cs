using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DissolvePlayer : Dissolve
{
    public CharacterBase characterBase;
    public override void ObtainCharacterModels()
    {
        objectsToDisolve = new List<Renderer>();
        foreach (KeyValuePair<CharactersModelDBSO.TypeModel, CharacterBase.CharacterModelData> model in characterBase.characterModel.meshesData)
        {
            objectsToDisolve.Add(model.Value.meshRenderer);
        }
    }
}