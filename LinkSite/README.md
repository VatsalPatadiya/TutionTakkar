# Invite link site

Serves `https://penfighter-6be8b.web.app/join/<CODE>` (room invites) and the
Android App Links file `/.well-known/assetlinks.json`.

Deploy (from this folder, once logged in):

    npx firebase-tools login
    npx firebase-tools deploy --only hosting

`assetlinks.json` holds the SHA-256 of the release keystore. If the app is
published on Google Play with Play App Signing, add the SHA-256 from
Play Console → Setup → App signing as a second fingerprint.
