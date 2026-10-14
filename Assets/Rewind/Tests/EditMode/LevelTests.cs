using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rewind.Core;
using Rewind.Data;
using UnityEditor;

namespace Rewind.Tests
{
    public sealed class LevelTests
    {
        const int LoopTicks = 80;

        // Every tape the game ships (the catalog) must hold up. Level assets outside the catalog are drafts.
        static IEnumerable<TestCaseData> Levels()
        {
            var catalogGuid = AssetDatabase.FindAssets("t:LevelCatalog", new[] { "Assets/Rewind" }).First();
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(catalogGuid));
            foreach (var level in catalog.Levels)
            {
                var path = AssetDatabase.GetAssetPath(level);
                yield return new TestCaseData(path).SetName("Level: " + System.IO.Path.GetFileNameWithoutExtension(path));
            }
        }

        static LevelData Load(string path) => AssetDatabase.LoadAssetAtPath<LevelData>(path);

        static GameConfig Config => AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameConfig").First()));

        // the level's own loop length, as the game would use it
        static int LoopTicksFor(LevelData level) => Config.LoopTicksFor(level.LoopSeconds);

        [TestCaseSource(nameof(Levels))]
        public void ReferenceSolutionSolvesAtParWithoutParadox(string path)
        {
            var level = Load(path);
            Assert.That(level.ReferenceSolution, Is.Not.Empty, "Level has no reference solution.");
            var result = Solver.Play(level.BuildLayout(), level.ReferenceSolution, LoopTicksFor(level), Config.LoopTicksFor);
            Assert.That(result.Solved, Is.True, "Reference solution does not reach the exit.");
            Assert.That(result.LoopsUsed, Is.EqualTo(level.TotalPar), "Reference solution does not match par.");
            Assert.That(result.Paradoxes, Is.Empty, "Reference solution causes a paradox.");
        }

        [TestCaseSource(nameof(Levels))]
        public void CannotBeFinishedInOneLoop(string path)
        {
            var level = Load(path);
            var layout = level.BuildLayout();
            var search = Solver.FindLoop(layout, Array.Empty<EchoRecording>(), LoopTicksFor(level), w => Solver.ReachedExit(layout, w), 400_000);
            if (search.Capped) Assert.Inconclusive($"Search stopped after {search.StatesExplored} states.");
            Assert.That(search.Script, Is.Null, $"Finishable in one loop with: {search.Script}");
        }

