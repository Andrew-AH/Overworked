# Rebuild and reinstall after every confirmed change

Andrew uses the installed Overworked app and `ow` on this Mac. A change isn't finished until it's running there too.

Once a change has been made and confirmed (the tests pass, and Andrew has agreed to it or asked for it to be committed), rebuild and reinstall before saying it's done. Do it even if he doesn't ask. Run these from the repo root.

## The app (any change under `src/`)

```bash
./mac/package.sh
osascript -e 'quit app "Overworked"'   # may print "User cancelled (-128)" yet still quit; check with pgrep
pgrep -fl "Overworked.app/Contents/MacOS"   # must print nothing before the next step
rm -rf /Applications/Overworked.app && ditto publish/mac/Overworked.app /Applications/Overworked.app
open /Applications/Overworked.app
```

## The `ow` command (any change under `src/Overworked.Core` or `src/Overworked.Cli`)

The version number doesn't change between releases, so remove the old copy and NuGet's cached package first. Otherwise the reinstall uses the stale build.

```bash
dotnet pack src/Overworked.Cli -c Release -o nupkg
dotnet tool uninstall -g Overworked.Cli
rm -rf ~/.nuget/packages/overworked.cli
dotnet tool install -g Overworked.Cli --add-source ./nupkg
```

## Then

Tell Andrew what was reinstalled. If a step failed, give him the error and don't call the change done.

This doesn't publish anything. A GitHub release (new version and a `v*` tag) only happens when Andrew asks for one.
