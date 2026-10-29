using DungeonGame.Client.Art;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Raid;
using DungeonGame.Core.Run;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// 遊び方の説明。
    ///  ・大きな「遊び方」ウィンドウ(絵付き・ページ送り)。各モードに初めて入ったときに自動で開き、「? 遊び方」でいつでも開ける
    ///  ・マップの左上に、今の状況に合わせた「次にやること」を表示する
    /// </summary>
    public partial class GameApp
    {
        private sealed class HelpPage
        {
            public string Title;
            public string Body;
            public string[] Pictures;
            public HelpPage(string title, string body, params string[] pictures) { Title = title; Body = body; Pictures = pictures; }
        }

        private static readonly HelpPage[] TitleHelp =
        {
            new HelpPage("このゲームについて",
                "「ゆうしゃとまおう」は、魔王と勇者が時間差で攻めて守るダンジョンゲームです。\n\n" +
                "■ 魔王モード … ダンジョンを掘ってモンスターを育て、攻めてくる勇者を迎え撃つ準備をします。\n" +
                "■ 勇者モード … 3人のパーティで、別々の魔王が作った3つの層を順に攻略し、最下層の魔王を倒します。\n" +
                "■ リプレイ … 勇者モードの記録を再生し、結果が正しいかを検証します。\n\n" +
                "まずは「勇者モード」で遊び、次に「魔王モード」で自分のダンジョンを作ってみましょう。",
                "demon_lord", "warrior", "mage", "priest", "thief"),
        };

        private static readonly HelpPage[] DemonHelp =
        {
            new HelpPage("魔王モードの目的",
                "あなたは新米の魔王です。勇者が攻めてくるダンジョンを作りましょう。\n\n" +
                "入口(光る扉)からゴール(階段/魔王の玉座)まで道をつなぎ、その途中にモンスターを住ませて、勇者を返り討ちにするのが目的です。\n\n" +
                "流れ: 掘る → 育てる → テスト防衛で試す → 保存",
                "demon_lord", "icon_pickaxe", "moss_slime", "icon_sword"),
            new HelpPage("① 掘る",
                "マップの土を左クリック(ドラッグでも可)すると掘れます。\n" +
                "掘れるのは「入口か、掘った通路の隣」の土だけ。掘れる量は「掘削」ゲージの分だけです。\n\n" +
                "土の模様に注目!\n" +
                "・緑の苔 = 養分   ・青い結晶 = 魔分\n" +
                "濃い土を掘ると、その場所にモンスターが生まれます。",
                "icon_pickaxe", "icon_leaf", "icon_drop"),
            new HelpPage("② モンスターの生まれ方:土",
                "モンスターの多くは、掘った土の養分・魔分の濃さで生まれる種族が決まります。\n" +
                "・うすい土 … コケスライム・マナダニなどの下位\n" +
                "・養分も魔分も濃い土 … ホラアナトカゲやアビスワームなどの強い種族\n\n" +
                "深い層ほど土が豊かなので、強いモンスターが生まれやすくなります。\n" +
                "モンスターが住みつくと、まわりの土の見た目がその種族らしく変わります(スライムのぬめり、結晶、焼け焦げ…)。\n" +
                "どの土で何が生まれるかは「図鑑」で確かめられます。",
                "moss_slime", "mana_mite", "cave_lizard", "abyss_wyrm"),
            new HelpPage("③ 特殊な生まれ方",
                "土からは生まれない、特別なモンスターもいます。育成中に条件がそろうと現れます。\n\n" +
                "・巣 … 3×3以上の広い部屋を掘っておくと、カゲコウモリが巣を作る\n" +
                "・墓場 … モンスターが何度も死んで骨が積もった通路から、ドクロ魔導士が起き上がる\n" +
                "・進化 … 長生きして育ち切ったコケスライムはヒカリダケに、アビスワームは冥竜に進化する\n\n" +
                "体の分は、まわりの土の養分・魔分から使われます。",
                "shadow_bat", "skull_mage", "glow_shroom", "abyss_dragon"),
            new HelpPage("④ 育てる",
                "「育てる」ボタンで時間を進めると、モンスターが土を吸って増えたり、食べたり食べられたりします。\n" +
                "食物連鎖:下位(土を吸う)→ 中位(下位を食べる)→ 上位(中位を食べる)→ 最上位(上位も食べる)\n\n" +
                "よく育った(体の大きい)モンスターほど、勇者との戦いで強くなります。\n" +
                "育てられる時間には上限があります(「育成」ゲージ)。",
                "icon_leaf", "moss_slime", "cave_lizard"),
            new HelpPage("⑤ テスト防衛と保存",
                "「テスト防衛」で、NPCの勇者(戦士・魔法使い・僧侶)を送り込んで様子を見られます。\n" +
                "「10回まとめて試す」で、どのくらい突破されるかがわかります。\n\n" +
                "完成したら「保存」。ただし次のダンジョンは保存できません:\n" +
                "・掘削予算をオーバーしている\n" +
                "・入口からゴールまでの道がない(完全封鎖は禁止)",
                "warrior", "mage", "priest", "icon_shield"),
            new HelpPage("操作のまとめ",
                "・左クリック / ドラッグ … 掘る\n" +
                "・マウスホイール、右上の+/- … 拡大・縮小\n" +
                "・右ドラッグ … 表示位置を動かす\n" +
                "・「全体」 … 全体表示に戻す\n" +
                "・マップにマウスを乗せる … 土の養分・魔分やモンスターの様子が左下に出る",
                "icon_pickaxe", "icon_hourglass"),
        };

        private static readonly HelpPage[] HeroHelp =
        {
            new HelpPage("勇者モードの目的",
                "3人のパーティで、深さの違う5つの層を順に攻略します。\n" +
                "途中の層は階段にたどり着けば突破、最下層(5層)は奥の魔王を倒せばクリアです!\n\n" +
                "評価: ★の数 = 突破した層の数(★5で魔王撃破)。深い層ほど強いモンスターが待っています。",
                "warrior", "mage", "priest", "icon_star", "demon_lord"),
            new HelpPage("勇者は自動で動く",
                "勇者は霧に包まれたダンジョンを自分で探索し、敵を見つけると自動で戦います。\n" +
                "あなたの仕事は、ここぞという時の「介入」と、層の合間の「育成」です。\n\n" +
                "・戦士 … 打たれ強い前衛   ・魔法使い … 射程2の魔法\n・僧侶 … 仲間を自動で回復   ・盗賊 … 素早く視界が広い\n" +
                "・狩人 … 射程3の弓   ・聖騎士 … とても硬く回復もできる\n・武闘家 … 最速の連打と高い会心率",
                "warrior", "mage", "priest", "thief", "hunter", "paladin", "monk"),
            new HelpPage("介入スキル",
                "・回復(ハート) … パーティ全員のHPを回復します。\n" +
                "・たいまつ … ボタンを押してからマップをクリックすると、その周りの霧が晴れます。ゴールを先に照らすと、勇者が迷わず進みます。\n\n" +
                "使える回数は全部の層を通して共通です。使いどころが勝負!",
                "icon_heart", "icon_torch"),
            new HelpPage("報酬と成長",
                "層を突破すると報酬画面になります。\n" +
                "・3つの候補からスキルを1つ獲得(パーティ全員に効く)\n" +
                "・スキルポイントを勇者ごとに「+」で割り振る\n\n" +
                "HPは次の層に引き継がれます。倒れた仲間は次の層に参加できません(スキル「不死鳥の羽」で復活できます)。",
                "icon_star", "icon_heart", "icon_sword", "icon_shield", "icon_boot", "icon_orb"),
            new HelpPage("操作のまとめ",
                "・速度ボタン(|| x1〜x8) … 一時停止・早送り\n" +
                "・マウスホイール、右上の+/- … 拡大・縮小(勇者を自動で追いかけます)\n" +
                "・右ドラッグ … 表示位置を動かす(「追従」で追いかけ再開)\n" +
                "・結果画面の「ログを保存」 … リプレイで見返せます",
                "icon_hourglass", "icon_torch"),
        };

        private static readonly HelpPage[] ReplayHelp =
        {
            new HelpPage("リプレイと検証",
                "勇者モードで保存した入力ログ(JSON)を再生します。\n\n" +
                "入力ログには「いつ・何をしたか」(介入スキル、スキル選択、ポイント配分)だけが記録されています。" +
                "同じ入力からはいつも同じ結果になるので、再計算した結果と、記録された★が一致するかを確かめられます。\n" +
                "(将来オンライン化したとき、サーバーで不正を見抜くのと同じ仕組みです)",
                "icon_hourglass", "icon_star"),
        };

        private bool _helpOpen;
        private HelpPage[] _helpPages;
        private int _helpPage;

        private void OpenHelp(HelpPage[] pages)
        {
            _helpPages = pages;
            _helpPage = 0;
            _helpOpen = true;
        }

        /// <summary>そのモードに初めて入ったときだけ、遊び方を自動で開く。</summary>
        private void OpenHelpFirstTime(string key, HelpPage[] pages)
        {
            string pref = "help_seen_" + key;
            if (PlayerPrefs.GetInt(pref, 0) == 1) return;
            PlayerPrefs.SetInt(pref, 1);
            PlayerPrefs.Save();
            OpenHelp(pages);
        }

        /// <summary>今の画面に合った遊び方。</summary>
        private HelpPage[] HelpForScreen()
        {
            switch (_screen)
            {
                case Screen.Demon: return DemonHelp;
                case Screen.HeroSetup:
                case Screen.Run: return HeroHelp;
                case Screen.Replay: return ReplayHelp;
                default: return TitleHelp;
            }
        }

        /// <summary>パネル用の「? 遊び方」ボタン。</summary>
        private void HelpButton(Column col)
        {
            if (_screen != Screen.Demon)
            {
                if (col.Button("? 遊び方", height: 30f, color: new Color(0.15f, 0.4f, 0.45f))) OpenHelp(HelpForScreen());
                return;
            }
            // 魔王の部屋では「遊び方」と「図鑑」を並べる
            var row = col.Next(30f, 6f);
            float half = (row.width - 6f) / 2f;
            if (Ui.Btn(new Rect(row.x, row.y, half, row.height), "? 遊び方", true, null, new Color(0.15f, 0.4f, 0.45f), 14)) OpenHelp(HelpForScreen());
            if (BookButton(new Rect(row.x + half + 6f, row.y, half, row.height))) OpenBook(0);
        }

        /// <summary>遊び方ウィンドウ(画面の上に大きく出す)。</summary>
        private void DrawHelp()
        {
            if (!_helpOpen || _helpPages == null) return;
            // 後ろを暗くする
            Ui.Fill(new Rect(0, 0, _vw, _vh), new Color(0, 0, 0, 0.6f));

            var page = _helpPages[_helpPage];
            float w = Mathf.Min(860f, _vw - 30f), h = Mathf.Min(600f, _vh - 40f);
            var win = new Rect((_vw - w) / 2f, (_vh - h) / 2f + 8, w, h);
            Ui.Window(win, $"遊び方  {_helpPage + 1}/{_helpPages.Length}");

            var inner = new Rect(win.x + 28, win.y + 28, w - 56, h - 56);
            var title = Ui.Title;
            Ui.Text(new Rect(inner.x, inner.y, inner.width, 40), page.Title, title, Ui.Gold);
            Ui.Fill(new Rect(inner.x, inner.y + 42, inner.width, 2), new Color(1f, 0.84f, 0.3f, 0.5f));

            // 絵
            float pic = Mathf.Min(72f, (inner.width - 20f) / Mathf.Max(1, page.Pictures.Length) - 12f);
            float total = page.Pictures.Length * (pic + 12f) - 12f;
            float px = inner.center.x - total / 2f;
            for (int i = 0; i < page.Pictures.Length; i++)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f + i * 0.8f)) * 5f;
                var pr = new Rect(px + i * (pic + 12f), inner.y + 56, pic, pic);
                Ui.Fill(new Rect(pr.x - 4, pr.y - 4, pr.width + 8, pr.height + 8), new Color(1, 1, 1, 0.06f));
                Ui.Sprite(new Rect(pr.x, pr.y - bob, pr.width, pr.height), Sprites.Anim(page.Pictures[i], i * 5));
            }

            // 本文
            var bodyRect = new Rect(inner.x, inner.y + 56 + pic + 20, inner.width, inner.height - (56 + pic + 20) - 56);
            Ui.Fill(new Rect(bodyRect.x - 8, bodyRect.y - 8, bodyRect.width + 16, bodyRect.height + 16), new Color(0, 0, 0, 0.3f));
            var style = Ui.Body;
            var old = style.normal.textColor;
            style.normal.textColor = Ui.Cream;
            GUI.Label(bodyRect, page.Body, style);
            style.normal.textColor = old;

            // ページ送り
            var nav = new Rect(inner.x, inner.yMax - 44, inner.width, 44);
            float bw = Mathf.Min(170f, (nav.width - 20f) / 3f);
            if (Ui.Btn(new Rect(nav.x, nav.y, bw, nav.height), "◀ 前へ", _helpPage > 0)) _helpPage--;
            bool last = _helpPage >= _helpPages.Length - 1;
            if (Ui.Btn(new Rect(nav.xMax - bw, nav.y, bw, nav.height), last ? "はじめる!" : "次へ ▶", true, null,
                    last ? new Color(0.6f, 0.18f, 0.2f) : (Color?)null))
            {
                if (last) _helpOpen = false; else _helpPage++;
            }
            if (Ui.Btn(new Rect(nav.center.x - bw / 2f, nav.y, bw, nav.height), "閉じる")) _helpOpen = false;
            // ページの点
            for (int i = 0; i < _helpPages.Length; i++)
                Ui.Fill(new Rect(nav.center.x - _helpPages.Length * 7 + i * 14, nav.y - 14, 8, 8), i == _helpPage ? Ui.Gold : new Color(1, 1, 1, 0.3f));
        }

        // ================= 次にやること =================

        /// <summary>今の状況に合わせた「次にやること」。マップの左上に表示する。</summary>
        private string CurrentHint()
        {
            switch (_screen)
            {
                case Screen.Demon: return DemonHint();
                case Screen.Run: return RunHint();
                case Screen.Replay:
                    return _replayResult != null ? "再生が終わりました。左のパネルに検証結果が出ています。"
                        : "記録どおりに勇者が動きます。速度ボタンで早送りできます。";
                default: return null;
            }
        }

        private string DemonHint()
        {
            if (_testRaid != null)
                return _testRaid.IsFinished ? "結果を見て、守りを見直そう。「編集に戻る」で掘る・育てるに戻れます。"
                    : "NPC勇者が攻めてきた!手下がどう戦うか見てみよう(速度ボタンで早送り)。";
            var b = _builder;
            if (DemonBusy) return "育成中…モンスターが土を吸い、食べ、増えていきます。";
            if (b.Map.DugCount == 0) return "まずは入口(光る扉)のすぐ下の土をクリックして掘ろう!";
            bool connected = PathFinder.HasPath(b.Map, b.Map.Entrance, b.Map.Goal);
            if (!connected && b.Map.RemainingBudget > 0)
                return b.Rule.Goal == GoalType.Stairs
                    ? "ゴール(下の階段)まで道をつなげよう。寄り道して部屋を作ると手下が住みやすい。"
                    : "ゴール(下の魔王の玉座)まで道をつなげよう。寄り道して部屋を作ると手下が住みやすい。";
            if (!connected) return "掘削予算を使い切ったのに道がつながっていない…上の層ボタンを押すと、その層を最初から掘り直せます。";
            if (b.State.Monsters.Count == 0) return "手下がいない…緑の苔・青い結晶が濃い土を掘るとモンスターが生まれます。";
            if (b.State.EcologyTick == 0) return "「育てる」でモンスターを育てよう。増えて強くなります。";
            if (b.RemainingEcologyTicks > 0) return "まだ育てられます。「テスト防衛」で勇者を送り込んで、守りを試すのもおすすめ。";
            return "完成!「テスト防衛」で試し、よければ「保存」しよう。";
        }

        private string RunHint()
        {
            if (_run == null || _run.Phase != RunPhase.Raid) return null;
            var raid = _run.CurrentRaid;
            if (_torchMode) return "照らしたい場所をクリック!ゴールのありそうな方向(下のほう)がおすすめ。";
            if (raid.IsFinished) return raid.Outcome == RaidOutcome.Cleared ? "突破!左のパネルの「次へ」で報酬を受け取ろう。" : "冒険はここまで…左の「次へ」で結果を見よう。";
            foreach (var h in raid.Heroes)
                if (h.Active && h.Hp * 100 < h.Stats.MaxHp * 35 && _run.Charges.Heal > 0)
                    return $"{h.Class.Name}のHPが危ない!「回復」を使おう。";
            if (!raid.Fog.IsKnown(raid.Dungeon.Map.Goal) && raid.CurrentTick > 300 && _run.Charges.Torch > 0)
                return "ゴールがまだ見つからない…「たいまつ」で先を照らすと勇者が迷わず進みます。";
            if (raid.CurrentTick < 50) return "勇者は自動で探索・戦闘します。HPが減ったら回復、迷っていたらたいまつで助けよう。";
            return raid.Boss != null ? "奥に魔王が待ち構えている…HPを温存して挑もう!" : "勇者たちが下の階段を目指して進んでいる…";
        }
    }
}
