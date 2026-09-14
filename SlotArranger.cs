// 所持品欄の並べ直し。ゲームの状態には InventoryDB.SetCellItem でしか触らない。
//
// 用語
//   席（home）   : cfg で品目に割り当てたセル。その品目以外は入れない（予約席）
//   自由席       : どの品目にも割り当てていないセル。本体どおり先頭から詰まる
//
// 手順（変化が無くなるまで、上限つきで繰り返す）
//   セル c に品目 X が居るとき
//     1. X に席 h があり h != c なら、X を h へ
//          h が空        → 移す
//          h も X        → 合流（容量を超える分は c に残す）
//          h が別の品目 Z → 入れ替え。Z は次の周で自分の席か自由席へ向かう
//     2. X に席が無く、c が誰かの予約席なら、空いている自由席へ移す。無ければそのまま
//
// 終了する理由: 1 は「席に着いた品目」を必ず 1 つ増やし、席に着いた品目は二度と動かない。
// 2 は「予約席に居る自由な品目」を 1 つ減らし、増やさない。念のため回数にも上限を置く。
//
// 選択中のセルは追いかけない。キー＝席が固定されるのがこの Mod の目的なので、
// 中身が入れ替わったら本体（EquipActionHandler.SubscribeCellID）が手持ちを引き直す。

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Items.Inventory;
using R3;

namespace LwfPinnedSlots
{
    internal static class SlotArranger
    {
        private const string NoneId = "None";

        // 本体がセルの跳ねる演出に使う口（InventoryWindow.SubscribeAnimation が購読）。
        // 席へ動かした先でも跳ねさせたいので、同じ口を叩く
        private static readonly FieldInfo CellValueChangedField =
            AccessTools.Field(typeof(InventoryDepositor), "_onCellValueChanged");

        private static bool _busy;

        /// <summary>セル index を予約している品目。無ければ null。</summary>
        internal static string OwnerOf(int index)
        {
            if (index < 0 || index >= PinnedSlotsPlugin.SlotCount || PinnedSlotsPlugin.Slots == null) { return null; }
            for (int i = 0; i <= index; i++)
            {
                string id = PinnedSlotsPlugin.Slots[i].Value;
                if (id == null) { continue; }
                id = id.Trim();
                if (id.Length == 0 || id == NoneId) { continue; }
                // ReadPins と同じ規則: 同じ品目は若い番号が勝つので、手前で既に出ていれば無効
                bool dup = false;
                for (int j = 0; j < i; j++)
                {
                    string prev = PinnedSlotsPlugin.Slots[j].Value;
                    if (prev != null && prev.Trim() == id) { dup = true; break; }
                }
                if (i == index) { return dup ? null : id; }
            }
            return null;
        }

        internal static void ArrangePlayerInventory()
        {
            if (PinnedSlotsPlugin.Enabled == null || !PinnedSlotsPlugin.Enabled.Value) { return; }
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null) { return; }
            Arrange(inv);
        }

        internal static void Arrange(InventoryManager inv)
        {
            if (_busy) { return; }
            _busy = true;
            try
            {
                ArrangeCore(inv.GetDB(), inv.GetDepositor());
                SlotView.RefreshAll();
            }
            catch (Exception e)
            {
                PinnedSlotsPlugin.Log.LogError("[arrange] " + e);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void ArrangeCore(InventoryDB db, InventoryDepositor depositor)
        {
            int cellCount = db.cellIDEnd + 1;
            string[] owner = new string[cellCount];                        // セル → 予約している品目（無ければ null）
            Dictionary<string, int> home = new Dictionary<string, int>();  // 品目 → 席
            ReadPins(cellCount, owner, home);
            if (home.Count == 0) { return; }

            int capacity = db.GetCellCapacity;
            int maxPass = cellCount * 2 + 2;
            for (int pass = 0; pass < maxPass; pass++)
            {
                bool changed = false;
                for (int c = 0; c < cellCount; c++)
                {
                    string id = db.ItemID(c);
                    int count = db.ItemCount(c);
                    if (count <= 0 || id == NoneId) { continue; }

                    int h;
                    if (home.TryGetValue(id, out h))
                    {
                        if (h == c) { continue; }
                        if (MoveHome(db, depositor, c, h, id, count, capacity)) { changed = true; }
                    }
                    else if (owner[c] != null)
                    {
                        int free = FindFreeCell(db, owner, cellCount);
                        if (free >= 0)
                        {
                            Move(db, depositor, c, free, id, count);
                            changed = true;
                        }
                    }
                }
                if (!changed) { break; }
            }
        }

        private static void ReadPins(int cellCount, string[] owner, Dictionary<string, int> home)
        {
            int n = Math.Min(PinnedSlotsPlugin.SlotCount, cellCount);
            for (int i = 0; i < n; i++)
            {
                string id = PinnedSlotsPlugin.Slots[i].Value;
                if (id == null) { continue; }
                id = id.Trim();
                if (id.Length == 0 || id == NoneId) { continue; }
                // 同じ品目を 2 つの席に書いたら、若い番号を採る
                if (home.ContainsKey(id)) { continue; }
                home[id] = i;
                owner[i] = id;
            }
        }

        private static int FindFreeCell(InventoryDB db, string[] owner, int cellCount)
        {
            for (int i = 0; i < cellCount; i++)
            {
                if (owner[i] != null) { continue; }
                if (db.ItemCount(i) > 0 && db.ItemID(i) != NoneId) { continue; }
                return i;
            }
            return -1;
        }

        /// <summary>セル from の品目 id（count 個）を席 to へ。動かせたら true。</summary>
        private static bool MoveHome(InventoryDB db, InventoryDepositor depositor,
            int from, int to, string id, int count, int capacity)
        {
            string toId = db.ItemID(to);
            int toCount = db.ItemCount(to);

            if (toCount <= 0 || toId == NoneId)
            {
                Move(db, depositor, from, to, id, count);
                return true;
            }
            if (toId == id)
            {
                // 合流。容量（既定は int.MaxValue）を超える分は元のセルに残す
                long space = (long)capacity - toCount;
                if (space <= 0) { return false; }
                int moved = (int)Math.Min(space, (long)count);
                int rest = count - moved;
                db.SetCellItem(to, id, toCount + moved);
                db.SetCellItem(from, rest > 0 ? id : NoneId, rest);
                Notify(depositor, to);
                PinnedSlotsPlugin.Log.LogDebug("[arrange] merge " + id + " x" + moved + " cell " + from + " -> " + to);
                return true;
            }
            // 席に別の品目が居る。入れ替えて、相手は次の周に任せる
            db.SetCellItem(to, id, count);
            db.SetCellItem(from, toId, toCount);
            Notify(depositor, to);
            PinnedSlotsPlugin.Log.LogDebug("[arrange] swap " + id + " cell " + from + " <-> " + toId + " cell " + to);
            return true;
        }

        private static void Move(InventoryDB db, InventoryDepositor depositor, int from, int to, string id, int count)
        {
            db.SetCellItem(to, id, count);
            db.SetCellItem(from, NoneId, 0);
            Notify(depositor, to);
            PinnedSlotsPlugin.Log.LogDebug("[arrange] move " + id + " x" + count + " cell " + from + " -> " + to);
        }

        private static void Notify(InventoryDepositor depositor, int cell)
        {
            if (CellValueChangedField == null) { return; }
            ReactiveProperty<int> rp = CellValueChangedField.GetValue(depositor) as ReactiveProperty<int>;
            if (rp != null) { rp.OnNext(cell); }
        }
    }
}
