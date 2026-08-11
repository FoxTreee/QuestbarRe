# Questbar Inspector Help

This editor-only plugin displays Questbar-specific explanations beneath exported C# properties in Godot's Inspector. It does not change property values, scenes, resources, runtime behavior, or exported-game logic.

## Enable

1. Open **Project > Project Settings > Plugins**.
2. Set **Questbar Inspector Help** to **Enabled**.
3. Select a Questbar node or resource that uses a documented C# script.

The plugin currently contains descriptions for all 267 exported properties found in the documented project snapshot. The descriptions live in `property_descriptions.json`, keyed by C# script path and exact exported property name.

If a C# export is renamed or moved to another script, update the matching JSON key. If a new export is added, add its description to the JSON file so the help panel appears beneath it.
