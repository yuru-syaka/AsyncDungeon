using System;
using DungeonGame.Core.Data;
using DungeonGame.Core.Serialization;
using DungeonGame.Core.Simulation;

namespace DungeonGame.Core.Run
{
    /// <summary>入力ログのJSON変換。</summary>
    public static class RunLogIO
    {
        public static string ToJson(RunLog log) => GameJson.Serialize(log);

        /// <summary>JSONからログを読む。壊れている・新しすぎる形式なら例外。</summary>
        public static RunLog FromJson(string json)
        {
            RunLog log;
            try { log = GameJson.Deserialize<RunLog>(json); }
            catch (Exception e) { throw new SnapshotFormatException("入力ログのJSONが読めません: " + e.Message); }
            if (log == null) throw new SnapshotFormatException("入力ログが空です");
            if (log.FormatVersion > RunLog.CurrentFormatVersion)
                throw new SnapshotFormatException($"未対応の入力ログ形式 {log.FormatVersion}");
            return log;
        }
    }

    /// <summary>再計算(検証)の結果。</summary>
    public sealed class RunReplayResult
    {
        public bool IsValid;
        /// <summary>不一致・不正があったときの理由。</summary>
        public string Error;
        /// <summary>再計算して得られた★。</summary>
        public int Stars;
        public ulong FinalHash;
        public RunState Run;

        public override string ToString() => IsValid ? $"OK ★{Stars}" : "NG: " + Error;
    }

    /// <summary>
    /// 入力ログからランを再現する。
    ///  ・画面での再生:Step() を1ティックずつ呼ぶ(SimulationDriver でそのまま動かせる)
    ///  ・検証:Verify() で最後まで一気に回し、ログに書かれた結果と一致するか調べる
    /// どちらも同じ処理なので、「再生では一致するのに検証では食い違う」ことは起きない。
    /// 将来はサーバーで Verify を動かし、クライアントが申告した★が正しいかを確かめる。
    ///
    /// ログの入力が実際には実行できない(候補にないスキル・残り回数を超える介入など)場合は、
    /// その時点で Error を設定して止まる。
    /// </summary>
    public sealed class RunReplayer : ITickSimulation
    {
        public RunLog Log { get; }
        public RunState Run { get; }
        /// <summary>不正・不一致があれば理由が入る(以降は進まない)。</summary>
        public string Error { get; private set; }
        /// <summary>再生中の層(ログ上の番号、0始まり)。</summary>
        public int LayerIndex { get; private set; }

        private int _cmdIndex;

        public RunReplayer(RunLog log, GameData data)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            if (log.FormatVersion > RunLog.CurrentFormatVersion) { Error = $"未対応の入力ログ形式 {log.FormatVersion}"; return; }
            try
            {
                Run = RunState.Start(data, log.Seed, log.PartyClasses);
            }
            catch (Exception e)
            {
                Error = "編成が不正です: " + e.Message;
                return;
            }
            if (log.Layers == null || log.Layers.Count == 0) { Error = "層の記録がありません"; return; }
            CheckLayerStart();
        }

        public bool IsFinished => Error != null || Run == null || Run.IsOver;
        public int CurrentTick => Run?.CurrentRaid?.CurrentTick ?? 0;
        private RunLayerLog LayerLog => Log.Layers[LayerIndex];

        /// <summary>1ティック進める。攻略が終わったら、ログどおりに報酬を選んで次の層を始める。</summary>
        public void Step()
        {
            if (IsFinished) return;
            var raid = Run.CurrentRaid;
            var cmds = LayerLog.Commands;

            if (_cmdIndex < cmds.Count && cmds[_cmdIndex].Tick < raid.CurrentTick)
            {
                Fail($"{LayerLog.Layer}層: 介入の記録が時間順になっていません");
                return;
            }
            while (_cmdIndex < cmds.Count && cmds[_cmdIndex].Tick == raid.CurrentTick)
            {
                var c = cmds[_cmdIndex++];
                if (!raid.Submit(c.Type, c.X, c.Y))
                {
                    Fail($"{LayerLog.Layer}層 {c.Tick}ティック: 介入 {c.Type} は実行できません(回数切れ・範囲外)");
                    return;
                }
            }

            raid.Step();
            if (raid.IsFinished) FinishLayer();
        }

