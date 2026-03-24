# SPEC_005_ELGATO_MARKETPLACE: Elgato Marketplace Submission

> **Status:** NOT STARTED
> **Date:** 2026-03-23
> **Priority:** Low — only needed when plugin is stable and art is ready
> **Parent Spec:** `SPEC_005_STREAMDECK.md`

---

## 1. Overview

Publishing the dIKta.me Stream Deck plugin on the [Elgato Marketplace](https://marketplace.elgato.com/stream-deck/plugins) enables one-click installation for any Stream Deck user. Currently the plugin is sideloaded via folder copy. This spec covers everything needed to go from sideloaded to Marketplace-listed.

---

## 2. Prerequisites

### 2.1 Become a Maker

1. Go to the [Maker Console](https://maker.elgato.com) and sign in (or create an account)
2. Sign the **Maker Agreement** (required since December 2025 — must be signed by the organization's leader/owner)
3. Once approved, you can submit products

### 2.2 Install Stream Deck CLI

```bash
npm install -g @elgato/cli
```

Used for packaging (replaces the deprecated `DistributionTool.exe`).

---

## 3. What Needs to Change

### 3.1 Gap Analysis

| Item | Current State | Required for Marketplace |
|------|---------------|-------------------------|
| Plugin icon | 256x256 teal square | 288x288 branded PNG |
| Action icons | 72x72 colored squares | 20x20 + 40x40 white-on-transparent |
| Category icon | 28x28 teal square | 28x28 + 56x56 white-on-transparent |
| Key state icons | Solid color placeholders | Branded 72x72 + 144x144 PNGs |
| Preview images | None | At least 1 screenshot |
| manifest.json `SDKVersion` | 2 | 3 |
| manifest.json `Software.MinimumVersion` | 6.5 | 6.9 |
| Support URL | `https://dikta.me` | Dedicated support page (e.g., `https://dikta.me/streamdeck`) |
| `.sdignore` | None | Create to exclude dev files |
| Packaging | Manual folder copy | `.streamDeckPlugin` via CLI |
| Maker Agreement | Not signed | Required |

### 3.2 Manifest Changes

Update `src/DiktaMe.StreamDeck/manifest.json`:

```json
{
  "SDKVersion": 3,
  "Software": {
    "MinimumVersion": "6.9"
  },
  "Version": "1.0.0.0",
  "URL": "https://dikta.me/streamdeck"
}
```

**SDKVersion 3 compatibility note:** StreamDeck-Tools 6.4.0 uses SDKVersion 2. Options:
- Test if 6.4.0 works with SDKVersion 3 in the manifest (the underlying protocol is the same — it may just work)
- Upgrade to StreamDeck-Tools 7.0.0 (stable) which targets SDKVersion 3
- Worst case: migrate off StreamDeck-Tools to raw WebSocket (the protocol is documented)

---

## 4. Asset Requirements

### 4.1 Plugin Icon (Marketplace Listing)

- **Size:** 288 x 288 px PNG
- **Purpose:** Shown in the Marketplace listing and Stream Deck app plugin browser
- **Rules:** Must accurately represent what the plugin does. No copyright infringement, no offensive imagery.
- **Current:** 256x256 teal square — needs redesign to 288x288 with dIKta.me branding (mic + waveform or similar)

### 4.2 Action Icons (Sidebar List)

These appear in the Stream Deck app's action list when browsing the dIKta.me category.

- **Size:** 20 x 20 px (1x) and 40 x 40 px (2x)
- **Format:** SVG (recommended) or PNG
- **Style:** Monochromatic white (`#FFFFFF`) on transparent background
- **Rules:** No colored styling, no solid backgrounds

Need 2 icons:
| Action | Icon concept |
|--------|-------------|
| Pipeline Trigger | Mic or play triangle |
| Settings Toggle | Toggle switch or gear |

### 4.3 Category Icon

- **Size:** 28 x 28 px (1x) and 56 x 56 px (2x)
- **Format:** SVG (recommended) or PNG
- **Style:** Same white-on-transparent rules as action icons
- **Concept:** dIKta.me logo mark (simplified for small size)

### 4.4 Key Icons (Button State Images)

These appear on the physical Stream Deck buttons. **Can use color.**

- **Size:** 72 x 72 px (1x) and 144 x 144 px (2x)
- **Format:** SVG, PNG, or GIF

Need 6 key icons (already have placeholders, need branded versions):

| Icon | Current | Target |
|------|---------|--------|
| `trigger-idle` | Dark #1a1a2e square | Mic icon on dark background |
| `trigger-active` | Red #e74c3c square | Mic icon on red background (recording pulse) |
| `trigger-offline` | Grey #555555 square | Mic icon greyed out, disconnected indicator |
| `toggle-on` | Teal #00607a square | Toggle/switch icon in ON state |
| `toggle-off` | Dark #1a1a2e square | Toggle/switch icon in OFF state |
| `toggle-offline` | Grey #555555 square | Toggle icon greyed out |

### 4.5 Gallery / Preview Images

- **Minimum:** 1 preview image
- **Purpose:** Marketplace listing gallery — shows potential users what the plugin looks like in action
- **Placement:** `previews/` folder in the plugin directory
- **Suggestions:**
  1. Photo of Stream Deck Plus with dIKta.me buttons configured (real hardware)
  2. Screenshot of Property Inspector with pipeline dropdown open
  3. Side-by-side: Stream Deck button states (idle → recording → done)

---

## 5. Packaging

### 5.1 Create `.sdignore`

Create `src/DiktaMe.StreamDeck/.sdignore` to exclude dev files from the package:

```gitignore
# Dev files
*.pdb
*.xml
install-plugin.cmd
```

### 5.2 Build and Package

```bash
# Build the plugin
dotnet build src/DiktaMe.StreamDeck/DiktaMe.StreamDeck.csproj -c Release

# Package into .streamDeckPlugin installer
streamdeck pack src/DiktaMe.StreamDeck/bin/Release/me.dikta.streamdeck.sdPlugin
```

The CLI:
1. Validates the plugin and supporting files
2. Bundles the contents of the `.sdPlugin` directory
3. Outputs a `.streamDeckPlugin` installer file (double-click to install)

### 5.3 DRM

The Stream Deck CLI enables DRM by default for Marketplace plugins. DRM is supported for C#, C++, Go, and Node.js plugins — not limited to the JS SDK. This prevents unauthorized redistribution.

---

## 6. Submission Process

### 6.1 Submit via Maker Console

1. Log into [Maker Console](https://maker.elgato.com)
2. Upload the `.streamDeckPlugin` file
3. Fill in listing details:
   - **Title:** "dIKta.me"
   - **Description:** Voice dictation control — trigger pipelines, toggle settings, get visual feedback. Requires dIKta.me V2 desktop app.
   - **Category:** "Productivity" (or "Utilities")
   - **Support URL:** `https://dikta.me/streamdeck` (or GitHub issues)
   - **Preview images:** Upload gallery screenshots
4. Submit for review

### 6.2 Review Criteria

Elgato reviews for:
- **Quality:** No errors, bugs, or crashes
- **Performance:** Minimal CPU/memory impact (our plugin is lightweight — named pipe + JSON)
- **Safety:** Must not compromise user safety or device integrity
- **Audience:** Suitable for a broad audience
- **Guidelines:** Follows [Submission Guidelines](https://docs.elgato.com/guidelines/submissions/) and brand/vocabulary rules

The team may request changes before approval. Published products undergo periodic re-evaluation.

### 6.3 Post-Submission

- Updates are submitted through the same Maker Console
- Users with Marketplace accounts get automatic updates
- Sideloaded installations do NOT auto-update — users must manually update or switch to the Marketplace version

---

## 7. Revenue Model

- **Free plugins** — no fees to list, no fees for users
- **Paid plugins** — you set the price, Elgato takes a **30% commission**
- Legacy plugins (from the old Store) remain permanently free

**Recommendation:** List as free. dIKta.me is the product — the Stream Deck plugin is a companion tool. Making it free maximizes adoption and reduces support friction.

---

## 8. Content & Legal Requirements

From the [Submission Guidelines](https://docs.elgato.com/guidelines/submissions/):

- **Intellectual property:** Must own the rights and IP for all submitted content
- **Content policy:** No inappropriate, offensive, or discriminatory content. Must be suitable for a broad audience with child protections.
- **Accessibility:** Should be accessibility-friendly
- **Data collection:** If collecting data, requires explicit user consent and a privacy policy
- **Support:** Contact information must be accurate and kept up to date
- **Credit:** Provide appropriate credit for any inspiration or third-party assets

**Our status:** The plugin collects no user data, uses no third-party assets (icons will be original), and the dIKta.me brand/IP is owned by us. No blockers here.

---

## 9. Implementation Checklist

- [ ] Sign Maker Agreement at [maker.elgato.com](https://maker.elgato.com)
- [ ] Design and export branded plugin icon (288x288 PNG)
- [ ] Design and export action icons (20x20 + 40x40, white-on-transparent SVG/PNG)
- [ ] Design and export category icon (28x28 + 56x56, white-on-transparent SVG/PNG)
- [ ] Design and export key state icons (72x72 + 144x144, 6 variants)
- [ ] Create at least 1 preview/gallery image
- [ ] Update `manifest.json` (SDKVersion 3, MinimumVersion 6.9, support URL)
- [ ] Test plugin with updated manifest (verify SDKVersion 3 compatibility)
- [ ] Create `.sdignore` file
- [ ] Create support page at `dikta.me/streamdeck`
- [ ] Install `@elgato/cli` and run `streamdeck pack`
- [ ] Test the `.streamDeckPlugin` installer (double-click install, verify it works)
- [ ] Upload to Maker Console and fill in listing details
- [ ] Submit for review
- [ ] Address any review feedback

---

## 10. References

- [Maker Console (developer portal)](https://maker.elgato.com)
- [Elgato Marketplace](https://marketplace.elgato.com/stream-deck/plugins)
- [Submission Guidelines](https://docs.elgato.com/guidelines/submissions/)
- [Plugin Images & Layouts Guide](https://docs.elgato.com/guidelines/streamdeck/plugins/images-and-layouts/)
- [Distribution & Packaging Docs](https://docs.elgato.com/streamdeck/sdk/introduction/distribution/)
- [Marketplace FAQ](https://marketplace.elgato.com/learn/announcements/stream-deck-marketplace-questions-answered)
- [Stream Deck SDK Manifest Reference](https://docs.elgato.com/streamdeck/sdk/references/manifest/)
