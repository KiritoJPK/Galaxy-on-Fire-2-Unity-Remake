// DragScroll.cs
// Drag-to-scroll for ScrollViews with the mouse or a pen, with momentum after release, like the original's inertial
// list scrolling (MenuTouchWindow: damping 0.9 per frame). Scrolling only starts after a small threshold, so clicks on
// buttons inside the list still count. Fingers are left to the ScrollView's own touch scrolling (two handlers moving
// the same list fought after the release: the list jumped back).
// PointerActive: a pointer pressed, moved or wheeled over a list in this or the last frame. Focus changes then come
// from the pointer (a tap, the hover focus), and the menus don't scroll the focused row into view for them
// (ScrollTo against the layout of a list that is moving snapped it back).
// The capture only starts past the threshold, so a release outside the list before that never reaches OnUp: a move with
// no button held (or the list losing the capture or its panel) ends the press, else the next hover dragged the list.
// A press on the list's own scroll bar (its Scroller: the thumb, the track, the arrows) is the bar's: dragging the thumb
// down was taken as a drag of the content and moved the list the other way (players' report: "scrolls in the wrong
// direction").

using UnityEngine;
using UnityEngine.UIElements;

namespace GoF2Remake.UI
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class DragScroll : PointerManipulator
    {
        const float StartThreshold = 12f;     // panel units before a press becomes a drag
        const float Damping = 0.9f;           // velocity kept per 60 Hz frame after release

        readonly ScrollView scroll;
        int pointerId = -1;
        Vector2 startPos, lastPos;
        float startOffset, velocity;
        bool dragging;
        IVisualElementScheduledItem inertia;

        static int lastPointerFrame = -10;

        /// <summary>A pointer acted on a list this frame or the last: the focus follows the pointer, don't ScrollTo.</summary>
        public static bool PointerActive => Time.frameCount - lastPointerFrame <= 1;

        /// <summary>A pointer event outside a DragScroll list (a menu's root) counts too.</summary>
        public static void NotePointer() => lastPointerFrame = Time.frameCount;

        public DragScroll(ScrollView scrollView)
        {
            scroll = scrollView;
            target = scrollView;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(OnCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            target.RegisterCallback<WheelEvent>(_ => { lastPointerFrame = Time.frameCount; inertia?.Pause(); }, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(OnCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>A horizontal ScrollView (the main menu's mod option cards) scrolls along x, the rest along y.</summary>
        bool Sideways => scroll.mode == ScrollViewMode.Horizontal;
        float Along(Vector2 v) => Sideways ? v.x : v.y;

        float MaxOffset => Sideways ? Mathf.Max(0f, scroll.contentContainer.layout.width - scroll.contentViewport.layout.width)
                                    : Mathf.Max(0f, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height);

        void OnDown(PointerDownEvent e)
        {
            lastPointerFrame = Time.frameCount;
            if (e.pointerType == UnityEngine.UIElements.PointerType.touch || e.button != 0) return;
            if (OnScroller(e.target as VisualElement)) return;   // the scroll bar drags itself, the right way round
            if (pointerId >= 0 && pointerId != e.pointerId) return;   // another pointer's press is running
            inertia?.Pause();
            pointerId = e.pointerId;
            startPos = lastPos = e.position;
            startOffset = Along(scroll.scrollOffset);
            velocity = 0f;
            dragging = false;
        }

        /// <summary>'v' is (inside) one of the list's scroll bars.</summary>
        bool OnScroller(VisualElement v)
        {
            for (; v != null && v != scroll; v = v.parent)
                if (v is Scroller) return true;
            return false;
        }

        void OnMove(PointerMoveEvent e)
        {
            lastPointerFrame = Time.frameCount;
            if (e.pointerId != pointerId) return;
            if (e.pressedButtons == 0) { EndPress(); return; }   // released where the list didn't hear it
            Vector2 p = e.position;
            if (!dragging)
            {
                if (Mathf.Abs(Along(p) - Along(startPos)) < StartThreshold) return;
                dragging = true;
                target.CapturePointer(pointerId);   // from here on the list owns the gesture
            }
            float dy = Along(p) - Along(lastPos);
            velocity = Mathf.Lerp(velocity, -dy, 0.5f);
            lastPos = p;
            SetOffset(startOffset - (Along(p) - Along(startPos)));
            e.StopImmediatePropagation();   // also keeps the ScrollView's own touch handling from doubling it
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != pointerId) return;
            if (dragging)
            {
                target.ReleasePointer(pointerId);
                e.StopImmediatePropagation();   // a drag is not a click
                StartInertia();
            }
            pointerId = -1;
            dragging = false;
        }

        void OnCancel(PointerCancelEvent e)
        {
            if (e.pointerId == pointerId) EndPress();
        }

        void OnCaptureOut(PointerCaptureOutEvent e)
        {
            if (e.pointerId == pointerId && dragging) EndPress();
        }

        void OnDetach(DetachFromPanelEvent e)
        {
            inertia?.Pause();
            EndPress();
        }

        void EndPress()
        {
            if (pointerId >= 0 && target.HasPointerCapture(pointerId)) target.ReleasePointer(pointerId);
            pointerId = -1;
            dragging = false;
        }

        void StartInertia()
        {
            inertia?.Pause();
            inertia = scroll.schedule.Execute(t =>
            {
                float frames = t.deltaTime / 16.67f;
                velocity *= Mathf.Pow(Damping, frames);
                SetOffset(Along(scroll.scrollOffset) + velocity * frames);
                if (Mathf.Abs(velocity) < 0.2f) inertia.Pause();
            }).Every(16);
        }

        void SetOffset(float v) => scroll.scrollOffset = Sideways ? new Vector2(Mathf.Clamp(v, 0f, MaxOffset), scroll.scrollOffset.y)
                                                             : new Vector2(scroll.scrollOffset.x, Mathf.Clamp(v, 0f, MaxOffset));
    }
}
