# Team setup & troubleshooting

Everything here is a **one-time** setup on each teammate's machine. Do all four
steps before opening the project in Unity for the first time.

---

## 1. Install Git properly (not just GitHub Desktop)

**This is the fix for the "No 'git' executable was found" error.**

GitHub Desktop, Fork, Sourcetree and similar GUI clients ship their own private
copy of git that is *not* on the system PATH. That is enough to clone and pull,
but Unity shells out to a plain `git` command for some operations and finds
nothing — so the Package Manager fails to resolve and the project opens with
compile errors.

Install [Git for Windows](https://git-scm.com/download/win) and, on the "Adjusting
your PATH environment" page, keep the default **"Git from the command line and
also from 3rd-party software"**. Then restart Unity Hub *and* Unity — Unity only
reads PATH at launch, so a running Editor will keep showing the old error.

Verify (open a new terminal — an old one still has the stale PATH):

```bash
git --version
```

> The git-URL package that triggered this (`com.coplaydev.unity-mcp`) has been
> removed from `Packages/manifest.json` — it is a solo development bridge, not a
> game dependency, so nobody else needs it. Git itself is still required.

---

## 2. Install Git LFS

Art and audio are stored in Git LFS. Without it you get 130-byte text pointer
files where the PNGs should be, and Unity fills the project with pink sprites.

```bash
git lfs install
```

If you already cloned before installing LFS, fix the existing checkout with:

```bash
git lfs pull
```

---

## 3. Enable Unity's YAML merge driver

**This is the fix for "There are unresolved conflicts in the working directory".**

Scenes, prefabs and `.asset` files are YAML. Git's default line-based merge does
not understand them: it will happily "merge" two scenes into a file that is
valid text but a corrupt scene. `.gitattributes` routes these files to Unity's
own merge tool instead — but each machine has to point git at the executable
once.

Run this from any terminal (adjust the path if your Unity version differs):

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
```

```bash
git config --global merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

Check the path actually exists first:

```bash
ls "/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Data/Tools/UnityYAMLMerge.exe"
```

---

## 4. Close Unity before pulling

Unity rewrites scene, `.meta` and settings files while it is open — including
files you never touched. Pulling into a running Editor is the most common way to
create a conflict out of nothing, and Unity will also happily overwrite what git
just wrote. **Save, close Unity, pull, then reopen.**

---

## Recovering from a broken pull

If a client already says **"There are unresolved conflicts in the working
directory"**, the merge is stopped half-way and must be finished or abandoned.
Pick one:

### You have no local work worth keeping (the usual case)

Throws away everything local and takes the server's version exactly. **This
deletes your uncommitted changes** — check `git status` first if unsure.

```bash
git merge --abort
```

```bash
git fetch origin && git reset --hard origin/main && git clean -fd
```

### You do have local work worth keeping

Park it, take the clean upstream state, then replay your work on top:

```bash
git stash -u
```

```bash
git fetch origin && git reset --hard origin/main
```

```bash
git stash pop
```

### Only a scene or prefab is conflicted

Do not hand-edit the YAML. Take one side whole and redo the other side's change
in the Editor — it is faster and cannot corrupt the scene:

```bash
git checkout --theirs Assets/Scenes/Catacombs.unity && git add Assets/Scenes/Catacombs.unity
```

(`--theirs` = the version being pulled in; `--ours` = your local version.)

---

## Warning: never re-import the Unity URP template package

If Unity ever offers to import the "Universal 2D / URP sample" template content,
**decline it.** It writes its own `SampleScene.unity` straight over ours — same
path, new GUID — and drops `Assets/Settings/`, `Assets/TutorialInfo/`,
`Assets/Readme.asset`, `InputSystem_Actions` and `Assets/TextMesh Pro/` into the
project. This happened once on 2026-07-24 and reduced `SampleScene.unity` from
114 GameObjects to the template's 3 (Main Camera, Directional Light, Global
Volume). It was caught before it reached `main`, but only because the diff was
read line by line.

If it happens to you, restore the scene instead of re-building it:

```bash
git restore --staged --worktree Assets/Scenes/SampleScene.unity Assets/Scenes/SampleScene.unity.meta
```

Always restore the `.meta` alongside the scene — the template assigns a **new
GUID**, and leaving that in place silently detaches every reference to the scene.

---

## Why conflicts kept happening (fixed 2026-07-24)

Three separate causes, all now addressed in the repo:

| Symptom | Cause | Fix |
|---|---|---|
| `No 'git' executable was found` | `manifest.json` had a git-URL package; GUI clients keep git off PATH | Package removed from the manifest; install Git for Windows (step 1) |
| Conflicts in scenes nobody edited | `* text=auto` normalized Unity's LF files to CRLF on checkout, so simply opening the project produced a whole-file diff | Unity YAML files are now marked `-text` in `.gitattributes` |
| Merged scene loads broken | Line-based merging of YAML | `merge=unityyamlmerge` in `.gitattributes` + step 3 |

Unity template leftovers (`Assets/Settings/`, `Assets/TextMesh Pro/`,
`Assets/TutorialInfo/`, `Assets/Readme.asset`, `InputSystem_Actions`) are now
ignored as well. Every teammate's Unity regenerates them slightly differently,
and none of them is referenced by our code — the URP assets the game actually
uses live in `Assets/Resources/Assets/Rendering/`.
