using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuDevilContractButton : MonoBehaviour
{
    void Start(){GetComponent<Button>().onClick.AddListener(()=>DevilContractView.Open());Refresh();}
    void OnEnable(){LocalizationManager.LanguageChanged+=Changed;DevilContracts.Changed+=Refresh;}
    void OnDisable(){LocalizationManager.LanguageChanged-=Changed;DevilContracts.Changed-=Refresh;}
    void Changed(LocalizationManager.Language language)=>Refresh();
    void Refresh(){var font=LocalizationManager.Instance.GetTitleFont();foreach(var t in GetComponentsInChildren<TMP_Text>(true)){if(font){t.font=font;t.fontSharedMaterial=font.material;}t.text=GameUIText.Get("悪魔の契約","Devil contracts","恶魔契约");}}
}
