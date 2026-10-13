using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Characters;
using HexGrid;
using TileEffects;

namespace Tests.TileEffects
{
    public class TileEffectSystemTests
    {
        private HexGridManager grid;
        private TileEffectSystem system;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            grid = TestHelper.CreateTestGrid(3);
            objects.Add(grid.gameObject);
            var systemGo = new GameObject("TileEffectSystem");
            objects.Add(systemGo);
            system = systemGo.AddComponent<TileEffectSystem>();
            system.InitializeForTests(grid, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (system != null) system.Shutdown();
            foreach (GameObject go in objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            foreach (Character character in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(character.gameObject);
            }
            objects.Clear();
        }

        private Character At(int q, int r)
        {
            Character character = TestHelper.CreateCharacterOnGrid(grid, 0, q, r);
            objects.Add(character.gameObject);
            return character;
        }

        private HexCell Cell(int q, int r) => grid.GetCell(new HexCoordinates(q, r));

        private FireTileEffectSO Fire(int fireDamage = 2, int explosionDamage = 3)
        {
            var fire = ScriptableObject.CreateInstance<FireTileEffectSO>();
            fire.EffectName = "Fire";
            fire.Duration = 2;
            fire.FireDamage = fireDamage;
            fire.ExplosionDamage = explosionDamage;
            return fire;
        }

        private OilTileEffectSO Oil()
        {
            var oil = ScriptableObject.CreateInstance<OilTileEffectSO>();
            oil.EffectName = "Oil";
            oil.Duration = 0;
            oil.Slippery = true;
            return oil;
        }

        private RockTileEffectSO Rock()
        {
            var rock = ScriptableObject.CreateInstance<RockTileEffectSO>();
            rock.EffectName = "Rock";
            rock.Duration = 0;
            rock.BlocksMovement = true;
            return rock;
        }

        // ------------------------------------------------------- movement / entry

        [Test]
        public void Oil_Slides_CharacterOneTileForward_InMovementDirection()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            HexCell beyond = Cell(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R);

            system.PlaceEffect(oilCell, Oil());

            system.WalkInto(hero, oilCell);

            Assert.AreEqual(beyond.Coordinates, hero.CurrentCell.Coordinates,
                "Character should slide past the oil.");
            Assert.AreEqual(oilCoords, hero.LastCell.Coordinates);
        }

        [Test]
        public void Oil_SlideBlocked_CharacterStopsOnOil()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            At(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R); // blocker occupies the slide target

            system.PlaceEffect(oilCell, Oil());

            system.WalkInto(hero, oilCell);

            Assert.AreEqual(oilCell.Coordinates, hero.CurrentCell.Coordinates);
        }

        [Test]
        public void Fire_DamagesCharacter_OnEntry()
        {
            Character hero = At(0, 0);
            HexCell fireCell = Cell(1, 0);
            system.PlaceEffect(fireCell, Fire());

            system.WalkInto(hero, fireCell);

            Assert.AreEqual(10 - 2, hero.Stats.CurrentHealth);
        }

        [Test]
        public void MoveEffect_Routes_ThroughSlippingOil()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            HexCell beyond = Cell(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R);
            system.PlaceEffect(oilCell, Oil());

            var move = ScriptableObject.CreateInstance<global::Cards.Effects.MoveEffect>();
            move.Execute(new global::Cards.CardContext
            {
                Source = hero,
                Grid = grid,
                TargetCell = oilCell,
                MovePath = new List<HexCell> { oilCell }
            });

