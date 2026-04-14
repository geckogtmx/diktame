## Session Summary: 2026-04-14

### ✅ Completed
- Designed the "Bootstrap Strategy" for initial distribution via the Microsoft Store using Lemon Squeezy to retain 100% of revenue and gather emails.
- Architected the dual-build strategy using `.csproj` constants (`STORE_BUILD` / `STANDALONE_BUILD`) to easily toggle MSIX output and strip out Velopack for Store cloud submissions.
- Added strict documentation on Store metadata formatting traps (automated bullets) and Lemon Squeezy TOS compliance (evergreen rules).
- Detailed the MS Store App Tester requirements, explicitly mandating a 100% discount "Tester bypass" code be provided to avoid "Incomplete App" rejections.
- Included exact CLI publishing commands needed to generate the `.msixupload` to ensure PDBs exist for MS Partner Center crash analytics.
- Documented the critical local testing mandate using the **Windows App Certification Kit (WACK)** to prevent 3-day cloud rejection cycles.
- Analyzed the MSIX data virtualization (Black Hole) risks and devised a clean path for legacy MS Store users when switching to independent distribution later.

### 🚧 In Progress
- Implementation of the `StoreBuild` toggle pattern in `DiktaMe.App.csproj`.
- Implementing C# feature toggles `if (_licenseManager.IsLicensed)` to lock the Whisper/Ollama components.
- Designing the checkout routing system (`ikta.me/checkout`).
- Preparing graphical store assets and store metadata copy.

### 📋 Next Steps (Priority Order)
1. Update `DiktaMe.App.csproj` to support the dual-build architecture (`STORE_BUILD`) and single-project MSIX settings to produce `.msixupload`.
2. Wrap Velopack initialization in `STANDALONE_BUILD` preprocessor directives.
3. Ensure absolute local paths for SQLite/DPAPI so that data sits gracefully across both the free tier and standalone migrations.
4. Prepare the final MS Store assets and execute local WACK testing against the Store bundle.

### 🔍 Key Context
- **Files Modified:** `plans/SPEC_020_SIGNING.md`, `README.md`
- **Dependencies Added:** None this session.
- **Configuration Changes:** Add MSBuild conditions for `WindowsPackageType=MSIX`.
- **Known Issues:** The MSIX Virtualization Sandbox will intercept `AppData/Roaming`. If a user uninstalls the MSIX store version natively, Windows wipes their local data. Any forced standalone migration must be done with extreme care via email communication.

### 💡 Notes for Next Session
When building the `.msixupload`, ensure you use the exact CLI command provided in `SPEC_020_SIGNING.md` (which relies on `UapAppxPackageBuildMode=StoreUpload`) to guarantee symbol generation. Do NOT skip the local WACK test. Provide the Store testers with a Lemonsqueezy coupon specifically for them to bypass the $20 paywall so they don't reject the app.
