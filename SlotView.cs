// 席の見える化。予約席が空のときはその品目の絵を薄く出して「0」、1 個のときは「1」を出す。
//
// 本体（InventoryViewSynchronizer.Sync）はセルの絵と数字を「品目の絵・2 個以上なら数字」で描く。
// 0 個のセルは "None" の絵（空）で数字も無いので、どの席がどの品目か分からない。
// Sync の Postfix で、プレイヤーの所持品欄のときだけ上書きする。
//
// 触るのは CellComponents の絵・色・文字だけ。DB には触らない。
// 色は薄くした席だけ覚えておき、戻すときは白に決め打ち（本体はセルの色を変えない）。

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Items;
using Items.Inventory;
using UI.SelectableWindow.Cell;
using UnityEngine;
using Utility.StringFormatter;

namespace LwfPinnedSlots
{
    internal static class SlotView
    {
        private const string NoneId = "None";

        private static readonly FieldInfo SyncField = AccessTools.Field(typeof(InventoryManager), "_sync");

        // 薄くしてある席（戻すときの目印）
        private static readonly HashSet<CellComponents> _ghosted = new HashSet<CellComponents>();

        /// <summary>全セルを描き直す（並べ直しの後・設定変更・F6 の直後）。</summary>
        internal static void RefreshAll()
        {
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null || inv.GetWindow() == null) { return; }
            try
            {
                List<CellComponents> cells = inv.GetWindow().GetCellComponentsList();
                InventoryDB db = inv.GetDB();
                if (cells == null) { return; }
                int n = Math.Min(cells.Count, db.cellIDEnd + 1);
                for (int i = 0; i < n; i++)
                {
                    if (cells[i] == null) { continue; }
                    // 本体と同じ手順で素の状態に戻してから上書きする
                    string id = db.ItemID(i);
                    int count = db.ItemCount(i);
                    cells[i].SetImageSprite(ItemParamGetter.GetSprite(id));
                    cells[i].SetText(count <= 1 ? "" : NumberFormatter.Format(count));
                    Decorate(cells[i], i, id, count);
                }
            }
            catch (Exception e)
            {
                PinnedSlotsPlugin.Log.LogError("[view] " + e);
            }
        }

        internal static void Decorate(CellComponents cell, int index, string itemID, int count)
        {
            bool enabled = PinnedSlotsPlugin.Enabled.Value && PinnedSlotsPlugin.ShowCounts.Value;
            string owner = enabled ? SlotArranger.OwnerOf(index) : null;
            bool empty = count <= 0 || itemID == NoneId;
            SlotMouse.Ensure(cell, index, SlotArranger.OwnerOf(index) != null);

            if (owner != null && empty)
            {
                Sprite sprite = ItemParamGetter.GetSprite(owner);
                if (sprite != null) { cell.SetImageSprite(sprite); }
                float a = Mathf.Clamp(PinnedSlotsPlugin.GhostBrightness.Value, 0, 100) / 100f;
                cell.SetImageColor(new Color(1f, 1f, 1f, a));
                cell.SetText("0");
                _ghosted.Add(cell);
                return;
            }

            if (_ghosted.Remove(cell))
            {
                cell.SetImageColor(Color.white);
            }
            // 杖は常に 1 本なので数字を出さない
            if (owner != null && count == 1 && itemID != "Wand")
            {
                cell.SetText("1");
            }
        }

        internal static bool IsPlayerSync(object sync)
        {
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null || SyncField == null) { return false; }
            return object.ReferenceEquals(SyncField.GetValue(inv), sync);
        }
    }

    /// <summary>本体がセルを描いた直後に、席の情報を重ねる。</summary>
    [HarmonyPatch(typeof(InventoryViewSynchronizer), "Sync",
        new Type[] { typeof(CellComponents), typeof(string), typeof(int) })]
    internal static class ViewSyncPatch
    {
        private static void Postfix(InventoryViewSynchronizer __instance, CellComponents cell, string itemID, int count)
        {
            try
            {
                if (cell == null || !SlotView.IsPlayerSync(__instance)) { return; }
                InventoryManager inv = InventoryManager.playerInventory;
                List<CellComponents> cells = inv.GetWindow().GetCellComponentsList();
                int index = (cells == null) ? -1 : cells.IndexOf(cell);
                if (index < 0) { return; }
                SlotView.Decorate(cell, index, itemID, count);
            }
            catch (Exception e)
            {
                PinnedSlotsPlugin.Log.LogError("[view] " + e);
            }
        }
    }
}
