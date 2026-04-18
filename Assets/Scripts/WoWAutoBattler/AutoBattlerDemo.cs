using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WoWAutoBattler
{
    public sealed class AutoBattlerDemo : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExists()
        {
            if (FindFirstObjectByType<AutoBattlerDemo>() != null) return;
            var bootstrap = new GameObject("DemoBootstrap");
            bootstrap.AddComponent<AutoBattlerDemo>();
        }

        private enum Phase { Prep, Battle, Results }
        private enum SelectionMode { None, Bench, Board }

        private sealed class Tile
        {
            public int X;
            public int Y;
            public Vector3 Position;
            public Unit Unit;
            public bool IsPlayerZone => Y <= 2;
        }

        private sealed class Unit
        {
            public UnitDefinition Def;
            public bool IsPlayer;
            public int Star = 1;
            public float Hp;
            public float MaxHp;
            public float Ad;
            public float AtkSpeed;
            public float Range;
            public float MoveSpeed;
            public float MaxMana;
            public float Mana;
            public float Ap;
            public float Crit;
            public float Lifesteal;
            public float Regen;
            public float FlatReduce;
            public float StartShield;
            public float Shield;
            public float ManaGain = 20f;
            public float Cooldown;
            public Tile Tile;
            public GameObject View;
            public bool Alive => Hp > 0f;
            public string Label => Star + "* " + Def.DisplayName;
        }

        [SerializeField] private Vector2Int boardSize = new(7, 6);
        [SerializeField] private float tileSpacingX = 1.35f;
        [SerializeField] private float tileSpacingZ = 1.18f;
        [SerializeField] private int round = 1;
        [SerializeField] private int gold = 10;
        [SerializeField] private int health = 20;
        [SerializeField] private int refreshCost = 2;
        [SerializeField] private float battleSpeed = 1f;

        private readonly List<Tile> _tiles = new();
        private readonly List<TraitDefinition> _traits = new();
        private readonly List<UnitDefinition> _defs = new();
        private readonly Dictionary<string, UnitDefinition> _defById = new();
        private readonly List<Unit> _players = new();
        private readonly List<Unit> _enemies = new();
        private readonly List<UnitDefinition> _shop = new();
        private readonly Unit[] _bench = new Unit[8];
        private readonly Dictionary<string, int> _uiTraitLevels = new();

        private Phase _phase = Phase.Prep;
        private SelectionMode _selectionMode = SelectionMode.None;
        private int _selectedBench = -1;
        private Unit _selectedBoard;
        private Camera _cameraRef;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _smallStyle;
        private string _message = "Ready";

        private void Awake()
        {
            _traits.AddRange(DemoUnitLibrary.BuildTraits());
            _defs.AddRange(DemoUnitLibrary.BuildUnits());
            foreach (var def in _defs) _defById[def.Id] = def;
        }

        private void Start()
        {
            _cameraRef = Camera.main;
            BuildBoard();
            RollShop(true);
            SetupCamera();
        }

        private void Update()
        {
            if (_phase == Phase.Prep) HandleClicks();
            if (_phase == Phase.Battle) TickBattle(Time.deltaTime * battleSpeed);
        }

        private void BuildBoard()
        {
            for (var y = 0; y < boardSize.y; y++)
            {
                for (var x = 0; x < boardSize.x; x++)
                {
                    var pos = GetTilePosition(x, y);
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.transform.SetParent(transform);
                    go.transform.position = pos;
                    go.transform.localScale = new Vector3(0.95f, 0.08f, 0.95f);
                    var renderer = go.GetComponent<Renderer>();
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.color = y <= 2 ? new Color(0.22f, 0.39f, 0.60f) : new Color(0.56f, 0.31f, 0.24f);
                    var tag = go.AddComponent<BoardTileView>();
                    tag.X = x;
                    tag.Y = y;
                    _tiles.Add(new Tile { X = x, Y = y, Position = pos });
                }
            }
        }

        private void SetupCamera()
        {
            if (_cameraRef == null) return;
            if (_cameraRef.transform.position == Vector3.zero)
            {
                _cameraRef.transform.position = new Vector3(4f, 11f, -7f);
                _cameraRef.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }
        }

        private void SetupStyles()
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _titleStyle.normal.textColor = Color.white;
            _textStyle.normal.textColor = Color.white;
            _smallStyle.normal.textColor = Color.white;
        }

        private void HandleClicks()
        {
            if (_cameraRef == null) return;
            if (!TryGetPointerDown(out var pointerPosition)) return;
            var ray = _cameraRef.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out var hit)) return;

            var pickedUnit = FindUnit(hit.collider.gameObject);
            if (pickedUnit != null && pickedUnit.IsPlayer)
            {
                SelectBoard(pickedUnit);
                return;
            }

            var tileView = hit.collider.GetComponent<BoardTileView>();
            if (tileView == null) return;
            var tile = GetTile(tileView.X, tileView.Y);
            if (tile == null || !tile.IsPlayerZone)
            {
                _message = "Player tiles are the bottom 3 rows.";
                return;
            }

            if (_selectionMode == SelectionMode.Bench && _selectedBench >= 0)
            {
                var unit = _bench[_selectedBench];
                if (unit == null) { ClearSelection(); return; }
                if (tile.Unit == null) MoveBenchToTile(_selectedBench, tile);
                else SwapBenchAndBoard(_selectedBench, tile.Unit);
                return;
            }

            if (_selectionMode == SelectionMode.Board && _selectedBoard != null)
            {
                if (tile.Unit == null) MoveBoardToTile(_selectedBoard, tile);
                else if (tile.Unit != _selectedBoard) SwapBoardUnits(_selectedBoard, tile.Unit);
                return;
            }

            if (tile.Unit != null && tile.Unit.IsPlayer) SelectBoard(tile.Unit);
        }

        private bool TryGetPointerDown(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif
            pointerPosition = default;
            return false;
        }

        private Unit FindUnit(GameObject hit)
        {
            foreach (var unit in _players.Concat(_enemies))
            {
                if (unit.View == hit || (unit.View != null && hit.transform.IsChildOf(unit.View.transform))) return unit;
            }
            return null;
        }

        private Tile GetTile(int x, int y) => _tiles.FirstOrDefault(tile => tile.X == x && tile.Y == y);

        private Vector3 GetTilePosition(int x, int y)
        {
            var offset = y % 2 == 0 ? 0f : tileSpacingX * 0.5f;
            return new Vector3(x * tileSpacingX + offset, 0f, y * tileSpacingZ);
        }

        private void MoveBenchToTile(int benchIndex, Tile tile)
        {
            var unit = _bench[benchIndex];
            _bench[benchIndex] = null;
            PlaceOnTile(unit, tile);
            _message = unit.Label + " deployed";
            ClearSelection();
        }

        private void MoveBoardToTile(Unit unit, Tile tile)
        {
            if (unit.Tile != null) unit.Tile.Unit = null;
            PlaceOnTile(unit, tile);
            _message = unit.Label + " moved";
            ClearSelection();
        }

        private void PlaceOnTile(Unit unit, Tile tile)
        {
            if (unit.Tile != null) unit.Tile.Unit = null;
            tile.Unit = unit;
            unit.Tile = tile;
            unit.View.transform.position = tile.Position + Vector3.up * 0.65f;
        }

        private void SwapBenchAndBoard(int benchIndex, Unit boardUnit)
        {
            var benchUnit = _bench[benchIndex];
            var boardTile = boardUnit.Tile;
            _bench[benchIndex] = boardUnit;
            PositionBench(boardUnit, benchIndex);
            if (boardTile != null) boardTile.Unit = null;
            boardUnit.Tile = null;
            PlaceOnTile(benchUnit, boardTile);
            _message = "Swapped bench and board units";
            ClearSelection();
        }

        private void SwapBoardUnits(Unit a, Unit b)
        {
            var ta = a.Tile;
            var tb = b.Tile;
            ta.Unit = b;
            tb.Unit = a;
            a.Tile = tb;
            b.Tile = ta;
            a.View.transform.position = tb.Position + Vector3.up * 0.65f;
            b.View.transform.position = ta.Position + Vector3.up * 0.65f;
            _message = "Board units swapped";
            ClearSelection();
        }

        private void SelectBoard(Unit unit)
        {
            _selectionMode = SelectionMode.Board;
            _selectedBoard = unit;
            _selectedBench = -1;
            _message = unit.Label + " selected";
        }

        private void SelectBench(int index)
        {
            if (_bench[index] == null) return;
            _selectionMode = SelectionMode.Bench;
            _selectedBench = index;
            _selectedBoard = null;
            _message = _bench[index].Label + " selected";
        }

        private void ClearSelection()
        {
            _selectionMode = SelectionMode.None;
            _selectedBench = -1;
            _selectedBoard = null;
        }

        private void StartBattle()
        {
            if (_phase != Phase.Prep) return;
            var deployed = _players.Where(unit => unit.Tile != null).ToList();
            if (deployed.Count == 0)
            {
                _message = "Deploy at least one unit.";
                return;
            }

            SpawnWave();
            ApplyBonuses(deployed, true);
            ApplyBonuses(_enemies, false);
            _phase = Phase.Battle;
            _message = "Battle started";
            ClearSelection();
        }

        private void SpawnWave()
        {
            ClearEnemies();
            var entries = DemoUnitLibrary.BuildWave(round);
            var enemyTiles = _tiles.Where(tile => !tile.IsPlayerZone).OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToList();
            var index = 0;
            foreach (var entry in entries)
            {
                if (!_defById.TryGetValue(entry.UnitId, out var def)) continue;
                for (var i = 0; i < entry.Count && index < enemyTiles.Count; i++)
                {
                    var unit = CreateUnit(def, false);
                    _enemies.Add(unit);
                    PlaceOnTile(unit, enemyTiles[index++]);
                }
            }
        }

        private Unit CreateUnit(UnitDefinition def, bool isPlayer)
        {
            var unit = new Unit { Def = def, IsPlayer = isPlayer };
            ResetStats(unit);
            unit.View = CreateUnitView(unit);
            return unit;
        }

        private GameObject CreateUnitView(Unit unit)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.transform.SetParent(transform);
            root.transform.localScale = Vector3.one * (0.82f + 0.12f * (unit.Star - 1));
            var renderer = root.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = unit.IsPlayer ? unit.Def.PrimaryColor : unit.Def.PrimaryColor * 0.72f;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(root.transform);
            marker.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            marker.transform.localScale = Vector3.one * 0.25f;
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.material = new Material(Shader.Find("Standard"));
            markerRenderer.material.color = unit.IsPlayer ? Color.cyan : new Color(1f, 0.35f, 0.35f);
            return root;
        }

        private void TickBattle(float dt)
        {
            var alivePlayers = _players.Where(unit => unit.Alive && unit.Tile != null).ToList();
            var aliveEnemies = _enemies.Where(unit => unit.Alive && unit.Tile != null).ToList();
            if (alivePlayers.Count == 0 || aliveEnemies.Count == 0)
            {
                EndBattle(alivePlayers.Count > 0);
                return;
            }

            foreach (var unit in alivePlayers) TickUnit(unit, aliveEnemies, dt);
            foreach (var unit in aliveEnemies) TickUnit(unit, alivePlayers, dt);
        }

        private void TickUnit(Unit unit, List<Unit> enemies, float dt)
        {
            if (!unit.Alive || unit.Tile == null) return;
            unit.Cooldown -= dt;
            unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + unit.Regen * dt);
            var target = FindNearest(unit, enemies);
            if (target == null) return;

            var a = unit.View.transform.position;
            var b = target.View.transform.position;
            var dist = Vector3.Distance(a, b);

            if (unit.Mana >= unit.MaxMana && unit.Def.AbilityType != AbilityType.None)
            {
                CastAbility(unit, target, enemies);
                unit.Mana = 0f;
                return;
            }

            if (dist > unit.Range)
            {
                var direction = (b - a).normalized;
                direction.y = 0f;
                unit.View.transform.position += direction * unit.MoveSpeed * dt;
                return;
            }

            if (unit.Cooldown > 0f) return;
            var damage = UnityEngine.Random.value < unit.Crit ? unit.Ad * 1.6f : unit.Ad;
            DealDamage(unit, target, damage);
            unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + unit.ManaGain);
            unit.Cooldown = 1f / Mathf.Max(0.1f, unit.AtkSpeed);
        }

        private Unit FindNearest(Unit source, List<Unit> enemies)
        {
            var best = default(Unit);
            var bestDist = float.MaxValue;
            var origin = source.View.transform.position;
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive || enemy.Tile == null) continue;
                var dist = Vector3.Distance(origin, enemy.View.transform.position);
                if (dist < bestDist)
                {
                    best = enemy;
                    bestDist = dist;
                }
            }

            return best;
        }

        private void DealDamage(Unit source, Unit target, float rawDamage)
        {
            var damage = Mathf.Max(1f, rawDamage - target.FlatReduce);
            if (target.Shield > 0f)
            {
                var absorbed = Mathf.Min(target.Shield, damage);
                target.Shield -= absorbed;
                damage -= absorbed;
            }

            if (damage > 0f)
            {
                target.Hp -= damage;
                if (source.Lifesteal > 0f) source.Hp = Mathf.Min(source.MaxHp, source.Hp + damage * source.Lifesteal);
            }

            if (target.Hp <= 0f) Kill(target);
        }

        private void CastAbility(Unit caster, Unit target, List<Unit> enemies)
        {
            switch (caster.Def.AbilityType)
            {
                case AbilityType.ArcaneBurst: DamageClosest(caster, enemies, 2.8f, 30f + caster.Ap, 3); break;
                case AbilityType.ChainLightning: DamageChain(caster, enemies, 34f + caster.Ap, 3); break;
                case AbilityType.HolyLight: HealLowest(caster, 35f + caster.Ap); break;
                case AbilityType.Whirlwind: DamageNearby(caster, enemies, 2.0f, 28f + caster.Ap); break;
                case AbilityType.ShadowStrike: DealDamage(caster, target, 48f + caster.Ap); break;
                case AbilityType.FrostNova: DamageNearby(caster, enemies, 2.4f, 26f + caster.Ap); break;
                case AbilityType.Starfall: DamageRandom(caster, enemies, 3, 24f + caster.Ap); break;
                case AbilityType.ShieldSlam:
                    caster.Shield += 20f + caster.Ap * 0.5f;
                    DealDamage(caster, target, 24f + caster.Ap);
                    break;
            }

            if (caster.Def.Traits.Contains("druid")) caster.Hp = Mathf.Min(caster.MaxHp, caster.Hp + GetDruidBonus());
        }

        private void DamageClosest(Unit caster, List<Unit> enemies, float radius, float damage, int maxTargets)
        {
            foreach (var enemy in enemies.Where(unit => unit.Alive && unit.Tile != null)
                         .OrderBy(unit => Vector3.Distance(caster.View.transform.position, unit.View.transform.position))
                         .Take(maxTargets))
            {
                if (Vector3.Distance(caster.View.transform.position, enemy.View.transform.position) <= radius) DealDamage(caster, enemy, damage);
            }
        }

        private void DamageChain(Unit caster, List<Unit> enemies, float damage, int jumps)
        {
            var current = damage;
            foreach (var enemy in enemies.Where(unit => unit.Alive && unit.Tile != null)
                         .OrderBy(unit => Vector3.Distance(caster.View.transform.position, unit.View.transform.position))
                         .Take(jumps))
            {
                DealDamage(caster, enemy, current);
                current *= 0.75f;
            }
        }

        private void DamageNearby(Unit caster, List<Unit> enemies, float radius, float damage)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.Alive && enemy.Tile != null && Vector3.Distance(caster.View.transform.position, enemy.View.transform.position) <= radius)
                    DealDamage(caster, enemy, damage);
            }
        }

        private void DamageRandom(Unit caster, List<Unit> enemies, int hits, float damage)
        {
            foreach (var enemy in enemies.Where(unit => unit.Alive && unit.Tile != null).OrderBy(_ => UnityEngine.Random.value).Take(hits))
                DealDamage(caster, enemy, damage);
        }

        private void HealLowest(Unit caster, float amount)
        {
            var team = caster.IsPlayer ? _players : _enemies;
            var ally = team.Where(unit => unit.Alive && unit.Tile != null).OrderBy(unit => unit.Hp / unit.MaxHp).FirstOrDefault();
            if (ally == null) return;
            ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + amount);
            ally.Shield += amount * 0.15f;
        }

        private void Kill(Unit unit)
        {
            unit.Hp = 0f;
            if (unit.Tile != null)
            {
                unit.Tile.Unit = null;
                unit.Tile = null;
            }

            if (unit.View != null) unit.View.SetActive(false);
        }

        private void EndBattle(bool playerWon)
        {
            _phase = Phase.Results;
            if (playerWon)
            {
                gold += 5;
                _message = "Victory";
            }
            else
            {
                health -= 2;
                gold += 3;
                _message = "Defeat";
            }
        }

        private void NextRound()
        {
            if (_phase != Phase.Results) return;
            round++;
            _phase = Phase.Prep;
            RollShop(true);
            ResetPlayersAfterBattle();
            ClearEnemies();
            _message = "Next prep phase";
        }

        private void ResetPlayersAfterBattle()
        {
            foreach (var unit in _players)
            {
                if (unit.Tile == null) continue;
                ResetStats(unit);
                unit.View.SetActive(true);
                unit.View.transform.position = unit.Tile.Position + Vector3.up * 0.65f;
            }
        }

        private void ClearEnemies()
        {
            foreach (var unit in _enemies)
            {
                if (unit.View != null) Destroy(unit.View);
            }

            _enemies.Clear();
            foreach (var tile in _tiles.Where(tile => !tile.IsPlayerZone)) tile.Unit = null;
        }

        private void ResetStats(Unit unit)
        {
            var scale = 1f + (unit.Star - 1) * 0.8f;
            unit.MaxHp = unit.Def.MaxHealth * scale;
            unit.Hp = unit.MaxHp;
            unit.Ad = unit.Def.AttackDamage * (1f + (unit.Star - 1) * 0.55f);
            unit.AtkSpeed = unit.Def.AttackSpeed;
            unit.Range = unit.Def.AttackRange;
            unit.MoveSpeed = unit.Def.MoveSpeed;
            unit.MaxMana = unit.Def.MaxMana;
            unit.Mana = 0f;
            unit.Ap = unit.Def.AbilityPower * scale;
            unit.Crit = 0f;
            unit.Lifesteal = 0f;
            unit.Regen = 0f;
            unit.FlatReduce = 0f;
            unit.StartShield = 0f;
            unit.Shield = 0f;
            unit.ManaGain = 20f;
            unit.Cooldown = 0f;
            if (unit.View != null) unit.View.transform.localScale = Vector3.one * (0.82f + 0.12f * (unit.Star - 1));
        }

        private void ApplyBonuses(List<Unit> team, bool storeUiTraits)
        {
            foreach (var unit in team) ResetStats(unit);
            var counts = new Dictionary<string, int>();
            foreach (var unit in team)
            {
                foreach (var trait in unit.Def.Traits)
                {
                    counts[trait] = counts.TryGetValue(trait, out var value) ? value + 1 : 1;
                }
            }

            if (storeUiTraits) _uiTraitLevels.Clear();

            foreach (var unit in team)
            {
                foreach (var trait in unit.Def.Traits)
                {
                    var level = GetTraitLevel(trait, counts.TryGetValue(trait, out var count) ? count : 0);
                    if (storeUiTraits && level > 0) _uiTraitLevels[trait] = level;
                    ApplyTrait(unit, trait, level);
                }
                unit.Shield += unit.StartShield;
            }
        }

        private int GetTraitLevel(string traitId, int count)
        {
            var trait = _traits.FirstOrDefault(item => item.Id == traitId);
            if (trait == null || trait.Thresholds == null) return 0;
            var level = 0;
            for (var i = 0; i < trait.Thresholds.Length; i++)
            {
                if (count >= trait.Thresholds[i]) level = i + 1;
            }
            return level;
        }

        private void ApplyTrait(Unit unit, string traitId, int level)
        {
            if (level <= 0) return;
            switch (traitId)
            {
                case "alliance": unit.StartShield += level == 1 ? 22f : 50f; break;
                case "horde": unit.Ad += level == 1 ? 6f : 14f; break;
                case "scourge": unit.Lifesteal += level == 1 ? 0.12f : 0.24f; break;
                case "cenarion": unit.Regen += level == 1 ? 2f : 5f; break;
                case "titanforged": unit.MaxHp += level == 1 ? 45f : 100f; unit.Hp += level == 1 ? 45f : 100f; break;
                case "warrior": unit.FlatReduce += level == 1 ? 2f : 5f; break;
                case "mage": unit.Ap += level == 1 ? 10f : 24f; unit.Mana += level == 1 ? 15f : 35f; break;
                case "shaman": unit.ManaGain += level == 1 ? 10f : 25f; break;
                case "rogue": unit.Crit += level == 1 ? 0.15f : 0.35f; break;
                case "paladin": unit.StartShield += level == 1 ? 28f : 64f; break;
                case "hunter": unit.Range += level == 1 ? 0.8f : 1.6f; break;
                case "druid": unit.Ap += level == 1 ? 8f : 18f; break;
            }
        }

        private float GetDruidBonus() => _uiTraitLevels.TryGetValue("druid", out var level) && level >= 2 ? 20f : 8f;

        private void RollShop(bool freeRoll)
        {
            if (!freeRoll && gold < refreshCost)
            {
                _message = "Not enough gold";
                return;
            }

            if (!freeRoll) gold -= refreshCost;
            _shop.Clear();
            for (var i = 0; i < 5; i++) _shop.Add(_defs[UnityEngine.Random.Range(0, _defs.Count)]);
            _message = freeRoll ? "Shop ready" : "Shop refreshed";
        }

        private void BuyFromShop(int index)
        {
            if (_phase != Phase.Prep || index < 0 || index >= _shop.Count) return;
            var offer = _shop[index];
            if (offer == null) return;
            if (gold < offer.Cost) { _message = "Not enough gold"; return; }
            var benchIndex = FirstEmptyBench();
            if (benchIndex < 0) { _message = "Bench full"; return; }

            gold -= offer.Cost;
            var unit = CreateUnit(offer, true);
            _players.Add(unit);
            _bench[benchIndex] = unit;
            PositionBench(unit, benchIndex);
            _shop[index] = null;
            TryPromotions();
            _message = offer.DisplayName + " bought";
        }

        private int FirstEmptyBench()
        {
            for (var i = 0; i < _bench.Length; i++) if (_bench[i] == null) return i;
            return -1;
        }

        private void PositionBench(Unit unit, int benchIndex)
        {
            var x = -0.25f + benchIndex * 1.15f;
            unit.View.transform.position = new Vector3(x, 0.65f, -1.4f);
            unit.Tile = null;
            unit.View.SetActive(true);
        }

        private void TryPromotions()
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var group in _players.GroupBy(unit => new { unit.Def.Id, unit.Star }).ToList())
                {
                    var trio = group.Take(3).ToList();
                    if (trio.Count < 3) continue;
                    Promote(trio);
                    changed = true;
                    break;
                }
            }
        }

        private void Promote(List<Unit> trio)
        {
            var anchor = trio[0];
            var anchorTile = anchor.Tile;
            var anchorBench = Array.IndexOf(_bench, anchor);

            for (var i = 0; i < trio.Count; i++) RemovePlayerUnit(trio[i], i == 0);
            anchor.Star++;
            ResetStats(anchor);
            anchor.View.SetActive(true);

            if (anchorTile != null) PlaceOnTile(anchor, anchorTile);
            else
            {
                if (anchorBench < 0) anchorBench = FirstEmptyBench();
                if (anchorBench >= 0)
                {
                    _bench[anchorBench] = anchor;
                    PositionBench(anchor, anchorBench);
                }
            }

            if (!_players.Contains(anchor)) _players.Add(anchor);
            _message = anchor.Def.DisplayName + " promoted to " + anchor.Star + " star";
        }

        private void RemovePlayerUnit(Unit unit, bool keepView)
        {
            var benchIndex = Array.IndexOf(_bench, unit);
            if (benchIndex >= 0) _bench[benchIndex] = null;
            if (unit.Tile != null)
            {
                unit.Tile.Unit = null;
                unit.Tile = null;
            }

            _players.Remove(unit);
            if (keepView) unit.View.SetActive(false);
            else Destroy(unit.View);
        }

        private void ReturnSelectedToBench()
        {
            if (_selectionMode != SelectionMode.Board || _selectedBoard == null)
            {
                _message = "Select a board unit first";
                return;
            }

            var benchIndex = FirstEmptyBench();
            if (benchIndex < 0)
            {
                _message = "Bench full";
                return;
            }

            _selectedBoard.Tile.Unit = null;
            _selectedBoard.Tile = null;
            _bench[benchIndex] = _selectedBoard;
            PositionBench(_selectedBoard, benchIndex);
            ClearSelection();
            _message = "Returned to bench";
        }

        private string SelectedText()
        {
            if (_selectionMode == SelectionMode.Bench && _selectedBench >= 0 && _bench[_selectedBench] != null) return _bench[_selectedBench].Label + " (bench)";
            if (_selectionMode == SelectionMode.Board && _selectedBoard != null) return _selectedBoard.Label + " (board)";
            return "None";
        }

        private Dictionary<string, int> CurrentTraitCounts()
        {
            var map = new Dictionary<string, int>();
            foreach (var unit in _players.Where(unit => unit.Tile != null))
            {
                foreach (var trait in unit.Def.Traits)
                {
                    map[trait] = map.TryGetValue(trait, out var count) ? count + 1 : 1;
                }
            }
            return map;
        }

        private void OnGUI()
        {
            if (_titleStyle == null) SetupStyles();

            GUI.Box(new Rect(12, 12, 320, 175), string.Empty);
            GUI.Label(new Rect(24, 22, 240, 24), "WoW Style Auto Battler", _titleStyle);
            GUI.Label(new Rect(24, 52, 260, 20), "Round: " + round, _textStyle);
            GUI.Label(new Rect(24, 74, 260, 20), "Phase: " + _phase, _textStyle);
            GUI.Label(new Rect(24, 96, 260, 20), "Gold: " + gold, _textStyle);
            GUI.Label(new Rect(24, 118, 260, 20), "Health: " + health, _textStyle);
            GUI.Label(new Rect(24, 142, 290, 24), _message, _smallStyle);

            GUI.Box(new Rect(Screen.width - 308, 12, 296, 250), string.Empty);
            GUI.Label(new Rect(Screen.width - 296, 22, 220, 24), "Active Traits", _titleStyle);
            var counts = CurrentTraitCounts();
            var row = 0;
            foreach (var trait in _traits)
            {
                if (!counts.TryGetValue(trait.Id, out var count)) continue;
                var level = GetTraitLevel(trait.Id, count);
                if (level <= 0) continue;
                GUI.Label(new Rect(Screen.width - 296, 52 + row * 28, 270, 24), trait.DisplayName + " " + count + " - Tier " + level, _textStyle);
                row++;
            }
            if (row == 0) GUI.Label(new Rect(Screen.width - 296, 52, 270, 24), "Deploy units to activate traits.", _smallStyle);

            GUI.Box(new Rect(12, Screen.height - 230, Screen.width - 24, 218), string.Empty);
            GUI.Label(new Rect(24, Screen.height - 220, 160, 24), "Bench", _titleStyle);
            GUI.Label(new Rect(24, Screen.height - 192, 420, 22), "Selected: " + SelectedText(), _textStyle);

            for (var i = 0; i < _bench.Length; i++)
            {
                var x = 24 + i * 118;
                var y = Screen.height - 164;
                var label = _bench[i] == null ? "[" + (i + 1) + "] Empty" : _bench[i].Label + "\n" + string.Join("/", _bench[i].Def.Traits);
                if (GUI.Button(new Rect(x, y, 110, 52), label)) SelectBench(i);
            }

            GUI.Label(new Rect(24, Screen.height - 102, 160, 24), "Shop", _titleStyle);
            for (var i = 0; i < 5; i++)
            {
                var x = 24 + i * 150;
                var y = Screen.height - 74;
                var offer = i < _shop.Count ? _shop[i] : null;
                var text = offer == null ? "Sold" : offer.DisplayName + "\n" + offer.Cost + "G / " + string.Join("/", offer.Traits);
                if (GUI.Button(new Rect(x, y, 140, 54), text)) BuyFromShop(i);
            }

            var controlX = Screen.width - 478;
            if (GUI.Button(new Rect(controlX, Screen.height - 168, 140, 34), "Start Battle")) StartBattle();
            if (GUI.Button(new Rect(controlX + 150, Screen.height - 168, 140, 34), "Refresh Shop (" + refreshCost + "G)")) RollShop(false);
            if (GUI.Button(new Rect(controlX, Screen.height - 124, 140, 34), "Return To Bench")) ReturnSelectedToBench();
            if (GUI.Button(new Rect(controlX + 150, Screen.height - 124, 140, 34), "Clear Selection"))
            {
                ClearSelection();
                _message = "Selection cleared";
            }
            if (GUI.Button(new Rect(controlX, Screen.height - 80, 290, 34), "Next Round")) NextRound();
        }
    }
}
