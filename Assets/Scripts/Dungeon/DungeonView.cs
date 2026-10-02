using System.Collections;
using System.Collections.Generic;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Draws a DungeonRun (terrain, actors, items) and animates the GameEvents each action produces.
    /// Holds no rules of its own: everything it shows comes from the run's state and events.
    /// </summary>
    public sealed class DungeonView : MonoBehaviour
    {
        const float StepTime = 0.11f;
        const float LungeDistance = 0.35f;
        const int WallPadding = 14; // Extra wall drawn past the map edge so the camera never shows the void.
        const int StairsOrder = 10;
        const int ItemOrder = 20;

        static readonly Color HeroSlashTint = new Color(1f, 0.88f, 0.45f); // Warm, so it reads against the white hit flash.
        static readonly Color EnemySlashTint = new Color(1f, 0.5f, 0.45f);
        static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color HeroHurtColor = new Color(1f, 0.5f, 0.45f);
        static readonly Color HealColor = new Color(0.45f, 1f, 0.5f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        static readonly Color WarningColor = new Color(1f, 0.62f, 0.3f);
        static readonly Color BossBurstColor = new Color(0.75f, 0.5f, 1f);

        readonly Dictionary<int, ActorView> actors = new Dictionary<int, ActorView>();
        readonly Dictionary<int, SpriteRenderer> items = new Dictionary<int, SpriteRenderer>();
        readonly List<GameObject> warnings = new List<GameObject>();
        DungeonHud hud;
        PixelCamera pixelCamera;
        Tilemap terrain;
        Tilemap wallShadows;
        Transform actorRoot;
        Transform itemRoot;
        Transform effectRoot;
        SpriteRenderer stairs;
        Tile[] floorTiles;
        Tile wallTop;
        Tile wallFace;
        Tile wallShadow;

        public static Vector3 TileCenter(GridPos p) => new Vector3(p.X + 0.5f, p.Y + 0.5f, 0f);

        public void Init(DungeonHud dungeonHud, PixelCamera camera)
        {
            hud = dungeonHud;
            pixelCamera = camera;

            var grid = new GameObject("Grid", typeof(Grid)).transform;
            grid.SetParent(transform, false);
            terrain = CreateTilemap(grid, "Terrain", 0);
            wallShadows = CreateTilemap(grid, "WallShadows", 1);
            actorRoot = CreateChild("Actors");
            itemRoot = CreateChild("Items");
            effectRoot = CreateChild("Effects");

            floorTiles = new Tile[4];
            for (int i = 0; i < floorTiles.Length; i++) floorTiles[i] = MakeTile($"Tiles/floor_{i}", new Color(0.3f, 0.27f, 0.23f));
            wallTop = MakeTile("Tiles/wall_top", new Color(0.16f, 0.14f, 0.21f));
            wallFace = MakeTile("Tiles/wall_face", new Color(0.27f, 0.23f, 0.33f));
            wallShadow = MakeTile("Tiles/shadow_top", new Color(0f, 0f, 0f, 0.3f));

            stairs = NewSprite("Stairs", transform, SpriteLibrary.Get("Tiles/stairs", Color.gray), StairsOrder);
        }

        /// <summary>Redraws everything from the run's current state (new run or new floor).</summary>
        public void Rebuild(DungeonRun run)
        {
            foreach (var view in actors.Values) Destroy(view.gameObject);
            actors.Clear();
            foreach (var item in items.Values) Destroy(item.gameObject);
            items.Clear();
            ClearWarnings();

            DrawTerrain(run.Map);
            stairs.gameObject.SetActive(run.Map.InBounds(run.Map.Stairs));
            stairs.transform.position = TileCenter(run.Map.Stairs);
            foreach (var actor in run.Actors) AddActor(actor);
            foreach (var item in run.Items) AddItem(item);

            var boss = run.Boss;
            if (boss != null) hud.ShowBoss(boss.Name, boss.Hp, boss.MaxHp);
            else hud.HideBoss();

            if (actors.TryGetValue(run.Hero.Id, out var hero))
            {
                pixelCamera.Target = hero.transform;
                pixelCamera.SnapToTarget();
            }
        }

        /// <summary>Banner subtitle for the current floor, e.g. "B3F" or "B5F: Boss".</summary>
        public static string FloorTitle(DungeonRun run) => run.IsBossFloor ? $"B{run.Floor}F: Boss" : $"B{run.Floor}F";

        /// <summary>Animates one action's events in order. Everyone who walked this turn moves together, like Mystery Dungeon.</summary>
        public IEnumerator Play(DungeonRun run, IReadOnlyList<GameEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case MovedEvent _:
                    {
                        var moves = new List<MovedEvent>();
                        while (i < events.Count && events[i] is MovedEvent move)
                        {
                            moves.Add(move);
                            i++;
                        }
                        i--;
                        yield return AnimateMoves(moves);
                        break;
                    }
                    case FacingChangedEvent facing:
                        if (actors.TryGetValue(facing.ActorId, out var turner)) turner.SetFacing(facing.Direction);
                        break;
                    case AttackEvent attack:
                    {
                        var hit = i + 1 < events.Count ? events[i + 1] as DamageEvent : null;
                        if (hit != null && hit.TargetId == attack.TargetId) i++;
                        else hit = null;
                        yield return AnimateAttack(run, attack, hit);
                        break;
                    }
                    case DamageEvent damage:
                        ShowDamage(run, damage, Vector3.zero);
                        break;
                    case DiedEvent died:
                        yield return AnimateDeath(died);
                        break;
                    case ExpGainedEvent exp:
                        hud.AddMessage($"Gained {exp.Amount} EXP.");
                        break;
                    case LevelUpEvent levelUp:
                        if (actors.TryGetValue(levelUp.ActorId, out var leveled))
                        {
                            hud.ShowFloatingText(leveled.transform.position + Vector3.up * 0.9f, "LEVEL UP!", GoldColor, 1.1f);
                            Effects.Sparkle(effectRoot, leveled.transform.position, GoldColor);
                        }
                        hud.AddMessage($"{run.Hero.Name} grew to Lv {levelUp.Level}!", GoldColor);
                        yield return new WaitForSeconds(0.35f);
                        break;
                    case HealedEvent heal:
                        if (actors.TryGetValue(heal.ActorId, out var healed))
                        {
                            hud.ShowFloatingText(healed.transform.position + Vector3.up * 0.55f, "+" + heal.Amount, HealColor, 1.1f);
                            Effects.Sparkle(effectRoot, healed.transform.position, HealColor);
                            if (healed.IsHero) hud.SetHeroHp(heal.HpAfter, run.Hero.MaxHp);
                            hud.AddMessage($"{healed.DisplayName} recovered {heal.Amount} HP.", HealColor);
                        }
                        yield return new WaitForSeconds(0.25f);
                        break;
                    case ItemPickedUpEvent pickup:
                        if (items.TryGetValue(pickup.ItemId, out var itemRenderer))
                        {
                            items.Remove(pickup.ItemId);
                            StartCoroutine(PopAndDestroy(itemRenderer));
                        }
                        hud.AddMessage($"Picked up a {pickup.Kind}.");
                        break;
                    case ActorSpawnedEvent spawned:
                        var newcomer = run.FindActor(spawned.ActorId);
                        if (newcomer != null) AddActor(newcomer).FadeIn();
                        break;
                    case BossActionEvent bossAction when bossAction.Action == BossAction.Slam:
                    {
                        var hits = new List<DamageEvent>();
                        while (i + 1 < events.Count && events[i + 1] is DamageEvent hit)
                        {
                            hits.Add(hit);
                            i++;
                        }
                        yield return AnimateSlam(run, bossAction.ActorId, hits);
                        break;
                    }
                    case BossActionEvent bossAction:
                        yield return bossAction.Action == BossAction.Charge
                            ? AnimateCharge(run, bossAction.ActorId)
                            : AnimateSummon(bossAction.ActorId);
                        break;
                    case FloorStartedEvent _:
                        yield return AnimateFloorChange(run);
                        break;
                    case RunEndedEvent _:
                        yield return new WaitForSeconds(0.4f); // A beat before the controller shows the end panel.
                        break;
                }
            }
            SyncToState(run);
        }

        // ---- Animations ----

        IEnumerator AnimateMoves(List<MovedEvent> moves)
        {
            var movers = new List<(ActorView view, Vector3 from, Vector3 to)>();
            foreach (var move in moves)
            {
                if (!actors.TryGetValue(move.ActorId, out var view)) continue;
                view.SetFacing(move.Direction);
                movers.Add((view, TileCenter(move.From), TileCenter(move.To)));
            }
            for (float t = 0f; t < StepTime; t += Time.deltaTime)
            {
                float k = t / StepTime;
                foreach (var (view, from, to) in movers)
                {
                    view.Place(Vector3.Lerp(from, to, k));
                    view.SetHop(k);
                }
                yield return null;
            }
            foreach (var (view, _, to) in movers)
            {
                view.Place(to);
                view.SetHop(0f);
            }
        }

        IEnumerator AnimateAttack(DungeonRun run, AttackEvent attack, DamageEvent hit)
        {
            if (!actors.TryGetValue(attack.AttackerId, out var attacker)) yield break;
            attacker.SetFacing(attack.Direction);
            var offset = attack.Direction.ToOffset();
            var direction = new Vector3(offset.X, offset.Y, 0f).normalized;

            yield return attacker.Lunge(direction, LungeDistance);
            var targetTile = attacker.transform.position + new Vector3(offset.X, offset.Y, 0f);
            Effects.Slash(effectRoot, targetTile, attack.Direction, attacker.IsHero ? HeroSlashTint : EnemySlashTint);

            if (hit != null)
            {
                ShowDamage(run, hit, direction);
                if (actors.TryGetValue(hit.TargetId, out var target))
                    hud.AddMessage(DescribeHit(attacker, target, hit), target.IsHero ? HeroHurtColor : DungeonHud.TextColor);
                yield return new WaitForSeconds(hit.Critical ? 0.14f : 0.06f); // Hit-stop sells the impact.
            }
            yield return attacker.Recover(0.09f);
        }

        void ShowDamage(DungeonRun run, DamageEvent hit, Vector3 knockDirection)
        {
            if (!actors.TryGetValue(hit.TargetId, out var target)) return;
            target.Hurt(knockDirection, hit.HpAfter);
            var color = hit.Critical ? CritColor : target.IsHero ? HeroHurtColor : Color.white;
            // Numbers pop above the target, unless the attacker stands above it: then beside it, so they don't cover the attacker.
            var numberOffset = knockDirection.y < -0.1f ? new Vector3(0.6f, 0.1f, 0f) : Vector3.up * (target.IsBoss ? 1f : 0.55f);
            hud.ShowFloatingText(target.transform.position + numberOffset, hit.Amount.ToString(), color, hit.Critical ? 1.5f : 1f);
            if (target.IsHero) hud.SetHeroHp(hit.HpAfter, run.Hero.MaxHp);
            if (target.IsBoss) hud.SetBossHp(hit.HpAfter);
            Effects.Burst(effectRoot, target.transform.position, hit.Critical ? CritColor : Color.white, hit.Critical ? 12 : 5, 2.5f);
            pixelCamera.Shake(hit.Critical ? 0.14f : 0.05f, hit.Critical ? 0.22f : 0.1f);
        }

        static string DescribeHit(ActorView attacker, ActorView target, DamageEvent hit)
        {
            string text = $"{Subject(attacker)} hit {Object(target)} for {hit.Amount}.";
            return hit.Critical ? "Critical! " + text : text;
        }

        static string Subject(ActorView view) => view.IsHero ? view.DisplayName : "The " + view.DisplayName;
        static string Object(ActorView view) => view.IsHero ? view.DisplayName : "the " + view.DisplayName;

        IEnumerator AnimateDeath(DiedEvent died)
        {
            if (!actors.TryGetValue(died.ActorId, out var view)) yield break;
            actors.Remove(died.ActorId);
            hud.AddMessage(view.IsHero ? $"{view.DisplayName} fainted..." : $"{Subject(view)} was defeated!",
                view.IsHero ? HeroHurtColor : view.IsBoss ? GoldColor : DungeonHud.TextColor);
            if (view.IsBoss)
            {
                ClearWarnings();
                hud.HideBoss();
                pixelCamera.Shake(0.2f, 0.6f);
                Effects.Burst(effectRoot, view.transform.position + Vector3.up * 0.3f, view.BurstColor, 40, 5f);
                Effects.Sparkle(effectRoot, view.transform.position, GoldColor);
            }
            else
            {
                Effects.Burst(effectRoot, view.transform.position, view.BurstColor, 16, 3.5f);
            }
            yield return view.Die();
            Destroy(view.gameObject);
        }

        /// <summary>The boss winds up: it crouches and trembles, and the tiles it will hit pulse red.</summary>
        IEnumerator AnimateCharge(DungeonRun run, int bossId)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            boss.SetCharging(true);
            ClearWarnings();
            var center = run.FindActor(bossId)?.Pos;
            if (center.HasValue)
            {
                for (int dy = -EnemyBrain.SlamRadius; dy <= EnemyBrain.SlamRadius; dy++)
                    for (int dx = -EnemyBrain.SlamRadius; dx <= EnemyBrain.SlamRadius; dx++)
                    {
                        var tile = new GridPos(center.Value.X + dx, center.Value.Y + dy);
                        if ((dx != 0 || dy != 0) && run.Map.IsWalkable(tile)) warnings.Add(Effects.WarningTile(effectRoot, TileCenter(tile)));
                    }
            }
            hud.AddMessage($"{Subject(boss)} is gathering its strength! Get away!", WarningColor);
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>The boss leaps and lands: shockwave, screen shake, then damage to everyone caught.</summary>
        IEnumerator AnimateSlam(DungeonRun run, int bossId, List<DamageEvent> hits)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            yield return boss.Leap(0.7f, 0.16f, 0.1f);
            ClearWarnings();
            pixelCamera.Shake(0.22f, 0.3f);
            Effects.Shockwave(effectRoot, boss.transform.position, BossBurstColor, 24, 3.5f);
            if (hits.Count == 0) hud.AddMessage($"{Subject(boss)}'s slam hit nothing!", DungeonHud.TextColor);
            foreach (var hit in hits)
            {
                if (!actors.TryGetValue(hit.TargetId, out var target)) continue;
                var away = (target.transform.position - boss.transform.position).normalized;
                ShowDamage(run, hit, away);
                hud.AddMessage($"{Subject(boss)}'s slam hit {Object(target)} for {hit.Amount}!", target.IsHero ? HeroHurtColor : DungeonHud.TextColor);
            }
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator AnimateSummon(int bossId)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            hud.AddMessage($"{Subject(boss)} called for help!", WarningColor);
            Effects.Sparkle(effectRoot, boss.transform.position, BossBurstColor);
            pixelCamera.Shake(0.06f, 0.2f);
            yield return new WaitForSeconds(0.3f);
        }

        void ClearWarnings()
        {
            foreach (var warning in warnings)
                if (warning != null) Destroy(warning);
            warnings.Clear();
            foreach (var view in actors.Values) view.SetCharging(false);
        }

        IEnumerator AnimateFloorChange(DungeonRun run)
        {
            yield return hud.Fade(1f, 0.35f);
            Rebuild(run);
            hud.Refresh(run);
            yield return new WaitForSeconds(0.15f);
            hud.ShowBanner(run.Config.Name, FloorTitle(run));
            if (run.IsBossFloor) hud.AddMessage("A powerful presence fills the air...", WarningColor);
            yield return hud.Fade(0f, 0.35f);
        }

        IEnumerator PopAndDestroy(SpriteRenderer renderer)
        {
            var start = renderer.transform.position;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                float k = t / 0.2f;
                renderer.transform.position = start + Vector3.up * (0.3f * k);
                renderer.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            Destroy(renderer.gameObject);
        }

        /// <summary>Snaps every view to the run's state after an action, so animation can never leave things out of place.</summary>
        void SyncToState(DungeonRun run)
        {
            foreach (var actor in run.Actors)
            {
                if (!actors.TryGetValue(actor.Id, out var view)) continue;
                view.Place(TileCenter(actor.Pos));
                view.SetHp(actor.Hp);
            }
        }

        // ---- Building ----

        ActorView AddActor(Actor actor)
        {
            bool isHero = actor.Team == Team.Hero;
            var sprite = SpriteLibrary.Get("Characters/" + actor.Definition.Id, isHero ? new Color(0.3f, 0.5f, 1f) : new Color(0.4f, 0.8f, 0.4f));
            var burstColor = isHero ? new Color(0.5f, 0.75f, 1f) : actor.Definition.IsBoss ? BossBurstColor : new Color(0.45f, 0.85f, 0.4f);
            var view = ActorView.Create(actorRoot, actor, sprite, SpriteLibrary.Get("Effects/shadow", new Color(0f, 0f, 0f, 0.3f)),
                squishy: !isHero, burstColor: burstColor);
            view.Place(TileCenter(actor.Pos));
            view.SetFacing(actor.Facing);
            actors[actor.Id] = view;
            return view;
        }

        void AddItem(FloorItem item)
        {
            var renderer = NewSprite(item.Kind.ToString(), itemRoot, SpriteLibrary.Get("Items/berry", Color.red), ItemOrder);
            renderer.transform.position = TileCenter(item.Pos);
            items[item.Id] = renderer;
        }

        void DrawTerrain(DungeonMap map)
        {
            terrain.ClearAllTiles();
            wallShadows.ClearAllTiles();
            var cells = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            var shadowCells = new List<Vector3Int>();

            for (int y = -WallPadding; y < map.Height + WallPadding; y++)
            {
                for (int x = -WallPadding; x < map.Width + WallPadding; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    cells.Add(cell);
                    if (map.IsWalkable(new GridPos(x, y)))
                    {
                        tiles.Add(FloorTileAt(x, y));
                        if (!map.IsWalkable(new GridPos(x, y + 1))) shadowCells.Add(cell);
                    }
                    else
                    {
                        // A wall with floor below shows its face; deeper walls show their rocky top.
                        tiles.Add(map.IsWalkable(new GridPos(x, y - 1)) ? wallFace : wallTop);
                    }
                }
            }
            terrain.SetTiles(cells.ToArray(), tiles.ToArray());
            var shadowTiles = new TileBase[shadowCells.Count];
            for (int i = 0; i < shadowTiles.Length; i++) shadowTiles[i] = wallShadow;
            wallShadows.SetTiles(shadowCells.ToArray(), shadowTiles);
        }

        /// <summary>Stable pseudo-random floor variant per tile, mostly plain with occasional detail.</summary>
        Tile FloorTileAt(int x, int y)
        {
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
            h = unchecked((h ^ (h >> 13)) * 0x5bd1e995u);
            h ^= h >> 15;
            uint roll = h % 10u;
            return floorTiles[roll < 6 ? 0 : roll < 8 ? 1 : roll < 9 ? 2 : 3];
        }

        Transform CreateChild(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            return child;
        }

        static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        static Tile MakeTile(string path, Color fallback)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SpriteLibrary.Get(path, fallback);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
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
