
# Tutorial - Project Setup

We will start by creating a project that is set-up for DOTS.

- Create/Open a Unity project. Make sure it also uses either the URP or HDRP. 
- Import the `com.unity.charactercontroller` package from the package manager. This will take care of importing the `com.unity.entities` and `com.unity.physics` packages into your project, if not already present.
- Import the `com.unity.entities.graphics` package from the package manager.
- (RECOMMENDED) Go to `Edit > Project Settings > Editor`. Enable `Enter Play Mode Options`, and make sure the `Reload Domain` and `Reload Scene` underneath are both **disabled**. This will make entering play mode much faster

Note: For best performance in editor, pay attention to these settings:
* `Jobs > Burst > Enable Compilation` should be **enabled** in the top bar menu
* `Edit > Preferences > Jobs > Enable Jobs Debugger` should be **disabled**