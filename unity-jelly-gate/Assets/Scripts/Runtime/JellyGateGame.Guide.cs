using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private bool showGuidePanel;
        private int guideTab;
        private Vector2 guideScroll;
        private bool guideDragActive;
        private Vector2 guideDragLast;
        private int guideTouchFingerId = -1;
        private int guideTouchProcessedFrame = -1;
        private Vector2 guideTouchLast;
        private Texture2D privacyBrandLogo;

        private const float GuideUnitCardHeight = 292f;
        private const float GuideUnitCardStride = 301f;
        private const float GuideUnitRosterHeaderHeight = 99f;
        private const float GuideScrollBottomPadding = 24f;

        private static readonly UnitArchetype[] GuideUnits =
        {
            UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer, UnitArchetype.AreaMage,
            UnitArchetype.SingleMage, UnitArchetype.Bombardier, UnitArchetype.Lancer, UnitArchetype.Druid,
            UnitArchetype.Musketeer, UnitArchetype.Oracle
        };

        private static readonly EnemyClass[] GuideEnemies =
        {
            EnemyClass.Melee, EnemyClass.Skeleton, EnemyClass.Runner, EnemyClass.Brute, EnemyClass.Shaman,
            EnemyClass.Siege, EnemyClass.Piercer, EnemyClass.Wisp, EnemyClass.Flyer, EnemyClass.Mage
        };

        private void DrawGuideOverlay()
        {
            var screen = new Rect(0f, 0f, GuiWidth, GuiHeight);
            DrawPanel(screen, new Color(0f, .008f, .02f, .82f));
            var safe = SafeGuiRect;
            var width = Mathf.Min(416f, safe.width * .96f);
            var height = Mathf.Min(676f, safe.height * .94f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f, width, height);
            DrawOrnatePanel(panel, new Color(.014f, .034f, .071f, .998f),
                new Color(.38f, .86f, .79f), 4f);

            GUI.Label(new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 37f),
                L("게임 정보", "FIELD MANUAL"), modalTitleStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 43f, panel.width - 36f, 18f),
                L("현재 적용된 규칙과 수치를 한곳에서 확인합니다.",
                    "CURRENT RULES, ROSTERS AND LIVE BALANCE DATA"), centeredStyle);

            var tabs = new[]
            {
                L("기본", "BASICS"), L("아군", "UNITS"), L("보스", "BOSSES"),
                L("전투", "COMBAT"), L("증강", "AUGMENTS"),
                L("개인정보처리방침", "PRIVACY")
            };
            const float tabGap = 4f;
            var tabWidth = (panel.width - 28f - tabGap * 2f) / 3f;
            for (var i = 0; i < tabs.Length; i++)
            {
                var selected = guideTab == i;
                var column = i % 3;
                var row = i / 3;
                var rect = new Rect(panel.x + 14f + column * (tabWidth + tabGap),
                    panel.y + 69f + row * 38f, tabWidth, 34f);
                if (!DrawPremiumButton(rect, tabs[i],
                        selected ? new Color(.035f, .15f, .15f, .99f) : new Color(.025f, .055f, .105f, .99f),
                        selected ? new Color(.38f, 1f, .82f) : new Color(.35f, .52f, .72f), true)) continue;
                guideTab = i;
                guideScroll = Vector2.zero;
            }

            var viewport = GuideViewportRect(panel);
            HandleGuideTouchDrag(viewport, GuideContentHeight(guideTab));
            guideScroll = GUI.BeginScrollView(viewport, guideScroll,
                new Rect(0f, 0f, viewport.width - 18f, GuideContentHeight(guideTab)), false, true);
            var y = 2f;
            switch (guideTab)
            {
                case 0: DrawGuideBasics(viewport.width - 18f, ref y); break;
                case 1: DrawGuideUnitRoster(viewport.width - 18f, ref y); break;
                case 2: DrawGuideBossRoster(viewport.width - 18f, ref y); break;
                case 3: DrawGuideCombatRules(viewport.width - 18f, ref y); break;
                case 4: DrawGuideAugmentsAndStore(viewport.width - 18f, ref y); break;
                default: DrawGuidePrivacyPolicy(viewport.width - 18f, ref y); break;
            }
            GUI.EndScrollView();

            if (DrawPremiumButton(new Rect(panel.x + 27f, panel.yMax - 49f, panel.width - 54f, 36f),
                    L("닫기", "CLOSE"), new Color(.035f, .075f, .115f, .99f),
                    new Color(.44f, .72f, .88f), true)) showGuidePanel = false;
        }

        private static float GuideContentHeight(int tab) => tab switch
        {
            0 => 1030f,
            1 => GuideUnitRosterContentHeight(),
            2 => 2390f,
            3 => 1110f,
            4 => 7200f,
            _ => 2260f
        };

        private void DrawGuidePrivacyPolicy(float width, ref float y)
        {
            if (privacyBrandLogo == null) privacyBrandLogo = Resources.Load<Texture2D>("Legal/umi-logo");
            var logoCard = new Rect(4f, y, width - 8f, 132f);
            DrawOrnatePanel(logoCard, new Color(.97f, .985f, .99f, 1f), new Color(.42f, .82f, .82f), 2f);
            if (privacyBrandLogo != null)
                GUI.DrawTexture(new Rect(logoCard.x + 20f, logoCard.y + 12f, logoCard.width - 40f, 82f),
                    privacyBrandLogo, ScaleMode.ScaleToFit, true);
            DrawFittedLabel(new Rect(logoCard.x + 12f, logoCard.yMax - 34f, logoCard.width - 24f, 24f),
                L("CROWNFRONT 개인정보처리방침 · 2026년 8월 22일", "CROWNFRONT PRIVACY POLICY · AUGUST 22, 2026"),
                new GUIStyle(centeredStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(.04f, .24f, .34f) }
                }, 10);
            y += 143f;

            DrawGuideCard(width, ref y, L("개요", "OVERVIEW"),
                L("CROWNFRONT는 사용자의 개인정보를 중요하게 생각하며 Google Play 정책 및 관련 법령에 따라 사용자 데이터를 처리합니다.",
                    "CROWNFRONT values user privacy and processes data under Google Play policies and applicable law."), 92f);
            DrawGuideCard(width, ref y, L("1. 개발자가 직접 수집하는 정보", "1. DATA COLLECTED DIRECTLY"),
                L("별도 회원가입·로그인을 제공하지 않으며 이름, 이메일, 전화번호, 정확한 위치, 결제수단 정보를 직접 수집하거나 자체 서버에 저장하지 않습니다. 게임 진행·도전 기록, 언어·음향 설정, 보유·장착 스킨은 기기에 저장됩니다. Android 백업이 켜진 경우 Google 계정으로 백업·복원될 수 있으나 개발자는 해당 백업에 직접 접근하지 않습니다.",
                    "No account or login is required. We do not directly collect or store names, email addresses, phone numbers, precise location, or payment credentials. Progress, challenges, settings, and cosmetic ownership are stored on-device and may be included in Android backup without developer access."), 188f);
            DrawGuideCard(width, ref y, L("2. 광고와 자동 처리 정보", "2. ADS AND AUTOMATIC PROCESSING"),
                L("Google AdMob, Google Mobile Ads SDK 및 중재 광고 파트너인 Unity Ads가 IP 기반 대략적 위치, 앱·광고 상호작용, 진단 정보, 광고 ID·App Set ID 등의 식별자를 처리할 수 있습니다. 광고 제공·입찰·측정·분석·부정행위 방지·보안·규정 준수 목적이며, 해당 지역에서는 Google UMP 동의 및 개인정보 보호 옵션을 제공합니다.",
                    "Google AdMob, the Mobile Ads SDK, and the mediated advertising partner Unity Ads may process approximate IP-based location, app and ad interactions, diagnostics, advertising ID, App Set ID, and related identifiers for ad delivery, bidding, measurement, analytics, fraud prevention, security, and compliance. Google UMP consent and privacy options are shown where required."), 218f);
            DrawGuideCard(width, ref y, L("3. 인앱 구매", "3. IN-APP PURCHASES"),
                L("광고 제거와 디지털 상품 결제는 Google Play Billing이 처리하며 개발자는 결제수단 정보를 수집·저장하지 않습니다. 상품 제공과 재설치·기기 변경 후 소유권 확인을 위해 상품 식별자와 구매 상태를 조회할 수 있습니다. 환불·취소·결제수단은 Google Play 정책과 계정 설정에 따라 관리됩니다.",
                    "Google Play Billing processes digital purchases. We do not collect or store payment credentials. Product identifiers and purchase status may be queried to deliver and restore ownership. Refunds, cancellations, and payment methods are managed under Google Play policies and account settings."), 176f);
            DrawGuideCard(width, ref y, L("4. 이용하는 제3자 서비스", "4. THIRD-PARTY SERVICES"),
                L("Google AdMob / Google Mobile Ads SDK, Google User Messaging Platform, Unity Ads, Google Play Billing, Android 백업·기기 이전 기능을 이용합니다. 각 제공자는 자체 개인정보처리방침에 따라 데이터를 처리할 수 있습니다.\nGoogle 개인정보처리방침: policies.google.com/privacy\nUnity 개인정보처리방침: unity.com/legal/privacy-policy\n광고 및 개인정보 보호: policies.google.com/technologies/ads\nPlay 약관: play.google.com/about/play-terms/",
                    "Services used: Google AdMob / Mobile Ads SDK, Google User Messaging Platform, Unity Ads, Google Play Billing, and optional Android backup/device transfer. Providers may process data under their own policies.\nGoogle Privacy: policies.google.com/privacy\nUnity Privacy: unity.com/legal/privacy-policy\nAds: policies.google.com/technologies/ads\nPlay Terms: play.google.com/about/play-terms/"), 248f);
            DrawGuideCard(width, ref y, L("5. 데이터 보안", "5. DATA SECURITY"),
                L("Google 및 Unity 광고 서비스 SDK를 통해 전송되는 데이터는 전송 과정에서 암호화됩니다. 앱은 필요한 기능에 한해 데이터를 사용하고 합리적인 보안 조치를 적용하지만 인터넷 전송 또는 전자 저장 방식의 절대적 보안을 보장할 수는 없습니다.",
                    "Data transmitted through Google and Unity advertising SDKs is encrypted in transit. The app limits use to necessary functions and applies reasonable safeguards, but no internet transmission or electronic storage method is absolutely secure."), 154f);
            DrawGuideCard(width, ref y, L("6. 보관·삭제 및 사용자 선택권", "6. RETENTION, DELETION, AND CHOICES"),
                L("개발자는 별도 계정 데이터베이스를 운영하지 않습니다. 기기 게임 데이터는 Android 설정의 앱 데이터 삭제 또는 앱 제거로 삭제할 수 있습니다. Android 백업, 광고 정보, Play 결제 기록은 Google 계정과 각 서비스 정책에 따라 관리됩니다. 앱의 개인정보 보호 옵션과 Google 광고 설정을 이용할 수 있으며 열람·정정·삭제는 아래 문의처로 요청할 수 있습니다.",
                    "We operate no separate account database. On-device data can be deleted through Android app-data settings or uninstalling the app. Android backup, advertising data, and Play purchase records follow Google account and service policies. Privacy options and Google ad settings are available; access, correction, or deletion inquiries can be sent below."), 202f);
            DrawGuideCard(width, ref y, L("7. 아동 및 청소년의 개인정보", "7. CHILDREN AND TEENS"),
                L("Google Play Console에 신고한 대상 연령과 관련 정책을 준수합니다. 대상 연령, 광고 방식 또는 사용 SDK가 변경되면 필요한 보호 설정과 본 방침을 함께 갱신합니다.",
                    "We follow the target age declared in Google Play Console and applicable policies. If target age, advertising, or SDK use changes, safeguards and this policy will be updated together."), 126f);
            DrawGuideCard(width, ref y, L("8. 방침 변경", "8. POLICY CHANGES"),
                L("앱 기능, 사용 SDK 또는 관련 정책 변경에 따라 본 방침을 수정할 수 있습니다. 변경 내용은 공식 페이지에 게시하고 최종 업데이트일을 함께 변경합니다.",
                    "This policy may change with app features, SDKs, or applicable policies. Updates are posted on the official page with a revised update date."), 116f);
            DrawGuideCard(width, ref y, L("9. 문의", "9. CONTACT"),
                L("개발자: 우미\n이메일: iprite359@gmail.com\n공식 방침: https://sites.google.com/view/crownfront",
                    "Developer: UMI\nEmail: iprite359@gmail.com\nOfficial policy: https://sites.google.com/view/crownfront"), 126f);
        }

        private static float GuideUnitRosterContentHeight() =>
            GuideUnitRosterHeaderHeight + GuideUnits.Length * GuideUnitCardStride + GuideScrollBottomPadding;

        private static Rect GuideLastUnitCardRect(float width)
        {
            var y = GuideUnitRosterHeaderHeight + (GuideUnits.Length - 1) * GuideUnitCardStride;
            return new Rect(4f, y, width - 8f, GuideUnitCardHeight);
        }

        private void HandleGuideTouchDrag(Rect viewport, float contentHeight)
        {
            // Android touch input is handled once from UpdateGuideTouchInput. Processing the
            // synthetic IMGUI mouse event too would apply the same finger drag twice.
            if (Application.isMobilePlatform) return;

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && viewport.Contains(evt.mousePosition))
            {
                guideDragActive = true;
                guideDragLast = evt.mousePosition;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && guideDragActive)
            {
                var delta = evt.mousePosition - guideDragLast;
                guideDragLast = evt.mousePosition;
                ApplyGuideDragDelta(delta.y, viewport, contentHeight);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && guideDragActive)
            {
                guideDragActive = false;
                evt.Use();
            }
        }

        private Rect GuideViewportRect(Rect panel) =>
            new(panel.x + 13f, panel.y + 151f, panel.width - 26f, panel.height - 210f);

        private Rect CurrentGuideViewportRect()
        {
            var safe = SafeGuiRect;
            var width = Mathf.Min(416f, safe.width * .96f);
            var height = Mathf.Min(676f, safe.height * .94f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f,
                width, height);
            return GuideViewportRect(panel);
        }

        private void UpdateGuideTouchInput()
        {
            if (!Application.isMobilePlatform || guideTouchProcessedFrame == Time.frameCount) return;
            guideTouchProcessedFrame = Time.frameCount;
            var viewport = CurrentGuideViewportRect();
            var trackedTouchFound = false;
            for (var index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);
                var guiPosition = GuideTouchToGuiPosition(touch.position);
                if (guideTouchFingerId < 0 && touch.phase == TouchPhase.Began && viewport.Contains(guiPosition))
                {
                    guideTouchFingerId = touch.fingerId;
                    guideTouchLast = guiPosition;
                    guideDragActive = true;
                    trackedTouchFound = true;
                    continue;
                }
                if (touch.fingerId != guideTouchFingerId) continue;
                trackedTouchFound = true;
                if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
                {
                    guideTouchFingerId = -1;
                    guideDragActive = false;
                    continue;
                }
                var delta = guiPosition - guideTouchLast;
                guideTouchLast = guiPosition;
                ApplyGuideDragDelta(delta.y, viewport, GuideContentHeight(guideTab));
            }
            if (guideTouchFingerId >= 0 && !trackedTouchFound)
            {
                guideTouchFingerId = -1;
                guideDragActive = false;
            }
        }

        private Vector2 GuideTouchToGuiPosition(Vector2 screenPosition)
        {
            var scaleX = GuiWidth / Mathf.Max(1f, Screen.width);
            var scaleY = GuiHeight / Mathf.Max(1f, Screen.height);
            return new Vector2(screenPosition.x * scaleX, (Screen.height - screenPosition.y) * scaleY);
        }

        private void ApplyGuideDragDelta(float guiDeltaY, Rect viewport, float contentHeight)
        {
            guideScroll.y = Mathf.Clamp(guideScroll.y - guiDeltaY, 0f,
                Mathf.Max(0f, contentHeight - viewport.height));
        }

        public bool VerifyGuideTouchScrollForQa()
        {
            var saved = guideScroll;
            guideScroll = Vector2.zero;
            var viewport = new Rect(0f, 0f, 380f, 450f);
            ApplyGuideDragDelta(-180f, viewport, 1600f);
            var movedDown = Mathf.Abs(guideScroll.y - 180f) < .01f;
            ApplyGuideDragDelta(500f, viewport, 1600f);
            var clampedTop = guideScroll.y == 0f;
            ApplyGuideDragDelta(-5000f, viewport, 1600f);
            var clampedBottom = Mathf.Abs(guideScroll.y - 1150f) < .01f;
            guideScroll = saved;
            return movedDown && clampedTop && clampedBottom;
        }

        private void DrawGuideBasics(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("전장 진행", "BATTLE FLOW"));
            DrawGuideCard(width, ref y, L("1. 편성 및 배치", "1. FORMATION & DEPLOYMENT"),
                L("라운드 시작 코인으로 아군을 소환합니다. 소환할 유닛을 고른 동안만 배치 모드가 켜지며, 취소 후에는 한 명 클릭 또는 빈 땅 드래그로 여러 명을 선택해 즉시 재배치할 수 있습니다.",
                    "Spend the round budget to summon defenders. Placement mode exists only while a unit is armed; cancel it to click one unit or drag-select a group and reposition them immediately."), 116f);
            DrawGuideCard(width, ref y, L("2. 웨이브", "2. WAVE"),
                L("웨이브가 시작되면 같은 세력의 전열·기동·원거리·특수 병력이 매 라운드 혼합 편성되어 외곽 성문에서 진격합니다. 한 마리가 닿는다고 즉시 패배하지 않으며, 성벽 내구도가 0이 될 때 패배합니다.",
                    "Every round mixes frontline, mobile, ranged and specialist troops from one faction. They advance from the outer gates; defeat occurs only when citadel durability reaches zero."), 116f);
            DrawGuideCard(width, ref y, L("3. 라운드 종료", "3. ROUND END"),
                L("살아남은 아군은 삭제되지 않고 다음 라운드에 유지됩니다. 성벽 내구도는 자동 회복하지 않습니다. 기본 코인도 라운드 수로 증가하지 않으며, 코인 증가는 증강으로만 얻습니다.",
                    "Surviving defenders persist into the next round. Gate durability never auto-heals, and base coins do not rise with round number; only augments can increase the budget."), 109f);

            DrawGuideChapter(width, ref y, L("컨트롤", "CONTROLS"));
            DrawGuideCard(width, ref y, L("이동 우선 전투", "MOVE PRIORITY"),
                L("선택한 아군으로 땅을 누르면 현재 공격보다 이동 명령이 우선됩니다. 공격 도중에도 즉시 위치를 바꿀 수 있으며, 지정 지점은 선명한 전술 핑으로 표시됩니다.",
                    "Tap the ground with defenders selected to override the current attack. Units can reposition during combat, and a clear tactical ping marks the destination."), 100f);
            DrawGuideCard(width, ref y, L("집중 공격", "FOCUS FIRE"),
                L("적을 누르면 지정 표적이 됩니다. 사거리 밖이라면 공격 가능한 최대 사거리까지 접근한 뒤 공격하며, 표적을 직접 지정하지 않았을 때는 탐지 범위 안의 가장 가까운 적을 우선합니다.",
                    "Tap an enemy to assign a focus target. Units approach to maximum firing range if needed; without a focus order they prioritize the nearest enemy inside detection range."), 111f);
            DrawGuideCard(width, ref y, L("정지", "HOLD"),
                L("정지는 추적과 이동만 멈춥니다. 정지 상태는 유닛 아래 아이콘으로 표시되며, 공격 사거리 안으로 들어온 적은 계속 공격합니다.",
                    "Hold stops movement and pursuit only. A clean ground marker shows the state, and the unit still attacks enemies that enter attack range."), 96f);
            DrawGuideCard(width, ref y, L("카메라와 미니맵", "CAMERA & MINIMAP"),
                L("두 손가락 핀치 또는 마우스 휠로 확대·축소하고, 드래그로 넓은 맵을 이동합니다. 우측 상단 미니맵으로 전선과 유닛 분포를 빠르게 확인합니다.",
                    "Pinch or use the mouse wheel to zoom, and pan across the expanded battlefield. The top-right minimap summarizes fronts and unit positions."), 96f);
        }

        private void DrawGuideUnitRoster(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("아군 10종 · 실제 기본 수치", "10 DEFENDERS · LIVE BASE STATS"));
            GUI.Label(new Rect(8f, y, width - 16f, 48f),
                L("공격력과 마력은 별도 능력치입니다. 모든 유닛은 고유 스킬을 가지며, 5레벨에 외형과 성능이 크게 강화된 영웅으로 진화해 궁극기를 사용할 수 있습니다.",
                    "Attack and Magic are separate stats. Every unit has a signature skill; at level 5 it becomes a visually distinct hero and unlocks an ultimate."),
                GuideBodyStyle(13, TextAnchor.UpperLeft));
            y += 55f;
            foreach (var unit in GuideUnits)
            {
                if (!definitions.TryGetValue(unit, out var definition)) continue;
                var rect = new Rect(4f, y, width - 8f, GuideUnitCardHeight);
                DrawOrnatePanel(rect, new Color(.025f, .055f, .105f, .99f), definition.Color, 2f);
                var sprite = GetUnitCardSprite(unit);
                var portraitFrame = GuideUnitPortraitFrameRect(rect);
                var portraitRect = GuideUnitPortraitRect(rect);
                DrawOrnatePanel(portraitFrame, new Color(.018f, .025f, .055f, .98f),
                    Color.Lerp(definition.Color, Color.white, .28f), 2f);
                if (sprite != null) DrawSpriteContained(portraitRect, sprite);
                DrawPanel(new Rect(portraitFrame.x + 7f, portraitFrame.y + 7f, 4f, 4f),
                    new Color(1f, .91f, .62f, .9f));
                DrawPanel(new Rect(portraitFrame.xMax - 11f, portraitFrame.y + 7f, 4f, 4f),
                    new Color(1f, .91f, .62f, .9f));
                var textX = portraitFrame.xMax + 10f;
                GUI.Label(new Rect(textX, rect.y + 7f, rect.xMax - textX - 8f, 25f),
                    L($"{GuideUnitName(unit)}  {definition.Cost}코인",
                        $"{GuideUnitName(unit)}  {definition.Cost} COINS"), GuideTitleStyle(15));
                GUI.Label(new Rect(textX, rect.y + 33f, rect.xMax - textX - 8f, 38f),
                    GuideUnitRoleOnly(unit),
                    GuideBodyStyle(11, TextAnchor.UpperLeft));
                GUI.Label(new Rect(textX, rect.y + 70f, rect.xMax - textX - 8f, 62f),
                    L($"HP {definition.MaxHealth:0} · 공격력 {definition.AttackPower:0} · 마력 {definition.MagicPower:0}\n사거리 {definition.Range:0.00} · 방어력 {definition.Armor:0} · 마법 저항 {definition.MagicResistance:0}",
                        $"HP {definition.MaxHealth:0} · ATTACK {definition.AttackPower:0} · MAGIC {definition.MagicPower:0}\nRANGE {definition.Range:0.00} · ARMOR {definition.Armor:0} · RESIST {definition.MagicResistance:0}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft));
                GUI.Label(new Rect(rect.x + 12f, rect.y + 140f, rect.width - 24f, 30f),
                    L($"기본 공격: {GuideBasicAttack(unit)}", $"BASIC ATTACK: {GuideBasicAttack(unit)}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft));
                GUI.Label(new Rect(rect.x + 12f, rect.y + 172f, rect.width - 24f, 52f),
                    L($"스킬: {GuideSkillName(unit)} — {GuideSkillEffect(unit)}",
                        $"SKILL: {GuideSkillName(unit)} — {GuideSkillEffect(unit)}"),
                    GuideBodyStyle(11, TextAnchor.UpperLeft));
                GUI.Label(new Rect(rect.x + 12f, rect.y + 228f, rect.width - 24f, 54f),
                    L($"영웅 궁극기: {GuideUltimateName(unit)} — {GuideUltimateEffect(unit)}",
                        $"HERO ULTIMATE: {GuideUltimateName(unit)} — {GuideUltimateEffect(unit)}"),
                    GuideBodyStyle(11, TextAnchor.UpperLeft));
                y += GuideUnitCardStride;
            }
        }

        public bool VerifyGuideUnitLastCardClearanceForQa()
        {
            var panelSizes = new[] { new Vector2(416f, 676f), new Vector2(340f, 560f) };
            return GuideUnits[^1] == UnitArchetype.Oracle && panelSizes.All(size =>
            {
                var panel = new Rect(0f, 0f, size.x, size.y);
                var viewport = GuideViewportRect(panel);
                var contentHeight = GuideContentHeight(1);
                var lastCard = GuideLastUnitCardRect(viewport.width - 18f);
                var maxScroll = Mathf.Max(0f, contentHeight - viewport.height);
                var lastCardBottomInViewportAtEnd = lastCard.yMax - maxScroll;
                var clearanceAboveViewportBottom = viewport.height - lastCardBottomInViewportAtEnd;
                var closeButton = new Rect(panel.x + 27f, panel.yMax - 49f, panel.width - 54f, 36f);
                return contentHeight >= lastCard.yMax + GuideScrollBottomPadding &&
                       clearanceAboveViewportBottom >= GuideScrollBottomPadding &&
                       viewport.yMax + 9f <= closeButton.yMin;
            });
        }

        public bool VerifyGuideOracleCopyFitsForQa()
        {
            var savedLanguage = GameLocalization.Current;
            var fits = true;
            foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English })
            {
                GameLocalization.Current = language;
                var skill = L($"스킬: {GuideSkillName(UnitArchetype.Oracle)} — " +
                              GuideSkillEffect(UnitArchetype.Oracle),
                    $"SKILL: {GuideSkillName(UnitArchetype.Oracle)} — " +
                    GuideSkillEffect(UnitArchetype.Oracle));
                var ultimate = L($"영웅 궁극기: {GuideUltimateName(UnitArchetype.Oracle)} — " +
                                 GuideUltimateEffect(UnitArchetype.Oracle),
                    $"HERO ULTIMATE: {GuideUltimateName(UnitArchetype.Oracle)} — " +
                    GuideUltimateEffect(UnitArchetype.Oracle));
                var style = GuideBodyStyle(11, TextAnchor.UpperLeft);
                foreach (var textWidth in new[] { 250f, 324f })
                    fits &= style.CalcHeight(new GUIContent(skill), textWidth) <= 52f &&
                            style.CalcHeight(new GUIContent(ultimate), textWidth) <= 54f;
            }
            GameLocalization.Current = savedLanguage;
            return fits;
        }

        private IEnumerator QaGuideUnitScroll2682Routine()
        {
            yield return null;
            var lastUnitIsOracle = GuideUnits[^1] == UnitArchetype.Oracle;
            var layoutSafe = VerifyGuideUnitLastCardClearanceForQa();
            var contentHeight = GuideContentHeight(1);
            var expectedMinimum = GuideLastUnitCardRect(372f).yMax + GuideScrollBottomPadding;
            var copyFits = VerifyGuideOracleCopyFitsForQa();
            var passed = lastUnitIsOracle && layoutSafe && copyFits && contentHeight >= expectedMinimum;
            Debug.Log($"QA_GUIDE_UNIT_SCROLL_2682 passed={passed} last=Oracle:{lastUnitIsOracle} " +
                      $"content={contentHeight:0} minimum={expectedMinimum:0} safe={layoutSafe} copy={copyFits}");
            Application.Quit(passed ? 0 : 82);
        }

        private static Rect GuideUnitPortraitFrameRect(Rect card) =>
            new(card.x + 10f, card.y + 10f, 132f, 120f);

        private static Rect GuideUnitPortraitRect(Rect card)
        {
            var frame = GuideUnitPortraitFrameRect(card);
            return new Rect(frame.x + 10f, frame.y + 9f, frame.width - 20f, frame.height - 19f);
        }

        private void DrawGuideEnemyRoster(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("핵심 적 40종 · 전술 특수병 3종", "40 CORE ENEMIES · 3 TACTICAL SPECIALISTS"));
            GUI.Label(new Rect(8f, y, width - 16f, 48f),
                L("각 계열은 4종의 일반 적을 순서대로 추가하며 다섯 번째 라운드에 보스가 등장합니다. 아래 설명은 실제 전투 등급에 따른 기본 공격과 일반 스킬입니다.",
                    "Each family adds four regular enemies in sequence, then a boss on its fifth round. Entries below use each variant's live combat class for attacks and skills."),
                GuideBodyStyle(13, TextAnchor.UpperLeft));
            y += 57f;
            for (var chapter = 0; chapter < GuideEnemies.Length; chapter++)
            {
                DrawGuideChapter(width, ref y,
                    $"R{chapter * 5 + 1}-{chapter * 5 + 4} · {GuideEnemyName(GuideEnemies[chapter])}");
                for (var stage = 0; stage < 4; stage++)
                {
                    var profile = EnemyVariantCatalog.ForChapterStage(chapter, stage);
                    var rect = new Rect(4f, y, width - 8f, 120f);
                    DrawOrnatePanel(rect, new Color(.045f, .04f, .085f, .99f), profile.Accent, 2f);
                    GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 24f),
                        $"R{chapter * 5 + stage + 1} · {profile.Name}", GuideTitleStyle(14));
                    GUI.Label(new Rect(rect.x + 10f, rect.y + 33f, rect.width - 20f, 31f),
                        L($"기본 공격: {GuideEnemyBasicAttack(profile.CombatClass)}",
                            $"BASIC: {GuideEnemyBasicAttack(profile.CombatClass)}"),
                        GuideBodyStyle(10, TextAnchor.UpperLeft));
                    GUI.Label(new Rect(rect.x + 10f, rect.y + 66f, rect.width - 20f, 31f),
                        L($"스킬: {GuideEnemySkill(profile.CombatClass)}",
                            $"SKILL: {GuideEnemySkill(profile.CombatClass)}"),
                        GuideBodyStyle(10, TextAnchor.UpperLeft));
                    GUI.Label(new Rect(rect.x + 10f, rect.y + 99f, rect.width - 20f, 16f),
                        L($"체력 ×{profile.HealthMultiplier:0.00} · 공격 ×{profile.AttackMultiplier:0.00} · 마력 ×{profile.MagicMultiplier:0.00} · 속도 ×{profile.SpeedMultiplier:0.00}",
                            $"HP ×{profile.HealthMultiplier:0.00} · ATK ×{profile.AttackMultiplier:0.00} · MAGIC ×{profile.MagicMultiplier:0.00} · SPEED ×{profile.SpeedMultiplier:0.00}"),
                        GuideBodyStyle(9, TextAnchor.UpperLeft));
                    y += 128f;
                }
            }

            DrawGuideChapter(width, ref y, L("전술 특수병", "TACTICAL SPECIALISTS"));
            foreach (var profile in EnemyVariantCatalog.SpecialProfiles)
            {
                var roundLabel = profile.Id switch
                {
                    "veil_binder" => "R8-10",
                    "armor_render" => "R28-30",
                    _ => "R38-40"
                };
                var rect = new Rect(4f, y, width - 8f, 120f);
                DrawOrnatePanel(rect, new Color(.035f, .045f, .085f, .99f), profile.Accent, 2f);
                GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 24f),
                    $"{roundLabel} · {profile.Name}", GuideTitleStyle(14));
                GUI.Label(new Rect(rect.x + 10f, rect.y + 33f, rect.width - 20f, 31f),
                    L($"기본 공격: {GuideEnemyBasicAttack(profile.CombatClass)}",
                        $"BASIC: {GuideEnemyBasicAttack(profile.CombatClass)}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft));
                GUI.Label(new Rect(rect.x + 10f, rect.y + 66f, rect.width - 20f, 46f),
                    L($"스킬: {GuideEnemySkill(profile.CombatClass)}",
                        $"SKILL: {GuideEnemySkill(profile.CombatClass)}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft));
                y += 128f;
            }
        }

        private void DrawGuideBossRoster(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("보스 10종 · 기본 공격·스킬·지속 효과", "10 BOSSES · BASIC·SKILL·PASSIVE"));
            for (var chapter = 0; chapter < GuideEnemies.Length; chapter++)
            {
                var family = GuideEnemies[chapter];
                var variant = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var identity = BossIdentityCatalog.For(family);
                var rect = new Rect(4f, y, width - 8f, 238f);
                DrawOrnatePanel(rect, new Color(.062f, .025f, .07f, .995f), identity.Accent, 3f);
                var sprite = GuideBossPortraitSprite(chapter);
                var portraitFrame = GuideBossPortraitFrameRect(rect);
                var portraitRect = GuideBossPortraitRect(rect);
                DrawOrnatePanel(portraitFrame, new Color(.018f, .025f, .055f, .98f),
                    Color.Lerp(identity.Accent, Color.white, .28f), 2f);
                if (sprite != null)
                    DrawSpriteContained(portraitRect, sprite);
                DrawPanel(new Rect(portraitFrame.x + 7f, portraitFrame.y + 7f, 4f, 4f),
                    new Color(1f, .91f, .62f, .9f));
                DrawPanel(new Rect(portraitFrame.xMax - 11f, portraitFrame.y + 7f, 4f, 4f),
                    new Color(1f, .91f, .62f, .9f));
                var textX = portraitFrame.xMax + 10f;
                var titleRect = new Rect(textX, rect.y + 7f, rect.xMax - textX - 10f, 39f);
                DrawFittedWrappedLabel(titleRect, $"BOSS R{(chapter + 1) * 5} · {variant.Name}",
                    GuideTitleStyle(15), 10);
                DrawFittedWrappedLabel(new Rect(textX, rect.y + 50f, rect.xMax - textX - 10f, 68f),
                    L($"기본 공격: {GuideEnemyBasicAttack(variant.CombatClass)}",
                        $"BASIC: {GuideEnemyBasicAttack(variant.CombatClass)}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft), 8);
                DrawFittedWrappedLabel(new Rect(rect.x + 12f, rect.y + 140f, rect.width - 24f, 39f),
                    L($"고유 스킬: {GuideBossActiveName(family)} — {GuideBossSkillEffect(family)}",
                        $"SIGNATURE: {GuideBossActiveName(family)} — {GuideBossSkillEffect(family)}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft), 8);
                DrawFittedWrappedLabel(new Rect(rect.x + 12f, rect.y + 184f, rect.width - 24f, 47f),
                    L($"지속 효과: {identity.PassiveName} — {identity.PassiveDescription}",
                        $"PASSIVE: {identity.PassiveName} — {identity.PassiveDescription}"),
                    GuideBodyStyle(10, TextAnchor.UpperLeft), 8);
                y += 247f;
            }
        }

        private Sprite GuideBossPortraitSprite(int chapter)
        {
            var profile = EnemyVariantCatalog.ForChapterStage(Mathf.Clamp(chapter, 0, 9), 4);
            // The guide needs the full authored body. Combat direction cells can extend into the
            // next animation row and are intentionally cropped around the feet; using one here
            // cut the goblin warchief and other tall bosses inside the card.
            return GetEnemyVariantSprite(profile, true);
        }

        private static Rect GuideBossPortraitFrameRect(Rect card)
        {
            var width = Mathf.Clamp(card.width * .3f, 96f, 116f);
            return new Rect(card.x + 10f, card.y + 10f, width, 120f);
        }

        private static Rect GuideBossPortraitRect(Rect card)
        {
            var frame = GuideBossPortraitFrameRect(card);
            // Twelve logical pixels on every edge remain visible around the complete opaque body.
            // The portrait therefore communicates an intentional frame instead of an accidental
            // crop, even for the Ent's canopy and the Iron Colossus' wide fists.
            return new Rect(frame.x + 12f, frame.y + 11f, frame.width - 24f, frame.height - 23f);
        }

        private static string GuideEnemyBasicAttack(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Runner => L("빠른 근접 물리 공격", "Fast melee physical strike"),
            EnemyClass.Brute => L("느리지만 강한 근접 물리 강타", "Slow heavy melee physical smash"),
            EnemyClass.Shaman => L("마력 기반 원거리 주술탄", "Ranged Magic hex bolt"),
            EnemyClass.Siege => L("긴 사거리의 공성 마법탄", "Long-range siege magic shell"),
            EnemyClass.Mage => L("마력 기반 원거리 투사체", "Ranged Magic projectile"),
            EnemyClass.Piercer => L("방어력을 무시하는 근접 순수 피해", "Melee pure damage that bypasses Armor"),
            EnemyClass.Wisp => L("언덕도 노리는 원거리 마법 공격", "Ranged Magic attack that can target high ground"),
            EnemyClass.Flyer => L("지형을 넘어 공중에서 가하는 물리 공격", "Airborne physical strike ignoring terrain"),
            EnemyClass.Silencer => L("마법사를 우선 노리는 원거리 영혼 공격", "Ranged spirit attack prioritizing mages"),
            EnemyClass.Cursebinder => L("마법 저항을 흔드는 원거리 저주 공격", "Ranged curse attack pressuring Magic Resistance"),
            EnemyClass.Sunderer => L("양날 절단기로 가하는 근접 물리 공격", "Melee physical strike with twin shears"),
            _ => L("한 대상을 치는 근접 물리 공격", "Single-target melee physical strike")
        };

        private static string GuideEnemySkill(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Runner => L("질주 타격: 빠르게 파고들어 좁은 범위를 강타", "Rush hit: dives in and strikes a narrow area"),
            EnemyClass.Brute => L("분쇄 강타: 넓은 범위 물리 피해와 기절", "Crushing slam: wide physical damage and stun"),
            EnemyClass.Shaman => L("저주 폭발: 넓은 범위 마법 피해", "Hex burst: wide Magic damage"),
            EnemyClass.Siege => L("공성 포격: 가장 넓은 범위의 원거리 마법 폭발", "Siege barrage: largest ranged Magic blast"),
            EnemyClass.Mage => L("비전 폭발: 대상 주변 마법 피해", "Arcane burst: Magic damage around the target"),
            EnemyClass.Piercer => L("관통 돌격: 좁은 범위 순수 피해", "Piercing charge: narrow pure-damage impact"),
            EnemyClass.Wisp => L("위상 폭발: 언덕을 포함한 범위 마법 피해", "Phase burst: area Magic damage including high ground"),
            EnemyClass.Flyer => L("급강하: 대상 지점에 공중 물리 폭발", "Dive strike: airborne physical area impact"),
            EnemyClass.Silencer => L("침묵의 종가름: 마법사의 스킬과 궁극기를 잠시 봉인", "Silencing Toll: briefly seals mage skills and ultimates"),
            EnemyClass.Cursebinder => L("봉인 저주: 마법 저항력을 5.2초 동안 감소", "Veil Curse: reduces Magic Resistance for 5.2 seconds"),
            EnemyClass.Sunderer => L("갑주 절단: 근접 피해 후 방어력을 5초 동안 감소", "Armor Rend: melee hit followed by 5 seconds of Armor reduction"),
            _ => L("전열 강타: 주변에 물리 피해", "Frontline smash: physical damage around the target")
        };

        private static string GuideBossSkillEffect(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Melee => L("몸을 응축한 뒤 젤리 호위병을 분열 소환합니다.", "Compresses, then splits off Jelly escorts."),
            EnemyClass.Skeleton => L("전장에 해골 병사 셋을 소환합니다.", "Summons three Skeleton soldiers onto the field."),
            EnemyClass.Runner => L("직선 경로를 돌진해 전열을 무너뜨립니다.", "Charges down a line to break the frontline."),
            EnemyClass.Brute => L("지면을 내려찍어 넓은 물리 피해와 기절을 줍니다.", "Slams the ground for wide physical damage and stun."),
            EnemyClass.Shaman => L("대상 지역을 저주하고 피해 일부를 회복합니다.", "Hexes a target area and recovers part of its strength."),
            EnemyClass.Siege => L("세 지점을 차례로 포격하고 방벽을 얻습니다.", "Bombards three positions and gains a barrier."),
            EnemyClass.Piercer => L("한 지점을 꿰뚫어 강한 순수 피해를 줍니다.", "Impales one position for heavy pure damage."),
            EnemyClass.Wisp => L("여러 지점에 성운 폭풍을 일으킵니다.", "Creates astral storms at several positions."),
            EnemyClass.Flyer => L("네 지점에 연속 급강하 공격을 가합니다.", "Performs chained skyfall attacks on four positions."),
            _ => L("세 개의 비전 프리즘으로 범위 마법 피해를 줍니다.", "Strikes three areas with arcane prisms.")
        };

        private static string GuideBossActiveName(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Melee => L("왕실 분열", "ROYAL DIVISION"),
            EnemyClass.Skeleton => L("망자의 군단", "LEGION OF THE DEAD"),
            EnemyClass.Runner => L("진홍 선봉 돌진", "CRIMSON VANGUARD"),
            EnemyClass.Brute => L("대지 분쇄", "EARTH SHATTER"),
            EnemyClass.Shaman => L("선조의 저주", "ANCESTRAL HEX"),
            EnemyClass.Siege => L("공허 포격", "VOID BARRAGE"),
            EnemyClass.Piercer => L("고룡 관통술", "ANCIENT DRAGON IMPALE"),
            EnemyClass.Wisp => L("성운 폭풍", "ASTRAL TEMPEST"),
            EnemyClass.Flyer => L("공중 사냥", "SKYFALL HUNT"),
            _ => L("비전 프리즘", "ARCANE PRISM")
        };

        private void DrawGuideCombatRules(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("피해와 방어", "DAMAGE & DEFENCE"));
            DrawGuideCard(width, ref y, L("물리 피해", "PHYSICAL DAMAGE"),
                L("공격력 기반 피해는 방어력으로 감소합니다. 현재 감소 배율은 100 ÷ (100 + 방어력)입니다. 방어 관통형 적은 이 방어 계산을 무시합니다.",
                    "Attack-based damage is reduced by Armor. The live multiplier is 100 / (100 + Armor). Piercing enemies bypass this Armor calculation."), 101f);
            DrawGuideCard(width, ref y, L("마법 피해", "MAGIC DAMAGE"),
                L("마력 기반 피해는 마법 저항으로 감소하며 같은 공식을 사용합니다. 마법사는 약한 마력 기반 평타와 강력한 스킬을 조합합니다. 위습은 높은 방어력과 낮은 마저를 가져 마법 대응이 핵심입니다.",
                    "Magic-based damage is reduced by Resistance with the same formula. Mages combine weak magic basic attacks with strong skills. Wisps have extreme Armor and low Resistance, demanding magic counters."), 118f);
            DrawGuideCard(width, ref y, L("순수 피해", "PURE DAMAGE"),
                L("순수 피해는 방어력과 마법 저항을 모두 무시합니다. 타격 순간에는 색상별 적중 섬광, 충격 링, 파편, 피해 숫자와 화면 흔들림이 함께 발생합니다.",
                    "Pure damage ignores both Armor and Resistance. Hits combine color-coded flashes, impact rings, fragments, damage numbers and controlled screen shake."), 101f);

            DrawGuideChapter(width, ref y, L("성장과 지형", "GROWTH & TERRAIN"));
            DrawGuideCard(width, ref y, L("경험치와 영웅 진화", "XP & HERO EVOLUTION"),
                L("적 처치에 기여한 아군은 직접 마지막 공격을 하지 않아도 경험치를 얻습니다. 방패병은 피해를 받아 아군을 지킨 기여도도 반영됩니다. 5레벨이 되면 영웅 외형, 강화 스킬과 궁극기가 활성화됩니다.",
                    "Defenders that contribute to a defeat gain XP even without delivering the final attack. Tanks also gain credit for protecting allies by absorbing damage. Level 5 unlocks hero art, enhanced skills and an ultimate."), 116f);
            DrawGuideCard(width, ref y, L("스킬 쿨타임", "SKILL COOLDOWNS"),
                L("배치 단계에서는 스킬 쿨타임이 돌지 않습니다. 웨이브 중 스킬을 실제로 사용한 순간부터 다음 쿨타임이 시작됩니다. 궁극기도 영웅 상태에서 같은 원칙을 따릅니다.",
                    "Cooldowns do not tick during deployment. A skill's next cooldown begins only after the skill is actually cast during a wave. Hero ultimates follow the same rule."), 105f);
            DrawGuideCard(width, ref y, L("언덕", "HIGH GROUND"),
                L("길 유닛은 언덕에 올라갈 수 없고, 언덕에 소환한 지상 유닛은 내려갈 수 없습니다. 근접 지상 적은 언덕을 공격하지 못하지만 원거리와 공중 적은 공격할 수 있습니다.",
                    "Road units cannot climb high ground, while ground units deployed on high ground cannot descend. Ground melee enemies cannot strike them, but ranged and flying enemies can."), 107f);
            DrawGuideCard(width, ref y, L("성벽 공격 우선순위", "GATE TARGET PRIORITY"),
                L("성에 도달한 적은 한 번만 피해를 주는 것이 아니라 공격 주기마다 지속 피해를 줍니다. 성을 공격하기 시작한 적은 주변 아군보다 성 공격을 우선하며, 원거리 적은 자기 사거리에서 성을 공격합니다.",
                    "Enemies at the citadel deal repeating attacks, not one contact hit. Once attacking the gate they prioritize it over nearby defenders, while ranged enemies bombard it from their own range."), 116f);
        }

        private void DrawGuideAugmentsAndStore(float width, ref float y)
        {
            DrawGuideChapter(width, ref y, L("증강 규칙", "AUGMENT RULES"));
            DrawGuideCard(width, ref y, L("등급 선택", "RARITY ROLL"),
                L("라운드를 완료하면 먼저 브론즈·실버·골드·플래티넘·다이아 중 하나의 등급이 정해집니다. 화면의 세 선택지는 모두 정해진 등급 안에서만 구성됩니다.",
                    "After a round, one rarity is selected first: Bronze, Silver, Gold, Platinum or Diamond. All three choices then come from that same rarity."), 108f);
            DrawGuideCard(width, ref y, L("등급 가중치", "RARITY WEIGHTS"),
                L("다음 등급은 이전에 선택한 증강 등급에 따라 가중치가 달라집니다. 같은 결과가 단순 반복되지 않도록 등급 흐름이 조정되며, 보스 라운드를 완료하면 상위 등급 가중치가 높아집니다.",
                    "The previous augment rarity changes the weighting of the next roll. The flow discourages simple repetition, and clearing a boss round increases the weight of higher rarities."), 116f);
            DrawGuideCard(width, ref y, L("중첩과 구성", "STACKING & CONTENT"),
                L("수치형 증강은 중첩되며 중첩할수록 증가량이 완만해집니다. 액티브 스킬, 복제, 마법 재현, 도탄 사격과 신규 유닛 편입은 한 번 획득하면 같은 플레이에서 다시 등장하지 않습니다.",
                    "Numeric augments stack with diminishing gains. Active skills, duplication, spell reprise, ricochet and recruit contracts disappear from the pool once acquired."), 120f);
            DrawGuideCard(width, ref y, L("다섯 병과", "FIVE ROLES"),
                L("탱커는 피해 흡수·반격, 근접은 방어 파쇄·처형, 원거리는 사거리·관통·도탄, 마법사는 범위·마법 재현, 서포터는 회복·재사용 대기시간을 중심으로 강화됩니다.",
                    "Tank augments focus on mitigation and counters; Melee on shatter and execution; Ranged on range, criticals and ricochet; Mage on area and reprise; Support on healing and cooldowns."), 116f);
            DrawGuideCard(width, ref y, L("언덕 증강", "HIGH-GROUND AUGMENTS"),
                L("언덕 전용 방어·공격·사거리·공격 속도 강화가 등급별로 존재하며, 다이아 등급의 언덕 지배는 여러 효과를 한 번에 강화합니다.",
                    "Tiered high-ground augments improve defence, damage, range and attack speed. Diamond High-Ground Dominion combines several bonuses at once."), 96f);

            foreach (var tier in new[] { AugmentTier.Bronze, AugmentTier.Silver, AugmentTier.Gold,
                         AugmentTier.Platinum, AugmentTier.Diamond })
            {
                DrawGuideChapter(width, ref y, GuideAugmentTierName(tier));
                var templates = GetAugmentPool(tier)
                    .Where(template => !TryGetUnlockUnit(template.EffectKey, out _))
                    .Concat(FixedRecruitAugments(tier))
                    .GroupBy(template => template.EffectKey)
                    .Select(group => group.First());
                foreach (var template in templates)
                {
                    var power = TierPower(tier);
                    var title = GameLocalization.AugmentName(template.EffectKey, template.Name);
                    var description = GameLocalization.AugmentDescription(template.EffectKey, power,
                        DescribeAugment(template, power));
                    DrawGuideCard(width, ref y, title, description, 72f);
                }
            }
        }

        private static string GuideAugmentTierName(AugmentTier tier) => tier switch
        {
            AugmentTier.Bronze => L("브론즈 증강 전체", "ALL BRONZE AUGMENTS"),
            AugmentTier.Silver => L("실버 증강 전체", "ALL SILVER AUGMENTS"),
            AugmentTier.Gold => L("골드 증강 전체", "ALL GOLD AUGMENTS"),
            AugmentTier.Platinum => L("플래티넘 증강 전체", "ALL PLATINUM AUGMENTS"),
            _ => L("다이아 증강 전체", "ALL DIAMOND AUGMENTS")
        };

        private void DrawGuideChapter(float width, ref float y, string title)
        {
            DrawPanel(new Rect(4f, y, width - 8f, 32f), new Color(.04f, .15f, .16f, .96f));
            GUI.Label(new Rect(12f, y, width - 24f, 32f), title,
                GuideTitleStyle(16, TextAnchor.MiddleLeft));
            y += 42f;
        }

        private void DrawGuideCard(float width, ref float y, string title, string body, float height)
        {
            var rect = new Rect(4f, y, width - 8f, height);
            DrawFramedPanel(rect, new Color(.025f, .055f, .105f, .985f),
                new Color(.28f, .52f, .68f), 2f);
            GUI.Label(new Rect(rect.x + 11f, rect.y + 7f, rect.width - 22f, 25f), title,
                GuideTitleStyle(14, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(rect.x + 11f, rect.y + 34f, rect.width - 22f, rect.height - 40f), body,
                GuideBodyStyle(12, TextAnchor.UpperLeft));
            y += height + 9f;
        }

        private GUIStyle GuideTitleStyle(int size, TextAnchor alignment = TextAnchor.MiddleLeft) =>
            new GUIStyle(smallStyle)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = alignment,
                normal = { textColor = new Color(.9f, .98f, 1f) }
            };

        private GUIStyle GuideBodyStyle(int size, TextAnchor alignment) =>
            new GUIStyle(statStyle)
            {
                fontSize = size,
                alignment = alignment,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(.78f, .88f, .95f) }
            };

        private static string GuideUnitName(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("왕관 방패병", "CROWN SHIELD GUARD"),
            UnitArchetype.Melee => L("대지 망치병", "EARTHSHAKER GUARD"),
            UnitArchetype.Archer => L("바람길 궁수", "GALE PATHFINDER"),
            UnitArchetype.AreaMage => L("별가루 범위 마법사", "STARDUST AREA MAGE"),
            UnitArchetype.SingleMage => L("유리구슬 단일 마법사", "GLASS ORB MAGE"),
            UnitArchetype.Bombardier => L("시계태엽 포병", "CLOCKWORK BOMBARDIER"),
            UnitArchetype.Lancer => L("용맥 창기병", "DRAGONVEIN LANCER"),
            UnitArchetype.Druid => L("숲의 정령술사", "GROVE SPIRITCALLER"),
            UnitArchetype.Musketeer => L("왕실 머스킷병", "ROYAL MUSKETEER"),
            _ => L("달빛 예언자", "MOONLIGHT ORACLE")
        };

        private static string GuideUnitRoleOnly(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("병과: 탱커 · 전열 방어 및 아군 보호", "CLASS: TANK · FRONTLINE DEFENCE AND ALLY PROTECTION"),
            UnitArchetype.Melee => L("병과: 근접 · 범위 물리 공격", "CLASS: MELEE · AREA PHYSICAL DAMAGE"),
            UnitArchetype.Archer => L("병과: 원거리 · 장거리 고속 단일 물리 공격", "CLASS: RANGED · RAPID SINGLE-TARGET PHYSICAL DAMAGE"),
            UnitArchetype.AreaMage => L("병과: 마법사 · 저속 범위 마법", "CLASS: MAGE · SLOW AREA MAGIC"),
            UnitArchetype.SingleMage => L("병과: 마법사 · 고위력 단일 마법", "CLASS: MAGE · HEAVY SINGLE-TARGET MAGIC"),
            UnitArchetype.Bombardier => L("병과: 원거리 · 최장거리 범위 포격", "CLASS: RANGED · EXTREME-RANGE AREA BOMBARDMENT"),
            UnitArchetype.Lancer => L("병과: 근접 · 고속 돌진 물리 공격", "CLASS: MELEE · FAST CHARGE PHYSICAL DAMAGE"),
            UnitArchetype.Druid => L("병과: 서포터 · 범위 마법 및 아군 지원", "CLASS: SUPPORT · AREA MAGIC AND ALLY SUPPORT"),
            UnitArchetype.Musketeer => L("병과: 원거리 · 초장거리 단일 물리 공격", "CLASS: RANGED · VERY LONG-RANGE PHYSICAL DAMAGE"),
            _ => L("병과: 마법사 · 단일 마법 및 전투 지원",
                "CLASS: MAGE · SINGLE-TARGET MAGIC AND BATTLE SUPPORT")
        };

        private static string GuideBasicAttack(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("방패 뒤에서 검으로 한 대상을 공격하는 물리 근접 공격",
                "Physical melee strike against one target while braced behind the shield"),
            UnitArchetype.Melee => L("망치 충격이 주 대상과 가까운 적에게 일부 확산되는 물리 공격",
                "Physical hammer blow whose shock carries partial damage to nearby enemies"),
            UnitArchetype.Archer => L("긴 사거리에서 빠르게 화살을 발사하는 단일 물리 공격",
                "Rapid single-target physical arrows from long range"),
            UnitArchetype.AreaMage => L("마력 기반의 약한 별가루 탄이 좁은 범위에 피해를 주는 공격",
                "Weak Magic-based stardust bolt with a small impact area"),
            UnitArchetype.SingleMage => L("느리지만 비교적 강한 단일 마력 탄환",
                "Slow but comparatively strong single-target Magic bolt"),
            UnitArchetype.Bombardier => L("가장 긴 사거리에서 포탄이 폭발하는 혼합 범위 공격",
                "Extreme-range shell with a mixed area explosion"),
            UnitArchetype.Lancer => L("창끝으로 한 대상을 찌르는 빠른 물리 근접 공격",
                "Fast single-target physical spear thrust"),
            UnitArchetype.Druid => L("정령탄이 좁은 범위에 약한 마법 피해를 주는 공격",
                "Spirit bolt dealing light Magic damage in a small area"),
            UnitArchetype.Musketeer => L("초장거리에서 한 대상에게 높은 물리 피해를 주는 사격",
                "Very-long-range shot dealing high physical damage to one target"),
            _ => L("월광 구체가 대상 주변에 약한 마법 피해를 주는 공격",
                "Moon orb dealing light Magic damage around its target")
        };

        private static string GuideSkillName(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("왕실 방패", "ROYAL BULWARK"),
            UnitArchetype.Melee => L("태엽 회전격", "WIND-UP WHIRL"),
            UnitArchetype.Archer => L("관통 연사", "PIERCING VOLLEY"),
            UnitArchetype.AreaMage => L("별무리 폭발", "STAR CLUSTER"),
            UnitArchetype.SingleMage => L("수정 창", "CRYSTAL LANCE"),
            UnitArchetype.Bombardier => L("연금 폭격", "ALCHEMICAL BARRAGE"),
            UnitArchetype.Lancer => L("초승달 돌진", "CRESCENT CHARGE"),
            UnitArchetype.Druid => L("꽃잎 결계", "PETAL WARD"),
            UnitArchetype.Musketeer => L("정밀 일제사", "AIMED VOLLEY"),
            UnitArchetype.Oracle => L("달빛 파동", "MOONLIGHT PULSE"),
            _ => L("전투 기술", "COMBAT SKILL")
        };

        private static string GuideSkillEffect(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("4.2초 동안 받는 피해 38% 감소, 방어력 +28, 마법 저항 +24를 얻습니다.",
                "For 4.2s, gains 38% damage reduction, +28 Armor and +24 Resistance."),
            UnitArchetype.Melee => L("주변을 강타해 방어력 16을 4.5초간 파괴하고 0.58초 기절시킵니다.",
                "Smashes nearby enemies, breaking 16 Armor for 4.5s and stunning for 0.58s."),
            UnitArchetype.Archer => L("직선상의 적들을 관통해 공격합니다.", "Fires through every enemy along a line."),
            UnitArchetype.AreaMage => L("넓은 범위에 강한 마법 폭발을 일으킵니다.", "Creates a powerful magic explosion over a wide area."),
            UnitArchetype.SingleMage => L("한 대상을 향해 고위력 마법 투사체를 발사합니다.",
                "Launches a high-power magic projectile at one target."),
            UnitArchetype.Bombardier => L("지정 지점에 여러 차례 범위 포격을 가합니다.",
                "Bombards the target area multiple times."),
            UnitArchetype.Lancer => L("적진을 빠르게 돌파하며 경로의 적을 공격합니다.",
                "Charges through the enemy line and damages targets along the path."),
            UnitArchetype.Druid => L("범위 피해와 함께 주변 아군을 보호합니다.",
                "Deals area damage while protecting nearby allies."),
            UnitArchetype.Musketeer => L("한 대상에게 높은 물리 피해를 주는 정밀 사격입니다.",
                "A precision shot that deals heavy physical damage to one target."),
            UnitArchetype.Oracle => L("대상 주변에 마법 피해를 주고, 가까운 아군을 회복하며 스킬 재사용을 앞당깁니다.",
                "Damages the target area, heals nearby allies and advances their skill recovery."),
            _ => L("유닛 고유의 전투 기술을 사용합니다.", "Uses the unit's unique combat skill.")
        };

        private static string GuideUltimateName(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("왕성의 천벽", "CITADEL SKY-WALL"),
            UnitArchetype.Melee => L("황금 태엽 난무", "GOLDEN WIND-UP STORM"),
            UnitArchetype.Archer => L("왕실 화살비", "ROYAL ARROW RAIN"),
            UnitArchetype.AreaMage => L("초신성 낙하", "SUPERNOVA FALL"),
            UnitArchetype.SingleMage => L("절대 수정창", "ABSOLUTE CRYSTAL LANCE"),
            UnitArchetype.Bombardier => L("왕실 전탄 포격", "ROYAL FULL SALVO"),
            UnitArchetype.Lancer => L("비취 돌격대", "JADE CHARGE LINE"),
            UnitArchetype.Druid => L("만개한 성역", "BLOOM SANCTUARY"),
            UnitArchetype.Musketeer => L("왕실 삼중 사격", "ROYAL TRIPLE SHOT"),
            _ => L("보름달 심판", "FULL-MOON JUDGMENT")
        };

        private static string GuideUltimateEffect(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("자신에게 강한 피해 감소·방어력·마법 저항력과 약화 효과 면역을 부여합니다.",
                "Greatly reinforces the shield guard itself with mitigation, Armor, Resistance and debuff immunity."),
            UnitArchetype.Melee => L("주변 전장을 휩쓰는 연속 강타를 가합니다.",
                "Unleashes a sequence of heavy strikes across the nearby battlefield."),
            UnitArchetype.Archer => L("넓은 지역에 대량의 화살을 쏟아붓습니다.",
                "Rains a large number of arrows over a wide area."),
            UnitArchetype.AreaMage => L("거대한 별을 떨어뜨려 광범위 마법 피해를 줍니다.",
                "Drops a giant star for devastating area magic damage."),
            UnitArchetype.SingleMage => L("보스급 대상에게 매우 강한 단일 마법 피해를 줍니다.",
                "Deals exceptional single-target magic damage, ideal against bosses."),
            UnitArchetype.Bombardier => L("전장 여러 지점에 대규모 연속 포격을 가합니다.",
                "Launches a massive chained barrage across multiple positions."),
            UnitArchetype.Lancer => L("돌진 거리 안에서 적이 가장 밀집된 지점을 골라 파고들어 넓게 관통합니다.",
                "Selects the densest reachable enemy cluster and dives through it with a wide piercing charge."),
            UnitArchetype.Druid => L("주변 아군의 체력을 회복하고 피해 감소·방어력·마법 저항력을 부여합니다.",
                "Heals nearby allies and grants damage reduction, Armor and Resistance."),
            UnitArchetype.Musketeer => L("강력한 세 발을 연속으로 발사해 핵심 적을 제거합니다.",
                "Fires three powerful shots to eliminate a priority target."),
            _ => L("넓은 범위의 적을 기절시키고, 주변 아군을 크게 회복합니다.",
                "Stuns enemies across a wide area and greatly heals nearby allies.")
        };

        private static string GuideUnitRole(UnitArchetype unit) => unit switch
        {
            UnitArchetype.Tank => L("전열 탱커 · 스킬: 왕실 방패 강화 · 영웅 궁극기: 왕성의 천벽",
                "FRONT TANK · SKILL: ROYAL BULWARK · HERO ULT: CITADEL SKY-WALL"),
            UnitArchetype.Melee => L("근접 물리 딜러 · 스킬: 회전 강타 · 영웅 궁극기: 황금 망치 폭풍",
                "MELEE PHYSICAL DPS · SKILL: WHIRL STRIKE · HERO ULT: GOLDEN HAMMERSTORM"),
            UnitArchetype.Archer => L("장거리 고속 연사·보통 단발 · 스킬: 관통 사격 · 영웅 궁극기: 왕실 화살비",
                "LONG-RANGE RAPID FIRE·MODERATE HIT · SKILL: PIERCING SHOT · HERO ULT: ROYAL ARROW RAIN"),
            UnitArchetype.AreaMage => L("단거리 저속·범위 폭발 · 스킬: 별무리 폭발 · 영웅 궁극기: 초신성 강하",
                "SHORT-RANGE SLOW AREA BURST · SKILL: STAR CLUSTER · HERO ULT: SUPERNOVA FALL"),
            UnitArchetype.SingleMage => L("중거리 저속·초고위력 단일 마법 · 스킬: 수정창 · 영웅 궁극기: 칠중 수정창",
                "MID-RANGE SLOW HEAVY MAGIC · SKILL: CRYSTAL LANCE · HERO ULT: SEVENFOLD LANCE"),
            UnitArchetype.Bombardier => L("최장거리 혼합 포격 · 스킬: 집중 포격 · 영웅 궁극기: 왕실 전탄 사격",
                "EXTREME-RANGE HYBRID · SKILL: BOMBARDMENT · HERO ULT: ROYAL FULL SALVO"),
            UnitArchetype.Lancer => L("고속 돌진 물리 · 스킬: 번개 돌진 · 영웅 궁극기: 비취 낙뢰선",
                "FAST CHARGE DPS · SKILL: LIGHTNING CHARGE · HERO ULT: JADE THUNDERLINE"),
            UnitArchetype.Druid => L("범위 마법·지원 · 스킬: 꽃잎 결계 · 영웅 궁극기: 만개의 성역",
                "AREA MAGIC·SUPPORT · SKILL: PETAL WARD · HERO ULT: BLOOM SANCTUARY"),
            UnitArchetype.Musketeer => L("초장거리 단일 물리 · 스킬: 정밀 사격 · 영웅 궁극기: 황동 일제사격",
                "VERY LONG-RANGE PHYSICAL · SKILL: AIMED SHOT · HERO ULT: BRASS FIRING LINE"),
            _ => L("단일 마법·전투 지원 · 스킬: 달빛 파동 · 영웅 궁극기: 보름달 심판",
                "SINGLE MAGIC·BATTLE SUPPORT · SKILL: MOONLIGHT PULSE · HERO ULT: FULL-MOON JUDGMENT")
        };

        private static string GuideEnemyName(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Melee => L("젤리 군단", "JELLY LEGION"),
            EnemyClass.Skeleton => L("해골 군단", "SKELETON LEGION"),
            EnemyClass.Runner => L("고블린 전단", "GOBLIN WARBAND"),
            EnemyClass.Brute => L("암석 군단", "STONE LEGION"),
            EnemyClass.Shaman => L("수림 군단", "GROVE LEGION"),
            EnemyClass.Siege => L("태엽 군단", "CLOCKWORK LEGION"),
            EnemyClass.Piercer => L("진홍 군단", "CRIMSON LEGION"),
            EnemyClass.Wisp => L("망령 군단", "SPECTRAL LEGION"),
            EnemyClass.Flyer => L("비공 군단", "SKY LEGION"),
            _ => L("심연 군단", "ABYSSAL LEGION")
        };

        private static string GuideEnemyDescription(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Melee => L("기본 근접 계열. 초반 전선 운영을 익히는 상대이며 보스는 증식과 충격 공격으로 진형을 압박합니다.",
                "Core melee family. Teaches early formation control; its boss pressures the line with splitting and impact attacks."),
            EnemyClass.Skeleton => L("높은 방어력의 밀집 보병. 보스 리치는 해골 하수인을 추가 소환해 물량을 늘립니다.",
                "Armored formation infantry. The Lich boss summons additional skeleton minions to expand the wave."),
            EnemyClass.Runner => L("체력은 낮지만 이동 속도가 매우 빠른 돌파형. 빈 길과 후방 노출을 즉시 파고듭니다.",
                "Low-health but very fast breakthrough units that punish open lanes and exposed backlines."),
            EnemyClass.Brute => L("높은 체력·방어력과 강한 근접 피해. 보스의 지면 강타는 넓은 충격파와 화면 흔들림을 일으킵니다.",
                "High HP, Armor and melee damage. The boss ground slam creates a wide shockwave and heavy screen impact."),
            EnemyClass.Shaman => L("마력이 높고 마법 저항도 강한 원거리 주술 계열. 물리 원거리로 빠르게 끊는 대응이 유효합니다.",
                "High-Magic ranged casters with strong Resistance. Focused physical fire is an effective counter."),
            EnemyClass.Siege => L("느리고 단단한 장거리 공성 병기. 성벽 피해가 특히 높아 접근 전에 집중 공격해야 합니다.",
                "Slow, durable long-range siege engines with exceptional gate damage. Focus them before they set up."),
            EnemyClass.Piercer => L("방어력 계산을 무시하는 물리 관통 공격. 방패병만 세우기보다 여러 공격수를 배치해 빠르게 제거해야 합니다.",
                "Physical attacks bypass Armor. Field several damage dealers to remove them quickly instead of relying only on tanks."),
            EnemyClass.Wisp => L("물리 방어가 극단적으로 높고 마법 저항은 낮습니다. 마법 피해가 핵심이며 원거리로 언덕 위 아군도 공격합니다.",
                "Extreme Armor but low Resistance. Magic is the key counter, and their ranged attacks can target high ground."),
            EnemyClass.Flyer => L("지형과 지상 차단을 무시하고 비행합니다. 원거리 아군만 대응 가능하며 공중에서 길과 언덕 모두를 공격합니다.",
                "Flies over terrain and ground blockers. Only ranged defenders can engage it, and it attacks both roads and high ground."),
            _ => L("마법·공성·특수 방어가 결합된 최종 계열입니다. 심연 군주는 넓은 마법 공격으로 전열과 후열을 동시에 압박합니다.",
                "The final family combines magic, siege and unusual defences. Its sovereign pressures both front and rear lines with wide spells.")
        };

        private static Color GuideEnemyColor(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Melee => new Color(.72f, .36f, .92f),
            EnemyClass.Mage => new Color(.35f, .65f, 1f),
            EnemyClass.Skeleton => new Color(.78f, .82f, .88f),
            EnemyClass.Runner => new Color(.42f, .86f, .3f),
            EnemyClass.Brute => new Color(.72f, .52f, .32f),
            EnemyClass.Shaman => new Color(.42f, .9f, .62f),
            EnemyClass.Siege => new Color(.72f, .52f, .94f),
            EnemyClass.Piercer => new Color(1f, .32f, .25f),
            EnemyClass.Wisp => new Color(.38f, .86f, 1f),
            _ => new Color(1f, .68f, .24f)
        };

        private IEnumerator QaGuideRoutine()
        {
            yield return null;
            showGuidePanel = true;
            var modal = !MainMenuBaseInputEnabled;
            var tabs = GuideContentHeight(0) > 900f && GuideContentHeight(4) > 3000f;
            var unitData = GuideUnits.Length == 10 && definitions.Count >= 10;
            var bossEntries = Enumerable.Range(0, 10)
                .Count(chapter => !string.IsNullOrWhiteSpace(
                    EnemyVariantCatalog.ForChapterStage(chapter, 4).Name));
            var augmentEntries = new[] { AugmentTier.Bronze, AugmentTier.Silver, AugmentTier.Gold,
                    AugmentTier.Platinum, AugmentTier.Diamond }
                .SelectMany(tier => GetAugmentPool(tier)
                    .Where(template => !TryGetUnlockUnit(template.EffectKey, out _))
                    .Concat(FixedRecruitAugments(tier))
                    .Select(template => $"{tier}:{template.EffectKey}"))
                .Distinct().Count();
            HandleBackButton();
            var back = !showGuidePanel;
            var passed = modal && tabs && unitData && bossEntries == 10 && augmentEntries >= 35 && back;
            Debug.Log($"QA_GUIDE modal={modal} tabs={tabs} units={GuideUnits.Length} " +
                      $"enemySection=boss-only:boss={bossEntries} " +
                      $"augments={augmentEntries} back={back}");
            Application.Quit(passed ? 0 : 34);
        }

        private IEnumerator QaGuideViewRoutine()
        {
            yield return null;
            showMainMenu = true;
            showGuidePanel = true;
            guideTab = HasCommandLineArgument("-qaGuideBossView") ? 2 :
                HasCommandLineArgument("-qaGuideOracleView") ? 1 : 0;
            guideScroll = Vector2.zero;
            if (HasCommandLineArgument("-qaAutoCapture"))
            {
                while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                if (guideTab == 1)
                {
                    var viewport = CurrentGuideViewportRect();
                    guideScroll.y = Mathf.Max(0f, GuideContentHeight(guideTab) - viewport.height);
                }
                yield return new WaitForEndOfFrame();
                var capture = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", guideTab == 2
                        ? "guide-boss-v262.bmp" : guideTab == 1
                            ? "guide-oracle-v2705.bmp" : "guide-main-v262.bmp"));
                CaptureCurrentFrameForQa(capture);
                yield return new WaitForSecondsRealtime(1.2f);
                Debug.Log($"QA_GUIDE_VIEW_262 capture={capture}");
                Application.Quit(0);
            }
        }
    }
}
