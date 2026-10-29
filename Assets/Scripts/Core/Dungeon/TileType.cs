namespace DungeonGame.Core.Dungeon
{
    /// <summary>マスの種類。</summary>
    public enum TileType : byte
    {
        /// <summary>岩盤。掘れない外枠。</summary>
        Bedrock = 0,
        /// <summary>土。掘ると通路になる。養分・魔分を含む。</summary>
        Soil = 1,
        /// <summary>掘った通路・部屋。</summary>
        Floor = 2,
        /// <summary>勇者の入口。</summary>
        Entrance = 3,
        /// <summary>ゴール(次層への階段 または 魔王の間)。</summary>
        Goal = 4,
    }

    /// <summary>層のゴールの種類。</summary>
    public enum GoalType
    {
        /// <summary>次の層への階段。</summary>
        Stairs = 0,
        /// <summary>魔王(倒すとクリア)。</summary>
        DemonLord = 1,
    }

    /// <summary>マス種別とJSON用の1文字の相互変換。</summary>
    public static class TileChars
    {
        public const char Bedrock = '#';
        public const char Soil = 's';
        public const char Floor = '.';
        public const char Entrance = 'E';
        public const char Goal = 'G';

        public static char ToChar(TileType t)
        {
            switch (t)
            {
                case TileType.Bedrock: return Bedrock;
                case TileType.Soil: return Soil;
                case TileType.Floor: return Floor;
                case TileType.Entrance: return Entrance;
                case TileType.Goal: return Goal;
                default: return '?';
            }
        }

        public static bool TryParse(char c, out TileType t)
        {
            switch (c)
            {
                case Bedrock: t = TileType.Bedrock; return true;
                case Soil: t = TileType.Soil; return true;
                case Floor: t = TileType.Floor; return true;
                case Entrance: t = TileType.Entrance; return true;
                case Goal: t = TileType.Goal; return true;
                default: t = TileType.Bedrock; return false;
            }
        }

        /// <summary>勇者やモンスターが歩けるマスか。</summary>
        public static bool IsWalkable(TileType t) =>
            t == TileType.Floor || t == TileType.Entrance || t == TileType.Goal;
    }
}
