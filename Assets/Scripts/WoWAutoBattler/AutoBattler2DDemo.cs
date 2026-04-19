using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WoWAutoBattler
{
    public sealed class AutoBattler2DDemo : MonoBehaviour
    {
        private enum Phase { Prep, Battle, Results }
        private enum SelectionMode { None, Bench, Board }

        private sealed class Tile
        {
            public int X;
            public int Y;
            public Vector2 Position;
            public SpriteRenderer Renderer;
            public RuntimeUnit Occupant;
            public bool IsPlayerZone => Y <= 2;
        }

        private sealed class RuntimeUnit
        {
            public UnitDefinition Definition;
            public bool IsPlayer;
            public int Star = 1;
            public float Hp;
            public float MaxHp;
            public float Ad;
            public float AttackSpeed;
            public float Range;
            public float MoveSpeed;
            public float MaxMana;
            public float Mana;
            public float Ap;
            public float CritChance;
            public float Lifesteal;
            public float Regen;
            public float DamageReduction;
            public float StartShield;
            public float Shield;
            public float ManaGain = 20f;
            public float Cooldown;
            public Tile Tile;
            public GameObject View;
            public SpriteRenderer BodyRenderer;
            public SpriteRenderer HealthFillRenderer;
            public SpriteRenderer ManaFillRenderer;
            public bool IsAlive => Hp > 0f;
            public string Label => Star + "* " + Definition.DisplayName;
        }

        [SerializeField] private Vector2Int boardSize = new(7, 6);
        [SerializeField] private float tileSpacingX = 1.25f;
        [SerializeField] private float tileSpacingY = 1.05f;
        [SerializeField] private int round = 1;
        [SerializeField] private int gold = 10;
        [SerializeField] private int health = 20;
        [SerializeField] private int refreshCost = 2;
        [SerializeField] private float battleSpeed = 1f;

        private readonly List<Tile> _tiles = new();
        private readonly List<TraitDefinition> _traits = new();
        private readonly List<UnitDefinition> _definitions = new();
        private readonly Dictionary<string, UnitDefinition> _definitionById = new();
        private readonly List<RuntimeUnit> _playerUnits = new();
        private readonly List<RuntimeUnit> _enemyUnits = new();
        private readonly List<UnitDefinition> _shopOffers = new();
        private readonly RuntimeUnit[] _bench = new RuntimeUnit[8];
        private readonly Dictionary<string, int> _uiTraitLevels = new();

        private Camera _cameraRef;
        private Sprite _quadSprite;
        private Phase _phase = Phase.Prep;
        private SelectionMode _selectionMode = SelectionMode.None;
        private int _selectedBenchIndex = -1;
        private RuntimeUnit _selectedBoardUnit;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _smallStyle;
        private string _message = "2D 데모 준비 완료";

        private static readonly Color PlayerTileColor = new(0.16f, 0.31f, 0.48f, 1f);
        private static readonly Color EnemyTileColor = new(0.40f, 0.21f, 0.19f, 1f);
        private static readonly Color BenchStripColor = new(0.12f, 0.12f, 0.14f, 0.85f);
        private static readonly Color SelectionColor = new(1f, 0.92f, 0.45f, 1f);

        private void Awake()
        {
            _traits.AddRange(DemoUnitLibrary.BuildTraits());
            _definitions.AddRange(DemoUnitLibrary.BuildUnits());
            foreach (var definition in _definitions) _definitionById[definition.Id] = definition;
        }

        private void Start()
        {
            _cameraRef = Camera.main;
            _quadSprite = CreateQuadSprite();
            SetupCamera();
            BuildBoard();
            BuildBenchBackdrop();
            RollShop(true);
        }

        private void Update()
        {
            if (_phase == Phase.Prep) HandlePointer();
            if (_phase == Phase.Battle) TickBattle(Time.deltaTime * battleSpeed);
            UpdateUnitBars();
        }

        private void SetupCamera()
        {
            if (_cameraRef == null)
            {
                var cameraObject = new GameObject("Main Camera");
                _cameraRef = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _cameraRef.orthographic = true;
            _cameraRef.orthographicSize = 5.8f;
            _cameraRef.transform.position = new Vector3(4.2f, 2.2f, -10f);
            _cameraRef.backgroundColor = new Color(0.07f, 0.08f, 0.12f, 1f);
            _cameraRef.clearFlags = CameraClearFlags.SolidColor;
        }

        private void BuildBoard()
        {
            for (var y = 0; y < boardSize.y; y++)
            {
                for (var x = 0; x < boardSize.x; x++)
                {
                    var position = GetTilePosition(x, y);
                    var tileObject = new GameObject($"Tile_{x}_{y}");
                    tileObject.transform.SetParent(transform);
                    tileObject.transform.position = new Vector3(position.x, position.y, 0f);

                    var renderer = tileObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = _quadSprite;
                    renderer.drawMode = SpriteDrawMode.Sliced;
                    renderer.size = new Vector2(1.08f, 0.92f);
                    renderer.color = y <= 2 ? PlayerTileColor : EnemyTileColor;
                    renderer.sortingOrder = 0;

                    tileObject.AddComponent<BoxCollider2D>().size = new Vector2(1.08f, 0.92f);

                    var tileView = tileObject.AddComponent<BoardTileView>();
                    tileView.X = x;
                    tileView.Y = y;

                    _tiles.Add(new Tile { X = x, Y = y, Position = position, Renderer = renderer });
                }
            }
        }

        private void BuildBenchBackdrop()
        {
            var benchObject = new GameObject("BenchStrip");
            benchObject.transform.SetParent(transform);
            benchObject.transform.position = new Vector3(3.7f, -2.1f, 0f);
            var renderer = benchObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _quadSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(9.7f, 1.25f);
            renderer.color = BenchStripColor;
            renderer.sortingOrder = -2;
        }

        private void HandlePointer()
        {
            if (_cameraRef == null) return;
            if (!TryGetPointerDown(out var screenPosition)) return;

            var world = _cameraRef.ScreenToWorldPoint(screenPosition);
            var hitPoint = new Vector2(world.x, world.y);
            var hits = Physics2D.OverlapPointAll(hitPoint);
            if (hits == null || hits.Length == 0) return;

            foreach (var hit in hits)
            {
                var runtimeUnit = FindUnitByCollider(hit);
                if (runtimeUnit != null && runtimeUnit.IsPlayer)
                {
                    SelectBoardUnit(runtimeUnit);
                    return;
                }

                var tileView = hit.GetComponent<BoardTileView>();
                if (tileView != null)
                {
                    HandleTileClick(tileView.X, tileView.Y);
                    return;
                }
            }
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

        private RuntimeUnit FindUnitByCollider(Collider2D collider)
        {
            foreach (var unit in _playerUnits.Concat(_enemyUnits))
            {
                if (unit.View == null) continue;
                if (collider.gameObject == unit.View || collider.transform.IsChildOf(unit.View.transform)) return unit;
            }

            return null;
        }

        private void HandleTileClick(int x, int y)
        {
            var tile = GetTile(x, y);
            if (tile == null || !tile.IsPlayerZone)
            {
                _message = "하단 3줄이 내 배치 구역입니다.";
                return;
            }

            if (_selectionMode == SelectionMode.Bench && _selectedBenchIndex >= 0)
            {
                var unit = _bench[_selectedBenchIndex];
                if (unit == null)
                {
                    ClearSelection();
                    return;
                }

                if (tile.Occupant == null) DeployFromBench(_selectedBenchIndex, tile);
                else SwapBenchAndBoard(_selectedBenchIndex, tile.Occupant);
                return;
            }

            if (_selectionMode == SelectionMode.Board && _selectedBoardUnit != null)
            {
                if (tile.Occupant == null) MoveBoardUnit(_selectedBoardUnit, tile);
                else if (tile.Occupant != _selectedBoardUnit) SwapBoardUnits(_selectedBoardUnit, tile.Occupant);
                return;
            }

            if (tile.Occupant != null && tile.Occupant.IsPlayer) SelectBoardUnit(tile.Occupant);
        }

        private Tile GetTile(int x, int y) => _tiles.FirstOrDefault(tile => tile.X == x && tile.Y == y);

        private Vector2 GetTilePosition(int x, int y)
        {
            var offset = y % 2 == 0 ? 0f : tileSpacingX * 0.5f;
            return new Vector2(x * tileSpacingX + offset, y * tileSpacingY);
        }

        private void DeployFromBench(int benchIndex, Tile tile)
        {
            var unit = _bench[benchIndex];
            _bench[benchIndex] = null;
            PlaceOnTile(unit, tile);
            _message = unit.Label + " 배치 완료";
            ClearSelection();
        }

        private void MoveBoardUnit(RuntimeUnit unit, Tile tile)
        {
            if (unit.Tile != null) unit.Tile.Occupant = null;
            PlaceOnTile(unit, tile);
            _message = unit.Label + " 이동 완료";
            ClearSelection();
        }

        private void PlaceOnTile(RuntimeUnit unit, Tile tile)
        {
            if (unit.Tile != null) unit.Tile.Occupant = null;
            tile.Occupant = unit;
            unit.Tile = tile;
            unit.View.transform.position = new Vector3(tile.Position.x, tile.Position.y + 0.1f, 0f);
        }

        private void SwapBenchAndBoard(int benchIndex, RuntimeUnit boardUnit)
        {
            var benchUnit = _bench[benchIndex];
            var boardTile = boardUnit.Tile;
            _bench[benchIndex] = boardUnit;
            PositionBenchUnit(boardUnit, benchIndex);
            if (boardTile != null) boardTile.Occupant = null;
            boardUnit.Tile = null;
            PlaceOnTile(benchUnit, boardTile);
            _message = "벤치 유닛과 보드 유닛을 교체했습니다.";
            ClearSelection();
        }

        private void SwapBoardUnits(RuntimeUnit a, RuntimeUnit b)
        {
            var tileA = a.Tile;
            var tileB = b.Tile;
            tileA.Occupant = b;
            tileB.Occupant = a;
            a.Tile = tileB;
            b.Tile = tileA;
            a.View.transform.position = new Vector3(tileB.Position.x, tileB.Position.y + 0.1f, 0f);
            b.View.transform.position = new Vector3(tileA.Position.x, tileA.Position.y + 0.1f, 0f);
            _message = "Board units swapped";
            ClearSelection();
        }

        private void SelectBoardUnit(RuntimeUnit unit)
        {
            _selectionMode = SelectionMode.Board;
            _selectedBoardUnit = unit;
            _selectedBenchIndex = -1;
            _message = unit.Label + " 선택";
            RefreshSelectionHighlights();
        }

        private void SelectBenchUnit(int index)
        {
            if (_bench[index] == null) return;
            _selectionMode = SelectionMode.Bench;
            _selectedBenchIndex = index;
            _selectedBoardUnit = null;
            _message = _bench[index].Label + " 선택";
            RefreshSelectionHighlights();
        }

        private void ClearSelection()
        {
            _selectionMode = SelectionMode.None;
            _selectedBenchIndex = -1;
            _selectedBoardUnit = null;
            RefreshSelectionHighlights();
        }

        private void RefreshSelectionHighlights()
        {
            foreach (var unit in _playerUnits.Concat(_enemyUnits))
            {
                if (unit.BodyRenderer == null) continue;
                unit.BodyRenderer.color = GetUnitBaseColor(unit);
            }

            if (_selectionMode == SelectionMode.Board && _selectedBoardUnit?.BodyRenderer != null)
            {
                _selectedBoardUnit.BodyRenderer.color = SelectionColor;
            }
        }

        private void StartBattle()
        {
            if (_phase != Phase.Prep) return;
            var deployed = _playerUnits.Where(unit => unit.Tile != null).ToList();
            if (deployed.Count == 0)
            {
                _message = "최소 1명의 유닛을 배치하세요.";
                return;
            }

            SpawnWave();
            ApplyBonuses(deployed, true);
            ApplyBonuses(_enemyUnits, false);
            _phase = Phase.Battle;
            _message = "전투 시작";
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
                if (!_definitionById.TryGetValue(entry.UnitId, out var definition)) continue;
                for (var i = 0; i < entry.Count && index < enemyTiles.Count; i++)
                {
                    var unit = CreateRuntimeUnit(definition, false);
                    _enemyUnits.Add(unit);
                    PlaceOnTile(unit, enemyTiles[index++]);
                }
            }
        }

        private RuntimeUnit CreateRuntimeUnit(UnitDefinition definition, bool isPlayer)
        {
            var unit = new RuntimeUnit { Definition = definition, IsPlayer = isPlayer };
            ResetStats(unit);
            unit.View = CreateUnitView(unit);
            unit.BodyRenderer = unit.View.GetComponent<SpriteRenderer>();
            return unit;
        }

        private GameObject CreateUnitView(RuntimeUnit unit)
        {
            var root = new GameObject(unit.Definition.DisplayName + (unit.IsPlayer ? "_Player2D" : "_Enemy2D"));
            root.transform.SetParent(transform);

            var body = root.AddComponent<SpriteRenderer>();
            body.sprite = _quadSprite;
            body.drawMode = SpriteDrawMode.Sliced;
            body.size = new Vector2(0.72f, 0.72f);
            body.color = GetUnitBaseColor(unit);
            body.sortingOrder = 20;

            root.AddComponent<CircleCollider2D>().radius = 0.36f;

            var badge = new GameObject("Badge");
            badge.transform.SetParent(root.transform);
            badge.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            var badgeRenderer = badge.AddComponent<SpriteRenderer>();
            badgeRenderer.sprite = _quadSprite;
            badgeRenderer.drawMode = SpriteDrawMode.Sliced;
            badgeRenderer.size = new Vector2(0.26f, 0.18f);
            badgeRenderer.color = unit.IsPlayer ? new Color(0.70f, 0.95f, 1f, 1f) : new Color(1f, 0.54f, 0.46f, 1f);
            badgeRenderer.sortingOrder = 21;

            var healthBack = CreateBar("HealthBack", root.transform, new Vector3(0f, 0.53f, 0f), new Vector2(0.78f, 0.08f), new Color(0.16f, 0.12f, 0.12f, 0.92f), 22);
            var healthFill = CreateBar("HealthFill", healthBack.transform, new Vector3(0f, 0f, 0f), new Vector2(0.74f, 0.06f), new Color(0.28f, 0.90f, 0.32f, 1f), 23);
            var manaBack = CreateBar("ManaBack", root.transform, new Vector3(0f, 0.42f, 0f), new Vector2(0.78f, 0.06f), new Color(0.10f, 0.13f, 0.22f, 0.92f), 22);
            var manaFill = CreateBar("ManaFill", manaBack.transform, new Vector3(0f, 0f, 0f), new Vector2(0.74f, 0.04f), new Color(0.24f, 0.66f, 1f, 1f), 23);
            unit.HealthFillRenderer = healthFill;
            unit.ManaFillRenderer = manaFill;

            return root;
        }

        private SpriteRenderer CreateBar(string name, Transform parent, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _quadSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private Color GetUnitBaseColor(RuntimeUnit unit)
        {
            return unit.IsPlayer ? unit.Definition.PrimaryColor : unit.Definition.PrimaryColor * 0.72f;
        }

        private void TickBattle(float deltaTime)
        {
            var alivePlayers = _playerUnits.Where(unit => unit.IsAlive && unit.Tile != null).ToList();
            var aliveEnemies = _enemyUnits.Where(unit => unit.IsAlive && unit.Tile != null).ToList();
            if (alivePlayers.Count == 0 || aliveEnemies.Count == 0)
            {
                EndBattle(alivePlayers.Count > 0);
                return;
            }

            foreach (var unit in alivePlayers) TickUnit(unit, aliveEnemies, deltaTime);
            foreach (var unit in aliveEnemies) TickUnit(unit, alivePlayers, deltaTime);
        }

        private void TickUnit(RuntimeUnit unit, List<RuntimeUnit> enemies, float deltaTime)
        {
            if (!unit.IsAlive || unit.View == null || unit.Tile == null) return;
            unit.Cooldown -= deltaTime;
            unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + unit.Regen * deltaTime);

            var target = FindNearestTarget(unit, enemies);
            if (target == null) return;

            var current = (Vector2)unit.View.transform.position;
            var targetPosition = (Vector2)target.View.transform.position;
            var distance = Vector2.Distance(current, targetPosition);

            if (unit.Mana >= unit.MaxMana && unit.Definition.AbilityType != AbilityType.None)
            {
                CastAbility(unit, target, enemies);
                unit.Mana = 0f;
                return;
            }

            if (distance > unit.Range)
            {
                var direction = (targetPosition - current).normalized;
                unit.View.transform.position += (Vector3)(direction * unit.MoveSpeed * deltaTime);
                return;
            }

            if (unit.Cooldown > 0f) return;
            var damage = UnityEngine.Random.value < unit.CritChance ? unit.Ad * 1.6f : unit.Ad;
            DealDamage(unit, target, damage);
            unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + unit.ManaGain);
            unit.Cooldown = 1f / Mathf.Max(0.1f, unit.AttackSpeed);
        }

        private RuntimeUnit FindNearestTarget(RuntimeUnit source, List<RuntimeUnit> enemies)
        {
            var nearest = default(RuntimeUnit);
            var shortest = float.MaxValue;
            var sourcePosition = (Vector2)source.View.transform.position;
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive || enemy.Tile == null) continue;
                var distance = Vector2.Distance(sourcePosition, enemy.View.transform.position);
                if (distance < shortest)
                {
                    nearest = enemy;
                    shortest = distance;
                }
            }

            return nearest;
        }

        private void DealDamage(RuntimeUnit source, RuntimeUnit target, float rawDamage)
        {
            var damage = Mathf.Max(1f, rawDamage - target.DamageReduction);
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

            if (target.Hp <= 0f) KillUnit(target);
        }

        private void CastAbility(RuntimeUnit caster, RuntimeUnit target, List<RuntimeUnit> enemies)
        {
            switch (caster.Definition.AbilityType)
            {
                case AbilityType.ArcaneBurst: DamageClosest(caster, enemies, 2.8f, 30f + caster.Ap, 3); break;
                case AbilityType.ChainLightning: DamageChain(caster, enemies, 34f + caster.Ap, 3); break;
                case AbilityType.HolyLight: HealLowestAlly(caster, 35f + caster.Ap); break;
                case AbilityType.Whirlwind: DamageNearby(caster, enemies, 2f, 28f + caster.Ap); break;
                case AbilityType.ShadowStrike: DealDamage(caster, target, 48f + caster.Ap); break;
                case AbilityType.FrostNova: DamageNearby(caster, enemies, 2.4f, 26f + caster.Ap); break;
                case AbilityType.Starfall: DamageRandom(caster, enemies, 3, 24f + caster.Ap); break;
                case AbilityType.ShieldSlam:
                    caster.Shield += 20f + caster.Ap * 0.5f;
                    DealDamage(caster, target, 24f + caster.Ap);
                    break;
            }

            if (caster.Definition.Traits.Contains("druid")) caster.Hp = Mathf.Min(caster.MaxHp, caster.Hp + GetDruidBonus());
        }

        private void DamageClosest(RuntimeUnit caster, List<RuntimeUnit> enemies, float radius, float damage, int maxTargets)
        {
            foreach (var enemy in enemies.Where(unit => unit.IsAlive && unit.Tile != null)
                         .OrderBy(unit => Vector2.Distance(caster.View.transform.position, unit.View.transform.position))
                         .Take(maxTargets))
            {
                if (Vector2.Distance(caster.View.transform.position, enemy.View.transform.position) <= radius) DealDamage(caster, enemy, damage);
            }
        }

        private void DamageChain(RuntimeUnit caster, List<RuntimeUnit> enemies, float damage, int jumps)
        {
            var currentDamage = damage;
            foreach (var enemy in enemies.Where(unit => unit.IsAlive && unit.Tile != null)
                         .OrderBy(unit => Vector2.Distance(caster.View.transform.position, unit.View.transform.position))
                         .Take(jumps))
            {
                DealDamage(caster, enemy, currentDamage);
                currentDamage *= 0.75f;
            }
        }

        private void DamageNearby(RuntimeUnit caster, List<RuntimeUnit> enemies, float radius, float damage)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive && enemy.Tile != null && Vector2.Distance(caster.View.transform.position, enemy.View.transform.position) <= radius)
                {
                    DealDamage(caster, enemy, damage);
                }
            }
        }

        private void DamageRandom(RuntimeUnit caster, List<RuntimeUnit> enemies, int hits, float damage)
        {
            foreach (var enemy in enemies.Where(unit => unit.IsAlive && unit.Tile != null).OrderBy(_ => UnityEngine.Random.value).Take(hits))
            {
                DealDamage(caster, enemy, damage);
            }
        }

        private void HealLowestAlly(RuntimeUnit caster, float amount)
        {
            var allies = caster.IsPlayer ? _playerUnits : _enemyUnits;
            var ally = allies.Where(unit => unit.IsAlive && unit.Tile != null).OrderBy(unit => unit.Hp / unit.MaxHp).FirstOrDefault();
            if (ally == null) return;
            ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + amount);
            ally.Shield += amount * 0.15f;
        }

        private void KillUnit(RuntimeUnit unit)
        {
            unit.Hp = 0f;
            if (unit.Tile != null)
            {
                unit.Tile.Occupant = null;
                unit.Tile = null;
            }

            if (unit.View != null) unit.View.SetActive(false);
        }

        private void UpdateUnitBars()
        {
            foreach (var unit in _playerUnits.Concat(_enemyUnits))
            {
                if (unit.View == null || unit.HealthFillRenderer == null || unit.ManaFillRenderer == null) continue;
                if (!unit.View.activeSelf) continue;

                var healthRatio = Mathf.Clamp01(unit.MaxHp <= 0f ? 0f : unit.Hp / unit.MaxHp);
                var manaRatio = Mathf.Clamp01(unit.MaxMana <= 0f ? 0f : unit.Mana / unit.MaxMana);

                unit.HealthFillRenderer.size = new Vector2(0.74f * healthRatio, 0.06f);
                unit.HealthFillRenderer.transform.localPosition = new Vector3((-0.74f + unit.HealthFillRenderer.size.x) * 0.5f, 0f, 0f);

                unit.ManaFillRenderer.size = new Vector2(0.74f * manaRatio, 0.04f);
                unit.ManaFillRenderer.transform.localPosition = new Vector3((-0.74f + unit.ManaFillRenderer.size.x) * 0.5f, 0f, 0f);
            }
        }

        private void EndBattle(bool playerWon)
        {
            _phase = Phase.Results;
            if (playerWon)
            {
                gold += 5;
                _message = "승리";
            }
            else
            {
                health -= 2;
                gold += 3;
                _message = "패배";
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
            _message = "다음 준비 단계";
        }

        private void ResetPlayersAfterBattle()
        {
            foreach (var unit in _playerUnits)
            {
                if (unit.Tile == null) continue;
                ResetStats(unit);
                unit.View.SetActive(true);
                unit.View.transform.position = new Vector3(unit.Tile.Position.x, unit.Tile.Position.y + 0.1f, 0f);
            }
        }

        private void ClearEnemies()
        {
            foreach (var enemy in _enemyUnits)
            {
                if (enemy.View != null) Destroy(enemy.View);
            }

            _enemyUnits.Clear();
            foreach (var tile in _tiles.Where(tile => !tile.IsPlayerZone)) tile.Occupant = null;
        }

        private void ResetStats(RuntimeUnit unit)
        {
            var scale = 1f + (unit.Star - 1) * 0.8f;
            unit.MaxHp = unit.Definition.MaxHealth * scale;
            unit.Hp = unit.MaxHp;
            unit.Ad = unit.Definition.AttackDamage * (1f + (unit.Star - 1) * 0.55f);
            unit.AttackSpeed = unit.Definition.AttackSpeed;
            unit.Range = unit.Definition.AttackRange;
            unit.MoveSpeed = unit.Definition.MoveSpeed;
            unit.MaxMana = unit.Definition.MaxMana;
            unit.Mana = 0f;
            unit.Ap = unit.Definition.AbilityPower * scale;
            unit.CritChance = 0f;
            unit.Lifesteal = 0f;
            unit.Regen = 0f;
            unit.DamageReduction = 0f;
            unit.StartShield = 0f;
            unit.Shield = 0f;
            unit.ManaGain = 20f;
            unit.Cooldown = 0f;
        }

        private void ApplyBonuses(List<RuntimeUnit> team, bool storeUiTraits)
        {
            foreach (var unit in team) ResetStats(unit);
            var counts = new Dictionary<string, int>();
            foreach (var unit in team)
            {
                foreach (var trait in unit.Definition.Traits)
                {
                    counts[trait] = counts.TryGetValue(trait, out var value) ? value + 1 : 1;
                }
            }

            if (storeUiTraits) _uiTraitLevels.Clear();
            foreach (var unit in team)
            {
                foreach (var trait in unit.Definition.Traits)
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

        private void ApplyTrait(RuntimeUnit unit, string traitId, int level)
        {
            if (level <= 0) return;
            switch (traitId)
            {
                case "alliance": unit.StartShield += level == 1 ? 22f : 50f; break;
                case "horde": unit.Ad += level == 1 ? 6f : 14f; break;
                case "scourge": unit.Lifesteal += level == 1 ? 0.12f : 0.24f; break;
                case "cenarion": unit.Regen += level == 1 ? 2f : 5f; break;
                case "titanforged": unit.MaxHp += level == 1 ? 45f : 100f; unit.Hp += level == 1 ? 45f : 100f; break;
                case "warrior": unit.DamageReduction += level == 1 ? 2f : 5f; break;
                case "mage": unit.Ap += level == 1 ? 10f : 24f; unit.Mana += level == 1 ? 15f : 35f; break;
                case "shaman": unit.ManaGain += level == 1 ? 10f : 25f; break;
                case "rogue": unit.CritChance += level == 1 ? 0.15f : 0.35f; break;
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
                _message = "골드가 부족합니다.";
                return;
            }

            if (!freeRoll) gold -= refreshCost;
            _shopOffers.Clear();
            for (var i = 0; i < 5; i++) _shopOffers.Add(_definitions[UnityEngine.Random.Range(0, _definitions.Count)]);
            _message = freeRoll ? "상점 준비 완료" : "상점 새로고침";
        }

        private void BuyFromShop(int index)
        {
            if (_phase != Phase.Prep || index < 0 || index >= _shopOffers.Count) return;
            var offer = _shopOffers[index];
            if (offer == null) return;
            if (gold < offer.Cost)
            {
                _message = "골드가 부족합니다.";
                return;
            }

            var benchIndex = FindFirstEmptyBench();
            if (benchIndex < 0)
            {
                _message = "벤치가 가득 찼습니다.";
                return;
            }

            gold -= offer.Cost;
            var unit = CreateRuntimeUnit(offer, true);
            _playerUnits.Add(unit);
            _bench[benchIndex] = unit;
            PositionBenchUnit(unit, benchIndex);
            _shopOffers[index] = null;
            TryPromotions();
            _message = offer.DisplayName + " 구매 완료";
        }

        private int FindFirstEmptyBench()
        {
            for (var i = 0; i < _bench.Length; i++) if (_bench[i] == null) return i;
            return -1;
        }

        private void PositionBenchUnit(RuntimeUnit unit, int benchIndex)
        {
            var x = -0.8f + benchIndex * 1.05f;
            var y = -2.1f;
            unit.View.transform.position = new Vector3(x, y, 0f);
            unit.Tile = null;
            unit.View.SetActive(true);
        }

        private void TryPromotions()
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var group in _playerUnits.GroupBy(unit => new { unit.Definition.Id, unit.Star }).ToList())
                {
                    var trio = group.Take(3).ToList();
                    if (trio.Count < 3) continue;
                    Promote(trio);
                    changed = true;
                    break;
                }
            }
        }

        private void Promote(List<RuntimeUnit> trio)
        {
            var anchor = trio[0];
            var tile = anchor.Tile;
            var benchIndex = Array.IndexOf(_bench, anchor);
            for (var i = 0; i < trio.Count; i++) RemoveOwnedUnit(trio[i], i == 0);

            anchor.Star++;
            ResetStats(anchor);
            anchor.View.SetActive(true);

            if (tile != null) PlaceOnTile(anchor, tile);
            else
            {
                if (benchIndex < 0) benchIndex = FindFirstEmptyBench();
                if (benchIndex >= 0)
                {
                    _bench[benchIndex] = anchor;
                    PositionBenchUnit(anchor, benchIndex);
                }
            }

            if (!_playerUnits.Contains(anchor)) _playerUnits.Add(anchor);
            _message = anchor.Definition.DisplayName + " 승급 완료";
        }

        private void RemoveOwnedUnit(RuntimeUnit unit, bool keepView)
        {
            var benchIndex = Array.IndexOf(_bench, unit);
            if (benchIndex >= 0) _bench[benchIndex] = null;
            if (unit.Tile != null)
            {
                unit.Tile.Occupant = null;
                unit.Tile = null;
            }

            _playerUnits.Remove(unit);
            if (unit.View != null)
            {
                if (keepView) unit.View.SetActive(false);
                else Destroy(unit.View);
            }
        }

        private void ReturnSelectedToBench()
        {
            if (_selectionMode != SelectionMode.Board || _selectedBoardUnit == null)
            {
                _message = "보드 유닛을 먼저 선택하세요.";
                return;
            }

            var benchIndex = FindFirstEmptyBench();
            if (benchIndex < 0)
            {
                _message = "벤치가 가득 찼습니다.";
                return;
            }

            if (_selectedBoardUnit.Tile != null)
            {
                _selectedBoardUnit.Tile.Occupant = null;
                _selectedBoardUnit.Tile = null;
            }

            _bench[benchIndex] = _selectedBoardUnit;
            PositionBenchUnit(_selectedBoardUnit, benchIndex);
            _message = "벤치로 복귀했습니다.";
            ClearSelection();
        }

        private string SelectedLabel()
        {
            if (_selectionMode == SelectionMode.Bench && _selectedBenchIndex >= 0 && _bench[_selectedBenchIndex] != null) return _bench[_selectedBenchIndex].Label + " (벤치)";
            if (_selectionMode == SelectionMode.Board && _selectedBoardUnit != null) return _selectedBoardUnit.Label + " (보드)";
            return "없음";
        }

        private Dictionary<string, int> BuildCurrentTraitCounts()
        {
            var result = new Dictionary<string, int>();
            foreach (var unit in _playerUnits.Where(unit => unit.Tile != null))
            {
                foreach (var trait in unit.Definition.Traits)
                {
                    result[trait] = result.TryGetValue(trait, out var count) ? count + 1 : 1;
                }
            }

            return result;
        }

        private RuntimeUnit GetSelectedUnit()
        {
            if (_selectionMode == SelectionMode.Board && _selectedBoardUnit != null) return _selectedBoardUnit;
            if (_selectionMode == SelectionMode.Bench && _selectedBenchIndex >= 0) return _bench[_selectedBenchIndex];
            return null;
        }

        private string GetUnitLocationLabel(RuntimeUnit unit)
        {
            if (unit == null) return "-";
            if (unit.Tile != null) return "전장";
            return "벤치";
        }

        private string FormatTraits(RuntimeUnit unit)
        {
            if (unit == null || unit.Definition?.Traits == null || unit.Definition.Traits.Length == 0) return "-";
            return string.Join(" / ", unit.Definition.Traits.Select(GetTraitDisplayName));
        }

        private string GetTraitDisplayName(string traitId)
        {
            var trait = _traits.FirstOrDefault(item => item.Id == traitId);
            return trait?.DisplayName ?? traitId;
        }

        private string GetUnitStatLine(RuntimeUnit unit)
        {
            if (unit == null) return "-";
            return "체력 " + Mathf.CeilToInt(unit.Hp) + "/" + Mathf.CeilToInt(unit.MaxHp)
                + "  공격력 " + Mathf.CeilToInt(unit.Ad)
                + "  사거리 " + unit.Range.ToString("0.0");
        }

        private string GetUnitSecondaryStatLine(RuntimeUnit unit)
        {
            if (unit == null) return "-";
            return "공속 " + unit.AttackSpeed.ToString("0.00")
                + "  마나 " + Mathf.CeilToInt(unit.Mana) + "/" + Mathf.CeilToInt(unit.MaxMana)
                + "  이동속도 " + unit.MoveSpeed.ToString("0.0");
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

        private void OnGUI()
        {
            if (_titleStyle == null) SetupStyles();

            GUI.Box(new Rect(12, 12, 320, 175), string.Empty);
            GUI.Label(new Rect(24, 22, 240, 24), "워크래프트 전술 전투 2D", _titleStyle);
            GUI.Label(new Rect(24, 52, 260, 20), "라운드: " + round, _textStyle);
            GUI.Label(new Rect(24, 74, 260, 20), "단계: " + GetPhaseLabel(), _textStyle);
            GUI.Label(new Rect(24, 96, 260, 20), "골드: " + gold, _textStyle);
            GUI.Label(new Rect(24, 118, 260, 20), "체력: " + health, _textStyle);
            GUI.Label(new Rect(24, 142, 290, 24), _message, _smallStyle);

            GUI.Box(new Rect(Screen.width - 308, 12, 296, 250), string.Empty);
            GUI.Label(new Rect(Screen.width - 296, 22, 220, 24), "활성 시너지", _titleStyle);
            var counts = BuildCurrentTraitCounts();
            var row = 0;
            foreach (var trait in _traits)
            {
                if (!counts.TryGetValue(trait.Id, out var count)) continue;
                var level = GetTraitLevel(trait.Id, count);
                if (level <= 0) continue;
                GUI.Label(new Rect(Screen.width - 296, 52 + row * 28, 270, 24), trait.DisplayName + " " + count + "명 - 단계 " + level, _textStyle);
                row++;
            }

            if (row == 0) GUI.Label(new Rect(Screen.width - 296, 52, 270, 24), "유닛을 배치하면 시너지가 활성화됩니다.", _smallStyle);

            var selectedUnit = GetSelectedUnit();
            GUI.Box(new Rect(Screen.width - 308, 272, 296, 156), string.Empty);
            GUI.Label(new Rect(Screen.width - 296, 282, 220, 24), "선택 유닛 정보", _titleStyle);
            GUI.Label(new Rect(Screen.width - 296, 312, 270, 22), selectedUnit != null ? selectedUnit.Label : "선택된 유닛 없음", _textStyle);
            GUI.Label(new Rect(Screen.width - 296, 336, 270, 22), "위치: " + GetUnitLocationLabel(selectedUnit), _smallStyle);
            GUI.Label(new Rect(Screen.width - 296, 358, 270, 30), "특성: " + FormatTraits(selectedUnit), _smallStyle);
            GUI.Label(new Rect(Screen.width - 296, 388, 270, 22), GetUnitStatLine(selectedUnit), _smallStyle);
            GUI.Label(new Rect(Screen.width - 296, 410, 270, 22), GetUnitSecondaryStatLine(selectedUnit), _smallStyle);

            GUI.Box(new Rect(12, Screen.height - 230, Screen.width - 24, 218), string.Empty);
            GUI.Label(new Rect(24, Screen.height - 220, 160, 24), "벤치", _titleStyle);
            GUI.Label(new Rect(24, Screen.height - 192, 420, 22), "선택: " + SelectedLabel(), _textStyle);

            for (var i = 0; i < _bench.Length; i++)
            {
                var x = 24 + i * 118;
                var y = Screen.height - 164;
                var label = _bench[i] == null ? "[" + (i + 1) + "] 비어 있음" : _bench[i].Label + "\n" + string.Join("/", _bench[i].Definition.Traits);
                if (GUI.Button(new Rect(x, y, 110, 52), label)) SelectBenchUnit(i);
            }

            GUI.Label(new Rect(24, Screen.height - 102, 160, 24), "상점", _titleStyle);
            for (var i = 0; i < 5; i++)
            {
                var x = 24 + i * 150;
                var y = Screen.height - 74;
                var offer = i < _shopOffers.Count ? _shopOffers[i] : null;
                var text = offer == null ? "구매 완료" : offer.DisplayName + "\n" + offer.Cost + "G / " + string.Join("/", offer.Traits);
                if (GUI.Button(new Rect(x, y, 140, 54), text)) BuyFromShop(i);
            }

            var controlX = Screen.width - 478;
            if (GUI.Button(new Rect(controlX, Screen.height - 168, 140, 34), "전투 시작")) StartBattle();
            if (GUI.Button(new Rect(controlX + 150, Screen.height - 168, 140, 34), "상점 새로고침 (" + refreshCost + "G)")) RollShop(false);
            if (GUI.Button(new Rect(controlX, Screen.height - 124, 140, 34), "벤치로 복귀")) ReturnSelectedToBench();
            if (GUI.Button(new Rect(controlX + 150, Screen.height - 124, 140, 34), "선택 해제"))
            {
                ClearSelection();
                _message = "선택 해제";
            }

            if (GUI.Button(new Rect(controlX, Screen.height - 80, 290, 34), "다음 라운드")) NextRound();
        }

        private string GetPhaseLabel()
        {
            return _phase switch
            {
                Phase.Prep => "준비",
                Phase.Battle => "전투",
                Phase.Results => "결과",
                _ => _phase.ToString(),
            };
        }

        private Sprite CreateQuadSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
