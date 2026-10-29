using System.Collections.Generic;
using System.Linq;
using DungeonGame.Client.Art;
using DungeonGame.Core.Dungeon;
using DungeonGame.Core.Ecology;
using DungeonGame.Core.Heroes;
using DungeonGame.Core.Skills;
using UnityEngine;

namespace DungeonGame.Client
{
    /// <summary>
    /// 図鑑:モンスター・勇者・スキルの一覧と詳細。ゲームデータ(JSON)の内容をそのまま見せる。
    /// タイトル・魔王の部屋・勇者の編成画面から開け、「もどる」で元の画面に戻る。
    /// </summary>
    public partial class GameApp
    {
        private int _bookTab;      // 0=モンスター 1=勇者 2=スキル
        private int _bookIndex;
        private Screen _bookReturn = Screen.Title;
        private Vector2 _bookListScroll, _bookDetailScroll;

        private static readonly string[] BookTabs = { "モンスター", "勇者", "スキル" };

        private void OpenBook(int tab = 0)
        {
            if (_screen != Screen.Book) _bookReturn = _screen;
            _screen = Screen.Book;
            _bookTab = tab;
            _bookIndex = 0;
            _bookListScroll = _bookDetailScroll = Vector2.zero;
        }

        /// <summary>パネル用の「図鑑」ボタン。</summary>
        private bool BookButton(Rect r) => Ui.Btn(r, "図鑑", true, Sprites.Get("icon_orb"), new Color(0.45f, 0.3f, 0.12f), 14);

        private void DrawBook()
        {
            float pad = 10f;
            Rect listRect, detailRect;
            if (_portrait)
            {
                listRect = new Rect(pad, 24, _vw - pad * 2, _vh * 0.3f);
                detailRect = new Rect(pad, listRect.yMax + 22, _vw - pad * 2, _vh - listRect.yMax - 32);
            }
            else
            {
                listRect = new Rect(pad, 24, 360, _vh - 34);
                detailRect = new Rect(listRect.xMax + 16, 24, _vw - listRect.xMax - 16 - pad, _vh - 34);
            }

            // ---- 一覧 ----
            Ui.Window(listRect, "図鑑");
            var head = new Rect(listRect.x + 16, listRect.y + 20, listRect.width - 32, 32);
            float tw = (head.width - 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var color = i == _bookTab ? new Color(0.62f, 0.42f, 0.1f) : (Color?)null;
                if (Ui.Btn(new Rect(head.x + i * (tw + 4f), head.y, tw, head.height), BookTabs[i], true, null, color, 14))
                {
                    _bookTab = i;
                    _bookIndex = 0;
                    _bookDetailScroll = Vector2.zero;
                }
            }
            var listInner = new Rect(listRect.x + 12, head.yMax + 10, listRect.width - 24, listRect.yMax - head.yMax - 60);
            DrawBookList(listInner);
            if (Ui.Btn(new Rect(listRect.x + 16, listRect.yMax - 46, listRect.width - 32, 34), "もどる")) _screen = _bookReturn;

            // ---- 詳細 ----
            switch (_bookTab)
            {
                case 0: DrawMonsterDetail(detailRect); break;
                case 1: DrawHeroDetail(detailRect); break;
                default: DrawSkillDetail(detailRect); break;
            }
        }

        // ================= 一覧 =================

        private List<MonsterSpecies> BookMonsters => _data.Monsters.Species.OrderBy(s => s.IsBoss ? 99 : s.Tier).ToList();