        private void FinishLayer()
        {
            var expected = LayerLog;
            if (_cmdIndex != expected.Commands.Count)
            {
                Fail($"{expected.Layer}層: 攻略終了後の介入が記録されています");
                return;
            }
            Run.CompleteRaid();
            var actual = Run.Log.Layers[LayerIndex];
            if (actual.Outcome != expected.Outcome || actual.EndTick != expected.EndTick)
            {
                Fail($"{expected.Layer}層: 攻略結果が一致しません(記録 {expected.Outcome}/{expected.EndTick}ティック, 再計算 {actual.Outcome}/{actual.EndTick}ティック)");
                return;
            }

            if (Run.Phase != RunPhase.Reward)
            {
                if (Log.Layers.Count != LayerIndex + 1) Fail("ランが終わった後の層が記録されています");
                return;
            }

            if (expected.SkillChoice != null && !Run.ChooseSkill(expected.SkillChoice))
            {
                Fail($"{expected.Layer}層: スキル '{expected.SkillChoice}' は候補にありません");
                return;
            }
            foreach (var a in expected.Allocations)
            {
                if (!Run.Allocate(a.HeroIndex, a.Stat, a.Points))
                {
                    Fail($"{expected.Layer}層: ポイント配分が不正です(勇者{a.HeroIndex} {a.Stat} {a.Points})");
                    return;
                }
            }
            if (!Run.ConfirmReward())
            {
                Fail($"{expected.Layer}層: 報酬が確定できません(スキル未選択)");
                return;
            }

            LayerIndex++;
            _cmdIndex = 0;
            if (LayerIndex >= Log.Layers.Count) { Fail("ログが途中で終わっています"); return; }
            CheckLayerStart();
        }

        private void CheckLayerStart()
        {
            var l = LayerLog;
            if (l.Layer != Run.Layer) Fail($"層番号が一致しません(記録 {l.Layer}, 再計算 {Run.Layer})");
            else if (l.DungeonId != Run.CurrentDungeonId) Fail($"{l.Layer}層: ダンジョンが一致しません(記録 {l.DungeonId}, 再計算 {Run.CurrentDungeonId})");
            else if (l.DungeonHash != Run.Log.Layers[LayerIndex].DungeonHash) Fail($"{l.Layer}層: ダンジョンの中身が記録と違います");
        }

        private void Fail(string message)
        {
            if (Error == null) Error = message;
        }

        /// <summary>最後まで再計算して結果をまとめる。</summary>
        public RunReplayResult RunToEnd()
        {
            while (!IsFinished) Step();
            var result = new RunReplayResult { Run = Run, Error = Error };
            if (Run != null)
            {
                result.Stars = Run.Stars;
                result.FinalHash = Run.IsOver ? Run.Log.FinalHash : 0;
            }
            if (result.Error == null)
            {
                if (result.Stars != Log.Stars) result.Error = $"★が一致しません(記録 {Log.Stars}, 再計算 {result.Stars})";
                else if (result.FinalHash != Log.FinalHash) result.Error = "最終状態のハッシュが一致しません";
                else if (RunLogIO.ToJson(Run.Log) != RunLogIO.ToJson(Log)) result.Error = "ログの内容が再計算結果と一致しません";
            }
            result.IsValid = result.Error == null;
            return result;
        }

        /// <summary>入力ログを検証する(サーバー側での利用を想定)。</summary>
        public static RunReplayResult Verify(RunLog log, GameData data) => new RunReplayer(log, data).RunToEnd();

        /// <summary>JSON文字列の入力ログを検証する。</summary>
        public static RunReplayResult Verify(string logJson, GameData data)
        {
            try { return Verify(RunLogIO.FromJson(logJson), data); }
            catch (Exception e) { return new RunReplayResult { IsValid = false, Error = e.Message }; }
        }
    }
}
