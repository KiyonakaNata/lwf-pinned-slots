// LWF Pinned Slots
//
// Lazy Witch's Factory の所持品欄（画面下の 11 セル。キー 1〜9・0・-）の並びを
// 品目ごとに固定する BepInEx Mod。
//
// -- 本体の仕様 ---------------------------------------------------------
// 所持品欄は InventoryDB の 11 セル（LWFParamLimits.INVENTORY_CELL_COUNT）。
//   ・入庫（InventoryDepositor.DepositInternal）は「同じ品目のセル → 先頭の空きセル」の順で詰める
//   ・出庫（InventoryWithdrawer）で 0 個になったセルは "None" に戻る
// だから在庫が切れたセルは次に来た別の品目に埋まり、キーと品目の対応がずれる。
//
// -- 何をするか ---------------------------------------------------------
// cfg の [2. Slots] で「Slot n = 品目ID」を決めておく。
//   ・入庫のたびに（DepositInternal の後ろで）並べ直す
//       - 固定した品目が席以外のセルに居たら席へ動かす（席が埋まっていれば入れ替え）
//       - 固定していない品目が予約席に居たら、空いている自由席へ動かす
//   ・出庫では並べ直さない。設置のドラッグ中に手持ちのセルが動くと
//     本体が RefreshCursorState → OnHoldCancel で操作を切るため。
//   ・cfg を変えた瞬間（ConfigurationManager）と、F6 で読み直した直後にも並べ直す。
//
// 通常プレイで欄に入る品目は 10 種（触媒 9＋杖）なので、11 席に全部固定できる。
// 固定しない品目は本体どおり自由席に詰まる。
//
// -- 書き方の制約 -------------------------------------------------------
// C# 5 コンパイラ（csc.exe / .NET Framework 4.0）でビルドするため、
// 文字列補間・?. 演算子・式形式メンバ・out var・nameof は使えない。

using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Items.Inventory;

namespace LwfPinnedSlots
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PinnedSlotsPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "kiyonakanata.lwfpinnedslots";
        internal const string PluginName = "LWF Pinned Slots";
        internal const string PluginVersion = "1.0.0";

        // 本体のセル数（LWFParamLimits.INVENTORY_CELL_COUNT）。実際の数は DB から読むので、
        // ここは cfg の項目数を決めるだけ
        internal const int SlotCount = 11;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string>[] Slots;
        internal static ConfigEntry<bool> ShowCounts;
        internal static ConfigEntry<int> GhostBrightness;
        internal static ConfigEntry<int> MarkerSize;
        internal static ConfigEntry<string> MarkerColor;
        // SlotEdit が cfg を続けて書き換える間、SettingChanged からの並べ直しを止める
        internal static bool SuppressArrange;
        private Harmony _harmony;

        // 所持品欄に入る品目。先頭の "" は「固定しない」。
        // items.csv の item_type 2 と 3 は 13 種あるが、通常プレイで欄に入らないものは外す（2026-09-14）:
        //   Summon-Caretaker / Summon-Farmer … 未実装の支援者（ベルゼブブ / アスモデウス）の触媒
        //   Summon-TransporterExit          … レイヴンの荷下ろし地点設定で置くだけで、欄には入らない
        // 候補に無い値は AcceptableValueList が "" に丸めるので、古い cfg に残っていても害はない
        internal static readonly string[] ItemIds = new string[]
        {
            "",
            "Wand",
            "Summon-Conveyor",
            "Summon-Conveyor2",
            "Summon-Splitter",
            "Summon-Splitter2",
            "Summon-Worker",
            "Summon-Transporter",
            "Summon-Crafter",
            "Summon-Smelter",
            "Summon-Summoner"
        };

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1. General", "Enabled", true, "");

            // 既定は全て空（固定なし）。入れただけでは何も変わらず、右クリックで固定した席から効く
            // （初期配布順 Wand/Conveyor/Worker/Transporter を既定にする案は 2026-09-14 に取り下げ）
            string[] defaults = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++) { defaults[i] = ""; }

            Slots = new ConfigEntry<string>[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                // AcceptableValueList にしておくと cfg に候補が書き出され、
                // ConfigurationManager では選択式になる
                Slots[i] = Config.Bind("2. Slots", "Slot " + (i + 1), defaults[i],
                    new ConfigDescription("", new AcceptableValueList<string>(ItemIds)));
            }
            // 予約席が空なら品目の絵を薄く出して「0」、1 個なら「1」を出す
            ShowCounts = Config.Bind("3. View", "Show 0 and 1 on pinned slots", true, "");
            GhostBrightness = Config.Bind("3. View", "Empty pinned slot icon brightness (%)", 35,
                new ConfigDescription("", new AcceptableValueRange<int>(0, 100)));
            // 予約席の数字の帯の左端に出す印（帯は高さ 16）
            // 本体の check_small_true（35×28）に色を掛ける。既定は白＝素の色。
            // 本体の赤 #F59488（LWFColors.RED_COLOR）も試したが白の方が見えた（2026-09-14）。
            // Hell/Inferno の赤みは DifficultyToneApplicator が Image.color を寄せているだけ
            MarkerSize = Config.Bind("3. View", "Pin marker size", 14,
                new ConfigDescription("", new AcceptableValueRange<int>(2, 64)));
            MarkerColor = Config.Bind("3. View", "Pin marker color", "#FFFFFF", "");
            Config.SettingChanged += OnSettingChanged;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PinnedSlotsPlugin).Assembly);
            int patches = 0;
            foreach (System.Reflection.MethodBase m in _harmony.GetPatchedMethods()) { patches++; }
            Log.LogInfo("[boot] " + PluginName + " " + PluginVersion + " patches=" + patches);

            // F6（ScriptEngine）で読み直したときは、既に所持品があるのでその場で並べ直す
            SlotArranger.ArrangePlayerInventory();
            SlotView.RefreshAll();
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnSettingChanged;
            SlotMouse.DetachAll();
            if (_harmony != null) { _harmony.UnpatchSelf(); }
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (SuppressArrange) { return; }
            SlotArranger.ArrangePlayerInventory();
            SlotView.RefreshAll();
        }
    }

    /// <summary>入庫のたびに所持品欄を並べ直す。プレイヤーの所持品欄のときだけ動く。</summary>
    [HarmonyPatch(typeof(InventoryDepositor), "DepositInternal")]
    internal static class DepositInternalPatch
    {
        private static void Postfix(InventoryDepositor __instance)
        {
            if (!PinnedSlotsPlugin.Enabled.Value) { return; }
            InventoryManager inv = InventoryManager.playerInventory;
            // 工房や宝箱などの InventoryDB も同じ Depositor を使うので、プレイヤーの物か確かめる
            if (inv == null || !object.ReferenceEquals(inv.GetDepositor(), __instance)) { return; }
            SlotArranger.Arrange(inv);
        }
    }
}
