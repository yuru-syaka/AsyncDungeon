using System;
using System.Collections.Generic;

namespace DungeonGame.Core.Dungeon
{
    /// <summary>
    /// 1つの層のルール(大きさ・掘削予算・土の豊かさ・ゴールなど)。
    /// どのモンスターが生まれるかは層では決めず、土の養分・魔分の濃さと特殊な条件で決まる(土の豊かな深い層ほど強いものが生まれやすい)。
    /// JSON(Resources/GameData/layer_rules.json)で定義し、層を増やすときはデータを足すだけでよい。
    /// </summary>
    [Serializable]
    public sealed class LayerRule
    {
        /// <summary>層番号(1始まり)。</summary>
        public int Layer;
        /// <summary>表示名。</summary>
        public string Name;
        /// <summary>マップの幅・高さ(外枠の岩盤を含む)。</summary>
        public int Width;
        public int Height;
        /// <summary>掘削予算(掘れるマス数の上限)。</summary>
        public int DigBudget;
        /// <summary>この層のゴール。</summary>
        public GoalType Goal;
        /// <summary>土1マスあたりの養分・魔分の最大値(マップ生成用)。</summary>
        public int SoilNutrientMax;
        public int SoilManaMax;
        /// <summary>1マスに溜められる養分・魔分の上限。</summary>
        public int TileResourceCap;
        /// <summary>生態系を動かして育てられる合計ティック数の上限。</summary>
        public int MaxEcologyTicks;
        /// <summary>毎ティック、下位モンスターが濃い土から自然に湧く判定を行う確率(%)。</summary>
        public int WildSpawnPercent;
        /// <summary>自然発生に必要な、土の養分(魔分)の誕生条件からの上乗せ量。</summary>
        public int WildSpawnMargin;
        /// <summary>勇者の攻略の制限時間(ティック)。過ぎたら勇者側の失敗。</summary>
        public int RaidTimeLimitTicks = 3000;
        /// <summary>ゴールが魔王の層で、ゴールに置くボスの種族ID。</summary>
        public string BossSpecies;
    }

    /// <summary>全層ぶんのルール集。</summary>
    [Serializable]
    public sealed class LayerRuleSet
    {
        public List<LayerRule> Layers = new List<LayerRule>();

        public int LayerCount => Layers.Count;

        public LayerRule Get(int layer)
        {
            foreach (var r in Layers)
                if (r.Layer == layer) return r;
            return null;
        }
    }
}
