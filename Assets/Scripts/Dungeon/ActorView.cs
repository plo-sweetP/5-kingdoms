using System.Collections;
using System.Collections.Generic;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Visual for one actor: body sprite, drop shadow, idle motion, enemy HP bar, status icons, an aura's glow, and the
    /// step hop, attack lunge, hit flash, boss wind-up and leap, and death animations. The root transform sits exactly on
    /// the tile; all wobble is applied to a child so it never drifts from the gameplay position. Sprites taller than a
    /// tile (bosses) stand on the tile's lower edge and overflow upward.
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        const int ShadowOrder = 30;
        const int AuraOrder = 3;
        const int HpBarOrder = 9000;
        const float HpBarWidth = 0.7f;
        const float IconSpacing = 13f * SpriteLibrary.Pixel;
        static readonly Color AuraColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color HurtTint = new Color(1f, 0.5f, 0.5f);
        static readonly Color ChargeTint = new Color(1f, 0.7f, 0.7f);

        Transform visual;
        SpriteRenderer body;
        SpriteRenderer flash;
        SpriteRenderer shadow;
        Transform hpBar;
        Transform hpFill;
        Transform statusRow;
        readonly List<SpriteRenderer> statusIcons = new List<SpriteRenderer>();
        GameObject auraField;
        bool squishy;
        bool charging;
        Color statusTint = Color.white;
        int maxHp;
        float idleClock;
        float hop;
        float scale = 1f;
        float lift;        // Raises sprites taller than one tile so they stand on the tile.
        float squashAnchor; // Distance from the visual's center to its bottom, kept planted while squashing.
        Vector3 lunge;

        public int ActorId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsHero { get; private set; }
        public bool IsBoss { get; private set; }
        public Color BurstColor { get; private set; }

        public static ActorView Create(Transform parent, Actor actor, Sprite sprite, Sprite shadowSprite, bool squishy, Color burstColor)
        {
            var root = new GameObject($"{actor.Name} #{actor.Id}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ActorView>();
            view.ActorId = actor.Id;
            view.DisplayName = actor.Name;
            view.IsHero = actor.Team == Team.Hero;
            view.IsBoss = actor.Definition.IsBoss;
            view.squishy = squishy;
            view.maxHp = actor.MaxHp;
            view.BurstColor = burstColor;
            view.idleClock = UnityEngine.Random.value * 10f; // Keeps a room full of spiders from wobbling in sync.

            var size = sprite.bounds.size;
            view.lift = Mathf.Max(0f, size.y - 1f) / 2f;
            view.squashAnchor = size.y * 0.45f;

            view.shadow = NewRenderer("Shadow", root.transform, shadowSprite, ShadowOrder);
            view.shadow.transform.localScale = new Vector3(Mathf.Max(1f, size.x), 1f, 1f);
            view.visual = new GameObject("Visual").transform;
            view.visual.SetParent(root.transform, false);
            view.body = NewRenderer("Body", view.visual, sprite, 0);
            var silhouette = SpriteLibrary.Silhouette(sprite);
            if (silhouette != null)
            {
                view.flash = NewRenderer("Flash", view.visual, silhouette, 0);
                view.flash.color = new Color(1f, 1f, 1f, 0f);
            }
            // Bosses show their HP in the HUD instead of over their heads.
            float top = SpriteLibrary.VisibleTop(sprite);
            bool hpBar = !view.IsHero && !view.IsBoss;
            if (hpBar) view.CreateHpBar(top + 3f * SpriteLibrary.Pixel);
            view.statusRow = new GameObject("Statuses").transform;
            view.statusRow.SetParent(root.transform, false);
            view.statusRow.localPosition = new Vector3(0f, top + view.lift + (hpBar ? 12f : 8f) * SpriteLibrary.Pixel, 0f);
            view.SetHp(actor.Hp);
            return view;
        }

        /// <summary>
        /// One small icon per status (mark, snare, taunt, guard, aura), in a row over the actor's head, and last a stun
        /// icon while its coming turn is pushed back (<paramref name="delayed"/>: a stun or a slow, until it acts).
        /// </summary>
        public void SetStatuses(IReadOnlyList<StatusEffect> statuses, bool delayed)
        {
            int statusCount = statuses?.Count ?? 0;
            int count = statusCount + (delayed ? 1 : 0);
            while (statusIcons.Count < count) statusIcons.Add(NewRenderer("Icon", statusRow, null, HpBarOrder + 2));
            for (int i = 0; i < statusIcons.Count; i++)
            {
                var icon = statusIcons[i];
                icon.gameObject.SetActive(i < count);
                if (i >= count) continue;
                string name = i < statusCount ? statuses[i].Kind.ToString().ToLowerInvariant() : "stunned";
                icon.sprite = SpriteLibrary.Get("Effects/status_" + name, Color.white);
                icon.transform.localPosition = new Vector3((i - (count - 1) / 2f) * IconSpacing, 0f, 0f);
            }
        }

        /// <summary>Aura of Protection: a soft gold glow over the 3x3 tiles the aura covers, moving with its holder.</summary>
        public void SetAura(bool on)
        {
            if (on && auraField == null)
            {
                var field = NewRenderer("Aura", transform, SpriteLibrary.White, AuraOrder);
                field.transform.localScale = new Vector3(3f - 2f * SpriteLibrary.Pixel, 3f - 2f * SpriteLibrary.Pixel, 1f);
                field.gameObject.AddComponent<Pulse>().Init(AuraColor, 0.1f, 0.24f, 3f);
                auraField = field.gameObject;
            }
            else if (!on && auraField != null)
            {
                Destroy(auraField);
                auraField = null;
            }
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

        /// <summary>Boss wind-up: crouch low, tint and tremble until the special attack lands.</summary>
        public void SetCharging(bool value)
        {
            charging = value;
            body.color = RestingColor;
        }

        /// <summary>A lasting tint for a status (a guard's blue, a mark's red); white for none.</summary>
        public void SetStatusTint(Color tint)
        {
            if (statusTint == tint) return;
            statusTint = tint;
            body.color = RestingColor;
        }

        Color RestingColor => charging ? ChargeTint : statusTint;

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

        /// <summary>A jump straight up and a hard landing (the boss's slam). Returns when it lands.</summary>
        public IEnumerator Leap(float height, float riseTime, float fallTime)
        {
            SetCharging(false);
            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                float k = t / riseTime;
                lunge = Vector3.up * (height * (1f - (1f - k) * (1f - k)));
                yield return null;
            }
            for (float t = 0f; t < fallTime; t += Time.deltaTime)
            {
                float k = t / fallTime;
                lunge = Vector3.up * (height * (1f - k * k));
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
            charging = false;
            if (hpBar != null) hpBar.gameObject.SetActive(false);
            statusRow.gameObject.SetActive(false);
            SetAura(false);
            SetFlash(1f);
            yield return new WaitForSeconds(IsBoss ? 0.3f : 0.08f);
            float duration = IsBoss ? 0.6f : 0.3f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                scale = 1f - 0.7f * k;
                SetBodyAlpha(1f - k);
                if (flash != null) flash.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            SetBodyAlpha(0f);
        }

        public void FadeIn() => StartCoroutine(FadeInRoutine());

        /// <summary>Leaves a fading, tinted copy of the current pose behind (fast moves like a dash).</summary>
        public void LeaveAfterimage(Transform parent, Color tint) => Effects.Afterimage(parent, body, tint);

        void LateUpdate()
        {
            idleClock += Time.deltaTime;
            var offset = lunge;
            offset.y += lift;
            var visualScale = new Vector3(scale, scale, 1f);
            if (hop > 0f && hop < 1f) offset.y += Mathf.Sin(hop * Mathf.PI) * 2f * SpriteLibrary.Pixel;

            if (charging)
            {
                // Crouched low and trembling: a clear "something big is coming" pose.
                visualScale.x *= 1.12f;
                visualScale.y *= 0.82f;
                offset.y -= 0.18f * squashAnchor;
                offset.x += Mathf.Sin(idleClock * 70f) * SpriteLibrary.Pixel;
            }
            else if (squishy)
            {
                float wobble = Mathf.Sin(idleClock * 4f) * 0.06f;
                visualScale.x *= 1f + wobble;
                visualScale.y *= 1f - wobble;
                offset.y -= wobble * squashAnchor * scale; // Keep the bottom planted while squashing.
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
            body.color = RestingColor;
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

        void CreateHpBar(float height)
        {
            hpBar = new GameObject("HpBar").transform;
            hpBar.SetParent(transform, false);
            hpBar.localPosition = new Vector3(0f, height, 0f);
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
