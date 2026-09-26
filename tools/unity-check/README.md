# unity-check

Compile-only check of `client/Assets` without a Unity license. It catches C#
errors (wrong API, missing using, C# > 9) before CI does. It does not run
Unity, import assets, or compile shaders.

```bash
export UNITY_DATA=/path/to/Unity/Editor/Data   # e.g. extracted from unityci/editor:ubuntu-6000.3.25f1-base-3
dotnet build tools/unity-check/Game      # runtime, native defines
dotnet build tools/unity-check/GameWeb   # runtime, UNITY_WEBGL
dotnet build tools/unity-check/Editor    # runtime + editor + tests
```

Code that depends on packages from the Unity registry (URP) is not covered.
