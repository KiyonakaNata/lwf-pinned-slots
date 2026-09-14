// 席の書き換え（固定／解除・入れ替え）。cfg を書き換えるだけで、並べ直しと描き直しは既存の道に流す。
//
// 入れ替えの手順: DB の 2 セルを入れ替え → cfg の 2 席を入れ替え → 並べ直し。
// cfg は 1 項目ずつしか書けず、途中の状態では同じ品目が 2 席に居る瞬間があるので、
// その間は SettingChanged からの並べ直しを止める（PinnedSlotsPlugin.SuppressArrange）。

using Items.Inventory;

namespace LwfPinnedSlots
{
    internal static class SlotEdit
    {
        private const string NoneId = "None";

        /// <summary>セル from と to の中身と予約を入れ替える。</summary>
        internal static void SwapSeats(InventoryManager inv, int from, int to)
        {
            InventoryDB db = inv.GetDB();
            if (from == to || from < 0 || to < 0 || from > db.cellIDEnd || to > db.cellIDEnd) { return; }
            string idFrom = db.ItemID(from);
            int cntFrom = db.ItemCount(from);
            string idTo = db.ItemID(to);
            int cntTo = db.ItemCount(to);
            string ownerFrom = SlotValue(from);
            string ownerTo = SlotValue(to);

            PinnedSlotsPlugin.SuppressArrange = true;
            try
            {
                db.SetCellItem(to, idFrom, cntFrom);
                db.SetCellItem(from, idTo, cntTo);
                SetSlot(from, ownerTo);
                SetSlot(to, ownerFrom);
            }
            finally
            {
                PinnedSlotsPlugin.SuppressArrange = false;
            }
            SlotArranger.Arrange(inv);
            SlotView.RefreshAll();
            PinnedSlotsPlugin.Log.LogInfo("[edit] swap cell " + from + " <-> " + to
                + " (" + Show(idFrom, ownerFrom) + " <-> " + Show(idTo, ownerTo) + ")");
        }

        /// <summary>
        /// 選択した席の固定を切り替える。予約済みなら解除。品目が居る自由席ならその品目をここに固定
        /// （他の席に同じ品目の予約があれば外す）。空の自由席なら何もしない。
        /// </summary>
        internal static void TogglePin(InventoryManager inv, int cell)
        {
            InventoryDB db = inv.GetDB();
            if (cell < 0 || cell > db.cellIDEnd) { return; }
            string id = db.ItemID(cell);
            int cnt = db.ItemCount(cell);
            string owner = SlotArranger.OwnerOf(cell);

            PinnedSlotsPlugin.SuppressArrange = true;
            try
            {
                if (owner != null)
                {
                    SetSlot(cell, "");
                    PinnedSlotsPlugin.Log.LogInfo("[edit] unpin slot " + (cell + 1) + " (" + owner + ")");
                }
                else if (cnt > 0 && id != NoneId)
                {
                    for (int i = 0; i < PinnedSlotsPlugin.SlotCount; i++)
                    {
                        if (i != cell && SlotValue(i) == id) { SetSlot(i, ""); }
                    }
                    SetSlot(cell, id);
                    PinnedSlotsPlugin.Log.LogInfo("[edit] pin slot " + (cell + 1) + " = " + id);
                }
                else
                {
                    return;
                }
            }
            finally
            {
                PinnedSlotsPlugin.SuppressArrange = false;
            }
            SlotArranger.Arrange(inv);
            SlotView.RefreshAll();
        }

        private static string SlotValue(int i)
        {
            if (i < 0 || i >= PinnedSlotsPlugin.SlotCount) { return ""; }
            string v = PinnedSlotsPlugin.Slots[i].Value;
            return v == null ? "" : v.Trim();
        }

        private static void SetSlot(int i, string value)
        {
            if (i < 0 || i >= PinnedSlotsPlugin.SlotCount) { return; }
            if (PinnedSlotsPlugin.Slots[i].Value != value) { PinnedSlotsPlugin.Slots[i].Value = value; }
        }

        private static string Show(string id, string owner)
        {
            return id + (owner.Length > 0 ? "@" + owner : "");
        }
    }
}
