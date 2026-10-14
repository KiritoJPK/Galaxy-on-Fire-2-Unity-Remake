// AssemblyInfo.cs
// GoF2.Space: the flight and station levels (flight, combat, NPCs, the world, the mod models) and the helpers the screens
// above share. It knows nothing of multiplayer or the screens: SpaceHooks / UiHooks are their way in.
// Layers, lowest first: GoF2.Core <- GoF2.Game <- GoF2.Space <- GoF2.Net <- GoF2.App (Scripts/Runtime/README.md).

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GoF2.Net")]
[assembly: InternalsVisibleTo("GoF2.App")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("GoF2.Tests")]
