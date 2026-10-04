using System;
using System.Collections.Generic;
using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// The party's status cards (top-left): portrait, name and level, HP, the ultimate's charge and EXP for each hero.
    /// The hero the player controls gets a gold card; partners get a tactic badge (Attack, Follow, Hold). Tapping a card
    /// takes control of that hero, tapping a badge cycles its tactic. Fallen heroes stay listed, dimmed. Built in code
    /// like the rest of the HUD, in 1920x1080 reference pixels, from the HUD art's frames and bars; the portrait is the
    /// hero's own head in its current look.
    /// </summary>
    public sealed class PartyPanel : MonoBehaviour
    {
        public const float CardWidth = 470f;
        public const float CardHeight = 76f;
        public const float CardGap = 6f;
        const float PortraitBox = 64f;

        static readonly Color CardColor = new Color(0.3f, 0.33f, 0.42f, 0.92f);
        static readonly Color LeaderColor = new Color(0.82f, 0.62f, 0.26f, 0.96f);
        static readonly Color TacticColor = new Color(0.42f, 0.5f, 0.66f, 1f);
        static readonly Color LeadTextColor = new Color(1f, 0.92f, 0.55f);
        static readonly Color ChargeColor = new Color(0.95f, 0.72f, 0.2f);
        static readonly Color ChargeReadyColor = new Color(1f, 0.92f, 0.45f);
        static readonly Color TroughColor = new Color(0.086f, 0.11f, 0.18f, 0.85f);

        /// <summary>A card was tapped: take control of this party member (party index).</summary>
        public event Action<int> MemberTapped;

        /// <summary>A partner's tactic badge was tapped (party index).</summary>
        public event Action<int> TacticTapped;

        readonly List<Card> cards = new List<Card>();
        RectTransform root;

        sealed class Card
        {
            public int ActorId;
            public HoldButton Button;
            public Image Portrait;
            public Text Name, Hp, Charge, Lead;
            public Image HpFill, ChargeFill, ExpFill;
            public HoldButton Tactic;
        }

        public static PartyPanel Create(RectTransform parent, Vector2 position)
        {
            var rect = UiFactory.CreateRect("Party", parent);
            UiFactory.Place(rect, new Vector2(0f, 1f), position, new Vector2(CardWidth, 3 * CardHeight + 2 * CardGap), new Vector2(0f, 1f));
            var panel = rect.gameObject.AddComponent<PartyPanel>();
            panel.root = rect;
            return panel;
        }

        /// <summary>Bottom edge of the panel for a party of this size, from the top of its parent.</summary>
        public static float Height(int members) => members * CardHeight + Mathf.Max(0, members - 1) * CardGap;

        /// <summary>An actor's small portrait: a hero's own head in its current look, a monster's face.</summary>
        public static Sprite PortraitOf(Actor actor) =>
            actor.Team == Team.Hero ? HeroLooks.Sprites(actor.Definition.Id).Portrait : SpriteLibrary.Monster(actor.Definition.Id).Portrait;

        /// <summary>
        /// A square window showing the middle of a portrait at the HUD art's scale (whole pixels), cut off at its edges.
        /// Returns the image to give a sprite to (then call <see cref="UiArtScaler.Size"/>).
        /// </summary>
        public static Image CreatePortrait(Transform parent, Vector2 anchor, Vector2 position, float box, Vector2 pivot)
        {
            var window = UiFactory.CreateRect("Portrait", parent);
            UiFactory.Place(window, anchor, position, new Vector2(box, box), pivot);
            window.gameObject.AddComponent<RectMask2D>();
            var image = UiFactory.CreateIcon("Face", window, null);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            return image;
        }

        public void Refresh(DungeonRun run)
        {
            var party = run.Party;
            while (cards.Count < party.Count) cards.Add(BuildCard(cards.Count));
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool used = i < party.Count;
                card.Button.gameObject.SetActive(used);
                if (!used) continue;

                var member = party[i];
                bool leader = member == run.Hero;
                card.ActorId = member.Id;
                card.Portrait.sprite = PortraitOf(member);
                card.Portrait.enabled = card.Portrait.sprite != null;
                UiArtScaler.Size(card.Portrait);
                card.Name.text = $"{member.Name}   Lv {member.Level}";
                card.Button.SetTint(leader ? LeaderColor : CardColor);
                card.Button.Interactable = member.IsAlive;
                SetBar(card.HpFill, member.Hp, member.MaxHp);
                card.HpFill.color = HpColor(member.Hp, member.MaxHp);
                card.Hp.text = member.IsAlive ? $"{member.Hp}/{member.MaxHp}" : "Fallen";
                ShowCharge(card, member.Definition.Ultimate != null ? member.Charge : -1);
                SetBar(card.ExpFill, member.Exp, CombatRules.ExpToNextLevel(member.Level));
                card.Lead.gameObject.SetActive(leader && member.IsAlive);
                card.Tactic.gameObject.SetActive(!leader && member.IsAlive);
                card.Tactic.SetLabel(TacticLabel(member.Tactic));
            }
        }

        /// <summary>Keeps a hero's HP in step with the animation (the full refresh comes after the action).</summary>
        public void SetHp(int actorId, int hp, int maxHp)
        {
            var card = Find(actorId);
            if (card == null) return;
            SetBar(card.HpFill, hp, maxHp);
            card.HpFill.color = HpColor(hp, maxHp);
            card.Hp.text = hp > 0 ? $"{hp}/{maxHp}" : "Fallen";
        }

        /// <summary>Keeps a hero's ultimate meter in step with the animation.</summary>
        public void SetCharge(int actorId, int charge)
        {
            var card = Find(actorId);
            if (card != null) ShowCharge(card, charge);
        }

        /// <summary>The gold ultimate meter: its percentage, and "ULT ready" (brighter) when full. -1: no ultimate.</summary>
        static void ShowCharge(Card card, int charge)
        {
            card.ChargeFill.transform.parent.gameObject.SetActive(charge >= 0);
            card.Charge.gameObject.SetActive(charge >= 0);
            if (charge < 0) return;
            bool ready = charge >= CombatRules.MaxCharge;
            SetBar(card.ChargeFill, charge, CombatRules.MaxCharge);
            card.ChargeFill.color = ready ? ChargeReadyColor : ChargeColor;
            card.Charge.text = ready ? "ULT ready" : $"ULT {charge}%";
            card.Charge.color = ready ? ChargeReadyColor : new Color(1f, 0.86f, 0.55f);
        }

        public static string TacticLabel(PartyTactic tactic) =>
            tactic == PartyTactic.Attack ? "Attack" : tactic == PartyTactic.Follow ? "Follow" : "Hold";

        Card Find(int actorId)
        {
            foreach (var card in cards)
                if (card.ActorId == actorId && card.Button.gameObject.activeSelf) return card;
            return null;
        }

        Card BuildCard(int index)
        {
            var card = new Card();
            var button = HoldButton.Create(root, $"Member {index + 1}", "", new Vector2(0f, 1f),
                new Vector2(CardWidth / 2f, -CardHeight / 2f - index * (CardHeight + CardGap)), new Vector2(CardWidth, CardHeight),
                "frame", 20, CardColor);
            button.Pressed += () => MemberTapped?.Invoke(index);
            card.Button = button;
            var t = button.transform;

            card.Portrait = CreatePortrait(t, new Vector2(0f, 0.5f), new Vector2(8f, 1f), PortraitBox, new Vector2(0f, 0.5f));

            card.Name = UiFactory.CreateText("Name", t, "", 24, TextAnchor.UpperLeft, DungeonHud.TextColor);
            UiFactory.Place(card.Name.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -4f), new Vector2(260f, 28f), new Vector2(0f, 1f));

            card.HpFill = UiFactory.CreateBar(t, "Hp", new Vector2(80f, -32f), new Vector2(226f, 16f), Color.green);
            card.Hp = UiFactory.CreateText("HpText", t, "", 20, TextAnchor.MiddleLeft, DungeonHud.TextColor);
            UiFactory.Place(card.Hp.rectTransform, new Vector2(0f, 1f), new Vector2(312f, -40f), new Vector2(110f, 24f), new Vector2(0f, 0.5f));

            card.ChargeFill = CreateStrip(t, "Charge", new Vector2(80f, -52f), new Vector2(226f, 8f), ChargeColor);
            card.Charge = UiFactory.CreateText("ChargeText", t, "", 17, TextAnchor.MiddleLeft, ChargeColor);
            UiFactory.Place(card.Charge.rectTransform, new Vector2(0f, 1f), new Vector2(312f, -57f), new Vector2(90f, 20f), new Vector2(0f, 0.5f));

            card.ExpFill = CreateStrip(t, "Exp", new Vector2(80f, -63f), new Vector2(226f, 4f), new Color(0.78f, 0.62f, 1f));

            card.Lead = UiFactory.CreateText("Lead", t, "LEAD", 20, TextAnchor.MiddleCenter, LeadTextColor);
            UiFactory.Place(card.Lead.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(70f, 30f));

            card.Tactic = HoldButton.Create(t, "Tactic", "Attack", new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(76f, 42f),
                "frame", 18, TacticColor);
            card.Tactic.Pressed += () => TacticTapped?.Invoke(index);
            return card;
        }

        /// <summary>A thin meter without a frame (charge, EXP): a dark strip and a coloured one over it.</summary>
        static Image CreateStrip(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var back = UiFactory.CreateImage(name + "Back", parent, null, TroughColor);
            UiFactory.Place(back.rectTransform, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            var fill = UiFactory.CreateImage(name + "Fill", back.transform, null, color);
            UiFactory.Stretch(fill.rectTransform);
            return fill;
        }

        static void SetBar(Image fill, int value, int max) =>
            fill.rectTransform.anchorMax = new Vector2(max > 0 ? Mathf.Clamp01(value / (float)max) : 0f, 1f);

        static Color HpColor(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? hp / (float)maxHp : 0f;
            return ratio > 0.5f ? new Color(0.36f, 0.86f, 0.42f) : ratio > 0.25f ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.92f, 0.3f, 0.26f);
        }
    }
}