        private void DrawBookList(Rect area)
        {
            int count = _bookTab == 0 ? BookMonsters.Count : _bookTab == 1 ? _data.Heroes.Classes.Count : _data.Skills.Skills.Count;
            float rowH = 54f;
            int perRow = _portrait ? 2 : 1;
            float cw = (area.width - 16f - 6f * (perRow - 1)) / perRow;
            int rows = (count + perRow - 1) / perRow;
            var view = new Rect(0, 0, area.width - 16f, rows * (rowH + 6f));
            _bookListScroll = GUI.BeginScrollView(area, _bookListScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < count; i++)
            {
                var r = new Rect((i % perRow) * (cw + 6f), (i / perRow) * (rowH + 6f), cw, rowH);
                string id, name, sub;
                Texture2D icon = null;
                if (_bookTab == 0)
                {
                    var s = BookMonsters[i];
                    id = s.Id; name = s.Name; sub = TierLabel(s);
                }
                else if (_bookTab == 1)
                {
                    var c = _data.Heroes.Classes[i];
                    id = c.Id; name = c.Name; sub = c.Ability == ClassAbility.Heal ? "回復役" : c.AttackStat == AttackStat.Magic ? "魔法攻撃" : "物理攻撃";
                }
                else
                {
                    var sk = _data.Skills.Skills[i];
                    id = null; name = sk.Name; sub = sk.Description; icon = SkillIcon(sk);
                }
                bool selected = i == _bookIndex;
                bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
                Ui.Fill(r, selected ? new Color(0.35f, 0.26f, 0.08f) : hover ? new Color(0.18f, 0.18f, 0.32f) : new Color(0.1f, 0.1f, 0.18f));
                Ui.Frame(r, selected ? Ui.Gold : new Color(1, 1, 1, 0.2f), selected ? 2f : 1f);
                var pic = new Rect(r.x + 4, r.y + 4, rowH - 8, rowH - 8);
                if (id != null) Ui.Portrait(pic, id, Ui.PortraitColor(id), i * 3, false, selected);
                else Ui.Sprite(new Rect(pic.x + 6, pic.y + 6, pic.width - 12, pic.height - 12), icon);
                Ui.Text(new Rect(pic.xMax + 8, r.y + 4, r.width - pic.width - 16, 24), name, Ui.Label, selected ? Ui.Gold : Ui.Cream);
                Ui.Text(new Rect(pic.xMax + 8, r.y + 30, r.width - pic.width - 16, 20), sub, Ui.Small, Ui.Sub, false);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _bookIndex = i; _bookDetailScroll = Vector2.zero; Sound?.Play("click"); }
            }
            GUI.EndScrollView();
        }

        private static string TierLabel(MonsterSpecies s)
            => s.IsBoss ? "ボス" : s.Tier == 1 ? "下位" : s.Tier == 2 ? "中位" : s.Tier == 3 ? "上位" : "最上位";

        // ================= モンスターの詳細 =================

