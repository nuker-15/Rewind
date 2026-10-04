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

        static IEnumerable<TestCaseData> Levels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Rewind" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return new TestCaseData(path).SetName("Level: " + System.IO.Path.GetFileNameWithoutExtension(path));
            }
        }

        static LevelData Load(string path) => AssetDatabase.LoadAssetAtPath<LevelData>(path);

        [TestCaseSource(nameof(Levels))]
        public void ReferenceSolutionSolvesAtParWithoutParadox(string path)
        {
            var level = Load(path);
            Assert.That(level.ReferenceSolution, Is.Not.Empty, "Level has no reference solution.");
            var result = Solver.Play(level.BuildLayout(), level.ReferenceSolution, LoopTicks);
            Assert.That(result.Solved, Is.True, "Reference solution does not reach the exit.");
            Assert.That(result.LoopsUsed, Is.EqualTo(level.Par), "Reference solution does not match par.");
            Assert.That(result.Paradoxes, Is.Empty, "Reference solution causes a paradox.");
        }

        [TestCaseSource(nameof(Levels))]
        public void CannotBeFinishedInOneLoop(string path)
        {
            var layout = Load(path).BuildLayout();
            var search = Solver.FindLoop(layout, Array.Empty<EchoRecording>(), LoopTicks, w => Solver.ReachedExit(layout, w), 400_000);
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
    }
}
