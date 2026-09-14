// マウスでの席の操作と、固定中の印。
//
//   右クリック           : その席の固定／解除（SlotEdit.TogglePin）
//   ドラッグ＆ドロップ   : 落とした席と入れ替え（SlotEdit.SwapSeats）。中身と予約が一緒に動く
//   印                   : 予約席の数字の帯（下段）の左端にチェック印を出す
//
// セルの Button が付いている GameObject に SlotCellHandle（MonoBehaviour）を足して、
// Unity UI のポインタイベント（IPointerClickHandler / IDrag… / IDropHandler）で受ける。
// 同じ GameObject の Button は左クリックしか拾わないので、本体の「セルを選ぶ」はそのまま生きる。
// 本体のキー設定には依存しない。
//
// ドラッグ中は絵の写しをカーソルに付けて出す（raycastTarget を切って、落とし先の判定を邪魔しない）。
//
// F6（ScriptEngine）で読み直すと古い型の部品が残るので、OnDestroy で全部外す。

using System.Collections.Generic;
using Items;
using TMPro;
using Items.Inventory;
using UI.SelectableWindow.Cell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LwfPinnedSlots
{
    internal static class SlotMouse
    {
        private const string MarkerName = "LwfPinnedSlots.Marker";
        // 印に使う本体のスプライト。InGame シーンのチェックボックスが使っているので、
        // ゲーム中はメモリに載っている（Resources.FindObjectsOfTypeAll で拾える）
        private const string MarkerSpriteName = "check_small_true";
        private static Sprite _markerSprite;

        private static Sprite FindMarkerSprite()
        {
            if (_markerSprite != null) { return _markerSprite; }
            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == MarkerSpriteName) { _markerSprite = all[i]; break; }
            }
            if (_markerSprite == null) { PinnedSlotsPlugin.Log.LogWarning("[view] sprite not found: " + MarkerSpriteName); }
            return _markerSprite;
        }

        /// <summary>セルに部品と印を付ける（何度呼んでも 1 つ）。印の表示を pinned に合わせる。</summary>
        internal static void Ensure(CellComponents cell, int index, bool pinned)
        {
            GameObject host = HostOf(cell);
            if (host == null) { return; }
            SlotCellHandle handle = host.GetComponent<SlotCellHandle>();
            if (handle == null) { handle = host.AddComponent<SlotCellHandle>(); }
            handle.Index = index;
            handle.Cell = cell;

            Transform parent = MarkerParent(cell);
            if (parent == null) { return; }
            Transform t = parent.Find(MarkerName);
            Image marker;
            if (t == null)
            {
                GameObject go = new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                marker = go.GetComponent<Image>();
                marker.raycastTarget = false;
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                marker = t.GetComponent<Image>();
            }
            // 数字（CountText）は下の帯（高さ 16・左右 4 の余白・右寄せ）。印はその帯の左端に置く。
            // 帯の高さ位置は数字の RectTransform から引くので、プレハブが変わっても追従する
            float size = Mathf.Max(2, PinnedSlotsPlugin.MarkerSize.Value);
            float bandY = 8.9f;
            if (cell.GetText() != null) { bandY = cell.GetText().rectTransform.anchoredPosition.y; }
            // 数字は 1000 以上で "12.3K" のように 6 文字まで伸びる（NumberFormatter）。
            // 帯の残り幅に合わせて印を縮め、6px を切るなら隠す
            TextMeshProUGUI txt = cell.GetText();
            float bandW = 36f;
            float textW = 0f;
            if (txt != null)
            {
                float w = txt.rectTransform.rect.width;
                if (w > 0f) { bandW = w; }
                if (!string.IsNullOrEmpty(txt.text)) { textW = txt.preferredWidth; }
            }
            float avail = bandW - textW - 2f;
            if (avail < size) { size = avail; }
            bool fits = size >= 6f;
            RectTransform mrt = marker.rectTransform;
            mrt.sizeDelta = new Vector2(size, size);
            mrt.anchoredPosition = new Vector2(4f + size * 0.5f, bandY);
            Sprite sprite = FindMarkerSprite();
            marker.sprite = sprite;             // 無ければ白い四角（点）のまま
            marker.preserveAspect = sprite != null;
            marker.color = MarkerColor();
            marker.gameObject.SetActive(pinned && fits && PinnedSlotsPlugin.Enabled.Value);
        }

        internal static void DetachAll()
        {
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null || inv.GetWindow() == null) { return; }
            List<CellComponents> cells = inv.GetWindow().GetCellComponentsList();
            if (cells == null) { return; }
            foreach (CellComponents cell in cells)
            {
                if (cell == null) { continue; }
                GameObject host = HostOf(cell);
                if (host != null)
                {
                    SlotCellHandle h = host.GetComponent<SlotCellHandle>();
                    if (h != null) { Object.Destroy(h); }
                }
                Transform parent = MarkerParent(cell);
                if (parent != null)
                {
                    Transform t = parent.Find(MarkerName);
                    if (t != null) { Object.Destroy(t.gameObject); }
                }
            }
            SlotCellHandle.DestroyGhost();
        }

        // 印の親は数字と同じ階層（InventoryCell）。数字が無ければ絵の親
        private static Transform MarkerParent(CellComponents cell)
        {
            if (cell.GetText() != null) { return cell.GetText().transform.parent; }
            if (cell.GetImage() != null) { return cell.GetImage().transform.parent; }
            return null;
        }

        private static GameObject HostOf(CellComponents cell)
        {
            Button b = cell.GetButton();
            if (b != null) { return b.gameObject; }
            return cell.GetGameObject();
        }

        private static Color MarkerColor()
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(PinnedSlotsPlugin.MarkerColor.Value, out c)) { return c; }
            return Color.white;
        }
    }

    /// <summary>セル 1 つ分のポインタ処理。</summary>
    internal sealed class SlotCellHandle : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private const string GhostName = "LwfPinnedSlots.Ghost";
        private static GameObject _ghost;

        internal int Index;
        internal CellComponents Cell;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Right) { return; }
            if (!PinnedSlotsPlugin.Enabled.Value) { return; }
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null) { return; }
            SlotEdit.TogglePin(inv, Index);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !PinnedSlotsPlugin.Enabled.Value) { return; }
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null) { return; }
            InventoryDB db = inv.GetDB();
            string id = db.ItemID(Index);
            string owner = SlotArranger.OwnerOf(Index);
            // 何も無く予約も無い席は掴めない
            if ((db.ItemCount(Index) <= 0 || id == "None") && owner == null) { return; }
            Sprite sprite = ItemParamGetter.GetSprite(db.ItemCount(Index) > 0 && id != "None" ? id : owner);
            MakeGhost(sprite, e);
        }

        public void OnDrag(PointerEventData e)
        {
            MoveGhost(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            DestroyGhost();
        }

        public void OnDrop(PointerEventData e)
        {
            if (!PinnedSlotsPlugin.Enabled.Value || e.pointerDrag == null) { return; }
            SlotCellHandle src = e.pointerDrag.GetComponent<SlotCellHandle>();
            if (src == null || src == this || src.Index == Index) { return; }
            InventoryManager inv = InventoryManager.playerInventory;
            if (inv == null) { return; }
            SlotEdit.SwapSeats(inv, src.Index, Index);
        }

        private void MakeGhost(Sprite sprite, PointerEventData e)
        {
            DestroyGhost();
            Image icon = Cell != null ? Cell.GetImage() : null;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (icon == null || canvas == null) { return; }
            canvas = canvas.rootCanvas;

            _ghost = new GameObject(GhostName, typeof(RectTransform), typeof(Image));
            _ghost.transform.SetParent(canvas.transform, false);
            _ghost.transform.SetAsLastSibling();
            Image img = _ghost.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.8f);
            RectTransform rt = _ghost.GetComponent<RectTransform>();
            rt.sizeDelta = icon.rectTransform.rect.size;
            MoveGhost(e);
        }

        private static void MoveGhost(PointerEventData e)
        {
            if (_ghost == null) { return; }
            RectTransform rt = _ghost.GetComponent<RectTransform>();
            Canvas canvas = _ghost.GetComponentInParent<Canvas>();
            if (canvas == null) { return; }
            Vector3 world;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.transform as RectTransform, e.position, cam, out world))
            {
                rt.position = world;
            }
        }

        internal static void DestroyGhost()
        {
            if (_ghost != null) { Object.Destroy(_ghost); _ghost = null; }
        }

        private void OnDisable()
        {
            DestroyGhost();
        }
    }
}
