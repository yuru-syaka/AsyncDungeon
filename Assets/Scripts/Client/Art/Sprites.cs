using System.Collections.Generic;
using UnityEngine;

namespace DungeonGame.Client.Art
{
    /// <summary>
    /// ゲームで使うドット絵。すべてオリジナルの仮素材。
    ///  ・キャラクター(勇者・魔王・モンスター):24×24。3段階の陰影で描き、輪郭と待機アニメの2コマ目は PixelArt が自動で付ける。
    ///    '|' は8ピクセルごとの区切り(数えやすくするため。描画には影響しない)。
    ///  ・アイコン:16×16。輪郭も手で描いている。
    /// 種族ID・職業ID・"icon_xxx" で引けるようにしてあり、未知のIDには「?」の絵を返す。
    /// </summary>
    public static class Sprites
    {
        // ================= モンスター =================

        private static readonly string[] MossSlime =
        {
            "........|........|........",
            "........|...lG...|........",
            "........|..lGGl..|........",
            "........|.G.lg.G.|........",
            "........|llggggll|........",
            "......ll|gggggggg|gg......",
            ".....lgg|gwwggggg|ggg.....",
            "....lggg|wwgggggg|gggg....",
            "....gggg|gggggggg|gggg....",
            "...gggkk|ggggggkk|gggG....",
            "...gggkw|ggggggkw|gggG....",
            "..lggggg|gggggggg|ggggG...",
            "..gggggg|ggrrrrgg|ggggGG..",
            "..Gggggg|gggrrggg|gggGGG..",
            "..GGgggg|gggggggg|ggGGGL..",
            "...GGGGg|gggggggg|gGGGL...",
            "....LGGG|GGGGGGGG|GGLL....",
            "......LL|LLLLLLLL|LL......",
        };

        private static readonly string[] ManaMite =
        {
            "......k.|........|.k......",
            ".......k|........|k.......",
            "........|k......k|........",
            "........|.kcccck.|........",
            "........|kcwcccck|........",
            "......kB|bcccccbb|Bk......",
            ".....Bbb|bbbccbbb|bbbB....",
            "....Bbbb|bbbbbbbb|bbbbB...",
            "...Bbbkk|bbbbbbbb|kkbbbB..",
            "...bbkwk|bbbbbbbb|kwkbbB..",
            "...bbkkk|bbbbbbbb|kkkbbB..",
            "...Bbbbb|bbbbbbbb|bbbbBB..",
            "...BBbbb|bbmmmmbb|bbbBBB..",
            "....BBBb|bbbbbbbb|bBBBB...",
            ".....CBB|BBBBBBBB|BBBC....",
            "....k.Ck|.k.kk.k.|kC.k....",
            "...k..k.|.k.kk.k.|.k..k...",
            "..k..k..|k..kk..k|..k..k..",
        };

        private static readonly string[] CaveLizard =
        {
            "........|........|.oooo...",
            "........|........|oouuoo..",
            "........|...O.O.O|oowkooo.",
            "........|..OoOoOo|ooookooo",
            "........|.Ooooooo|oooooRRo",
            "..OO....|Oooooooo|oooooooR",
            ".Oo.....|ooooooou|uooooRR.",
            ".Oo....O|oooouuuu|uuoooo..",
            ".Oo...Oo|oouuuuuu|uuoooO..",
            "..Oo.Ooo|ouuuuuuu|uuuoO...",
            "...OOooo|oouuuuuu|uuoO....",
            "....OOoo|ooouuuuu|ooO.....",
            "......OO|oooooooo|oO......",
            "......Oo|O.....Oo|O.......",
            ".....Oo.|O....Oo.|O.......",
            ".....OO.|.....OO.|........",
        };

        private static readonly string[] AbyssWyrm =
        {
            "........|.PPPP...|........",
            "........|PppppP..|........",
            ".......P|pvppppP.|........",
            ".......p|pkyppppP|........",
            ".......p|pppppppP|........",
            "........|ppaapaap|........",
            "........|.pa.pa.p|........",
            "........|.....Ppp|P.......",
            "........|.....ppv|pP......",
            "........|......pv|ppP.....",
            "...PPP..|......Pp|vpP.....",
            "..Ppppp.|.....Ppv|ppP.....",
            ".PpvvvpP|....Ppvp|pP......",
            ".ppvpppp|PPPPpvpp|P.......",
            ".ppppPpp|pppppvpp|........",
            ".Pppp.Pp|pvvvvvpP|........",
            "..PpP..P|pppppppP|........",
            "...P....|PPPPPPP.|........",
        };

