using System.Collections;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Visual for one actor: body sprite, drop shadow, idle motion, enemy HP bar, and the step hop, attack
    /// lunge, hit flash and death animations. The root transform sits exactly on the tile; all wobble is
    /// applied to a child so it never drifts from the gameplay position.
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        const int ShadowOrder = 30;
        const int HpBarOrder = 9000;
        const float HpBarWidth = 0.7f;
        static readonly Color HurtTint = new Color(1f, 0.5f, 0.5f);

        Transform visual;
        SpriteRenderer body;
        SpriteRenderer flash;
        SpriteRenderer shadow;
        Transform hpBar;
        Transform hpFill;
        bool squishy;
        int maxHp;
        float idleClock;
        float hop;
        float scale = 1f;
        Vector3 lunge;

        public int ActorId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsHero { get; private set; }
        public Color BurstColor { get; private set; }

        public static ActorView Create(Transform parent, Actor actor, Sprite sprite, Sprite shadowSprite, bool squishy, Color burstColor)
        {
            var root = new GameObject($"{actor.Name} #{actor.Id}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ActorView>();
            view.ActorId = actor.Id;
            view.DisplayName = actor.Name;
            view.IsHero = actor.Team == Team.Hero;
            view.squishy = squishy;
            view.maxHp = actor.MaxHp;
            view.BurstColor = burstColor;
            view.idleClock = UnityEngine.Random.value * 10f; // Keeps a room full of slimes from wobbling in sync.

            view.shadow = NewRenderer("Shadow", root.transform, shadowSprite, ShadowOrder);
            view.visual = new GameObject("Visual").transform;
            view.visual.SetParent(root.transform, false);
            view.body = NewRenderer("Body", view.visual, sprite, 0);
            var silhouette = SpriteLibrary.Silhouette(sprite);
            if (silhouette != null)
            {
                view.flash = NewRenderer("Flash", view.visual, silhouette, 0);
                view.flash.color = new Color(1f, 1f, 1f, 0f);
            }
            if (!view.IsHero) view.CreateHpBar();
            view.SetHp(actor.Hp);
            return view;
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            int order = 5000 - Mathf.RoundToInt(position.y * 10f); // Lower on screen draws in front.
            body.sortingOrder = order;
            if (flash != null) flash.sortingOrder = order + 1;
        }

        public void SetFacing(Direction8 direction)
        {
            int dx = direction.ToOffset().X;
            if (dx == 0) return; // Straight up or down keeps the last left/right facing.
            body.flipX = dx < 0; // Art faces right.
            if (flash != null) flash.flipX = body.flipX;
        }

        /// <summary>Progress (0-1) through a one-tile step; drives a small hop.</summary>
        public void SetHop(float progress) => hop = progress;

        public void SetHp(int hp)
        {
            if (hpBar == null) return;
            float ratio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            hpBar.gameObject.SetActive(hp > 0 && hp < maxHp);
            hpFill.localScale = new Vector3(HpBarWidth * ratio, 2f * SpriteLibrary.Pixel, 1f);
            hpFill.localPosition = new Vector3(-HpBarWidth * (1f - ratio) / 2f, 0f, 0f);
        }

        /// <summary>Attack swing: a short wind-up away from the target, then a fast lunge toward it. Call Recover after impact.</summary>
        public IEnumerator Lunge(Vector3 direction, float distance)
        {
            const float windUp = 0.05f, strike = 0.06f;
            for (float t = 0f; t < windUp; t += Time.deltaTime)
            {
                lunge = -direction * (0.08f * (t / windUp));
                yield return null;
            }
            for (float t = 0f; t < strike; t += Time.deltaTime)
            {
                float k = t / strike;
                lunge = Vector3.Lerp(-direction * 0.08f, direction * distance, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            lunge = direction * distance;
        }

        public IEnumerator Recover(float duration)
        {
            var start = lunge;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                lunge = Vector3.Lerp(start, Vector3.zero, t / duration);
                yield return null;
            }
            lunge = Vector3.zero;
        }

        /// <summary>Hit reaction: white flash, knock-back away from the attacker, red tint while it settles.</summary>
        public void Hurt(Vector3 knockDirection, int hpAfter)
        {
            SetHp(hpAfter);
            StartCoroutine(HurtRoutine(knockDirection));
        }

        public IEnumerator Die()
        {
            if (hpBar != null) hpBar.gameObject.SetActive(false);
            SetFlash(1f);
            yield return new WaitForSeconds(0.08f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                scale = 1f - 0.7f * k;
                SetBodyAlpha(1f - k);
                if (flash != null) flash.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            SetBodyAlpha(0f);
        }

        public void FadeIn() => StartCoroutine(FadeInRoutine());

        void LateUpdate()
        {
            idleClock += Time.deltaTime;
            var offset = lunge;
            var visualScale = new Vector3(scale, scale, 1f);
            if (hop > 0f && hop < 1f) offset.y += Mathf.Sin(hop * Mathf.PI) * 2f * SpriteLibrary.Pixel;

            if (squishy)
            {
                float wobble = Mathf.Sin(idleClock * 4f) * 0.06f;
                visualScale.x *= 1f + wobble;
                visualScale.y *= 1f - wobble;
                offset.y -= wobble * 0.45f * scale; // Keep the bottom planted while squashing.
            }
            else if (Mathf.Sin(idleClock * 3f) > 0.4f)
            {
                offset.y -= SpriteLibrary.Pixel; // One-pixel breathing bob.
            }
            visual.localPosition = offset;
            visual.localScale = visualScale;
        }

        IEnumerator HurtRoutine(Vector3 knockDirection)
        {
            SetFlash(1f);
            lunge = knockDirection * 0.14f;
            yield return new WaitForSeconds(0.07f);
            SetFlash(0f);
            body.color = HurtTint;
            const float settle = 0.16f;
            for (float t = 0f; t < settle; t += Time.deltaTime)
            {
                float k = 1f - t / settle;
                lunge = knockDirection * (0.14f * k) + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.03f * k);
                yield return null;
            }
            lunge = Vector3.zero;
            body.color = Color.white;
        }

        IEnumerator FadeInRoutine()
        {
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                SetBodyAlpha(t / 0.3f);
                yield return null;
            }
            SetBodyAlpha(1f);
        }

        void SetFlash(float amount)
        {
            if (flash != null) flash.color = new Color(1f, 1f, 1f, amount);
            else body.color = Color.Lerp(Color.white, HurtTint, amount); // No readable texture: tint instead.
        }

        void SetBodyAlpha(float alpha)
        {
            var color = body.color;
            color.a = alpha;
            body.color = color;
            shadow.color = new Color(1f, 1f, 1f, alpha);
        }

        void CreateHpBar()
        {
            hpBar = new GameObject("HpBar").transform;
            hpBar.SetParent(transform, false);
            hpBar.localPosition = new Vector3(0f, 0.44f, 0f);
            var back = NewRenderer("Back", hpBar, SpriteLibrary.White, HpBarOrder);
            back.color = new Color(0f, 0f, 0f, 0.8f);
            back.transform.localScale = new Vector3(HpBarWidth + 2f * SpriteLibrary.Pixel, 4f * SpriteLibrary.Pixel, 1f);
            var fill = NewRenderer("Fill", hpBar, SpriteLibrary.White, HpBarOrder + 1);
            fill.color = new Color(0.45f, 0.95f, 0.4f);
            hpFill = fill.transform;
        }

        static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
