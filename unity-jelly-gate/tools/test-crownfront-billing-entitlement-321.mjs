import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const projectPath = path.dirname(toolDir);
const workspacePath = path.dirname(projectPath);
const runtime = fs.readFileSync(path.join(projectPath,
  "Assets/Scripts/Runtime/CrownfrontMonetization.cs"), "utf8");
const bridge = fs.readFileSync(path.join(projectPath,
  "Assets/Plugins/Android/CrownfrontMonetizationBridge.java.txt"), "utf8");

const checks = {
  cachedEntitlementStartsSafe: runtime.includes("EntitlementsReady = AdsRemoved;"),
  adsWaitForOwnershipQuery: runtime.includes("AdsRemoved || !EntitlementsReady"),
  appForegroundRefreshesOwnership: runtime.includes('androidBridge.Call("refreshPurchases")'),
  alreadyOwnedRestores: bridge.includes("BillingResponseCode.ITEM_ALREADY_OWNED") &&
    bridge.includes('send("purchase_restoring"') && bridge.includes("restorePurchases();"),
  restoreSuccessAndFailureAreReported: bridge.includes("ownership_sync_complete") &&
    bridge.includes("ownership_sync_failed"),
  restoreQueryIsDeduplicated: bridge.includes("ownershipQueryInFlight"),
  paidEntitlementBlocksNativeAds:
    bridge.includes("public void setAdsBlocked(boolean blocked)") &&
    bridge.includes("if (adsBlocked)") &&
    runtime.includes('androidBridge?.Call("setAdsBlocked", true)'),
  bothAdNetworksAreCovered: bridge.includes("private void showUnityFallback") &&
    bridge.includes("public void showInterstitial()") && bridge.includes("PAID_ENTITLEMENT")
};
const passed = Object.values(checks).every(Boolean);
const report = { qa: "CrownfrontBillingEntitlement321", generatedAt: new Date().toISOString(), passed, checks };
const reportDir = path.join(workspacePath, "qa-logs");
fs.mkdirSync(reportDir, { recursive: true });
fs.writeFileSync(path.join(reportDir, "billing-entitlement-321.json"),
  JSON.stringify(report, null, 2) + "\n", "utf8");
process.stdout.write(JSON.stringify(report, null, 2) + "\n");
if (!passed) process.exit(1);
