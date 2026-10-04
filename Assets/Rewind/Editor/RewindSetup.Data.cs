using System.IO;
using System.Linq;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rewind.EditorTools
{
    public static partial class RewindSetup
    {
        static class Assets
        {
            public static GameConfig Config;
            public static BoardPalette Palette;
            public static LevelCatalog Catalog;
            public static InputActionAsset Controls;
            public static SfxLibrary SfxLibrary;
            public static BoardPrefabSet PrefabSet;
        }

        struct LevelDef
        {
            public string File, Name, Hint;
            public int Par;
            public string[] Map, Solution;
        }

        static string Waits(int n) => new string('.', n);

        // The nine tapes from the browser prototype, each with a solution verified by the solver.
        static LevelDef[] DefaultLevels() => new[]
        {
            new LevelDef
            {
                File = "01_FirstEcho", Name = "First Echo", Par = 2,
                Hint = "The door is only open while someone stands on the plate. Step onto it, then press Space to end the loop. Next loop, your echo holds the plate for you.",
                Map = new[] { "###########", "#S..#.....#", "#...#.....#", "#.1.a...X.#", "#...#.....#", "###########" },
                Solution = new[] { "RDD", "RRDDRRRRR" },
            },
            new LevelDef
            {
                File = "02_HeavyLifting", Name = "Heavy Lifting", Par = 2,
                Hint = "Crates hold plates down too. Your echo walks through that door expecting it open. Knock the crate off the plate first and the door shuts on your own past.",
                Map = new[] { "#############", "#S....#.....#", "#..o..a..2..#", "#.....#.....#", "#..1..###b###", "#.....#.....#", "#.....#..X..#", "#############" },
                Solution = new[] { "RRDDRRURRRR", "RRRRDRRRRD.DDD" },
            },
            new LevelDef
            {
                File = "03_Relay", Name = "Relay", Par = 3,
                Hint = "Echoes stack. Your second echo only gets through because your first one held the door for it.",
                Map = new[] { "###############", "#S...#...#....#", "#....a...b..X.#", "#.1..#.2.#....#", "#....#...#....#", "###############" },
                Solution = new[] { "RDD", "RRRDRRDR", "RRRDRRRRRRRR" },
            },
            new LevelDef
            {
                File = "04_Bottleneck", Name = "Bottleneck", Par = 2,
                Hint = "The corridor is one tile wide. Push the crate in while your echo still needs that space and you erase your own past. Watch its trail and time it.",
                Map = new[] { "##############", "#....#####...#", "#S.o...1.....#", "#....#####.2.#", "#....#####...#", "###a##########", "###b..X#######", "##############" },
                Solution = new[] { "URRRDRRRRRRRD", "R....RRRRLLLDDDDRRR" },
            },
            new LevelDef
            {
                File = "05_Airlock", Name = "Airlock", Par = 2,
                Hint = "Striped doors work in reverse: they shut while their plate is held. Your echo has to let go of the amber plate at the right moment.",
                Map = new[] { "##############", "#S.....#..#..#", "#...o..a..A.X#", "#..2...#..#..#", "##b#####..#..#", "#.1###########", "##############" },
                Solution = new[] { "RRRDRDLULLDDD" + Waits(18) + "L", "RRRRRD" + Waits(7) + "RRR" + Waits(16) + "RRR" },
            },
            new LevelDef
            {
                File = "06_Crumble", Name = "Crumble", Par = 2,
                Hint = "Cracked floor gives way as soon as nobody is standing on it. A crate pushed into a hole fills it. You and your echo both need to get across.",
                Map = new[] { "################", "#S...__.....##1#", "#..o.~.........#", "#....__..##a####", "#....__..##.X..#", "################" },
                Solution = new[] { "RRRDRRRRRRRRRRU", "....DRRRRRRRRRRDDR" },
            },
            new LevelDef
            {
                File = "07_Quorum", Name = "Quorum", Par = 3,
                Hint = "The exit only opens while all three amber plates are held at the same moment. Count the jobs, then count your helpers. Something has to do double duty.",
                Map = new[] { "####################", "#1#..__......#...#1#", "#....__..S...#.....#", "##...__......#....##", "#....~....2o.b.....#", "#....__......#.....#", "#a###__.o.1..#.....#", "#X###__......#.....#", "####################" },
                Solution = new[] { "RRRDDLRRRUURRRRU", "LLDDLLLUULLLU", "DDDLLDRRURRULLLLLLLDLLLDD" },
            },
            new LevelDef
            {
                File = "08_Footsteps", Name = "Footsteps", Par = 3,
                Hint = "Three of you have to reach the island, and every tile of that bridge gives way the moment it is empty.",
                Map = new[] { "#################", "#...#______#1...#", "#S...~~~~~~.....#", "#...#______#..1.#", "#############a###", "#############X###", "#################" },
                Solution = new[] { "RRR" + string.Concat(Enumerable.Repeat("R..", 6)) + "RRU", "...RRRR..R..R..RRRRRDRR", "......RRRR..R..RR.RRRRDRDD" },
            },
            new LevelDef
            {
                File = "09_Clockwork", Name = "Clockwork", Par = 3,
                Hint = "Three amber plates, one crate, and it starts locked away. The cyan door and the striped cyan door never agree, and the bridge only holds while someone is on it.",
                Map = new[] { "######################", "###############1######", "###########.....######", "###########B##########", "#1#...####....2.#....#", "#.....####......#....#", "##....~~~~...1..b.o..#", "#.....####......#....#", "###a######..S...######", "###X##################", "######################" },
                Solution = new[] { "UUUURR" + Waits(24) + "LLLUURRRRU", ".UURRRRRURRDLLLLLULLDLLL..L..L..L..LULLLLU", Waits(22) + "UULLL..L..L..L..LDLLDD" },
            },
        };

        static void BuildData()
        {
            Progress("Writing data assets", 0.35f);
            Assets.Config = LoadOrCreate<GameConfig>($"{DataDir}/GameConfig.asset");
            Assets.Palette = LoadOrCreate<BoardPalette>($"{DataDir}/BoardPalette.asset");

            var levels = DefaultLevels().Select(BuildLevel).ToArray();
            string catalogPath = $"{DataDir}/LevelCatalog.asset";
            bool newCatalog = !Exists(catalogPath);
            Assets.Catalog = LoadOrCreate<LevelCatalog>(catalogPath);
            if (newCatalog || overwrite)
            {
                SetArray(Assets.Catalog, "levels", levels.Cast<Object>().ToArray());
                EditorUtility.SetDirty(Assets.Catalog);
            }

            BuildInputActions();

            string sfxPath = $"{DataDir}/SfxLibrary.asset";
            bool newSfx = !Exists(sfxPath);
            Assets.SfxLibrary = LoadOrCreate<SfxLibrary>(sfxPath);
            if (newSfx || overwrite)
            {
                var lib = Assets.SfxLibrary;
                lib.step = Sfx.Step; lib.bump = Sfx.Bump; lib.push = Sfx.Push;
                lib.doorOpen = Sfx.DoorOpen; lib.doorClose = Sfx.DoorClose;
                lib.paradox = Sfx.Paradox; lib.crumble = Sfx.Crumble; lib.fill = Sfx.Fill;
                lib.rewind = Sfx.Rewind; lib.loopStart = Sfx.LoopStart; lib.warning = Sfx.Warning; lib.solved = Sfx.Solved;
                EditorUtility.SetDirty(lib);
            }
            AssetDatabase.SaveAssets();
        }

        static LevelData BuildLevel(LevelDef def)
        {
            string path = $"{LevelsDir}/{def.File}.asset";
            bool fresh = !Exists(path);
            var level = LoadOrCreate<LevelData>(path);
            if (fresh || overwrite)
            {
                Set(level, "displayName", def.Name);
                Set(level, "par", def.Par);
                Set(level, "hint", def.Hint);
                Set(level, "map", string.Join("\n", def.Map));
                SetArray(level, "referenceSolution", def.Solution);
                EditorUtility.SetDirty(level);
            }
            return level;
        }

        static void BuildInputActions()
        {
            if (ShouldBuild(InputPath))
            {
                var asset = ScriptableObject.CreateInstance<InputActionAsset>();
                asset.name = "RewindControls";

                var play = asset.AddActionMap("Gameplay");
                AddButton(play, "MoveUp", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
                AddButton(play, "MoveDown", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
                AddButton(play, "MoveLeft", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
                AddButton(play, "MoveRight", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
                AddButton(play, "EndLoop", "<Keyboard>/space", "<Gamepad>/buttonNorth");
                AddButton(play, "RetryLoop", "<Keyboard>/r", "<Gamepad>/buttonWest");
                AddButton(play, "EraseEcho", "<Keyboard>/z", "<Gamepad>/leftShoulder");
                AddButton(play, "StartLoop", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");

                var shell = asset.AddActionMap("Shell");
                AddButton(shell, "Back", "<Keyboard>/escape", "<Gamepad>/buttonEast");
                AddButton(shell, "Pause", "<Keyboard>/p", "<Gamepad>/start");
                AddButton(shell, "Hint", "<Keyboard>/h", "<Gamepad>/select");
                AddButton(shell, "Fullscreen", "<Keyboard>/f", "<Keyboard>/f11");
                AddButton(shell, "Mute", "<Keyboard>/m");

                File.WriteAllText(InputPath, asset.ToJson());
                Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceSynchronousImport);
            }
            Assets.Controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        }

        static void AddButton(InputActionMap map, string name, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
        }
    }
}
