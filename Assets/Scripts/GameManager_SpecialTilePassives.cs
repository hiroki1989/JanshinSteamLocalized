public partial class GameManager
{
    void SpecialTilePassives_Refresh()
    {
        if(_preparedForSceneUnload||!isActiveAndEnabled)return;
        UpdateRightInfoUI_Manual();
    }
    void SpecialTilePassives_OnLanguageChanged(LocalizationManager.Language language)=>SpecialTilePassives_Refresh();
}