        private static readonly string[] GlowShroom =
        {
            "........|.YYYYYY.|........",
            "......YY|yyyyyyyy|YY......",
            ".....Yyy|yjjyyyyy|yyY.....",
            "....Yyyj|jyyyyjjy|yyyY....",
            "...Yyyyy|yyyyyjjy|yyyyY...",
            "...YyjjY|yyyyyyyy|YjyyY...",
            "..YYYYYY|YYYYYYYY|YYYYYY..",
            "........|uuuuuuuu|........",
            ".......u|ukuuukuu|u.......",
            ".......u|uuuuuuuu|u.......",
            ".......u|uuurruuu|u.......",
            "........|uuuuuuuu|........",
            "........|uuuuuuuh|........",
            "......lG|huuuuuuh|Gl......",
            ".....lGG|Ghhhhhhh|GGl.....",
        };

        private static readonly string[] ShadowBat =
        {
            "P.......|..k..k..|.......P",
            "PP......|.kPPPPk.|......PP",
            "pPP.....|kPPPPPPk|.....PPp",
            "ppPP....|PPyPPyPP|....PPpp",
            "pppPP...|PPPPPPPP|...PPppp",
            "ppppPP..|PPaPPaPP|..PPpppp",
            "pPppppPP|PPPPPPPP|PPppppPp",
            ".PpPpppp|PPPPPPPP|pppPpPp.",
            "..P.Pp.p|pPPPPPPp|p.pP.P..",
            "......P.|.pPPPPp.|.P......",
            "........|..P..P..|........",
        };

        private static readonly string[] CrystalGolem =
        {
            "........|..c..c..|........",
            "........|.cwc.cc.|........",
            "........|ZzzzzzzZ|........",
            ".......Z|zzkzzkzz|Z.......",
            ".......Z|zzczzczz|Z.......",
            ".......Z|zzzzzzzz|Z.......",
            "....ZZZZ|ZzzzzzzZ|ZZZZ....",
            "...Zzzzc|ZzzzzzzZ|czzzZ...",
            "...Zzzcc|zzzccczz|cczzZ...",
            "...Zzzzz|zzcwcczz|zzzzZ...",
            "...ZZzzz|zzzcczzz|zzzZZ...",
            "....ZZZ.|ZzzzzzzZ|.ZZZ....",
            "........|ZzzZZzzZ|........",
            ".......Z|zzZ..Zzz|Z.......",
            ".......Z|ZZZ..ZZZ|Z.......",
        };

        private static readonly string[] ArmorCentipede =
        {
            "........|........|..kk.k..",
            "........|........|.k..k...",
            "........|........|RrrR....",
            "........|........|rqrrR...",
            "........|...RRRRR|rkrrrR..",
            "...RRRRR|RRrrrrrR|rrrrraa.",
            "..RrrrrR|rrrqrrrR|rrrrR...",
            ".RrqrrrR|rrrrrrrR|RRRR....",
            ".RrrrrrR|RRRRRRRR|........",
            "..RRRRRR|.N.N.N.N|........",
            "..N.N.N.|.N.N.N.N|........",
            ".N.N.N.N|N.N.N.N.|........",
        };

        private static readonly string[] SkullMage =
        {
            "........|..aaaa..|........",
            "........|.aaaaaa.|..m.....",
            "........|aakaakaa|.mvm....",
            "........|aakaakaa|..m.....",
            "........|.aaaaaa.|..n.....",
            "........|..akaa..|..n.....",
            "......dd|ddddddd.|..n.....",
            ".....dPP|PPPPPPdd|.an.....",
            "....dPPP|PvvvPPPP|daa.....",
            "....dPPP|PPvPPPPP|d.n.....",
            "....aadP|PPvPPPPd|..n.....",
            "....aa.d|PPPPPPPd|..n.....",
            ".......d|PPPPPPPd|..n.....",
            "......dP|PPPPPPPP|d.n.....",
            ".....dPP|PPPPPPPP|Pdn.....",
            ".....ddd|dddddddd|dd......",
        };

