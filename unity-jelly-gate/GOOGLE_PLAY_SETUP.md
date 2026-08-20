# CROWNFRONT Google Play setup

The APK contains Google Play Billing and Google Mobile Ads. Google-owned IDs and products must still
be created in the publisher's Play Console/AdMob accounts.

Configured in v2.53.0:

- Android package: `com.toykingdom.jellygate`

There is no in-game Google Play Games sign-in surface. Purchase ownership is restored through Google
Play Billing when the app is installed from Google Play with the same Play account.

## 1. One-time products

Create and activate these one-time products in Play Console with the exact IDs below:

- `crownfront.castle.azure`
- `crownfront.castle.ember`
- `crownfront.unit.tank.a`
- `crownfront.unit.tank.b`
- `crownfront.unit.melee.a`
- `crownfront.unit.melee.b`
- `crownfront.unit.archer.a`
- `crownfront.unit.archer.b`
- `crownfront.unit.area_mage.a`
- `crownfront.unit.area_mage.b`
- `crownfront.unit.single_mage.a`
- `crownfront.unit.single_mage.b`
- `crownfront.unit.bombardier.a`
- `crownfront.unit.bombardier.b`
- `crownfront.unit.lancer.a`
- `crownfront.unit.lancer.b`
- `crownfront.unit.druid.a`
- `crownfront.unit.druid.b`
- `crownfront.unit.musketeer.a`
- `crownfront.unit.musketeer.b`
- `crownfront.unit.oracle.a`
- `crownfront.unit.oracle.b`
- `crownfront.menu.sunrise`
- `crownfront.menu.moonlit`
- `crownfront.remove_ads_2000`

Recommended Play Console prices:

- `crownfront.remove_ads_2000`: Korea `₩4,900`, non-Korea `US$4.00`
- `crownfront.castle.*`: Korea `₩4,900`, non-Korea `US$4.00`
- `crownfront.unit.*`: Korea `₩3,900`, non-Korea `US$3.00`
- `crownfront.menu.*`: Korea `₩2,900`, non-Korea `US$2.00`

The app's fallback display prices match these values, but the checkout sheet always uses the active
price configured in Play Console.

Upload a signed AAB to Internal testing, add the purchasing Google account as a license tester, opt in,
and install the build from Google Play. A directly sideloaded APK cannot fully validate Play Billing.

## 2. AdMob

Current checked-in Android AdMob config:

- App ID: `ca-app-pub-1688606489162660~5049427486`
- Interstitial ad unit ID: `ca-app-pub-1688606489162660/1175233834`
- `useTestAds`: `false`

Before production/internal release testing:

1. Create the Android app in AdMob with package `com.toykingdom.jellygate`.
2. Create an interstitial ad unit.
3. Replace `adMobAppId` and `interstitialAdUnitId` in
   `Assets/Resources/crownfront-google-services.json` if the AdMob account uses different IDs.
4. Keep `useTestAds` as `false` for Play-installed internal/production builds.
5. During testing with production IDs, register the device as a test device.

The runtime intentionally uses Google's test interstitial ID for sideloaded APKs. It uses the
configured production interstitial only when Android reports the installer as Google Play
(`com.android.vending`). This prevents accidental live-ad serving from locally sideloaded builds.
