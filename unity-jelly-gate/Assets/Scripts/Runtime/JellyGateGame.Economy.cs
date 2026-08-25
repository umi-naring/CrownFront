using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private CrownfrontEconomy economy;
        private readonly HashSet<TacticalItemId> activeRunItems = new();
        private readonly HashSet<TacticalItemId> pendingRunItems = new();
        private bool usedAnyTacticalItemThisRun;
        private bool showPregameLoadout;
        private bool sortieGateTransition;
        private float sortieGateTransitionStartedAt;
        private Texture2D reviveTicketTexture;
        private Texture2D tacticalItemAtlasTexture;
        private Texture2D removeAdsTexture;
        private Texture2D unitSkillIconAtlasTexture;
        private Texture2D selectedStatIconAtlasTexture;
        private readonly Dictionary<int, Texture2D> gemPackTextures = new();
        private int inspectedPregameItem = -1;
        private int inspectedRunItem = -1;
        private int fieldAidUsesThisRun;
        private int runGoldScore;
        private int perfectRoundsThisRun;
        private float gateHealthAtWaveStart;
        private bool runGoldAwarded;
        private int pressedRunItem = -1;
        private float pressedRunItemAt;
        private float inspectedRunItemUntil;
        private int pressedUnitAbilitySlot = -1;
        private float pressedUnitAbilityAt;
        private int inspectedUnitAbilitySlot = -1;
        private float inspectedUnitAbilityUntil;
        private int pendingTacticalItemUse = -1;
        private bool showCurrencyShortageDialog;
        private ShopCurrency shortageCurrency;
        private CrownfrontShopProduct shortageProduct;
        private float tacticalItemPromptPreviousTimeScale = 1f;
        private const float TacticalItemLongPressSeconds = .52f;

        private bool CurrencyShortagePromptVisible => showCurrencyShortageDialog;

        private void InitializeEconomy()
        {
            economy = gameObject.AddComponent<CrownfrontEconomy>();
            economy.Initialize();
            reviveTicketTexture = Resources.Load<Texture2D>("Shop/revive-ticket");
            tacticalItemAtlasTexture = Resources.Load<Texture2D>("Shop/tactical-item-atlas-v1");
            removeAdsTexture = Resources.Load<Texture2D>("Shop/remove-ads-v1");
            unitSkillIconAtlasTexture = Resources.Load<Texture2D>("Shop/unit-skill-icons-v2");
            selectedStatIconAtlasTexture = Resources.Load<Texture2D>("UI/stat-icons-v4");
            var gemAssets = new[]
            {
                (100, 100), (305, 310), (515, 525), (1040, 1075), (2100, 2200)
            };
            foreach (var (granted, asset) in gemAssets)
                gemPackTextures[granted] = Resources.Load<Texture2D>($"Shop/Products/gem-{asset}");
        }

        private void BindMonetizationEconomy()
        {
            if (monetization == null || economy == null) return;
            monetization.GemsPurchased += economy.GrantGems;
            monetization.EmergencyRevivePurchased += CompleteEmergencyRevivePurchase;
        }

        private void OpenPregameLoadout()
        {
            if (economy == null)
            {
                BeginSortieGateTransition();
                return;
            }
            showPregameLoadout = true;
        }

        private void ConfirmPregameLoadout()
        {
            pendingRunItems.Clear();
            foreach (var item in economy.ConsumeSelectedPregameItems()) pendingRunItems.Add(item);
            usedAnyTacticalItemThisRun = pendingRunItems.Count > 0;
            showPregameLoadout = false;
            BeginSortieGateTransition();
        }

        private void BeginSortieGateTransition()
        {
            sortieGateTransition = true;
            sortieGateTransitionStartedAt = Time.unscaledTime;
        }

        private void UpdateSortieGateTransition()
        {
            if (!sortieGateTransition || Time.unscaledTime - sortieGateTransitionStartedAt < 1.45f) return;
            sortieGateTransition = false;
            StartNewFront();
            activeRunItems.Clear();
            foreach (var item in pendingRunItems) activeRunItems.Add(item);
            pendingRunItems.Clear();
        }

        private void DrawEconomyWallet()
        {
            if (economy == null) return;
            var safe = SafeGuiRect;
            var width = Mathf.Min(188f, safe.width * .45f);
            var rect = new Rect(safe.xMax - width - 8f, safe.y + 8f, width, 31f);
            DrawPanel(rect, new Color(.012f, .02f, .034f, .82f));
            var half = rect.width * .5f;
            DrawFittedLabel(new Rect(rect.x + 5f, rect.y + 1f, half - 6f, rect.height - 2f),
                $"●  {economy.Gold:N0}", new GUIStyle(centeredStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, .84f, .35f) }
                }, 10);
            DrawFittedLabel(new Rect(rect.x + half, rect.y + 1f, half - 5f, rect.height - 2f),
                $"◆  {economy.Gems:N0}", new GUIStyle(centeredStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(.43f, .86f, 1f) }
                }, 10);
        }

        private void DrawPregameLoadout()
        {
            var screen = new Rect(0f, 0f, GuiWidth, GuiHeight);
            DrawPanel(screen, new Color(0f, .008f, .025f, .82f));
            var safe = SafeGuiRect;
            var width = Mathf.Min(424f, safe.width * .94f);
            var height = Mathf.Min(548f, safe.height * .79f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f, width, height);
            DrawOrnatePanel(panel, new Color(.018f, .038f, .072f, .995f), new Color(.78f, .65f, .34f), 3f);
            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 40f),
                L("출전 준비", "FRONT PREPARATION"), modalTitleStyle, 16);
            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 52f, panel.width - 36f, 34f),
                L($"보유 아이템 중 최대 {economy.PregameSelectionLimit}개 선택 · 선택 {economy.SelectedPregameItems.Count}/{economy.PregameSelectionLimit}",
                    $"SELECT UP TO {economy.PregameSelectionLimit} OWNED ITEMS · {economy.SelectedPregameItems.Count}/{economy.PregameSelectionLimit}"),
                new GUIStyle(statStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true }, 9);

            var items = economy.Catalog.Where(item => item.PregameSelectable).ToArray();
            if (inspectedPregameItem < 0 && items.Length > 0) inspectedPregameItem = (int)items[0].Id;
            var listTop = panel.y + 96f;
            const float gap = 7f;
            var cardWidth = (panel.width - 42f - gap * 3f) * .25f;
            const float cardHeight = 103f;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var col = i % 4;
                var row = i / 4;
                var rect = new Rect(panel.x + 21f + col * (cardWidth + gap),
                    listTop + row * (cardHeight + gap), cardWidth, cardHeight);
                var selected = economy.SelectedPregameItems.Contains(item.Id);
                var owned = economy.Count(item.Id);
                var inspected = inspectedPregameItem == (int)item.Id;
                DrawOrnatePanel(rect, selected ? new Color(.12f, .105f, .05f, .99f) :
                    new Color(.026f, .052f, .088f, .99f), selected ? new Color(1f, .79f, .34f) :
                    inspected ? new Color(.62f, .7f, .78f) : new Color(.3f, .42f, .56f), selected ? 3f : 2f);
                DrawTacticalItemIcon(new Rect(rect.x + 9f, rect.y + 7f, rect.width - 18f, rect.height - 34f), item.Id,
                    owned > 0 ? Color.white : new Color(.42f, .42f, .42f, .7f));
                DrawFittedLabel(new Rect(rect.x + 5f, rect.yMax - 27f, rect.width - 10f, 22f), $"× {owned}",
                    new GUIStyle(centeredStyle)
                    {
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = owned > 0 ? new Color(.96f, .9f, .73f) : new Color(.55f, .52f, .48f) }
                    }, 11);
                if (selected) DrawFittedLabel(new Rect(rect.xMax - 27f, rect.y + 4f, 22f, 22f), "✓",
                    new GUIStyle(centeredStyle) { fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, .85f, .32f) } }, 12);
                var available = owned > 0 && (selected || economy.SelectedPregameItems.Count < economy.PregameSelectionLimit);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    inspectedPregameItem = (int)item.Id;
                    if (available) economy.TogglePregameSelection(item.Id);
                }
            }

            var detail = items.FirstOrDefault(item => (int)item.Id == inspectedPregameItem) ?? items.FirstOrDefault();
            var detailRect = new Rect(panel.x + 21f, listTop + cardHeight * 2f + gap * 2f,
                panel.width - 42f, 98f);
            DrawOrnatePanel(detailRect, new Color(.025f, .05f, .084f, .995f), new Color(.5f, .61f, .73f), 2f);
            if (detail != null)
            {
                DrawTacticalItemIcon(new Rect(detailRect.x + 10f, detailRect.y + 10f, 72f, 72f), detail.Id, Color.white);
                DrawFittedLabel(new Rect(detailRect.x + 92f, detailRect.y + 7f, detailRect.width - 104f, 27f), detail.Name,
                    new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, .86f, .52f) } }, 12);
                DrawFittedLabel(new Rect(detailRect.x + 92f, detailRect.y + 34f, detailRect.width - 104f, 53f),
                    detail.Description, new GUIStyle(statStyle) { alignment = TextAnchor.UpperLeft, wordWrap = true,
                        normal = { textColor = new Color(.86f, .91f, .97f) } }, 10);
            }

            var confirmY = detailRect.yMax + 14f;
            if (DrawPremiumButton(new Rect(panel.x + 20f, confirmY, panel.width - 40f, 46f),
                     L("선택 완료 · 전선 출전", "CONFIRM · DEPLOY"), new Color(.2f, .13f, .045f, .99f),
                     new Color(.92f, .72f, .3f), true)) ConfirmPregameLoadout();
            if (DrawPremiumButton(new Rect(panel.x + 20f, confirmY + 54f, panel.width - 40f, 42f),
                     L("뒤로", "BACK"), new Color(.08f, .07f, .055f, .99f),
                     new Color(.55f, .48f, .37f), true)) showPregameLoadout = false;
        }

        private void DrawTacticalItemIcon(Rect rect, TacticalItemId id, Color tint)
        {
            if (tacticalItemAtlasTexture == null) return;
            var index = Mathf.Clamp((int)id, 0, 10);
            var column = index % 4;
            var row = index / 4;
            var uv = new Rect(column * .25f, 1f - (row + 1f) / 3f, .25f, 1f / 3f);
            var previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, tacticalItemAtlasTexture, uv, true);
            GUI.color = previous;
        }

        private void DrawActiveTacticalItemRail()
        {
            if (economy == null) return;
            var safe = SafeGuiRect;
            var ordered = activeRunItems.OrderBy(id => (int)id).Take(3).ToList();
            if (economy.Count(TacticalItemId.FieldAid) > 0 && !ordered.Contains(TacticalItemId.FieldAid))
                ordered.Add(TacticalItemId.FieldAid);
            if (ordered.Count == 0) return;
            const float iconSize = 46f;
            const float gap = 6f;
            var top = safe.y + TopHudHeight + 62f;
            var evt = Event.current;
            for (var i = 0; i < ordered.Count; i++)
            {
                var id = ordered[i];
                var rect = new Rect(safe.x + 7f, top + i * (iconSize + gap), iconSize, iconSize);
                var inspected = inspectedRunItem == (int)id;
                DrawOrnatePanel(rect, new Color(.09f, .072f, .047f, .94f),
                    inspected ? new Color(1f, .78f, .32f) : new Color(.52f, .43f, .3f), inspected ? 3f : 2f);
                DrawTacticalItemIcon(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), id, Color.white);
                var count = id == TacticalItemId.FieldAid ? economy.Count(id) : 1;
                DrawFittedLabel(new Rect(rect.x + 22f, rect.yMax - 18f, 21f, 15f), $"×{count}",
                    new GUIStyle(smallStyle)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white }
                    }, 8);

                if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
                {
                    pressedRunItem = (int)id;
                    pressedRunItemAt = Time.unscaledTime;
                    evt.Use();
                }
                else if (pressedRunItem == (int)id && Input.GetMouseButton(0) &&
                         Time.unscaledTime - pressedRunItemAt >= TacticalItemLongPressSeconds)
                {
                    inspectedRunItem = (int)id;
                    inspectedRunItemUntil = Time.unscaledTime + 3.5f;
                }
                else if (evt.type == EventType.MouseUp && evt.button == 0 && pressedRunItem == (int)id)
                {
                    var held = Time.unscaledTime - pressedRunItemAt;
                    pressedRunItem = -1;
                    if (held >= TacticalItemLongPressSeconds)
                    {
                        inspectedRunItem = (int)id;
                        inspectedRunItemUntil = Time.unscaledTime + 3.5f;
                    }
                    else if (id == TacticalItemId.FieldAid) RequestTacticalItemUse(id);
                    evt.Use();
                }
            }

            if (inspectedRunItem < 0 || Time.unscaledTime > inspectedRunItemUntil) return;
            var detail = economy.Definition((TacticalItemId)inspectedRunItem);
            if (detail == null || !activeRunItems.Contains(detail.Id) &&
                !(detail.Id == TacticalItemId.FieldAid && economy.Count(detail.Id) > 0))
            {
                inspectedRunItem = -1;
                return;
            }
            var infoWidth = Mathf.Min(326f, safe.width - 76f);
            var info = new Rect(safe.x + 60f, top, infoWidth, 116f);
            DrawOrnatePanel(info, new Color(.11f, .085f, .052f, .97f), new Color(.68f, .55f, .34f), 2f);
            DrawFittedLabel(new Rect(info.x + 12f, info.y + 8f, info.width - 24f, 26f), detail.Name,
                new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, .86f, .54f) } }, 11);
            DrawFittedWrappedLabel(new Rect(info.x + 12f, info.y + 38f, info.width - 24f, info.height - 47f),
                detail.Description, new GUIStyle(statStyle) { alignment = TextAnchor.UpperLeft, wordWrap = true,
                    clipping = TextClipping.Clip,
                    normal = { textColor = new Color(.92f, .88f, .79f) } }, 9);
        }

        private bool TacticalItemUsePromptVisible => pendingTacticalItemUse >= 0;

        private void RequestTacticalItemUse(TacticalItemId id)
        {
            if (economy == null || economy.Count(id) <= 0 || pendingTacticalItemUse >= 0) return;
            pendingTacticalItemUse = (int)id;
            tacticalItemPromptPreviousTimeScale = Time.timeScale;
            pointerHeld = pointerDragged = false;
            pressedUnit = null;
            pressedEnemy = null;
            Time.timeScale = 0f;
        }

        private void CancelTacticalItemUse()
        {
            pendingTacticalItemUse = -1;
            Time.timeScale = tacticalItemPromptPreviousTimeScale;
        }

        private void ConfirmTacticalItemUse()
        {
            if (pendingTacticalItemUse < 0) return;
            var id = (TacticalItemId)pendingTacticalItemUse;
            pendingTacticalItemUse = -1;
            Time.timeScale = tacticalItemPromptPreviousTimeScale;
            if (id == TacticalItemId.FieldAid) TryUseFieldAid();
            else if (id == TacticalItemId.TacticalReroll) TryUseTacticalReroll();
        }

        private void DrawTacticalItemUsePrompt()
        {
            if (pendingTacticalItemUse < 0 || economy == null) return;
            var id = (TacticalItemId)pendingTacticalItemUse;
            var definition = economy.Definition(id);
            if (definition == null) { CancelTacticalItemUse(); return; }
            var safe = SafeGuiRect;
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(0f, .004f, .015f, .78f));
            var width = Mathf.Min(354f, safe.width - 28f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - 143f, width, 286f);
            DrawOrnatePanel(panel, new Color(.075f, .055f, .035f, .998f), new Color(.92f, .72f, .3f), 3f);
            DrawTacticalItemIcon(new Rect(panel.x + 20f, panel.y + 18f, 68f, 68f), id, Color.white);
            DrawFittedLabel(new Rect(panel.x + 102f, panel.y + 18f, panel.width - 122f, 34f),
                definition.Name, new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold }, 12);
            DrawFittedLabel(new Rect(panel.x + 102f, panel.y + 54f, panel.width - 122f, 26f),
                L($"보유 ×{economy.Count(id)}", $"OWNED ×{economy.Count(id)}"),
                new GUIStyle(statStyle) { alignment = TextAnchor.MiddleLeft }, 10);
            DrawFittedWrappedLabel(new Rect(panel.x + 20f, panel.y + 101f, panel.width - 40f, 72f),
                definition.Description, new GUIStyle(statStyle) { alignment = TextAnchor.UpperLeft,
                    wordWrap = true, clipping = TextClipping.Clip }, 9);
            DrawPanel(new Rect(panel.x + 18f, panel.y + 184f, panel.width - 36f, 2f),
                new Color(.68f, .55f, .34f, .85f));
            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 191f, panel.width - 36f, 28f),
                L("이 아이템을 사용합니까?", "USE THIS ITEM?"), centeredStyle, 10);
            var buttonWidth = (panel.width - 54f) * .5f;
            if (DrawPremiumButton(TacticalItemUseButtonRect(panel, buttonWidth),
                    L("사용", "USE"), new Color(.22f, .13f, .035f), new Color(1f, .76f, .25f), true))
                ConfirmTacticalItemUse();
            if (DrawPremiumButton(TacticalItemCancelButtonRect(panel, buttonWidth),
                    L("취소", "CANCEL"), new Color(.06f, .075f, .1f), new Color(.56f, .68f, .82f), true))
                CancelTacticalItemUse();
        }

        private static Rect TacticalItemUseButtonRect(Rect panel, float buttonWidth) =>
            new(panel.x + 18f, panel.yMax - 67f, buttonWidth, 48f);

        private static Rect TacticalItemCancelButtonRect(Rect panel, float buttonWidth) =>
            new(panel.x + 36f + buttonWidth, panel.yMax - 67f, buttonWidth, 48f);

        private Rect TacticalItemRailRect()
        {
            if (economy == null) return Rect.zero;
            var count = Mathf.Min(3, activeRunItems.Count) +
                        (economy.Count(TacticalItemId.FieldAid) > 0 &&
                         !activeRunItems.Contains(TacticalItemId.FieldAid) ? 1 : 0);
            if (count <= 0) return Rect.zero;
            var safe = SafeGuiRect;
            return new Rect(safe.x + 4f, safe.y + TopHudHeight + 59f, 53f, count * 52f + 2f);
        }

        private bool TryUseFieldAid()
        {
            if (economy == null || fieldAidUsesThisRun >= 2 ||
                Phase is GamePhase.Defeat or GamePhase.Victory || !economy.TryConsume(TacticalItemId.FieldAid))
                return false;
            foreach (var unit in units)
                if (unit != null && unit.IsAlive) unit.RestoreHealth(unit.MaxHealth * .60f);
            fieldAidUsesThisRun++;
            usedAnyTacticalItemThisRun = true;
            inspectedRunItem = (int)TacticalItemId.FieldAid;
            inspectedRunItemUntil = Time.unscaledTime + 2.4f;
            ShowToast(L($"야전 구호품 사용 · 남은 수량 {economy.Count(TacticalItemId.FieldAid)}",
                $"FIELD AID USED · {economy.Count(TacticalItemId.FieldAid)} LEFT"));
            return true;
        }

        private void DrawSortieGateTransition()
        {
            UpdateSortieGateTransition();
            if (!sortieGateTransition) return;
            var screen = new Rect(0f, 0f, GuiWidth, GuiHeight);
            var elapsed = Time.unscaledTime - sortieGateTransitionStartedAt;
            var opening = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .24f) / 1.05f));
            // The sortie is a dedicated stage beat. Keeping a true black matte behind the doors
            // prevents the busy menu illustration from leaking through the opening and gives the
            // short animation the same visual authority on bright and dark menu skins.
            DrawPanel(screen, Color.black);
            var doorWidth = GuiWidth * .51f;
            var slide = opening * (doorWidth + 12f);
            var left = new Rect(-slide, 0f, doorWidth, GuiHeight);
            var right = new Rect(GuiWidth - doorWidth + slide, 0f, doorWidth, GuiHeight);
            DrawSortieDoorLeaf(left, true, opening);
            DrawSortieDoorLeaf(right, false, opening);
            // Keep the opened gap genuinely black. A former glow-and-panel seam remained as a
            // grey vertical stripe at the exact screen centre on narrow Android aspect ratios.

            var caption = new Rect(20f, GuiHeight * .435f, GuiWidth - 40f, 60f);
            DrawPanel(new Rect(caption.x + 20f, caption.y + 8f, caption.width - 40f, caption.height - 16f),
                new Color(0f, 0f, 0f, Mathf.Lerp(.68f, .35f, opening)));
            DrawFittedLabel(caption, L("왕성의 문이 열립니다", "THE CITADEL GATE OPENS"),
                new GUIStyle(overlayTitleStyle)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, .9f, .58f) }
                }, 15);
        }

        private void DrawSortieDoorLeaf(Rect leaf, bool leftLeaf, float opening)
        {
            if (leaf.width <= 0f || leaf.height <= 0f) return;
            var edge = new Color(.92f, .68f, .22f, 1f);
            var steel = new Color(.035f, .062f, .105f, 1f);
            DrawOrnatePanel(leaf, steel, edge, 5f);
            var inset = new Rect(leaf.x + 11f, leaf.y + 13f, leaf.width - 22f, leaf.height - 26f);
            DrawOrnatePanel(inset, new Color(.022f, .042f, .076f, 1f),
                new Color(.28f, .48f, .66f, 1f), 2f);

            // Repeating ribs, inset plates and rivets make the gate read as a constructed royal
            // object instead of two flat rectangles. The pattern mirrors perfectly at the seam.
            const int plateCount = 7;
            var plateHeight = inset.height / plateCount;
            for (var i = 0; i < plateCount; i++)
            {
                var plate = new Rect(inset.x + 5f, inset.y + 5f + i * plateHeight,
                    inset.width - 10f, Mathf.Max(8f, plateHeight - 10f));
                var tone = i % 2 == 0
                    ? new Color(.055f, .095f, .15f, 1f)
                    : new Color(.035f, .072f, .12f, 1f);
                DrawOrnatePanel(plate, tone, new Color(.18f, .34f, .5f, .94f), 1.4f);
                DrawSortieDoorRivet(new Vector2(plate.x + 10f, plate.center.y), edge);
                DrawSortieDoorRivet(new Vector2(plate.xMax - 10f, plate.center.y), edge);
            }

            var seamX = leftLeaf ? leaf.xMax - 18f : leaf.x + 8f;
            DrawPanel(new Rect(seamX, leaf.y + 6f, 10f, leaf.height - 12f),
                new Color(.68f, .46f, .13f, 1f));
            DrawPanel(new Rect(seamX + (leftLeaf ? 2f : 5f), leaf.y + 9f, 3f, leaf.height - 18f),
                new Color(1f, .82f, .37f, .88f));

            var crestSize = Mathf.Min(leaf.width * .38f, 126f);
            var crestX = leftLeaf ? leaf.xMax - crestSize * .78f : leaf.x - crestSize * .22f;
            var crest = new Rect(crestX, leaf.center.y - crestSize * .5f, crestSize, crestSize);
            if (CircleSprite != null)
                DrawSpriteInGui(CircleSprite, crest, new Color(.025f, .08f, .14f, 1f));
            if (CommandRingSprite != null)
                DrawSpriteInGui(CommandRingSprite, crest, new Color(1f, .7f, .2f, .96f));
            if (SparkSprite != null)
                DrawSpriteInGui(SparkSprite, new Rect(crest.x + crest.width * .25f, crest.y + crest.height * .25f,
                    crest.width * .5f, crest.height * .5f), new Color(.54f, .86f, 1f, .9f));

            var shimmer = Mathf.Clamp01(1f - opening * 1.3f) * (.55f + Mathf.Sin(Time.unscaledTime * 7f) * .12f);
            if (GlowSprite != null)
                DrawSpriteInGui(GlowSprite, new Rect(seamX - 22f, leaf.center.y - 60f, 54f, 120f),
                    new Color(.32f, .74f, 1f, shimmer));
        }

        private void DrawSortieDoorRivet(Vector2 center, Color color)
        {
            if (CircleSprite == null) return;
            DrawSpriteInGui(CircleSprite, new Rect(center.x - 3.5f, center.y - 3.5f, 7f, 7f),
                new Color(.01f, .02f, .035f, .9f));
            DrawSpriteInGui(CircleSprite, new Rect(center.x - 2.3f, center.y - 2.5f, 4.6f, 4.6f), color);
            DrawSpriteInGui(CircleSprite, new Rect(center.x - 1.2f, center.y - 1.5f, 1.8f, 1.8f),
                new Color(1f, .94f, .7f, .95f));
        }

        private void DrawTacticalAugmentButtons(Rect panel)
        {
            if (economy == null) return;
            var rerolls = economy.Count(TacticalItemId.TacticalReroll);
            var id = TacticalItemId.TacticalReroll;
            var icon = new Rect(panel.x + 14f, panel.y + 88f, 46f, 46f);
            var inspected = inspectedRunItem == (int)id && Time.unscaledTime <= inspectedRunItemUntil;
            DrawOrnatePanel(icon, new Color(.06f, .045f, .12f, .99f),
                inspected ? new Color(1f, .78f, .32f) : new Color(.72f, .52f, 1f), inspected ? 3f : 2f);
            DrawTacticalItemIcon(new Rect(icon.x + 4f, icon.y + 4f, icon.width - 8f, icon.height - 8f), id,
                rerolls > 0 ? Color.white : new Color(.38f, .38f, .4f));
            DrawFittedLabel(new Rect(icon.x + 22f, icon.yMax - 18f, 21f, 15f), $"×{rerolls}",
                new GUIStyle(smallStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                }, 8);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && icon.Contains(evt.mousePosition))
            {
                pressedRunItem = (int)id;
                pressedRunItemAt = Time.unscaledTime;
                evt.Use();
            }
            else if (pressedRunItem == (int)id && Input.GetMouseButton(0) &&
                     Time.unscaledTime - pressedRunItemAt >= TacticalItemLongPressSeconds && !inspected)
            {
                inspectedRunItem = (int)id;
                inspectedRunItemUntil = Time.unscaledTime + 3.5f;
                var definition = economy.Definition(id);
                if (definition != null) ShowToast($"{definition.Name} · {definition.Description}");
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && pressedRunItem == (int)id)
            {
                var held = Time.unscaledTime - pressedRunItemAt;
                pressedRunItem = -1;
                if (held < TacticalItemLongPressSeconds && rerolls > 0) RequestTacticalItemUse(id);
                evt.Use();
            }
        }

        private void TryUseTacticalReroll()
        {
            if (economy == null || currentOffers.Length == 0) return;
            var tier = currentOffers[0].Tier;
            var pool = GetAvailableAugmentTemplates(tier).OrderBy(_ => UnityEngine.Random.value).Take(3).ToArray();
            if (pool.Length != 3 || !economy.TryConsume(TacticalItemId.TacticalReroll)) return;
            currentOffers = pool.Select(template =>
            {
                var power = TierPower(tier);
                return new AugmentOffer(
                    GameLocalization.AugmentName(template.EffectKey, template.Name),
                    GameLocalization.AugmentDescription(template.EffectKey, power,
                        DescribeAugment(template, power)), template.EffectKey, tier, power);
            }).ToArray();
            usedAnyTacticalItemThisRun = true;
        }

        public bool HasActiveTacticalItem(TacticalItemId id) => activeRunItems.Contains(id);

        public float GetTacticalDamageMultiplier(PlayerUnit unit)
        {
            if (unit == null) return 1f;
            var bonus = activeRunItems.Contains(TacticalItemId.AllBoost) ? .02f : 0f;
            bonus += RoleFor(unit.Archetype) switch
            {
                DefenderRole.Tank when activeRunItems.Contains(TacticalItemId.TankBoost) => .03f,
                DefenderRole.Melee when activeRunItems.Contains(TacticalItemId.MeleeBoost) => .03f,
                DefenderRole.Ranged when activeRunItems.Contains(TacticalItemId.RangedBoost) => .03f,
                DefenderRole.Mage when activeRunItems.Contains(TacticalItemId.MageBoost) => .03f,
                DefenderRole.Support when activeRunItems.Contains(TacticalItemId.SupportBoost) => .03f,
                _ => 0f
            };
            return 1f + bonus;
        }

        public float GetTacticalHealthMultiplier(UnitArchetype archetype) =>
            GetTacticalRoleBonus(archetype, true);

        public float GetTacticalDefenseBonus(PlayerUnit unit, float baseDefense) =>
            unit == null ? 0f : baseDefense * (GetTacticalRoleBonus(unit.Archetype, true) - 1f);

        public float GetTacticalExperienceMultiplier() =>
            activeRunItems.Contains(TacticalItemId.MasteryManual) ? 1.06f : 1f;

        private float GetTacticalRoleBonus(UnitArchetype archetype, bool includeAll)
        {
            var bonus = includeAll && activeRunItems.Contains(TacticalItemId.AllBoost) ? .02f : 0f;
            var role = RoleFor(archetype);
            if (role == DefenderRole.Tank && activeRunItems.Contains(TacticalItemId.TankBoost) ||
                role == DefenderRole.Melee && activeRunItems.Contains(TacticalItemId.MeleeBoost) ||
                role == DefenderRole.Ranged && activeRunItems.Contains(TacticalItemId.RangedBoost) ||
                role == DefenderRole.Mage && activeRunItems.Contains(TacticalItemId.MageBoost) ||
                role == DefenderRole.Support && activeRunItems.Contains(TacticalItemId.SupportBoost)) bonus += .03f;
            return 1f + bonus;
        }

        private bool TryPurchaseInGameProduct(CrownfrontShopProduct product, ShopCurrency currency)
        {
            if (product == null || economy == null || product.DirectPurchase) return false;
            // Gold is deliberately restricted to consumable tactical supplies. Cosmetics remain
            // gem-only even if malformed catalog data or a stale UI attempts a gold transaction.
            if (currency == ShopCurrency.Gold && !product.HasTacticalItem) return false;
            var price = currency == ShopCurrency.Gold ? product.GoldPrice : product.GemPrice;
            if (price <= 0 || !economy.TrySpend(currency, price))
            {
                OpenCurrencyShortageDialog(currency, product);
                return false;
            }
            if (product.HasTacticalItem)
            {
                // Spending was completed above; credit the purchased item without charging twice.
                economy.GrantPurchasedItem(product.TacticalItem);
                usedAnyTacticalItemThisRun |= !showMainMenu;
            }
            else monetization.GrantInGameProduct(product.Id);
            ShowToast(L($"{product.Name} 구매 완료", $"{product.Name} PURCHASED"));
            return true;
        }

        private void OpenCurrencyShortageDialog(ShopCurrency currency, CrownfrontShopProduct product = null)
        {
            shortageCurrency = currency;
            shortageProduct = product;
            showCurrencyShortageDialog = true;
        }

        private void CloseCurrencyShortageDialog()
        {
            showCurrencyShortageDialog = false;
            shortageProduct = null;
        }

        private void DrawCurrencyShortageDialog()
        {
            if (!showCurrencyShortageDialog) return;
            var safe = SafeGuiRect;
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(0f, 0f, .015f, .78f));
            var width = Mathf.Min(360f, safe.width - 28f);
            var gems = shortageCurrency == ShopCurrency.Gems;
            var height = gems ? 246f : 210f;
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f, width, height);
            var accent = gems ? new Color(.42f, .86f, 1f) : new Color(1f, .78f, .3f);
            DrawOrnatePanel(panel, new Color(.018f, .038f, .072f, .998f), accent, 3f);
            DrawFittedLabel(new Rect(panel.x + 22f, panel.y + 18f, panel.width - 44f, 34f),
                gems ? L("보석이 부족합니다", "NOT ENOUGH GEMS") :
                    L("골드가 부족합니다", "NOT ENOUGH GOLD"), modalTitleStyle, 14);
            var productName = shortageProduct?.Name;
            var message = CurrencyShortageMessage(gems, productName);
            DrawFittedLabel(new Rect(panel.x + 24f, panel.y + 64f, panel.width - 48f, 76f),
                message, new GUIStyle(centeredStyle) { wordWrap = true }, 10);
            if (gems)
            {
                var buttonWidth = (panel.width - 58f) * .5f;
                if (DrawPremiumButton(new Rect(panel.x + 20f, panel.yMax - 62f, buttonWidth, 42f),
                        L("예", "YES"), new Color(.025f, .10f, .14f, .99f),
                        accent, true)) OpenGemStoreFromShortage();
                if (DrawPremiumButton(new Rect(panel.x + 38f + buttonWidth, panel.yMax - 62f, buttonWidth, 42f),
                        L("아니요", "NO"), new Color(.04f, .055f, .085f, .99f),
                        new Color(.48f, .62f, .76f), true)) CloseCurrencyShortageDialog();
            }
            else if (DrawPremiumButton(new Rect(panel.x + 24f, panel.yMax - 57f, panel.width - 48f, 39f),
                         L("확인", "OK"), new Color(.08f, .065f, .035f, .99f), accent, true))
                CloseCurrencyShortageDialog();
        }

        private string CurrencyShortageMessage(bool gems, string productName)
        {
            return gems
                ? L(string.IsNullOrWhiteSpace(productName)
                        ? "보석을 충전하시겠습니까?"
                        : $"{productName} 구매에 필요한 보석이 부족합니다.\n보석을 충전하시겠습니까?",
                    string.IsNullOrWhiteSpace(productName)
                        ? "OPEN THE GEM SHOP?"
                        : $"YOU NEED MORE GEMS FOR {productName}.\nOPEN THE GEM SHOP?")
                : L(string.IsNullOrWhiteSpace(productName)
                        ? "플레이로 골드를 모은 뒤 다시 시도하세요."
                        : $"{productName} 구매에 필요한 골드가 부족합니다.\n플레이로 골드를 모은 뒤 다시 시도하세요.",
                    string.IsNullOrWhiteSpace(productName)
                        ? "EARN MORE GOLD IN PLAY, THEN TRY AGAIN."
                        : $"YOU NEED MORE GOLD FOR {productName}.\nEARN GOLD IN PLAY, THEN TRY AGAIN.");
        }

        private void OpenGemStoreFromShortage()
        {
            CloseCurrencyShortageDialog();
            pendingPurchaseProduct = null;
            showShopPanel = true;
            shopCategory = ShopCategory.Currency;
            shopScroll = Vector2.zero;
        }

        private void DrawInGamePurchaseConfirmation(CrownfrontShopProduct product)
        {
            var safe = SafeGuiRect;
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(0f, 0f, .015f, .8f));
            var width = Mathf.Min(420f, safe.width - 24f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - 205f, width, 410f);
            DrawOrnatePanel(panel, new Color(.018f, .04f, .082f, .998f), product.Accent, 4f);
            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 15f, panel.width - 36f, 38f),
                L("구매 방법 선택", "CHOOSE PAYMENT"), modalTitleStyle, 16);
            DrawShopProductPreview(product, new Rect(panel.x + 26f, panel.y + 65f, 116f, 124f));
            DrawFittedLabel(new Rect(panel.x + 158f, panel.y + 67f, panel.width - 184f, 34f),
                product.Name, new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold }, 12);
            DrawFittedLabel(new Rect(panel.x + 158f, panel.y + 105f, panel.width - 184f, 84f),
                product.Description, new GUIStyle(statStyle) { alignment = TextAnchor.UpperLeft,
                    wordWrap = true }, 9);
            DrawFittedLabel(new Rect(panel.x + 24f, panel.y + 202f, panel.width - 48f, 30f),
                $"● {economy.Gold:N0}G     ◆ {economy.Gems:N0}",
                new GUIStyle(centeredStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    normal = { textColor = new Color(.78f, .93f, 1f) }
                }, 10);

            var gemEnabled = product.GemPrice > 0 && economy.Gems >= product.GemPrice;
            if (product.HasTacticalItem)
            {
                var goldEnabled = product.GoldPrice > 0 && economy.Gold >= product.GoldPrice;
                if (DrawPremiumButton(new Rect(panel.x + 24f, panel.y + 244f, panel.width - 48f, 48f),
                        L($"구매  ·  ● {product.GoldPrice:N0}G", $"BUY  ·  ● {product.GoldPrice:N0}G"),
                        new Color(.16f, .105f, .025f, .99f), new Color(1f, .79f, .3f), goldEnabled) &&
                    TryPurchaseInGameProduct(product, ShopCurrency.Gold)) pendingPurchaseProduct = null;
            }
            var gemY = product.HasTacticalItem ? panel.y + 302f : panel.y + 272f;
            if (DrawPremiumButton(new Rect(panel.x + 24f, gemY, panel.width - 48f, 52f),
                    L($"구매  ·  ◆ {product.GemPrice:N0}", $"BUY  ·  ◆ {product.GemPrice:N0}"),
                    new Color(.035f, .10f, .14f, .99f), new Color(.52f, .9f, 1f), product.GemPrice > 0))
            {
                if (TryPurchaseInGameProduct(product, ShopCurrency.Gems)) pendingPurchaseProduct = null;
                else pendingPurchaseProduct = null;
            }
            if (DrawPremiumButton(new Rect(panel.x + 24f, panel.yMax - 46f, panel.width - 48f, 31f),
                    L("취소", "CANCEL"), new Color(.04f, .06f, .095f, .99f),
                    new Color(.5f, .64f, .8f), true)) pendingPurchaseProduct = null;
        }

        private void CompleteEmergencyRevivePurchase()
        {
            emergencyRevivePaymentConfirmed = true;
            TryExecuteSelectedRevive();
        }

        private void UpdateItemlessChallengeProgress()
        {
            if (usedAnyTacticalItemThisRun) return;
            var best = Mathf.Max(PlayerPrefs.GetInt("Crownfront.Challenge.ItemlessBest", 0), roundsCleared);
            PlayerPrefs.SetInt("Crownfront.Challenge.ItemlessBest", best);
            foreach (var goal in new[] { 10, 25, 50 })
                if (best >= goal) CompleteChallenge("itemless_" + goal);
            PlayerPrefs.Save();
        }

        private void BeginWaveGoldScoring() => gateHealthAtWaveStart = gateHealth;

        private void ScoreCompletedRoundGold()
        {
            var chapter = Mathf.Clamp((Round - 1) / 5, 0, 9);
            // Gold is a long-term collection currency, not a duplicate of the round formation
            // budget.  The former 15+ per-round base paid roughly 200G by round 12 and erased the
            // value of challenges/ads.  Keep a readable one-point clear score, a one-point perfect
            // bonus, and a modest boss premium that grows only every second chapter.
            runGoldScore += 1;
            if (gateHealth >= gateHealthAtWaveStart - .01f)
            {
                perfectRoundsThisRun++;
                runGoldScore += 1;
            }
            if (Round % 5 == 0) runGoldScore += 2 + Mathf.CeilToInt(chapter * .5f);
        }

        internal static bool IsRunSettlementEligible(GamePhase phase, int clearedRounds) =>
            phase is GamePhase.Defeat or GamePhase.Victory || clearedRounds >= MaxRounds;

        private int AwardRunGold()
        {
            // A checkpoint keeps the live run score, but leaving an unfinished battle must never
            // convert it into account gold. Settlement is final only on defeat or full clear.
            if (!IsRunSettlementEligible(Phase, roundsCleared) || runGoldAwarded || economy == null) return 0;
            var total = CalculateRunGoldReward(runGoldScore, roundsCleared,
                Mathf.Clamp01(gateHealth / GateMaxHealth), revivalUsedThisRun);
            economy.GrantGold(total);
            runGoldAwarded = true;
            return total;
        }

        internal static int CalculateRunGoldReward(int clearScore, int clearedRounds,
            float gateHealthRatio, bool revivalUsed)
        {
            var preservation = Mathf.RoundToInt(Mathf.Max(0, clearedRounds) *
                                                Mathf.Clamp01(gateHealthRatio) * .25f);
            var noRevive = revivalUsed ? 0 : Mathf.RoundToInt(Mathf.Max(0, clearedRounds) * .15f);
            return Mathf.Max(0, clearScore + preservation + noRevive);
        }
    }
}