        private static readonly string[] AbyssDragon =
        {
            "........|........|..a..a..",
            "........|........|..TT.T..",
            "T.......|........|.TttTT..",
            "TT......|.......T|TtyttkT.",
            "tTT.....|......Tt|tttttttT",
            "ttTT....|.....Ttt|ttttaaa.",
            "tttTT...|....TTtt|tTTT....",
            "ttttTTT.|..TTtttt|tT......",
            ".tttttTT|TTtttttt|tT......",
            "..TtttTt|tttucuuT|tT......",
            "....TTtt|ttuuuuuT|T.......",
            ".....Ttt|tuuuuuuT|........",
            "....TtTt|ttttttTT|........",
            "...TtT.T|tT..TtT.|........",
            "..TT...T|T....TT.|........",
            ".TT.....|........|........",
        };

        private static readonly string[] DemonLord =
        {
            ".a......|........|......a.",
            ".aa.....|........|.....aa.",
            "..aa....|..YyyY..|....aa..",
            "..aaa...|.YyjjyY.|...aaa..",
            "...aaa..|YyEEEEyY|..aaa...",
            "....aaEE|EEEEEEEE|EEaa....",
            ".....EEx|xxxxxxxx|xEE.....",
            ".....Exx|yyxxxxyy|xxE.....",
            ".....Exx|xxxxxxxx|xxE.....",
            ".....ExR|xxrrrrxx|RxE.....",
            "...RR.Ex|xaxaaxax|xE.RR...",
            "..RrrR.E|xxxxxxxx|E.RrrR..",
            ".RrrrRRP|PYPPPPYP|PRRrrrR.",
            ".RrrrrRP|ppYppYpp|PRrrrrR.",
            "RRrrrrRP|pppYYppp|PRrrrrRR",
            "RrrrrRRs|PppppppP|sRRrrrrR",
            "RrrrR.sS|PpppppPP|SsRrrrrR",
            "RrrrR...|PPpppPP.|..RrrrrR",
            "RrrR....|ZzP..PzZ|...RrrrR",
            "RrR.....|Zz....zZ|....RrRR",
            "RR......|EE....EE|.....RRR",
        };

        // ================= 勇者 =================

        private static readonly string[] Warrior =
        {
            "........|..rr....|........",
            "........|.rrqr...|........",
            "........|..rrr...|........",
            "........|.zzzzz..|........",
            "........|zzizzzZ.|........",
            ".......z|zizzzzzZ|........",
            ".......z|ZZZZZZZZ|........",
            ".......Z|skssskSS|.....i..",
            ".......Z|sssssssS|....iz..",
            "........|SssssssS|...iz...",
            "........|.SssssS.|..iz....",
            "....bbb.|zzzzzzzz|.iz.....",
            "...bBbbb|zizzzzzz|zYY.....",
            "...bByBb|rrrrrrrr|sYs.....",
            "...bBbbb|rrryrrrr|Ss......",
            "...bBbbb|rrryrrrR|........",
            "....Bbb.|RrrrrrrR|........",
            ".....B..|ZZYYYYZZ|........",
            "........|NNN..NNN|........",
            "........|NNN..NNN|........",
            "........|nnN..nnN|........",
            ".......N|NNN..NNN|N.......",
        };

        private static readonly string[] Mage =
        {
            "........|...P....|........",
            "........|...pP...|........",
            "........|..ppP...|........",
            "........|..pvpP..|....c...",
            "........|.ppyppP.|...cwc..",
            "........|.pppppP.|....c...",
            "......PP|pppppppP|P...n...",
            ".....PPp|pppppppp|PP..n...",
            "........|sssssssS|....n...",
            "........|skssskSS|....n...",
            "........|sssssssS|....n...",
            "........|.SssssS.|....n...",
            ".......p|pppvvppp|pp.sn...",
            "......pp|ppvppvpp|ppps....",
            ".....ppp|pppvvppp|ppP.n...",
            ".....ppp|pppppppp|pP..n...",
            ".....Ppp|ppppyppp|pP..n...",
            ".....Ppp|pppppppp|pP..n...",
            "....PPpp|pppppppp|pPP.n...",
            "....PPPp|pppppppp|PPP.n...",
            "...PPPPP|PPPPPPPP|PPPP....",
        };

