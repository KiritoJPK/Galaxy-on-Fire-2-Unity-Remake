// GoF2.Core: the remake's foundation, plain rules and utilities that use nothing else of the game (JavaRandom, the mining
// and hacking minigames, hit points, collision volumes, routes...). The layers above see its internals.
// Layers, lowest first: GoF2.Core <- GoF2.Game <- GoF2.Space <- GoF2.Net <- GoF2.App (Scripts/Runtime/README.md).
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("GoF2.Game")]
[assembly: InternalsVisibleTo("GoF2.Space")]
[assembly: InternalsVisibleTo("GoF2.Net")]
[assembly: InternalsVisibleTo("GoF2.App")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("GoF2.Tests")]
