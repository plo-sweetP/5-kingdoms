using System.Collections;
using System.Collections.Generic;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Visual for one actor: its animated sprite (idle, run, attack and the rest of what its art has), shadow, enemy HP
    /// bar, status icons, an aura's glow, a ring set's twinkle, and the lunge, hit flash, boss wind-up and death. The root
    /// transform sits exactly on the tile; all motion is applied to a child so it never drifts from the gameplay
    /// position. Sprites are bigger than a tile and stand on it by their pivot (the art manifest's ground point), so a
    /// tall one overflows upward; one that hides another actor behind it turns see-through.
    /// </summary>
    public sealed class ActorView : MonoBehaviour
    {
        const int ShadowOrder = 30;
        const int AuraOrder = 3;
        const int HpBarOrder = 9000;
        const float RingTwinkleMin = 2.5f, RingTwinkleMax = 4.5f;
        const float HpBarWidth = 0.7f;
        const float IconSpacing = 26f * SpriteLibrary.Pixel;
        const float RunFps = 14f;
        const float StopRunningAfter = 0.14f; // Keeps the run cycle going between the steps of a walk.
        const float SeeThroughAlpha = 0.6f;
        static readonly Color AuraColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color HurtTint = new Color(1f, 0.5f, 0.5f);
        static readonly Color ChargeTint = new Color(1f, 0.7f, 0.7f);

        /// <summary>How long <see cref="Lunge"/> takes to reach its target: when a strike lands.</summary>
        public const float LungeTime = 0.11f;

        Transform visual;
        SpriteRenderer body;
        SpriteRenderer flash;
        SpriteRenderer shadow;
        Transform hpBar;
        Transform hpFill;
        Transform statusRow;
        readonly List<SpriteRenderer> statusIcons = new List<SpriteRenderer>();
        GameObject auraField;
        ActorSprites sprites;
        SpriteAnim anim;
        string queued;          // Plays after the current one-shot animation.
        float animTime;         // In frames.
        float animFps;
        bool holdLastFrame;
        float runUntil;
        bool charging;
        bool dying;
        Color statusTint = Color.white;
        Color tint = Color.white;
        float alpha = 1f;       // Fades (spawning, dying).
        float seeThrough = 1f;  // Lowered while this actor hides another.
        float seeThroughTarget = 1f;
        float flashAmount;
        Color? ring;            // The worn ring set's colour.
        float ringFlash;
        float nextTwinkle;
        int maxHp;
        Vector3 lunge;

        public int ActorId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsHero { get; private set; }
        public bool IsBoss { get; private set; }
        public Color BurstColor { get; private set; }

        /// <summary>How far the top of its head is above its tile's centre, in tiles.</summary>
        public float HeadTop { get; private set; }

        /// <summary>How far its sprite reaches to each side and upward, in tiles: what it can hide.</summary>
        public Vector2 Reach { get; private set; }

        /// <summary>Where words and numbers pop up: over its head (not higher than a tall boss's chest).</summary>
        public Vector3 TextAnchor => transform.position + Vector3.up * (Mathf.Min(HeadTop, 1.5f) + 0.2f);

        public Sprite Portrait => sprites.Portrait;

        public static ActorView Create(Transform parent, Actor actor, ActorSprites sprites, Color burstColor)
        {
            var root = new GameObject($"{actor.Name} #{actor.Id}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ActorView>();
            view.ActorId = actor.Id;
            view.DisplayName = actor.Name;
            view.IsHero = actor.Team == Team.Hero;
            view.IsBoss = actor.Definition.IsBoss;
            view.maxHp = actor.MaxHp;
            view.BurstColor = burstColor;
            view.sprites = sprites;

            var first = sprites.Idle.Frames[0];
            view.HeadTop = SpriteLibrary.VisibleTop(first);
            view.Reach = new Vector2(Mathf.Max(first.pivot.x, first.rect.width - first.pivot.x) / first.pixelsPerUnit, view.HeadTop);

            if (sprites.Shadow != null) view.shadow = NewRenderer("Shadow", root.transform, sprites.Shadow, ShadowOrder);
            view.visual = new GameObject("Visual").transform;
            view.visual.SetParent(root.transform, false);
            view.body = NewRenderer("Body", view.visual, first, 0);
            view.flash = NewRenderer("Flash", view.visual, null, 0);
            view.flash.color = new Color(1f, 1f, 1f, 0f);

            // Bosses show their HP in the HUD instead of over their heads.
            bool hpBar = !view.IsHero && !view.IsBoss;
            if (hpBar) view.CreateHpBar(view.HeadTop + 6f * SpriteLibrary.Pixel);
            view.statusRow = new GameObject("Statuses").transform;
            view.statusRow.SetParent(root.transform, false);
            view.statusRow.localPosition = new Vector3(0f, Mathf.Min(view.HeadTop, 2f) + (hpBar ? 24f : 16f) * SpriteLibrary.Pixel, 0f);
            view.SetHp(actor.Hp);

            view.PlayLoop(sprites.Idle);
            view.animTime = Random.value * sprites.Idle.Frames.Length; // Keeps a room full of spiders from moving in step.
            return view;
        }

        // ---- Animation ----

        /// <summary>
        /// Plays a one-shot animation (the first of these names the actor's art has), then goes back to idle. With
        /// <paramref name="impactAfter"/> the animation is timed so that its impact frame comes that many seconds from
        /// now, whatever its length: the turn's pace sets the animation's speed, not the other way round. Returns how
        /// long the animation takes at that speed (0 when the art has nothing like it: the lunge alone shows it then).
        /// </summary>
        public float Play(string name, float impactAfter = 0f, params string[] fallbacks)
        {
            if (dying) return 0f;
            var next = sprites.Get(name, fallbacks);
            if (next == sprites.Idle && name != "idle") return 0f;
            anim = next;
            animTime = 0f;
            holdLastFrame = false;
            queued = null;
            animFps = impactAfter > 0f && next.Impact > 0 ? next.Impact / impactAfter : next.Fps * 1.5f;
            ShowFrame(0);
            return next.Frames.Length / animFps;
        }

        /// <summary>The animation to play once the current one-shot ends (a boss's recovery after its attack).</summary>
        public void Then(string name)
        {
            if (sprites.Has(name)) queued = name;
        }

        /// <summary>Plays an animation and stays on its last frame until <see cref="Release"/> or another animation.</summary>
        public void PlayAndHold(string name)
        {
            if (dying || !sprites.Has(name)) return;
            anim = sprites.Get(name);
            animTime = 0f;
            animFps = anim.Fps * 1.5f;
            holdLastFrame = true;
            queued = null;
            ShowFrame(0);
        }

        public void Release()
        {
            if (holdLastFrame && !dying) PlayLoop(sprites.Idle);
        }

        /// <summary>Walking: the run cycle plays while steps keep coming.</summary>
        public void SetMoving()
        {
            runUntil = Time.time + StopRunningAfter;
            if (dying || holdLastFrame || !anim.Loop) return;
            var run = sprites.Get("run");
            if (anim != run) PlayLoop(run, RunFps);
        }

        void PlayLoop(SpriteAnim loop, float fps = 0f)
        {
            anim = loop;
            animTime = 0f;
            animFps = fps > 0f ? fps : loop.Fps;
            holdLastFrame = false;
            queued = null;
            ShowFrame(0);
        }

        void Animate()
        {
            animTime += Time.deltaTime * animFps;
            int frame = (int)animTime;
            int count = anim.Frames.Length;
            if (frame >= count)
            {
                if (anim.Loop)
                {
                    animTime -= count * (frame / count);
                    frame %= count;
                }
                else if (holdLastFrame || dying)
                {
                    frame = count - 1;
                }
                else if (queued != null)
                {
                    Play(queued);
                    animFps = anim.Fps * 1.2f;
                    return;
                }
                else
                {
                    bool running = Time.time < runUntil;
                    PlayLoop(running ? sprites.Get("run") : sprites.Idle, running ? RunFps : 0f);
                    return;
                }
            }
            if (anim.Loop && anim != sprites.Idle && Time.time >= runUntil)
            {
                PlayLoop(sprites.Idle); // The walk is over.
                return;
            }
            ShowFrame(frame);
        }

        void ShowFrame(int frame)
        {
            var sprite = anim.Frames[Mathf.Clamp(frame, 0, anim.Frames.Length - 1)];
            if (body.sprite == sprite) return;
            body.sprite = sprite;
            if (flashAmount > 0f || ringFlash > 0f) flash.sprite = SpriteLibrary.Silhouette(sprite);
        }

        // ---- State shown on the actor ----

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
                icon.sprite = SpriteLibrary.Still("Effects/status_" + name);
                icon.transform.localPosition = new Vector3((i - (count - 1) / 2f) * IconSpacing, 0f, 0f);
            }
        }

        /// <summary>Aura of Protection: a soft gold glow over the 3x3 tiles the aura covers, moving with its holder.</summary>
        public void SetAura(bool on)
        {
            if (on && auraField == null)
            {
                var field = NewRenderer("Aura", transform, SpriteLibrary.White, AuraOrder);
                field.transform.localScale = new Vector3(3f - 4f * SpriteLibrary.Pixel, 3f - 4f * SpriteLibrary.Pixel, 1f);
                field.gameObject.AddComponent<Pulse>().Init(AuraColor, 0.1f, 0.24f, 3f);
                auraField = field.gameObject;
            }
            else if (!on && auraField != null)
            {
                Destroy(auraField);
                auraField = null;
            }
        }

        /// <summary>
        /// The hero wears a ring set: its colour twinkles on the weapon hand every few seconds (Peter, 2026-10-04: a
        /// sparkle on the hand, not a glow on the ground, where the game shows what the player has to read). Null for none.
        /// </summary>
        public void SetRing(Color? color)
        {
            ring = color;
            nextTwinkle = Time.time + Random.Range(0.4f, RingTwinkleMax);
        }

        /// <summary>
        /// The ring set's bonus fired: a short pulse of its colour over the hero and a few twinkles around it. The ring
        /// bonuses come with the gear rules (milestone 1h); this is the effect they trigger.
        /// </summary>
        public void RingFlash()
        {
            if (!ring.HasValue || dying) return;
            StartCoroutine(RingFlashRoutine());
        }

        IEnumerator RingFlashRoutine()
        {
            for (int i = 0; i < 3; i++)
                Effects.Play(transform, "Effects/twinkle", transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, HeadTop), 0f), ring);
            const float duration = 0.35f;
            for (float t = 0f; t < duration && ring.HasValue; t += Time.deltaTime)
            {
                ringFlash = 0.7f * (1f - t / duration);
                if (flash.sprite == null || flashAmount <= 0f) flash.sprite = SpriteLibrary.Silhouette(body.sprite);
                ApplyColor();
                yield return null;
            }
            ringFlash = 0f;
            ApplyColor();
        }

        void Twinkle()
        {
            var hand = new Vector3(body.flipX ? -sprites.Hand.x : sprites.Hand.x, sprites.Hand.y, 0f);
            Effects.Play(transform, "Effects/twinkle", transform.position + hand + lunge, ring);
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            int order = 5000 - Mathf.RoundToInt(position.y * 10f); // Lower on screen draws in front.
            body.sortingOrder = order;
            flash.sortingOrder = order + 1;
        }

        public void SetFacing(Direction8 direction)
        {
            int dx = direction.ToOffset().X;
            if (dx == 0) return; // Straight up or down keeps the last left/right facing.
            body.flipX = dx < 0; // Art faces right.
            flash.flipX = body.flipX;
            if (shadow != null) shadow.flipX = body.flipX;
        }

        public void SetHp(int hp)
        {
            if (hpBar == null) return;
            float ratio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            hpBar.gameObject.SetActive(hp > 0 && hp < maxHp);
            hpFill.localScale = new Vector3(HpBarWidth * ratio, 4f * SpriteLibrary.Pixel, 1f);
            hpFill.localPosition = new Vector3(-HpBarWidth * (1f - ratio) / 2f, 0f, 0f);
        }

        /// <summary>
        /// Boss wind-up: it raises its weapon and holds the pose, tinted and trembling, until the special attack lands.
        /// </summary>
        public void SetCharging(bool value)
        {
            if (charging == value) return;
            charging = value;
            if (value) PlayAndHold("windup");
            else Release();
            ApplyColor();
        }

        /// <summary>A lasting tint for a status (a guard's blue, a mark's red); white for none.</summary>
        public void SetStatusTint(Color color)
        {
            if (statusTint == color) return;
            statusTint = color;
            ApplyColor();
        }

        /// <summary>Turns the sprite see-through while it stands in front of another actor (big monsters do).</summary>
        public void SetSeeThrough(bool on) => seeThroughTarget = on ? SeeThroughAlpha : 1f;

        Color RestingColor => charging ? ChargeTint : statusTint;

        void ApplyColor()
        {
            var color = tint == Color.white ? RestingColor : tint;
            color.a = alpha * seeThrough;
            body.color = color;
            if (shadow != null) shadow.color = new Color(1f, 1f, 1f, alpha);
            // A hit flashes white; a ring's bonus pulses in the ring's colour.
            var flashColor = flashAmount > 0f || !ring.HasValue ? Color.white : ring.Value;
            flashColor.a = (flashAmount > 0f ? flashAmount : ringFlash) * alpha;
            flash.color = flashColor;
        }

        // ---- Motion ----

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
            if (!dying) StartCoroutine(HurtRoutine(knockDirection));
        }

        /// <summary>
        /// Falls: with a death animation in its art (the Troll's club breaks), that plays out and the body fades; without
        /// one, a white flash and a quick fade.
        /// </summary>
        public IEnumerator Die()
        {
            charging = false;
            if (hpBar != null) hpBar.gameObject.SetActive(false);
            statusRow.gameObject.SetActive(false);
            SetAura(false);
            ring = null;
            ringFlash = 0f;
            lunge = Vector3.zero;
            tint = Color.white;
            if (sprites.Has("dead"))
            {
                anim = sprites.Get("dead");
                animTime = 0f;
                animFps = anim.Fps * 1.4f;
                queued = null;
                dying = true;
                SetFlash(0f);
                yield return new WaitForSeconds(anim.Frames.Length / animFps + 0.25f);
            }
            else
            {
                dying = true;
                SetFlash(1f);
                yield return new WaitForSeconds(0.08f);
            }
            const float fade = 0.3f;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                alpha = 1f - t / fade;
                ApplyColor();
                yield return null;
            }
            alpha = 0f;
            ApplyColor();
        }

        public void FadeIn() => StartCoroutine(FadeInRoutine());

        /// <summary>Leaves a fading, tinted copy of the current pose behind (fast moves like a dash).</summary>
        public void LeaveAfterimage(Transform parent, Color color) => Effects.Afterimage(parent, body, color);

        void LateUpdate()
        {
            Animate();
            var offset = lunge;
            if (charging) offset.x += Mathf.Sin(Time.time * 70f) * 2f * SpriteLibrary.Pixel; // Trembling: something big is coming.
            visual.localPosition = offset;
            if (ring.HasValue && !dying && Time.time >= nextTwinkle)
            {
                Twinkle();
                nextTwinkle = Time.time + Random.Range(RingTwinkleMin, RingTwinkleMax);
            }
            if (!Mathf.Approximately(seeThrough, seeThroughTarget))
            {
                seeThrough = Mathf.MoveTowards(seeThrough, seeThroughTarget, 4f * Time.deltaTime);
                ApplyColor();
            }
        }

        IEnumerator HurtRoutine(Vector3 knockDirection)
        {
            SetFlash(1f);
            lunge = knockDirection * 0.14f;
            yield return new WaitForSeconds(0.07f);
            if (dying) yield break;
            SetFlash(0f);
            tint = HurtTint;
            ApplyColor();
            const float settle = 0.16f;
            for (float t = 0f; t < settle && !dying; t += Time.deltaTime)
            {
                float k = 1f - t / settle;
                lunge = knockDirection * (0.14f * k) + (Vector3)(Random.insideUnitCircle * 0.03f * k);
                yield return null;
            }
            if (dying) yield break;
            lunge = Vector3.zero;
            tint = Color.white;
            ApplyColor();
        }

        IEnumerator FadeInRoutine()
        {
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                alpha = t / 0.3f;
                ApplyColor();
                yield return null;
            }
            alpha = 1f;
            ApplyColor();
        }

        void SetFlash(float amount)
        {
            flashAmount = amount;
            if (amount > 0f)
            {
                var silhouette = SpriteLibrary.Silhouette(body.sprite);
                flash.sprite = silhouette;
                if (silhouette == null) tint = Color.Lerp(Color.white, HurtTint, amount); // No readable texture: tint instead.
            }
            ApplyColor();
        }

        void CreateHpBar(float height)
        {
            hpBar = new GameObject("HpBar").transform;
            hpBar.SetParent(transform, false);
            hpBar.localPosition = new Vector3(0f, height, 0f);
            var back = NewRenderer("Back", hpBar, SpriteLibrary.White, HpBarOrder);
            back.color = new Color(0.086f, 0.11f, 0.18f, 0.9f);
            back.transform.localScale = new Vector3(HpBarWidth + 4f * SpriteLibrary.Pixel, 8f * SpriteLibrary.Pixel, 1f);
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