        private static readonly string[] Priest =
        {
            "........|..iiii..|........",
            "........|.iwwwwi.|........",
            "........|iwwwwwwi|........",
            ".......i|wwYYYYww|i.......",
            ".......i|wssssssw|i.......",
            ".......i|wsksskSw|i.......",
            ".......i|wssssssw|i....y..",
            ".......i|iwSssSwi|i...yjy.",
            "......iw|wwwwwwww|wi...y..",
            ".....iww|wwwYwwww|wwi..n..",
            ".....iww|wwYYYwww|wwi.sn..",
            "....swww|wwwYwwww|wwwsSn..",
            "....Swww|wwwYwwww|wwwi.n..",
            ".....iww|wwwwwwww|wwi..n..",
            ".....iww|wwwwwwww|wwi..n..",
            ".....iiw|wwwwwwww|wii..n..",
            ".....iii|wwwwwwww|iii..n..",
            "....eiii|iiiiiiii|iiie.n..",
            "....eeYY|YYYYYYYY|YYee.n..",
            "....eeee|eeeeeeee|eeee....",
        };

        private static readonly string[] Thief =
        {
            "........|.GGGGG..|........",
            "........|GgggggG.|........",
            ".......G|gggllggG|........",
            ".......G|gggggggG|G.......",
            ".......G|ssssssGG|.G......",
            ".......G|kkskkkGG|..G.....",
            "........|sssssSG.|........",
            "........|LLLLLLL.|........",
            "......gg|GgggggGg|g.......",
            ".....sgg|gggggggg|gs......",
            "....iSgg|gNNYNNgg|gSi.....",
            "...iz.gg|gggggggg|g.zi....",
            "..iz..Gg|gggggggg|G..zi...",
            "......Gg|ggggggGG|........",
            ".......G|GGG..GGG|........",
            ".......N|NN....NN|N.......",
            ".......N|N......N|N.......",
            "......NN|N......N|NN......",
            "......NN|........|NN......",
        };

        private static readonly string[] Hunter =
        {
            "........|..yy....|........",
            "........|.hhhy...|........",
            "........|hhhhhh..|........",
            ".......h|hhhhhhh.|........",
            ".......N|sssssss.|........",
            ".......N|skssskS.|.n......",
            "........|sssssSS.|.nn.....",
            "........|.SssSS..|.n.n....",
            "......nn|hhhhhhn.|.n..n...",
            ".....nhh|hhhhhhhn|wn...n..",
            "....Shhh|NNYNNhhh|.n...n..",
            "....sShh|hhhhhhhh|.n..n...",
            ".......h|hhhhhhhh|.n.n....",
            ".......h|hhh..hhh|.nn.....",
            ".......N|NN....NN|.n......",
            ".......N|N......N|........",
            "......NN|N......N|N.......",
            "......NN|........|NN......",
        };

        private static readonly string[] Paladin =
        {
            "........|..zzzz..|........",
            "........|.zziizz.|........",
            "........|zzyzzyzz|........",
            "........|zZyyyyZz|........",
            "........|zZskskZz|........",
            "........|.ZssssZ.|........",
            ".....www|wbbbbbbw|ww......",
            "....wbbb|bbyybbbb|bw.yy...",
            "...BBBBb|bbyybbbb|bw.yy...",
            "..BbyybB|bbbbbbbb|bwssn...",
            "..BbyybB|bbbbbbbb|bw..n...",
            "..BbbbbB|zzYYYYzz|bw..n...",
            "..BBbbBB|bbbbbbbb|w.......",
            "...BBBB.|bbb..bbb|w.......",
            "........|ZZZ..ZZZ|........",
            "........|ZZZ..ZZZ|........",
            ".......Z|ZZZ..ZZZ|Z.......",
        };

        private static readonly string[] Monk =
        {
            "........|..ssss..|........",
            "........|.ssssss.|........",
            "........|rrrrrrrr|r.......",
            "........|sksssksS|.r......",
            "........|sssssssS|........",
            "........|.SsssSS.|........",
            "....ss..|.oooooo.|..ss....",
            "...sSs.o|oooooooo|o.sSs...",
            "....Ss.o|ooouuooo|o.sS....",
            ".....soo|ooouuooo|oos.....",
            "......oo|oooooooo|oo......",
            "........|xxxxxxxx|........",
            ".......o|oooooooo|o.......",
            "......oo|ooo..ooo|oo......",
            "......oo|oo....oo|oo......",
            ".....sss|s......s|sss.....",
        };

