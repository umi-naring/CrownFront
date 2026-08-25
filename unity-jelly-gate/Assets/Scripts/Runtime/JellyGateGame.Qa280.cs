using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private static readonly UnitArchetype[] BalanceRoster280 =
        {
            UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
            UnitArchetype.AreaMage, UnitArchetype.SingleMage, UnitArchetype.Bombardier,
            UnitArchetype.Lancer, UnitArchetype.Druid, UnitArchetype.Musketeer, UnitArchetype.Oracle
        };

        private IEnumerator QaUnitBalance280Routine()
        {
            yield return null;
            Phase = GamePhase.Preparation;
            augmentPower.Clear();
            var failures = new List<string>();
            var probes = new List<PlayerUnit>();
            var originalLanguage = GameLocalization.Current;

            foreach (var archetype in BalanceRoster280)
            {
                var probe = new GameObject($"QA 280 {archetype}").AddComponent<PlayerUnit>();
                probe.Initialize(this, archetype, definitions[archetype],
                    new Vector2(-4f + probes.Count * .72f, -6f));
                probes.Add(probe);
                var chance = GetCriticalChance(probe);
                var multiplier = GetCriticalDamageMultiplier(probe);
                if (Mathf.Abs(chance) > .0001f || Mathf.Abs(multiplier - 1f) > .0001f)
                    failures.Add($"critical-base-{archetype}:{chance:0.000}/{multiplier:0.000}");
            }

            var tank = probes.First(unit => unit.Archetype == UnitArchetype.Tank);
            GameLocalization.Current = GameLanguage.Korean;
            var koreanCriticalRemoved = !SelectedUnitCombatRangesAndCritical(tank).Contains("치명");
            GameLocalization.Current = GameLanguage.English;
            var englishCriticalRemoved = !SelectedUnitCombatRangesAndCritical(tank)
                .Contains("CRIT", StringComparison.OrdinalIgnoreCase);
            GameLocalization.Current = originalLanguage;
            if (!koreanCriticalRemoved || !englishCriticalRemoved) failures.Add("critical-ui-remains");

            var tankDefinition = definitions[UnitArchetype.Tank];
            var lancerDefinition = definitions[UnitArchetype.Lancer];
            var tankPhysicalEhp = tankDefinition.MaxHealth * (100f + tankDefinition.Armor) / 100f;
            var tankMagicEhp = tankDefinition.MaxHealth * (100f + tankDefinition.MagicResistance) / 100f;
            var oldPhysicalEhp = 240f * 1.56f;
            var oldMagicEhp = 240f * 1.40f;
            if (tankPhysicalEhp < oldPhysicalEhp * 1.10f || tankMagicEhp < oldMagicEhp * 1.10f ||
                tankDefinition.SkillCooldown > 7.31f)
                failures.Add($"tank-base:{tankPhysicalEhp:0.0}/{tankMagicEhp:0.0}/{tankDefinition.SkillCooldown:0.00}");

            var plainTank = new GameObject("QA 280 Plain Tank").AddComponent<PlayerUnit>();
            plainTank.Initialize(this, UnitArchetype.Tank, tankDefinition, new Vector2(-.5f, -5f));
            var bracedTank = new GameObject("QA 280 Braced Tank").AddComponent<PlayerUnit>();
            bracedTank.Initialize(this, UnitArchetype.Tank, tankDefinition, new Vector2(.5f, -5f));
            bracedTank.ApplyDefensiveStance(4.2f, .38f, 28f, 24f);
            var plainBefore = plainTank.Health;
            var bracedBefore = bracedTank.Health;
            plainTank.TakeDamage(140f, DamageType.Physical);
            bracedTank.TakeDamage(140f, DamageType.Physical);
            var plainLoss = plainBefore - plainTank.Health;
            var bracedLoss = bracedBefore - bracedTank.Health;
            if (bracedLoss >= plainLoss * .61f)
                failures.Add($"tank-stance:{bracedLoss:0.0}/{plainLoss:0.0}");

            var profiles = new[] { 0f, 35f, 70f, 110f };
            foreach (var archetype in BalanceRoster280)
            {
                var definition = definitions[archetype];
                var profileDps = profiles.Select(defense =>
                    SimulatedBasicDps280(archetype, definition, defense)).ToArray();
                Debug.Log($"QA_BALANCE_280_UNIT type={archetype} cost={definition.Cost} " +
                          $"hp={definition.MaxHealth:0} armor={definition.Armor:0} resist={definition.MagicResistance:0} " +
                          $"dps={string.Join("/", profileDps.Select(value => value.ToString("0.00")))}");
                if (profileDps.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value <= 0f))
                    failures.Add($"invalid-dps-{archetype}");
            }

            var roleShape = tankDefinition.MaxHealth >= 260f && tankDefinition.Armor >= 60f &&
                            tankDefinition.MagicResistance >= 44f &&
                            definitions[UnitArchetype.Archer].Range > definitions[UnitArchetype.SingleMage].Range &&
                            definitions[UnitArchetype.SingleMage].Range > definitions[UnitArchetype.AreaMage].Range &&
                            definitions[UnitArchetype.AreaMage].SplashRadius >= 1.4f &&
                            definitions[UnitArchetype.SingleMage].MagicPower >= 70f &&
                            lancerDefinition.AttackPower <= 44f && lancerDefinition.AttackDelay >= .64f;
            if (!roleShape) failures.Add("role-shape");

            foreach (var probe in probes) Destroy(probe.gameObject);
            Destroy(plainTank.gameObject);
            Destroy(bracedTank.gameObject);
            var passed = failures.Count == 0;
            Debug.Log($"QA_UNIT_BALANCE_280 passed={passed} roster={BalanceRoster280.Length} " +
                      $"profiles={BalanceRoster280.Length * profiles.Length} critical=removed " +
                      $"tankEhp={tankPhysicalEhp:0.0}/{tankMagicEhp:0.0} " +
                      $"stance={bracedLoss:0.0}/{plainLoss:0.0} failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 100);
        }

        private static float SimulatedBasicDps280(UnitArchetype archetype, UnitDefinition definition,
            float targetDefense)
        {
            var magicBasic = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle;
            var multiplier = BasicAttackMultiplierFor(archetype);
            var baseDamage = magicBasic
                ? definition.MagicPower * multiplier
                : definition.AttackPower;
            var penetration = magicBasic ? definition.MagicPenetration : definition.PhysicalPenetration;
            var damageType = magicBasic ? DamageType.Magic : DamageType.Physical;
            return CombatMath.MitigatedDamage(baseDamage, damageType,
                targetDefense, targetDefense, penetration, penetration) / definition.AttackDelay;
        }
    }
}
