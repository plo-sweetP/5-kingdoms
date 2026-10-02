using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>Short-lived visual effects (slash swipes, hit bursts, sparkles). Each one cleans itself up.</summary>
    public static class Effects
    {
        const int EffectOrder = 9500;
        const int WarningOrder = 5; // Above floor and wall shadows, below stairs, items and actors.
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

        public void Init(Vector3 startVelocity, float lifetime, float fall)
        {
            velocity = startVelocity;
            life = lifetime;
            gravity = fall;
            spriteRenderer = GetComponent<SpriteRenderer>();
            color = spriteRenderer.color;
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
            color.a = 1f - age / life;
            spriteRenderer.color = color;
        }
    }
}
