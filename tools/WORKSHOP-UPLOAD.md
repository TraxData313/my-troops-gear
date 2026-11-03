# Uploading to the Steam Workshop

Same uploader and quirks as the sibling mods (see better-skirmisher-separation's `tools\WORKSHOP-UPLOAD.md`):
Bannerlord's own `TaleWorlds.MountAndBlade.SteamWorkshop.exe`, riding the logged-in Steam client.

Item: **not created yet.** (Account note: since 2026-10-10 new uploads from this account go hidden
"awaiting approval" - see `MODS-STATUS.md`, "The Steam block".)

## First upload (creates the item - once only)

```powershell
powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Force
& "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Asus ROG\Documents\BannerlordMods\my-troops-gear\tools\WorkshopCreate.xml"
```

Run it from a scratch folder. Success = "Uploading done!" in the output, never the exit code. Then:

1. Put the new item ID into `WorkshopUpdate.xml` (replace `ITEM_ID`) and here, with the page link, and
   mark `WorkshopCreate.xml` "never run again".
2. Send the full description right away (the create task only has a short one):
   `python tools\make_workshop_update.py --notes "v1.0.0 - first release."` and run the uploader on the printed path.

## Updating

```powershell
powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Force
$task = python tools\make_workshop_update.py --notes "vX.Y.Z - what changed."
& "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" $task
```

Run it from a scratch folder. Success = "Uploading done!" in the output, never the exit code.
The script sends `dist\MyTroopsGear`, `STEAM-DESCRIPTION.bbcode` and the change notes (`--visibility` optional).

## By hand on the item page

The uploader cannot: add Harmony (2859188632) and UIExtenderEx (2859222409) under "Add/Remove Required
Items", and the screenshots in `docs\screenshots` under "Add/edit images & videos" (in their numbered order).
