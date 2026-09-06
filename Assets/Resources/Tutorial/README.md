# チュートリアルの編集

## 開く
Unityメニューの **Tools > Janshin > Open Tutorial Prefab** で開きます。
または同じフォルダーの **FirstMatchTutorial.prefab** をダブルクリックします。
RunSceneへの手動配置・GameManagerへのドラッグ登録は不要です。初回対局でこのPrefabを読み込みます。

## 見た目
Prefab内の **SafeArea > GuideCard** 以下を編集します。
- GuideCard: パネルのサイズ、位置、Imageの背景色
- Title: 見出しの位置、サイズ、色
- Body: 通常ページの本文の位置、サイズ、色
- BodyWithExample: 牌の例を表示するページの本文
- Hint: 補足文
- ExampleTiles: 牌の例を置く領域
- Back / Skip / Next: ボタンの位置、サイズ、色
- Dim0～3: 背景の暗さ
- Focus以下: 強調枠の色・太さ

ルートの **Auto Position Card** をOFFにすると、GuideCardの手動位置・サイズを維持します。
ONの場合も編集したカードサイズを基準にしますが、画面内に収まるよう調整し、説明対象から離れた場所に自動配置します。
フォントを各TMPに直接指定する場合は、ルートの **Use Localized Fonts** をOFFにしてください。

## 説明文・ページ順
**FirstMatchTutorialContent.asset** の Inspector を編集します。
Pagesの各要素で、Title / Body / Hint のJapanese / English / Chinese Simplifiedを変更します。
Pagesの順序も変更できます。Editor Labelは編集時に見分けるための名前です。
Focus Targetsは強調箇所、Example Tilesは牌の例です。
装備中スキルのページでは {skillName} / {skillDescription} / {mpCost} を実際の値に置き換えます。
Navigation and Skip Confirmationではボタン名とスキップ確認文も変更できます。

TMPのText欄はプレビューです。実行時は説明文データで置き換えるため、文章の修正はContent assetで行ってください。

## 編集モードで確認
Prefabルートを選択し、Inspector下部でLanguageとPageを選び **選択ページをプレビュー** を押します。
対局や初回表示フラグを変更せずに確認できます。
本文のレイアウトはBodyとBodyWithExampleを使い分けます。
変更はPrefabを保存してください。Play中の変更は保存されません。

## 検証
Tools > Janshin > Validate First Match Tutorial
一時プレビューSceneと専用のPlayerPrefsキーを使い、初回判定・中断復帰・言語別レイアウトを検証します。
結果と画像はプロジェクト内のTemp/TutorialQAに出力されます。
