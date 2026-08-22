using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRelease303Routine()
        {
            yield return null;
            var failures = new List<string>();

            var returnTicket = economy.Definition(TacticalItemId.ReviveTicket);
            if (returnTicket == null || returnTicket.GemPrice != 11)
                failures.Add($"return-ticket:{returnTicket?.GemPrice ?? -1}");
            var fieldAid = economy.Definition(TacticalItemId.FieldAid);
            if (fieldAid == null || !fieldAid.KoreanDescription.Contains("60%") ||
                !fieldAid.EnglishDescription.Contains("60%"))
                failures.Add("field-aid-copy");

            var aidProbeObject = new GameObject("QA 303 Field Aid Probe");
            var aidProbe = aidProbeObject.AddComponent<PlayerUnit>();
            var tank = definitions[UnitArchetype.Tank];
            aidProbe.Initialize(this, UnitArchetype.Tank, tank,
                NearestWalkable(new Vector2(0f, -5.5f), tank.Radius));
            units.Add(aidProbe);
            Phase = GamePhase.Preparation;
            fieldAidUsesThisRun = 0;
            var aidInventoryBefore = economy.Count(TacticalItemId.FieldAid);
            aidProbe.TakeDamage(aidProbe.MaxHealth * .70f, DamageType.Pure);
            var healthBeforeAid = aidProbe.Health;
            economy.GrantPurchasedItem(TacticalItemId.FieldAid);
            var aidUsed = TryUseFieldAid();
            var expectedAidHealth = Mathf.Min(aidProbe.MaxHealth,
                healthBeforeAid + aidProbe.MaxHealth * .60f);
            if (!aidUsed || Mathf.Abs(aidProbe.Health - expectedAidHealth) > .05f ||
                economy.Count(TacticalItemId.FieldAid) != aidInventoryBefore)
                failures.Add($"field-aid-runtime:{aidUsed}:{healthBeforeAid:0.0}>{aidProbe.Health:0.0}/{expectedAidHealth:0.0}");

            var abilityCornersClear = true;
            var abilityCentersOpaque = true;
            for (var index = 0; index < 20; index++)
            {
                var icon = SelectedAbilityIconTexture(index);
                abilityCornersClear &= icon != null && selectedAbilityMaskPixelsValid;
                abilityCentersOpaque &= icon != null && selectedAbilityMaskPixelsValid;
            }
            if (!abilityCornersClear || !abilityCentersOpaque || selectedAbilityIconTextures.Count != 20 ||
                selectedAbilityMaskVerifiedCount != 20)
                failures.Add($"ability-mask:{abilityCornersClear}/{abilityCentersOpaque}/" +
                             $"{selectedAbilityIconTextures.Count}/{selectedAbilityMaskVerifiedCount}");

            selectedUnits.Clear();
            selectedUnits.Add(aidProbe);
            var statModels = SelectedUnitStatCellModels(aidProbe);
            if (statModels.Length != 6 || statModels.Select(model => model.Icon).Distinct().Count() != 6 ||
                statModels.Any(model => string.IsNullOrWhiteSpace(model.Value) ||
                                        string.IsNullOrWhiteSpace(model.Label)))
                failures.Add($"stat-icons:{statModels.Length}/{statModels.Select(model => model.Icon).Distinct().Count()}");
            var statAtlasReady = selectedStatIconAtlasTexture != null && selectedStatIconAtlasTexture.isReadable &&
                                 selectedStatIconAtlasTexture.width == 1024 &&
                                 selectedStatIconAtlasTexture.height == 512;
            var statAtlasCellsOpaque = statAtlasReady;
            var statAtlasCornersClear = statAtlasReady;
            if (statAtlasReady)
            {
                for (var index = 0; index < 8; index++)
                {
                    var column = index % 4;
                    var rowFromTop = index / 4;
                    var centerU = (column + .5f) / 4f;
                    var centerV = 1f - (rowFromTop + .5f) / 2f;
                    statAtlasCellsOpaque &= selectedStatIconAtlasTexture.GetPixelBilinear(centerU, centerV).a > .9f;
                    var leftU = (column + .01f) / 4f;
                    var rightU = (column + .99f) / 4f;
                    var topV = 1f - (rowFromTop + .01f) / 2f;
                    var bottomV = 1f - (rowFromTop + .99f) / 2f;
                    statAtlasCornersClear &= selectedStatIconAtlasTexture.GetPixelBilinear(leftU, topV).a < .05f &&
                                             selectedStatIconAtlasTexture.GetPixelBilinear(rightU, topV).a < .05f &&
                                             selectedStatIconAtlasTexture.GetPixelBilinear(leftU, bottomV).a < .05f &&
                                             selectedStatIconAtlasTexture.GetPixelBilinear(rightU, bottomV).a < .05f;
                }
            }
            if (!statAtlasReady || !statAtlasCellsOpaque || !statAtlasCornersClear)
                failures.Add($"stat-atlas:{statAtlasReady}/{statAtlasCellsOpaque}/{statAtlasCornersClear}");

            var roster = Enum.GetValues(typeof(UnitArchetype)).Cast<UnitArchetype>()
                .Where(archetype => archetype != UnitArchetype.None).ToArray();
            var defenseProfiles = new[] { 0f, 35f, 70f, 110f };
            var throughput = new Dictionary<UnitArchetype, float[]>();
            foreach (var archetype in roster)
            {
                var definition = definitions[archetype];
                throughput[archetype] = defenseProfiles
                    .Select(defense => Qa303CombatThroughput(archetype, definition, defense)).ToArray();
                if (definition.Cost <= 0 || throughput[archetype].Any(value => value <= 0f ||
                        float.IsNaN(value) || float.IsInfinity(value)))
                    failures.Add($"balance-invalid:{archetype}");
            }

            var melee35 = throughput[UnitArchetype.Melee][1];
            var lancer35 = throughput[UnitArchetype.Lancer][1];
            if (lancer35 < melee35 * 1.08f || lancer35 > melee35 * 1.28f)
                failures.Add($"lancer-efficiency:{lancer35 / Mathf.Max(.01f, melee35):0.00}");
            if (definitions[UnitArchetype.Tank].MaxHealth < 260f ||
                definitions[UnitArchetype.Tank].Armor < 60f)
                failures.Add("tank-durability");
            if (definitions[UnitArchetype.AreaMage].SplashRadius < 1.45f ||
                definitions[UnitArchetype.AreaMage].MagicPower < 44f)
                failures.Add("area-mage-role");
            if (definitions[UnitArchetype.Bombardier].MaxHealth < 82f ||
                definitions[UnitArchetype.Bombardier].AttackPower < 35f ||
                definitions[UnitArchetype.Bombardier].MagicPower < 24f)
                failures.Add("bombardier-price-value");
            if (definitions[UnitArchetype.Druid].MaxHealth < 74f ||
                definitions[UnitArchetype.Druid].MagicPower < 55f)
                failures.Add("druid-price-value");
            if (definitions[UnitArchetype.Musketeer].AttackPower < 43f ||
                definitions[UnitArchetype.Musketeer].Range < 4.25f)
                failures.Add("musketeer-price-value");
            if (definitions[UnitArchetype.Oracle].Cost != 8 ||
                definitions[UnitArchetype.Oracle].MaxHealth < 80f ||
                definitions[UnitArchetype.Oracle].MagicPower < 68f ||
                definitions[UnitArchetype.Oracle].SkillCooldown > 5.9f)
                failures.Add("oracle-price-value");

            selectedUnits.Clear();
            units.Remove(aidProbe);
            Destroy(aidProbeObject);
            var balanceSignature = string.Join("|", roster.Select(archetype =>
                $"{archetype}:{definitions[archetype].Cost}c/{throughput[archetype][1]:0.0}"));
            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_303 passed={passed} returnTicket=11gem fieldAid=60% " +
                      $"icons={selectedAbilityIconTextures.Count}/20 verified={selectedAbilityMaskVerifiedCount}/20 " +
                      $"transparent={abilityCornersClear} " +
                      $"stats={statModels.Length}/6 atlas=8/8:{statAtlasReady}/{statAtlasCornersClear} " +
                      $"balance={balanceSignature} failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 123);
        }

        private static float Qa303CombatThroughput(UnitArchetype archetype, UnitDefinition definition,
            float defense)
        {
            const float levelOnePower = .55f;
            const float levelOneDelay = 1.18f;
            var attack = definition.AttackPower * levelOnePower;
            var magic = definition.MagicPower * levelOnePower;
            var basicType = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle ? DamageType.Magic : DamageType.Physical;
            var basicCoefficient = archetype switch
            {
                UnitArchetype.AreaMage => .26f,
                UnitArchetype.SingleMage => .55f,
                UnitArchetype.Druid => .34f,
                UnitArchetype.Oracle => .34f,
                _ => 1f
            };
            var basic = CombatMath.MitigatedDamage(
                (basicType == DamageType.Magic ? magic : attack) * basicCoefficient, basicType,
                defense, defense, definition.PhysicalPenetration, definition.MagicPenetration) /
                Mathf.Max(.1f, definition.AttackDelay * levelOneDelay);

            float skillPhysical = 0f;
            float skillMagic = 0f;
            switch (archetype)
            {
                case UnitArchetype.Melee: skillPhysical = attack * 1.78f + magic * .20f; break;
                case UnitArchetype.Archer: skillPhysical = attack * 1.65f + magic * .35f; break;
                case UnitArchetype.AreaMage: skillMagic = magic * 2.28f + attack * .20f; break;
                case UnitArchetype.SingleMage: skillMagic = magic * 2.82f + attack * .20f; break;
                case UnitArchetype.Bombardier:
                    skillPhysical = attack * .82f;
                    skillMagic = magic * 1.46f;
                    break;
                case UnitArchetype.Lancer: skillPhysical = attack * 2.04f + magic * .18f; break;
                case UnitArchetype.Druid: skillMagic = magic * 2.12f + attack * .18f; break;
                case UnitArchetype.Musketeer: skillPhysical = attack * 2.16f + magic * .20f; break;
                case UnitArchetype.Oracle: skillMagic = magic * 2.18f + attack * .15f; break;
            }
            var skill = CombatMath.MitigatedDamage(skillPhysical, DamageType.Physical, defense, defense,
                            definition.PhysicalPenetration, definition.MagicPenetration) +
                        CombatMath.MitigatedDamage(skillMagic, DamageType.Magic, defense, defense,
                            definition.PhysicalPenetration, definition.MagicPenetration);
            return basic + skill / Mathf.Max(.1f, definition.SkillCooldown * levelOneDelay);
        }

        private IEnumerator QaRelease303CaptureRoutine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;
            yield return new WaitForEndOfFrame();
            showMainMenu = false;
            showFormationPanel = false;
            showSettings = showMissionPanel = showShopPanel = showSkinPanel = showGuidePanel = false;
            Phase = GamePhase.Preparation;
            cameraMapCenter = new Vector2(0f, -4.8f);
            cameraZoom = cameraZoomTarget = 4.7f;
            ApplyCameraPose();
            var archer = definitions[UnitArchetype.Archer];
            var probeObject = new GameObject("QA 303 Ability HUD Probe");
            var probe = probeObject.AddComponent<PlayerUnit>();
            probe.Initialize(this, UnitArchetype.Archer, archer,
                NearestWalkable(new Vector2(0f, -4.8f), archer.Radius));
            probe.AddExperience(10000f);
            units.Add(probe);
            selectedUnits.Clear();
            selectedUnits.Add(probe);
            yield return new WaitForSecondsRealtime(.2f);
            yield return CaptureFullFrameRoutine("Crownfront-code11-ability-hud.ppm");

            selectedUnits.Clear();
            units.Remove(probe);
            Destroy(probeObject);
            var mageDefinition = definitions[UnitArchetype.SingleMage];
            var mageObject = new GameObject("QA 303 Magic Stat HUD Probe");
            var mage = mageObject.AddComponent<PlayerUnit>();
            mage.Initialize(this, UnitArchetype.SingleMage, mageDefinition,
                NearestWalkable(new Vector2(0f, -4.8f), mageDefinition.Radius));
            units.Add(mage);
            selectedUnits.Add(mage);
            yield return new WaitForSecondsRealtime(.16f);
            yield return CaptureFullFrameRoutine("Crownfront-code11-stat-hud-magic.ppm");

            selectedUnits.Clear();
            units.Remove(mage);
            Destroy(mageObject);
            showMainMenu = true;
            sortieGateTransition = true;
            sortieGateTransitionStartedAt = Time.unscaledTime - .42f;
            yield return new WaitForSecondsRealtime(.08f);
            yield return CaptureFullFrameRoutine("Crownfront-code11-sortie-gate.ppm");
            Application.Quit(0);
        }
    }
}
