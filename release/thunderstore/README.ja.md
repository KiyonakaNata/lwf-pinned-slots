# LWF Pinned Slots（Thunderstore 用 README の日本語対訳）

同梱しない。英語版 `README.md` の中身を確認するための控え。

---

# LWF Pinned Slots

**Lazy Witch's Factory** の所持品欄のアイコンの場所を固定する MOD。

## アイコンの場所を固定

- 所持品欄のアイコン表示位置を固定する
- 固定した場所には印
- 在庫 0 の場所は薄いアイコンと「0」

![アイコンの場所を固定](動画: pin-keep.webp)

## 固定・解除

- セルを右クリック → その品目を固定
- 固定した場所を右クリック → 解除

![固定と解除](動画: pin-toggle.webp)

## 入れ替え

- セルを別のセルへドラッグ → 中身と固定が一緒に移る
- 落とし先に品目があれば入れ替わる

---

設定は `BepInEx/config/kiyonakanata.lwfpinnedslots.cfg`。詳しくは [GitHub](https://github.com/KiyonakaNata/lwf-pinned-slots)。

Lazy Witch's Factory **ver 0.29.1** で動作確認。非公式のMOD、公式のサポート対象外。
