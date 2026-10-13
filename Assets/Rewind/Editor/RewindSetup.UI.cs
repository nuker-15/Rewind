using Rewind.Data;
using Rewind.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.EditorTools
{
    // uGUI prefabs. Every screen is its own prefab built from shared widget prefabs, nested inside UIRoot.
    public static partial class RewindSetup
    {
        static class UIColor
        {
            public static readonly Color Ink = BoardPalette.Hex("090A14");
            public static readonly Color Bezel = BoardPalette.Hex("12142B");
            public static readonly Color Deck = BoardPalette.Hex("1C1F40");
            public static readonly Color Line = BoardPalette.Hex("2B3060");
            public static readonly Color Fg = BoardPalette.Hex("EBE9F8");
            public static readonly Color Muted = BoardPalette.Hex("9A9DC6");
            public static readonly Color Rec = BoardPalette.Hex("FF4B55");
            public static readonly Color Amber = BoardPalette.Hex("FFB547");
            public static readonly Color Cyan = BoardPalette.Hex("48D8FF");
            public static readonly Color Exit = BoardPalette.Hex("C4FF6A");
            public static readonly Color Paper = BoardPalette.Hex("EFE7D4");
        }

        static class Widgets
        {
            public static MenuButton MenuButton;
            public static Button IconButton;
            public static LevelCard LevelCard;
            public static TimelineTrack TimelineTrack;
        }

        static class Screens
        {
            public static GameObject Title, LevelSelect, Hud, Pause, Solved, HowTo, Root;
        }

        static TMP_FontAsset font;

        static void BuildUI()
        {
            font = TMP_Settings.defaultFontAsset;
            if (!font) Debug.LogWarning("TextMeshPro has no default font asset; text will use whatever TMP falls back to.");
            Progress("Building UI widgets", 0.7f);
            BuildWidgets();
            Progress("Building UI screens", 0.8f);
            Screens.Title = BuildScreen("TitleScreen", BuildTitle);
            Screens.LevelSelect = BuildScreen("LevelSelectScreen", BuildLevelSelect);
            Screens.Hud = BuildScreen("HudScreen", BuildHud);
            Screens.Pause = BuildScreen("PausePanel", BuildPause);
            Screens.Solved = BuildScreen("SolvedPanel", BuildSolved);
            Screens.HowTo = BuildScreen("HowToPlayPanel", BuildHowTo);
            BuildUIRoot();
        }

        // ---- Widgets ---------------------------------------------------------------------------------------

        static void BuildWidgets()
        {
            string menuPath = $"{UIWidgetDir}/MenuButton.prefab";
            if (ShouldBuild(menuPath))
            {
                var rt = UI("MenuButton", null);
                rt.sizeDelta = new Vector2(520, 56);
                var hit = Img(rt, Art.UIPanel, Clear(UIColor.Deck), raycast: true);
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 56;
                le.preferredWidth = 520;

                var selected = Stretch(UI("Selected", rt));
                Img(selected, Art.UIPanel, UIColor.Deck);
                Img(Stretch(UI("Outline", selected)), Art.UIOutline, UIColor.Rec);
                var arrow = Place(UI("Arrow", selected), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, 0), new Vector2(20, 20));
                Img(arrow, Art.Triangle, UIColor.Rec);
                selected.gameObject.SetActive(false);

                var label = Txt(Stretch(UI("Label", rt), 52, 0, 90, 0), "MENU ITEM", 30, UIColor.Muted, TextAlignmentOptions.MidlineLeft);
                var key = Txt(Stretch(UI("Key", rt), 0, 0, 20, 0), "", 22, UIColor.Muted, TextAlignmentOptions.MidlineRight);
                var mb = rt.gameObject.AddComponent<MenuButton>();
                Set(mb, "label", label);
                Set(mb, "keyHint", key);
                Set(mb, "selectedMarker", selected.gameObject);
                Set(mb, "labelNormal", UIColor.Muted);
                Set(mb, "labelSelected", UIColor.Fg);
                Set(mb, "labelDisabled", UIColor.Line);
                SavePrefab(rt.gameObject, menuPath);
            }
            Widgets.MenuButton = LoadPrefab<MenuButton>(menuPath);

            string iconPath = $"{UIWidgetDir}/IconButton.prefab";
            if (ShouldBuild(iconPath))
            {
                var rt = UI("IconButton", null);
                rt.sizeDelta = new Vector2(56, 56);
                var bg = Img(rt, Art.UIPanel, UIColor.Bezel, raycast: true);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.5f, 1.5f, 1.8f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.9f);
                colors.colorMultiplier = 1.5f;
                button.colors = colors;
                NoNavigation(button);
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 56;
                le.preferredHeight = 56;
                Img(Stretch(UI("Outline", rt)), Art.UIOutline, UIColor.Line);
                Img(Place(UI("Icon", rt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30)), Art.IconPause, UIColor.Fg);
                Txt(Stretch(UI("Glyph", rt)), "", 36, UIColor.Fg, TextAlignmentOptions.Center);
                SavePrefab(rt.gameObject, iconPath);
            }
            Widgets.IconButton = LoadPrefab<Button>(iconPath);

            string cardPath = $"{UIWidgetDir}/LevelCard.prefab";
            if (ShouldBuild(cardPath))
            {
                var rt = UI("LevelCard", null);
                rt.sizeDelta = new Vector2(330, 170);
                var bg = Img(rt, Art.UIPanel, UIColor.Bezel, raycast: true);
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = bg;
                Img(Stretch(UI("Outline", rt)), Art.UIOutline, UIColor.Line);
                var selected = Stretch(UI("Selected", rt), -4, -4, -4, -4);
                Img(selected, Art.UIOutline, UIColor.Rec);
                selected.gameObject.SetActive(false);

                var label = Place(UI("Label", rt), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(302, 70));
                Img(label, Art.UIPanel, UIColor.Paper);
                Img(Place(UI("StripeRed", label), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 13), new Vector2(302, 4)), Art.Square, UIColor.Rec);
                Img(Place(UI("StripeAmber", label), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(302, 4)), Art.Square, UIColor.Amber);
                var number = Txt(Place(UI("Number", label), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 6), new Vector2(64, 50)), "01", 40, UIColor.Ink, TextAlignmentOptions.MidlineLeft);
                var title = Txt(Place(UI("Title", label), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(74, 6), new Vector2(220, 50)), "TAPE", 26, UIColor.Ink, TextAlignmentOptions.MidlineLeft);
                title.overflowMode = TextOverflowModes.Ellipsis;

                var window = Place(UI("ReelWindow", rt), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(170, 38));
                Img(window, Art.UIPanel, UIColor.Ink);
                var reelA = Place(UI("ReelA", window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56, 0), new Vector2(28, 28));
                var reelB = Place(UI("ReelB", window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56, 0), new Vector2(28, 28));
                Img(reelA, Art.Reel, UIColor.Paper);
                Img(reelB, Art.Reel, UIColor.Paper);

                var par = Txt(Place(UI("Par", rt), new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 10), new Vector2(140, 30)), "PAR 2", 22, UIColor.Muted, TextAlignmentOptions.BottomLeft);
                var best = Txt(Place(UI("Best", rt), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-18, 10), new Vector2(160, 30)), "UNPLAYED", 22, UIColor.Muted, TextAlignmentOptions.BottomRight);

                var card = rt.gameObject.AddComponent<LevelCard>();
                Set(card, "number", number);
                Set(card, "title", title);
                Set(card, "parLabel", par);
                Set(card, "bestLabel", best);
                Set(card, "selectedFrame", selected.gameObject);
                SetArray(card, "reels", new Object[] { reelA, reelB });
                Set(card, "bestAtPar", UIColor.Amber);
                Set(card, "bestOverPar", UIColor.Exit);
                Set(card, "unplayed", UIColor.Muted);
                SavePrefab(rt.gameObject, cardPath);
            }
            Widgets.LevelCard = LoadPrefab<LevelCard>(cardPath);

            string trackPath = $"{UIWidgetDir}/TimelineTrack.prefab";
            if (ShouldBuild(trackPath))
            {
                var rt = UI("TimelineTrack", null);
                rt.sizeDelta = new Vector2(1400, 22);
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 22;
                le.minHeight = 22;
                le.flexibleWidth = 1;
                var label = Txt(Place(UI("Label", rt), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(110, 24)), "ECHO 1", 18, UIColor.Fg, TextAlignmentOptions.MidlineLeft);
                var trackRt = Stretch(UI("Track", rt), 120, 0, 0, 0);
                var graphic = trackRt.gameObject.AddComponent<TimelineTrackGraphic>();
                graphic.raycastTarget = false;
                Set(graphic, "cutColor", new Color(UIColor.Bezel.r, UIColor.Bezel.g, UIColor.Bezel.b, 0.85f));
                Set(graphic, "crossColor", Assets.Palette.paradox);
                var reasonRt = UI("Reason", trackRt);
                reasonRt.anchorMin = new Vector2(0, 0);
                reasonRt.anchorMax = new Vector2(0, 1);
                reasonRt.pivot = new Vector2(0, 0.5f);
                reasonRt.anchoredPosition = Vector2.zero;
                reasonRt.sizeDelta = new Vector2(400, 0);
                var reason = Txt(reasonRt, "", 15, Assets.Palette.paradox, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                reason.overflowMode = TextOverflowModes.Ellipsis;
                reasonRt.gameObject.SetActive(false);
                var track = rt.gameObject.AddComponent<TimelineTrack>();
                Set(track, "label", label);
                Set(track, "track", graphic);
                Set(track, "reason", reason);
                Set(track, "layoutElement", le);
                SavePrefab(rt.gameObject, trackPath);
            }
            Widgets.TimelineTrack = LoadPrefab<TimelineTrack>(trackPath);
        }

        // ---- Screens -------------------------------------------------------------------------------------------

        static GameObject BuildScreen(string name, System.Action<RectTransform> build)
        {
            string path = $"{UIScreenDir}/{name}.prefab";
            if (ShouldBuild(path))
            {
                var rt = Stretch(UI(name, null));
                rt.gameObject.AddComponent<CanvasGroup>();
                build(rt);
                SavePrefab(rt.gameObject, path);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void BuildTitle(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<TitleScreen>();

            // wandering echoes behind everything
            var backdrop = Stretch(UI("Backdrop", root));
            Img(backdrop, Art.Square, UIColor.Ink);
            const int walkers = 7, trail = 8;
            var walkerRts = new Object[walkers];
            var dots = new Object[walkers * trail];
            for (int i = 0; i < walkers; i++)
            {
                var color = Assets.Palette.Echo(i);
                for (int k = 0; k < trail; k++)
                {
                    var dot = Place(UI($"Trail{i}_{k}", backdrop), Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9));
                    dots[i * trail + k] = Img(dot, Art.Circle, Clear(color, 0.5f * (1f - k / (float)trail)));
                }
            }
            for (int i = 0; i < walkers; i++)
            {
                var color = Assets.Palette.Echo(i);
                var w = Place(UI($"Echo{i}", backdrop), Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
                Img(Stretch(UI("Fill", w)), Art.RoundedSquare, Clear(color, 0.18f));
                Img(Stretch(UI("Outline", w)), Art.RoundedOutline, Clear(color, 0.7f));
                walkerRts[i] = w;
            }
            var backdropView = backdrop.gameObject.AddComponent<TitleBackdrop>();
            Set(backdropView, "area", backdrop);
            SetArray(backdropView, "walkers", walkerRts);
            SetArray(backdropView, "trailDots", dots);
            Set(backdropView, "trailLength", trail);
            Img(Stretch(UI("Dim", root)), Art.Square, Clear(UIColor.Ink, 0.35f));

            // VCR on-screen display
            Img(Place(UI("PlayIcon", root), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(44, -56), new Vector2(30, 30)), Art.Triangle, UIColor.Fg);
            Txt(Place(UI("PlayLabel", root), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(86, -56), new Vector2(200, 50)), "PLAY", 40, UIColor.Fg, TextAlignmentOptions.MidlineLeft);
            Txt(Place(UI("Channel", root), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-44, -56), new Vector2(200, 50)), "CH 03", 40, UIColor.Fg, TextAlignmentOptions.MidlineRight);

            // logo
            var logo = Place(UI("Logo", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1200, 230));
            var arrowA = Place(UI("ArrowA", logo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-470, 0), new Vector2(120, 120));
            var arrowB = Place(UI("ArrowB", logo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-395, 0), new Vector2(120, 120));
            Img(arrowA, Art.Triangle, UIColor.Rec).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            Img(arrowB, Art.Triangle, UIColor.Rec).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            Txt(Place(UI("WordCyan", logo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(84, 0), new Vector2(900, 230)), "REWIND", 210, Clear(UIColor.Cyan, 0.7f), TextAlignmentOptions.Center);
            Txt(Place(UI("WordRed", logo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96, 0), new Vector2(900, 230)), "REWIND", 210, Clear(UIColor.Rec, 0.7f), TextAlignmentOptions.Center);
            Txt(Place(UI("Word", logo), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90, 0), new Vector2(900, 230)), "REWIND", 210, UIColor.Fg, TextAlignmentOptions.Center);
            Txt(Place(UI("Subtitle", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 40)),
                "A PUZZLE ABOUT CAUSE AND EFFECT, TEN SECONDS AT A TIME", 28, UIColor.Muted, TextAlignmentOptions.Center);

            var menu = MenuColumn(root, new Vector2(0, -150), 520);
            var start = AddMenuButton(menu, "START", "");
            var select = AddMenuButton(menu, "SELECT TAPE", "");
            var howTo = AddMenuButton(menu, "HOW TO PLAY", "");
            var fullscreen = AddMenuButton(menu, "FULLSCREEN", "F");
            var sound = AddMenuButton(menu, "SOUND: ON", "M");
            var quit = AddMenuButton(menu, "QUIT", "");

            var progress = Txt(Place(UI("Progress", root), new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 36), new Vector2(600, 40)), "0 OF 9 TAPES SOLVED", 26, UIColor.Muted, TextAlignmentOptions.BottomLeft);
            Txt(Place(UI("Controls", root), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 36), new Vector2(900, 40)), "ARROWS + ENTER   |   F FULLSCREEN   |   M SOUND", 26, UIColor.Muted, TextAlignmentOptions.BottomRight);

            Set(screen, "firstSelected", start.Button);
            Set(screen, "continueButton", start);
            Set(screen, "selectButton", select);
            Set(screen, "howToButton", howTo);
            Set(screen, "fullscreenButton", fullscreen);
            Set(screen, "soundButton", sound);
            Set(screen, "quitButton", quit);
            Set(screen, "progressLabel", progress);
        }

        static void BuildLevelSelect(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<LevelSelectScreen>();
            Img(Stretch(UI("Background", root)), Art.Square, UIColor.Ink);
            Txt(Place(UI("Header", root), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(64, -76), new Vector2(800, 80)), "SELECT TAPE", 64, UIColor.Fg, TextAlignmentOptions.MidlineLeft);
            var progress = Txt(Place(UI("Progress", root), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(66, -132), new Vector2(800, 40)), "0 OF 9 SOLVED", 28, UIColor.Muted, TextAlignmentOptions.MidlineLeft);

            var back = Instantiate(Widgets.MenuButton, root);
            Place((RectTransform)back.transform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-60, -76), new Vector2(220, 56));
            back.Label = "BACK";
            back.KeyHint = "ESC";

            var grid = Place(UI("Grid", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(3 * 330 + 2 * 28, 3 * 170 + 2 * 28));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(330, 170);
            layout.spacing = new Vector2(28, 28);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperCenter;

            Txt(Place(UI("Footer", root), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 40)),
                "ARROWS CHOOSE   |   ENTER PLAY   |   ESC BACK", 26, UIColor.Muted, TextAlignmentOptions.Center);

            Set(screen, "cardPrefab", Widgets.LevelCard);
            Set(screen, "grid", grid);
            Set(screen, "backButton", back);
            Set(screen, "progressLabel", progress);
            Set(screen, "firstSelected", back.Button);
        }

        static void BuildHud(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<HudScreen>();
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            // top bar: status on the left, tape in the middle, time and buttons on the right
            var left = Row(root, "Status", new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(36, -50), 16);
            var statusIcon = Img(Fixed(UI("StatusIcon", left), 34, 34), Art.Circle, UIColor.Rec);
            var statusLabel = Txt(UI("StatusLabel", left), "READY", 44, UIColor.Fg, TextAlignmentOptions.MidlineLeft);
            var parLabel = Txt(UI("ParLabel", left), "PAR 2", 32, UIColor.Muted, TextAlignmentOptions.MidlineLeft);

            var centre = Row(root, "Tape", new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -50), 14);
            var tapeLabel = Txt(UI("TapeLabel", centre), "TAPE 01", 32, UIColor.Rec, TextAlignmentOptions.MidlineLeft);
            var nameLabel = Txt(UI("NameLabel", centre), "FIRST ECHO", 46, UIColor.Fg, TextAlignmentOptions.MidlineLeft);

            var right = Row(root, "Clock", new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-32, -50), 10);
            var timeLabel = Txt(UI("TimeLabel", right), "0:00.0", 52, UIColor.Fg, TextAlignmentOptions.MidlineRight);
            Fixed(UI("Gap", right), 8, 8);
            var sound = IconButton(right, "Sound", Art.IconSound, "");
            var fullscreen = IconButton(right, "Fullscreen", Art.IconFullscreen, "");
            var pause = IconButton(right, "Pause", Art.IconPause, "");

            // prompt and toast
            var prompt = Txt(Place(UI("ReadyPrompt", root), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 336), new Vector2(1200, 50)),
                "MOVE TO START RECORDING", 34, UIColor.Fg, TextAlignmentOptions.Center);
            prompt.gameObject.AddComponent<CanvasGroup>();
            prompt.gameObject.AddComponent<Blink>();

            var toastRt = Place(UI("Toast", root), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(920, 112));
            toastRt.gameObject.AddComponent<CanvasGroup>();
            Img(toastRt, Art.UIPanel, Clear(UIColor.Bezel, 0.96f));
            var accent = Img(Stretch(UI("Accent", toastRt)), Art.UIOutline, Assets.Palette.paradox);
            var toastTitle = Txt(Place(UI("Title", toastRt), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(880, 44)), "ECHO 1 NEVER EXISTED", 34, Assets.Palette.paradox, TextAlignmentOptions.Center);
            var toastBody = Txt(Place(UI("Body", toastRt), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(880, 44)), "", 22, UIColor.Fg, TextAlignmentOptions.Center, FontStyles.Normal, wrap: true);
            var toast = toastRt.gameObject.AddComponent<ToastView>();
            Set(toast, "title", toastTitle);
            Set(toast, "body", toastBody);
            Set(toast, "accent", accent);

            // timeline
            var tl = Place(UI("Timeline", root), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(1560, 250));
            Img(tl, Art.UIPanel, Clear(UIColor.Bezel, 0.92f));
            Img(Stretch(UI("Outline", tl)), Art.UIOutline, UIColor.Line);
            var content = Stretch(UI("Content", tl), 24, 14, 24, 12);
            var seconds = Stretch(UI("Seconds", content), 120, 0, 0, 0);
            seconds.anchorMin = new Vector2(0, 1);
            seconds.offsetMin = new Vector2(120, -24);
            seconds.offsetMax = new Vector2(0, 0);
            var scale = Stretch(UI("Scale", content), 120, 0, 0, 30);
            for (int s = 0; s <= 10; s++)
            {
                float f = s / 10f;
                var label = UI($"{s}s", seconds);
                label.anchorMin = label.anchorMax = new Vector2(f, 0.5f);
                label.pivot = new Vector2(0.5f, 0.5f);
                label.sizeDelta = new Vector2(60, 24);
                Txt(label, $"{s}s", 18, UIColor.Muted, TextAlignmentOptions.Center);
                var tick = UI($"Grid{s}", scale);
                tick.anchorMin = new Vector2(f, 0);
                tick.anchorMax = new Vector2(f, 1);
                tick.sizeDelta = new Vector2(1, 0);
                Img(tick, Art.Square, UIColor.Line);
            }
            var rows = Stretch(UI("Rows", content), 0, 0, 0, 30);
            var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var playhead = UI("Playhead", scale);
            playhead.anchorMin = new Vector2(0, 0);
            playhead.anchorMax = new Vector2(0, 1);
            playhead.sizeDelta = new Vector2(3, 8);
            var playheadImage = Img(playhead, Art.Square, UIColor.Fg);
            var timeline = tl.gameObject.AddComponent<TimelineView>();
            Set(timeline, "palette", Assets.Palette);
            Set(timeline, "trackPrefab", Widgets.TimelineTrack);
            Set(timeline, "rows", rows);
            Set(timeline, "scale", scale);
            Set(timeline, "playhead", playhead);
            Set(timeline, "playheadGraphic", playheadImage);

            var keys = Txt(Place(UI("KeyHints", root), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1600, 34)),
                "ARROWS MOVE   |   SPACE END LOOP   |   R RETRY   |   Z ERASE ECHO   |   ESC MENU", 22, UIColor.Muted, TextAlignmentOptions.Center);

            // touch controls, hidden unless a touchscreen is present
            var touchRt = Stretch(UI("TouchControls", root));
            var touchRoot = Stretch(UI("Root", touchRt));
            var holds = new Object[4];
            var dpad = Place(UI("DPad", touchRoot), new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(200, 300), new Vector2(360, 360));
            (string name, PadDirection dir, Vector2 pos, float rot)[] pads =
            {
                ("Up", PadDirection.Up, new Vector2(0, 118), 90), ("Down", PadDirection.Down, new Vector2(0, -118), -90),
                ("Left", PadDirection.Left, new Vector2(-118, 0), 180), ("Right", PadDirection.Right, new Vector2(118, 0), 0),
            };
            for (int i = 0; i < pads.Length; i++)
            {
                var b = Place(UI(pads[i].name, dpad), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pads[i].pos, new Vector2(110, 110));
                var bg = Img(b, Art.UIPanel, Clear(UIColor.Bezel, 0.9f), raycast: true);
                Img(Stretch(UI("Outline", b)), Art.UIOutline, UIColor.Line);
                var arrow = Img(Place(UI("Arrow", b), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40)), Art.Triangle, UIColor.Fg);
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, pads[i].rot);
                var hold = b.gameObject.AddComponent<HoldButton>();
                Set(hold, "direction", (int)pads[i].dir);
                Set(hold, "highlight", bg);
                holds[i] = hold;
            }
            var endLoop = TouchButton(touchRoot, "EndLoop", "END LOOP", new Vector2(-150, 420), new Vector2(250, 100), UIColor.Rec);
            var retry = TouchButton(touchRoot, "Retry", "RETRY", new Vector2(-150, 300), new Vector2(250, 84), UIColor.Muted);
            var erase = TouchButton(touchRoot, "Erase", "ERASE", new Vector2(-150, 196), new Vector2(250, 84), UIColor.Muted);
            var touch = touchRt.gameObject.AddComponent<TouchControls>();
            Set(touch, "root", touchRoot.gameObject);
            Set(touch, "endLoop", endLoop);
            Set(touch, "retry", retry);
            Set(touch, "erase", erase);
            touchRoot.gameObject.SetActive(false);

            Set(screen, "palette", Assets.Palette);
            Set(screen, "statusIcon", statusIcon);
            Set(screen, "statusLabel", statusLabel);
            Set(screen, "recordSprite", Art.Circle);
            Set(screen, "readySprite", Art.IconPause);
            Set(screen, "rewindSprite", Art.IconRewind);
            Set(screen, "stopSprite", Art.IconStop);
            Set(screen, "parLabel", parLabel);
            Set(screen, "tapeLabel", tapeLabel);
            Set(screen, "nameLabel", nameLabel);
            Set(screen, "timeLabel", timeLabel);
            Set(screen, "textColor", UIColor.Fg);
            Set(screen, "soundButton", sound);
            Set(screen, "fullscreenButton", fullscreen);
            Set(screen, "pauseButton", pause);
            Set(screen, "soundIcon", sound.transform.Find("Icon").GetComponent<Image>());
            Set(screen, "soundOnSprite", Art.IconSound);
            Set(screen, "soundOffSprite", Art.IconMute);
            Set(screen, "fullscreenIcon", fullscreen.transform.Find("Icon").GetComponent<Image>());
            Set(screen, "enterFullscreenSprite", Art.IconFullscreen);
            Set(screen, "exitFullscreenSprite", Art.IconWindowed);
            Set(screen, "toast", toast);
            Set(screen, "readyPrompt", prompt);
            Set(screen, "keyHints", keys.gameObject);
            Set(screen, "touchControls", touch);
        }

        static void BuildPause(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<PausePanel>();
            Img(Stretch(UI("Dim", root)), Art.Square, Clear(UIColor.Ink, 0.7f), raycast: true);
            var card = Card(root, new Vector2(580, 392), UIColor.Line);
            Txt(Place(UI("Title", card), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(500, 70)), "PAUSED", 56, UIColor.Fg, TextAlignmentOptions.Center);
            var menu = MenuColumn(card, Vector2.zero, 500);
            Place(menu, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(500, 0));
            var resume = AddMenuButton(menu, "RESUME", "ESC");
            var howTo = AddMenuButton(menu, "HOW TO PLAY", "");
            var select = AddMenuButton(menu, "SELECT TAPE", "");
            var title = AddMenuButton(menu, "TITLE SCREEN", "");
            Set(screen, "firstSelected", resume.Button);
            Set(screen, "resume", resume);
            Set(screen, "howTo", howTo);
            Set(screen, "levelSelect", select);
            Set(screen, "title", title);
        }

        static void BuildSolved(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<SolvedPanel>();
            Img(Stretch(UI("Dim", root)), Art.Square, Clear(UIColor.Ink, 0.55f), raycast: true);
            var card = Card(root, new Vector2(660, 520), UIColor.Exit);
            Txt(Place(UI("Headline", card), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -84), new Vector2(620, 120)), "SOLVED", 110, UIColor.Exit, TextAlignmentOptions.Center);
            var loops = Txt(Place(UI("Loops", card), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -168), new Vector2(620, 50)), "2 LOOPS  |  PAR 2", 40, UIColor.Fg, TextAlignmentOptions.Center);
            var verdict = Txt(Place(UI("Verdict", card), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -218), new Vector2(620, 40)), "MATCHED PAR", 28, UIColor.Amber, TextAlignmentOptions.Center);
            var menu = MenuColumn(card, Vector2.zero, 520);
            Place(menu, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -268), new Vector2(520, 0));
            var next = AddMenuButton(menu, "NEXT TAPE", "ENTER");
            var replay = AddMenuButton(menu, "REPLAY LAST LOOP", "");
            var select = AddMenuButton(menu, "SELECT TAPE", "ESC");
            Set(screen, "firstSelected", next.Button);
            Set(screen, "loopsLabel", loops);
            Set(screen, "verdictLabel", verdict);
            Set(screen, "next", next);
            Set(screen, "replay", replay);
            Set(screen, "levelSelect", select);
            Set(screen, "atParColor", UIColor.Amber);
            Set(screen, "overParColor", UIColor.Muted);
        }

        static readonly string[] Rules =
        {
            "Every loop lasts 10 seconds. Then time rewinds and you start over.",
            "Each past loop replays as an echo that repeats exactly what you did.",
            "An echo has to be able to repeat its history. If it can't, it never existed, and it vanishes on the spot.",
            "Bodies pass through each other, but block crates.",
            "Cracked floor gives way once nobody is standing on it. Push a crate into a hole to fill it.",
            "A door opens while every plate of its colour is held. Dots on a door count its plates. Striped doors do the opposite.",
        };

        static void BuildHowTo(RectTransform root)
        {
            var screen = root.gameObject.AddComponent<HowToPlayPanel>();
            Img(Stretch(UI("Dim", root)), Art.Square, Clear(UIColor.Ink, 0.8f), raycast: true);
            var card = Card(root, new Vector2(1560, 880), UIColor.Line);
            Txt(Place(UI("Title", card), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(60, -66), new Vector2(900, 80)), "HOW TIME WORKS", 60, UIColor.Fg, TextAlignmentOptions.MidlineLeft);
            var back = Instantiate(Widgets.MenuButton, card);
            Place((RectTransform)back.transform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-52, -66), new Vector2(220, 56));
            back.Label = "BACK";
            back.KeyHint = "ESC";

            var rules = Place(UI("Rules", card), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -130), new Vector2(820, 560));
            var rulesLayout = rules.gameObject.AddComponent<VerticalLayoutGroup>();
            rulesLayout.spacing = 22;
            rulesLayout.childControlHeight = true;
            rulesLayout.childControlWidth = true;
            rulesLayout.childForceExpandHeight = false;
            for (int i = 0; i < Rules.Length; i++)
            {
                var row = UI($"Rule{i + 1}", rules);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 20;
                h.childAlignment = TextAnchor.UpperLeft;
                h.childControlHeight = true;
                h.childControlWidth = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                h.padding = new RectOffset(0, 0, 0, 0);
                var bullet = Fixed(UI("Bullet", row), 12, 12);
                Img(bullet, Art.Square, UIColor.Rec);
                var text = Txt(UI("Text", row), Rules[i], 25, UIColor.Fg, TextAlignmentOptions.TopLeft, FontStyles.Normal, wrap: true);
                text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }

            var legend = Place(UI("Legend", card), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -130), new Vector2(580, 560));
            Txt(Place(UI("Heading", legend), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, -16), new Vector2(580, 34)), "ON THE BOARD", 26, UIColor.Muted, TextAlignmentOptions.MidlineLeft);
            var grid = Stretch(UI("Items", legend), 0, 0, 0, 50);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(285, 88);
            gl.spacing = new Vector2(10, 18);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            var p = Assets.Palette;
            LegendItem(grid, "Plate", icon =>
            {
                Img(Place(UI("Fill", icon), Half, Half, Vector2.zero, new Vector2(42, 42)), Art.RoundedSquare, Clear(p.Channel(1), 0.12f));
                Img(Place(UI("Frame", icon), Half, Half, Vector2.zero, new Vector2(42, 42)), Art.RoundedOutline, p.Channel(1));
            });
            LegendItem(grid, "Crate", icon => Img(Stretch(UI("Crate", icon)), Art.Crate, p.crate));
            LegendItem(grid, "Opens while held", icon => Img(Stretch(UI("Door", icon)), Art.DoorBars, p.Channel(1)));
            LegendItem(grid, "Shuts while held", icon => Img(Stretch(UI("Door", icon)), Art.DoorStripes, p.Channel(1)));
            LegendItem(grid, "Cracked floor", icon => Img(Stretch(UI("Cracks", icon)), Art.Cracks, p.crack));
            LegendItem(grid, "Hole", icon =>
            {
                Img(Stretch(UI("Pit", icon)), Art.Square, p.pit);
                Img(Place(UI("Rim", icon), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(64, 10)), Art.Square, p.wallFace);
            });
            LegendItem(grid, "Echo and its trail", icon =>
            {
                Img(Place(UI("Fill", icon), Half, Half, Vector2.zero, new Vector2(38, 38)), Art.RoundedSquare, Clear(p.Echo(0), 0.26f));
                Img(Place(UI("Outline", icon), Half, Half, Vector2.zero, new Vector2(38, 38)), Art.RoundedOutline, p.Echo(0));
            });
            LegendItem(grid, "Exit", icon =>
            {
                Img(Place(UI("RingA", icon), Half, Half, Vector2.zero, new Vector2(41, 41)), Art.Ring, p.exit);
                Img(Place(UI("RingB", icon), Half, Half, Vector2.zero, new Vector2(26, 26)), Art.Ring, Clear(p.exit, 0.7f)).rectTransform.localRotation = Quaternion.Euler(0, 0, 140);
            });

            Txt(Place(UI("Controls", card), new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 40), new Vector2(1440, 64)),
                "ARROWS / WASD move   |   SPACE end loop early   |   R retry loop   |   Z erase last echo   |   ESC menu   |   F fullscreen   |   M sound",
                22, UIColor.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Normal, wrap: true);
            Set(screen, "back", back);
            Set(screen, "firstSelected", back.Button);
        }

        static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        static void LegendItem(RectTransform grid, string label, System.Action<RectTransform> drawIcon)
        {
            var item = UI(label, grid);
            var icon = Place(UI("Icon", item), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(64, 64));
            Img(icon, Art.Square, Assets.Palette.floorA);
            drawIcon(icon);
            Img(Stretch(UI("Frame", icon)), Art.UIOutline, UIColor.Line);
            Txt(Place(UI("Label", item), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(205, 64)), label, 20, UIColor.Fg, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, wrap: true);
        }

        // ---- Root ---------------------------------------------------------------------------------------------

        static void BuildUIRoot()
        {
            Progress("Assembling UIRoot", 0.88f);
            string path = $"{UIDir}/UIRoot.prefab";
            if (ShouldBuild(path))
            {
                var root = UI("UIRoot", null);
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                root.gameObject.AddComponent<GraphicRaycaster>();
                var router = root.gameObject.AddComponent<UIRouter>();

                // screens back to front; later siblings draw on top
                var hud = Nest<HudScreen>(Screens.Hud, root, false);
                var title = Nest<TitleScreen>(Screens.Title, root, true);
                var select = Nest<LevelSelectScreen>(Screens.LevelSelect, root, false);
                var pause = Nest<PausePanel>(Screens.Pause, root, false);
                var solved = Nest<SolvedPanel>(Screens.Solved, root, false);
                var howTo = Nest<HowToPlayPanel>(Screens.HowTo, root, false);

                foreach (var s in new UIScreen[] { hud, title, select, pause, solved, howTo }) Set(s, "router", router);
                foreach (var t in hud.GetComponentsInChildren<TimelineView>(true)) Set(t, "router", router);
                foreach (var t in hud.GetComponentsInChildren<TouchControls>(true)) Set(t, "router", router);
                foreach (var h in hud.GetComponentsInChildren<HoldButton>(true)) Set(h, "router", router);

                Set(router, "title", title);
                Set(router, "levelSelect", select);
                Set(router, "hud", hud);
                Set(router, "pause", pause);
                Set(router, "solved", solved);
                Set(router, "howTo", howTo);

                // CRT treatment over everything
                var crt = Stretch(UI("CRT", root));
                var lines = Img(Stretch(UI("Scanlines", crt)), Art.Scanlines, new Color(0, 0, 0, 0.22f));
                lines.type = Image.Type.Tiled;
                Img(Stretch(UI("Vignette", crt)), Art.Vignette, new Color(0, 0, 0, 0.55f));

                var shell = root.gameObject.AddComponent<ShellInput>();
                Set(shell, "router", router);
                Set(shell, "actions", Assets.Controls);

                var source = root.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                var audio = root.gameObject.AddComponent<UIAudio>();
                Set(audio, "source", source);
                Set(audio, "navigate", Sfx.UiMove);
                Set(audio, "activate", Sfx.UiSelect);

                SavePrefab(root.gameObject, path);
            }
            Screens.Root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static T Nest<T>(GameObject prefab, RectTransform parent, bool visible) where T : UIScreen
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            go.GetComponent<CanvasGroup>().alpha = visible ? 1f : 0f;
            go.SetActive(visible);
            return go.GetComponent<T>();
        }

        // ---- uGUI helpers ---------------------------------------------------------------------------------------

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            if (parent) rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        static RectTransform Fixed(RectTransform rt, float width, float height)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = width;
            le.preferredHeight = le.minHeight = height;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static TextMeshProUGUI Txt(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align,
            FontStyles style = FontStyles.Bold, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.characterSpacing = style == FontStyles.Bold ? 3f : 0f;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Color Clear(Color c, float alpha = 0f)
        {
            c.a = alpha;
            return c;
        }

        static void NoNavigation(Selectable s)
        {
            var n = s.navigation;
            n.mode = Navigation.Mode.None;
            s.navigation = n;
        }

        static RectTransform Card(RectTransform parent, Vector2 size, Color outline)
        {
            var card = Place(UI("Card", parent), Half, Half, Vector2.zero, size);
            Img(card, Art.UIPanel, UIColor.Bezel, raycast: true);
            Img(Stretch(UI("Outline", card)), Art.UIOutline, outline);
            return card;
        }

        static RectTransform Row(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, float spacing)
        {
            var row = Place(UI(name, parent), anchor, pivot, position, new Vector2(0, 60));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return row;
        }

        static RectTransform MenuColumn(RectTransform parent, Vector2 position, float width)
        {
            var menu = Place(UI("Menu", parent), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), position, new Vector2(width, 0));
            var v = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = menu.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return menu;
        }

        static MenuButton AddMenuButton(RectTransform menu, string label, string key)
        {
            var b = Instantiate(Widgets.MenuButton, menu);
            b.gameObject.name = label;
            b.Label = label;
            b.KeyHint = key;
            return b;
        }

        static T Instantiate<T>(T prefab, Transform parent) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            return go.GetComponent<T>();
        }

        static Button IconButton(RectTransform parent, string name, Sprite icon, string glyph)
        {
            var b = Instantiate(Widgets.IconButton, parent);
            b.gameObject.name = name;
            var image = b.transform.Find("Icon").GetComponent<Image>();
            image.sprite = icon;
            image.enabled = icon;
            b.transform.Find("Glyph").GetComponent<TMP_Text>().text = glyph;
            return b;
        }

        static Button TouchButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Color outline)
        {
            var rt = Place(UI(name, parent), new Vector2(1, 0), new Vector2(0.5f, 0.5f), position, size);
            var bg = Img(rt, Art.UIPanel, Clear(UIColor.Bezel, 0.9f), raycast: true);
            Img(Stretch(UI("Outline", rt)), Art.UIOutline, outline);
            Txt(Stretch(UI("Label", rt)), label, 32, UIColor.Fg, TextAlignmentOptions.Center);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            NoNavigation(b);
            return b;
        }
    }
}
