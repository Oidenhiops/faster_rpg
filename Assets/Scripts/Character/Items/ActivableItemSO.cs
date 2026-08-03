using System.Collections;
using UnityEngine;
[CreateAssetMenu(fileName = "ActivableItem", menuName = "ScriptableObjects/Items/ActivableItem", order = 1)]
public class ActivableItemSO : ItemBaseSO
{
    public GameObject activableItemPrefab;
    public override async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        if (characterItem.activableItemIsActive)
        {
            characterItem.activableItemId = GetRandomId(character);
            GameObject activablePrefab = Instantiate(
                activableItemPrefab,
                character.characterModel.meshesData[ItemsDBSO.TypeModel.FastItems][0].meshRenderer.transform.position,
                Quaternion.identity,
                character.characterModel.meshesData[ItemsDBSO.TypeModel.FastItems][0].meshRenderer.transform
            );
            Coroutine handleCoroutine = character.StartCoroutine(DiscountDurability(character, characterItem, refreshModel, isFastItem));
            character.activableItems[characterItem.activableItemId] = new CharacterBase.ActibableItemsInfo(this, activablePrefab, handleCoroutine);
        }
    }
    public override async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        if (characterItem.activableItemIsActive)
        {
            if (character.activableItems[characterItem.activableItemId].activableItemPrefab)
            {
                Destroy(character.activableItems[characterItem.activableItemId].activableItemPrefab);
            }
            if (character.activableItems[characterItem.activableItemId].handleCoroutine != null)
            {
                character.StopCoroutine(character.activableItems[characterItem.activableItemId].handleCoroutine);
            }
            character.activableItems.Remove(characterItem.activableItemId);
        }
    }
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        useItemInfo.characterItem.activableItemIsActive = !useItemInfo.characterItem.activableItemIsActive;
        if (useItemInfo.characterItem.activableItemIsActive)
        {
            useItemInfo.characterItem.activableItemId = GetRandomId(useItemInfo.character);
            GameObject activablePrefab = Instantiate(
                activableItemPrefab,
                useItemInfo.character.characterModel.meshesData[ItemsDBSO.TypeModel.FastItems][0].meshRenderer.transform.position,
                Quaternion.identity,
                useItemInfo.character.characterModel.meshesData[ItemsDBSO.TypeModel.FastItems][0].meshRenderer.transform
            );
            Coroutine handleCoroutine = useItemInfo.character.StartCoroutine(DiscountDurability(useItemInfo.character, useItemInfo.characterItem, false, true));
            useItemInfo.character.activableItems[useItemInfo.characterItem.activableItemId] = new CharacterBase.ActibableItemsInfo(this, activablePrefab, handleCoroutine);
        }
        else
        {
            if (useItemInfo.character.activableItems[useItemInfo.characterItem.activableItemId].activableItemPrefab)
            {
                Destroy(useItemInfo.character.activableItems[useItemInfo.characterItem.activableItemId].activableItemPrefab);
            }
            if (useItemInfo.character.activableItems[useItemInfo.characterItem.activableItemId].handleCoroutine != null)
            {
                useItemInfo.character.StopCoroutine(useItemInfo.character.activableItems[useItemInfo.characterItem.activableItemId].handleCoroutine);
            }
            useItemInfo.character.activableItems.Remove(useItemInfo.characterItem.activableItemId);
        }
    }
    int GetRandomId(CharacterBase character)
    {
        int nextId = 0;
        while (nextId == 0 || character.activableItems.ContainsKey(nextId))
        {
            nextId = Random.Range(1, int.MaxValue);
        }
        return nextId;
    }
    public IEnumerator DiscountDurability(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        while (characterItem.activableItemIsActive)
        {
            characterItem.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue -= 1;
            character.characterPlayerHud?.RefreshFastItems();
            if (characterItem.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue <= 0)
            {
                characterItem.activableItemIsActive = false;
                if (character.activableItems[characterItem.activableItemId].activableItemPrefab)
                {
                    Destroy(character.activableItems[characterItem.activableItemId].activableItemPrefab);
                }
                character.activableItems.Remove(characterItem.activableItemId);
                break;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
