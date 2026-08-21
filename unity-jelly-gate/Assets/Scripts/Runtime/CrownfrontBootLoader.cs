using System.Collections;
using System;
using UnityEngine;

namespace JellyGate
{
    /// <summary>
    /// Keeps a real illustrated loading scene visible while the runtime atlas, navigation and
    /// UI data are initialized. The percentage is runtime UI, so it remains sharp and localized.
    /// </summary>
    public sealed class CrownfrontBootLoader : MonoBehaviour
    {
        private static readonly string[] LegacyKoreanTips =
        {
            "궁수는 긴 사거리와 빠른 연사로 흩어진 적을 정리합니다.",
            "유리구슬 마법사는 느리지만 한 대상에게 매우 강한 일격을 가합니다.",
            "별가루 마법사는 짧은 사거리 대신 뭉친 적에게 강합니다.",
            "보스의 패시브와 고유 기술은 게임 정보에서 미리 확인할 수 있습니다.",
            "근접 유닛은 길목을 지키고 원거리 유닛을 보호할 때 가장 강합니다.",
            "점사 명령으로 위험한 주술사와 공성 유닛을 먼저 제거하세요.",
            "영웅은 일반 유닛보다 크고 강하며 전용 궁극기를 사용할 수 있습니다.",
            "증강 조합은 공격력뿐 아니라 성문 유지력과 군중 제어도 중요합니다.",
            "높은 지형에 배치한 유닛은 그 지형 안에서만 이동합니다.",
            "보스 웨이브에서는 좌우 호위 병력을 먼저 정리하면 전선이 안정됩니다.",
            "선택 초상화 목록을 접거나 펼쳐 전장을 더 넓게 볼 수 있습니다.",
            "50라운드를 돌파하면 아직 보유하지 않은 스킨을 우선순위에 따라 받습니다."
        };

        private static readonly string[] KoreanTips =
        {
            "궁수는 긴 사거리와 빠른 연사로 흩어진 적을 정리합니다.",
            "유리구슬 마법사는 공격이 느리지만 단일 대상에게 매우 강한 피해를 줍니다.",
            "별가루 마법사는 비교적 짧은 사거리 대신 뭉친 적에게 강합니다.",
            "배치 전에 게임 정보에서 보스의 고유 지속 효과와 기술을 확인하세요.",
            "근접 유닛은 길목을 지키고 원거리 아군을 보호할 때 가장 강합니다.",
            "집중 공격으로 위험한 주술사와 공성 유닛을 먼저 제거하세요.",
            "영웅은 더 강하며 각자 고유한 궁극기를 사용할 수 있습니다.",
            "증강은 공격뿐 아니라 성문 유지력과 군중 제어도 함께 구성하세요.",
            "언덕에 배치한 유닛은 해당 언덕의 경계 안에서만 이동합니다.",
            "보스 라운드에서는 호위 병력을 먼저 정리하면 전선이 안정됩니다.",
            "유닛을 드래그하면 여러 명을 한 번에 선택할 수 있습니다.",
            "50라운드를 돌파하면 아직 보유하지 않은 스킨을 우선 획득합니다."
        };

        private static readonly string[] EnglishTips =
        {
            "ARCHERS CLEAR SPREAD-OUT ENEMIES WITH LONG RANGE AND RAPID FIRE.",
            "GLASS ORB MAGES ATTACK SLOWLY BUT LAND DEVASTATING SINGLE-TARGET HITS.",
            "STARDUST MAGES TRADE RANGE FOR POWERFUL DAMAGE AGAINST GROUPED ENEMIES.",
            "REVIEW EACH BOSS PASSIVE AND SIGNATURE SKILL IN GAME INFO BEFORE DEPLOYING.",
            "MELEE UNITS EXCEL AT HOLDING CHOKEPOINTS AND PROTECTING RANGED ALLIES.",
            "USE FOCUS FIRE TO REMOVE DANGEROUS SHAMANS AND SIEGE UNITS FIRST.",
            "HEROES ARE LARGER, STRONGER, AND CAN UNLEASH A UNIQUE ULTIMATE.",
            "AUGMENT BUILDS NEED GATE SUSTAIN AND CROWD CONTROL AS WELL AS DAMAGE.",
            "UNITS DEPLOYED ON HIGH GROUND REMAIN WITHIN THAT TACTICAL ISLAND.",
            "CLEAR A BOSS WAVE'S SIDE ESCORTS FIRST TO STABILIZE THE FRONT.",
            "COLLAPSE OR EXPAND THE SELECTION PORTRAIT RAIL TO CONTROL SCREEN SPACE.",
            "CLEAR ROUND 50 TO RECEIVE AN UNOWNED SKIN BY REWARD PRIORITY."
        };

