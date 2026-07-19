# GameTemplate

Starter Unity project extracted from the FengShui codebase. Unity **2022.3.53f1**, URP.

## What's inside

- **Core** (`Assets/Modules/Shared/Core`) — MVVM UI system (View/ViewModel/ViewFactory, view animations, V* UI components), services (Save, Screen, VideoSettings, Camera, Time, Wallet, Crash reports, Unity Analytics), editor toolbag, Addressables-based view loading.
- **Audio abstraction** (`Assets/Modules/Shared/Core/Audio`) — `IAudioService` with a no-op `NullAudioService` bound by `AudioInstaller`. UI sound hooks (button clicks, popup show/hide) stay functional; plug in FMOD/Wwise/Unity Audio by binding a real implementation.
- **Input** (`Assets/Modules/Shared/Game/Input`) — Input System wrapper (`InputService`), rebinding (`InputRebindingService`, `RebindCatalog`), gamepad cursor support, custom hardware cursor with hover states.
- **Views** — SettingsView (audio/video/controls/game tabs, key rebinding UI), RebindingPopup, ConfirmationPopup, AnalyticsConsent. Registered in the `Views` Addressables group.
- **Localization** — Unity Localization package setup with locales, string tables and TMP font styles/fallbacks (CJK, Cyrillic, Thai fonts included).
- **Scene transitions** — `SceneTransition` (TransitionsPlus + PrimeTween), wired in `ProjectContext`.
- **Bootstrap** — Zenject `ProjectContext.prefab` in `Assets/Resources` (SceneTransition, SignalBus, UnityAnalyticsService, Steamworks init, save service, screen service, null audio).
- **Plugins** — Zenject, UniRx, PrimeTween, TextMesh Pro resources, EditorJunkie (SearchableEnum/SceneReference/QuickButtons), ConsolePro, vTools (vFavorites/vFolders/vHierarchy/vInspector/vRuler/vTabs).

## First steps in a new game

1. Open in Unity 2022.3.53f1 and create your first scene (none are included).
2. Set product/company name in Player Settings (currently `GameTemplate`).
3. Link your own Unity Cloud project id (cleared on purpose) if you use Analytics/Cloud Diagnostics.
4. Update `GlobalData` (`Assets/Modules/Shared/Game/Utils`) — Steam app id, URLs, layer masks.
5. Add your views: create a View + ViewModel + ViewDefinition, add an entry to `ViewName`, handle it in `ViewDefaultDefinitionFactory`, register the prefab in the `Views` Addressables group.

## Intentionally excluded

Wwise (replaced by `NullAudioService`), Discord SDK, FogOfWar (removed from URP renderers), FlatKit, all FengShui gameplay modules (Map, Build/furniture, Characters, Dialogue, levels).