        static LevelLayout Layout(string file) =>
            AssetDatabase.FindAssets(file + " t:LevelData", new[] { "Assets/Rewind" })
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g)))
                .First(l => l.name == file).BuildLayout();

        [Test]
        public void KnockingTheCrateOffThePlateShutsTheDoorOnTheEcho()
        {
            var result = Solver.Play(Layout("02_HeavyLifting"), new[] { "RRDDRRURRRR", "DDDRRL" + new string('.', 10) }, LoopTicks);
            Assert.That(result.Paradoxes.Select(p => p.Reason), Does.Contain(ParadoxReason.DoorShut));
        }

        [Test]
        public void PushingTheCrateIntoTheCorridorTooEarlyBlocksTheEcho()
        {
            var result = Solver.Play(Layout("04_Bottleneck"), new[] { "URRRDRRRRRRRD", "RR" + new string('.', 20) }, LoopTicks);
            Assert.That(result.Paradoxes.Select(p => p.Reason), Does.Contain(ParadoxReason.CrateInPath));
        }

        [Test]
        public void SprintingAheadOnCrackedFloorDropsTheEcho()
        {
            var result = Solver.Play(Layout("08_Footsteps"), new[] { "RRR" + string.Concat(Enumerable.Repeat("R..", 6)) + "RRU", "RRRRRRRRR" }, LoopTicks);
            Assert.That(result.Paradoxes.Select(p => p.Reason), Does.Contain(ParadoxReason.FloorCrumbled));
        }
    }

    public sealed class CoreRuleTests
    {
        [Test]
        public void MapsMustBeRectangular()
        {
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#####\n#S.X#\n####"));
        }

        [Test]
        public void MapsNeedStartAndExit()
        {
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#####\n#..X#\n#####"));
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#####\n#S..#\n#####"));
        }

        [Test]
        public void DoorsNeedEveryPlateOfTheirChannel()
        {
            var layout = LevelLayout.Parse("#######\n#1S1aX#\n#######");
            var world = WorldState.Create(layout, 0);
            Simulation.UpdateDoors(layout, world);
            Assert.That(world.DoorOpen[0], Is.False);
        }

        [Test]
        public void LinkedDoorsOpenOnlyWhileAllTheirPlatesAreHeld()
        {
            const string map = "########\n#Sp.pdX#\n########";
            var onPlate = new UnityEngine.Vector2Int(2, 1);

            var layout = LevelLayout.Parse(map, "2,1>5,1;4,1>5,1");
            var world = WorldState.Create(layout, 0);
            world.Player.Cell = onPlate;
            Simulation.UpdateDoors(layout, world);
            Assert.That(world.DoorOpen[0], Is.False, "Only one of its two plates is held.");

            layout = LevelLayout.Parse(map, "2,1>5,1");
            world = WorldState.Create(layout, 0);
            world.Player.Cell = onPlate;
            Simulation.UpdateDoors(layout, world);
            Assert.That(world.DoorOpen[0], Is.True, "Its only plate is held.");
        }

        [Test]
        public void ColourLinkedMapsConvertToTheSameLinks()
        {
            var grid = new[] { "#######".ToCharArray(), "#1S1aX#".ToCharArray(), "#######".ToCharArray() };
            string links = LevelLinks.ConvertColourLinks(grid, "");
            Assert.That(new string(grid[1]), Is.EqualTo("#pSpdX#"));
            var layout = LevelLayout.Parse(string.Join("\n", grid.Select(r => new string(r))), links);
            Assert.That(layout.DoorPlates(0).Count, Is.EqualTo(2));
        }

        [Test]
        public void RoomsCanShareAWallButNotOverlap()
        {
            var grid = Enumerable.Range(0, 9).Select(_ => new string('#', 20).ToCharArray()).ToArray();
            string rooms = "";
            Assert.That(LevelRooms.TryAdd(grid, ref rooms, new UnityEngine.RectInt(0, 0, 10, 9)), Is.Null);
            Assert.That(new string(grid[4]), Is.EqualTo("#........###########"), "Drawn into solid wall, a room is carved out.");
            Assert.That(LevelRooms.TryAdd(grid, ref rooms, new UnityEngine.RectInt(9, 0, 8, 9)), Is.Null, "Sharing a wall is fine.");
            Assert.That(LevelRooms.TryAdd(grid, ref rooms, new UnityEngine.RectInt(5, 0, 8, 9)), Is.Not.Null, "Overlapping insides are not.");

            var list = LevelRooms.For(rooms, 20, 9);
            Assert.That(LevelRooms.IndexOf(list, new UnityEngine.Vector2Int(9, 4), 1), Is.EqualTo(1), "A doorway in a shared wall keeps the current room.");
            Assert.That(LevelRooms.IndexOf(list, new UnityEngine.Vector2Int(3, 4), 1), Is.EqualTo(0));
            Assert.That(LevelRooms.For("", 20, 9).Single().Cells, Is.EqualTo(new UnityEngine.RectInt(0, 0, 20, 9)), "Without rooms the whole map is one.");
        }

        [Test]
        public void CheckpointsCloseTheSectionBehindAndErasingGoesBack()
        {
            var layout = LevelLayout.Parse("#########\n#S..+..X#\n#########", null, "4,1,1,0");
            var checkpoint = new UnityEngine.Vector2Int(4, 1);
            Assert.That(layout.SectionCount, Is.EqualTo(2));
            var session = new LoopSession(layout, 80);
            session.Begin();
            for (int i = 0; i < 3; i++) session.Step(Dir.Right);
            Assert.That(session.LastReport.ReachedCheckpoint, Is.True);
            Assert.That(session.Phase, Is.EqualTo(LoopPhase.Ready));
            Assert.That(session.World.Player.Cell, Is.EqualTo(checkpoint));

            session.Begin();
            session.Step(Dir.Left);
            Assert.That(session.World.Player.Cell, Is.EqualTo(checkpoint), "The section behind is closed.");
            Assert.That(session.LoopsUsed, Is.EqualTo(2));

            Assert.That(session.RemoveLastEcho(), Is.True, "With no echoes left, erasing goes back a section.");
            session.StartLoop();
            Assert.That(session.SectionsFinished, Is.EqualTo(0));
            Assert.That(session.World.Player.Cell, Is.EqualTo(layout.Start));
        }

        [Test]
        public void ACheckpointMustCutTheLevelInTwo()
        {
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#######\n#S.+.X#\n#.....#\n#######"));
        }

        [Test]
        public void TheCanvasGrowsAndTrimsWithSolidWall()
        {
            var grid = new[] { "#####".ToCharArray(), "#S.X#".ToCharArray(), "#####".ToCharArray() };
            var (grown, move) = MapEdit.Extend(grid, 3, 0, 0, 2);
            Assert.That(new string(grown[1]), Is.EqualTo("####S.X#"));
            Assert.That(grown.Length, Is.EqualTo(5));
            Assert.That(move(new UnityEngine.Vector2Int(1, 1)), Is.EqualTo(new UnityEngine.Vector2Int(4, 1)));

            var (trimmed, _) = MapEdit.Trim(grown, Enumerable.Empty<UnityEngine.RectInt>());
            Assert.That(trimmed.Select(r => new string(r)), Is.EqualTo(grid.Select(r => new string(r))), "Trimming keeps one layer of wall.");

            var (wider, _) = MapEdit.Resize(grid, 7, 3);
            Assert.That(new string(wider[1]), Is.EqualTo("#S.X###"), "New columns are solid wall, just inside the right edge.");
        }

        [Test]
        public void LinksMustJoinAPlateToADoor()
        {
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#####\n#SpX#\n#####", "2,1>3,1"));   // 3,1 is the exit
            Assert.Throws<FormatException>(() => LevelLayout.Parse("#####\n#SpX#\n#####", "nonsense"));
        }

        [Test]
        public void CrackedFloorBreaksOnceEmpty()
        {
            var layout = LevelLayout.Parse("#######\n#S~..X#\n#######");
            var session = new LoopSession(layout, 80);
            session.Begin();
            session.Step(Dir.Right);
            Assert.That(session.World.Ground[layout.Index(new UnityEngine.Vector2Int(2, 1))], Is.EqualTo(GroundState.Stepped));
            session.Step(Dir.Right);
            Assert.That(session.World.Ground[layout.Index(new UnityEngine.Vector2Int(2, 1))], Is.EqualTo(GroundState.Hole));
        }

        [Test]
        public void EchoesReplayTheirRecording()
        {
            var layout = LevelLayout.Parse("#########\n#S.....X#\n#########");
            var echoes = Solver.Record(layout, new[] { "RRR" }, 80);
            var session = new LoopSession(layout, 80);
            session.SetEchoes(echoes);
            session.Begin();
            for (int i = 0; i < 3; i++) session.Step(null);
            Assert.That(session.World.Bodies[0].Cell, Is.EqualTo(new UnityEngine.Vector2Int(4, 1)));
            Assert.That(session.World.Player.Cell, Is.EqualTo(layout.Start));
        }

        [Test]
        public void LevelCodeKeepsLoopSecondsAndReadsOldCodes()
        {
            var code = LevelCode.Encode(new CustomLevel { name = "Long", par = 3, map = "#####\n#S.X#\n#####", loopSeconds = 20f });
            Assert.That(LevelCode.TryDecode(code, out var level, out _), Is.True);
            Assert.That(level.par, Is.EqualTo(3));
            Assert.That(level.loopSeconds, Is.EqualTo(20f));
            Assert.That(level.map, Is.EqualTo("#####\n#S.X#\n#####"));

            Assert.That(LevelCode.TryDecode("RWD1|Old|2|#####/#S.X#/#####", out var old, out _), Is.True);
            Assert.That(old.loopSeconds, Is.EqualTo(0f));
        }
    }
}
