# Making mods with an AI assistant

`gof2-modding/` holds a guide written for AI assistants (Claude, ChatGPT, Gemini...): `SKILL.md` is the complete mod file
format with rules, balancing numbers and a checklist, and `reference.md` lists every original item, ship, star system and
station with its number and stats (the numbers mod files refer to). With both, an assistant can write a working mod for you.

## Claude

- **claude.ai (Skills):** zip the `gof2-modding` folder and upload it as a custom skill in claude.ai's settings. Then ask, for
  example: "Make a GoF2 Remake mod that adds a Valkyrie ship as a blueprint after the Supernova."
- **Claude Code:** copy the `gof2-modding` folder into `~/.claude/skills/` (or your project's `.claude/skills/`). Claude uses
  it by itself when you ask for a GoF2 mod.
- **Any chat:** attach `SKILL.md` and `reference.md` (or paste them) at the start of the conversation.

## ChatGPT and others

Attach `SKILL.md` and `reference.md` to the chat (or a custom GPT's knowledge, or a project's files), then say what the mod
should do. If the assistant can't read files, paste `SKILL.md` first and the part of `reference.md` you need.

## Tips

- Say which ships or items your new content should be like ("as strong as the Phantom", "a blaster like the K'booskk") and
  what you have: models (GLB), icons, card art, sounds. The assistant tells you the sizes and folders for the files it names.
- Copy the files it gives you into a folder named after the mod id, put it in the game's Mods folder, press **Refresh** in the
  Mods screen and turn it on. If the Mods screen shows an error or warning, paste it back to the assistant.
- Quests and bar missions are event graphs made in the Unity Editor; the assistant can plan them node by node for you.
