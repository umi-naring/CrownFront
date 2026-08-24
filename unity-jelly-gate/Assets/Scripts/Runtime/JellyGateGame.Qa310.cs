using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaSpawnPool310Routine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;

            var freshOnly = CrownfrontEconomy.ShouldGrantFirstProfileTrialForQa(false,
                                false, false, false) &&
                            !CrownfrontEconomy.ShouldGrantFirstProfileTrialForQa(true,
                                false, false, false) &&
                            !CrownfrontEconomy.ShouldGrantFirstProfileTrialForQa(false,
                                true, false, false) &&
                            CrownfrontEconomy.FirstProfileTrialAmountForQa == 3;
            var everyTrialItem = economy.Catalog.Count == 11;

            var firstProfile = MainMenuRepresentativeProfileForRound(1);
            var bossProfile = MainMenuRepresentativeProfileForRound(25);
            var portraitsMatchRound = firstProfile == EnemyVariantCatalog.ForWaveMember(1, 0) &&
                                      bossProfile == EnemyVariantCatalog.ForChapterStage(4, 4) &&
                                      GetEnemyVariantSprite(firstProfile, false) != null &&
                                      GetEnemyVariantSprite(bossProfile, true) != null;

            var chips = ShopPriceChipRects(new Rect(0f, 0f, 132f, 22f), false);
            var priceRelationClear = chips.gold.xMax < chips.relation.x &&
                                     chips.relation.xMax < chips.gems.x &&
                                     chips.gems.xMax <= 132.01f &&
                                     chips.gold.width >= 48f && chips.gems.width >= 34f;

            Round = 50;
            Phase = GamePhase.Preparation;
            BeginEnemyPoolPrewarm(Round);
            var prewarmDeadline = Time.realtimeSinceStartup + 8f;
            while (enemyPoolPrewarmRoutine != null && Time.realtimeSinceStartup < prewarmDeadline)
                yield return null;
            var prewarmCompleted = enemyPoolPrewarmRoutine == null;
            var oneCreationPerFrame = enemyPoolMaxCreatedInFrame <= 1;

            var plan = BuildEnemyPoolWarmPlan(Round);
            var borrowed = new List<EnemyUnit>();
            var createdBeforeBorrow = enemyPoolCreatedCount;
            var reusedBeforeBorrow = enemyPoolReuseCount;
            var watch = Stopwatch.StartNew();
            foreach (var request in plan.Values)
            for (var i = 0; i < request.Count; i++)
            {
                var enemyClass = request.Profile?.CombatClass ?? EnemyClass.Melee;
                var enemy = AcquireEnemy(request.Profile, request.Boss, enemyClass);
                enemy.Initialize(this, i, 500f, request.Boss, 0, enemyClass, request.Profile, false);
                borrowed.Add(enemy);
            }
            watch.Stop();
            var borrowCreated = enemyPoolCreatedCount - createdBeforeBorrow;
            var borrowReused = enemyPoolReuseCount - reusedBeforeBorrow;
            var burstReuseComplete = borrowCreated == 0 && borrowReused == borrowed.Count && borrowed.Count >= 80;
            var reuseBudgetHealthy = watch.ElapsedMilliseconds < 300;
            foreach (var enemy in borrowed) RecycleEnemy(enemy);

            var released = enemyPoolReleasedCount >= borrowed.Count;
            var passed = freshOnly && everyTrialItem && portraitsMatchRound && priceRelationClear &&
                         prewarmCompleted && oneCreationPerFrame && burstReuseComplete &&
                         reuseBudgetHealthy && released;
            UnityEngine.Debug.Log($"QA_SPAWN_POOL_310 passed={passed} starter={freshOnly}:{everyTrialItem}:x" +
                                  $"{CrownfrontEconomy.FirstProfileTrialAmountForQa} portrait={portraitsMatchRound}:" +
                                  $"{firstProfile?.Id}/{bossProfile?.Id} price={priceRelationClear}:" +
                                  $"{chips.gold}/{chips.relation}/{chips.gems} prewarm={prewarmCompleted}:" +
                                  $"maxCreateFrame={enemyPoolMaxCreatedInFrame} burst={borrowed.Count}:" +
                                  $"created={borrowCreated}:reused={borrowReused}:ms={watch.ElapsedMilliseconds} " +
                                  $"released={enemyPoolReleasedCount}");
            Application.Quit(passed ? 0 : 130);
        }
    }
}
