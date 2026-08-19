PROJECT_ROOT="${PWD}"

UNITY_EDITOR_DIR="$HOME/Unity/Hub/Editor/2023.3.0a18/Editor"
ANDROID_PLAYER_DIR="$UNITY_EDITOR_DIR/Data/PlaybackEngines/AndroidPlayer"

# In batch mode the editor does not always forward the embedded Android tool
# paths to external tools (Burst's bcl linker fails with "ANDROID_NDK_ROOT is
# undefined" when linking the ARMv7 slice), so point them at the tools that
# ship with the editor explicitly.
export ANDROID_SDK_ROOT="$ANDROID_PLAYER_DIR/SDK"
export ANDROID_NDK_ROOT="$ANDROID_PLAYER_DIR/NDK"
export ANDROID_NDK_HOME="$ANDROID_NDK_ROOT"
export JAVA_HOME="$ANDROID_PLAYER_DIR/OpenJDK"

"$UNITY_EDITOR_DIR/Unity" \
    -batchmode -nographics -quit \
    -projectPath "${PROJECT_ROOT}" \
    -executeMethod Nofun.CommandLineBuild.BuildAndroidApk \
    -customBuildPath "${PROJECT_ROOT}/Builds/Android/app.apk" \
    -logFile "${PROJECT_ROOT}/build-android.log"
