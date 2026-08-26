import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const projectPath = path.dirname(toolDir);
const workspacePath = path.dirname(projectPath);
const unityPath = "C:/Program Files/Unity/Hub/Editor/6000.0.82f1/Editor/Unity.exe";
const qaDir = path.join(workspacePath, "qa-artifacts/Crownfront-QA-321");
const qaExe = path.join(qaDir, "Crownfront-QA.exe");
const logDir = path.join(workspacePath, "qa-logs/v1.00-code26");
const buildLog = path.join(logDir, "windows-player-build.log");
fs.mkdirSync(qaDir, { recursive: true });
fs.mkdirSync(logDir, { recursive: true });

const run = (file, args, extra = {}) => spawnSync(file, args, {
  cwd: workspacePath,
  encoding: "utf8",
  windowsHide: true,
  maxBuffer: 32 * 1024 * 1024,
  timeout: 20 * 60 * 1000,
  env: {
    ...process.env,
    USERPROFILE: "C:/Users/Administrator",
    LOCALAPPDATA: "C:/Users/Administrator/AppData/Local",
    BEE_CACHE_DIRECTORY: path.join(workspacePath, "qa-artifacts/bee-cache")
  },
  ...extra
});

const build = run(unityPath, [
  "-batchmode", "-nographics", "-noUpm", "-projectPath", projectPath,
  "-executeMethod", "JellyGate.Editor.JellyGateBuild.BuildWindowsQa",
  "-outputPath", qaExe, "-logFile", buildLog, "-quit"
]);
const buildText = fs.existsSync(buildLog) ? fs.readFileSync(buildLog, "utf8") : "";
const buildPassed = fs.existsSync(qaExe) && buildText.includes("Build Finished, Result: Success.") &&
  !/error CS|Scripts have compiler errors|BuildFailedException/.test(buildText);
if (!buildPassed) {
  process.stderr.write(`QA build failed (exit ${build.status}). See ${buildLog}\n`);
  process.exit(1);
}

const probes = [
  ["all-unit-poses-320", "-qaAllUnitPoses320", "QA_ALL_UNIT_POSES_320 passed=True"],
  ["guide-english-layout", "-qaGuide", "englishLayout=True"],
  ["guide-unit-scroll", "-qaGuideUnitScroll2682", "QA_GUIDE_UNIT_SCROLL_2682 passed=True"],
  ["battlefield-sprite-307", "-qaBattlefieldSprite307", "QA_BATTLEFIELD_SPRITE_307 passed=True"],
  ["enemy-presentation-269", "-qaEnemyPresentation269", "QA_ENEMY_PRESENTATION_269 passed=True"],
  ["boss-grounding-278", "-qaBossGrounding278", "QA_BOSS_GROUNDING_278 passed=True"],
  ["unit-balance-280", "-qaUnitBalance280", "QA_UNIT_BALANCE_280 passed=True"],
  ["settlement-301", "-qaRelease301", "QA_RELEASE_301 passed=True"],
  ["unit-economy-303", "-qaRelease303", "QA_RELEASE_303 passed=True"],
  ["release-319", "-qaRelease319", "QA_RELEASE_319 passed=True"]
];
const results = [];
for (const [name, argument, pattern] of probes) {
  const runtimeLog = path.join(logDir, `${name}.log`);
  const result = run(qaExe, ["-batchmode", "-nographics", argument, "-logFile", runtimeLog]);
  const text = fs.existsSync(runtimeLog) ? fs.readFileSync(runtimeLog, "utf8") : "";
  const exceptions = (text.match(/NullReferenceException|ArgumentException|IndexOutOfRangeException/g) || []).length;
  const passed = text.includes(pattern) && exceptions === 0;
  results.push({ name, passed, exitCode: result.status, exceptions, runtimeLog });
  process.stdout.write(`${name}: passed=${passed} exit=${result.status}\n`);
}

const billing = run(process.execPath, [path.join(toolDir, "test-crownfront-billing-entitlement-321.mjs")]);
const billingPassed = billing.status === 0;
process.stdout.write(`billing-entitlement-321: passed=${billingPassed} exit=${billing.status}\n`);
const passed = buildPassed && billingPassed && results.every(result => result.passed);
const summary = { version: "1.00", versionCode: 26, generatedAt: new Date().toISOString(),
  passed, buildLog, billingPassed, probes: results };
fs.writeFileSync(path.join(logDir, "qa-summary.json"), JSON.stringify(summary, null, 2) + "\n");
if (!passed) process.exit(1);
process.stdout.write(`CROWNFRONT code 26 QA completed: ${path.join(logDir, "qa-summary.json")}\n`);
