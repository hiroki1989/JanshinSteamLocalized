using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonSEOnPointerDown : MonoBehaviour, IPointerDownHandler
{
    [Header("AudioManager SE Type")]
    [SerializeField] private SEType seType = SEType.Click;

    [Header("Options")]
    [SerializeField] private bool ignoreIfButtonNotInteractable = true;
    [SerializeField] private bool ignoreIfObjectInactive = true;

    public enum SEType
    {
        Click,
        Back,
        Confirm,
        Cancel,

        TileDiscard,
        TileDraw,
        TileSelect,
        TileSwap,

        DealOfferTile,
        BattleDamage,
        ScoringPanelOk,

        Victory,
        Defeat
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ignoreIfObjectInactive && !gameObject.activeInHierarchy) return;

        if (ignoreIfButtonNotInteractable)
        {
            var btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;
        }

        try
        {
            if (AudioManager.Instance == null) return;

            switch (seType)
            {
                case SEType.Click:         AudioManager.Instance.PlaySE_Click(); break;
                case SEType.Back:          AudioManager.Instance.PlaySE_Back(); break;
                case SEType.Confirm:       AudioManager.Instance.PlaySE_Confirm(); break;
                case SEType.Cancel:        AudioManager.Instance.PlaySE_Cancel(); break;

                case SEType.TileDiscard:   AudioManager.Instance.PlaySE_TileDiscard(); break;
                case SEType.TileDraw:      AudioManager.Instance.PlaySE_TileDraw(); break;
                case SEType.TileSelect:    AudioManager.Instance.PlaySE_TileSelect(); break;
                case SEType.TileSwap:      AudioManager.Instance.PlaySE_TileSwap(); break;

                case SEType.DealOfferTile: AudioManager.Instance.PlaySE_DealOfferTile(); break;
                case SEType.BattleDamage:  AudioManager.Instance.PlaySE_BattleDamage(); break;
                case SEType.ScoringPanelOk:AudioManager.Instance.PlaySE_ScoringPanelOk(); break;

                case SEType.Victory:       AudioManager.Instance.PlaySE_Victory(); break;
                case SEType.Defeat:        AudioManager.Instance.PlaySE_Defeat(); break;
            }
        }
        catch
        {
            // SEが鳴らなくても進行は止めない
        }
    }
}