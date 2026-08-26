import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const projectPath = path.dirname(toolDir);
const workspacePath = path.dirname(projectPath);
const unityRoot = "C:/Program Files/Unity/Hub/Editor/6000.0.82f1";
const unity = path.join(unityRoot, "Editor/Unity.exe");
const androidRoot = path.join(unityRoot, "Editor/Data/PlaybackEngines/AndroidPlayer");
const java = path.join(androidRoot, "OpenJDK/bin/java.exe");
const jarsigner = path.join(androidRoot, "OpenJDK/bin/jarsigner.exe");
const keytool = path.join(androidRoot, "OpenJDK/bin/keytool.exe");
const bundletool = path.join(androidRoot, "Tools/bundletool-all-1.17.2.jar");
const keystore = path.join(workspacePath, "release-keys/Crownfront-upload.keystore");
const keyInfo = path.join(workspacePath, "release-keys/IMPORTANT-Crownfront-upload-key.txt");
const output = path.join(workspacePath, "outputs/Crownfront-v1.00-code26.aab");
const logDir = path.join(workspacePath, "qa-logs/v1.00-code26");
const buildLog = path.join(workspacePath, "android-aab-build-100-code26.log");
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.mkdirSync(logDir, { recursive: true });

for (const required of [unity, java, jarsigner, keytool, bundletool, keystore, keyInfo]) {
  if (!fs.existsSync(required)) throw new Error(`Required release input missing: ${required}`);
}
const saved = {};
for (const line of fs.readFileSync(keyInfo, "utf8").split(/\r?\n/)) {
  const match = line.match(/^\s*([^:=]+?)\s*[:=]\s*(.+)$/);
  if (match) saved[match[1].trim()] = match[2].trim();
}
const keystorePassword = saved["Keystore password"];
const aliasPassword = saved["Alias password"];
const alias = saved.Alias;
if (!keystorePassword || !aliasPassword || !alias) throw new Error("Upload-key metadata is incomplete.");

spawnSync("reg.exe", ["ADD", "HKCU\\Software\\Unity Technologies\\Unity Editor 5.x",
  "/v", "SdkUseEmbedded_h968012308", "/t", "REG_DWORD", "/d", "1", "/f"],
  { windowsHide: true, encoding: "utf8" });
const build = spawnSync(unity, [
  "-batchmode", "-nographics", "-projectPath", projectPath,
  "-executeMethod", "JellyGate.Editor.JellyGateBuild.BuildAndroidAppBundle",
  "-outputPath", output, "-logFile", buildLog, "-quit"
], {
  cwd: workspacePath,
  windowsHide: true,
  encoding: "utf8",
  timeout: 40 * 60 * 1000,
  maxBuffer: 64 * 1024 * 1024,
  env: {
    ...process.env,
    CROWNFRONT_UPLOAD_KEYSTORE: keystore,
    CROWNFRONT_UPLOAD_KEYSTORE_PASS: keystorePassword,
    CROWNFRONT_UPLOAD_ALIAS: alias,
    CROWNFRONT_UPLOAD_ALIAS_PASS: aliasPassword,
    USERPROFILE: "C:/Users/Administrator",
    LOCALAPPDATA: "C:/Users/Administrator/AppData/Local",
    APPDATA: "C:/Users/Administrator/AppData/Roaming",
    TEMP: "C:/Users/Administrator/AppData/Local/Temp",
    TMP: "C:/Users/Administrator/AppData/Local/Temp",
    BEE_CACHE_DIRECTORY: path.join(workspacePath, "qa-artifacts/bee-cache")
  }
});
const buildText = fs.existsSync(buildLog) ? fs.readFileSync(buildLog, "utf8") : "";
if (!fs.existsSync(output) || /error CS|BuildFailedException|AAB build failed/.test(buildText)) {
  throw new Error(`Signed AAB build failed (exit ${build.status}). See ${buildLog}`);
}

const run = (file, args) => spawnSync(file, args, {
  cwd: workspacePath, windowsHide: true, encoding: "utf8", maxBuffer: 32 * 1024 * 1024
});
const validate = run(java, ["-jar", bundletool, "validate", `--bundle=${output}`]);
const manifestResult = run(java, ["-jar", bundletool, "dump", "manifest", `--bundle=${output}`, "--module=base"]);
const manifest = `${manifestResult.stdout || ""}\n${manifestResult.stderr || ""}`;
const signature = run(jarsigner, ["-verify", output]);
const certificateResult = run(keytool,
  ["-J-Duser.language=en", "-J-Duser.country=US", "-printcert", "-jarfile", output]);
const certificate = `${certificateResult.stdout || ""}\n${certificateResult.stderr || ""}`;
const sha256 = crypto.createHash("sha256").update(fs.readFileSync(output)).digest("hex").toUpperCase();
const checks = {
  bundleValid: validate.status === 0,
  packageValid: manifest.includes('package="com.toykingdom.jellygate"'),
  versionNameValid: manifest.includes('android:versionName="1.00"'),
  versionCodeValid: manifest.includes('android:versionCode="26"'),
  signatureValid: signature.status === 0,
  releaseCertificate: certificate.includes("CN=CROWNFRONT Upload") && !certificate.includes("CN=Android Debug"),
  billingBridgeCompiled: buildText.includes("CrownfrontMonetizationBridge") ||
    !/CrownfrontMonetizationBridge.*(?:error|failed)/i.test(buildText)
};
const passed = Object.values(checks).every(Boolean);
const report = { version: "1.00", versionCode: 26, generatedAt: new Date().toISOString(),
  passed, ...checks, package: "com.toykingdom.jellygate", sha256, bundlePath: output };
fs.writeFileSync(path.join(logDir, "play-bundle-summary.json"), JSON.stringify(report, null, 2) + "\n");
process.stdout.write(JSON.stringify(report, null, 2) + "\n");
if (!passed) process.exit(1);
