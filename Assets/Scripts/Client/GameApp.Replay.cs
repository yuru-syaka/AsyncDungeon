using DungeonGame.Client.Art;
using DungeonGame.Core.Run;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>リプレイ:入力ログからランを再生し、最後に記録された結果と一致するか検証する。</summary>
    public partial class GameApp
    {
        private RunReplayer _replayer;
        private RunReplayResult _replayResult;
        private string _replayInput = "";
        private string _replayMessage = "";

        private void EnterReplay()
        {
            _screen = Screen.Replay;
            _replayer = null;
            _replayResult = null;
            if (string.IsNullOrEmpty(_replayInput)) _replayInput = SaveStore.Load(SaveStore.LastRunLogKey) ?? "";
            _replayMessage = string.IsNullOrEmpty(_replayInput)
                ? "保存された入力ログがありません。勇者モードで遊んで「ログを保存」するか、JSONを貼り付けてください。"
                : "保存された入力ログを読み込みました。";
            OpenHelpFirstTime("replay", ReplayHelp);
        }

        private void StartReplay(RunLog log)
        {
            _screen = Screen.Replay;
            _replayInput = RunLogIO.ToJson(log);
            _replayResult = null;
            _replayer = new RunReplayer(RunLogIO.FromJson(_replayInput), _data);
            _replayMessage = _replayer.Error ?? "";
            _driver.Simulation = _replayer;
            _driver.Paused = false;
            _driver.SpeedPercent = 400;
        }

        private void DrawReplay()
        {
            if (_replayer == null) { DrawReplaySelect(); return; }

            var run = _replayer.Run;
            var raid = run?.CurrentRaid;
            if (raid != null) ShowRaid(raid);

            // 再生が終わったら検証結果をまとめる
            if (_replayer.IsFinished && _replayResult == null) _replayResult = _replayer.RunToEnd();

            Panel("リプレイ", col =>
            {
                var head = col.Next(22f, 4f);
                Ui.Text(head, $"シード {_replayer.Log.Seed}", Ui.Small, Ui.Cream);
                var party = col.Next(36f, 4f);
                for (int i = 0; i < _replayer.Log.PartyClasses.Count; i++)
                    Ui.Sprite(new Rect(party.x + i * 40, party.y, 34, 34), Sprites.Anim(_replayer.Log.PartyClasses[i], i * 3));
                if (raid != null) RaidStatus(col, raid);
                col.Space(4);
                SpeedButtons(col);
                if (_replayResult != null)
                {
                    var r = col.Next(40f, 4f);
                    Ui.Fill(r, _replayResult.IsValid ? new Color(0.1f, 0.35f, 0.15f) : new Color(0.4f, 0.1f, 0.1f));
                    Ui.Text(new Rect(r.x + 10, r.y + 6, r.width - 20, 30),
                        _replayResult.IsValid ? $"検証OK:記録どおり ★{_replayResult.Stars}" : "検証NG", Ui.Title, Ui.Cream);
                    if (!_replayResult.IsValid) GUI.Label(col.Next(70f), _replayResult.Error, Ui.Wrap);
                }
                if (!string.IsNullOrEmpty(_replayMessage)) GUI.Label(col.Next(40f), _replayMessage, Ui.Wrap);
                if (col.Button("最初から再生", height: 32f)) StartReplay(RunLogIO.FromJson(_replayInput));
                HelpButton(col);
                if (col.Button("ログを選び直す", height: 30f)) { _driver.Simulation = null; _replayer = null; _replayResult = null; ShowRaid(null); }
                if (col.Button("タイトルへ", height: 28f)) GoTitle();
                col.Space(4);
                EventLog(col, 8);
            });

            if (raid != null) DrawRaidMap(raid, useFog: true);
        }

        private void DrawReplaySelect()
        {
            float w = Mathf.Min(820f, _vw - 24f);
            var win = new Rect((_vw - w) / 2f, 24, w, _vh - 40);
            Ui.Window(win, "リプレイ");
            var col = new Column(win.x + 24, win.y + 26, w - 48);
            Ui.Text(col.Next(24f), "入力ログ(JSON)を貼り付けるか、保存済みのログを使います", Ui.Label, Ui.Gold);
            _replayInput = GUI.TextArea(col.Next(Mathf.Min(320f, _vh - 260f)), _replayInput, Ui.TextArea);
            int b = col.Buttons(new[] { "再生する", "検証だけ", "保存済みを読む", "タイトルへ" }, 40f);
            if (b == 0 || b == 1)
            {
                try
                {
                    var log = RunLogIO.FromJson(_replayInput);
                    if (b == 0) StartReplay(log);
                    else
                    {
                        var r = RunReplayer.Verify(log, _data);
                        _replayMessage = r.IsValid ? $"検証OK:記録どおり ★{r.Stars}" : "検証NG:" + r.Error;
                    }
                }
                catch (System.Exception e)
                {
                    _replayMessage = "ログを読めません:" + e.Message;
                }
            }
            else if (b == 2) _replayInput = SaveStore.Load(SaveStore.LastRunLogKey) ?? "";
            else if (b == 3) GoTitle();
            var style = Ui.Wrap;
            GUI.Label(col.Next(60f), _replayMessage, style);
        }
    }
}