        private static readonly string[] Unknown =
        {
            "........|.mmmmmm.|........",
            "........|mmmmmmmm|........",
            ".......m|mm....mm|m.......",
            ".......m|m......m|m.......",
            "........|.......m|m.......",
            "........|......mm|m.......",
            "........|....mmmm|........",
            "........|...mmm..|........",
            "........|...mm...|........",
            "........|........|........",
            "........|...mm...|........",
            "........|...mm...|........",
        };

        // ---------------- アイコン ----------------

        private static readonly string[] Heart =
        {
            "................",
            "................",
            "...kkk...kkk....",
            "..krrrk.krrrk...",
            ".krwrrrkrrrrrk..",
            ".krwrrrrrrrrrk..",
            ".krrrrrrrrrrrk..",
            "..krrrrrrrrrk...",
            "...krrrrrrrk....",
            "....krrrrrk.....",
            ".....krrrk......",
            "......krk.......",
            ".......k........",
            "................",
        };

        private static readonly string[] Torch =
        {
            ".......y........",
            "......yoy.......",
            ".....yoroy......",
            ".....yorroy.....",
            "......yrry......",
            "......kyyk......",
            "......kNNk......",
            "......knnk......",
            "......kNNk......",
            "......knnk......",
            "......kNNk......",
            "......knnk......",
            "......kkkk......",
        };

        private static readonly string[] Star =
        {
            ".......k........",
            "......kyk.......",
            "......kyk.......",
            ".....kyyyk......",
            "kkkkkyyyyykkkkk.",
            ".kyyyyywyyyyyk..",
            "..kyyyyyyyyyk...",
            "...kyyyyyyyk....",
            "...kyyyyyyyk....",
            "..kyyykkkyyyk...",
            "..kyyk...kyyk...",
            ".kyk.......kyk..",
            ".kk.........kk..",
        };

        private static readonly string[] StarEmpty =
        {
            ".......k........",
            "......kEk.......",
            "......kEk.......",
            ".....kEEEk......",
            "kkkkkEEEEEkkkkk.",
            ".kEEEEEEEEEEEk..",
            "..kEEEEEEEEEk...",
            "...kEEEEEEEk....",
            "...kEEEEEEEk....",
            "..kEEEkkkEEEk...",
            "..kEEk...kEEk...",
            ".kEk.......kEk..",
            ".kk.........kk..",
        };

        private static readonly string[] Pickaxe =
        {
            "................",
            "...kkkkkk.......",
            "..kiiiiiik......",
            ".kiikkkkiik.....",
            ".kik..kn.kik....",
            ".kk...knk.kk....",
            "......knnk......",
            ".......knk......",
            "........knk.....",
            ".........knk....",
            "..........knk...",
            "...........knk..",
            "............kk..",
        };

        private static readonly string[] Sword =
        {
            "...........kk...",
            "..........kik...",
            ".........kiik...",
            "........kiik....",
            ".......kiik.....",
            "......kiik......",
            "..kk.kiik.......",
            "..kykiik........",
            "...kyik.........",
            "...kkyk.........",
            "..kyk.kyk.......",
            ".knk...kk.......",
            ".kk.............",
        };

        private static readonly string[] Shield =
        {
            "................",
            "..kkkkkkkkkk....",
            "..kbbbbibbbbk...",
            "..kbbbbibbbbk...",
            "..kbiiiiiiibk...",
            "..kbbbbibbbbk...",
            "..kbbbbibbbbk...",
            "...kbbbibbbk....",
            "...kbbbibbbk....",
            "....kbbibbk.....",
            ".....kbbbk......",
            "......kkk.......",
        };

        private static readonly string[] Boot =
        {
            "................",
            "....kkkkk.......",
            "....knnnk..c....",
            "....knnnk.c.....",
            "....knnnk..c....",
            "....knnnk.......",
            "....knnnkkkk....",
            "....knnnnnnnk...",
            "...knnnnnnnnk...",
            "...kNNNNNNNNk...",
            "...kkkkkkkkkk...",
        };

        private static readonly string[] Orb =
        {
            "................",
            ".....kkkkk......",
            "....kvvcvvk.....",
            "...kvcwcvvvk....",
            "...kvwcvvvvk....",
            "...kvvvvvvpk....",
            "...kvvvvvppk....",
            "....kvvpppk.....",
            ".....kkkkk......",
            "....kyyyyyk.....",
            "...kYYYYYYYk....",
            "...kkkkkkkkk....",
        };