            Assert.AreEqual(beyond.Coordinates, hero.CurrentCell.Coordinates);
        }

        // ------------------------------------------------------- turn start / ticking

        [Test]
        public void Fire_DamagesCharacter_AtStartOfTurn()
        {
            // Fire is placed while the tile is empty so this isolates the turn-start trigger
            // (creating a character on a tile is a placement, not an entry).
            HexCell fireCell = Cell(1, 0);
            system.PlaceEffect(fireCell, Fire());
            Character hero = At(1, 0);

            system.HandleTurnStarted(hero, isRoundStart: false);

            Assert.AreEqual(10 - 2, hero.Stats.CurrentHealth);
        }

        [Test]
        public void Fire_PlacedOnOccupiedTile_BurnsOccupantImmediately()
        {
            Character victim = At(1, 0);
            system.PlaceEffect(victim.CurrentCell, Fire());

            Assert.AreEqual(10 - 2, victim.Stats.CurrentHealth,
                "Fire that appears on a character's tile should burn them right away.");
        }

        [Test]
        public void Fire_TicksDown_OncePerRound_AndExpires()
        {
            Character hero = At(0, 0);
            HexCell fireCell = Cell(2, 1);
            TileEffectInstance placed = system.PlaceEffect(fireCell, Fire());
            Assert.AreEqual(2, placed.TurnsRemaining);

            system.HandleTurnStarted(hero, isRoundStart: true);
            Assert.AreEqual(1, placed.TurnsRemaining, "Round start should tick one round.");

            system.HandleTurnStarted(hero, isRoundStart: false);
            Assert.AreEqual(1, placed.TurnsRemaining, "Mid-round turns should not tick.");

            system.HandleTurnStarted(hero, isRoundStart: true);
            Assert.AreEqual(0, placed.TurnsRemaining);
            Assert.IsFalse(fireCell.Effects.Contains(placed), "Fire should be removed after its last round.");
        }

        [Test]
        public void PermanentEffects_NeverExpire()
        {
            Character hero = At(0, 0);
            HexCell oilCell = Cell(2, 1);
            TileEffectInstance placed = system.PlaceEffect(oilCell, Oil());

            system.HandleTurnStarted(hero, isRoundStart: true);
            system.HandleTurnStarted(hero, isRoundStart: true);

            Assert.IsTrue(oilCell.Effects.Contains(placed), "Permanent oil should survive rounds.");
        }

        // ------------------------------------------------------- ignition & rock

        [Test]
        public void FireOnOil_Explodes_DamagingTileAndAdjacentCharacters()
        {
            Character victim = At(0, 0);
            Character neighbor = At(1, 0);

            system.PlaceEffect(victim.CurrentCell, Oil());
            system.PlaceEffect(victim.CurrentCell, Fire());

            Assert.AreEqual(10 - 3, victim.Stats.CurrentHealth);
            Assert.AreEqual(10 - 3, neighbor.Stats.CurrentHealth);
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect.Slippery), "Oil should be consumed by the blast.");
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect is FireTileEffectSO), "The blast consumes the fire too.");
            Assert.AreEqual(0, victim.CurrentCell.Effects.Count, "Fire on oil leaves nothing behind on the tile.");
        }

        [Test]
        public void Rock_BlocksPathing_MoveStopsBeforeIt()
        {
            Character hero = At(0, 0);
            HexCell rockCell = Cell(1, 0);
            system.PlaceEffect(rockCell, Rock());

            List<HexCell> moved = system.MoveCharacter(hero, new List<HexCell> { rockCell });

            Assert.IsEmpty(moved);
            Assert.AreEqual(new HexCoordinates(0, 0), hero.CurrentCell.Coordinates);
            Assert.IsFalse(rockCell.IsWalkable);
        }

        [Test]
        public void Rock_OnOccupiedTile_IsRejected()
        {
            Character hero = At(1, 0);

            TileEffectInstance placed = system.PlaceEffect(hero.CurrentCell, Rock());

            Assert.IsNull(placed);
            Assert.IsFalse(hero.CurrentCell.HasBlockingEffect);
        }

        [Test]
        public void Rock_BlocksPathfinding_ShortestRouteCannotCrossIt()
        {
            Character hero = At(0, 0);
            Assert.IsNotNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(2, 0), 2),
                "Baseline: (0,0)->(2,0) is two steps with no obstacles.");

            system.PlaceEffect(Cell(1, 0), Rock());

            Assert.IsNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(2, 0), 2),
                "The shortest two-step route straight through the rock must be blocked.");
            Assert.IsNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(1, 0), 5),
                "The rock cell itself is never walkable.");
        }

        // ------------------------------------------------------- visual indicators

        [Test]
        public void GameDataLibrary_References_FireAndOilVisualPrefabs()
        {
            var library = Resources.Load<global::Equipment.GameDataLibrary>("GameDataLibrary");
            Assert.IsNotNull(library, "The shared GameDataLibrary asset must exist in Resources.");
            Assert.IsNotNull(library.fireEffectVisualPrefab, "The Fire visual prefab must be wired up.");
            Assert.IsNotNull(library.oilEffectVisualPrefab, "The Oil visual prefab must be wired up.");
            Assert.IsNotNull(library.fireEffectVisualPrefab.GetComponent<TileEffectVisual>());
            Assert.IsNotNull(library.oilEffectVisualPrefab.GetComponent<TileEffectVisual>());
        }

        [Test]
        public void PlacingFireAndOil_SpawnsRenderedVisuals_UnderTheirCells()
        {
            var library = Resources.Load<global::Equipment.GameDataLibrary>("GameDataLibrary");
            system.BindVisualPrefabs(library.fireEffectVisualPrefab, library.oilEffectVisualPrefab);

            HexCell fireCell = Cell(1, 0);
            system.PlaceEffect(fireCell, Fire());
            TileEffectVisual fireVisual = fireCell.transform.GetComponentInChildren<TileEffectVisual>();
            Assert.IsNotNull(fireVisual, "Placing fire should instantiate its visual under the cell.");

            HexCell oilCell = Cell(2, 1);
            system.PlaceEffect(oilCell, Oil());
            TileEffectVisual oilVisual = oilCell.transform.GetComponentInChildren<TileEffectVisual>();
            Assert.IsNotNull(oilVisual, "Placing oil should instantiate its visual under the cell.");

            foreach (TileEffectVisual visual in new[] { fireVisual, oilVisual })
            {
                Renderer renderer = visual.GetComponent<Renderer>();
                Assert.IsNotNull(renderer, "The visual prefab must carry a renderer.");
                MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
                Assert.IsNotNull(meshFilter, "The visual prefab must carry a mesh filter.");
                visual.ConfigureRenderer();
                Assert.IsNotNull(renderer.sharedMaterial, "The visual must be assigned a material so it shows in play.");
                Assert.IsNotNull(renderer.sharedMaterial.shader, "The assigned material must resolve a real shader.");
                Mesh mesh = meshFilter.sharedMesh;
                Assert.IsNotNull(mesh, "The visual prefab's mesh must resolve (built-in Quad reference).");
                Assert.Greater(mesh.vertexCount, 0, "The resolved mesh must actually have geometry to draw.");
            }
        }

        [Test]
        public void RemovingAnEffect_DestroysItsVisual()
        {
            var library = Resources.Load<global::Equipment.GameDataLibrary>("GameDataLibrary");
            system.BindVisualPrefabs(library.fireEffectVisualPrefab, library.oilEffectVisualPrefab);

            HexCell cell = Cell(1, 0);
            TileEffectInstance fire = system.PlaceEffect(cell, Fire());
            Assert.AreEqual(1, cell.transform.childCount, "A fresh fire effect should show exactly one visual child.");

            system.RemoveEffect(cell, fire);
            Assert.AreEqual(0, cell.transform.childCount, "Removing the effect must take its visual away too.");
        }

        [Test]
        public void GreaseOnBurningTile_Explodes_DamagingTileAndAdjacentCharacters()
        {
            // Fire goes down first on an empty tile so this isolates the explosion (dumping oil
            // on burning ground) from the fire's own on-appearance burn.
            HexCell center = Cell(0, 0);
            system.PlaceEffect(center, Fire());
            Character victim = At(0, 0);
            Character neighbor = At(1, 0);

            system.PlaceEffect(victim.CurrentCell, Oil());

            Assert.AreEqual(10 - 3, victim.Stats.CurrentHealth);
            Assert.AreEqual(10 - 3, neighbor.Stats.CurrentHealth);
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect.Slippery), "Oil should be consumed by the blast.");
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect is FireTileEffectSO), "The blast consumes the fire too.");
            Assert.AreEqual(0, victim.CurrentCell.Effects.Count, "Dumping oil on fire leaves nothing behind on the tile.");
        }

        [Test]
        public void RealSpawnRockAsset_RockPrefabResolves_AndSpawnsABoardRock()
        {
            var spawn = UnityEditor.AssetDatabase.LoadAssetAtPath<global::Cards.Effects.SpawnRockEffect>(
                "Assets/CardEffect/Environment/SpawnRock.asset");
            Assert.IsNotNull(spawn, "The SpawnRock effect asset must exist.");
            Assert.IsNotNull(spawn.rockPrefab, "SpawnRock.asset must reference the Big Rock prefab (GameObject field).");
            var mainAsset = UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Environment/Big Rock.prefab");
            Assert.AreEqual(mainAsset, spawn.rockPrefab,
                "rockPrefab must point at the prefab's root GameObject, not the Character component inside it.");
            Assert.IsNotNull(spawn.rockPrefab.GetComponent<Character>(),
                "The referenced prefab must carry a Character on its root so the rock lane spawns a structure.");

            System.Console.WriteLine($"Resolved rockPrefab: {spawn.rockPrefab.name}");

            Character source = At(0, 0);
            HexCell target = Cell(1, 0);
            Assert.IsTrue(target.IsWalkable, "Empty cell must be walkable (rock lane).");

            spawn.Execute(new global::Cards.CardContext
            {
                Source = source,
                Grid = grid,
                TargetCell = target,
                Range = 3
            });

            Assert.IsNotNull(target.Occupant, "Playing Mould Earth's rock step must spawn a structure on the tile.");
            Assert.IsTrue(target.Occupant.IsStructure, "The spawned object must be registered as a structure.");
            Assert.AreNotEqual(global::Game.GameState.LocalPlayerId, target.Occupant.OwnerPlayerId,
                "The rock must never be owned by the local player or it steals the hand view.");
            Assert.AreEqual(global::Game.GameState.EnemyOwnerPlayerId, target.Occupant.OwnerPlayerId,
                "The rock is owned like a board fixture, matching the network rebuild.");
            Assert.AreEqual(-1, target.Occupant.TeamId, "The rock is neutral, so every team can attack it.");
            Assert.IsFalse(target.IsWalkable, "The structure blocks foot traffic.");
        }
    }
}