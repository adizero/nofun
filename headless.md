## How to build nofun apk on a headless Linux server
For example on Ubuntu 22.04:
- install Unity Hub (non-snap version)
```bash
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
sudo apt update
sudo apt install unityhub
```
The nofun project is built with Unity (it has pinned a specific version of Unity Editor in `ProjectSettings/ProjectVersion.txt` - 2023.3.0a18)

- create account/login to Unity Hub
    In Unity Hub:
    - Installs → Install Editor
    - Pick 2023.3.0a18 (same version as the project)
    - In modules, check:
        - Android Build Support
            - Android SDK & NDK Tools
            - OpenJDK

### Build using headless_build.sh script
```bash
./headless_build.sh
```

The final `app.apk` is in `Builds/Android/app.apk`

### Build via Editor GUI (non-headless)
- Disable `Project Settings > Player > Android > Publishing Settings > Custom Keystore` if you do not have a keystore

- If sdkmanager is failing to list packages due to HTTP_PROXY/HTTPS_PROXY unset the variables in the sdkmanager shell script:
```bash
vi ~/Unity/Hub/Editor/2023.3.0a18/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmdline-tools/6.0/bin/sdkmanager 
# add the following lines to the end of the file before the last line: `exec "$JAVACMD" "$@"`
unset HTTP_PROXY
unset HTTPS_PROXY
unset NO_PROXY
export HTTP_PROXY
export HTTPS_PROXY
export NO_PROXY
```
- Build using the menu `File > Build Settings > Build`

## How to run nofun on an Android device
- Install app.apk on android device
- Do not use nofun app directly, instead open the decrypted .mpn file in a file manager/explorer using nofun as the "open with app"
