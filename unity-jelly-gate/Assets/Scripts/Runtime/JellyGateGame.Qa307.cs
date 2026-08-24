using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaBattlefieldSprite307Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            Time.timeScale = 1f;
            var failures = new List<string>();

            ResetCameraToFullBattlefield();
            var aspect = Mathf.Max(.32f, Screen.width / (float)Mathf.Max(1, Screen.height));
            var cameraFull = gameCamera != null &&
                             Mathf.Abs(cameraZoom - cameraZoomMax) <= .001f &&
                             Mathf.Abs(cameraZoomTarget - cameraZoomMax) <= .001f &&
                             cameraMapCenter.sqrMagnitude <= .0001f &&
                             gameCamera.orthographicSize + .001f >= MapHeight * .5f &&
                             gameCamera.orthographicSize * aspect + .001f >= PlayableHalfWidth + .5f;
            if (!cameraFull)
                failures.Add($"camera={cameraZoom:0.000}/{cameraZoomTarget:0.000}/{cameraZoomMax:0.000}:" +
                             $"center={cameraMapCenter}:aspect={aspect:0.000}");

            var roster = new[]
            {
                UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
                UnitArchetype.AreaMage, UnitArchetype.SingleMage, UnitArchetype.Bombardier,
                UnitArchetype.Lancer, UnitArchetype.Druid, UnitArchetype.Musketeer,
                UnitArchetype.Oracle
            };
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var phases = Enumerable.Range(0, 16).Select(index => (index + .15f) / 16f).ToArray();
            var presentationCount = 0;
            var poseCount = 0;
            var uniqueSprites = new HashSet<Sprite>();
            var singleMageAttackSprites = new HashSet<int>();

            foreach (var archetype in roster)
            for (var variant = 0; variant <= 2; variant++)
            foreach (var hero in new[] { false, true })
            {
                DirectionalAnimationSet presentation;
                if (variant == 0)
                {
                    var source = hero ? heroDirectionalAnimations : unitDirectionalAnimations;
                    source.TryGetValue(archetype, out presentation);
                }
                else presentation = GetAuthoredSkinAnimation(archetype, variant, hero);
                if (presentation == null)
                {
                    failures.Add($"missing-{archetype}-v{variant}-h{hero}");
                    continue;
                }

                presentationCount++;
                var actor = new GameObject($"QA 307 {archetype} v{variant} h{hero}")
                    .AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definitions[archetype],
                    NearestWalkable(new Vector2(0f, -5.4f), .18f));
                if (hero) actor.AddExperience(99999f);
                actor.UseDirectionalAnimationForQa(presentation);
                var heights = new List<float>();
                var actorSafe = actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                actor.HasCompleteDirectionalAnimation;

                foreach (var direction in directions)
                for (var state = 0; state < 4; state++)
                foreach (var phase in phases)
                {
                    actor.PreviewMotionPoseForQa(direction, state, phase);
                    poseCount++;
                    var sprite = actor.PortraitSprite;
                    if (sprite == null)
                    {
                        actorSafe = false;
                        continue;
                    }
                    uniqueSprites.Add(sprite);
                    heights.Add(actor.VisualWorldHeightForQa);
                    var margins = PlayerUnit.SpriteOpaqueMarginsForQa(sprite);
                    var audit = SpriteFrameIsolationRegistry.For(sprite);
                    actorSafe &= actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                 actor.CurrentSpriteMetricsReadyForQa &&
                                 margins.x >= 8f && margins.y >= 8f &&
                                 margins.z >= 8f && margins.w >= 8f &&
                                 SpriteFrameIsolationRegistry.HasAudit(sprite) &&
                                 audit.RemainingForeignComponents == 0;
                    if (archetype == UnitArchetype.SingleMage && variant == 0 && state > 0)
                        singleMageAttackSprites.Add(actor.CurrentSpriteIdForQa);
                }

                var heightRatio = heights.Count == 0 ? float.PositiveInfinity :
                    heights.Max() / Mathf.Max(.001f, heights.Min());
                actorSafe &= heightRatio <= 1.13f;
                if (!actorSafe)
                    failures.Add($"pose-{archetype}-v{variant}-h{hero}:height={heightRatio:0.000}:" +
                                 actor.CurrentFrameTextureName);
                Destroy(actor.gameObject);
            }

            if (presentationCount != 60) failures.Add($"presentations={presentationCount}/60");
            if (singleMageAttackSprites.Count < 6)
                failures.Add($"single-mage-action-coverage={singleMageAttackSprites.Count}");

            var passed = failures.Count == 0;
            Debug.Log($"QA_BATTLEFIELD_SPRITE_307 passed={passed} camera={cameraFull}:" +
                      $"{cameraZoom:0.000}/{cameraZoomMax:0.000} presentations={presentationCount}/60 " +
                      $"poses={poseCount} sprites={uniqueSprites.Count} " +
                      $"singleMageAction={singleMageAttackSprites.Count} " +
                      $"fail={string.Join(",", failures.Take(40))}");
            Application.Quit(passed ? 0 : 124);
        }
    }
}
