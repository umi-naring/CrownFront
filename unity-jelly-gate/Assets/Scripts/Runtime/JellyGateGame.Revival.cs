using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private const string RevivalSnapshotKeyPrefix = "Crownfront.RevivalSnapshot.v1.";
        private const int RevivalSnapshotCount = 3;
        private bool revivalUsedThisRun;
        private bool emergencyRevivePaymentConfirmed;
        private int selectedReviveSnapshotIndex;
        private bool finalDefeatAdRequested;

        private void CaptureRevivalSnapshot()
        {
            if (units.Count == 0 || Phase != GamePhase.Preparation) return;
            var snapshot = CaptureRunCheckpoint(GamePhase.Preparation);
            if (snapshot.units == null || snapshot.units.Count == 0) return;
            var latest = ReadRevivalSnapshot(0);
            if (latest != null && latest.round == snapshot.round) return;
            for (var i = RevivalSnapshotCount - 1; i > 0; i--)
            {
                var previous = PlayerPrefs.GetString(RevivalSnapshotKeyPrefix + (i - 1), string.Empty);
                if (string.IsNullOrWhiteSpace(previous)) PlayerPrefs.DeleteKey(RevivalSnapshotKeyPrefix + i);
                else PlayerPrefs.SetString(RevivalSnapshotKeyPrefix + i, previous);
            }
            PlayerPrefs.SetString(RevivalSnapshotKeyPrefix + 0, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        private RunCheckpointData ReadRevivalSnapshot(int index)
        {
            if (index < 0 || index >= RevivalSnapshotCount) return null;
            var key = RevivalSnapshotKeyPrefix + index;
            var json = PlayerPrefs.GetString(key, string.Empty);
            // v1.00 prerelease builds accidentally wrote the newest slot to the bare prefix.
            // Migrate it once so an interrupted run remains recoverable after updating.
            if (index == 0 && string.IsNullOrWhiteSpace(json))
            {
                var legacyJson = PlayerPrefs.GetString(RevivalSnapshotKeyPrefix, string.Empty);
                if (!string.IsNullOrWhiteSpace(legacyJson))
                {
                    json = legacyJson;
                    PlayerPrefs.SetString(key, legacyJson);
                    PlayerPrefs.DeleteKey(RevivalSnapshotKeyPrefix);
                    PlayerPrefs.Save();
                }
            }
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<RunCheckpointData>(json);
                return data != null && data.version == RunCheckpointVersion && data.units != null &&
                       data.units.Count > 0 ? data : null;
            }
            catch { return null; }
        }

        private void ClearRevivalSnapshots()
        {
            for (var i = 0; i < RevivalSnapshotCount; i++)
                PlayerPrefs.DeleteKey(RevivalSnapshotKeyPrefix + i);
            PlayerPrefs.DeleteKey(RevivalSnapshotKeyPrefix);
            PlayerPrefs.Save();
        }

        private string RevivalRosterSummary(RunCheckpointData data)
        {
            if (data?.units == null || data.units.Count == 0) return L("편성 없음", "NO FORMATION");
            return string.Join(" · ", data.units
                .GroupBy(unit => new { unit.archetype, unit.level })
                .OrderBy(group => group.Key.archetype).ThenBy(group => group.Key.level)
                .Select(group =>
                {
                    var archetype = (UnitArchetype)Mathf.Clamp(group.Key.archetype,
                        (int)UnitArchetype.Tank, (int)UnitArchetype.Oracle);
                    var name = definitions.TryGetValue(archetype, out var definition)
                        ? definition.Name
                        : archetype.ToString();
                    return $"{name} Lv.{group.Key.level}×{group.Count()}";
                }));
        }

        private void DrawRevivalDefeatOverlay()
        {
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(.01f, .018f, .045f, .92f));
            var safe = SafeGuiRect;
            var width = Mathf.Min(434f, safe.width * .96f);
            var height = Mathf.Min(672f, safe.height * .92f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f, width, height);
            DrawOrnatePanel(panel, new Color(.026f, .04f, .075f, .998f), new Color(.92f, .38f, .25f), 4f);
            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 42f),
                revivalUsedThisRun ? L("최종 패배", "FINAL DEFEAT") : L("성문이 파괴됐습니다", "THE GATE HAS FALLEN"),
                overlayTitleStyle, 16);

            if (!revivalUsedThisRun)
            {
                DrawFittedLabel(new Rect(panel.x + 22f, panel.y + 57f, panel.width - 44f, 40f),
                    L("안전 편성을 선택하면 해당 라운드 시작 직전으로 복귀합니다.",
                        "SELECT A SAFE FORMATION TO RETURN TO ITS PRE-WAVE STATE."),
                    new GUIStyle(statStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true }, 9);
                var validCount = 0;
                for (var i = 0; i < RevivalSnapshotCount; i++)
                {
                    var data = ReadRevivalSnapshot(i);
                    if (data == null) continue;
                    validCount++;
                    var row = new Rect(panel.x + 18f, panel.y + 104f + i * 104f, panel.width - 36f, 94f);
                    var selected = selectedReviveSnapshotIndex == i;
                    DrawOrnatePanel(row, selected ? new Color(.055f, .13f, .115f, .995f) :
                        new Color(.03f, .06f, .105f, .995f), selected ? new Color(.42f, 1f, .75f) :
                        new Color(.42f, .64f, .86f), 2f);
                    if (GUI.Button(row, GUIContent.none, GUIStyle.none)) selectedReviveSnapshotIndex = i;
                    DrawFittedLabel(new Rect(row.x + 11f, row.y + 7f, row.width - 22f, 24f),
                        L($"{i + 1}라운드 전 · ROUND {data.round}",
                            $"{i + 1} ROUND{(i == 0 ? "" : "S")} BACK · ROUND {data.round}"),
                        new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft,
                            fontStyle = FontStyle.Bold }, 10);
                    DrawFittedLabel(new Rect(row.x + 11f, row.y + 33f, row.width - 22f, 52f),
                        RevivalRosterSummary(data), new GUIStyle(statStyle)
                        {
                            alignment = TextAnchor.UpperLeft,
                            wordWrap = true
                        }, 8);
                }
                if (validCount == 0)
                    DrawFittedLabel(new Rect(panel.x + 24f, panel.y + 126f, panel.width - 48f, 80f),
                        L("복구 가능한 편성이 없습니다.", "NO SAFE FORMATION IS AVAILABLE."), centeredStyle, 12);

                var actionY = panel.y + 426f;
                var tickets = economy?.Count(TacticalItemId.ReviveTicket) ?? 0;
                if (tickets > 0)
                {
                    if (DrawPremiumButton(new Rect(panel.x + 20f, actionY, panel.width - 40f, 48f),
                            L($"전선 복귀권 사용 · 보유 {tickets}", $"USE RETURN TOKEN · OWNED {tickets}"),
                            new Color(.035f, .14f, .12f, .995f), new Color(.42f, 1f, .78f), validCount > 0))
                        TryExecuteSelectedRevive();
                }
                else
                {
                    var third = (panel.width - 56f) / 3f;
                    if (DrawPremiumButton(new Rect(panel.x + 16f, actionY, third, 52f),
                            L("골드 250", "250 GOLD"), new Color(.16f, .1f, .025f, .995f),
                            new Color(1f, .78f, .28f), validCount > 0 && economy != null && economy.Gold >= 250))
                    {
                        if (economy.TrySpend(ShopCurrency.Gold, 250))
                        {
                            emergencyRevivePaymentConfirmed = true;
                            TryExecuteSelectedRevive();
                        }
                    }
                    if (DrawPremiumButton(new Rect(panel.x + 20f + third, actionY, third, 52f),
                            L("보석 11", "11 GEMS"), new Color(.025f, .095f, .14f, .995f),
                            new Color(.42f, .84f, 1f), validCount > 0 && economy != null && economy.Gems >= 11))
                    {
                        if (economy.TrySpend(ShopCurrency.Gems, 11))
                        {
                            emergencyRevivePaymentConfirmed = true;
                            TryExecuteSelectedRevive();
                        }
                    }
                    if (DrawPremiumButton(new Rect(panel.x + 24f + third * 2f, actionY, third, 52f),
                            L("긴급 구매 · ₩150", "EMERGENCY · $0.15"), new Color(.055f, .09f, .14f, .995f),
                            new Color(.52f, .88f, 1f), validCount > 0))
                        monetization?.Purchase(monetization.FindProduct(CrownfrontMonetization.EmergencyReviveId));
                }
            }
            else
            {
                DrawFittedLabel(new Rect(panel.x + 26f, panel.y + 105f, panel.width - 52f, 130f),
                    L("이번 전선의 복귀 기회는 이미 사용했습니다.\n메인 메뉴에서 새 전선을 준비하세요.",
                        "THE RETURN CHANCE FOR THIS RUN HAS ALREADY BEEN USED.\nPREPARE A NEW FRONT FROM THE MAIN MENU."),
                    new GUIStyle(centeredStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true }, 12);
            }

            var menuY = panel.yMax - 116f;
            if (DrawPremiumButton(new Rect(panel.x + 20f, menuY, panel.width - 40f, 48f),
                    L("메인 메뉴로", "MAIN MENU"), new Color(.08f, .055f, .075f, .995f),
                    new Color(.7f, .58f, .72f), true)) AbandonDefeatedRunToMainMenu();
            if (DrawPremiumButton(new Rect(panel.x + 20f, panel.yMax - 58f, panel.width - 40f, 38f),
                    L("처음부터 다시", "RESTART"), new Color(.14f, .065f, .045f, .995f),
                    new Color(.94f, .46f, .28f), true))
            {
                RestartDefeatedRunWithPreparation();
            }
        }

        private void TryExecuteSelectedRevive()
        {
            if (revivalUsedThisRun) return;
            var data = ReadRevivalSnapshot(selectedReviveSnapshotIndex);
            if (data == null) return;
            if (!emergencyRevivePaymentConfirmed)
            {
                if (economy == null || !economy.TryConsume(TacticalItemId.ReviveTicket)) return;
            }
            emergencyRevivePaymentConfirmed = false;
            if (!RestoreRunCheckpoint(data)) return;
            revivalUsedThisRun = true;
            showMainMenu = false;
            runInProgress = true;
            voiceBarks?.SetBattleMusic(true, true);
            ShowToast(L($"ROUND {Round} 안전 편성으로 복귀했습니다.",
                $"RETURNED TO THE ROUND {Round} SAFE FORMATION."));
        }

        private void AbandonDefeatedRunToMainMenu()
        {
            QueueMainMenuGoldNotice(AwardRunGold());
            if (!finalDefeatAdRequested)
            {
                finalDefeatAdRequested = monetization?.NotifyRunEnded() == true;
                if (finalDefeatAdRequested)
                {
                    monetization.InterstitialClosed += FinishDefeatedRunToMainMenu;
                    return;
                }
            }
            FinishDefeatedRunToMainMenu();
        }

        private void FinishDefeatedRunToMainMenu()
        {
            if (monetization != null) monetization.InterstitialClosed -= FinishDefeatedRunToMainMenu;
            ClearRevivalSnapshots();
            RestartGame();
            showMainMenu = true;
            mainMenuInputReadyAt = Time.unscaledTime + .42f;
            voiceBarks?.SetBattleMusic(false, true);
        }
    }
}
