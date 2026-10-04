using System.Collections;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Short-lived visual effects (the pack's dust, explosions, fire and heal light, punches, shock rings, arrows and
    /// arrow rain, hit bursts, sparkles, afterimages) and the markers that stay until removed (warning tiles, aiming
    /// highlights and target brackets). Short-lived ones clean themselves up. Frame effects play at their own size:
    /// pixel art is never scaled by fractions.
    /// </summary>
    public static class Effects
    {
        const int EffectOrder = 9500;
        const int GroundEffectOrder = 28; // On the ground: over tiles and markers, under shadows and actors.
        const int WarningOrder = 5;       // Above the ground, below stairs, items and actors.
        const int HighlightOrder = 6;

        /// <summary>Plays one of the art manifest's effect strips once at a position and removes it.</summary>
        public static SpriteRenderer Play(Transform parent, string strip, Vector3 position, Color? tint = null, bool onGround = false,
            float speed = 1f, bool flipX = false)
        {
            var anim = SpriteLibrary.Strip(strip);
            var go = new GameObject(strip);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = onGround ? GroundEffectOrder : EffectOrder;
            renderer.color = tint ?? Color.white;
            renderer.flipX = flipX;
            go.AddComponent<Flipbook>().Play(anim.Frames, 1f / (anim.Fps * speed));
            return renderer;
        }

        /// <summary>A gauntlet blow's impact: a star that bursts over the target tile.</summary>
        public static void Punch(Transform parent, Vector3 position, Color tint) =>
            Play(parent, "Effects/punch", position, tint).transform.rotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));

        /// <summary>A ring racing outward over the ground: a slam, a shove, a blow that goes through.</summary>
        public static void Ring(Transform parent, Vector3 position, Color tint) =>
            Play(parent, "Effects/ring", position + Vector3.down * 0.3f, tint, onGround: true);

        /// <summary>The pack's heal light rising around whoever is healed.</summary>
        public static void Heal(Transform parent, Vector3 position, Color? tint = null) => Play(parent, "Effects/heal", position, tint);

        /// <summary>A puff of dust at someone's feet (a dash, a roll, a landing).</summary>
        public static void Dust(Transform parent, Vector3 position, bool flipX = false) =>
            Play(parent, "Effects/dust", position + Vector3.down * 0.25f, flipX: flipX);

        /// <summary>Fire bursting on a target (a smite).</summary>
        public static void Explosion(Transform parent, Vector3 position) => Play(parent, "Effects/explosion", position, speed: 1.2f);

        /// <summary>Arrows raining down onto a tile (Volley); returns when the last one lands.</summary>
        public static IEnumerator RainArrows(Transform parent, Vector3 tileCenter, Color tint, int count, float duration)
        {
            var sprite = SpriteLibrary.Still("Effects/arrow");
            var arrows = new Transform[count];
            var starts = new Vector3[count];
            var delays = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("RainArrow");
                go.transform.SetParent(parent, false);
                go.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // Art points right; these fall straight down.
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = EffectOrder;
                renderer.color = tint;
                var offset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f), 0f);
                starts[i] = tileCenter + offset;
                delays[i] = duration * 0.5f * i / Mathf.Max(1, count - 1);
                go.transform.position = starts[i] + Vector3.up * 4f;
                arrows[i] = go.transform;
            }
            float fall = duration * 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    float k = Mathf.Clamp01((t - delays[i]) / fall);
                    arrows[i].position = starts[i] + Vector3.up * (4f * (1f - k));
                }
                yield return null;
            }
            foreach (var arrow in arrows) Object.Destroy(arrow.gameObject);
        }

        /// <summary>A translucent square over a tile (the aiming highlight's reach and area). Destroy it when aiming ends.</summary>
        public static GameObject TileHighlight(Transform parent, Vector3 tileCenter, Color color)
        {
            var go = new GameObject("Highlight");
            go.transform.SetParent(parent, false);
            go.transform.position = tileCenter;
            go.transform.localScale = new Vector3(1f - 4f * SpriteLibrary.Pixel, 1f - 4f * SpriteLibrary.Pixel, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.sortingOrder = HighlightOrder;
            renderer.color = color;
            return go;
        }

        /// <summary>The pack's target brackets around a tile; the chosen one pulses. Destroy it when aiming ends.</summary>
        public static GameObject Reticle(Transform parent, Vector3 tileCenter, Color color, bool pulse)
        {
            var go = new GameObject("Reticle");
            go.transform.SetParent(parent, false);
            go.transform.position = tileCenter;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Still("Effects/reticle");
            renderer.sortingOrder = EffectOrder;
            renderer.color = color;
            if (pulse) go.AddComponent<Pulse>().Init(color, 0.55f, 1f, 8f);
            return go;
        }

        /// <summary>
        /// An arrow flying from one point to another, pointing the way it flies (any angle); returns when it lands. A
        /// heavy one leaves a fading trail of itself.
        /// </summary>
        public static IEnumerator Arrow(Transform parent, Vector3 from, Vector3 to, Color tint, float duration, bool trail = false)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var flight = to - from;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flight.y, flight.x) * Mathf.Rad2Deg); // The art points right.
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Still("Effects/arrow");
            renderer.sortingOrder = EffectOrder;
            renderer.color = tint;
            float nextGhost = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                go.transform.position = Vector3.Lerp(from, to, t / duration);
                if (trail && t >= nextGhost)
                {
                    Afterimage(parent, renderer, new Color(tint.r, tint.g, tint.b, 0.5f));
                    nextGhost += 0.02f;
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>Square pixel chips flying out and falling, for hits and defeats.</summary>
        public static void Burst(Transform parent, Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(speed * 0.4f, speed)
                               + Vector3.up * (speed * 0.4f);
                Spawn(parent, position, color, velocity, Random.Range(0.35f, 0.6f), gravity: 7f);
            }
        }

        /// <summary>A ring of chips racing outward along the ground, for shockwaves like the boss's slam.</summary>
        public static void Shockwave(Transform parent, Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f, 0f) * speed;
                Spawn(parent, position + Vector3.down * 0.35f, color, velocity, 0.45f, gravity: 0f);
            }
        }

        /// <summary>A pulsing red square marking a tile that is about to be hit. Destroy it when the hit lands.</summary>
        public static GameObject WarningTile(Transform parent, Vector3 tileCenter)
        {
            var go = new GameObject("Warning");
            go.transform.SetParent(parent, false);
            go.transform.position = tileCenter;
            go.transform.localScale = new Vector3(1f - 4f * SpriteLibrary.Pixel, 1f - 4f * SpriteLibrary.Pixel, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.sortingOrder = WarningOrder;
            go.AddComponent<Pulse>().Init(new Color(1f, 0.2f, 0.15f), 0.2f, 0.46f, 9f);
            return go;
        }

        /// <summary>Gentle rising sparkles, for level-ups and charged moments.</summary>
        public static void Sparkle(Transform parent, Vector3 position, Color color)
        {
            for (int i = 0; i < 10; i++)
            {
                var start = position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.4f, 0.1f), 0f);
                var velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.6f), 0f);
                Spawn(parent, start, color, velocity, Random.Range(0.5f, 0.8f), gravity: 0f);
            }
        }

        /// <summary>A still, tinted copy of a sprite that fades out where it stands, just behind the original.</summary>
        public static void Afterimage(Transform parent, SpriteRenderer source, Color tint)
        {
            var go = new GameObject("Afterimage");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.color = tint;
            renderer.sortingOrder = source.sortingOrder - 1;
            go.AddComponent<Particle>().Init(Vector3.zero, 0.22f, 0f);
        }

        static void Spawn(Transform parent, Vector3 position, Color color, Vector3 velocity, float life, float gravity)
        {
            var go = new GameObject("Particle");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            float size = Random.Range(2, 4) * 2f * SpriteLibrary.Pixel; // Whole art pixels: 4 or 6 across.
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.color = color;
            renderer.sortingOrder = EffectOrder;
            go.AddComponent<Particle>().Init(velocity, life, gravity);
        }
    }

    /// <summary>Plays frames once and removes the object.</summary>
    sealed class Flipbook : MonoBehaviour
    {
        Sprite[] frames;
        float frameTime;
        float time;
        SpriteRenderer spriteRenderer;

        public void Play(Sprite[] sequence, float secondsPerFrame)
        {
            frames = sequence;
            frameTime = secondsPerFrame;
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames[0];
        }

        void Update()
        {
            time += Time.deltaTime;
            int frame = (int)(time / frameTime);
            if (frame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[frame];
        }
    }

    /// <summary>Plays an animation over and over (the cave's eyes, swaying bushes).</summary>
    sealed class LoopingSprite : MonoBehaviour
    {
        SpriteAnim anim;
        float time;
        SpriteRenderer spriteRenderer;

        public void Play(SpriteAnim loop, float startAt = 0f)
        {
            anim = loop;
            time = startAt;
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = anim.Frames[0];
        }

        void Update()
        {
            time += Time.deltaTime;
            spriteRenderer.sprite = anim.Frames[(int)(time * anim.Fps) % anim.Frames.Length];
        }
    }

    sealed class Pulse : MonoBehaviour
    {
        Color color;
        float minAlpha;
        float maxAlpha;
        float speed;
        SpriteRenderer spriteRenderer;

        public void Init(Color baseColor, float low, float high, float frequency)
        {
            color = baseColor;
            minAlpha = low;
            maxAlpha = high;
            speed = frequency;
            spriteRenderer = GetComponent<SpriteRenderer>();
            Update();
        }

        void Update()
        {
            color.a = Mathf.Lerp(minAlpha, maxAlpha, 0.5f + 0.5f * Mathf.Sin(Time.time * speed));
            spriteRenderer.color = color;
        }
    }

    sealed class Particle : MonoBehaviour
    {
        Vector3 velocity;
        float life;
        float age;
        float gravity;
        SpriteRenderer spriteRenderer;
        Color color;
        float startAlpha;

        public void Init(Vector3 startVelocity, float lifetime, float fall)
        {
            velocity = startVelocity;
            life = lifetime;
            gravity = fall;
            spriteRenderer = GetComponent<SpriteRenderer>();
            color = spriteRenderer.color;
            startAlpha = color.a;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            velocity.y -= gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            color.a = startAlpha * (1f - age / life);
            spriteRenderer.color = color;
        }
    }
}
