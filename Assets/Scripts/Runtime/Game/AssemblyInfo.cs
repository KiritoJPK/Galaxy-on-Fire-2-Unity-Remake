// GoF2.Game: the game's state and rules (Session, Database, Story, the shop, freelance missions, standings, saves) and the
// mod loading (Modding/: manifests, content, materials, audio). Nothing here knows about scenes, networking or UI: what it
// needs from those layers comes through GameHooks, which they fill in at startup.
// Layers, lowest first: GoF2.Core <- GoF2.Game <- GoF2.Space <- GoF2.Net <- GoF2.App (Scripts/Runtime/README.md).
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("GoF2.Space")]
[assembly: InternalsVisibleTo("GoF2.Net")]
[assembly: InternalsVisibleTo("GoF2.App")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("GoF2.Tests")]
