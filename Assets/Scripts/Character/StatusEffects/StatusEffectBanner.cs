using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectBanner : MonoBehaviour
{
    public Image statusEffectImage;
    public TMP_Text amountText;
    public GameObject amountBg;
    public Image statusEffectCounterFill;
    public TMP_Text statusEffectCounterText;
    public void SetBannerData(CharacterStatusEffect.StatusEffect statusEffect)
    {
        statusEffectImage.sprite = statusEffect.statusEffectBaseSO.icon;
        statusEffectCounterText.text = statusEffect.cd.ToString("F1");
        statusEffectCounterFill.fillAmount = statusEffect.cd / statusEffect.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
        amountBg.gameObject.SetActive(statusEffect.amount > 1);
        amountText.text = statusEffect.amount.ToString();
    }
    public void RefreshCD(CharacterStatusEffect.StatusEffect statusEffect)
    {
        statusEffectCounterText.text = statusEffect.cd.ToString("F1");
        statusEffectCounterFill.fillAmount = statusEffect.cd / statusEffect.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
    }
    public void RefreshData(CharacterStatusEffect.StatusEffect statusEffect)
    {
        statusEffectCounterText.text = statusEffect.cd.ToString("F1");
        statusEffectCounterFill.fillAmount = statusEffect.cd / statusEffect.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
        amountBg.gameObject.SetActive(statusEffect.amount > 1);
        amountText.text = statusEffect.amount.ToString();
    }
}
