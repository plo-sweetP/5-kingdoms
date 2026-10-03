using System.Collections;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Short-lived visual effects (slash swipes, punches, arrows and arrow rain, hit bursts, sparkles, afterimages) and the
    /// markers that stay until removed (warning tiles, aiming highlights and reticles). Short-lived ones clean themselves up.
    /// </summary>
    public static class Effects
    {
        const int EffectOrder = 9500;
        const int WarningOrder = 5; // Above floor and wall shadows, below stairs, items and actors.
        const int HighlightOrder = 6;
        static Sprite[] slashFrames;

        /// <summary>Crescent swipe over the target tile, rotated to the attack direction.</summary>
        public static void Slash(Transform parent, Vector3 position, Direction8 direction, Color tint)
        {
            slashFrames ??= new[]
            {
                SpriteLibrary.Get("Effects/slash_0", Color.white),
                SpriteLibrary.Get("Effects/slash_1", Color.white),
                SpriteLibrary.Get("Effects/slash_2", Color.white),
            };
            var go = new GameObject("Slash");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var offset = direction.ToOffset();
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.Y, offset.X) * Mathf.Rad2Deg);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = EffectOrder;
            renderer.color = tint;
            go.AddComponent<Flipbook>().Play(slashFrames, 0.045f);
        }

        /// <summary>A gauntlet blow's impact: a star that pops over the target tile and fades.</summary>
        public static void Punch(Transform parent, Vector3 position, Color tint)
        {
            var go = new GameObject("Punch");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-20f, 20f));
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Get("Effects/punch", Color.white);
            renderer.sortingOrder = EffectOrder;
            renderer.color = tint;
            go.AddComponent<PopFade>().Init(0.5f, 1.1f, 0.16f);
        }

        /// <summary>Arrows raining down onto a tile (Volley); returns when the last one lands.</summary>
        public static IEnumerator RainArrows(Transform parent, Vector3 tileCenter, Color tint, int count, float duration)
        {
            var sprite = SpriteLibrary.Get("Effects/arrow", Color.white);
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
                var offset = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.2f, 0.2f), 0f);
                starts[i] = tileCenter + offset;
                delays[i] = duration * 0.5f * i / Mathf.Max(1, count - 1);
                go.transform.position = starts[i] + Vector3.up * 3f;
                arrows[i] = go.transform;
            }
            float fall = duration * 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    float k = Mathf.Clamp01((t - delays[i]) / fall);
                    arrows[i].position = starts[i] + Vector3.up * (3f * (1f - k));
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
            go.transform.localScale = new Vector3(1f - 2f * SpriteLibrary.Pixel, 1f - 2f * SpriteLibrary.Pixel, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.sortingOrder = HighlightOrder;
            renderer.color = color;
            return go;
        }

        /// <summary>Corner brackets around a target tile; the chosen one pulses. Destroy it when aiming ends.</summary>
        public static GameObject Reticle(Transform parent, Vector3 tileCenter, Color color, bool pulse)
        {
            var go = new GameObject("Reticle");
            go.transform.SetParent(parent, false);
            go.transform.position = tileCenter;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Get("Effects/reticle", Color.white);
            renderer.sortingOrder = EffectOrder;
            renderer.color = color;
            if (pulse) go.AddComponent<Pulse>().Init(color, 0.55f, 1f, 8f);
            return go;
        }

        /// <summary>An arrow flying from one point to another, pointing the way it flies (any angle); returns when it lands.</summary>
        public static IEnumerator Arrow(Transform parent, Vector3 from, Vector3 to, Color tint, float duration)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var flight = to - from;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flight.y, flight.x) * Mathf.Rad2Deg); // The art points right.
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.Get("Effects/arrow", Color.white);
            renderer.sortingOrder = EffectOrder;
            renderer.color = tint;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                go.transform.position = Vector3.Lerp(from, to, t / duration);
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>Square pixel chips flying out and falling, for hits and defeats.</summary>
        public static void Burst(Transform parent, Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * UnityEngine.Random.Range(speed * 0.4f, speed)
                               + Vector3.up * (speed * 0.4f);
                Spawn(parent, position, color, velocity, UnityEngine.Random.Range(0.35f, 0.6f), gravity: 7f);
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
            go.transform.localScale = new Vector3(1f - 2f * SpriteLibrary.Pixel, 1f - 2f * SpriteLibrary.Pixel, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.sortingOrder = WarningOrder;
            go.AddComponent<Pulse>().Init(new Color(1f, 0.2f, 0.15f), 0.18f, 0.42f, 9f);
            return go;
        }

        /// <summary>Gentle rising sparkles, for heals and level-ups.</summary>
        public static void Sparkle(Transform parent, Vector3 position, Color color)
        {
            for (int i = 0; i < 10; i++)
            {
                var start = position + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.4f, 0.1f), 0f);
                var velocity = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(0.8f, 1.6f), 0f);
                Spawn(parent, start, color, velocity, UnityEngine.Random.Range(0.5f, 0.8f), gravity: 0f);
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
            float size = UnityEngine.Random.Range(2, 4) * SpriteLibrary.Pixel;
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteLibrary.White;
            renderer.color = color;
            renderer.sortingOrder = EffectOrder;
            go.AddComponent<Particle>().Init(velocity, life, gravity);
        }
    }

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

    /// <summary>Grows from one scale to another while fading out, then removes itself.</summary>
    sealed class PopFade : MonoBehaviour
    {
        float from, to, life, age;
        SpriteRenderer spriteRenderer;
        Color color;

        public void Init(float startScale, float endScale, float lifetime)
        {
            from = startScale;
            to = endScale;
            life = lifetime;
            spriteRenderer = GetComponent<SpriteRenderer>();
            color = spriteRenderer.color;
            transform.localScale = Vector3.one * from;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            float k = age / life;
            transform.localScale = Vector3.one * Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
            spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * (k < 0.5f ? 1f : 2f * (1f - k)));
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