        private Texture2D artwork;
        private Texture2D pixel;
        private float displayedProgress;
        private float targetProgress = .04f;
        private bool finished;
        private GUIStyle statusStyle;
        private GUIStyle percentStyle;
        private GUIStyle detailStyle;
        private GUIStyle touchHintStyle;
        private int currentTipIndex = -1;
        private float lastTipChangeAt;
        private bool firstPointerChangePending = true;
        private bool qaLoadingTips;

        public static int LoadingTipCountForQa => KoreanTips.Length;
        public int CurrentTipIndexForQa => currentTipIndex;

        private void Awake()
        {
            Application.runInBackground = true;
            if (!IsQaCommandLine())
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
            }
            Screen.orientation = ScreenOrientation.Portrait;
            GameLocalization.Current = GameLocalization.LoadInitialLanguage();
            artwork = Resources.Load<Texture2D>("loading-screen-v3");
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Loading UI Pixel",
                hideFlags = HideFlags.DontSave
            };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply(false, true);
            SelectNextTip();
            displayedProgress = targetProgress;
            qaLoadingTips = HasCommandLineArgument("-qaLoadingTips");
            StartCoroutine(qaLoadingTips ? QaLoadingTipsRoutine() : BootRoutine());
        }

        private IEnumerator BootRoutine()
        {
            // Unity asset and sprite construction must stay on the main thread. Before that
            // synchronous phase begins, reserve a short frame-cooperative 4% window so Android
            // can paint the loader and dispatch even an immediate first touch to the tip card.
            const float earlyInteractiveDuration = .24f;
            var earlyInteractiveUntil = Time.realtimeSinceStartup + earlyInteractiveDuration;
            var interactiveFrames = 0;
            while (Time.realtimeSinceStartup < earlyInteractiveUntil || interactiveFrames < 4)
            {
                targetProgress = .04f;
                interactiveFrames++;
                yield return null;
            }
            targetProgress = .17f;
            yield return WaitUntilProgressIsVisible(.16f);
            targetProgress = .29f;
            yield return WaitUntilProgressIsVisible(.28f);
            var game = gameObject.AddComponent<JellyGateGame>();
            targetProgress = .34f;
            yield return StartCoroutine(game.PrewarmBossPresentations(progress =>
                targetProgress = Mathf.Lerp(.34f, .84f, progress)));
            targetProgress = .84f;

            // The atlas construction above is synchronous. Keep a real interactive interval
            // after it completes so Android can deliver at least one touch to the tip surface.
            const float interactiveFinishDuration = 1.55f;
            var finishAt = Time.realtimeSinceStartup + interactiveFinishDuration;
            while (Time.realtimeSinceStartup < finishAt)
            {
                targetProgress = Mathf.Lerp(.84f, 1f,
                    1f - (finishAt - Time.realtimeSinceStartup) / interactiveFinishDuration);
                yield return null;
            }

            targetProgress = displayedProgress = 1f;
            yield return new WaitForSecondsRealtime(.16f);
            finished = true;
            Destroy(this);
        }

        private IEnumerator WaitUntilProgressIsVisible(float minimumDisplayedProgress)
        {
            // Resource and Sprite creation in JellyGateGame.Awake is main-thread-only. Never enter
            // that synchronous section while the UI still says 4%: a displayed 4% must remain a
            // real input-dispatching state, not a stale frame masking heavy initialization.
            while (displayedProgress + .001f < minimumDisplayedProgress)
                yield return null;
        }

        private void Update()
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress,
                Time.unscaledDeltaTime * .72f);
            var touchSignal = Input.touchCount > 0 &&
                              Input.GetTouch(0).phase is TouchPhase.Began or TouchPhase.Ended;
            if (touchSignal || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
                TrySelectNextTipFromPointer();
        }

        private void SelectNextTip()
        {
            currentTipIndex = NextTipIndexForQa(currentTipIndex,
                UnityEngine.Random.Range(0, int.MaxValue));
            lastTipChangeAt = Time.unscaledTime;
        }

        private bool TrySelectNextTipFromPointer(int? deterministicRandom = null, bool bypassDebounce = false)
        {
            if (!bypassDebounce && !firstPointerChangePending &&
                Time.unscaledTime < lastTipChangeAt + .12f) return false;
            var before = currentTipIndex;
            currentTipIndex = NextTipIndexForQa(currentTipIndex,
                deterministicRandom ?? UnityEngine.Random.Range(0, int.MaxValue));
            lastTipChangeAt = Time.unscaledTime;
            firstPointerChangePending = false;
            return currentTipIndex != before;
        }

        private IEnumerator QaLoadingTipsRoutine()
        {
            yield return null;
            displayedProgress = targetProgress = .04f;
            var before = currentTipIndex;
            var touchChanged = TrySelectNextTipFromPointer(7);
            var after = currentTipIndex;
            var immediateRepeatBlocked = !TrySelectNextTipFromPointer(9);
            yield return new WaitForSecondsRealtime(.13f);
            var laterTouchChanged = TrySelectNextTipFromPointer(11);
            targetProgress = .29f;
            var deadline = Time.realtimeSinceStartup + 1f;
            while (displayedProgress < .28f && Time.realtimeSinceStartup < deadline)
                yield return null;
            var heavyInitGuard = displayedProgress >= .28f;
            var passed = touchChanged && before != after && immediateRepeatBlocked && laterTouchChanged &&
                         heavyInitGuard && LoadingTipCountForQa >= 10;
            Debug.Log($"QA_LOADING_TIPS progress={displayedProgress:0.00} firstTouch={touchChanged}:" +
                      $"before={before}:after={after} repeatBlocked={immediateRepeatBlocked} " +
                      $"laterTouch={laterTouchChanged}:heavyInitGuard={heavyInitGuard}:" +
                      $"count={LoadingTipCountForQa}");
            Application.Quit(passed ? 0 : 86);
        }

        private static bool HasCommandLineArgument(string value)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsQaCommandLine()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
                if (argument.StartsWith("-qa", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static int NextTipIndexForQa(int current, int randomValue)
        {
            var count = KoreanTips.Length;
            if (count <= 1) return 0;
            if (current < 0 || current >= count) return (randomValue & int.MaxValue) % count;
            var candidate = (randomValue & int.MaxValue) % (count - 1);
            return candidate >= current ? candidate + 1 : candidate;
        }

        private void OnGUI()
        {
            if (finished) return;
            GUI.depth = -1000;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (artwork != null) GUI.DrawTexture(screen, artwork, ScaleMode.ScaleAndCrop, true);
            else DrawRect(screen, new Color(.025f, .055f, .12f, 1f));

            var safe = Screen.safeArea;
            var scale = Mathf.Max(.78f, Screen.width / 540f);
            var safeTop = Screen.height - safe.yMax;
            var panelWidth = Mathf.Min(safe.width - 36f * scale, 468f * scale);
            var panelHeight = 158f * scale;
            // The review target is the lower-middle band, around 74% of the portrait height.
            var panel = new Rect(safe.center.x - panelWidth * .5f,
                safeTop + safe.height * .68f,
                panelWidth, panelHeight);
            // One clean card and one progress track. Previous inner rules and side caps read as
            // extra loading bars on a phone, so decoration now stays outside the information area.
            DrawRect(new Rect(panel.x - 3f * scale, panel.y + 5f * scale,
                panel.width + 6f * scale, panel.height + 4f * scale), new Color(.005f, .01f, .025f, .62f));
            DrawRect(panel, new Color(.018f, .032f, .062f, .94f));
            DrawRect(new Rect(panel.x, panel.y, panel.width, 2f * scale),
                new Color(.96f, .74f, .28f, .98f));
            DrawRect(new Rect(panel.x + 18f * scale, panel.y + 8f * scale,
                38f * scale, 1f * scale), new Color(.96f, .74f, .28f, .68f));
            DrawRect(new Rect(panel.xMax - 56f * scale, panel.y + 8f * scale,
                38f * scale, 1f * scale), new Color(.96f, .74f, .28f, .68f));

            statusStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(17f * scale),
                normal = { textColor = new Color(.92f, .96f, 1f) }
            };
            percentStyle ??= new GUIStyle(statusStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Mathf.RoundToInt(19f * scale),
                normal = { textColor = new Color(1f, .86f, .42f) }
            };
            detailStyle ??= new GUIStyle(statusStyle)
            {
                fontStyle = FontStyle.Normal,
                fontSize = Mathf.RoundToInt(11f * scale),
                wordWrap = true,
                normal = { textColor = new Color(.68f, .8f, .94f) }
            };
            touchHintStyle ??= new GUIStyle(detailStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(10f * scale),
                normal = { textColor = new Color(1f, .82f, .38f) }
            };

            var phase = displayedProgress < .2f
                ? GameLocalization.Text("왕성 기록 확인", "CHECKING CITADEL RECORDS")
                : displayedProgress < .82f
                    ? GameLocalization.Text("전투 부대와 전장 구성", "ASSEMBLING TROOPS AND BATTLEFIELD")
                    : GameLocalization.Text("성문 개방 준비", "OPENING THE CITADEL GATE");

            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 12f * scale,
                    panel.width - 36f * scale, 27f * scale),
                GameLocalization.Text("왕성 전선 준비 중", "PREPARING THE FRONT"), statusStyle);
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 39f * scale,
                    panel.width - 82f * scale, 22f * scale), phase, detailStyle);
            GUI.Label(new Rect(panel.xMax - 72f * scale, panel.y + 37f * scale,
                    54f * scale, 25f * scale),
                $"{Mathf.RoundToInt(displayedProgress * 100f)}%", percentStyle);

            var track = new Rect(panel.x + 18f * scale, panel.y + 68f * scale,
                panel.width - 36f * scale, 16f * scale);
            DrawRect(track, new Color(.006f, .012f, .03f, .98f));
            DrawRect(new Rect(track.x + 2f * scale, track.y + 2f * scale,
                track.width - 4f * scale, track.height - 4f * scale), new Color(.035f, .08f, .15f, 1f));
            var fillWidth = Mathf.Max(0f, track.width - 6f * scale) * displayedProgress;
            var fill = new Rect(track.x + 3f * scale, track.y + 3f * scale,
                fillWidth, track.height - 6f * scale);
            DrawRect(fill, Color.Lerp(new Color(.08f, .48f, 1f), new Color(1f, .72f, .18f), displayedProgress));
            if (fill.width > 8f * scale)
            {
                var shimmer = Mathf.Repeat(Time.realtimeSinceStartup * 74f * scale, fill.width);
                DrawRect(new Rect(fill.x + shimmer, fill.y, Mathf.Min(9f * scale, fill.xMax - fill.x - shimmer),
                    fill.height), new Color(1f, .95f, .68f, .45f));
            }
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 91f * scale,
                    panel.width - 36f * scale, 38f * scale),
                $"TIP {currentTipIndex + 1:00} · " + GameLocalization.Text(
                    KoreanTips[Mathf.Clamp(currentTipIndex, 0, KoreanTips.Length - 1)],
                    EnglishTips[Mathf.Clamp(currentTipIndex, 0, EnglishTips.Length - 1)]), detailStyle);
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 130f * scale,
                    panel.width - 36f * scale, 18f * scale),
                GameLocalization.Text("화면을 터치하면 다른 팁을 볼 수 있습니다",
                    "TAP ANYWHERE FOR ANOTHER TIP"), touchHintStyle);

            // Android touch is also surfaced as an IMGUI mouse event. Handling it here keeps
            // the full loading artwork tappable even when no Update occurred during sync work.
            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type is EventType.MouseDown or EventType.MouseUp &&
                screen.Contains(currentEvent.mousePosition) && TrySelectNextTipFromPointer())
                currentEvent.Use();
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (pixel == null) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }
    }
}
