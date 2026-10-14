# Netcode Roslyn source generators

This file has our roslyn source generators. These are built and tested outside of the editor.

All these commands are to be run from the unity/unity root.

## Build the generators

For debugging, run

```bash
./dotnet.sh publish -c Debug "Workspace/com.unity.netcode/Runtime/SourceGenerators/Source~"
```

For the final build, run

```bash
./dotnet.sh publish -c Release "Workspace/com.unity.netcode/Runtime/SourceGenerators/Source~"
```

## Testing the generators

```bash
./dotnet.sh test "Workspace/com.unity.netcode/Runtime/SourceGenerators/Source~"
```

## Deep dive docs

Refer to [the internal source generator documentation](./Source~/Documentation/README.md) for deep dive information.
