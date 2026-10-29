using System.Collections.Generic;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>検証エラーの種類。</summary>
    public enum ValidationCode
    {
        /// <summary>JSONの形式が壊れている。</summary>
        FormatError,
        UnknownLayer,
        SizeMismatch,
        GoalTypeMismatch,
        BorderNotBedrock,
        EntranceCount,
        GoalCount,
        /// <summary>掘削予算を超えている。</summary>
        BudgetExceeded,
        /// <summary>入口からゴールまでの経路がない(完全封鎖)。</summary>
        PathBlocked,
        InvalidResource,
        /// <summary>ダンジョンに置けないモンスター(魔王など)がいる。</summary>
        MonsterNotAllowed,
        InvalidMonster,
    }

    public struct ValidationError
    {
        public ValidationCode Code;
        public string Message;
        public override string ToString() => $"{Code}: {Message}";
    }

    /// <summary>検証結果。エラーが1つもなければ有効。</summary>
    public sealed class ValidationResult
    {
        public readonly List<ValidationError> Errors = new List<ValidationError>();
        public bool IsValid => Errors.Count == 0;

        public bool Has(ValidationCode code)
        {
            foreach (var e in Errors) if (e.Code == code) return true;
            return false;
        }

        internal void Add(ValidationCode code, string message)
            => Errors.Add(new ValidationError { Code = code, Message = message });

        public override string ToString() => IsValid ? "OK" : string.Join("\n", Errors);
    }

    /// <summary>
    /// ダンジョンを保存してよいか検証する。
    /// 将来はサーバー側でも同じ検証を行い、改ざんされたスナップショットを弾く。
    /// そのため予算はマップ自身の値ではなく、層ルールの値と実際に掘られたマス数で判定する。
    /// </summary>
    public static class DungeonValidator
    {
        /// <summary>マップに加えて、モンスターと生態系の状態も検証する。</summary>
        public static ValidationResult ValidateState(DungeonState state, Data.GameData data)
        {
            var result = ValidateMap(state.Map, data.Rules);
            var rule = data.Rules.Get(state.Map.Layer);
            if (rule == null) return result;

            if (state.EcologyTick < 0 || state.EcologyTick > rule.MaxEcologyTicks)
                result.Add(ValidationCode.InvalidMonster, $"育成ティック {state.EcologyTick} が上限 {rule.MaxEcologyTicks} を超えています");

            foreach (var m in state.Monsters)
            {
                if (m.Dead) continue;
                // どの種族が生まれるかは土と特殊な条件で決まるので、層による制限はない。魔王だけは置けない。
                if (m.Species.IsBoss)
                    result.Add(ValidationCode.MonsterNotAllowed, $"{m.Species.Name}はダンジョンに置けません");
                if (state.Map.GetTile(m.Pos) != TileType.Floor)
                    result.Add(ValidationCode.InvalidMonster, $"モンスター{m.Id}が通路ではない場所 {m.Pos} にいます");
                if (m.Nutrient < 0 || m.Mana < 0 || m.Biomass > m.Species.MaxBiomass)
                    result.Add(ValidationCode.InvalidMonster, $"モンスター{m.Id}の体の養分・魔分が範囲外です");
                if (m.Hp <= 0 || m.Hp > m.Species.MaxHp)
                    result.Add(ValidationCode.InvalidMonster, $"モンスター{m.Id}のHPが範囲外です");
            }
            return result;
        }

        public static ValidationResult ValidateMap(DungeonMap map, LayerRuleSet rules)
        {
            var result = new ValidationResult();
            var rule = rules.Get(map.Layer);
            if (rule == null)
            {
                result.Add(ValidationCode.UnknownLayer, $"層{map.Layer}のルールがありません");
                return result;
            }

            if (map.Width != rule.Width || map.Height != rule.Height)
            {
                result.Add(ValidationCode.SizeMismatch,
                    $"サイズ {map.Width}x{map.Height} が層ルール {rule.Width}x{rule.Height} と違います");
                return result;
            }

            if (map.GoalType != rule.Goal)
                result.Add(ValidationCode.GoalTypeMismatch, $"この層のゴールは {rule.Goal} です");

            int entrances = 0, goals = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var t = map.GetTile(x, y);
                bool edge = x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;
                if (edge && t != TileType.Bedrock)
                {
                    result.Add(ValidationCode.BorderNotBedrock, $"外周 ({x},{y}) が岩盤ではありません");
                }
                if (t == TileType.Entrance) entrances++;
                if (t == TileType.Goal) goals++;

                int n = map.GetNutrient(x, y), m = map.GetMana(x, y);
                if (n < 0 || m < 0 || n > rule.TileResourceCap || m > rule.TileResourceCap)
                    result.Add(ValidationCode.InvalidResource, $"({x},{y}) の養分・魔分が範囲外です");
                if (map.GetBones(x, y) < 0)
                    result.Add(ValidationCode.InvalidResource, $"({x},{y}) の骨の数が範囲外です");
            }
            if (entrances != 1) result.Add(ValidationCode.EntranceCount, $"入口が{entrances}個あります(1個にしてください)");
            if (goals != 1) result.Add(ValidationCode.GoalCount, $"ゴールが{goals}個あります(1個にしてください)");

            int dug = map.DugCount;
            if (dug > rule.DigBudget)
                result.Add(ValidationCode.BudgetExceeded, $"掘削数 {dug} が予算 {rule.DigBudget} を超えています");

            if (entrances == 1 && goals == 1 && !PathFinder.HasPath(map, map.Entrance, map.Goal))
                result.Add(ValidationCode.PathBlocked, "入口からゴールまでの道がありません");

            return result;
        }
    }
}
