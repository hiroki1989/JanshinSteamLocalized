# iOS広告・課金の設定と確認

Google Mobile Ads Unity Pluginは8.7.0のままです。iOS依存SDKは10.14系です。

## UIの場所
- ShopScene: Commerce（IAPShopPanel）からIAPShopとOpenGemShopを参照。
- Assets/Resources/Commerce/IAPShop.prefab: 課金画面の編集用Prefab。Sceneで内容を確認できます。
- MenuScene: Adに広告・課金のサービス。Canvas内Adがリワード広告ボタン。
- Assets/Resources/Commerce/Services.prefab: ShopSceneから直接開始した場合のサービス。
  本番IDを入力するときはMenuSceneのAdとこのPrefabの両方をそろえてください。

## 現在のテスト設定
- iOSアプリID: ca-app-pub-3940256099942544~1458002511
- リワード: ca-app-pub-3940256099942544/1712485313（1回1宝石、1日10回）
- 全画面: ca-app-pub-3940256099942544/4411468910（3回に1回、100秒間隔）
- 宝石パック: 10 / 55 / 130個。商品IDの末尾の100 / 500 / 1200は仮IDの名称であり、付与数ではありません。
- Editorの課金はUnity IAP Fake Storeです。実際の請求はありませんが、テスト購入を承認するとローカルのゲームに商品が付与されます。
- 実機は商品IDが未登録のため購入無効、価格は「—」です。任意の金額を本物の価格として表示しません。

## 本番への切替
AdMobのiOSアプリ・広告ユニットと、App Store Connectの広告カット（非消耗型）・宝石3商品（消耗型）を作成してください。
Google Mobile Ads SettingsのiOS App ID、MenuSceneのAdとServices.prefabの広告ユニットID・商品IDを入力します。
価格はApp Storeから取得します。数量を変える場合は各IAPManagerの付与数もそろえます。
UMP同意メッセージはAdMob管理画面で設定してください。必要なユーザーには同意画面と広告プライバシー設定が表示されます。
現在の実装はIDFAを独自に要求しません。追跡を伴う配信に切り替える場合は、AdMobの設定とATTの構成を実機で確認してください。

## 保存
従来のSP_Gemsを最初の読込時に引き継ぎます。以後SP_WalletV1に宝石残高と付与済み取引IDをまとめて保存します。
広告カットはIAP_AdFreeです。広告カット購入後も、ユーザーが選択するリワード広告は利用できます。
購入のキャンセル・失敗・承認待ち・復元結果をUIに表示します。宝石は消耗型なので復元では再付与しません。

## 検証
Tools/Janshin/Validate Commerce: 取引の再送、広告終了通知、ショップ3言語・3サイズ。
Tools/Janshin/Validate First Match Tutorial: チュートリアル表示と初回フラグ。
結果とレンダー画像はTemp/TutorialQAに出力します。これらの自動検証では既存の宝石残高を変更しません。
iPhoneでのテスト広告表示、App Store Sandbox/TestFlightでの実購入・承認待ち・復元は別途必要です。
WindowsでのC#コンパイルはXcodeでのiOSビルド成功を意味しません。

## SDKの配信制約
GoogleのiOS SDK 10系の終了予定日は2026-06-30です。終了版からの広告リクエストは配信されない可能性があります。
8.7.0維持という指定のため依存SDKも更新していません。Editor検証に成功しても実機配信は保証できません。
https://developers.google.com/admob/ios/deprecation
https://github.com/googleads/googleads-mobile-unity/releases/tag/v8.7.0

## 2026-09-06 UI revision
The gem purchase / ad removal entry moves to MenuScene, below the high score.
Edit MenuScene > Commerce (IAPShopPanel), OpenGemShop, or Resources/Commerce/IAPShop.prefab.
Planned Japan prices: 10 gems = JPY 100; 55 = JPY 400; 130 = JPY 900; ad removal = JPY 800.
The Editor and disconnected preview show these planned prices. Products are still unregistered;
configure these prices in App Store Connect. On an iOS device with a connected store, localized store prices take precedence.
Generated TMP text uses the existing 玉ねぎ楷書激無料版v7改 SDF asset as the primary font.
Fallback fonts supply only characters absent from that font.
RunScene's TutorialPassiveFocus is an editable outline region covering the passive icons and descriptions.