        private static readonly string[] Leaf =
        {
            "................",
            ".........kkk....",
            ".......kkgggk...",
            "......kgggggk...",
            ".....kgglggk....",
            "....kgglggGk....",
            "....kglggGk.....",
            "...kgggGGk......",
            "...kGGGkk.......",
            "..kk.kk.........",
            ".kk.............",
        };

        private static readonly string[] Drop =
        {
            "......k.........",
            ".....kck........",
            ".....kck........",
            "....kcbbk.......",
            "...kcbbbbk......",
            "...kwbbbbk......",
            "..kcwbbbbBk.....",
            "..kcbbbbbBk.....",
            "..kbbbbbBBk.....",
            "...kbbBBBk......",
            "....kkkkk.......",
        };

        private static readonly string[] Skull =
        {
            "................",
            "....kkkkkkk.....",
            "...kiiiiiiik....",
            "..kiiiiiiiiik...",
            "..kikkiiikkik...",
            "..kikkiiikkik...",
            "..kiiiikiiiik...",
            "...kiikkkiik....",
            "....kiiiiik.....",
            "....kikikik.....",
            "....kkkkkkk.....",
        };

        private static readonly string[] Hourglass =
        {
            "..kkkkkkkkk.....",
            "..kNNNNNNNk.....",
            "...kyyyyyk......",
            "...kyyyyyk......",
            "....kyyyk.......",
            ".....kyk........",
            "....kcyck.......",
            "...kcccyck......",
            "...kcyyyyk......",
            "..kNNNNNNNk.....",
            "..kkkkkkkkk.....",
        };

        // ================= 引き出し =================

        private static readonly Dictionary<string, string[]> Characters = new Dictionary<string, string[]>
        {
            ["moss_slime"] = MossSlime, ["mana_mite"] = ManaMite, ["cave_lizard"] = CaveLizard,
            ["abyss_wyrm"] = AbyssWyrm, ["demon_lord"] = DemonLord,
            ["glow_shroom"] = GlowShroom, ["shadow_bat"] = ShadowBat, ["crystal_golem"] = CrystalGolem,
            ["armor_centipede"] = ArmorCentipede, ["skull_mage"] = SkullMage, ["abyss_dragon"] = AbyssDragon,
            ["warrior"] = Warrior, ["mage"] = Mage, ["priest"] = Priest, ["thief"] = Thief,
            ["hunter"] = Hunter, ["paladin"] = Paladin, ["monk"] = Monk,
        };

        private static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>
        {
            ["icon_heart"] = Heart, ["icon_torch"] = Torch, ["icon_star"] = Star, ["icon_star_empty"] = StarEmpty,
            ["icon_pickaxe"] = Pickaxe, ["icon_sword"] = Sword, ["icon_shield"] = Shield, ["icon_boot"] = Boot,
            ["icon_orb"] = Orb, ["icon_leaf"] = Leaf, ["icon_drop"] = Drop, ["icon_skull"] = Skull,
            ["icon_hourglass"] = Hourglass,
        };

        private static readonly Dictionary<string, Texture2D[]> Cache = new Dictionary<string, Texture2D[]>();

        /// <summary>IDでドット絵を取る(モンスター種族ID・職業ID・"icon_xxx")。キャラクターは待機の1コマ目。</summary>
        public static Texture2D Get(string id) => Get(id, 0);

        /// <summary>IDとコマ番号(0か1)でドット絵を取る。アイコンはコマに関係なく同じ絵。</summary>
        public static Texture2D Get(string id, int frame)
        {
            var key = id ?? "";
            if (!Cache.TryGetValue(key, out var frames) || frames == null || frames[0] == null)
            {
                if (Characters.TryGetValue(key, out var rows)) frames = PixelArt.BuildCharacter(rows);
                else if (Icons.TryGetValue(key, out var icon)) { var t = PixelArt.Build(icon); frames = new[] { t, t }; }
                else frames = PixelArt.BuildCharacter(Unknown);
                Cache[key] = frames;
            }
            return frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }

        /// <summary>
        /// 待機アニメのコマ(時間で1コマ目と2コマ目を切り替える)。seed をずらすと、キャラごとにタイミングがばらける。
        /// </summary>
        public static Texture2D Anim(string id, float seed = 0f, float speed = 2.2f)
            => Get(id, (int)(Time.unscaledTime * speed + seed * 0.37f) % 2);

        public static bool IsCharacter(string id) => id != null && Characters.ContainsKey(id);
    }
}