        private void DrawMonsterDetail(Rect win)
        {
            var list = BookMonsters;
            var s = list[Mathf.Clamp(_bookIndex, 0, list.Count - 1)];
            Ui.Window(win, $"No.{_bookIndex + 1:00}  {s.Name}");
            var inner = new Rect(win.x + 18, win.y + 22, win.width - 36, win.height - 36);
            var view = new Rect(0, 0, inner.width - 16f, 860f);
            _bookDetailScroll = GUI.BeginScrollView(inner, _bookDetailScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            float big = Mathf.Min(190f, view.width * 0.38f);
            var portrait = new Rect(4, 4, big, big);
            Ui.Portrait(portrait, s.Id, Ui.PortraitColor(s.Id), 0f, false, true);
            float x = portrait.xMax + 18;
            float w = view.width - x - 4;
            Ui.Text(new Rect(x, 4, w, 34), s.Name, Ui.Title, Ui.Gold);
            Badge(new Rect(x, 44, 70, 24), TierLabel(s), s.IsBoss ? Ui.Red : s.Tier == 4 ? new Color(0.2f, 0.45f, 0.6f) : s.Tier == 3 ? new Color(0.6f, 0.3f, 0.8f) : s.Tier == 2 ? new Color(0.85f, 0.55f, 0.15f) : new Color(0.3f, 0.7f, 0.3f));
            Badge(new Rect(x + 78, 44, 150, 24), DietLabel(s), new Color(0.25f, 0.35f, 0.55f));
            var body = Ui.Body;
            var oldColor = body.normal.textColor;
            body.normal.textColor = Ui.Cream;
            GUI.Label(new Rect(x, 76, w, big - 72), s.Description ?? "", body);
            body.normal.textColor = oldColor;

            // 戦闘能力
            float y = portrait.yMax + 16;
            Section(new Rect(0, y, view.width, 24), "戦闘能力(よく育つと最大+50%)");
            y += 30;
            var all = _data.Monsters.Species;
            StatLine(new Rect(0, y, view.width, 24), "icon_heart", "HP", s.MaxHp, all.Max(m => m.MaxHp)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_sword", "攻撃", s.Attack, all.Max(m => m.Attack)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_shield", "防御", s.Defense, all.Max(m => m.Defense)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_hourglass", "攻撃間隔", s.AttackInterval, all.Max(m => m.AttackInterval), $"{s.AttackInterval / 10f:0.0}秒"); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_boot", "移動間隔", s.MoveInterval, all.Max(m => m.MoveInterval), $"{s.MoveInterval / 10f:0.0}秒"); y += 34;

            // 生態
            Section(new Rect(0, y, view.width, 24), "生態");
            y += 30;
            InfoLine(ref y, view.width, "icon_pickaxe", "誕生", BirthText(s));
            if (!s.IsBoss)
            {
                InfoLine(ref y, view.width, "icon_hourglass", "寿命", $"{s.Lifespan / 10}秒(死ぬと体の養分・魔分が土に還り、通路に骨が残る)");
                string grow = $"体の養分+魔分が{s.ReproduceThreshold}以上になると分裂する(体の上限 {s.MaxBiomass})";
                var to = string.IsNullOrEmpty(s.EvolveTo) ? null : _data.Monsters.Get(s.EvolveTo);
                if (to != null) grow = $"生まれて{s.EvolveAge / 10}秒以上たち、分裂できるほど大きくなると、分裂せずに{to.Name}へ進化する";
                InfoLine(ref y, view.width, "icon_leaf", to != null ? "進化" : "増え方", grow);
                if (s.Diet == Diet.Predator) InfoLine(ref y, view.width, "icon_drop", "消化", $"一度食べると{s.DigestTicks / 10f:0.#}秒は食べない");
                InfoLine(ref y, view.width, "icon_star", "生まれやすい層", LayersFor(s));
            }
            y += 6;

            // 食物連鎖
            Section(new Rect(0, y, view.width, 24), "食物連鎖");
            y += 30;
            var eats = all.Where(o => !o.IsBoss && s.CanEat(o)).ToList();
            var eatenBy = all.Where(o => !o.IsBoss && o.CanEat(s)).ToList();
            ChainRow(new Rect(0, y, view.width, 52), "食べるもの", eats, s.Diet == Diet.Nutrient ? "土の養分" : s.Diet == Diet.Mana ? "土の魔分" : "なし");
            y += 58;
            ChainRow(new Rect(0, y, view.width, 52), "天敵", eatenBy, s.IsBoss ? "勇者だけ" : "なし");
            GUI.EndScrollView();
        }

        private static string DietLabel(MonsterSpecies s)
        {
            if (s.IsBoss) return "勇者を待ち構える";
            switch (s.Diet)
            {
                case Diet.Nutrient: return "土の養分を吸う";
                case Diet.Mana: return "土の魔分を吸う";
                default: return "モンスターを食べる";
            }
        }

        /// <summary>誕生のしかたの説明(土の濃さ/巣/墓場/進化)。</summary>
        private string BirthText(MonsterSpecies s)
        {
            if (s.IsBoss) return "掘っても生まれない(最下層のゴールにだけ現れる)";
            var from = _data.Monsters.Species.Where(o => o.EvolveTo == s.Id).Select(o => o.Name).ToList();
            var parts = new List<string>();
            if (s.DigSpawn) parts.Add($"【土】養分{s.SpawnNutrient}以上・魔分{s.SpawnMana}以上の土を掘ると生まれる");
            if (s.SpecialSpawn == SpecialSpawn.RoomNest)
                parts.Add("【巣】3×3以上の広い部屋を掘っておくと、育成中に巣を作って住みつく");
            if (s.SpecialSpawn == SpecialSpawn.Graveyard)
                parts.Add($"【墓場】モンスターが死んで骨が{s.SpecialBones}本以上積もった通路から、育成中に起き上がる");
            if (from.Count > 0) parts.Add($"【進化】{string.Join("・", from)}が育ち切ると進化して生まれる");
            if (!s.DigSpawn && s.SpecialSpawn != SpecialSpawn.None)
                parts.Add($"(体の分として、まわりの土から養分{s.SpawnNutrient}・魔分{s.SpawnMana}を使う)");
            return parts.Count == 0 ? "掘っても生まれない" : string.Join("\n", parts);
        }

        /// <summary>
        /// 土から生まれる種族が、どの層で生まれうるか(層の土の豊かさが誕生条件に届くか)。
        /// 特殊な誕生・進化の種族は、条件さえ整えばどの層でも生まれる。
        /// </summary>
        private string LayersFor(MonsterSpecies s)
        {
            if (!s.DigSpawn) return "どの層でも(特殊な条件しだい)";
            var layers = _data.Rules.Layers
                .Where(r => r.SoilNutrientMax >= s.SpawnNutrient && r.SoilManaMax >= s.SpawnMana)
                .Select(r => $"{r.Layer}層").ToList();
            return layers.Count == 0 ? "土が足りる層がない(土の養分・魔分が集まれば)" : string.Join("・", layers) + "(土が濃い場所で)";
        }

        // ================= 勇者の詳細 =================

        private void DrawHeroDetail(Rect win)
        {
            var classes = _data.Heroes.Classes;
            var c = classes[Mathf.Clamp(_bookIndex, 0, classes.Count - 1)];
            Ui.Window(win, $"勇者名鑑  {c.Name}");
            var inner = new Rect(win.x + 18, win.y + 22, win.width - 36, win.height - 36);
            var view = new Rect(0, 0, inner.width - 16f, 640f);
            _bookDetailScroll = GUI.BeginScrollView(inner, _bookDetailScroll, view, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            float big = Mathf.Min(190f, view.width * 0.38f);
            var portrait = new Rect(4, 4, big, big);
            Ui.Portrait(portrait, c.Id, Ui.PortraitColor(c.Id), 0f, false, true);
            float x = portrait.xMax + 18;
            float w = view.width - x - 4;
            Ui.Text(new Rect(x, 4, w, 34), c.Name, Ui.Title, Ui.Gold);
            Badge(new Rect(x, 44, 110, 24), c.AttackStat == AttackStat.Magic ? "魔法攻撃" : "物理攻撃", new Color(0.25f, 0.35f, 0.55f));
            if (c.Ability == ClassAbility.Heal) Badge(new Rect(x + 118, 44, 110, 24), "仲間を回復", new Color(0.25f, 0.55f, 0.3f));
            var body = Ui.Body;
            var oldColor = body.normal.textColor;
            body.normal.textColor = Ui.Cream;
            GUI.Label(new Rect(x, 76, w, big - 72), c.Description ?? "", body);
            body.normal.textColor = oldColor;

            float y = portrait.yMax + 16;
            Section(new Rect(0, y, view.width, 24), "初期の能力値");
            y += 30;
            var all = classes;
            var st = c.BaseStats;
            StatLine(new Rect(0, y, view.width, 24), "icon_heart", "HP", st.MaxHp, all.Max(o => o.BaseStats.MaxHp)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_sword", "攻撃", st.Attack, all.Max(o => o.BaseStats.Attack)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_shield", "防御", st.Defense, all.Max(o => o.BaseStats.Defense)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_boot", "素早さ", st.Speed, all.Max(o => o.BaseStats.Speed)); y += 28;
            StatLine(new Rect(0, y, view.width, 24), "icon_orb", "魔力", st.Magic, all.Max(o => o.BaseStats.Magic)); y += 34;

            Section(new Rect(0, y, view.width, 24), "特徴");
            y += 30;
            InfoLine(ref y, view.width, "icon_sword", "射程", c.AttackRange >= 2 ? $"{c.AttackRange}マス先まで攻撃できる" : "となりの敵だけ攻撃できる");
            InfoLine(ref y, view.width, "icon_torch", "視界", $"まわり{c.Vision}マスの霧を晴らしながら進む");
            InfoLine(ref y, view.width, "icon_star", "会心", $"{c.CritPercent}%の確率でダメージ2倍");
            if (c.Ability == ClassAbility.Heal)
                InfoLine(ref y, view.width, "icon_heart", "回復", $"HPが減った仲間を魔力×2回復({c.AbilityInterval / 10f:0.#}秒ごと)");
            GUI.EndScrollView();
        }

        // ================= スキル =================

        private void DrawSkillDetail(Rect win)
        {
            var skills = _data.Skills.Skills;
            var sk = skills[Mathf.Clamp(_bookIndex, 0, skills.Count - 1)];
            Ui.Window(win, "スキル一覧");
            var inner = new Rect(win.x + 18, win.y + 22, win.width - 36, win.height - 36);

            // 選んだスキルを大きく
            var top = new Rect(inner.x, inner.y, inner.width, 110);
            Ui.Fill(top, new Color(0.35f, 0.26f, 0.08f));
            Ui.Frame(top, Ui.Gold, 2f);
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 3f;
            Ui.Sprite(new Rect(top.x + 14, top.y + 14 + bob, 80, 80), SkillIcon(sk));
            Ui.Text(new Rect(top.x + 110, top.y + 10, top.width - 120, 34), sk.Name, Ui.Title, Ui.Gold);
            var body = Ui.Body;
            var oldColor = body.normal.textColor;
            body.normal.textColor = Ui.Cream;
            GUI.Label(new Rect(top.x + 110, top.y + 50, top.width - 120, 56), sk.Description, body);
            body.normal.textColor = oldColor;

            // 全スキルの早見表
            var grid = new Rect(inner.x, top.yMax + 14, inner.width, inner.height - top.height - 14);
            Section(new Rect(grid.x, grid.y, grid.width, 24), $"全{skills.Count}種(層を突破するたびに3つの候補から1つ獲得)");
            int perRow = _portrait ? 2 : 3;
            float cw = (grid.width - 8f * (perRow - 1)) / perRow;
            float ch = 40f;
            for (int i = 0; i < skills.Count; i++)
            {
                var r = new Rect(grid.x + (i % perRow) * (cw + 8f), grid.y + 32 + (i / perRow) * (ch + 6f), cw, ch);
                bool sel = i == _bookIndex;
                Ui.Fill(r, sel ? new Color(0.35f, 0.26f, 0.08f) : new Color(0.1f, 0.1f, 0.18f));
                Ui.Frame(r, sel ? Ui.Gold : new Color(1, 1, 1, 0.2f));
                Ui.Sprite(new Rect(r.x + 4, r.y + 4, 32, 32), SkillIcon(skills[i]));
                Ui.Text(new Rect(r.x + 42, r.y + 8, r.width - 46, 24), skills[i].Name, Ui.Label, sel ? Ui.Gold : Ui.Cream);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _bookIndex = i;
            }
        }

        // ================= 部品 =================

        private static void Badge(Rect r, string text, Color color)
        {
            Ui.Fill(r, Ui.Ink);
            Ui.Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), color);
            Ui.Text(r, text, Ui.Center, Ui.Cream);
        }

        private static void Section(Rect r, string title)
        {
            Ui.Text(new Rect(r.x, r.y, r.width, r.height), "■ " + title, Ui.Label, Ui.Gold);
            Ui.Fill(new Rect(r.x, r.yMax + 2, r.width, 1), new Color(1f, 0.84f, 0.3f, 0.4f));
        }

        private static void StatLine(Rect r, string icon, string label, int value, int max, string valueText = null)
        {
            Ui.Sprite(new Rect(r.x, r.y + 2, 20, 20), Sprites.Get(icon));
            Ui.Text(new Rect(r.x + 26, r.y, 90, r.height), label, Ui.Label, Ui.Cream);
            Ui.Bar(new Rect(r.x + 120, r.y + 6, r.width - 200, 12), value, Mathf.Max(1, max), Ui.Sky);
            Ui.Text(new Rect(r.xMax - 72, r.y, 72, r.height), valueText ?? value.ToString(), Ui.Label, Ui.Cream);
        }

        private static void InfoLine(ref float y, float width, string icon, string label, string text)
        {
            var r = new Rect(0, y, width, 26);
            Ui.Sprite(new Rect(r.x, r.y + 3, 20, 20), Sprites.Get(icon));
            Ui.Text(new Rect(r.x + 26, r.y + 1, 100, 24), label, Ui.Label, Ui.Gold);
            var style = Ui.Wrap;
            var old = style.normal.textColor;
            style.normal.textColor = Ui.Cream;
            float h = Mathf.Max(24f, style.CalcHeight(new GUIContent(text), width - 130f));
            GUI.Label(new Rect(r.x + 128, r.y + 4, width - 130f, h), text, style);
            style.normal.textColor = old;
            y += h + 6f;
        }

        private static void ChainRow(Rect r, string label, List<MonsterSpecies> list, string emptyText)
        {
            Ui.Fill(r, new Color(0, 0, 0, 0.3f));
            Ui.Text(new Rect(r.x + 8, r.y + 14, 100, 24), label, Ui.Label, Ui.Gold);
            if (list.Count == 0)
            {
                Ui.Text(new Rect(r.x + 110, r.y + 14, r.width - 120, 24), emptyText, Ui.Label, Ui.Cream);
                return;
            }
            // 欄の幅に収まるよう間隔を詰める(狭いときは名前を省いて絵だけにする)
            float step = Mathf.Min(150f, (r.width - 110f) / list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var p = new Rect(r.x + 110 + i * step, r.y + 4, 44, 44);
                Ui.Portrait(p, list[i].Id, Ui.PortraitColor(list[i].Id), i * 2, false, false);
                if (step >= 120f) Ui.Text(new Rect(p.xMax + 6, r.y + 14, step - 52, 24), list[i].Name, Ui.Small, Ui.Cream);
            }
        }
    }
}
