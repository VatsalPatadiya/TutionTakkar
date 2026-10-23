using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// All sounds are synthesised at startup, so there are no audio files to license.
    /// Swap any clip for a real recording later (record pens on a desk with your phone).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource[] pool;
        int next;
        AudioSource slide;
        AudioClip[] clacks;
        AudioClip tap, thud, ui, stamp, win, lose, pull;
        float lastClack;
        public bool Muted { get; set; }

        void Awake()
        {
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }

            clacks = new AudioClip[4];
            for (int i = 0; i < clacks.Length; i++) clacks[i] = MakeClack(i);
            tap = MakeDeskTap();
            thud = MakeFloorThud();
            ui = MakePaperTap();
            stamp = MakeStamp();
            win = MakeJingle(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f);
            lose = MakeJingle(new[] { 392f, 293.66f }, 0.2f);
            pull = MakeTick();

            slide = gameObject.AddComponent<AudioSource>();
            slide.clip = MakeSlideLoop();
            slide.loop = true;
            slide.volume = 0f;
            slide.playOnAwake = false;
            slide.Play();
        }

        void Play(AudioClip c, float vol, float pitchJitter = 0.06f)
        {
            if (Muted || c == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.volume = Mathf.Clamp01(vol);
            s.PlayOneShot(c);
        }

        public void Clack(float speed)
        {
            if (Time.unscaledTime - lastClack < 0.04f) return;
            float k = Mathf.Clamp01((speed - 0.12f) / 1.8f);
            if (k <= 0f) return;
            lastClack = Time.unscaledTime;
            Play(clacks[Random.Range(0, clacks.Length)], 0.25f + k * 0.75f, 0.1f);
        }

        public void DeskTap(float speed)
        {
            float k = Mathf.Clamp01((speed - 0.1f) / 1.2f);
            if (k > 0f) Play(tap, 0.15f + k * 0.6f);
        }

        public void FloorThud(float speed) => Play(thud, 0.6f + Mathf.Clamp01(speed / 2.5f) * 0.4f, 0.04f);
        public void Ui() => Play(ui, 0.5f, 0.1f);
        public void Stamp() => Play(stamp, 0.8f, 0.03f);
        public void Win() => Play(win, 0.6f, 0f);
        public void Lose() => Play(lose, 0.55f, 0f);
        public void AimTick(int step) => Play(pull, 0.25f + step * 0.08f, 0f);
        /// <summary>Wall-clock tick in the last seconds of a turn; a little louder at the very end.</summary>
        public void TimerTick(int secondsLeft) => Play(stamp, secondsLeft <= 3 ? 0.55f : 0.35f, 0f);

        /// <summary>Continuous slide noise that follows pen speed (real m/s).</summary>
        public void SetSlide(float speed)
        {
            float target = Muted ? 0f : Mathf.Clamp01(speed / 1.5f) * 0.22f;
            slide.volume = Mathf.MoveTowards(slide.volume, target, Time.unscaledDeltaTime * 2f);
            slide.pitch = 0.8f + Mathf.Clamp01(speed / 1.5f) * 0.5f;
        }

        // ---------- synthesis ----------

        static AudioClip Clip(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.9f;
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>Hard plastic on plastic: three short bright partials plus a body knock.</summary>
        static AudioClip MakeClack(int variant)
        {
            var r = new System.Random(100 + variant);
            int n = (int)(Rate * 0.07f);
            var d = new float[n];
            float j = 0.92f + variant * 0.05f;
            float[] f = { 1750f * j, 3050f * j, 5300f * j };
            float[] g = { 0.5f, 0.34f, 0.2f };
            float[] tau = { 0.012f, 0.008f, 0.005f };
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                for (int k = 0; k < 3; k++) s += Mathf.Sin(2f * Mathf.PI * f[k] * t) * g[k] * Mathf.Exp(-t / tau[k]);
                float nz = Noise(r);
                hp = 0.85f * (hp + nz - prev);
                prev = nz;
                s += hp * 0.35f * Mathf.Exp(-t / 0.003f);
                s += Mathf.Sin(2f * Mathf.PI * 420f * j * t) * 0.25f * Mathf.Exp(-t / 0.015f);
                d[i] = s;
            }
            return Clip("Clack" + variant, d);
        }

        static AudioClip MakeDeskTap()
        {
            var r = new System.Random(3);
            int n = (int)(Rate * 0.09f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise(r) - lp) * 0.2f;
                d[i] = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t / 0.02f) + lp * 0.6f * Mathf.Exp(-t / 0.01f);
            }
            return Clip("DeskTap", d);
        }

        static AudioClip MakeFloorThud()
        {
            var r = new System.Random(5);
            int n = (int)(Rate * 0.4f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise(r) - lp) * 0.08f;
                float s = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t / 0.09f)
                          + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.5f * Mathf.Exp(-t / 0.05f)
                          + lp * 0.8f * Mathf.Exp(-t / 0.03f);
                float t2 = t - 0.065f; // little bounce tick
                if (t2 > 0f) s += Mathf.Sin(2f * Mathf.PI * 1900f * t2) * 0.35f * Mathf.Exp(-t2 / 0.01f);
                d[i] = s;
            }
            return Clip("FloorThud", d);
        }

        static AudioClip MakePaperTap()
        {
            var r = new System.Random(9);
            int n = (int)(Rate * 0.05f);
            var d = new float[n];
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float nz = Noise(r);
                hp = 0.7f * (hp + nz - prev);
                prev = nz;
                d[i] = hp * Mathf.Exp(-t / 0.012f);
            }
            return Clip("PaperTap", d);
        }

        static AudioClip MakeStamp()
        {
            var r = new System.Random(11);
            int n = (int)(Rate * 0.18f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += (Noise(r) - lp) * 0.15f;
                d[i] = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-t / 0.05f) + lp * Mathf.Exp(-t / 0.02f);
            }
            return Clip("Stamp", d);
        }

        static AudioClip MakeJingle(float[] notes, float each)
        {
            int per = (int)(Rate * each);
            var d = new float[per * notes.Length + Rate / 5];
            for (int k = 0; k < notes.Length; k++)
            for (int i = 0; i < per + Rate / 5 && k * per + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float ph = Mathf.Repeat(notes[k] * t, 1f);
                float tri = 4f * Mathf.Abs(ph - 0.5f) - 1f;
                d[k * per + i] += tri * Mathf.Exp(-t / 0.12f) * 0.5f;
            }
            return Clip("Jingle", d);
        }

        static AudioClip MakeTick()
        {
            int n = (int)(Rate * 0.02f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t / 0.004f);
            }
            return Clip("Tick", d);
        }

        static AudioClip MakeSlideLoop()
        {
            var r = new System.Random(13);
            int n = Rate; // 1 s loop
            var d = new float[n];
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float nz = Noise(r);
                lp += (nz - lp) * 0.25f;
                hp = 0.95f * (hp + lp - prev);
                prev = lp;
                d[i] = hp;
            }
            // Cross-fade the ends so the loop has no click.
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float a = (float)i / fade;
                d[i] = d[i] * a + d[n - fade + i] * (1f - a);
            }
            return Clip("Slide", d);
        }
    }
}
