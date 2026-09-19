# The Kraken's Embrace AR — final device test and submission

Deadline: Sunday, 20 September 2026, 11:59 pm (Malaysia time).

## Files ready now

- Android APK: `Builds/KrakensEmbraceAR.apk` (version 1.0.2 / version code 3)
- Printable/tracked marker: `Assets/KrakensEmbrace/Art/Images/OceanTrackingMarker.png`
- Unity project folders: `Assets`, `Packages`, `ProjectSettings`
- Main scene: `Assets/Scenes/KrakensEmbraceAR.unity`
- Visual Scripting graphs: `Assets/KrakensEmbrace/VisualScripting`

APK identity and verification:

- Package: `com.utar.krakensembracear`
- Minimum Android SDK: 24
- Target Android SDK: 35
- File size: `41,633,506` bytes
- SHA-256: `5F3F52C832EA4D72002F012068D7FA580426640B6707F16B6CB3C2490521A17D`
- Final audio: `Kraken Ocean Ambience` by Johern Lim, CC BY 4.0; full attribution is in the app and `Assets/KrakensEmbrace/Audio/README.md`

## Physical marker setup

1. Print `OceanTrackingMarker.png` at 21.0 cm × 25.846 cm, or display it full-screen on a second non-glare display at the same portrait aspect ratio.
2. Keep the full compass rose, clouds, chart corner, horizon, and outer image edges visible.
3. Use even lighting. Avoid glare, deep shadows, screen reflections, and a bent print.
4. Do not test using the marker image on the same phone that runs the APK.

## Install and test matrix

Enable USB debugging, connect the Android phone, accept its RSA prompt, then install the APK. Record a pass/fail note for every row before recording the final video.

| Test | Required result |
|---|---|
| Cold start with marker absent | Camera opens; no title/instruction overlay is shown; tracked content is absent; audio is silent; the compact `Sound On` and `Info` buttons remain available. |
| First detection | Ship, moon, clouds, mist, lighting, and ocean backdrop appear at the marker; layered pirate theme and lower-volume ocean ambience start. |
| Stable movement | Content remains anchored with acceptable jitter while moving closer, farther, left, right, and obliquely. |
| Audio OFF | Label changes to `Sound Off`; both audio layers pause immediately. |
| Audio ON | Label changes to `Sound On`; both audio layers resume while the marker is tracked. |
| Info OPEN | Information panel appears and the button changes to `Close`; Johern Lim, artwork title/rights, audio title/source/licence, model credit, CC BY 4.0 licence, and source URLs are readable. |
| Info CLOSE | Tap `Close`; the panel disappears and the button returns to `Info`. |
| Tracking loss | Point completely away or cover the marker; every marker-anchored visual disappears and both audio layers pause. |
| Reacquisition | Return to the marker; content reappears in place and audio respects the last ON/OFF choice. |
| Repeated recovery | Repeat loss/reacquisition three times without duplicated models, stuck UI, or a crash. |

If detection is unreliable, first verify that the printed/displayed image is the new `OceanTrackingMarker.png`, not the older plain-ocean image.

## Unedited 4–5 minute video run sheet

Use the phone's built-in screen recorder with device audio enabled. Record one continuous take; do not cut, speed up, add titles, or splice footage.

- 0:00–0:20 — show a true cold start with the marker absent: camera view, no title/instruction overlay, and no tracked content.
- 0:20–0:55 — bring the marker into view and hold until the complete AR scene and original ocean ambience are clear.
- 0:55–1:35 — move around the marker: near/far, left/right, and an oblique angle to demonstrate stable anchoring and depth.
- 1:35–2:05 — toggle audio OFF, wait long enough to demonstrate silence, then toggle it ON.
- 2:05–2:45 — open the information panel; pause on the complete credit and source URL; close it.
- 2:45–3:25 — deliberately lose tracking; show that all tracked content disappears; reacquire the marker and show recovery.
- 3:25–4:15 — show the full 3D scene, slow ship motion, moon, clouds, particles/mist, lighting, and another stable viewing angle.
- 4:15–4:30 — finish on a steady complete-scene view. Stop before 5:00.

Suggested video filename: `UCCD3084_johernlim_ARCanvas_Demo.mp4`.

## Shared-drive upload and Weble submission

1. Create one shared-drive folder named `UCCD3084_johernlim_ARCanvas`.
2. Upload `Assets`, `Packages`, `ProjectSettings`, `Builds/KrakensEmbraceAR.apk`, and the final MP4.
3. Confirm every upload has finished and the uploaded APK size is 41,633,506 bytes.
4. Open the shared link in a private/incognito window while signed out and verify it can be accessed.
5. Set the access required by the brief (example: “Anyone with the link can edit”). This permission change must be performed or explicitly confirmed by the account owner.
6. Put only the shared-folder URL followed by a newline in `UCCD3084_johernlim.txt`.
7. Upload only that `.txt` file to Weble before the deadline; do not upload the project, APK, or video directly to Weble.
