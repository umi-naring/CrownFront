using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        [Serializable]
        private sealed class QaGoogleServicesConfig252
        {
            public string playGamesProjectId = string.Empty;
            public bool useTestAds = true;
            public string adMobAppId = string.Empty;
            public string interstitialAdUnitId = string.Empty;
        }

        private readonly struct QaTierStats252
        {
            public readonly float Mean;
            public readonly float P10;
            public readonly float P50;
            public readonly float P90;
            public readonly float Minimum;
            public readonly float Maximum;

            public QaTierStats252(float mean, float p10, float p50, float p90, float minimum, float maximum)
            {
                Mean = mean;
                P10 = p10;
                P50 = p50;
                P90 = p90;
                Minimum = minimum;
                Maximum = maximum;
            }
        }

        private IEnumerator QaAugmentTier252Routine(AugmentTier tier)
        {
            yield return null;
            const int seeds = 5;
            const int samplesPerSeed = 20000;
            var seedMeans = new float[seeds];
            var allPassed = true;
            QaTierStats252 last = default;
            var templates = GetAugmentPool(tier).Concat(FixedRecruitAugments(tier)).ToArray();
            var recruitCount = templates.Count(card => IsRecruitKey252(card.EffectKey));
            var scoredCards = templates.Where(card => !IsRecruitKey252(card.EffectKey)).ToArray();
            var unscored = scoredCards.Count(card => float.IsNaN(AugmentCoefficient252(card.EffectKey)));
            for (var seed = 0; seed < seeds; seed++)
            {
                last = SimulateAugmentTier252(tier, 25200 + (int)tier * 97 + seed * 7919,
                    samplesPerSeed, scoredCards);
                seedMeans[seed] = last.Mean;
                allPassed &= TierImpactWithinBand252(tier, last);
            }

            var monotonic = true;
            var tierMeans = new float[5];
            for (var index = 0; index < tierMeans.Length; index++)
            {
                var candidateTier = (AugmentTier)index;
                var cards = GetAugmentPool(candidateTier).Concat(FixedRecruitAugments(candidateTier))
                    .Where(card => !IsRecruitKey252(card.EffectKey)).ToArray();
                tierMeans[index] = SimulateAugmentTier252(candidateTier, 42000 + index * 101,
                    12000, cards).Mean;
                if (index > 0) monotonic &= tierMeans[index] >= tierMeans[index - 1] * 1.45f;
            }
            var seedSpread = seedMeans.Max() - seedMeans.Min();
            var stable = seedSpread <= .0025f;
            var passed = allPassed && unscored == 0 && monotonic && stable && scoredCards.Length >= 8;
            Debug.Log($"QA_AUGMENT_TIER_252 tier={tier} passed={passed} seeds={seeds} " +
                      $"samples={seeds * samplesPerSeed} cards={templates.Length}:{scoredCards.Length}:{recruitCount} " +
                      $"unscored={unscored} mean={seedMeans.Average():0.0000} " +
                      $"p10={last.P10:0.0000} p50={last.P50:0.0000} p90={last.P90:0.0000} " +
                      $"min={last.Minimum:0.0000} max={last.Maximum:0.0000} " +
                      $"stable={stable}:{seedSpread:0.0000} monotonic={monotonic}:" +
                      string.Join(":", tierMeans.Select(value => value.ToString("0.0000"))));
            Application.Quit(passed ? 0 : 102 + (int)tier);
        }

        private static QaTierStats252 SimulateAugmentTier252(AugmentTier tier, int seed, int samples,
            IReadOnlyList<AugmentTemplate> cards)
        {
            var random = new System.Random(seed);
            var values = new float[Mathf.Max(1, samples)];
            var power = TierPower(tier);
            for (var sample = 0; sample < values.Length; sample++)
            {
                var first = random.Next(cards.Count);
                var second = random.Next(cards.Count - 1);
                if (second >= first) second++;
                var third = random.Next(cards.Count - 2);
                var low = Mathf.Min(first, second);
                var high = Mathf.Max(first, second);
                if (third >= low) third++;
                if (third >= high) third++;
                values[sample] = Mathf.Max(
                    AugmentCoefficient252(cards[first].EffectKey),
                    Mathf.Max(AugmentCoefficient252(cards[second].EffectKey),
                        AugmentCoefficient252(cards[third].EffectKey))) * power;
            }
            Array.Sort(values);
            float Percentile(float p) => values[Mathf.Clamp(
                Mathf.RoundToInt((values.Length - 1) * p), 0, values.Length - 1)];
            return new QaTierStats252(values.Average(), Percentile(.10f), Percentile(.50f),
                Percentile(.90f), values[0], values[values.Length - 1]);
        }

        private static bool TierImpactWithinBand252(AugmentTier tier, QaTierStats252 result)
        {
            var band = tier switch
            {
                AugmentTier.Bronze => new Vector2(.013f, .027f),
                AugmentTier.Silver => new Vector2(.032f, .057f),
                AugmentTier.Gold => new Vector2(.060f, .106f),
                AugmentTier.Platinum => new Vector2(.096f, .165f),
                _ => new Vector2(.150f, .242f)
            };
            return result.Mean >= band.x && result.Mean <= band.y &&
                   result.Maximum <= band.y * 1.02f && result.Minimum >= band.x * .62f;
        }

        private static bool IsRecruitKey252(string key) =>
            key is "UnlockBombardier" or "UnlockMusketeer" or "UnlockLancer" or "UnlockDruid" or
                "UnlockOracle";

        // Coefficients are normalized first-pick combat/economy deltas. Multiplying them by the
        // exact live TierPower keeps the Monte Carlo coupled to shipped balance rather than to a
        // duplicate rarity table in the test.
        private static float AugmentCoefficient252(string key) => key switch
        {
            "TankDrill" => .095f, "MeleeEdge" => .10f, "RangedPractice" => .10f,
            "MageFocus" => .11f, "SupportTriage" => .084f, "AttackSpeed" => .05f,
            "HillGuard" => .072f, "Budget" => .064f, "TankAnchor" => .10f,
            "MeleeMarch" => .05f, "RangedSpotter" => .093f, "MageConserve" => .05f,

            "TankPlating" => .11f, "MeleeTempo" => .10f, "RangedRange" => .12f,
            "MageRadius" => .115f, "SupportCooldown" => .10f, "HillDamage" => .10f,
            "EnemySlow" => .088f, "FormationDiscipline" => .08f, "TankEscort" => .105f,
            "MeleePursuit" => .075f, "RangedFocus" => .12f, "MagePrimer" => .105f,
            "SupportRenewal" => .11f, "RoundRecovery" => .10f,

            "HillRange" => .13f, "TankThorns" => .115f, "MeleeShatter" => .145f,
            "RangedCrit" => .13f, "MageOverload" => .16f, "SupportAura" => .13f,
            "DoubleShot" => .12f, "ActiveVolley" => .12f, "TankRally" => .125f,
            "MeleeDuelist" => .10f, "RangedMark" => .125f, "MageBurn" => .14f,
            "SupportHaste" => .135f,

            "HillTempo" => .145f, "TankAegis" => .155f, "MeleeCleave" => .17f,
            "RangedRicochet" => .165f, "MageEcho" => .17f, "SupportSanctuary" => .16f,
            "ActiveFreeze" => .135f, "TankBulwark" => .15f, "MeleeRelentless" => .145f,
            "RangedPierce" => .16f, "MageResonance" => .175f, "SupportRescue" => .15f,

            "HillDominion" => .19f, "Meteor" => .16f, "Duplicate" => .145f,
            "TankUnbroken" => .185f, "MeleeExecution" => .18f, "RangedBarrage" => .19f,
            "MageCataclysm" => .205f, "SupportAscension" => .19f, "TankCitadel" => .18f,
            "MeleeOverdrive" => .185f, "RangedTempest" => .18f, "MageSingularity" => .19f,
            "SupportMiracle" => .185f,
            _ => float.NaN
        };

        private IEnumerator QaSpriteRange252Routine()
        {
            yield return null;
            Phase = GamePhase.Preparation;
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var profileFailures = new List<string>();
            var rangeFailures = new List<string>();
            var authoredCount = 0;
            var profiles = EnemyVariantCatalog.AllProfiles;
            foreach (var profile in profiles)
            {
                var actor = new GameObject($"QA 252 {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, 0, 120f, false, 0, profile.CombatClass, profile);
                var heights = new List<float>();
                var contacts = new List<float>();
                var scaleRatios = new List<float>();
                foreach (var direction in directions)
                {
                    heights.Add(actor.PreviewDirectionHeightForQa(direction));
                    contacts.Add(actor.PreviewGroundContactForQa(direction, .25f));
                    scaleRatios.Add(actor.CurrentVisualScaleHeightRatioForQa);
                }
                var minHeight = Mathf.Max(.001f, heights.Min());
                var heightRatio = heights.Max() / minHeight;
                var scaleRatio = scaleRatios.Max() / Mathf.Max(.001f, scaleRatios.Min());
                var contactSpread = contacts.Max() - contacts.Min();
                var stableSize = actor.HasAuthoredVariantDirectionalAnimationForQa
                    ? scaleRatio <= 1.05f
                    : heightRatio <= 1.12f;
                if (!stableSize || contactSpread > .22f || !actor.HasDirectionalBackPresentation)
                    profileFailures.Add($"{profile.Id}@bounds={heightRatio:0.00}/scale={scaleRatio:0.00}/" +
                                        $"ground={contactSpread:0.00}");

                if (actor.IsRanged)
                {
                    if (actor.AttackRange < 1.20f || actor.AttackRange > 3.20f)
                        rangeFailures.Add($"{profile.Id}=R{actor.AttackRange:0.00}");
                }
                else if (!actor.IsFlying &&
                         (actor.AttackRange > 1.10f || actor.AttackRange > actor.Radius + .58f))
                    rangeFailures.Add($"{profile.Id}=M{actor.AttackRange:0.00}/{actor.Radius:0.00}");

                if (profile.Id is "veil_binder" or "armor_render" or "silence_shroud")
                {
                    authoredCount++;
                    var stateFrames = Enumerable.Range(0, 3).All(state =>
                        actor.DistinctAnimationSpritesForQa(state) >= 5);
                    var atlasSafe = true;
                    foreach (var direction in directions)
                    for (var state = 0; state < 3; state++)
                    {
                        actor.PreviewMotionPoseForQa(direction, state, .48f);
                        atlasSafe &= actor.CurrentSpriteUsesStrictAtlasCellForQa &&
                                     actor.CurrentSpriteHasSafeCellMarginForQa;
                    }
                    if (!actor.HasCompleteDirectionalAnimationForQa || !stateFrames || !atlasSafe)
                        profileFailures.Add($"{profile.Id}@authored");
                }
                Destroy(actor.gameObject);
            }

            var playerRanges = true;
            foreach (var pair in definitions)
            {
                var role = RoleFor(pair.Key);
                if (pair.Key == UnitArchetype.Lancer)
                    // The chairman requested the lancer's oversized pseudo-ranged reach to be
                    // removed. It now remains a committed melee unit with only spear-tip reach.
                    playerRanges &= pair.Value.Range is >= 1.00f and <= 1.06f;
                else if (role is DefenderRole.Tank or DefenderRole.Melee)
                    playerRanges &= pair.Value.Range <= 1.25f;
                else if (role is DefenderRole.Ranged or DefenderRole.Mage)
                    playerRanges &= pair.Value.Range >= 1.65f;
            }
            var jellyMage = EnemyVariantCatalog.ForChapterStage(0, 2);
            var jellyMageBack = GetEnemyVariantBackSprite(jellyMage);
            var jellyMageIdentity = jellyMageBack != null && jellyMageBack.texture != null &&
                                    jellyMageBack.texture.name.Contains("enemy-jelly-mage-back-v1");
            var specialIdentity = EnemyVariantCatalog.SpecialProfiles.Select(profile => profile.Id).Distinct().Count() == 3 &&
                                  EnemyVariantCatalog.SpecialProfiles.Select(profile => profile.FamilyClass).Distinct().Count() == 3;
            var passed = profileFailures.Count == 0 && rangeFailures.Count == 0 && playerRanges &&
                         authoredCount == 3 && LoadedEnemyVariantDirectionalCountForQa == 3 &&
                         jellyMageIdentity && specialIdentity;
            Debug.Log($"QA_SPRITE_RANGE_252 passed={passed} profiles={profiles.Length} authored={authoredCount}:" +
                      $"{LoadedEnemyVariantDirectionalCountForQa} size={profileFailures.Count == 0} " +
                      $"range={rangeFailures.Count == 0}:{playerRanges} backIdentity={jellyMageIdentity} " +
                      $"specialIdentity={specialIdentity} profileFail={string.Join(",", profileFailures.Take(8))} " +
                      $"rangeFail={string.Join(",", rangeFailures.Take(8))}");
            Application.Quit(passed ? 0 : 108);
        }

        private IEnumerator QaSpecialEnemies252Routine()
        {
            yield return null;
            var scheduledCorrectly = true;
            var specialCounts = new Dictionary<string, int>();
            for (var round = 1; round <= 50; round++)
            {
                for (var member = 0; member < 120; member++)
                {
                    var profile = EnemyVariantCatalog.ForWaveMember(round, member);
                    if (!specialCounts.TryAdd(profile.Id, 1)) specialCounts[profile.Id]++;
                    if (profile.Id == "veil_binder") scheduledCorrectly &= round is >= 8 and <= 10;
                    if (profile.Id == "armor_render") scheduledCorrectly &= round is >= 28 and <= 30;
                    if (profile.Id == "silence_shroud") scheduledCorrectly &= round is >= 38 and <= 40;
                }
            }
            scheduledCorrectly &= specialCounts.ContainsKey("veil_binder") &&
                                  specialCounts.ContainsKey("armor_render") &&
                                  specialCounts.ContainsKey("silence_shroud");

            Round = 29;
            Phase = GamePhase.Battle;
            var tank = new GameObject("QA 252 Debuff Tank").AddComponent<PlayerUnit>();
            tank.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank], new Vector2(0f, -2f));
            var mage = new GameObject("QA 252 Debuff Mage").AddComponent<PlayerUnit>();
            mage.Initialize(this, UnitArchetype.AreaMage, definitions[UnitArchetype.AreaMage], new Vector2(1f, -2f));
            var singleMage = new GameObject("QA 252 Curse Mage").AddComponent<PlayerUnit>();
            singleMage.Initialize(this, UnitArchetype.SingleMage, definitions[UnitArchetype.SingleMage], new Vector2(2f, -2f));
            var melee = new GameObject("QA 252 Seal Immune Melee").AddComponent<PlayerUnit>();
            melee.Initialize(this, UnitArchetype.Melee, definitions[UnitArchetype.Melee], new Vector2(3f, -2f));

            EnemyUnit SpawnSpecial(string id, PlayerUnit target)
            {
                var profile = EnemyVariantCatalog.SpecialProfiles.Single(item => item.Id == id);
                var enemy = new GameObject($"QA 252 {id}").AddComponent<EnemyUnit>();
                // The four live defenders are capable of focus-firing this probe during its
                // authored wind-up. Use non-production QA health so we test the debuff contact,
                // not whether the probe survives an unrelated damage race.
                enemy.Initialize(this, 0, 100000f, false, 0, profile.CombatClass, profile);
                enemy.ForcePositionForQa(target.Position + Vector2.down * .42f);
                // Keep the battle state alive while the wind-up coroutine reaches its contact
                // frame. Without registration Update() sees an empty wave and completes the
                // round before any debuff can be applied.
                enemies.Add(enemy);
                return enemy;
            }

            var render = SpawnSpecial("armor_render", tank);
            var binder = SpawnSpecial("veil_binder", singleMage);
            var silence = SpawnSpecial("silence_shroud", mage);
            var armorBefore = tank.Armor;
            var resistanceBefore = singleMage.MagicResistance;
            var nonMageImmune = !melee.ApplyMagicSeal(3f);
            var castsStarted = render.ForceSpecialSkillForQa(tank) &&
                               binder.ForceSpecialSkillForQa(singleMage) &&
                               silence.ForceSpecialSkillForQa(mage);
            // Isolate contact-frame verification from the live navigation recovery system.
            // These intentionally invalid synthetic spawn points would otherwise be re-anchored
            // several world units apart while the authored wind-up is playing.
            var tankAnchor = new Vector2(-2.4f, -1.2f);
            var curseAnchor = new Vector2(0f, -1.2f);
            var silenceAnchor = new Vector2(2.4f, -1.2f);
            for (var elapsed = 0f; elapsed < 1.05f; elapsed += Time.deltaTime)
            {
                tank.transform.position = ActorWorldPosition(tankAnchor, true);
                render.transform.position = ActorWorldPosition(tankAnchor + Vector2.down * .42f, true);
                singleMage.transform.position = ActorWorldPosition(curseAnchor, true);
                binder.transform.position = ActorWorldPosition(curseAnchor + Vector2.down * .42f, true);
                mage.transform.position = ActorWorldPosition(silenceAnchor, true);
                silence.transform.position = ActorWorldPosition(silenceAnchor + Vector2.down * .42f, true);
                yield return null;
            }
            var armorLive = tank.ActiveArmorShredForQa > 0f && tank.Armor < armorBefore;
            var curseLive = singleMage.ActiveResistanceCurseForQa > 0f &&
                            singleMage.MagicResistance < resistanceBefore;
            var silenceLive = mage.IsMagicSealed && nonMageImmune;
            var identities = render.AppliesArmorShred && binder.AppliesResistanceCurse &&
                             silence.AppliesMagicSeal && !render.IsRanged && binder.IsRanged && silence.IsRanged;
            var passed = scheduledCorrectly && castsStarted && armorLive && curseLive && silenceLive && identities;
            Debug.Log($"QA_SPECIAL_ENEMIES_252 passed={passed} schedule={scheduledCorrectly} " +
                      $"casts={castsStarted} armor={armorLive}:{tank.ActiveArmorShredForQa:0.0} " +
                      $"curse={curseLive}:{singleMage.ActiveResistanceCurseForQa:0.0} " +
                      $"silence={silenceLive} identity={identities} " +
                      $"renderState={render.LastSpecialQaState} phase={Phase} tankAlive={tank.IsAlive} " +
                      $"counts={specialCounts.GetValueOrDefault("veil_binder")}:" +
                      $"{specialCounts.GetValueOrDefault("armor_render")}:" +
                      $"{specialCounts.GetValueOrDefault("silence_shroud")}");
            Application.Quit(passed ? 0 : 109);
        }

        private IEnumerator QaMonetization252Routine()
        {
            yield return null;
            var configAsset = Resources.Load<TextAsset>("crownfront-google-services");
            var config = configAsset != null
                ? JsonUtility.FromJson<QaGoogleServicesConfig252>(configAsset.text)
                : new QaGoogleServicesConfig252();
            var gamesConfigured = config != null && !string.IsNullOrWhiteSpace(config.playGamesProjectId) &&
                                  config.playGamesProjectId.Trim().Length >= 10 &&
                                  config.playGamesProjectId.Trim().All(char.IsDigit);
            var adConfigured = config != null &&
                               (!string.IsNullOrWhiteSpace(config.interstitialAdUnitId) || config.useTestAds) &&
                               !string.IsNullOrWhiteSpace(config.adMobAppId);
            var removeAdsProduct = monetization != null &&
                                   monetization.Products.Any(product =>
                                       product.Id == CrownfrontMonetization.RemoveAdsId &&
                                       product.Category == ShopCategory.Utility);
            var bridgeCatalogReady = monetization != null && monetization.Products.Count >= 3;
            // This is a release-readiness probe, not a desktop network mock. Missing real Play
            // Games credentials is reported as an explicit external setup blocker while the ad,
            // billing and product paths can still be structurally green.
            var structuralPassed = adConfigured && removeAdsProduct && bridgeCatalogReady;
            Debug.Log($"QA_MONETIZATION_252 structural={structuralPassed} gamesConfigured={gamesConfigured} " +
                      $"adConfigured={adConfigured} testAds={config?.useTestAds == true} " +
                      $"removeAds={removeAdsProduct} products={monetization?.Products.Count ?? 0} " +
                      $"billingEditorReady={monetization?.BillingReady == true}");
            Application.Quit(structuralPassed ? 0 : 110);
        }
    }
}
