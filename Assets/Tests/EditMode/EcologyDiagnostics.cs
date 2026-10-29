using System.Linq;
using System.Text;
using NUnit.Framework;

namespace DungeonGame.Core.Tests
{
    /// <summary>バランス調整用:個体数の推移を出力する(常に成功する診断テスト)。</summary>
    public class EcologyDiagnostics
    {
        [Test, Explicit("バランス調整用")]
        public void 個体数の推移を出力する()
        {
            var data = TestData.LoadGameData();
            var sb = new StringBuilder();
            for (int layer = 1; layer <= 5; layer++)
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var b = TestData.BuildTypical(data, layer, seed * 1000 + 24);
                sb.Append($"L{layer} s{seed}: dug={b.Map.DugCount} ");
                for (int t = 0; t <= b.Rule.MaxEcologyTicks; t += 600)
                {
                    var groups = b.State.Monsters.GroupBy(m => m.Species.Id.Substring(0, 4))
                        .Select(g => $"{g.Key}{g.Count()}");
                    sb.Append($"[{t}:{string.Join(",", groups)}] ");
                    b.Grow(200);
                }
                sb.AppendLine();
            }
            TestContext.WriteLine(sb.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "diag.txt"), sb.ToString());
        }
    }
}
