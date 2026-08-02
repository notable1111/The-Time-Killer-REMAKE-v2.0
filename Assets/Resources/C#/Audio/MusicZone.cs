// A music layer trigger you can see and drag in the scene view.
//
// The Mystery layer shipped on 2026-07-23 and had **never once fired in real
// play** by 2026-07-28, because its only entry was a demo Rect at (0,0,3,3) that
// the config itself labelled "MOVE/replace me in the editor". Nobody did, because
// hand-placing a region by typing four numbers into an array is miserable.
//
// It also could not have worked properly: `AudioConfig.mysteryZones` lives in ONE
// asset shared by CastleWingLDtk and Catacombs, so any rect authored for one map
// is live in the other too, at whatever happens to be at those coordinates.
//
// So zones live in the SCENE now. Drag them, see them, and they belong to the map
// they were placed in. The config rects still work and are still read — this is
// additive, and nothing that already exists had to be rewired.
using UnityEngine;

namespace TimeKiller.Audio
{
    public class MusicZone : MonoBehaviour
    {
        public enum Kind { Mystery, Safe }

        [Tooltip("Mystery = the 'about to enter / act' approach dread. Safe = sanctuary, quieter than dread.")]
        public Kind kind = Kind.Mystery;
        [Tooltip("Size in world units, centred on this object's position.")]
        public Vector2 size = new Vector2(10f, 8f);

        /// World-space rect, centred on the transform. Recomputed on read rather
        /// than cached so dragging the object in Play Mode takes effect live.
        public Rect Area => new Rect(
            transform.position.x - size.x * 0.5f,
            transform.position.y - size.y * 0.5f,
            size.x, size.y);

        public bool Contains(Vector2 point) => Area.Contains(point);

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var area = Area;
            // Mystery reads as the approach dread, Safe as sanctuary. Filled at low
            // alpha so overlapping zones are still legible.
            Gizmos.color = kind == Kind.Mystery
                ? new Color(0.75f, 0.35f, 0.9f, 0.18f)
                : new Color(0.3f, 0.85f, 0.5f, 0.18f);
            Gizmos.DrawCube(area.center, new Vector3(area.width, area.height, 0.1f));
            Gizmos.color = kind == Kind.Mystery
                ? new Color(0.8f, 0.4f, 1f, 0.9f)
                : new Color(0.35f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(area.center, new Vector3(area.width, area.height, 0.1f));
        }
#endif
    }
}
