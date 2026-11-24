# Teamers edition

The Windows app is a source reconstruction of the 2.5.1 ChestCounter build used for Teamers. The existing TooManyChest release remains available from the repository's Releases page. The Teamers app connects to the public Teamers site listed in the main README. Linking is optional and uses a one-time browser code approved in the app.

The Teamers build uses the replay parser from [ChestCounter v2.5.1](https://github.com/2xFlaw/ChestCounter/tree/v2.5.1/lib) in `lib-teamers/`. Font notices are in `src/TeamersDesktop/Notices/`.

## Build the Windows app

Run from the repository root on Windows with the .NET 10 SDK installed.

```powershell
dotnet publish src/TeamersDesktop/ChestCounter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist-teamers
```

Run `dist-teamers/ChestCounter.exe`. The app reads Fortnite replay files locally. When linked to Teamers it sends the profile name, a hashed player identifier, and the counts and match identifiers needed to show statistics and confirm shared matches. Replay files stay on the PC.

## Build the site

From this directory run `node build.mjs`. The output is `dist/server/index.js`. The site expects a D1 database bound as `DB`. Use the Sites publishing workflow to assign your hosting project and apply the migrations in `drizzle/`.

Edit `teamers.html` for static page text and `styles.css` for appearance. Browser behavior lives in `client.js` and `charts.js`. The remaining modules handle requests, pairing, teams, and statistics.
