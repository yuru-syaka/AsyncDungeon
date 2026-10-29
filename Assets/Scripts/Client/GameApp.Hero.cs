using System.Collections.Generic;
using System.Linq;
using DungeonGame.Client.Art;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Run;
using DungeonGame.Core.Skills;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>勇者モード:編成 → 全層の攻略(介入スキル) → 報酬(スキル3択・ポイント配分) → 結果。</summary>
    public partial class GameApp
    {
        private readonly List<string> _partyPick = new List<string>();
        private bool _partyPickInitialized;
        private string _seedText = "";
        private RunState _run;
        private bool _torchMode;
        private string _runMessage = "";
        private string _runLogJson;
        private float _resultStart;

        private static readonly StatKind[] AllStats = { StatKind.MaxHp, StatKind.Attack, StatKind.Defense, StatKind.Speed, StatKind.Magic };
        private static readonly string[] StatNames = { "HP", "攻撃", "防御", "素早さ", "魔力" };

        // ================= 編成 =================

        private void EnterHeroSetup()
        {
            _screen = Screen.HeroSetup;
            OpenHelpFirstTime("hero", HeroHelp);
        }

        private void DrawHeroSetup()
        {
            if (string.IsNullOrEmpty(_seedText)) _seedText = Random.Range(1, 1000000).ToString();
            if (!_partyPickInitialized)
            {
                _partyPickInitialized = true;
                if (_partyPick.Count == 0) _partyPick.AddRange(_data.Heroes.TestParty);
            }
            int size = _data.Heroes.PartySize;

            float w = Mathf.Min(820f, _vw - 24f);
            var win = new Rect((_vw - w) / 2f, 24, w, _vh - 40);
            Ui.Window(win, "勇者パーティ編成");
            if (Ui.Btn(new Rect(win.xMax - 130, win.y + 14, 110, 30), "? 遊び方", true, null, new Color(0.15f, 0.4f, 0.45f), 14)) OpenHelp(HeroHelp);
            if (BookButton(new Rect(win.xMax - 250, win.y + 14, 110, 30))) OpenBook(1);
            var col = new Column(win.x + 24, win.y + 24, w - 48);
            Ui.Text(col.Next(24f, 2f), $"{_data.Heroes.Classes.Count}職業から{size}人を選べ!(選んだ順が隊列。先頭が先に進む)", Ui.Label, Ui.Gold);
            col.Space(4);

            // 職業カード(横長は2列、縦長は1列)
            int perRow = _portrait ? 2 : 3;
            float cardW = (col.Width - 12f * (perRow - 1)) / perRow;
            float cardH = 112f;
            var classes = _data.Heroes.Classes;
            int rows = (classes.Count + perRow - 1) / perRow;
            var area = col.Next(rows * (cardH + 10f));
            for (int i = 0; i < classes.Count; i++)
            {
                var cls = classes[i];
                var r = new Rect(area.x + (i % perRow) * (cardW + 12f), area.y + (i / perRow) * (cardH + 10f), cardW, cardH);
                ClassCard(r, cls, size);
            }

            // 隊列のプレビュー
            var order = col.Next(56f);
            Ui.Fill(order, new Color(0, 0, 0, 0.3f));
            Ui.Text(new Rect(order.x + 10, order.y + 16, 80, 24), "隊列", Ui.Label, Ui.Cream);
            for (int i = 0; i < size; i++)
            {
                var slot = new Rect(order.x + 80 + i * 70, order.y + 4, 48, 48);
                Ui.Frame(slot, new Color(1, 1, 1, 0.3f), 2f);
                if (i < _partyPick.Count)
                {
                    float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f + i)) * 3f;
                    Ui.Sprite(new Rect(slot.x + 2, slot.y + 2 - bob, 44, 44), Sprites.Anim(_partyPick[i], i * 3));
                }
                Ui.Text(new Rect(slot.xMax + 2, slot.y + 14, 20, 20), i < size - 1 ? "▶" : "", Ui.Label, Ui.Gold);
            }

            // シード
            var seedRow = col.Next(32f, 6f);
            Ui.Text(new Rect(seedRow.x, seedRow.y + 6, 200, 22), "シード(同じなら同じ冒険)", Ui.Small, Ui.Cream);
            _seedText = GUI.TextField(new Rect(seedRow.x + 190, seedRow.y + 2, 150, 28), _seedText, 18);
            if (Ui.Btn(new Rect(seedRow.x + 350, seedRow.y, 110, 32), "ランダム")) _seedText = Random.Range(1, 1000000).ToString();

            var iv = _data.Heroes.Interventions;
            var ivRow = col.Next(22f, 6f);
            Ui.IconText(new Rect(ivRow.x, ivRow.y, 260, 22), Sprites.Get("icon_heart"), $"回復 {iv.HealCharges}回(全員HP{iv.HealPercent}%)", Ui.Small);
            Ui.IconText(new Rect(ivRow.x + 260, ivRow.y, 300, 22), Sprites.Get("icon_torch"), $"たいまつ {iv.TorchCharges}回(半径{iv.TorchRadius}の霧を晴らす)", Ui.Small);

            bool seedOk = TryParseSeed(_seedText, out ulong seed);
            bool ready = _partyPick.Count == size && seedOk;
            var row = col.Next(46f);
            float half = (row.width - 10f) / 2f;
            if (Ui.Btn(new Rect(row.x, row.y, half, row.height), "出発!", ready, Sprites.Get("icon_sword"), new Color(0.6f, 0.18f, 0.2f), 22)) StartRun(seed);
            if (Ui.Btn(new Rect(row.x + half + 10f, row.y, half, row.height), "タイトルへ")) GoTitle();
            if (_partyPick.Count < size) Ui.Text(col.Next(22f), $"あと{size - _partyPick.Count}人選んでください(カードをクリック)", Ui.Label, Ui.Red);
            else if (!seedOk) Ui.Text(col.Next(22f), "シードには半角の数字を入れてください", Ui.Label, Ui.Red);
        }

        private void ClassCard(Rect r, HeroClass cls, int size)
        {
            int order = _partyPick.IndexOf(cls.Id);
            bool selectable = order >= 0 || _partyPick.Count < size;
            bool hover = selectable && r.Contains(Event.current.mousePosition);
            Ui.Fill(r, order >= 0 ? new Color(0.22f, 0.26f, 0.5f) : hover ? new Color(0.18f, 0.18f, 0.3f) : new Color(0.1f, 0.1f, 0.18f));
            Ui.Frame(r, order >= 0 ? Ui.Gold : new Color(1, 1, 1, 0.25f), order >= 0 ? 3f : 1f);
            float bob = order >= 0 ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)) * 4f : 0f;
            float pic = r.height - 16f;
            Ui.Portrait(new Rect(r.x + 8, r.y + 8, pic, pic), cls.Id, Ui.PortraitColor(cls.Id), cls.Id.Length, !selectable, order >= 0);
            float sx = r.x + pic + 18f;
            Ui.Text(new Rect(sx, r.y + 4, r.width - pic - 80, 28), cls.Name, Ui.Heading, Ui.Cream);
            if (order >= 0) Ui.Text(new Rect(r.xMax - 64, r.y + 8, 58, 24), $"{order + 1}番目", Ui.Label, Ui.Gold);

            // 能力値(2段)
            var s = cls.BaseStats;
            int[] vals = { s.MaxHp, s.Attack, s.Defense, s.Speed, s.Magic };
            float cw = Mathf.Min(64f, (r.xMax - sx - 6f) / 3f);
            for (int k = 0; k < 5; k++)
            {
                float x = sx + (k % 3) * cw, y = r.y + 34 + (k / 3) * 24;
                Ui.IconText(new Rect(x, y, cw, 22), StatIcon(AllStats[k]), vals[k].ToString(), Ui.Small);
            }
            Ui.Text(new Rect(sx, r.y + 84, r.xMax - sx - 6, 22), ClassFeature(cls), Ui.Small, Ui.Sub, false);
            if (selectable && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                if (order >= 0) { _partyPick.Remove(cls.Id); Sound?.Play("click"); } else { _partyPick.Add(cls.Id); Sound?.Play("select"); }
            }
        }

        /// <summary>職業の特徴を短く(射程・回復・会心・視界)。</summary>
        private static string ClassFeature(HeroClass cls)
        {
            var parts = new List<string>();
            if (cls.AttackStat == AttackStat.Magic) parts.Add($"魔法(射程{cls.AttackRange})");
            else if (cls.AttackRange >= 2) parts.Add($"弓(射程{cls.AttackRange})");
            if (cls.Ability == ClassAbility.Heal) parts.Add("仲間を回復");
            if (cls.CritPercent >= 20) parts.Add($"会心{cls.CritPercent}%");
            if (cls.Vision >= 3) parts.Add($"視界{cls.Vision}");
            if (cls.BaseStats.Defense >= 5) parts.Add("打たれ強い");
            if (cls.BaseStats.Speed >= 12) parts.Add("素早い");
            return parts.Count == 0 ? "前衛" : string.Join("・", parts);
        }

        /// <summary>シード文字列を数値にする。全角数字・前後の空白も受け付ける。</summary>
        private static bool TryParseSeed(string text, out ulong seed)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (text ?? "").Trim())
                sb.Append(c >= '０' && c <= '９' ? (char)('0' + (c - '０')) : c);
            return ulong.TryParse(sb.ToString(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out seed);
        }

        private void StartRun(ulong seed)
        {
            _run = RunState.Start(_data, seed, _partyPick);
            _runLogJson = null;
            _runMessage = "";
            _torchMode = false;
            _screen = Screen.Run;
            BeginRaidView();
        }

        private void BeginRaidView()
        {
            ShowRaid(_run.CurrentRaid);
            _driver.Simulation = _run.CurrentRaid;
            _driver.Paused = false;
            _driver.SpeedPercent = 100;
        }

        // ================= ラン =================

        private void DrawRun()
        {
            switch (_run.Phase)
            {
                case RunPhase.Raid: DrawRunRaid(); break;
                case RunPhase.Reward: DrawReward(); break;
                default: DrawRunResult(); break;
            }
        }

        private void DrawRunRaid()
        {
            var raid = _run.CurrentRaid;
            Panel("冒険中", col =>
            {
                var starRow = col.Next(30f, 4f);
                Ui.Stars(new Rect(starRow.x, starRow.y + 4, 0, 22), _run.Stars, _run.LayerCount);
                if (Ui.Btn(new Rect(starRow.xMax - 110, starRow.y, 110, 30), "? 遊び方", true, null, new Color(0.15f, 0.4f, 0.45f), 14)) OpenHelp(HeroHelp);
                RaidStatus(col, raid);
                col.Space(4);

                // 介入スキル
                var row = col.Next(44f, 6f);
                float half = (row.width - 8f) / 2f;
                if (Ui.Btn(new Rect(row.x, row.y, half, row.height), $"回復 ×{_run.Charges.Heal}", _run.Charges.Heal > 0 && !raid.IsFinished,
                        Sprites.Get("icon_heart"), new Color(0.2f, 0.45f, 0.25f)))
                    _runMessage = raid.Submit(InterventionType.Heal) ? "回復の祈りを捧げた!" : "今は使えない。";
                if (Ui.Btn(new Rect(row.x + half + 8f, row.y, half, row.height), _torchMode ? "やめる" : $"たいまつ ×{_run.Charges.Torch}",
                        _torchMode || (_run.Charges.Torch > 0 && !raid.IsFinished), Sprites.Get("icon_torch"), new Color(0.55f, 0.35f, 0.1f)))
                    _torchMode = !_torchMode;
                if (_torchMode) Ui.Text(col.Next(20f), "照らしたい場所をマップでクリック!", Ui.Label, Ui.Gold);
                SpeedButtons(col);
                if (!string.IsNullOrEmpty(_runMessage)) Ui.Text(col.Next(20f), _runMessage, Ui.Small, Ui.Cream);

                if (raid.IsFinished)
                {
                    col.Space(4);
                    bool win = raid.Outcome == RaidOutcome.Cleared;
                    Ui.Text(col.Next(36f), win ? "突破した!" : raid.Outcome == RaidOutcome.Wiped ? "全滅した…" : "時間切れ…", Ui.Title, win ? Ui.Gold : Ui.Red);
                    if (col.Button("次へ ▶", height: 44f, color: new Color(0.6f, 0.45f, 0.1f)))
                    {
                        _run.CompleteRaid();
                        _driver.Simulation = null;
                        _runMessage = "";
                        _resultStart = Time.unscaledTime;
                    }
                }
                col.Space(4);
                EventLog(col, 8);
                if (col.Button("あきらめてタイトルへ", height: 26f)) GoTitle();
            });

            DrawRaidMap(raid, useFog: true, inside: () =>
            {
                // たいまつの対象選び(マップ領域の中の座標で処理する)
                if (!_torchMode) return;
                var hover = MapLocal.Contains(Event.current.mousePosition) ? _map.GridAt(Event.current.mousePosition) : null;
                if (hover != null) _map.HighlightArea(hover.Value, _data.Heroes.Interventions.TorchRadius, new Color(1f, 0.7f, 0.2f));
                var click = MapClick();
                if (click.Clicked && !click.IsDrag)
                {
                    _runMessage = raid.Submit(InterventionType.Torch, click.Pos.X, click.Pos.Y) ? "たいまつを投げ込んだ!" : "今は使えない。";
                    _torchMode = false;
                }
            });
        }

        // ================= 報酬 =================

        private void DrawReward()
        {
            float w = Mathf.Min(1100f, _vw - 24f);
            var win = new Rect((_vw - w) / 2f, 24, w, _vh - 40);
            Ui.Window(win, $"第{_run.Layer}層 突破!");
            if (Ui.Btn(new Rect(win.xMax - 130, win.y + 14, 110, 30), "? 遊び方", true, null, new Color(0.15f, 0.4f, 0.45f), 14))
            {
                OpenHelp(HeroHelp);
                _helpPage = 3; // 「報酬と成長」のページから
            }
            var col = new Column(win.x + 24, win.y + 24, w - 48);
            var head = col.Next(30f, 4f);
            Ui.Stars(new Rect(head.x, head.y, 0, 28), _run.Stars, _run.LayerCount);
            Ui.Text(new Rect(head.x + 110, head.y + 2, head.width - 110, 26), "報酬:スキルを1つ選び、ポイントを割り振れ!", Ui.Label, Ui.Gold);

            // スキル3択(カード)
            int n = _run.SkillCandidates.Count;
            bool stack = _portrait;
            float cardH = 118f;
            var cards = col.Next(stack ? n * (cardH + 8f) : cardH, 8f);
            float cw = stack ? cards.width : (cards.width - 12f * (n - 1)) / Mathf.Max(1, n);
            string chosen = _run.Log.Layers[_run.Log.Layers.Count - 1].SkillChoice;
            for (int i = 0; i < n; i++)
            {
                var def = _data.Skills.Get(_run.SkillCandidates[i]);
                var r = stack ? new Rect(cards.x, cards.y + i * (cardH + 8f), cw, cardH) : new Rect(cards.x + i * (cw + 12f), cards.y, cw, cardH);
                SkillCard(r, def, chosen == def.Id, !_run.SkillChosenThisReward);
            }
            Ui.Text(col.Next(18f, 6f), _run.Skills.Count > 0 ? "獲得済み:" + string.Join("、", _run.Skills.Select(id => _data.Skills.Get(id).Name)) : "", Ui.Small, Ui.Sub, false);

            // ポイント配分
            var pv = _data.Skills.PointValues;
            var ptRow = col.Next(26f, 4f);
            Ui.Text(ptRow, $"スキルポイント 残り {_run.UnspentPoints}", Ui.Label, _run.UnspentPoints > 0 ? Ui.Gold : Ui.Cream);
            Ui.Text(new Rect(ptRow.x + 240, ptRow.y + 4, ptRow.width - 240, 20),
                $"1ポイントで HP+{pv.MaxHp} 攻撃+{pv.Attack} 防御+{pv.Defense} 素早さ+{pv.Speed} 魔力+{pv.Magic}", Ui.Small, Ui.Sub, false);
            for (int i = 0; i < _run.Party.Count; i++)
            {
                var hero = _run.Party[i];
                var s = _run.EffectiveStats(i);
                var r = col.Next(58f, 4f);
                Ui.Fill(r, new Color(0, 0, 0, 0.3f));
                Ui.Portrait(new Rect(r.x + 3, r.y + 3, 52, 52), hero.ClassId, Ui.PortraitColor(hero.ClassId), i * 3, hero.Hp <= 0);
                Ui.Text(new Rect(r.x + 60, r.y + 4, 120, 22), _data.Heroes.Get(hero.ClassId).Name, Ui.Label, Ui.Cream);
                Ui.Bar(new Rect(r.x + 60, r.y + 32, 100, 10), hero.Hp, s.MaxHp);
                Ui.Text(new Rect(r.x + 60, r.y + 40, 120, 18), hero.Hp > 0 ? $"{hero.Hp}/{s.MaxHp}" : "戦闘不能", Ui.Small, Ui.Cream);
                float sx = r.x + 170;
                float sw = (r.width - 176) / AllStats.Length;
                for (int k = 0; k < AllStats.Length; k++)
                {
                    var cell = new Rect(sx + k * sw, r.y + 6, sw - 6, 46);
                    Ui.Sprite(new Rect(cell.x, cell.y + 2, 18, 18), StatIcon(AllStats[k]));
                    Ui.Text(new Rect(cell.x + 20, cell.y, cell.width - 60, 20), StatNames[k], Ui.Small, Ui.Sub, false);
                    Ui.Text(new Rect(cell.x + 4, cell.y + 20, cell.width - 44, 26), SkillMath.Get(s, AllStats[k]).ToString(), Ui.Label, Ui.Cream);
                    if (Ui.Btn(new Rect(cell.xMax - 40, cell.y + 8, 38, 32), "+", _run.UnspentPoints > 0, null, new Color(0.25f, 0.45f, 0.25f)))
                        _run.Allocate(i, AllStats[k], 1);
                }
            }
            col.Space(4);
            bool canGo = _run.SkillChosenThisReward || _run.SkillCandidates.Count == 0;
            string label = canGo ? $"第{_run.Layer + 1}層へ進む ▶" + (_run.UnspentPoints > 0 ? $"(残り{_run.UnspentPoints}は持ち越し)" : "") : "スキルを選んでください";
            if (col.Button(label, canGo, 46f, Sprites.Get("icon_sword"), new Color(0.6f, 0.18f, 0.2f)))
            {
                _run.ConfirmReward();
                BeginRaidView();
            }
        }

        private void SkillCard(Rect r, SkillDefinition def, bool chosen, bool selectable)
        {
            bool hover = selectable && r.Contains(Event.current.mousePosition);
            Ui.Fill(r, chosen ? new Color(0.42f, 0.3f, 0.08f) : hover ? new Color(0.2f, 0.2f, 0.36f) : new Color(0.1f, 0.1f, 0.2f));
            Ui.Frame(r, chosen ? Ui.Gold : hover ? Ui.Cream : new Color(1, 1, 1, 0.3f), chosen ? 3f : 2f);
            float bob = hover || chosen ? Mathf.Sin(Time.unscaledTime * 6f) * 2f : 0f;
            Ui.Sprite(new Rect(r.x + 10, r.y + 12 + bob, 48, 48), SkillIcon(def));
            Ui.Text(new Rect(r.x + 66, r.y + 10, r.width - 76, 30), def.Name, Ui.Title, chosen ? Ui.Gold : Ui.Cream);
            var style = Ui.Wrap;
            var old = style.normal.textColor;
            style.normal.textColor = Ui.Sub;
            GUI.Label(new Rect(r.x + 66, r.y + 46, r.width - 76, r.height - 52), def.Description, style);
            style.normal.textColor = old;
            if (chosen) Ui.Text(new Rect(r.xMax - 90, r.yMax - 28, 84, 24), "獲得!", Ui.Label, Ui.Gold);
            else if (selectable)
            {
                if (hover) Ui.Text(new Rect(r.xMax - 120, r.yMax - 28, 114, 24), "クリックで獲得", Ui.Small, Ui.Gold);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _run.ChooseSkill(def.Id); Sound?.Play("select"); }
            }
        }

        // ================= 結果 =================

        private void DrawRunResult()
        {
            bool win = _run.Phase == RunPhase.Victory;
            float w = Mathf.Min(720f, _vw - 24f);
            var win2 = new Rect((_vw - w) / 2f, 24, w, _vh - 40);
            Ui.Window(win2, win ? "冒険の記録 ― 勝利" : "冒険の記録");
            var col = new Column(win2.x + 28, win2.y + 26, w - 56);

            var huge = Ui.Huge;
            int old = huge.fontSize;
            huge.fontSize = 44;
            Ui.Text(col.Next(60f), win ? "魔王を討ち取った!" : "勇者たちは倒れた…", huge, win ? Ui.Gold : Ui.Red);
            huge.fontSize = old;

            // ★を1つずつ出す
            float t = Time.unscaledTime - _resultStart;
            var starRow = col.Next(56f, 8f);
            float s = 48f;
            float sx = starRow.center.x - (s * 3 + 16) / 2f;
            for (int i = 0; i < _run.LayerCount; i++)
            {
                bool lit = i < _run.Stars && t > 0.4f + i * 0.35f;
                float pop = lit ? Mathf.Clamp01((t - 0.4f - i * 0.35f) * 6f) : 1f;
                float size = s * (lit ? Mathf.Lerp(1.6f, 1f, pop) : 1f);
                Ui.Sprite(new Rect(sx + i * (s + 8) + (s - size) / 2f, starRow.y + (s - size) / 2f, size, size),
                    Sprites.Get(lit ? "icon_star" : "icon_star_empty"));
            }
            Ui.Text(col.Next(18f, 8f), $"★の数 = 突破した層の数(★{_run.LayerCount}で魔王撃破)", Ui.Small, Ui.Sub, false);

            // パーティ
            var partyRow = col.Next(60f, 6f);
            for (int i = 0; i < _run.Party.Count; i++)
            {
                var h = _run.Party[i];
                float bob = h.Hp > 0 && win ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f + i)) * 6f : 0f;
                var pr = new Rect(partyRow.center.x - 110 + i * 76, partyRow.y - bob, 56, 56);
                Ui.Sprite(pr, Sprites.Anim(h.ClassId, i * 3), h.Hp > 0 ? (Color?)null : new Color(0.3f, 0.3f, 0.3f));
                if (h.Hp <= 0) Ui.Sprite(new Rect(pr.x + 16, pr.y + 16, 24, 24), Sprites.Get("icon_skull"));
            }

            foreach (var l in _run.Log.Layers)
            {
                string skill = l.SkillChoice != null ? _data.Skills.Get(l.SkillChoice).Name : "-";
                var r = col.Next(22f, 2f);
                Ui.Text(r, $"第{l.Layer}層 {OutcomeName(l.Outcome)}({l.EndTick / 10f:0.0}秒)介入{l.Commands.Count}回 スキル:{skill}", Ui.Small,
                    l.Outcome == RaidOutcome.Cleared ? Ui.Cream : Ui.Red);
            }
            col.Space(6);
            int b = col.Buttons(new[] { "ログを保存", "リプレイ", "ログJSON" }, 36f);
            if (b == 0)
            {
                SaveStore.Save(SaveStore.LastRunLogKey, RunLogIO.ToJson(_run.Log));
                _runMessage = $"入力ログを保存した({SaveStore.FileLocation})";
            }
            else if (b == 1) StartReplay(_run.Log);
            else if (b == 2) _runLogJson = _runLogJson == null ? RunLogIO.ToJson(_run.Log) : null;
            if (_runLogJson != null) JsonBox(col, _runLogJson, 140f);
            if (!string.IsNullOrEmpty(_runMessage)) Ui.Text(col.Next(20f), _runMessage, Ui.Small, Ui.Cream);
            col.Space(4);
            int c = col.Buttons(new[] { "もう一度挑む", "タイトルへ" }, 40f);
            if (c == 0) { _seedText = Random.Range(1, 1000000).ToString(); _screen = Screen.HeroSetup; }
            if (c == 1) GoTitle();
        }

        private static string OutcomeName(RaidOutcome o)
            => o == RaidOutcome.Cleared ? "突破" : o == RaidOutcome.Wiped ? "全滅" : o == RaidOutcome.TimeUp ? "時間切れ" : "攻略中";
    }
}
