# Direct Companion Interactions (`CompanionRoleplay.dll`)

## ✨ Key Features
* **Context Action Menu:** Approach any companion within arm's reach and press **`G`** (configurable) to open a native-style interaction menu.
* **Carry Mechanics:** Pick up companions and carry them around the castle.
  * Automatically switches the player's movement to a natural walking pace and disables sprinting while carrying.
  * Supports custom upper-body poses/animations for the carrier and full-body poses/animations for the carried companion (supports both native game `ES2` pose files and multi-keyframe `.json` animations).
  * Press **`G`** again at any time to gently set the companion down.
* **Seamless Furniture Placement:** Walk up to any interactive furniture (pianos, beds, chairs, dance poles, etc.) while carrying a companion and press **`F`** to interact with it to place her directly onto the furniture and open the pose selection UI.

## 📦 Installation

1. Ensure **BepInEx 5.x** is installed in your game directory.
2. Extract the `.dll` files into your `BepInEx\plugins\` folder:
   ```text
   BepInEx/
   └── plugins/
       └── CompanionRoleplay/
           ├── CompanionRoleplay.dll
           └── Poses/
               ├── Carrier_Default.txt (or .json)
               └── Carried_Default.txt (or .json)
   ```
3. Launch the game once to generate default configuration files in `BepInEx\config\`.

## ⚙️ Configuration (`com.user.companionroleplay.cfg`)

* **`InteractionKey`** *(Default: `G`)* — Hotkey to open the companion interaction menu or put down a carried companion.
* **`GrabDistance`** *(Default: `2.0`)* — Maximum interaction distance in meters.
* **`LocalPositionOffset` / `LocalRotationOffset`** — Fine-tune the carried companion's relative alignment in real time (compatible with BepInEx Configuration Manager).

## 🗺️ To-Do & Roadmap

### Short-Term / Polishing
- [ ] **Fine tune the default poses in Free Pose:** 
- [ ] **Test JSON animation instead of a ES2 pose:** 
- [ ] **Megamorph: fix changing offset when carried by a person with scaled up/down body:** 
- [ ] **When Target is already interacting with furniture "Grab" option should change to "Release"** 
- [ ] **Carry Pose Selector Submenu:** Add a secondary selection menu when choosing `Carry` to switch between multiple carrying styles (over-the-shoulder, princess carry, etc.).

### Short-Term / Polishing
- [ ] **Fine-tune Default Poses in Free Pose:** Adjust shoulder, arm, neck and head alignment and torso angle in the in-game pose editor for seamless clipping-free carrying.
- [ ] **Multi-Keyframe JSON Animations:** Test and add dynamic `.json` carrying animations (swaying or struggling) alongside static `ES2` poses.
- [ ] **Megamorph Scale Compensation:** Dynamically adjust carry position offsets based on the carrier's body scale so characters with scaled-up or scaled-down proportions hold companions accurately.
- [ ] **Context-Aware "Release" Option:** Change the `Carry` / `Grab` menu button to `Release` when targeting a companion currently interacting with furniture to cleanly detach her from the object.
- [ ] **Carry Pose Selector Submenu:** Add a secondary selection menu when choosing `Carry` to switch between multiple carrying styles (over-the-shoulder, princess carry, etc.).

### Roleplay & Emotion Expansion
- [ ] **Carry Emotional Feedback:** Integrate `CompanionRoleplay` with `CompanionEmotionsMod` so picking up or carrying a companion dynamically affects embarrassment, trust, or arousal stats—and selects appropriate carried poses based on relationship traits.
- [ ] **Expanded Interaction Menu (`G`):**
  - [ ] Affectionate actions (*Kiss*, *Give Gift*, *Dialogue*).
  - [ ] Restraint & leading mechanics (*Lead by Hand / Leash*, *Bondage / Tie Up*).

## 🤝 Compatibility
* Compatible with **CompanionInteraction.dll** (`Attach` functionality works alongside `Carry`).
* Safe to add to existing saves.
