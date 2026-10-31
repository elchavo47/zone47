using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Zone47
{
    /// Looping background video shared by the lobby and the loading screen (one player, keeps running between them,
    /// paused during matches to save battery).
    public static class VideoBg
    {
        static VideoPlayer vp;
        static RenderTexture rt;
        public static bool Available => Z47Refs.I != null && Z47Refs.I.lobbyVideo != null;
        public static bool Ready => vp != null && vp.isPrepared && vp.frame > 0;
        public static Texture Texture => rt;

        public static void Play()
        {
            if (!Available) return;
            if (vp == null)
            {
                var go = new GameObject("BackgroundVideo");
                Object.DontDestroyOnLoad(go);
                var clip = Z47Refs.I.lobbyVideo;
                rt = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0) { name = "BgVideoRT" };
                vp = go.AddComponent<VideoPlayer>();
                vp.playOnAwake = false; vp.isLooping = true; vp.skipOnDrop = true;
                vp.audioOutputMode = VideoAudioOutputMode.None;
                vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt;
                vp.source = VideoSource.VideoClip; vp.clip = clip;
                vp.Prepare();
            }
            vp.Play();
        }

        public static void Pause() { if (vp != null) vp.Pause(); }

        /// Full-screen (cover) video image under `parent`, fading in once the first frame exists.
        public static RawImage AddTo(Transform parent, float dim)
        {
            if (!Available) return null;
            Play();
            var holder = UIK.Rect(parent, "videoBg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            holder.SetAsFirstSibling();
            var box = UIK.Rect(holder, "fit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
            var arf = box.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 16f / 9f;
            var ri = box.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false; ri.texture = rt; ri.color = new Color(1, 1, 1, 0);
            box.gameObject.AddComponent<VideoFadeIn>().Init(ri);
            if (dim > 0f) UIK.Img(holder, "dim", new Color(0, 0, 0, dim), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return ri;
        }
    }

    public class VideoFadeIn : MonoBehaviour
    {
        RawImage ri; float a;
        public void Init(RawImage r) { ri = r; }
        void Update()
        {
            if (ri == null || !VideoBg.Ready) return;
            a = Mathf.MoveTowards(a, 1f, Time.unscaledDeltaTime * 2.5f);
            ri.color = new Color(1, 1, 1, a);
            if (a >= 1f) enabled = false;
        }
    }
}
