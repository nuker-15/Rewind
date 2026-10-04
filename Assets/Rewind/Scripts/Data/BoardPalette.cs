using UnityEngine;

namespace Rewind.Data
{
    /// <summary>Every colour the board and timeline use. Swap this asset to restyle the game.</summary>
    [CreateAssetMenu(menuName = "Rewind/Board Palette", fileName = "BoardPalette", order = 3)]
    public sealed class BoardPalette : ScriptableObject
    {
        [Header("Scene")]
        public Color background = Hex("090A14");
        public Color floorA = Hex("12152C");
        public Color floorB = Hex("151831");
        public Color wall = Hex("2A2F5C");
        public Color wallTop = Hex("444B8C");
        public Color wallFace = Hex("1B1F43");
        public Color pit = Hex("04050C");
        public Color crack = Hex("7D81B3");
        public Color crackStepped = Hex("FF4B55");

        [Header("Pieces")]
        public Color player = Hex("F6F4FF");
        public Color crate = Hex("C98B4F");
        public Color crateDark = Hex("6E4422");
        public Color exit = Hex("C4FF6A");
        public Color startMarker = Hex("989BC3");

        [Header("Signals")]
        public Color record = Hex("FF4B55");
        public Color paradox = Hex("FF3FC0");
        public Color rewind = Hex("48D8FF");
        public Color pipLit = Hex("EBE9F8");
        public Color pipDark = Hex("090A14");

        [Header("Plate and door channels (1, 2, 3)")]
        public Color[] channels = { Hex("FFB547"), Hex("48D8FF"), Hex("FF8A5C") };

        [Header("Echoes, in recording order")]
        public Color[] echoes = { Hex("B8A4FF"), Hex("7CF2C9"), Hex("FFC49C"), Hex("9FD2FF"), Hex("FF9FC4"), Hex("D9DCFF") };

        public Color Channel(int channel) =>
            channels == null || channels.Length == 0 ? Color.white : channels[Mathf.Clamp(channel - 1, 0, channels.Length - 1)];

        public Color Echo(int index) =>
            echoes == null || echoes.Length == 0 ? Color.white : echoes[((index % echoes.Length) + echoes.Length) % echoes.Length];

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
