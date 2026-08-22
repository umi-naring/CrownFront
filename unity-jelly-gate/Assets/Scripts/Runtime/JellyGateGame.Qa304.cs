using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaBalance304Routine()
        {
            yield return null;
            var failures = new List<string>();

            var counts = Enumerable.Range(1, MaxRounds).Select(WaveEnemyCount).ToArray();
            if (counts.Length != 50 || counts[0] != 14 || counts[24] != 54 || counts[^1] != 80 ||
                counts.Any(count => count < 14 || count > 80))
                failures.Add($"wave-counts:{counts.FirstOrDefault()}-{counts.LastOrDefault()}");

            var threat = Enumerable.Range(1, MaxRounds)
                .Select(round => WaveEnemyCount(round) * BaseEnemyHealth(round) *
                                 EnemyRoundDamageMultiplierFor(round)).ToArray();
            var stepRatios = threat.Skip(1).Select((value, index) => value / threat[index]).ToArray();
            var minimumStep = stepRatios.Min();
            var maximumStep = stepRatios.Max();
            if (minimumStep < .84f || maximumStep > 1.38f ||
                threat[24] < threat[0] * 12f || threat[49] > threat[44] * 1.08f ||
                threat[49] < threat[44] * .85f)
                failures.Add($"threat-curve:{minimumStep:0.000}-{maximumStep:0.000}:" +
                             $"{threat[0]:0}-{threat[24]:0}-{threat[44]:0}-{threat[49]:0}");

            var openingRoles = Enumerable.Range(0, WaveEnemyCount(1))
                .Select(index => EnemyVariantCatalog.ForWaveMember(1, index).CombatClass).ToArray();
            if (!openingRoles.Contains(EnemyClass.Mage) || !openingRoles.Contains(EnemyClass.Siege) ||
                WaveSquadSize(1) < 5 || WaveSquadInterval(1) > .401f)
                failures.Add("opening-tactical-pressure");
            var undeadRoles = Enumerable.Range(0, WaveEnemyCount(7))
                .Select(index => EnemyVariantCatalog.ForWaveMember(7, index).CombatClass).ToArray();
            if (!undeadRoles.Contains(EnemyClass.Cursebinder)) failures.Add("undead-specialist-pressure");

            var hammer = definitions[UnitArchetype.Melee];
            if (hammer.Cost != 4 || hammer.MaxHealth < 150f || hammer.Range < .94f ||
                hammer.Armor < 34f || hammer.MagicResistance < 22f || hammer.SkillCooldown > 5.5f)
                failures.Add($"hammer:{hammer.Cost}/{hammer.MaxHealth:0}/{hammer.Range:0.00}/" +
                             $"{hammer.Armor:0}/{hammer.MagicResistance:0}/{hammer.SkillCooldown:0.0}");

            if (GuideContentHeight(0) < 1400f || GuideContentHeight(0) > 1500f)
                failures.Add($"guide-height:{GuideContentHeight(0):0}");

            var savedRound = Round;
            var savedPhase = Phase;
            var baseline = enemies.Where(enemy => enemy != null).ToHashSet();
            Round = 5;
            Phase = GamePhase.Battle;
            var bossProfile = EnemyVariantCatalog.ForChapterStage(0, 4);
            var bossObject = new GameObject("QA 304 Staggered Summon Boss");
            var boss = bossObject.AddComponent<EnemyUnit>();
            boss.Initialize(this, 0, 6000f, true, 0, bossProfile.CombatClass, bossProfile);
            enemies.Add(boss);
            var before = enemies.Count;
            var scheduled = SpawnBossMinions(boss, EnemyClass.Runner, 5);
            var maximumBuiltInFrame = enemies.Count - before;
            var previousCount = enemies.Count;
            for (var frame = 0; frame < 7; frame++)
            {
                yield return null;
                var currentCount = enemies.Count;
                maximumBuiltInFrame = Mathf.Max(maximumBuiltInFrame, currentCount - previousCount);
                previousCount = currentCount;
            }
            var summoned = enemies.Where(enemy => enemy != null && !baseline.Contains(enemy) && enemy != boss)
                .ToArray();
            if (scheduled != 5 || summoned.Length != 5 || maximumBuiltInFrame > 1 ||
                summoned.Any(enemy => !IsWithinGroundEnemyRoadCorridor(enemy.Position, enemy.Radius * .42f)))
                failures.Add($"summon-stagger:{scheduled}/{summoned.Length}/frame={maximumBuiltInFrame}");

            var summonStarts = summoned.ToDictionary(enemy => enemy, enemy => enemy.Position);
            yield return new WaitForSeconds(1.2f);
            if (summoned.Any(enemy => enemy != null && enemy.IsAlive &&
                                      Vector2.Distance(summonStarts[enemy], enemy.Position) < .08f))
                failures.Add("summon-path-progress");

            foreach (var enemy in enemies.Where(enemy => enemy != null && !baseline.Contains(enemy)).ToArray())
            {
                enemies.Remove(enemy);
                Destroy(enemy.gameObject);
            }
            Round = savedRound;
            Phase = savedPhase;

            var passed = failures.Count == 0;
            Debug.Log($"QA_BALANCE_304 passed={passed} waves={counts[0]}-{counts[^1]} " +
                      $"threat={threat[0]:0}/{threat[24]:0}/{threat[44]:0}/{threat[49]:0} " +
                      $"step={minimumStep:0.000}-{maximumStep:0.000} hammer=" +
                      $"{hammer.MaxHealth:0}hp/{hammer.Range:0.00}r/{hammer.Armor:0}arm " +
                      $"summon={scheduled}/{summoned.Length}/maxFrame{maximumBuiltInFrame} " +
                      $"guide={GuideContentHeight(0):0} failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 124);
        }
    }
}
