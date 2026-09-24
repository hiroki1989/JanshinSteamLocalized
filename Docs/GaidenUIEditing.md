# 外伝モードのUI編集

Projectウィンドウで `Assets/Resources/SeventeenSteps/GaidenUISettings.asset` を選択します。再生を停止して編集してください。

- Tutorial Title / Tutorial Pages: チュートリアルのタイトルと各ページ本文。配列の順番がページ順です。ページ数も変更できます。
- Previous Label / Next Label / Start Label: ナビゲーションの文言。
- Button Sprite / Button Image Type / Button Color: Tier選択の入口ボタン背景。
- Frame Sprite / Frame Color / Show Frame: 飾り枠。Frame Sprite未設定時は共通枠を使用します。
- Show Seals / Seal Color: 左右の菱形飾り。
- Button Label / Label Color / Button Font Size: ボタンの文字。Label内のcolorタグはLabel Colorより優先します。
- Button Position / Button Size: 右端基準の位置と大きさ。

保存後、該当画面を開き直すと反映されます。TierSelectControllerのGaiden UI Settingsに別の同型アセットを指定する場合、その指定は入口ボタンだけに適用されます。通常は未指定のまま共通アセットを使用してください。

役プレビューは13枚の仮選択時に表示されます。全待ちで共通する行は白、待ち次第で変わる行は橙色の「候補」です。リーチ可能な場合はリーチ後の翻数を表示し、裏ドラ・一発・河底・お札倍率は含めません。指定牌未使用の減点は含みます。文字は3列内で自動縮小します。
